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

        /// <summary>Every call of the kind to this environment until the host stops.</summary>
        AllowSession,

        /// <summary>Every call of the kind to this environment, remembered.</summary>
        AllowEnvironment,

        /// <summary>Every call of the kind to every environment of this tenant, remembered.</summary>
        AllowTenant,

        /// <summary>Every call of the kind to every environment of every tenant, remembered.</summary>
        AllowGlobal,
    }
}
