// <copyright file="FetchXmlPageSource.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Pages a FetchXML query with paging cookies. The <c>page</c>, <c>count</c> and <c>paging-cookie</c> attributes
    /// are set here on every request, whatever the caller supplied (SPEC §6.5 <c>query</c>).
    /// </summary>
    internal sealed class FetchXmlPageSource : IPageSource, IPageFields
    {
        private readonly IOrganizationService service;
        private readonly XDocument fetch;
        private readonly Func<Entity, JObject> map;
        private string? pagingCookie;
        private int pageNumber = 1;

        /// <summary>Initializes a new instance of the <see cref="FetchXmlPageSource"/> class.</summary>
        /// <param name="service">The connection's service.</param>
        /// <param name="fetch">The validated FetchXML; its paging attributes are overwritten.</param>
        /// <param name="map">Projects a row to a result item.</param>
        /// <param name="everyPage">Fields on every page.</param>
        public FetchXmlPageSource(IOrganizationService service, XDocument fetch, Func<Entity, JObject> map, JObject everyPage)
        {
            this.service = service;
            this.fetch = fetch;
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
            var root = this.fetch.Root!;
            root.SetAttributeValue("count", batchSize.ToString(CultureInfo.InvariantCulture));
            root.SetAttributeValue("page", this.pageNumber.ToString(CultureInfo.InvariantCulture));
            root.SetAttributeValue("paging-cookie", this.pagingCookie);

            var page = this.service.RetrieveMultiple(new FetchExpression(this.fetch.ToString(SaveOptions.DisableFormatting)));
            this.pagingCookie = page.PagingCookie;
            this.pageNumber++;
            this.Exhausted = !page.MoreRecords;
            return (IReadOnlyList<JObject>)page.Entities.Select(this.map).ToList();
        });
    }
}
