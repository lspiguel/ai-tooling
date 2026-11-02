// <copyright file="IHostInstance.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

    /// <summary>What the host needs from one open plugin tab (an instance): its approvals and its call log (C8, C9).</summary>
    internal interface IHostInstance : IApprovalPrompt
    {
        /// <summary>Gets a short label for the tab, recorded with the grants it gives.</summary>
        string TabLabel { get; }

        /// <summary>Gets the tab's call log.</summary>
        CallLog CallLog { get; }
    }
}
