// <copyright file="ApprovalKind.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    /// <summary>What a grant covers (SPEC §7). A grant of one kind never covers a call of another.</summary>
    internal enum ApprovalKind
    {
        /// <summary>Approval of every Read call except a query that touches records.</summary>
        ReadApproval,

        /// <summary>Confirmation that a connection is a development environment, which Write tools require.</summary>
        DevEnvironment,

        /// <summary>Approval of a query that touches records.</summary>
        RecordRead,

        /// <summary>Approval of every Write call.</summary>
        Write,
    }
}
