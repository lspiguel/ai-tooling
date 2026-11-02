// <copyright file="ApprovalScope.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    /// <summary>How long an approval or confirmation lasts (SPEC §7).</summary>
    internal enum ApprovalScope
    {
        /// <summary>One call and its continuation pages; for a confirmation, one binding. Never stored.</summary>
        Once,

        /// <summary>One organization until the host stops.</summary>
        Session,

        /// <summary>One organization, in this and later sessions. Persisted.</summary>
        Environment,

        /// <summary>Every organization, including ones bound later. Set only in settings. Persisted.</summary>
        Always,
    }
}
