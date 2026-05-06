// <copyright file="FetchXmlQueryRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Queries
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Xml.Linq;

    using D365ContextExporter.Helpers;
    using D365ContextExporter.Models;

    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>Executes a <c>fetchxml</c> query against Dataverse with automatic server-side paging.</summary>
    internal sealed class FetchXmlQueryRunner
    {
        private const int PageSize = 5000;

        private readonly IOrganizationService service;
        private readonly Action<string> log;

        /// <summary>Initializes a new instance of the <see cref="FetchXmlQueryRunner"/> class.</summary>
        public FetchXmlQueryRunner(IOrganizationService service, Action<string> log)
        {
            this.service = service;
            this.log = log;
        }

        /// <summary>Executes the query and returns all matching entities, paging transparently.</summary>
        /// <param name="query">The query definition (must have <c>Type == "fetchxml"</c>).</param>
        /// <param name="baseDir">Project base directory; resolved to <c>config/queries/</c> for the FetchXML file.</param>
        /// <param name="cancellationToken">Cancellation token checked between pages.</param>
        /// <returns>All retrieved entities, up to <c>query.MaxRecords</c> if set.</returns>
        public IReadOnlyList<Entity> Run(QueryDefinition query, string baseDir, CancellationToken cancellationToken)
        {
            this.log($"[fetchxml] Executing query '{query.Id}'...");

            var queriesDir = Path.Combine(baseDir, "config", "queries");
            var filePath = PathResolver.Resolve(query.Source!, queriesDir);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"FetchXML file not found for query '{query.Id}'.", filePath);
            }

            var originalXml = File.ReadAllText(filePath);
            var records = new List<Entity>();
            var page = 1;
            string? pagingCookie = null;
            bool moreRecords;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageXml = BuildPagedFetchXml(originalXml, page, PageSize, pagingCookie);
                var result = this.service.RetrieveMultiple(new FetchExpression(pageXml));

                records.AddRange(result.Entities);
                moreRecords = result.MoreRecords;
                pagingCookie = result.PagingCookie;
                page++;

                this.log($"[fetchxml] '{query.Id}': page {page - 1} retrieved {result.Entities.Count} records (total so far: {records.Count}).");

                if (query.MaxRecords.HasValue && records.Count >= query.MaxRecords.Value)
                {
                    break;
                }
            }
            while (moreRecords);

            this.log($"[fetchxml] '{query.Id}': complete, {records.Count} total records.");
            return records;
        }

        private static string BuildPagedFetchXml(string originalXml, int page, int count, string? pagingCookie)
        {
            var doc = XDocument.Parse(originalXml);
            var root = doc.Root!;
            root.SetAttributeValue("page", page);
            root.SetAttributeValue("count", count);

            if (pagingCookie != null)
            {
                root.SetAttributeValue("paging-cookie", pagingCookie);
            }
            else
            {
                root.Attribute("paging-cookie")?.Remove();
            }

            return doc.ToString();
        }
    }
}
