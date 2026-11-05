// <copyright file="ApprovalScope.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    /// <summary>
    /// How far an approval or confirmation reaches and how long it lasts (SPEC §7). The values are ordered from
    /// narrowest to broadest, and a call runs under the broadest grant that covers it.
    /// </summary>
    internal enum ApprovalScope
    {
        /// <summary>One call and its continuation pages; for a confirmation, one binding. Never stored.</summary>
        Once,

        /// <summary>One environment until the host stops.</summary>
        Session,

        /// <summary>One environment, in this and later sessions. Persisted.</summary>
        Environment,

        /// <summary>Every environment of one tenant, including ones bound later. Persisted.</summary>
        Tenant,

        /// <summary>Every environment of every tenant, including ones bound later; shown as Global. Persisted.</summary>
        Always,
    }
}
