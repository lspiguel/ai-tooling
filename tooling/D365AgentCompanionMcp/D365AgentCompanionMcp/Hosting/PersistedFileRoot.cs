// <copyright file="PersistedFileRoot.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;

    /// <summary>A File root as stored in the plugin settings (SPEC §6.8, §7). A setting, not a grant: no reset clears it.</summary>
    public sealed class PersistedFileRoot
    {
        /// <summary>Gets or sets the level the File root was set at: <c>Environment</c> or <c>Tenant</c>.</summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>Gets or sets the organization ID of the environment it was set for; empty for a tenant File root.</summary>
        public Guid OrganizationId { get; set; }

        /// <summary>Gets or sets the environment ID, when known.</summary>
        public string EnvironmentId { get; set; } = string.Empty;

        /// <summary>Gets or sets the tenant ID; for an environment File root, the tenant the environment reported, when known.</summary>
        public Guid TenantId { get; set; }

        /// <summary>Gets or sets the environment's friendly name, for display.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the folder.</summary>
        public string Path { get; set; } = string.Empty;
    }
}
