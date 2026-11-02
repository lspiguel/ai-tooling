// <copyright file="ApprovalKind.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    /// <summary>What a grant covers (SPEC §7).</summary>
    internal enum ApprovalKind
    {
        /// <summary>Approval of every Read call.</summary>
        ReadApproval,

        /// <summary>Confirmation that a connection is a development environment, which Write tools require.</summary>
        DevEnvironment,
    }
}
