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

    /// <summary>
    /// The spike's tools: enough to observe the singleton, the registry and approval routing (S1, S6),
    /// and to drive <c>AddSolutionComponentRequest</c> with explicit flags (S2).
    /// </summary>
    internal sealed class SpikeTools : IToolProvider
    {
        private const string Hello = "hello";
        private const string GetLog = "get_log";
        private const string ListConnections = "list_connections";
        private const string WhoAmI = "who_am_i";
        private const string CreateSolution = "create_solution";
        private const string AddSolutionComponent = "add_solution_component";

        private static readonly string[] Names = { Hello, GetLog, ListConnections, WhoAmI, CreateSolution, AddSolutionComponent };

        private static readonly JObject ConnectionProperty = new JObject { ["type"] = "string", ["description"] = "connectionId from list_connections" };

        public JArray ListTools() => new JArray(
            Tool(Hello, "Process, AppDomain and host details, to show that every tab shares one host. No approval.", new JObject()),
            Tool(GetLog, "Recent log lines from the host and every spike tab. No approval.", new JObject()),
            Tool(ListConnections, "Connections bound to plugin tabs. Pass a connectionId as `connection` to other tools. No approval.", new JObject()),
            Tool(WhoAmI, "The connected user's identity. Approved in the tab bound to `connection`.", new JObject
            {
                ["connection"] = ConnectionProperty,
            }, "connection"),
            Tool(CreateSolution, "Creates an empty unmanaged solution under an existing publisher. Approved in the tab.", new JObject
            {
                ["connection"] = ConnectionProperty,
                ["uniqueName"] = new JObject { ["type"] = "string" },
                ["friendlyName"] = new JObject { ["type"] = "string" },
                ["publisherUniqueName"] = new JObject { ["type"] = "string" },
            }, "connection", "uniqueName", "friendlyName", "publisherUniqueName"),
            Tool(AddSolutionComponent, "Runs AddSolutionComponentRequest with AddRequiredComponents = false and the given flags (spike S2). Approved in the tab.", new JObject
            {
                ["connection"] = ConnectionProperty,
                ["solutionUniqueName"] = new JObject { ["type"] = "string" },
                ["componentType"] = new JObject { ["type"] = "integer" },
                ["componentId"] = new JObject { ["type"] = "string" },
                ["doNotIncludeSubcomponents"] = new JObject { ["type"] = "boolean" },
                ["includedComponentSettingsValues"] = new JObject
                {
                    ["type"] = new JArray("array", "null"),
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "null leaves the property unset; [] sends an empty array",
                },
            }, "connection", "solutionUniqueName", "componentType", "componentId", "doNotIncludeSubcomponents"));

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
                case GetLog:
                    return ToolResult.Ok(new JObject { ["lines"] = new JArray(CompanionHost.RecentLog()) });
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
                    return await ApprovedCallAsync(name, arguments).ConfigureAwait(false);
            }
        }

        private static async Task<ToolResult> ApprovedCallAsync(string name, JObject arguments)
        {
            var connectionId = (string)arguments["connection"];
            var connection = connectionId == null ? null : CompanionHost.Registry.Find(connectionId);
            if (connection == null)
            {
                return ToolResult.Fail("connection_not_found", "No plugin tab is bound to that connection. Call list_connections.");
            }

            var summary = arguments.Properties()
                .Where(p => p.Name != "connection")
                .Aggregate(string.Empty, (text, p) => $"{text}{p.Name}: {p.Value.ToString(Newtonsoft.Json.Formatting.None)}\r\n");
            if (!await connection.Tab.RequestApprovalAsync(name, summary).ConfigureAwait(false))
            {
                return ToolResult.Fail("denied_by_user", "The person denied the call, or it timed out.");
            }

            var service = connection.Tab.Service;
            try
            {
                return await Task.Run(() =>
                {
                    switch (name)
                    {
                        case WhoAmI:
                            return WhoAmIResult(service);
                        case CreateSolution:
                            return CreateSolutionResult(service, arguments);
                        default:
                            return AddComponentResult(service, arguments);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                connection.Tab.Log($"{name} failed: {ex.GetBaseException().Message}");
                return ToolResult.Fail("dataverse_error", ex.GetBaseException().Message);
            }
        }

        private static ToolResult WhoAmIResult(IOrganizationService service)
        {
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
        }

        private static ToolResult CreateSolutionResult(IOrganizationService service, JObject arguments)
        {
            var publisherName = (string)arguments["publisherUniqueName"];
            var query = new QueryExpression("publisher") { ColumnSet = new ColumnSet("publisherid"), TopCount = 1 };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, publisherName);
            var publisher = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (publisher == null)
            {
                return ToolResult.Fail("not_found", $"Publisher not found: {publisherName}");
            }

            var solution = new Entity("solution")
            {
                ["uniquename"] = (string)arguments["uniqueName"],
                ["friendlyname"] = (string)arguments["friendlyName"],
                ["publisherid"] = publisher.ToEntityReference(),
                ["version"] = "1.0.0.0",
            };

            return ToolResult.Ok(new JObject { ["solutionId"] = service.Create(solution) });
        }

        private static ToolResult AddComponentResult(IOrganizationService service, JObject arguments)
        {
            var request = new AddSolutionComponentRequest
            {
                SolutionUniqueName = (string)arguments["solutionUniqueName"],
                ComponentType = (int)arguments["componentType"],
                ComponentId = Guid.Parse((string)arguments["componentId"]),
                AddRequiredComponents = false,
                DoNotIncludeSubcomponents = (bool)arguments["doNotIncludeSubcomponents"],
            };

            if (arguments["includedComponentSettingsValues"] is JArray settings)
            {
                request.IncludedComponentSettingsValues = settings.Select(s => (string)s).ToArray();
            }

            var response = (AddSolutionComponentResponse)service.Execute(request);
            return ToolResult.Ok(new JObject { ["solutionComponentId"] = response.id });
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
