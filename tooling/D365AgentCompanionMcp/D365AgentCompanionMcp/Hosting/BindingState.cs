// <copyright file="BindingState.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    /// <summary>Where a connection is in its binding (C3).</summary>
    internal enum BindingState
    {
        /// <summary>The connection ID is claimed; the organization is not yet known. Not listed, not routable.</summary>
        Claimed,

        /// <summary>Bound: listed by <c>list_connections</c> and accepting calls.</summary>
        Bound,

        /// <summary>The binding ended — the tab closed or changed connection, or lost a conflict.</summary>
        Ended,
    }
}
