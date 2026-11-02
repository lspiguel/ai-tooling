// <copyright file="HostTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
    using Newtonsoft.Json.Linq;

    /// <summary>The host tool, <c>list_connections</c> (SPEC §6.4).</summary>
    internal static class HostTools
    {
        /// <summary>The tool name.</summary>
        public const string ListConnections = "list_connections";

        /// <summary>Defines <c>list_connections</c>.</summary>
        /// <param name="host">The host.</param>
        /// <returns>The definition.</returns>
        public static ToolDefinition ListConnectionsTool(CompanionHost host) => ToolDefinition.Host(
            ListConnections,
            "Lists the XrmToolBox connections bound to Agent Companion tabs — the values accepted as `connection` by every other tool. No approval needed. An empty list means the person has not connected a tab: ask them to open the plugin and connect.",
            InputSchema.None(),
            ListConnectionsOutput(),
            () => RunListConnections(host));

        private static JObject ListConnectionsOutput()
        {
            var organization = OutputSchema.Object(
                ("id", OutputSchema.Guid()),
                ("uniqueName", OutputSchema.String()),
                ("friendlyName", OutputSchema.String()),
                ("url", OutputSchema.String()));
            var connection = OutputSchema.Object(
                ("connectionId", OutputSchema.String()),
                ("connectionName", OutputSchema.String()),
                ("organization", organization),
                ("devEnvironmentConfirmed", OutputSchema.Boolean()),
                ("boundOn", OutputSchema.DateTime()));
            var hostInfo = OutputSchema.Object(("version", OutputSchema.String()), ("protocolVersion", OutputSchema.String()));

            return OutputSchema.Object(("host", hostInfo), ("connections", OutputSchema.Array(connection)));
        }

        private static JObject RunListConnections(CompanionHost host) => new JObject
        {
            ["host"] = new JObject { ["version"] = host.Version, ["protocolVersion"] = McpConstants.ProtocolVersion },
            ["connections"] = new JArray(host.Registry.Bound().Select(b => new JObject
            {
                ["connectionId"] = b.ConnectionId,
                ["connectionName"] = b.ConnectionName,
                ["organization"] = new JObject
                {
                    ["id"] = ValueFormats.FormatGuid(b.OrganizationId),
                    ["uniqueName"] = b.OrganizationUniqueName,
                    ["friendlyName"] = b.OrganizationFriendlyName,
                    ["url"] = b.OrganizationUrl,
                },
                ["devEnvironmentConfirmed"] = host.DevEnvironmentScope(b) != null,
                ["boundOn"] = ValueFormats.FormatDateTime(b.BoundOnUtc!.Value),
            })),
        };
    }
}
