// <copyright file="MetadataTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The metadata Read tools — <c>list_tables</c>, <c>get_table_metadata</c>, <c>get_relationships</c> and
    /// <c>get_option_sets</c> (SPEC §6.5). Each reads its metadata once, on the first call, and pages the snapshot.
    /// Results are trimmed projections, never the raw metadata graph (SPEC §5 Serialization).
    /// </summary>
    internal static class MetadataTools
    {
        /// <summary>The <c>list_tables</c> tool name.</summary>
        public const string ListTables = "list_tables";

        /// <summary>The <c>get_table_metadata</c> tool name.</summary>
        public const string GetTableMetadata = "get_table_metadata";

        /// <summary>The <c>get_relationships</c> tool name.</summary>
        public const string GetRelationships = "get_relationships";

        /// <summary>The <c>get_option_sets</c> tool name.</summary>
        public const string GetOptionSets = "get_option_sets";

        private const string LogicalName = "logicalName";
        private const string NameContains = "nameContains";
        private const string Filter = "filter";
        private const string Include = "include";
        private const string ColumnFilter = "columnFilter";
        private const string Kinds = "kinds";
        private const string Names = "names";
        private const string IncludeOptions = "includeOptions";
        private const string OneToMany = "oneToMany";
        private const string ManyToOne = "manyToOne";
        private const string ManyToMany = "manyToMany";

        private static readonly string[] Filters = { "all", "custom", "system" };

        /// <summary>Defines <c>list_tables</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition ListTablesTool() => ToolDefinition.Paged(
            ListTables,
            ToolTier.Read,
            "Tables with logical, schema and display name, metadata ID, object type code and custom, managed, customizable and intersect flags. Paged.",
            250,
            InputSchema.ForConnection()
                .Enum(Filter, "all, custom or system tables.", Filters, "all")
                .String(NameContains, "Matches the logical, schema or display name, case-insensitive."),
            "tables",
            OutputSchema.Object(
                ("logicalName", OutputSchema.String()),
                ("schemaName", OutputSchema.String()),
                ("displayName", OutputSchema.NullableString()),
                ("metadataId", OutputSchema.Guid()),
                ("objectTypeCode", OutputSchema.Integer()),
                ("isCustom", OutputSchema.Boolean()),
                ("isManaged", OutputSchema.Boolean()),
                ("isCustomizable", OutputSchema.Boolean()),
                ("isIntersect", OutputSchema.Boolean())),
            OpenTables,
            input => input.WithReason(required: false));

        /// <summary>Defines <c>get_table_metadata</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetTableMetadataTool() => ToolDefinition.Paged(
            GetTableMetadata,
            ToolTier.Read,
            "One table's identifiers, columns and alternate keys. Paged over columns; table and keys come on the first page. Global choices carry options: null — read them with get_option_sets.",
            100,
            InputSchema.ForConnection()
                .String(LogicalName, "The table's logical name.", required: true)
                .StringArray(Include, "What to include; default both.", new[] { "columns", "keys" })
                .Enum(ColumnFilter, "all, custom or system columns.", Filters, "all"),
            "columns",
            ColumnSchema(),
            OpenTableMetadata,
            input => input.WithReason(required: false),
            PageField.First("table", TableSchema()),
            PageField.First("keys", OutputSchema.Array(KeySchema()))).WithOptionalItems();

        /// <summary>Defines <c>get_relationships</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetRelationshipsTool() => ToolDefinition.Paged(
            GetRelationships,
            ToolTier.Read,
            "One table's 1:N, N:1 and N:N relationships with cascade settings. kind is relative to the requested table; a self-referencing relationship appears once as each. Paged.",
            100,
            InputSchema.ForConnection()
                .String(LogicalName, "The table's logical name.", required: true)
                .StringArray(Kinds, "Which kinds; default all three.", new[] { OneToMany, ManyToOne, ManyToMany }),
            "relationships",
            new JObject { ["type"] = "object" },
            OpenRelationships,
            input => input.WithReason(required: false),
            PageField.Every(LogicalName, OutputSchema.String()));

        /// <summary>Defines <c>get_option_sets</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetOptionSetsTool() => ToolDefinition.Paged(
            GetOptionSets,
            ToolTier.Read,
            "Global choices with values and labels. Requested names that do not exist are listed in notFound on the first page. Paged.",
            50,
            InputSchema.ForConnection()
                .StringArray(Names, "Global choice names; omit for all.")
                .Enum(Filter, "all, custom or system choices.", Filters, "all")
                .Boolean(IncludeOptions, "Include the options.", true),
            "optionSets",
            OutputSchema.Object(
                ("name", OutputSchema.String()),
                ("displayName", OutputSchema.NullableString()),
                ("metadataId", OutputSchema.Guid()),
                ("optionSetType", OutputSchema.String()),
                ("isCustom", OutputSchema.Boolean()),
                ("isManaged", OutputSchema.Boolean()),
                ("options", NullableArray(OptionSchema()))),
            OpenOptionSets,
            input => input.WithReason(required: false),
            PageField.First("notFound", OutputSchema.Array(OutputSchema.String())));

        /// <summary>Projects the options of a choice — a Yes/No choice as its two options, 1 then 0.</summary>
        /// <param name="optionSet">The option set.</param>
        /// <param name="lcid">The base language.</param>
        /// <returns>The options.</returns>
        internal static JArray Options(OptionSetMetadataBase optionSet, int lcid)
        {
            if (optionSet is BooleanOptionSetMetadata boolean)
            {
                return new JArray(Option(boolean.TrueOption, 1, lcid), Option(boolean.FalseOption, 0, lcid));
            }

            var options = (optionSet as OptionSetMetadata)?.Options ?? new OptionMetadataCollection();
            return new JArray(options.Select(o => Option(o, o.Value ?? 0, lcid)));
        }

        /// <summary>Projects one column (SPEC §6.5 <c>Column</c>).</summary>
        /// <param name="attribute">The column.</param>
        /// <param name="lcid">The base language.</param>
        /// <returns>The item.</returns>
        internal static JObject Column(AttributeMetadata attribute, int lcid)
        {
            int? maxLength = attribute is StringAttributeMetadata text ? text.MaxLength
                : attribute is MemoAttributeMetadata memo ? memo.MaxLength
                : null;
            var targets = attribute is LookupAttributeMetadata lookup && lookup.Targets != null ? new JArray(lookup.Targets) : null;

            JToken optionSet = JValue.CreateNull();
            if (attribute is EnumAttributeMetadata choice && choice.OptionSet != null)
            {
                var isGlobal = choice.OptionSet.IsGlobal ?? false;
                optionSet = new JObject
                {
                    ["name"] = choice.OptionSet.Name ?? attribute.LogicalName,
                    ["isGlobal"] = isGlobal,
                    ["options"] = isGlobal ? JValue.CreateNull() : Options(choice.OptionSet, lcid),
                };
            }
            else if (attribute is BooleanAttributeMetadata yesNo && yesNo.OptionSet != null)
            {
                var isGlobal = yesNo.OptionSet.IsGlobal ?? false;
                optionSet = new JObject
                {
                    ["name"] = yesNo.OptionSet.Name ?? attribute.LogicalName,
                    ["isGlobal"] = isGlobal,
                    ["options"] = isGlobal ? JValue.CreateNull() : Options(yesNo.OptionSet, lcid),
                };
            }

            return new JObject
            {
                ["logicalName"] = attribute.LogicalName,
                ["schemaName"] = attribute.SchemaName,
                ["displayName"] = MetadataHelpers.Label(attribute.DisplayName, lcid),
                ["metadataId"] = MetadataHelpers.Id(attribute.MetadataId),
                ["type"] = attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() + "Type",
                ["requiredLevel"] = MetadataHelpers.Camel(attribute.RequiredLevel?.Value ?? AttributeRequiredLevel.None),
                ["isCustom"] = attribute.IsCustomAttribute ?? false,
                ["isManaged"] = attribute.IsManaged ?? false,
                ["isValidForForm"] = attribute.IsValidForForm ?? false,
                ["attributeOf"] = MetadataHelpers.Text(attribute.AttributeOf),
                ["maxLength"] = maxLength.HasValue ? new JValue(maxLength.Value) : JValue.CreateNull(),
                ["targets"] = (JToken?)targets ?? JValue.CreateNull(),
                ["optionSet"] = optionSet,
            };
        }

        private static JObject Option(OptionMetadata? option, int value, int lcid) => new JObject
        {
            ["value"] = value,
            ["label"] = MetadataHelpers.Label(option?.Label, lcid),
        };

        private static bool Matches(string? filter, bool? isCustom) =>
            filter == null || filter == "all" || (filter == "custom") == (isCustom ?? false);

        private static IPageSource OpenTables(ToolExecution execution)
        {
            var lcid = MetadataHelpers.BaseLanguage(execution);
            var filter = MetadataHelpers.OptionalString(execution.Arguments, Filter);
            var search = MetadataHelpers.OptionalString(execution.Arguments, NameContains);
            var response = (RetrieveAllEntitiesResponse)execution.Service.Execute(new RetrieveAllEntitiesRequest { EntityFilters = EntityFilters.Entity });

            return SnapshotPageSource.Of(response.EntityMetadata
                .Where(e => Matches(filter, e.IsCustomEntity))
                .Select(e => (Entity: e, DisplayName: MetadataHelpers.Label(e.DisplayName, lcid)))
                .Where(e => search == null
                    || MetadataHelpers.Contains(e.Entity.LogicalName, search)
                    || MetadataHelpers.Contains(e.Entity.SchemaName, search)
                    || MetadataHelpers.Contains((string?)e.DisplayName, search))
                .OrderBy(e => e.Entity.LogicalName, StringComparer.Ordinal)
                .Select(e => new JObject
                {
                    ["logicalName"] = e.Entity.LogicalName,
                    ["schemaName"] = e.Entity.SchemaName,
                    ["displayName"] = e.DisplayName,
                    ["metadataId"] = MetadataHelpers.Id(e.Entity.MetadataId),
                    ["objectTypeCode"] = e.Entity.ObjectTypeCode ?? 0,
                    ["isCustom"] = e.Entity.IsCustomEntity ?? false,
                    ["isManaged"] = e.Entity.IsManaged ?? false,
                    ["isCustomizable"] = e.Entity.IsCustomizable?.Value ?? false,
                    ["isIntersect"] = e.Entity.IsIntersect ?? false,
                }));
        }

        private static IPageSource OpenTableMetadata(ToolExecution execution)
        {
            var lcid = MetadataHelpers.BaseLanguage(execution);
            var logicalName = (string)execution.Arguments[LogicalName]!;
            var include = MetadataHelpers.OptionalStrings(execution.Arguments, Include) ?? new[] { "columns", "keys" };
            var columns = include.Contains("columns");
            var keys = include.Contains("keys");
            var filter = MetadataHelpers.OptionalString(execution.Arguments, ColumnFilter);

            // Alternate keys come with the full entity filter; columns alone need only Entity | Attributes.
            var filters = keys ? EntityFilters.All : EntityFilters.Entity | EntityFilters.Attributes;
            var entity = MetadataHelpers.RetrieveEntity(execution.Service, logicalName, filters);

            var firstPage = new JObject { ["table"] = Table(entity, lcid) };
            if (keys)
            {
                firstPage["keys"] = new JArray((entity.Keys ?? Array.Empty<EntityKeyMetadata>())
                    .OrderBy(k => k.LogicalName, StringComparer.Ordinal)
                    .Select(k => new JObject
                    {
                        ["logicalName"] = k.LogicalName,
                        ["schemaName"] = k.SchemaName,
                        ["displayName"] = MetadataHelpers.Label(k.DisplayName, lcid),
                        ["metadataId"] = MetadataHelpers.Id(k.MetadataId),
                        ["keyAttributes"] = new JArray(k.KeyAttributes ?? Array.Empty<string>()),
                        ["isManaged"] = k.IsManaged ?? false,
                    }));
            }

            var items = columns
                ? (entity.Attributes ?? Array.Empty<AttributeMetadata>())
                    .Where(a => Matches(filter, a.IsCustomAttribute))
                    .OrderBy(a => a.LogicalName, StringComparer.Ordinal)
                    .Select(a => Column(a, lcid))
                    .ToList()
                : new List<JObject>();
            return new SnapshotPageSource(items, firstPage: firstPage, omitItems: !columns);
        }

        private static JObject Table(EntityMetadata entity, int lcid) => new JObject
        {
            ["logicalName"] = entity.LogicalName,
            ["schemaName"] = entity.SchemaName,
            ["displayName"] = MetadataHelpers.Label(entity.DisplayName, lcid),
            ["metadataId"] = MetadataHelpers.Id(entity.MetadataId),
            ["objectTypeCode"] = entity.ObjectTypeCode ?? 0,
            ["primaryIdAttribute"] = entity.PrimaryIdAttribute,
            ["primaryNameAttribute"] = MetadataHelpers.Text(entity.PrimaryNameAttribute),
            ["ownershipType"] = MetadataHelpers.Camel(entity.OwnershipType ?? OwnershipTypes.None),
            ["isCustom"] = entity.IsCustomEntity ?? false,
            ["isManaged"] = entity.IsManaged ?? false,
            ["isCustomizable"] = entity.IsCustomizable?.Value ?? false,
        };

        private static IPageSource OpenRelationships(ToolExecution execution)
        {
            var logicalName = (string)execution.Arguments[LogicalName]!;
            var kinds = MetadataHelpers.OptionalStrings(execution.Arguments, Kinds) ?? new[] { OneToMany, ManyToOne, ManyToMany };
            var entity = MetadataHelpers.RetrieveEntity(execution.Service, logicalName, EntityFilters.Relationships);

            var items = new List<JObject>();
            if (kinds.Contains(OneToMany))
            {
                items.AddRange(Ordered(entity.OneToManyRelationships).Select(r => OneToManyItem(OneToMany, r)));
            }

            if (kinds.Contains(ManyToOne))
            {
                items.AddRange(Ordered(entity.ManyToOneRelationships).Select(r => OneToManyItem(ManyToOne, r)));
            }

            if (kinds.Contains(ManyToMany))
            {
                items.AddRange(Ordered(entity.ManyToManyRelationships).Select(r => new JObject
                {
                    ["kind"] = ManyToMany,
                    ["schemaName"] = r.SchemaName,
                    ["metadataId"] = MetadataHelpers.Id(r.MetadataId),
                    ["entity1LogicalName"] = r.Entity1LogicalName,
                    ["entity1IntersectAttribute"] = r.Entity1IntersectAttribute,
                    ["entity2LogicalName"] = r.Entity2LogicalName,
                    ["entity2IntersectAttribute"] = r.Entity2IntersectAttribute,
                    ["intersectEntityName"] = r.IntersectEntityName,
                    ["isCustom"] = r.IsCustomRelationship ?? false,
                    ["isManaged"] = r.IsManaged ?? false,
                }));
            }

            return new SnapshotPageSource(items, everyPage: new JObject { [LogicalName] = entity.LogicalName });
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T>? relationships)
            where T : RelationshipMetadataBase =>
            (relationships ?? Enumerable.Empty<T>()).OrderBy(r => r.SchemaName, StringComparer.Ordinal);

        private static JObject OneToManyItem(string kind, OneToManyRelationshipMetadata r)
        {
            var cascade = r.CascadeConfiguration ?? new CascadeConfiguration();
            return new JObject
            {
                ["kind"] = kind,
                ["schemaName"] = r.SchemaName,
                ["metadataId"] = MetadataHelpers.Id(r.MetadataId),
                ["referencedEntity"] = r.ReferencedEntity,
                ["referencedAttribute"] = r.ReferencedAttribute,
                ["referencingEntity"] = r.ReferencingEntity,
                ["referencingAttribute"] = r.ReferencingAttribute,
                ["cascade"] = new JObject
                {
                    ["assign"] = Cascade(cascade.Assign),
                    ["delete"] = Cascade(cascade.Delete),
                    ["merge"] = Cascade(cascade.Merge),
                    ["reparent"] = Cascade(cascade.Reparent),
                    ["share"] = Cascade(cascade.Share),
                    ["unshare"] = Cascade(cascade.Unshare),
                },
                ["isCustom"] = r.IsCustomRelationship ?? false,
                ["isManaged"] = r.IsManaged ?? false,
            };
        }

        private static string Cascade(CascadeType? type) => MetadataHelpers.Camel(type ?? CascadeType.NoCascade);

        private static IPageSource OpenOptionSets(ToolExecution execution)
        {
            var lcid = MetadataHelpers.BaseLanguage(execution);
            var names = MetadataHelpers.OptionalStrings(execution.Arguments, Names);
            var filter = MetadataHelpers.OptionalString(execution.Arguments, Filter);
            var includeOptions = MetadataHelpers.OptionalBool(execution.Arguments, IncludeOptions, true);
            var response = (RetrieveAllOptionSetsResponse)execution.Service.Execute(new RetrieveAllOptionSetsRequest());
            var all = response.OptionSetMetadata ?? Array.Empty<OptionSetMetadataBase>();

            var wanted = names == null ? null : new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var found = all.Where(o => wanted == null || wanted.Contains(o.Name)).ToList();
            var notFound = names == null
                ? new JArray()
                : new JArray(names.Where(n => !found.Any(o => string.Equals(o.Name, n, StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase));

            var items = found
                .Where(o => Matches(filter, o.IsCustomOptionSet))
                .OrderBy(o => o.Name, StringComparer.Ordinal)
                .Select(o => new JObject
                {
                    ["name"] = o.Name,
                    ["displayName"] = MetadataHelpers.Label(o.DisplayName, lcid),
                    ["metadataId"] = MetadataHelpers.Id(o.MetadataId),
                    ["optionSetType"] = o.OptionSetType?.ToString() ?? "Picklist",
                    ["isCustom"] = o.IsCustomOptionSet ?? false,
                    ["isManaged"] = o.IsManaged ?? false,
                    ["options"] = includeOptions ? Options(o, lcid) : JValue.CreateNull(),
                })
                .ToList();
            return new SnapshotPageSource(items, firstPage: new JObject { ["notFound"] = notFound });
        }

        private static JObject TableSchema() => OutputSchema.Object(
            ("logicalName", OutputSchema.String()),
            ("schemaName", OutputSchema.String()),
            ("displayName", OutputSchema.NullableString()),
            ("metadataId", OutputSchema.Guid()),
            ("objectTypeCode", OutputSchema.Integer()),
            ("primaryIdAttribute", OutputSchema.String()),
            ("primaryNameAttribute", OutputSchema.NullableString()),
            ("ownershipType", OutputSchema.String()),
            ("isCustom", OutputSchema.Boolean()),
            ("isManaged", OutputSchema.Boolean()),
            ("isCustomizable", OutputSchema.Boolean()));

        private static JObject KeySchema() => OutputSchema.Object(
            ("logicalName", OutputSchema.String()),
            ("schemaName", OutputSchema.String()),
            ("displayName", OutputSchema.NullableString()),
            ("metadataId", OutputSchema.Guid()),
            ("keyAttributes", OutputSchema.Array(OutputSchema.String())),
            ("isManaged", OutputSchema.Boolean()));

        private static JObject ColumnSchema() => OutputSchema.Object(
            ("logicalName", OutputSchema.String()),
            ("schemaName", OutputSchema.String()),
            ("displayName", OutputSchema.NullableString()),
            ("metadataId", OutputSchema.Guid()),
            ("type", OutputSchema.String()),
            ("requiredLevel", OutputSchema.String()),
            ("isCustom", OutputSchema.Boolean()),
            ("isManaged", OutputSchema.Boolean()),
            ("isValidForForm", OutputSchema.Boolean()),
            ("attributeOf", OutputSchema.NullableString()),
            ("maxLength", new JObject { ["type"] = new JArray("integer", "null") }),
            ("targets", NullableArray(OutputSchema.String())),
            ("optionSet", new JObject { ["type"] = new JArray("object", "null") }));

        private static JObject OptionSchema() => OutputSchema.Object(("value", OutputSchema.Integer()), ("label", OutputSchema.NullableString()));

        private static JObject NullableArray(JObject items) => new JObject { ["type"] = new JArray("array", "null"), ["items"] = items };
    }
}
