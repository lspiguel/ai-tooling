// <copyright file="ApprovalRequest.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;
    using System.Collections.Generic;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

    /// <summary>What an approval dialog shows (SPEC §8.2).</summary>
    internal sealed class ApprovalRequest
    {
        /// <summary>Gets the tool name.</summary>
        public string Tool { get; init; } = string.Empty;

        /// <summary>Gets the tool's tier.</summary>
        public ToolTier Tier { get; init; }

        /// <summary>Gets the connection name, as in the XrmToolBox connection manager.</summary>
        public string ConnectionName { get; init; } = string.Empty;

        /// <summary>Gets the organization's friendly name.</summary>
        public string OrganizationName { get; init; } = string.Empty;

        /// <summary>Gets the arguments, verbatim and pretty-printed, without <c>reason</c>.</summary>
        public string Arguments { get; init; } = string.Empty;

        /// <summary>Gets what the call will do, resolved to names — empty for tools that do not prepare.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>Gets the text the person must type to approve — the prefix of a new publisher (SPEC §8.3) — or <c>null</c>.</summary>
        public string? Confirmation { get; init; }

        /// <summary>Gets the agent's reason, or <c>null</c>. Unverified agent text (R8).</summary>
        public string? Reason { get; init; }

        /// <summary>Gets a value indicating whether the call reads records rather than configuration.</summary>
        public bool TouchesRecords { get; init; }

        /// <summary>Gets the scope recommended for this environment.</summary>
        public string Recommendation { get; init; } = string.Empty;

        /// <summary>Gets the decisions the dialog offers, besides <see cref="ApprovalDecision.Deny"/>.</summary>
        public IReadOnlyList<ApprovalDecision> Options { get; init; } = Array.Empty<ApprovalDecision>();

        /// <summary>Gets when the approval times out, UTC.</summary>
        public DateTime DeadlineUtc { get; init; }
    }
}
