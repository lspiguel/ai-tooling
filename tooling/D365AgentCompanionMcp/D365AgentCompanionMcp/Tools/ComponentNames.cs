// <copyright file="ComponentNames.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Microsoft.Xrm.Sdk.Metadata.Query;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Resolves component names where it is cheap (SPEC §6.5 <c>list_solution_components</c>): tables, columns
    /// (as <c>table.column</c>), global option sets, and the row-backed views, forms, web resources, sitemaps and
    /// app modules. One request per type per batch; table and option set maps are read once and kept. Other types stay unnamed.
    /// </summary>
    internal sealed class ComponentNames
    {
        /// <summary>Row-backed component types: their table, ID column and name column.</summary>
        public static readonly IReadOnlyDictionary<int, (string Table, string IdColumn, string NameColumn)> RowBacked = new Dictionary<int, (string, string, string)>
        {
            [ComponentTypes.SavedQuery] = ("savedquery", "savedqueryid", "name"),
            [ComponentTypes.SystemForm] = ("systemform", "formid", "name"),
            [ComponentTypes.WebResource] = ("webresource", "webresourceid", "name"),
            [ComponentTypes.SiteMap] = ("sitemap", "sitemapid", "sitemapname"),
            [ComponentTypes.AppModule] = ("appmodule", "appmoduleid", "name"),
        };

        /// <summary>The most values in one <c>In</c> condition.</summary>
        internal const int BatchSize = 200;

        private readonly IOrganizationService service;
        private Dictionary<Guid, string>? tables;
        private Dictionary<Guid, string>? optionSets;

        /// <summary>Initializes a new instance of the <see cref="ComponentNames"/> class.</summary>
        /// <param name="service">The service.</param>
        public ComponentNames(IOrganizationService service)
        {
            this.service = service;
        }

        /// <summary>Resolves the names of a batch of components.</summary>
        /// <param name="components">The components.</param>
        /// <returns>The names found, by type and ID.</returns>
        public Dictionary<(int Type, Guid Id), string> Resolve(IEnumerable<(int Type, Guid Id)> components)
        {
            var names = new Dictionary<(int, Guid), string>();
            foreach (var group in components.Distinct().GroupBy(c => c.Type))
            {
                var ids = group.Select(c => c.Id).ToList();
                switch (group.Key)
                {
                    case ComponentTypes.Entity:
                        this.tables ??= ((RetrieveAllEntitiesResponse)this.service.Execute(new RetrieveAllEntitiesRequest { EntityFilters = EntityFilters.Entity }))
                            .EntityMetadata.Where(e => e.MetadataId.HasValue).ToDictionary(e => e.MetadataId!.Value, e => e.LogicalName);
                        Add(names, group.Key, ids, this.tables);
                        break;
                    case ComponentTypes.OptionSet:
                        this.optionSets ??= ((RetrieveAllOptionSetsResponse)this.service.Execute(new RetrieveAllOptionSetsRequest()))
                            .OptionSetMetadata.Where(o => o.MetadataId.HasValue).ToDictionary(o => o.MetadataId!.Value, o => o.Name);
                        Add(names, group.Key, ids, this.optionSets);
                        break;
                    case ComponentTypes.Attribute:
                        foreach (var batch in Batches(ids))
                        {
                            Add(names, group.Key, batch, this.Columns(batch));
                        }

                        break;
                    default:
                        if (RowBacked.TryGetValue(group.Key, out var row))
                        {
                            foreach (var batch in Batches(ids))
                            {
                                Add(names, group.Key, batch, this.Rows(row, batch));
                            }
                        }

                        break;
                }
            }

            return names;
        }

        /// <summary>Splits IDs into batches for <c>In</c> conditions.</summary>
        /// <param name="ids">The IDs.</param>
        /// <returns>The batches.</returns>
        internal static IEnumerable<List<Guid>> Batches(IReadOnlyList<Guid> ids)
        {
            for (var i = 0; i < ids.Count; i += BatchSize)
            {
                yield return ids.Skip(i).Take(BatchSize).ToList();
            }
        }

        private static void Add(Dictionary<(int, Guid), string> names, int type, IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, string> found)
        {
            foreach (var id in ids)
            {
                if (found.TryGetValue(id, out var name) && !string.IsNullOrEmpty(name))
                {
                    names[(type, id)] = name;
                }
            }
        }

        private Dictionary<Guid, string> Columns(List<Guid> ids)
        {
            var criteria = new MetadataFilterExpression(LogicalOperator.And);
            criteria.Conditions.Add(new MetadataConditionExpression("MetadataId", MetadataConditionOperator.In, ids.ToArray()));
            var request = new RetrieveMetadataChangesRequest
            {
                Query = new EntityQueryExpression
                {
                    // Dataverse refuses an attribute query unless Attributes is among the entity properties (0x80072527).
                    Properties = new MetadataPropertiesExpression("LogicalName", "Attributes"),
                    AttributeQuery = new AttributeQueryExpression { Criteria = criteria, Properties = new MetadataPropertiesExpression("LogicalName") },
                },
            };

            var response = (RetrieveMetadataChangesResponse)this.service.Execute(request);
            return response.EntityMetadata
                .SelectMany(e => (e.Attributes ?? Array.Empty<AttributeMetadata>()).Select(a => (Entity: e.LogicalName, Attribute: a)))
                .Where(c => c.Attribute.MetadataId.HasValue)
                .GroupBy(c => c.Attribute.MetadataId!.Value)
                .ToDictionary(g => g.Key, g => $"{g.First().Entity}.{g.First().Attribute.LogicalName}");
        }

        private Dictionary<Guid, string> Rows((string Table, string IdColumn, string NameColumn) row, List<Guid> ids)
        {
            var query = new QueryExpression(row.Table) { ColumnSet = new ColumnSet(row.NameColumn) };
            query.Criteria.AddCondition(row.IdColumn, ConditionOperator.In, ids.Cast<object>().ToArray());
            return this.service.RetrieveMultiple(query).Entities
                .GroupBy(e => e.Id)
                .ToDictionary(g => g.Key, g => g.First().GetAttributeValue<string>(row.NameColumn));
        }
    }
}
