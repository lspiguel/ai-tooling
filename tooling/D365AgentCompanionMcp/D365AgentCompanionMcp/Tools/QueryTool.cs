// <copyright file="QueryTool.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The <c>query</c> Read tool (SPEC §6.5): FetchXML, always paged. A query reading any table outside the
    /// configuration allow-list touches records: the dialog flags it, and it is refused with <c>tool_disabled</c>
    /// before any dialog when record queries are off for the connection (C8).
    /// </summary>
    internal static class QueryTool
    {
        /// <summary>The tool name.</summary>
        public const string Query = "query";

        private const string FetchXml = "fetchXml";
        private const string IncludeFormattedValues = "includeFormattedValues";

        /// <summary>Defines <c>query</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition QueryToolDefinition() => ToolDefinition.Paged(
            Query,
            ToolTier.Read,
            "Runs FetchXML, paged. Name every column; <all-attributes /> and top are refused. page, count and paging-cookie are set by the server. A query on a table outside the configuration allow-list (publisher, solution, solutioncomponent, systemform, savedquery, sitemap, appmodule, appmodulecomponent, webresource, ribboncustomization, organization, workflow, customcontrol, environmentvariabledefinition, connectionreference, pluginassembly, sdkmessageprocessingstep, customapi, role, fieldsecurityprofile) touches records: the approval says so, and it fails with tool_disabled when record queries are off. Values: option sets {_option,label}, multi-select {_options,labels}, lookups {_ref:{entity,id},name}, money {_money,formatted}; empty columns are omitted; link-entity columns are keyed alias.column.",
            50,
            InputSchema.ForConnection()
                .String(FetchXml, "The FetchXML query.", required: true, minLength: 1)
                .Boolean(IncludeFormattedValues, "Add labels, lookup names and formatted money.", true),
            "records",
            new JObject { ["type"] = "object" },
            Open,
            input => input.WithReason(required: false),
            PageField.Every("entityName", OutputSchema.String()),
            PageField.Every("touchesRecords", OutputSchema.Boolean()))
            .WithInspection(Inspect);

        /// <summary>Checks the query before the dialog: rules, record detection and the per-connection switch.</summary>
        /// <param name="binding">The connection.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>What the dialog must flag.</returns>
        internal static CallInspection Inspect(BoundConnection binding, JObject arguments)
        {
            var query = FetchXmlQuery.Parse((string)arguments[FetchXml]!);
            if (query.TouchesRecords && !binding.RecordQueriesEnabled)
            {
                throw new ToolException(
                    ToolErrorCodes.ToolDisabled,
                    $"Record queries are switched off for this connection, and this query reads records ({string.Join(", ", query.RecordTables)}). Ask the person.",
                    new JObject { ["recordTables"] = new JArray(query.RecordTables) });
            }

            return new CallInspection(query.TouchesRecords);
        }

        private static IPageSource Open(ToolExecution execution)
        {
            var query = FetchXmlQuery.Parse((string)execution.Arguments[FetchXml]!);
            var formatted = MetadataHelpers.OptionalBool(execution.Arguments, IncludeFormattedValues, true);
            return new FetchXmlPageSource(
                execution.Service,
                query.Document,
                record => RecordValues.Serialize(record, formatted),
                new JObject { ["entityName"] = query.EntityName, ["touchesRecords"] = query.TouchesRecords });
        }
    }
}
