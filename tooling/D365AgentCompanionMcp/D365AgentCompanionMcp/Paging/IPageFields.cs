// <copyright file="IPageFields.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Result fields a page source adds beside its items — for example the <c>table</c> of
    /// <c>get_table_metadata</c> on the first page, or the <c>solution</c> of <c>list_solution_components</c> on every page.
    /// </summary>
    internal interface IPageFields
    {
        /// <summary>Gets the fields on every page, or <c>null</c>.</summary>
        JObject? EveryPage { get; }

        /// <summary>Gets the fields on the first page only, or <c>null</c>.</summary>
        JObject? FirstPage { get; }

        /// <summary>Gets a value indicating whether the item array is left out of the result, as when a caller asked for none.</summary>
        bool OmitItems { get; }
    }
}
