// <copyright file="CompanionHost.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

    /// <summary>
    /// The MCP host — listener, token, routing and connection registry — one per XrmToolBox process (SPEC §5).
    /// Every tab attaches to <see cref="Shared"/>; the first attached tab starts it, the last detached stops it (H1–H3).
    /// The object itself lives for the process, so its state survives a stop and restart; session-scoped grants,
    /// handshake sessions and cursors do not.
    /// </summary>
    internal sealed class CompanionHost
    {
        private static readonly Lazy<CompanionHost> SharedHost = new Lazy<CompanionHost>(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly object gate = new object();
        private readonly HashSet<IHostInstance> instances = new HashSet<IHostInstance>();
        private readonly Func<int, McpProtocol, IMcpServer> serverFactory;
        private readonly IClock clock;
        private volatile CancellationTokenSource running = new CancellationTokenSource();
        private IMcpServer? server;
        private McpProtocol? protocol;

        /// <summary>Initializes a new instance of the <see cref="CompanionHost"/> class.</summary>
        /// <param name="settings">The persisted settings.</param>
        /// <param name="protector">Encrypts the bearer token at rest.</param>
        /// <param name="clock">The clock.</param>
        /// <param name="serverFactory">Creates the listener for a port.</param>
        /// <param name="version">The plugin version.</param>
        /// <param name="tools">Creates the tool list; defaults to <see cref="ToolCatalog.Create"/>.</param>
        public CompanionHost(HostSettingsStore settings, ITokenProtector protector, IClock clock, Func<int, McpProtocol, IMcpServer> serverFactory, string version, Func<CompanionHost, IReadOnlyList<ToolDefinition>>? tools = null)
        {
            this.Settings = settings;
            this.clock = clock;
            this.serverFactory = serverFactory;
            this.Version = version;
            this.Tokens = new TokenStore(settings, protector);
            this.Approvals = new ApprovalStore(settings, clock);
            this.Registry = new ConnectionRegistry();
            this.Cursors = new CursorStore(clock);
            this.Dispatcher = new ToolDispatcher(this, (tools ?? ToolCatalog.Create)(this), clock);
            this.Notice = this.Tokens.Load();
            this.running.Cancel();
            this.Registry.Changed += (s, e) => this.OnStateChanged();
            this.Approvals.Changed += (s, e) => this.OnStateChanged();
        }

        /// <summary>Raised when the state, port or bound connections change.</summary>
        public event EventHandler? StateChanged;

        /// <summary>Raised for every host-log entry (C10); each tab keeps its own copy.</summary>
        public event EventHandler<CallLogEntry>? HostCallLogged;

        /// <summary>Raised when a notice should be shown on every tab.</summary>
        public event EventHandler<string>? NoticeRaised;

        /// <summary>Gets the process-wide host.</summary>
        public static CompanionHost Shared => SharedHost.Value;

        /// <summary>Gets the plugin version.</summary>
        public string Version { get; }

        /// <summary>Gets the persisted settings.</summary>
        public HostSettingsStore Settings { get; }

        /// <summary>Gets the bearer token store.</summary>
        public TokenStore Tokens { get; }

        /// <summary>Gets the approval store.</summary>
        public ApprovalStore Approvals { get; }

        /// <summary>Gets the connection registry.</summary>
        public ConnectionRegistry Registry { get; }

        /// <summary>Gets the paging cursors.</summary>
        public CursorStore Cursors { get; }

        /// <summary>Gets the tool dispatcher.</summary>
        public ToolDispatcher Dispatcher { get; }

        /// <summary>Gets the latest notice for every tab, or <c>null</c>.</summary>
        public string? Notice { get; private set; }

        /// <summary>Gets the listening state.</summary>
        public HostState State { get; private set; } = HostState.Stopped;

        /// <summary>Gets why the host could not start, or <c>null</c>.</summary>
        public string? StartError { get; private set; }

        /// <summary>Gets the configured port.</summary>
        public int Port => this.Settings.Read(s => s.Port);

        /// <summary>Gets the endpoint URL clients use.</summary>
        public string Endpoint => $"http://127.0.0.1:{this.Port}/mcp";

        /// <summary>Gets a token cancelled when the current run of the host stops. Read without the lock, so calls arriving while a stop drains do not wait.</summary>
        public CancellationToken RunningToken => this.running.Token;

        /// <summary>Gets the number of attached tabs.</summary>
        public int InstanceCount
        {
            get
            {
                lock (this.gate)
                {
                    return this.instances.Count;
                }
            }
        }

        /// <summary>Attaches a tab; the first tab starts the host (H1, H2). Safe to call more than once per tab.</summary>
        /// <param name="instance">The tab.</param>
        public void Attach(IHostInstance instance)
        {
            lock (this.gate)
            {
                if (!this.instances.Add(instance) || this.instances.Count > 1)
                {
                    return;
                }

                this.StartCore();
            }

            this.OnStateChanged();
        }

        /// <summary>Detaches a tab, ending its binding; the last tab stops the host (C6, H3).</summary>
        /// <param name="instance">The tab.</param>
        public void Detach(IHostInstance instance)
        {
            this.ReleaseBinding(instance);
            lock (this.gate)
            {
                if (!this.instances.Remove(instance) || this.instances.Count > 0)
                {
                    return;
                }

                this.StopCore();
            }

            this.OnStateChanged();
        }

        /// <summary>Ends a tab's binding: its pending calls fail, its cursors are released, and it leaves <c>list_connections</c> (C5).</summary>
        /// <param name="instance">The tab.</param>
        /// <returns>The binding that ended, or <c>null</c>.</returns>
        public BoundConnection? ReleaseBinding(IHostInstance instance)
        {
            var released = this.Registry.Release(instance);
            if (released != null)
            {
                this.Cursors.ReleaseConnection(released.ConnectionId);
                this.OnStateChanged();
            }

            return released;
        }

        /// <summary>Starts the host — <b>Start server</b> on any tab.</summary>
        public void Start()
        {
            lock (this.gate)
            {
                this.StartCore();
            }

            this.OnStateChanged();
        }

        /// <summary>Stops the host for every connection — <b>Stop server</b> on any tab (H3).</summary>
        public void Stop()
        {
            lock (this.gate)
            {
                this.StopCore();
            }

            this.OnStateChanged();
        }

        /// <summary>Changes the port, restarting the host when it runs. Affects every client configuration (H4).</summary>
        /// <param name="port">The new port.</param>
        public void ChangePort(int port)
        {
            lock (this.gate)
            {
                var wasRunning = this.State != HostState.Stopped;
                this.StopCore();
                this.Settings.Update(s => s.Port = port);
                if (wasRunning)
                {
                    this.StartCore();
                }
            }

            this.OnStateChanged();
        }

        /// <summary>Changes the host limits (H4).</summary>
        /// <param name="maxPageSize">The maximum page size.</param>
        /// <param name="responseCap">The response cap, in characters.</param>
        /// <param name="approvalTimeoutSeconds">The approval timeout, in seconds.</param>
        public void ChangeLimits(int maxPageSize, int responseCap, int approvalTimeoutSeconds)
        {
            this.Settings.Update(s =>
            {
                s.MaxPageSize = maxPageSize;
                s.ResponseCapCharacters = responseCap;
                s.ApprovalTimeoutSeconds = approvalTimeoutSeconds;
            });
            this.OnStateChanged();
        }

        /// <summary>Regenerates the bearer token; every client must pick up the new one (H4).</summary>
        public void RegenerateToken()
        {
            this.Tokens.Regenerate();
            this.RaiseNotice(TokenStore.RegeneratedNotice);
        }

        /// <summary>Gets how a connection is confirmed as development, or <c>null</c> when it is not.</summary>
        /// <param name="binding">The connection.</param>
        /// <returns>The scope of the confirmation in force.</returns>
        public ApprovalScope? DevEnvironmentScope(BoundConnection binding) =>
            binding.DevConfirmedOnce ? ApprovalScope.Once : this.Approvals.Find(ApprovalKind.DevEnvironment, binding.OrganizationId)?.Scope;

        /// <summary>Writes a host-log entry to every tab (C10).</summary>
        /// <param name="entry">The entry.</param>
        public void LogHostCall(CallLogEntry entry) => this.HostCallLogged?.Invoke(this, entry);

        /// <summary>Tells every tab that its view of the host may be out of date.</summary>
        public void OnStateChanged() => this.StateChanged?.Invoke(this, EventArgs.Empty);

        private static CompanionHost CreateDefault()
        {
            var version = typeof(CompanionHost).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
            return new CompanionHost(
                new HostSettingsStore(new XrmToolBoxSettingsStore()),
                new DpapiTokenProtector(),
                SystemClock.Instance,
                (port, protocol) => new McpHttpServer(port, protocol, message => Trace.WriteLine($"Agent Companion MCP: {message}")),
                version);
        }

        private void RaiseNotice(string notice)
        {
            this.Notice = notice;
            this.NoticeRaised?.Invoke(this, notice);
        }

        private void StartCore()
        {
            if (this.State == HostState.Running)
            {
                return;
            }

            var port = this.Port;
            var candidateProtocol = new McpProtocol(this.Dispatcher, () => this.Tokens.Token, this.Version, this.clock);
            var candidate = this.serverFactory(port, candidateProtocol);
            try
            {
                candidate.Start();
            }
            catch (Exception ex)
            {
                candidate.Dispose();
                this.State = HostState.Failed;
                this.StartError = $"Port {port} could not be used: {ex.Message}. Another XrmToolBox process may already run the Agent Companion host — close it, or change the port here and in every client configuration.";
                return;
            }

            this.running = new CancellationTokenSource();
            this.server = candidate;
            this.protocol = candidateProtocol;
            this.State = HostState.Running;
            this.StartError = null;
        }

        private void StopCore()
        {
            // Pending calls answer server_stopping; handshake sessions, session grants and cursors end (H3).
            this.running.Cancel();
            this.protocol?.EndAllSessions();
            if (this.server != null)
            {
                this.server.Stop();
                this.server.Dispose();
            }

            this.server = null;
            this.protocol = null;
            this.Approvals.EndSession();
            this.Cursors.Clear();
            this.State = HostState.Stopped;
            this.StartError = null;
        }
    }
}
