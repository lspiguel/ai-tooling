// <copyright file="ComponentSelector.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A <c>ComponentSelector</c> (SPEC §6.6 <c>add_solution_components</c>): a component by type and ID, or a table,
    /// column, global option set or relationship by name. Shared by <c>get_required_components</c> and the Write tools.
    /// </summary>
    internal sealed class ComponentSelector
    {
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

        /// <summary>Gets the component type code.</summary>
        public int Type { get; }

        /// <summary>Gets the ID, when the selector gives one.</summary>
        public Guid? Id { get; }

        /// <summary>Gets the table of a column selected by name.</summary>
        public string? Table { get; }

        /// <summary>Gets the name, when the selector gives one.</summary>
        public string? Name { get; }

        /// <summary>The input schema of one selector.</summary>
        /// <returns>The schema.</returns>
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["description"] = "{type, id} for any type; or {type: 1, logicalName} a table; {type: 2, table, logicalName} a column; {type: 9, name} a global option set; {type: 10, schemaName} a relationship.",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "integer" },
                ["id"] = new JObject { ["type"] = "string", ["format"] = "uuid" },
                ["logicalName"] = new JObject { ["type"] = "string" },
                ["table"] = new JObject { ["type"] = "string" },
                ["name"] = new JObject { ["type"] = "string" },
                ["schemaName"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("type"),
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
            if (!(entry is JObject selector) || selector["type"]?.Type != JTokenType.Integer)
            {
                throw ToolException.InvalidArgument(at, $"{at} must be an object with an integer type.");
            }

            var type = (int)selector["type"]!;
            if (selector["id"] != null)
            {
                return new ComponentSelector(index, type, ParseGuid(at, selector["id"]!), null, null);
            }

            string Text(string field) => selector[field]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)selector[field])
                ? ((string)selector[field]!).Trim()
                : throw ToolException.InvalidArgument(at, $"{at}: type {type} without id needs {field}.");

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
                default:
                    throw ToolException.InvalidArgument(at, $"{at}: type {type} must be selected by id. Only tables (1), columns (2), global option sets (9) and relationships (10) can be selected by name.");
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

        /// <summary>Resolves the selector to a component ID, with a name where the selector gives one.</summary>
        /// <param name="service">The service.</param>
        /// <returns>The type, ID and name; <c>not_found</c> when the named component does not exist.</returns>
        public (int Type, Guid Id, string? Name) Resolve(IOrganizationService service)
        {
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
                default:
                    var relationship = Retrieve<RetrieveRelationshipResponse>(service, new RetrieveRelationshipRequest { Name = this.Name }, "relationship", this.Name!);
                    return (this.Type, relationship.RelationshipMetadata.MetadataId!.Value, relationship.RelationshipMetadata.SchemaName);
            }
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
    }
}
