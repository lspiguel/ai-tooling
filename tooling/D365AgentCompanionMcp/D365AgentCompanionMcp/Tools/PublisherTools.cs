// <copyright file="PublisherTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>The publisher tools: the Read tool <c>list_publishers</c> (SPEC §6.5) and the Publisher tool <c>create_publisher</c> (§6.7).</summary>
    internal static class PublisherTools
    {
        /// <summary>The tool name.</summary>
        public const string ListPublishers = "list_publishers";

        /// <summary>The <c>create_publisher</c> tool name.</summary>
        public const string CreatePublisher = "create_publisher";

        private const string NameContains = "nameContains";
        private const string UniqueName = "uniqueName";
        private const string FriendlyName = "friendlyName";
        private const string Prefix = "prefix";
        private const string OptionValuePrefix = "optionValuePrefix";
        private const string Description = "description";

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

        /// <summary>Defines <c>create_publisher</c> (SPEC §6.7): disabled by default per connection; when enabled, the person types the prefix to confirm (R3).</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition CreatePublisherTool() => ToolDefinition.Simple(
            CreatePublisher,
            ToolTier.Publisher,
            "Creates a publisher. Rarely right: a second publisher, even with the same prefix, causes layering problems that cannot be fixed — reuse an existing one (list_publishers). Disabled unless the person enables it for the connection (tool_disabled tells you a person must create or choose the publisher). When enabled, the person must type the prefix to confirm; a wrong confirmation is denied_by_user. Requires reason and a connection confirmed as development.",
            InputSchema.ForConnection()
                .Pattern(UniqueName, "Letters, digits and underscores, starting with a letter or underscore.", "^[A-Za-z_][A-Za-z0-9_]*$", required: true)
                .String(FriendlyName, "The display name.", required: true, minLength: 1)
                .Pattern(Prefix, "2–8 letters and digits, starting with a letter; not starting with mscrm.", "^(?![Mm][Ss][Cc][Rr][Mm])[A-Za-z][A-Za-z0-9]{1,7}$", required: true)
                .Integer(OptionValuePrefix, "10000–99999.", 10000, 99999)
                .String(Description, "Optional description.")
                .WithReason(required: true),
            OutputSchema.Object(
                ("publisherId", OutputSchema.Guid()),
                ("uniqueName", OutputSchema.String()),
                ("friendlyName", OutputSchema.String()),
                ("customizationPrefix", OutputSchema.String()),
                ("optionValuePrefix", OutputSchema.Integer())),
            RunCreatePublisher)
            .WithPreparation(PrepareCreatePublisher);

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

        private static PreparedCall PrepareCreatePublisher(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var uniqueName = ((string)arguments[UniqueName]!).Trim();
            var prefix = ((string)arguments[Prefix]!).Trim();
            var optionValuePrefix = (int)arguments[OptionValuePrefix]!;

            var query = new QueryExpression("publisher") { ColumnSet = new ColumnSet("uniquename", "friendlyname", "customizationprefix", "customizationoptionvalueprefix") };
            query.AddOrder("friendlyname", OrderType.Ascending);
            var existing = execution.Service.RetrieveMultiple(query).Entities;
            if (existing.Any(p => string.Equals(p.GetAttributeValue<string>("uniquename"), uniqueName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ToolException(ToolErrorCodes.AlreadyExists, $"A publisher named '{uniqueName}' already exists. Reuse it.", new JObject { ["kind"] = "publisher", ["name"] = uniqueName });
            }

            // A shared prefix or option value prefix is warned about, not refused (SPEC §6.7).
            var summary = new StringBuilder().AppendLine("Existing publishers:");
            foreach (var publisher in existing)
            {
                summary.AppendLine($"  {publisher.GetAttributeValue<string>("uniquename"),-30} prefix {publisher.GetAttributeValue<string>("customizationprefix"),-8}  option prefix {publisher.GetAttributeValue<int>("customizationoptionvalueprefix").ToString(CultureInfo.InvariantCulture)}");
            }

            var samePrefix = existing.Any(p => string.Equals(p.GetAttributeValue<string>("customizationprefix"), prefix, StringComparison.OrdinalIgnoreCase));
            var sameOption = existing.Any(p => p.GetAttributeValue<int>("customizationoptionvalueprefix") == optionValuePrefix);
            summary.AppendLine()
                .AppendLine("Requested:")
                .AppendLine($"  Unique name   {uniqueName}   \"{((string)arguments[FriendlyName]!).Trim()}\"")
                .AppendLine($"  Prefix        {prefix}{(samePrefix ? "   ⚠ same prefix as an existing publisher" : string.Empty)}")
                .Append($"  Option pfx    {optionValuePrefix.ToString(CultureInfo.InvariantCulture)}{(sameOption ? "   ⚠ same as an existing publisher" : string.Empty)}");
            return new PreparedCall(summary.ToString(), confirmation: prefix);
        }

        private static JObject RunCreatePublisher(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var publisher = new Entity("publisher")
            {
                ["uniquename"] = ((string)arguments[UniqueName]!).Trim(),
                ["friendlyname"] = ((string)arguments[FriendlyName]!).Trim(),
                ["customizationprefix"] = ((string)arguments[Prefix]!).Trim(),
                ["customizationoptionvalueprefix"] = (int)arguments[OptionValuePrefix]!,
            };
            if (arguments[Description]?.Type == JTokenType.String)
            {
                publisher["description"] = (string)arguments[Description]!;
            }

            var id = execution.Service.Create(publisher);
            return new JObject
            {
                ["publisherId"] = ValueFormats.FormatGuid(id),
                ["uniqueName"] = publisher.GetAttributeValue<string>("uniquename"),
                ["friendlyName"] = publisher.GetAttributeValue<string>("friendlyname"),
                ["customizationPrefix"] = publisher.GetAttributeValue<string>("customizationprefix"),
                ["optionValuePrefix"] = publisher.GetAttributeValue<int>("customizationoptionvalueprefix"),
            };
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
