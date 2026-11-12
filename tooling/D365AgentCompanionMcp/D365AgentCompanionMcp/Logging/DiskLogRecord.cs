// <copyright file="DiskLogRecord.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Linq;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// One call as the automatic call log writes it (SPEC 6a.2): the entry, with full arguments and result, and the
    /// file it belongs in. Built when the call ends, from what the panel shows and what the agent sent and received —
    /// never the bearer token or anything else the plugin holds.
    /// </summary>
    internal sealed class DiskLogRecord
    {
        private DiskLogRecord(string? connectionId, string fileName, bool sharedName, JObject? connection, DateTimeOffset receivedLocal, JObject entry)
        {
            this.ConnectionId = connectionId;
            this.Name = fileName;
            this.SharedName = sharedName;
            this.Connection = connection;
            this.ReceivedLocal = receivedLocal;
            this.Entry = entry;
        }

        /// <summary>Gets the connection ID, or <c>null</c> for a host call.</summary>
        public string? ConnectionId { get; }

        /// <summary>Gets the name part of the file name: the connection name, or <see cref="CallLogFile.HostName"/>.</summary>
        public string Name { get; }

        /// <summary>Gets a value indicating whether another bound connection had the same name when the call ended.</summary>
        public bool SharedName { get; }

        /// <summary>Gets the file's <c>connection</c> object, or <c>null</c> for the host file.</summary>
        public JObject? Connection { get; }

        /// <summary>Gets when the host received the call, local.</summary>
        public DateTimeOffset ReceivedLocal { get; }

        /// <summary>Gets the entry.</summary>
        public JObject Entry { get; }

        /// <summary>Builds the record of a call.</summary>
        /// <param name="binding">The connection the call named, or <c>null</c> for a host call.</param>
        /// <param name="sharedName">Whether another bound connection has the same name.</param>
        /// <param name="call">The call as received.</param>
        /// <param name="tier">The tool's tier.</param>
        /// <param name="logged">The panel's entry for the call: its lines, approval and warnings.</param>
        /// <param name="result">The result or tool error returned to the agent.</param>
        /// <param name="duration">From receipt to reply.</param>
        /// <returns>The record.</returns>
        public static DiskLogRecord Create(BoundConnection? binding, bool sharedName, McpToolCall call, string tier, CallLogEntry logged, ToolResult result, TimeSpan duration)
        {
            var arguments = call.Arguments == null ? null : (JObject)call.Arguments.DeepClone();
            var reason = arguments?["reason"];
            arguments?.Remove("reason");

            var error = result.IsError ? (JObject)result.Payload["error"]! : null;
            var ran = logged.Approval != CallLogFormatter.NotRun;
            var outcome = new JObject
            {
                ["status"] = result.IsError ? "error" : "ok",
                ["errorCode"] = error?["code"]?.DeepClone() ?? JValue.CreateNull(),
                ["errorMessage"] = error?["message"]?.DeepClone() ?? JValue.CreateNull(),
                ["errorDetails"] = error?["details"]?.DeepClone() ?? JValue.CreateNull(),
                ["approval"] = ran ? logged.Approval : null,
                ["durationMs"] = (long)Math.Max(0, Math.Round(duration.TotalMilliseconds)),
                ["outcomeLine"] = logged.OutcomeLine,
                ["pages"] = result.IsError ? 0 : 1,
                ["responseBytes"] = Encoding.UTF8.GetByteCount(result.Text),
            };

            var receivedLocal = new DateTimeOffset(DateTime.SpecifyKind(call.ReceivedUtc, DateTimeKind.Utc)).ToLocalTime();
            var entry = new JObject
            {
                ["receivedAt"] = CallLogFile.FormatReceivedAt(receivedLocal),
                ["tool"] = call.Name,
                ["tier"] = tier.ToLowerInvariant(),
                ["reason"] = reason?.DeepClone() ?? JValue.CreateNull(),
                ["callLine"] = logged.CallLine,
                ["arguments"] = (JToken?)arguments ?? JValue.CreateNull(),
                ["outcome"] = outcome,
                ["result"] = result.IsError ? JValue.CreateNull() : result.Payload.DeepClone(),
                ["unexpected"] = new JArray(logged.Warnings.Select(w => w.Trim())),
            };

            if (binding == null)
            {
                return new DiskLogRecord(null, CallLogFile.HostName, false, null, receivedLocal, entry);
            }

            var connection = new JObject
            {
                ["connectionId"] = binding.ConnectionId,
                ["connectionName"] = binding.ConnectionName,
                ["organization"] = new JObject
                {
                    ["id"] = ValueFormats.FormatGuid(binding.OrganizationId),
                    ["uniqueName"] = binding.OrganizationUniqueName,
                    ["friendlyName"] = binding.OrganizationFriendlyName,
                    ["environmentId"] = string.IsNullOrEmpty(binding.EnvironmentId) ? null : binding.EnvironmentId,
                    ["tenantId"] = binding.TenantId == Guid.Empty ? null : ValueFormats.FormatGuid(binding.TenantId),
                },
            };

            return new DiskLogRecord(binding.ConnectionId, binding.ConnectionName, sharedName, connection, receivedLocal, entry);
        }

        /// <summary>The entry as compact JSON, for tests and diagnostics.</summary>
        /// <returns>The JSON text.</returns>
        public override string ToString() => this.Entry.ToString(Formatting.None);
    }
}
