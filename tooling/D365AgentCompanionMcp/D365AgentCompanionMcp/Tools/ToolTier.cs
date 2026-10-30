// <copyright file="ToolTier.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    /// <summary>The authorization tier of a tool (SPEC §7).</summary>
    internal enum ToolTier
    {
        /// <summary><c>list_connections</c>: logged, not approved (R2).</summary>
        Host,

        /// <summary>One prompt covers every read, scoped once, session, environment or always.</summary>
        Read,

        /// <summary>Approved once per call; needs dev-environment confirmation and a reason (R12).</summary>
        Write,

        /// <summary><c>create_publisher</c>: off by default, typed confirmation, a reason (R3, R12).</summary>
        Publisher,
    }
}
