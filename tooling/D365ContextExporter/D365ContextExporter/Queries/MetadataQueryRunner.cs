// <copyright file="MetadataQueryRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using D365ContextExporter.Models;

    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;

    /// <summary>Executes a <c>metadata</c> query using the typed Dataverse metadata SDK.</summary>
    internal sealed class MetadataQueryRunner
    {
        private readonly IOrganizationService service;
        private readonly Action<string> log;

        /// <summary>Initializes a new instance of the <see cref="MetadataQueryRunner"/> class.</summary>
        public MetadataQueryRunner(IOrganizationService service, Action<string> log)
        {
            this.service = service;
            this.log = log;
        }

        /// <summary>Dispatches to the appropriate metadata API based on <c>query.MetadataTarget</c>.</summary>
        public IReadOnlyList<Dictionary<string, object?>> Run(QueryDefinition query, CancellationToken cancellationToken)
        {
            this.log($"[metadata] Executing query '{query.Id}' (target: {query.MetadataTarget})...");

            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Dictionary<string, object?>> results = query.MetadataTarget switch
            {
                "entities" => this.RetrieveEntities(),
                "attributes" => this.RetrieveAttributes(),
                "optionsets" => this.RetrieveOptionSets(),
                "relationships" => this.RetrieveRelationships(),
                _ => throw new NotSupportedException($"Unknown metadataTarget '{query.MetadataTarget}' for query '{query.Id}'."),
            };

            this.log($"[metadata] '{query.Id}': complete, {results.Count} records.");
            return results;
        }

        private IReadOnlyList<Dictionary<string, object?>> RetrieveEntities()
        {
            var response = (RetrieveAllEntitiesResponse)this.service.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = false,
            });

            return response.EntityMetadata.Select(em => new Dictionary<string, object?>
            {
                ["logicalName"] = em.LogicalName,
                ["displayName"] = em.DisplayName?.UserLocalizedLabel?.Label,
                ["primaryIdAttribute"] = em.PrimaryIdAttribute,
                ["primaryNameAttribute"] = em.PrimaryNameAttribute,
                ["ownershipType"] = em.OwnershipType?.ToString(),
                ["isCustomEntity"] = em.IsCustomEntity,
            }).ToList();
        }

        private IReadOnlyList<Dictionary<string, object?>> RetrieveAttributes()
        {
            var response = (RetrieveAllEntitiesResponse)this.service.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = false,
            });

            return response.EntityMetadata
                .SelectMany(em => (em.Attributes ?? Array.Empty<AttributeMetadata>()).Select(am =>
                    new Dictionary<string, object?>
                    {
                        ["entityLogicalName"] = em.LogicalName,
                        ["logicalName"] = am.LogicalName,
                        ["displayName"] = am.DisplayName?.UserLocalizedLabel?.Label,
                        ["attributeType"] = am.AttributeType?.ToString(),
                        ["isCustomAttribute"] = am.IsCustomAttribute,
                        ["requiredLevel"] = am.RequiredLevel?.Value.ToString(),
                    }))
                .ToList();
        }

        private IReadOnlyList<Dictionary<string, object?>> RetrieveOptionSets()
        {
            var response = (RetrieveAllOptionSetsResponse)this.service.Execute(new RetrieveAllOptionSetsRequest());

            return response.OptionSetMetadata.Select(os => new Dictionary<string, object?>
            {
                ["name"] = os.Name,
                ["displayName"] = os.DisplayName?.UserLocalizedLabel?.Label,
                ["optionSetType"] = os.OptionSetType?.ToString(),
                ["options"] = (os as OptionSetMetadata)?.Options?
                    .Select(o => new Dictionary<string, object?>
                    {
                        ["value"] = o.Value,
                        ["label"] = o.Label?.UserLocalizedLabel?.Label,
                    }).ToList(),
            }).ToList();
        }

        private IReadOnlyList<Dictionary<string, object?>> RetrieveRelationships()
        {
            var response = (RetrieveAllEntitiesResponse)this.service.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Relationships,
                RetrieveAsIfPublished = false,
            });

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<Dictionary<string, object?>>();

            foreach (var em in response.EntityMetadata)
            {
                foreach (var r in em.OneToManyRelationships ?? Array.Empty<OneToManyRelationshipMetadata>())
                {
                    if (seen.Add(r.SchemaName))
                    {
                        results.Add(new Dictionary<string, object?>
                        {
                            ["type"] = "OneToMany",
                            ["schemaName"] = r.SchemaName,
                            ["referencedEntity"] = r.ReferencedEntity,
                            ["referencedAttribute"] = r.ReferencedAttribute,
                            ["referencingEntity"] = r.ReferencingEntity,
                            ["referencingAttribute"] = r.ReferencingAttribute,
                        });
                    }
                }

                foreach (var r in em.ManyToManyRelationships ?? Array.Empty<ManyToManyRelationshipMetadata>())
                {
                    if (seen.Add(r.SchemaName))
                    {
                        results.Add(new Dictionary<string, object?>
                        {
                            ["type"] = "ManyToMany",
                            ["schemaName"] = r.SchemaName,
                            ["entity1LogicalName"] = r.Entity1LogicalName,
                            ["entity2LogicalName"] = r.Entity2LogicalName,
                        });
                    }
                }
            }

            return results;
        }
    }
}
