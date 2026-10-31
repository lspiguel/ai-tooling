// <copyright file="Pager.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Pages one paged call (SPEC §6.2): up to the page size, ending early at a whole item when the next one would
    /// take the text content block past the response cap. Items left over are kept for the next page.
    /// </summary>
    internal sealed class Pager
    {
        /// <summary>Stands in for a cursor while measuring, as long as any cursor <see cref="CursorStore"/> issues.</summary>
        private static readonly string CursorPlaceholder = new string('0', CursorStore.CursorLength);

        private readonly IPageSource source;
        private readonly Queue<JObject> buffer = new Queue<JObject>();

        /// <summary>Initializes a new instance of the <see cref="Pager"/> class.</summary>
        /// <param name="source">Where the items come from.</param>
        /// <param name="pageSize">Items per page, fixed by the first call.</param>
        public Pager(IPageSource source, int pageSize)
        {
            this.source = source;
            this.PageSize = pageSize;
        }

        /// <summary>Gets the page size.</summary>
        public int PageSize { get; }

        /// <summary>Gets where the items come from.</summary>
        public IPageSource Source => this.source;

        /// <summary>Gets the number of pages taken so far.</summary>
        public int PagesTaken { get; private set; }

        /// <summary>Gets a value indicating whether another page follows.</summary>
        public bool HasMore => this.buffer.Count > 0 || !this.source.Exhausted;

        /// <summary>Takes the next page.</summary>
        /// <param name="responseCap">The cap on the result's text content block, in characters.</param>
        /// <param name="build">Builds the result from a page of items and a cursor.</param>
        /// <returns>The page's items.</returns>
        public async Task<JArray> NextPageAsync(int responseCap, Func<JArray, string?, JObject> build)
        {
            var items = new JArray();
            var length = build(new JArray(), CursorPlaceholder).ToString(Formatting.None).Length;
            while (items.Count < this.PageSize)
            {
                if (this.buffer.Count == 0 && !await this.FillAsync().ConfigureAwait(false))
                {
                    break;
                }

                var itemLength = this.buffer.Peek().ToString(Formatting.None).Length + (items.Count > 0 ? 1 : 0);
                if (items.Count > 0 && length + itemLength > responseCap)
                {
                    break;
                }

                items.Add(this.buffer.Dequeue());
                length += itemLength;
            }

            // Look ahead, so that the last page says so instead of handing out a cursor to an empty page.
            if (this.buffer.Count == 0 && !this.source.Exhausted)
            {
                await this.FillAsync().ConfigureAwait(false);
            }

            this.PagesTaken++;
            return items;
        }

        private async Task<bool> FillAsync()
        {
            while (this.buffer.Count == 0 && !this.source.Exhausted)
            {
                foreach (var item in await this.source.FetchAsync(this.PageSize).ConfigureAwait(false))
                {
                    this.buffer.Enqueue(item);
                }
            }

            return this.buffer.Count > 0;
        }
    }
}
