// <copyright file="ApprovalDecision.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    /// <summary>The person's answer in an approval dialog.</summary>
    internal enum ApprovalDecision
    {
        /// <summary>Denied.</summary>
        Deny,

        /// <summary>This call only.</summary>
        AllowOnce,

        /// <summary>Every Read call to this organization until the host stops.</summary>
        AllowSession,

        /// <summary>Every Read call to this organization, remembered.</summary>
        AllowEnvironment,
    }
}
