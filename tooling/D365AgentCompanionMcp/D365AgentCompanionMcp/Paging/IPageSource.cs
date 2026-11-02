// <copyright file="IPageSource.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    /// <summary>Where a paged tool's items come from — Dataverse pages for records, a snapshot for metadata.</summary>
    internal interface IPageSource
    {
        /// <summary>Gets a value indicating whether every item has been fetched.</summary>
        bool Exhausted { get; }

        /// <summary>Fetches the next batch of items.</summary>
        /// <param name="batchSize">How many items to ask for.</param>
        /// <returns>The items, possibly none.</returns>
        Task<IReadOnlyList<JObject>> FetchAsync(int batchSize);
    }
}
