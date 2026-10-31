// <copyright file="ToolDispatcher.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Microsoft.Xrm.Sdk;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Runs every tool call: checks in the order the spec sets (reason first for Write and Publisher tools, R12),
    /// routes by <c>connection</c> (C7), queues per connection, asks for approval in the bound tab (R2, SPEC §7),
    /// runs the tool on a background thread, and writes the call's line pair to the right log (R13, C10).
    /// </summary>
    internal sealed class ToolDispatcher : IToolHost
    {
        private readonly CompanionHost host;
        private readonly IClock clock;
        private readonly IReadOnlyList<ToolDefinition> tools;
        private readonly Dictionary<string, ToolDefinition> byName;

        /// <summary>Initializes a new instance of the <see cref="ToolDispatcher"/> class.</summary>
        /// <param name="host">The host.</param>
        /// <param name="tools">The tools, in listing order.</param>
        /// <param name="clock">The clock for receipt times and durations.</param>
        public ToolDispatcher(CompanionHost host, IReadOnlyList<ToolDefinition> tools, IClock clock)
        {
            this.host = host;
            this.tools = tools;
            this.clock = clock;
            this.byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        }

        /// <summary>Gets the tools, in listing order.</summary>
        public IReadOnlyList<ToolDefinition> Tools => this.tools;

        /// <inheritdoc/>
        public JArray ListTools() => new JArray(this.tools.Select(t => t.ToListEntry()));

        /// <inheritdoc/>
        public bool HasTool(string name) => this.byName.ContainsKey(name);

        /// <inheritdoc/>
        public async Task<ToolResult> CallAsync(McpToolCall call, CancellationToken cancellation)
        {
            var tool = this.byName[call.Name];
            var state = new CallState(call, tool, this.host.RunningToken, cancellation);
            if (tool.Tier != ToolTier.Host && call.Arguments?[InputSchema.Connection]?.Type == JTokenType.String)
            {
                state.Binding = this.host.Registry.Find((string)call.Arguments[InputSchema.Connection]!);
            }

            ToolResult result;
            try
            {
                result = ToolResult.Ok(await this.RunAsync(state).ConfigureAwait(false));
            }
            catch (ToolException ex)
            {
                result = ToolResult.Fail(ex.Code, ex.Message, ex.Details);
            }
            catch (Exception ex)
            {
                // Every call is logged (R13), including one that fails in an unexpected way.
                var error = DataverseError(ex);
                result = ToolResult.Fail(error.Code, error.Message, error.Details);
            }

            this.Log(state, result);
            return result;
        }

        private static ToolException DataverseError(Exception ex) => ToolException.FromDataverse(ex);

        private static ToolException Stopping() =>
            new ToolException(ToolErrorCodes.ServerStopping, "The host is stopping; the call did not run. Wait for the person.");

        private static ApprovalDecision[] OptionsFor(ToolTier tier) => tier == ToolTier.Read
            ? new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment }
            : new[] { ApprovalDecision.AllowOnce };

        private static async Task<PreparedCall> PrepareAsync(ToolDefinition tool, ToolExecution execution)
        {
            try
            {
                return await Task.Run(() => tool.Prepare!(execution)).ConfigureAwait(false);
            }
            catch (ToolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw DataverseError(ex);
            }
        }

        private async Task<JObject> RunAsync(CallState state)
        {
            var tool = state.Tool;
            var arguments = state.Call.Arguments;
            if (state.HostRun.IsCancellationRequested)
            {
                throw Stopping();
            }

            if (tool.Tier == ToolTier.Write || tool.Tier == ToolTier.Publisher)
            {
                ArgumentValidator.RequireReason(arguments);
            }

            ArgumentValidator.Validate(tool, arguments);
            if (tool.Tier == ToolTier.Host)
            {
                state.Approval = "none";
                return tool.RunHost!();
            }

            var binding = state.Binding ?? throw new ToolException(
                ToolErrorCodes.ConnectionNotFound,
                "No plugin tab is bound to that connection. Call list_connections, and ask the person to connect a tab if it is not listed.",
                new JObject { ["connection"] = arguments![InputSchema.Connection] });

            Pager? continuation = null;
            var cursor = arguments![InputSchema.Cursor]?.Type == JTokenType.String ? (string)arguments[InputSchema.Cursor]! : null;
            if (cursor != null)
            {
                continuation = this.host.Cursors.Find(cursor, binding.ConnectionId, tool.Name)
                    ?? throw new ToolException(ToolErrorCodes.InvalidCursor, "The cursor is unknown, expired, or belongs to another connection or tool. Start again from the first page.");
            }
            else
            {
                this.CheckGates(tool, binding, arguments);
                state.Inspection = tool.Inspect?.Invoke(binding, arguments) ?? CallInspection.None;
            }

            binding.BeginPending();
            var pendingEnded = false;
            using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(state.ClientGone, binding.Ended, state.HostRun))
            {
                try
                {
                    try
                    {
                        await binding.Queue.WaitAsync(waiting.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw this.Classify(state, binding);
                    }

                    try
                    {
                        if (continuation == null && tool.Prepare != null)
                        {
                            // Resolution runs in the queue, before the dialog, so the dialog shows names (SPEC §6.6).
                            state.Prepared = await PrepareAsync(tool, new ToolExecution(this.host, binding, arguments)).ConfigureAwait(false);
                        }

                        state.Approval = continuation != null ? "page" : await this.AuthorizeAsync(state, binding, waiting.Token).ConfigureAwait(false);
                        binding.EndPending();
                        pendingEnded = true;
                        binding.BeginRunning();
                        try
                        {
                            return await this.ExecuteAsync(tool, binding, arguments, continuation, cursor, state.Prepared).ConfigureAwait(false);
                        }
                        finally
                        {
                            binding.EndRunning();
                        }
                    }
                    finally
                    {
                        binding.Queue.Release();
                    }
                }
                finally
                {
                    if (!pendingEnded)
                    {
                        binding.EndPending();
                    }
                }
            }
        }

        private void CheckGates(ToolDefinition tool, BoundConnection binding, JObject arguments)
        {
            if (tool.IsPaged && !tool.FixedPageSize && arguments[InputSchema.PageSize] != null)
            {
                var maxPageSize = this.host.Settings.Read(s => s.MaxPageSize);
                if ((long)arguments[InputSchema.PageSize]! > maxPageSize)
                {
                    throw ToolException.InvalidArgument(InputSchema.PageSize, $"pageSize must be at most {maxPageSize}, the host's maximum page size.");
                }
            }

            if (tool.Tier == ToolTier.Publisher && !binding.PublisherCreationEnabled)
            {
                throw new ToolException(ToolErrorCodes.ToolDisabled, "Publisher creation is switched off for this connection. Ask the person.");
            }

            if ((tool.Tier == ToolTier.Write || tool.Tier == ToolTier.Publisher) && this.host.DevEnvironmentScope(binding) == null)
            {
                throw new ToolException(ToolErrorCodes.DevEnvironmentNotConfirmed, "This connection is not confirmed as a development environment. Ask the person to confirm it in its Agent Companion tab.");
            }
        }

        private async Task<string> AuthorizeAsync(CallState state, BoundConnection binding, CancellationToken waiting)
        {
            var tool = state.Tool;
            if (tool.Tier == ToolTier.Read)
            {
                var grant = this.host.Approvals.Find(ApprovalKind.ReadApproval, binding.OrganizationId);
                if (grant != null)
                {
                    return grant.Scope.ToLabel();
                }
            }

            var decision = await this.PromptAsync(state, binding, waiting).ConfigureAwait(false);
            switch (decision)
            {
                case ApprovalDecision.AllowSession:
                    this.host.Approvals.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, binding.OrganizationId, binding.OrganizationFriendlyName, binding.Owner.TabLabel);
                    return ApprovalScope.Session.ToLabel();
                case ApprovalDecision.AllowEnvironment:
                    this.host.Approvals.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, binding.OrganizationId, binding.OrganizationFriendlyName, binding.Owner.TabLabel);
                    return ApprovalScope.Environment.ToLabel();
                case ApprovalDecision.AllowOnce:
                    return ApprovalScope.Once.ToLabel();
                default:
                    throw new ToolException(ToolErrorCodes.DeniedByUser, "The person denied the call. Do not retry it without new instructions.");
            }
        }

        private async Task<ApprovalDecision> PromptAsync(CallState state, BoundConnection binding, CancellationToken waiting)
        {
            var timeout = TimeSpan.FromSeconds(this.host.Settings.Read(s => s.ApprovalTimeoutSeconds));
            var arguments = (JObject)state.Call.Arguments!.DeepClone();
            arguments.Remove(InputSchema.Reason);
            var devConfirmed = this.host.DevEnvironmentScope(binding) != null;
            var request = new ApprovalRequest
            {
                Tool = state.Tool.Name,
                Tier = state.Tool.Tier,
                ConnectionName = binding.ConnectionName,
                OrganizationName = binding.OrganizationFriendlyName,
                Arguments = arguments.ToString(Formatting.Indented),
                Reason = state.Call.Arguments[InputSchema.Reason]?.Type == JTokenType.String ? (string)state.Call.Arguments[InputSchema.Reason]! : null,
                TouchesRecords = state.Inspection.TouchesRecords,
                Summary = state.Prepared?.Summary ?? string.Empty,
                Confirmation = state.Prepared?.Confirmation,
                Recommendation = state.Tool.Tier != ToolTier.Read
                    ? string.Empty
                    : devConfirmed
                        ? "Recommended for a development environment: Allow for this environment."
                        : "Not confirmed as development — recommended: Allow for this session.",
                Options = OptionsFor(state.Tool.Tier),
                DeadlineUtc = this.clock.UtcNow + timeout,
            };

            using (var timer = new CancellationTokenSource(timeout))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(waiting, timer.Token))
            {
                try
                {
                    return await binding.Owner.PromptAsync(request, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timer.IsCancellationRequested && !waiting.IsCancellationRequested)
                {
                    throw new ToolException(ToolErrorCodes.DeniedNoResponse, $"Nobody answered the approval within {timeout.TotalSeconds:0} seconds. Do not retry without new instructions.");
                }
                catch (OperationCanceledException)
                {
                    throw this.Classify(state, binding);
                }
            }
        }

        private async Task<JObject> ExecuteAsync(ToolDefinition tool, BoundConnection binding, JObject arguments, Pager? continuation, string? cursor, PreparedCall? prepared)
        {
            var execution = new ToolExecution(this.host, binding, arguments, prepared);
            try
            {
                if (!tool.IsPaged)
                {
                    return await Task.Run(() => tool.Run!(execution)).ConfigureAwait(false);
                }

                var pager = continuation ?? new Pager(
                    await Task.Run(() => tool.OpenPages!(execution)).ConfigureAwait(false),
                    arguments[InputSchema.PageSize] != null && !tool.FixedPageSize ? (int)arguments[InputSchema.PageSize]! : tool.DefaultPageSize!.Value);
                var firstPage = pager.PagesTaken == 0;
                var fields = pager.Source as IPageFields;
                JObject Build(JArray page, string? next) => tool.BuildPage!(page, next, fields, firstPage);
                var items = await pager.NextPageAsync(this.host.Settings.Read(s => s.ResponseCapCharacters), Build).ConfigureAwait(false);

                string? next = null;
                if (pager.HasMore)
                {
                    next = cursor ?? this.host.Cursors.Open(binding.ConnectionId, tool.Name, pager);
                }
                else if (cursor != null)
                {
                    this.host.Cursors.Close(cursor);
                }

                return Build(items, next);
            }
            catch (ToolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw DataverseError(ex);
            }
        }

        private ToolException Classify(CallState state, BoundConnection binding)
        {
            if (state.HostRun.IsCancellationRequested)
            {
                return Stopping();
            }

            if (binding.Ended.IsCancellationRequested)
            {
                return new ToolException(ToolErrorCodes.ConnectionClosed, "The tab closed or changed connection before the call ran. Call list_connections and ask the person.");
            }

            return new ToolException(ToolErrorCodes.CancelledByClient, "The client closed the request before the call ran.");
        }

        private void Log(CallState state, ToolResult result)
        {
            var call = state.Call;
            var duration = this.clock.UtcNow - call.ReceivedUtc;
            var receivedLocal = call.ReceivedUtc.ToLocalTime();
            var callLine = CallLogFormatter.CallLine(receivedLocal, call.Name, call.Arguments, state.Tool.InputOrder);
            var outcomeLine = result.IsError
                ? CallLogFormatter.OutcomeError((string)result.Payload["error"]!["code"]!, state.Approval, duration, (string)result.Payload["error"]!["message"]!)
                : CallLogFormatter.OutcomeOk(state.Approval, duration, result.Payload);

            var entry = new CallLogEntry(
                receivedLocal,
                call.Name,
                state.Tool.Tier.ToString().ToUpperInvariant(),
                state.Approval,
                callLine,
                outcomeLine,
                !result.IsError && state.Tool.Warnings != null ? state.Tool.Warnings(result.Payload) : Array.Empty<string>(),
                call.Arguments?.ToString(Formatting.Indented) ?? "(arguments were not a JSON object)",
                result.Payload.ToString(Formatting.Indented));

            if (state.Binding != null)
            {
                state.Binding.Owner.CallLog.Add(entry);
            }
            else
            {
                this.host.LogHostCall(entry);
            }
        }

        /// <summary>What the dispatcher knows about one call as it runs.</summary>
        private sealed class CallState
        {
            public CallState(McpToolCall call, ToolDefinition tool, CancellationToken hostRun, CancellationToken clientGone)
            {
                this.Call = call;
                this.Tool = tool;
                this.HostRun = hostRun;
                this.ClientGone = clientGone;
            }

            public McpToolCall Call { get; }

            public ToolDefinition Tool { get; }

            public CancellationToken HostRun { get; }

            public CancellationToken ClientGone { get; }

            public BoundConnection? Binding { get; set; }

            public string Approval { get; set; } = CallLogFormatter.NotRun;

            public CallInspection Inspection { get; set; } = CallInspection.None;

            public PreparedCall? Prepared { get; set; }
        }
    }
}
