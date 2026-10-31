// <copyright file="RecordPageSource.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>Pages Dataverse rows with paging cookies, so every page continues the same query (SPEC §6.2).</summary>
    internal sealed class RecordPageSource : IPageSource, IPageFields
    {
        private readonly IOrganizationService service;
        private readonly QueryExpression query;
        private readonly Func<IReadOnlyList<Entity>, IEnumerable<JObject>> map;
        private string? pagingCookie;
        private int pageNumber = 1;

        /// <summary>Initializes a new instance of the <see cref="RecordPageSource"/> class.</summary>
        /// <param name="service">The connection's service.</param>
        /// <param name="query">The query; its paging is set here.</param>
        /// <param name="map">Projects a row to a result item.</param>
        public RecordPageSource(IOrganizationService service, QueryExpression query, Func<Entity, JObject> map)
            : this(service, query, rows => rows.Select(map))
        {
        }

        /// <summary>Initializes a new instance of the <see cref="RecordPageSource"/> class, projecting each batch as a whole.</summary>
        /// <param name="service">The connection's service.</param>
        /// <param name="query">The query; its paging is set here.</param>
        /// <param name="map">Projects a batch of rows to result items, in order — for lookups done once per batch.</param>
        /// <param name="everyPage">Fields on every page, or <c>null</c>.</param>
        public RecordPageSource(IOrganizationService service, QueryExpression query, Func<IReadOnlyList<Entity>, IEnumerable<JObject>> map, JObject? everyPage = null)
        {
            this.service = service;
            this.query = query;
            this.map = map;
            this.EveryPage = everyPage;
        }

        /// <inheritdoc/>
        public bool Exhausted { get; private set; }

        /// <inheritdoc/>
        public JObject? EveryPage { get; }

        /// <inheritdoc/>
        public JObject? FirstPage => null;

        /// <inheritdoc/>
        public bool OmitItems => false;

        /// <inheritdoc/>
        public Task<IReadOnlyList<JObject>> FetchAsync(int batchSize) => Task.Run(() =>
        {
            this.query.PageInfo = new PagingInfo { Count = batchSize, PageNumber = this.pageNumber, PagingCookie = this.pagingCookie };
            var page = this.service.RetrieveMultiple(this.query);
            this.pagingCookie = page.PagingCookie;
            this.pageNumber++;
            this.Exhausted = !page.MoreRecords;
            return (IReadOnlyList<JObject>)this.map(page.Entities.ToList()).ToList();
        });
    }
}
