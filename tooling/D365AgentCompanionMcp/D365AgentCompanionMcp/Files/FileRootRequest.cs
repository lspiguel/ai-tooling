// <copyright file="FileRootRequest.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Files
{
    using System;
    using System.Collections.Generic;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;

    /// <summary>What the File root prompt shows (SPEC §6.8, When no File root is set).</summary>
    internal sealed class FileRootRequest
    {
        /// <summary>Gets the connection name.</summary>
        public string ConnectionName { get; init; } = string.Empty;

        /// <summary>Gets the environment's friendly name.</summary>
        public string OrganizationName { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether the connection reports a tenant ID; when not, <i>Every environment in this tenant</i> is disabled.</summary>
        public bool TenantAvailable { get; init; }

        /// <summary>Gets a value indicating whether <i>This environment only</i> can be chosen; it cannot when the File root is set from a tenant line.</summary>
        public bool EnvironmentAvailable { get; init; } = true;

        /// <summary>Gets the level the prompt starts with: <see cref="ApprovalScope.Tenant"/> or <see cref="ApprovalScope.Environment"/>.</summary>
        public ApprovalScope PreselectedLevel { get; init; } = ApprovalScope.Tenant;

        /// <summary>Gets the paths in the call, which the folder picker uses to start at the git working tree holding the first absolute one.</summary>
        public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

        /// <summary>Gets the folder set now, when the person is changing it.</summary>
        public string? CurrentPath { get; init; }

        /// <summary>Gets when the prompt times out, UTC.</summary>
        public DateTime DeadlineUtc { get; init; }
    }
}
