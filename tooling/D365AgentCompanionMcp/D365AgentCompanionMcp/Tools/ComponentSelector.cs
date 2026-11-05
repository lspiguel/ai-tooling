// <copyright file="ComponentSelector.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A <c>ComponentSelector</c> (SPEC §6.6 <c>add_solution_components</c>): a component by type and ID, or a table,
    /// column, global option set, relationship or plug-in assembly by name; and, from Phase 6, a custom API, request
    /// parameter or response property by <c>componentTable</c>, whose type code differs between environments. Shared by
    /// <c>get_required_components</c> and the Write tools.
    /// </summary>
    internal sealed class ComponentSelector
    {
        /// <summary>The custom API table.</summary>
        public const string CustomApiTable = "customapi";

        /// <summary>The custom API request parameter table.</summary>
        public const string CustomApiRequestParameterTable = "customapirequestparameter";

        /// <summary>The custom API response property table.</summary>
        public const string CustomApiResponsePropertyTable = "customapiresponseproperty";

        /// <summary>The lowest type code the platform assigns per environment (SPEC §6.6): the same number means something else elsewhere.</summary>
        public const int FirstEnvironmentSpecificType = 10000;

        private static readonly string[] PluginTables = { CustomApiTable, CustomApiRequestParameterTable, CustomApiResponsePropertyTable };

        private ComponentSelector(int index, int type, Guid? id, string? table, string? name)
        {
            this.Index = index;
            this.Type = type;
            this.Id = id;
            this.Table = table;
            this.Name = name;
        }

        /// <summary>Gets the position in the request.</summary>
        public int Index { get; }

        /// <summary>Gets the component type code; 0 for a selector by <see cref="ComponentTable"/>, whose code is read per environment.</summary>
        public int Type { get; }

        /// <summary>Gets the ID, when the selector gives one.</summary>
        public Guid? Id { get; }

        /// <summary>Gets the table of a column selected by name.</summary>
        public string? Table { get; }

        /// <summary>Gets the name, when the selector gives one — for a custom API, request parameter or response property, its unique name.</summary>
        public string? Name { get; }

        /// <summary>Gets the table of a custom API component, which makes the selector one by <c>componentTable</c>; <c>null</c> otherwise.</summary>
        public string? ComponentTable { get; private set; }

        /// <summary>Gets the unique name of the custom API a request parameter or response property belongs to.</summary>
        public string? CustomApi { get; private set; }

        /// <summary>Gets the version of a plug-in assembly selected by name, when the caller gave one.</summary>
        public string? Version { get; private set; }

        /// <summary>The input schema of one selector.</summary>
        /// <returns>The schema.</returns>
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["description"] = "{type, id} for any type; or {type: 1, logicalName} a table; {type: 2, table, logicalName} a column; {type: 9, name} a global option set; {type: 10, schemaName} a relationship; {type: 91, name, version?} a plug-in assembly (version when several are registered under the name); {type: 92, id} a plug-in step, by ID only. Custom APIs have a type code that differs per environment: {componentTable: \"customapi\", id | uniqueName}, {componentTable: \"customapirequestparameter\" | \"customapiresponseproperty\", id | customApi + uniqueName}. A numeric type of 10000 or above is refused.",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "integer" },
                ["id"] = new JObject { ["type"] = "string", ["format"] = "uuid" },
                ["logicalName"] = new JObject { ["type"] = "string" },
                ["table"] = new JObject { ["type"] = "string" },
                ["name"] = new JObject { ["type"] = "string" },
                ["schemaName"] = new JObject { ["type"] = "string" },
                ["version"] = new JObject { ["type"] = "string" },
                ["componentTable"] = new JObject { ["type"] = "string", ["enum"] = new JArray(PluginTables) },
                ["customApi"] = new JObject { ["type"] = "string" },
                ["uniqueName"] = new JObject { ["type"] = "string" },
            },
            ["additionalProperties"] = false,
        };

        /// <summary>Parses one selector; any problem is <c>invalid_argument</c> naming the entry.</summary>
        /// <param name="argument">The argument name, for errors.</param>
        /// <param name="index">The entry's position.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>The selector.</returns>
        public static ComponentSelector Parse(string argument, int index, JToken entry)
        {
            var at = $"{argument}[{index}]";
            if (!(entry is JObject selector))
            {
                throw ToolException.InvalidArgument(at, $"{at} must be an object with an integer type, or a componentTable.");
            }

            string? Optional(string field) => selector[field]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)selector[field])
                ? ((string)selector[field]!).Trim()
                : null;

            if (selector["componentTable"] != null)
            {
                return ParseTable(at, index, selector, Optional);
            }

            if (selector["type"]?.Type != JTokenType.Integer)
            {
                throw ToolException.InvalidArgument(at, $"{at} must be an object with an integer type, or a componentTable.");
            }

            var type = (int)selector["type"]!;
            if (type >= FirstEnvironmentSpecificType)
            {
                throw ToolException.InvalidArgument(
                    at,
                    $"{at}: type {type.ToString(CultureInfo.InvariantCulture)} is a code the platform assigns per environment, so the same number means something else elsewhere. Select a custom API, request parameter or response property with componentTable.");
            }

            if (selector["id"] != null)
            {
                return new ComponentSelector(index, type, ParseGuid(at, selector["id"]!), null, null);
            }

            string Text(string field) => Optional(field) ?? throw ToolException.InvalidArgument(at, $"{at}: type {type} without id needs {field}.");

            switch (type)
            {
                case ComponentTypes.Entity:
                    return new ComponentSelector(index, type, null, null, Text("logicalName").ToLowerInvariant());
                case ComponentTypes.Attribute:
                    return new ComponentSelector(index, type, null, Text("table").ToLowerInvariant(), Text("logicalName").ToLowerInvariant());
                case ComponentTypes.OptionSet:
                    return new ComponentSelector(index, type, null, null, Text("name"));
                case ComponentTypes.EntityRelationship:
                    return new ComponentSelector(index, type, null, null, Text("schemaName"));
                case ComponentTypes.PluginAssembly:
                    return new ComponentSelector(index, type, null, null, Text("name")) { Version = Optional("version") };
                default:
                    throw ToolException.InvalidArgument(at, $"{at}: type {type} must be selected by id. Only tables (1), columns (2), global option sets (9), relationships (10) and plug-in assemblies (91) can be selected by name; custom APIs by componentTable.");
            }
        }

        /// <summary>Parses a GUID argument, in any case, with or without braces (SPEC §6.2).</summary>
        /// <param name="argument">The argument name, for errors.</param>
        /// <param name="value">The value.</param>
        /// <returns>The GUID.</returns>
        public static Guid ParseGuid(string argument, JToken value)
        {
            if (value.Type == JTokenType.String && Guid.TryParse((string)value!, out var id))
            {
                return id;
            }

            throw ToolException.InvalidArgument(argument, $"{argument}: id must be a GUID.");
        }

        /// <summary>
        /// Resolves the selector to a component type and ID, with a name where it can be known. A selector by
        /// <c>componentTable</c> reads the type code from the connection's organization, because it is assigned per
        /// environment (SPEC §6.6); a plug-in assembly selected by name must be unambiguous.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <returns>The type, ID and name; <c>not_found</c> when the named component does not exist.</returns>
        public (int Type, Guid Id, string? Name) Resolve(IOrganizationService service)
        {
            if (this.ComponentTable != null)
            {
                return this.ResolveCustomApiComponent(service);
            }

            if (this.Id.HasValue)
            {
                return (this.Type, this.Id.Value, null);
            }

            switch (this.Type)
            {
                case ComponentTypes.Entity:
                    return (this.Type, MetadataHelpers.RetrieveEntity(service, this.Name!, EntityFilters.Entity).MetadataId!.Value, this.Name);
                case ComponentTypes.Attribute:
                    var attribute = Retrieve<RetrieveAttributeResponse>(service, new RetrieveAttributeRequest { EntityLogicalName = this.Table, LogicalName = this.Name }, "column", $"{this.Table}.{this.Name}");
                    return (this.Type, attribute.AttributeMetadata.MetadataId!.Value, $"{this.Table}.{this.Name}");
                case ComponentTypes.OptionSet:
                    var optionSet = Retrieve<RetrieveOptionSetResponse>(service, new RetrieveOptionSetRequest { Name = this.Name }, "global option set", this.Name!);
                    return (this.Type, optionSet.OptionSetMetadata.MetadataId!.Value, optionSet.OptionSetMetadata.Name);
                case ComponentTypes.PluginAssembly:
                    return this.ResolveAssembly(service);
                default:
                    var relationship = Retrieve<RetrieveRelationshipResponse>(service, new RetrieveRelationshipRequest { Name = this.Name }, "relationship", this.Name!);
                    return (this.Type, relationship.RelationshipMetadata.MetadataId!.Value, relationship.RelationshipMetadata.SchemaName);
            }
        }

        /// <summary>Reads the component type code a custom API table has in this environment, from <c>solutioncomponentdefinition</c>.</summary>
        /// <param name="service">The service.</param>
        /// <param name="table">The table's logical name.</param>
        /// <returns>The code.</returns>
        internal static int TypeCodeOf(IOrganizationService service, string table)
        {
            var query = new QueryExpression("solutioncomponentdefinition") { ColumnSet = new ColumnSet("solutioncomponenttype", "primaryentityname"), TopCount = 1 };
            query.Criteria.AddCondition("primaryentityname", ConditionOperator.Equal, table);
            var row = service.RetrieveMultiple(query).Entities.FirstOrDefault()
                ?? throw new ToolException(
                    ToolErrorCodes.NotFound,
                    $"This environment has no solution component type for the table '{table}', so it cannot be added to a solution here.",
                    new JObject { ["kind"] = "solution component type", ["name"] = table });
            return row.GetAttributeValue<int>("solutioncomponenttype");
        }

        private static ComponentSelector ParseTable(string at, int index, JObject selector, Func<string, string?> optional)
        {
            var table = optional("componentTable");
            if (table == null || !PluginTables.Contains(table))
            {
                throw ToolException.InvalidArgument(at, $"{at}: componentTable must be one of {string.Join(", ", PluginTables)}.");
            }

            if (selector["type"] != null)
            {
                throw ToolException.InvalidArgument(at, $"{at}: a selector by componentTable has no numeric type; its code is read from the environment.");
            }

            ComponentSelector Make(Guid? id, string? customApi, string? uniqueName) =>
                new ComponentSelector(index, 0, id, null, uniqueName) { ComponentTable = table, CustomApi = customApi };

            if (selector["id"] != null)
            {
                return Make(ParseGuid(at, selector["id"]!), null, null);
            }

            var unique = optional("uniqueName") ?? throw ToolException.InvalidArgument(at, $"{at}: {table} without id needs uniqueName.");
            if (table == CustomApiTable)
            {
                return Make(null, null, unique);
            }

            var customApi = optional("customApi") ?? throw ToolException.InvalidArgument(at, $"{at}: {table} without id needs customApi (the custom API's unique name) and uniqueName.");
            return Make(null, customApi, unique);
        }

        private static T Retrieve<T>(IOrganizationService service, OrganizationRequest request, string kind, string name)
            where T : OrganizationResponse
        {
            try
            {
                return (T)service.Execute(request);
            }
            catch (Exception ex) when (MetadataHelpers.IsNotFound(ex))
            {
                throw MetadataHelpers.NotFound(kind, name);
            }
        }

        private static Entity? FirstRow(IOrganizationService service, string table, string[] columns, params (string Column, object Value)[] conditions)
        {
            var query = new QueryExpression(table) { ColumnSet = new ColumnSet(columns), TopCount = 2 };
            foreach (var (column, value) in conditions)
            {
                query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
            }

            return service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        private (int Type, Guid Id, string? Name) ResolveAssembly(IOrganizationService service)
        {
            var query = new QueryExpression("pluginassembly") { ColumnSet = new ColumnSet("name", "version") };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, this.Name);
            var found = service.RetrieveMultiple(query).Entities.ToList();
            if (found.Count == 0)
            {
                throw MetadataHelpers.NotFound("plug-in assembly", this.Name!);
            }

            if (this.Version != null)
            {
                found = found.Where(a => string.Equals(a.GetAttributeValue<string>("version"), this.Version, StringComparison.OrdinalIgnoreCase)).ToList();
                if (found.Count == 0)
                {
                    throw MetadataHelpers.NotFound("plug-in assembly", $"{this.Name} {this.Version}");
                }
            }

            if (found.Count > 1)
            {
                var versions = found.Select(a => a.GetAttributeValue<string>("version")).OrderBy(v => v, StringComparer.Ordinal).ToList();
                throw new ToolException(
                    ToolErrorCodes.InvalidArgument,
                    $"components[{this.Index}]: {versions.Count} plug-in assemblies are registered as '{this.Name}' (versions {string.Join(", ", versions)}). Add a version.",
                    new JObject { ["argument"] = $"components[{this.Index}]", ["versions"] = new JArray(versions) });
            }

            var assembly = found[0];
            return (this.Type, assembly.Id, $"{assembly.GetAttributeValue<string>("name")} {assembly.GetAttributeValue<string>("version")}");
        }

        private (int Type, Guid Id, string? Name) ResolveCustomApiComponent(IOrganizationService service)
        {
            var table = this.ComponentTable!;
            var code = TypeCodeOf(service, table);
            var label = table == CustomApiTable ? "custom API" : table == CustomApiRequestParameterTable ? "custom API request parameter" : "custom API response property";

            if (this.Id.HasValue)
            {
                var byId = FirstRow(service, table, new[] { "uniquename" }, (table + "id", this.Id.Value))
                    ?? throw MetadataHelpers.NotFound(label, ValueFormats.FormatGuid(this.Id.Value));
                return (code, byId.Id, byId.GetAttributeValue<string>("uniquename"));
            }

            if (table == CustomApiTable)
            {
                var api = FirstRow(service, table, new[] { "uniquename" }, ("uniquename", this.Name!))
                    ?? throw MetadataHelpers.NotFound(label, this.Name!);
                return (code, api.Id, api.GetAttributeValue<string>("uniquename"));
            }

            var owner = FirstRow(service, CustomApiTable, new[] { "uniquename" }, ("uniquename", this.CustomApi!))
                ?? throw MetadataHelpers.NotFound("custom API", this.CustomApi!);
            var row = FirstRow(service, table, new[] { "uniquename" }, ("customapiid", owner.Id), ("uniquename", this.Name!))
                ?? throw MetadataHelpers.NotFound(label, $"{this.CustomApi}.{this.Name}");
            return (code, row.Id, $"{this.CustomApi}.{row.GetAttributeValue<string>("uniquename")}");
        }
    }
}
