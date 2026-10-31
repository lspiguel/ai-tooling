// <copyright file="SetupView.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Setup
{
    /// <summary>What a tab shows before its main panel (SPEC §8.7).</summary>
    internal enum SetupView
    {
        /// <summary>Setup was completed for this version: the main panel at once.</summary>
        None,

        /// <summary>No version recorded: the three-page wizard.</summary>
        FirstRun,

        /// <summary>A different version recorded: the single <b>Plugin updated</b> page.</summary>
        Updated,
    }
}
