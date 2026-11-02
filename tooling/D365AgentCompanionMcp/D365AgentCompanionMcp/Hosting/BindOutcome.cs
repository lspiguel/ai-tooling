// <copyright file="BindOutcome.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    /// <summary>The result of completing a binding after the identity call (C3).</summary>
    internal enum BindOutcome
    {
        /// <summary>Bound.</summary>
        Bound,

        /// <summary>Another tab is bound to the same organization; the claim was released.</summary>
        OrganizationConflict,

        /// <summary>The claim ended first — the tab closed or changed connection while the identity call ran.</summary>
        Abandoned,
    }
}
