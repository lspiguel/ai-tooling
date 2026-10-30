// <copyright file="PublisherTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>The publisher Read tool, <c>list_publishers</c> (SPEC §6.5).</summary>
    internal static class PublisherTools
    {
        /// <summary>The tool name.</summary>
        public const string ListPublishers = "list_publishers";

        private const string NameContains = "nameContains";

        /// <summary>Defines <c>list_publishers</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition ListPublishersTool() => ToolDefinition.Paged(
            ListPublishers,
            ToolTier.Read,
            "Publishers with customization prefix and option value prefix. Reuse an existing publisher: a second one, even with the same prefix, causes layering problems. Paged.",
            100,
            InputSchema.ForConnection().String(NameContains, "Matches the unique or the friendly name."),
            "publishers",
            OutputSchema.Object(
                ("publisherId", OutputSchema.Guid()),
                ("uniqueName", OutputSchema.String()),
                ("friendlyName", OutputSchema.String()),
                ("customizationPrefix", OutputSchema.String()),
                ("optionValuePrefix", OutputSchema.Integer()),
                ("isReadonly", OutputSchema.Boolean()),
                ("description", OutputSchema.NullableString())),
            OpenPublishers,
            input => input.WithReason(required: false));

        /// <summary>Escapes the LIKE wildcards in a search text.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The escaped text.</returns>
        public static string EscapeLike(string text) => text.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

        private static IPageSource OpenPublishers(ToolExecution execution)
        {
            var query = new QueryExpression("publisher")
            {
                ColumnSet = new ColumnSet("uniquename", "friendlyname", "customizationprefix", "customizationoptionvalueprefix", "isreadonly", "description"),
            };
            query.AddOrder("friendlyname", OrderType.Ascending);

            if (execution.Arguments[NameContains]?.Type == JTokenType.String)
            {
                var pattern = $"%{EscapeLike((string)execution.Arguments[NameContains]!)}%";
                var names = new FilterExpression(LogicalOperator.Or);
                names.AddCondition("uniquename", ConditionOperator.Like, pattern);
                names.AddCondition("friendlyname", ConditionOperator.Like, pattern);
                query.Criteria.AddFilter(names);
            }

            return new RecordPageSource(execution.Service, query, ToItem);
        }

        private static JObject ToItem(Entity publisher) => new JObject
        {
            ["publisherId"] = ValueFormats.FormatGuid(publisher.Id),
            ["uniqueName"] = publisher.GetAttributeValue<string>("uniquename"),
            ["friendlyName"] = publisher.GetAttributeValue<string>("friendlyname"),
            ["customizationPrefix"] = publisher.GetAttributeValue<string>("customizationprefix"),
            ["optionValuePrefix"] = publisher.GetAttributeValue<int>("customizationoptionvalueprefix"),
            ["isReadonly"] = publisher.GetAttributeValue<bool>("isreadonly"),
            ["description"] = ValueFormats.NullableString(string.IsNullOrEmpty(publisher.GetAttributeValue<string>("description")) ? null : publisher.GetAttributeValue<string>("description")),
        };
    }
}
