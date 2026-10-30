// <copyright file="SpikeTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>The spike's tools: enough to observe the singleton, the registry and approval routing.</summary>
    internal sealed class SpikeTools : IToolProvider
    {
        private const string Hello = "hello";
        private const string ListConnections = "list_connections";
        private const string WhoAmI = "who_am_i";

        private static readonly string[] Names = { Hello, ListConnections, WhoAmI };

        public JArray ListTools() => new JArray(
            Tool(Hello, "Process, AppDomain and host details, to show that every tab shares one host. No approval.", new JObject()),
            Tool(ListConnections, "Connections bound to plugin tabs. Pass a connectionId as `connection` to other tools. No approval.", new JObject()),
            Tool(WhoAmI, "The connected user's identity. Approved in the tab bound to `connection`.", new JObject
            {
                ["connection"] = new JObject { ["type"] = "string", ["description"] = "connectionId from list_connections" },
            }, "connection"));

        public bool HasTool(string name) => Names.Contains(name);

        public async Task<ToolResult> CallAsync(string name, JObject arguments, CancellationToken cancellationToken)
        {
            switch (name)
            {
                case Hello:
                    return ToolResult.Ok(new JObject
                    {
                        ["processId"] = Process.GetCurrentProcess().Id,
                        ["appDomainId"] = AppDomain.CurrentDomain.Id,
                        ["hostLoadedUtc"] = CompanionHost.LoadedUtc,
                        ["tabs"] = CompanionHost.Instances,
                    });
                case ListConnections:
                    return ToolResult.Ok(new JObject
                    {
                        ["connections"] = new JArray(CompanionHost.Registry.Snapshot().Select(b => new JObject
                        {
                            ["connectionId"] = b.ConnectionId,
                            ["connectionIdMinted"] = b.ConnectionIdMinted,
                            ["connectionName"] = b.ConnectionName,
                            ["organization"] = new JObject { ["id"] = b.OrganizationId, ["friendlyName"] = b.OrganizationFriendlyName, ["url"] = b.Url },
                            ["boundOn"] = b.BoundOn,
                        })),
                    });
                default:
                    return await WhoAmIAsync(arguments).ConfigureAwait(false);
            }
        }

        private static async Task<ToolResult> WhoAmIAsync(JObject arguments)
        {
            var connectionId = (string)arguments["connection"];
            var connection = connectionId == null ? null : CompanionHost.Registry.Find(connectionId);
            if (connection == null)
            {
                return ToolResult.Fail("connection_not_found", "No plugin tab is bound to that connection. Call list_connections.");
            }

            if (!await connection.Tab.RequestApprovalAsync(WhoAmI, "Read the connected user's identity").ConfigureAwait(false))
            {
                return ToolResult.Fail("denied_by_user", "The person denied the call.");
            }

            return await Task.Run(() =>
            {
                var service = connection.Tab.Service;
                var who = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
                var user = service.Retrieve("systemuser", who.UserId, new ColumnSet("firstname", "lastname", "fullname", "domainname", "applicationid"));
                return ToolResult.Ok(new JObject
                {
                    ["userId"] = who.UserId,
                    ["businessUnitId"] = who.BusinessUnitId,
                    ["organizationId"] = who.OrganizationId,
                    ["firstName"] = user.GetAttributeValue<string>("firstname"),
                    ["lastName"] = user.GetAttributeValue<string>("lastname"),
                    ["fullName"] = user.GetAttributeValue<string>("fullname"),
                    ["domainName"] = user.GetAttributeValue<string>("domainname"),
                    ["isApplicationUser"] = user.GetAttributeValue<Guid?>("applicationid") != null,
                });
            }).ConfigureAwait(false);
        }

        private static JObject Tool(string name, string description, JObject properties, params string[] required) => new JObject
        {
            ["name"] = name,
            ["title"] = name,
            ["description"] = description,
            ["inputSchema"] = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new JArray(required),
                ["additionalProperties"] = false,
            },
            ["outputSchema"] = new JObject { ["type"] = "object" },
        };
    }
}
