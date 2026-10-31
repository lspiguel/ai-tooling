// <copyright file="SnapshotPageSource.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Pages items read once, on the first call, and held in memory for the cursor's lifetime — metadata and XML
    /// payloads (SPEC §6.2: one snapshot per paged call; nothing is written to disk).
    /// </summary>
    internal sealed class SnapshotPageSource : IPageSource, IPageFields
    {
        private readonly Queue<JObject> items;

        /// <summary>Initializes a new instance of the <see cref="SnapshotPageSource"/> class.</summary>
        /// <param name="items">The items, in result order.</param>
        /// <param name="everyPage">Fields on every page, or <c>null</c>.</param>
        /// <param name="firstPage">Fields on the first page only, or <c>null</c>.</param>
        /// <param name="omitItems">Whether the item array is left out of the result.</param>
        public SnapshotPageSource(IEnumerable<JObject> items, JObject? everyPage = null, JObject? firstPage = null, bool omitItems = false)
        {
            this.items = new Queue<JObject>(items);
            this.EveryPage = everyPage;
            this.FirstPage = firstPage;
            this.OmitItems = omitItems;
        }

        /// <inheritdoc/>
        public bool Exhausted => this.items.Count == 0;

        /// <inheritdoc/>
        public JObject? EveryPage { get; }

        /// <inheritdoc/>
        public JObject? FirstPage { get; }

        /// <inheritdoc/>
        public bool OmitItems { get; }

        /// <summary>Creates a source from items already projected.</summary>
        /// <param name="items">The items.</param>
        /// <returns>The source.</returns>
        public static SnapshotPageSource Of(IEnumerable<JObject> items) => new SnapshotPageSource(items.ToList());

        /// <inheritdoc/>
        public Task<IReadOnlyList<JObject>> FetchAsync(int batchSize)
        {
            var batch = new List<JObject>();
            while (batch.Count < batchSize && this.items.Count > 0)
            {
                batch.Add(this.items.Dequeue());
            }

            return Task.FromResult<IReadOnlyList<JObject>>(batch);
        }
    }
}
