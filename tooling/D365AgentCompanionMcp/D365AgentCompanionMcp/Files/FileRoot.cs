// <copyright file="FileRoot.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Files
{
    using System;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;

    /// <summary>
    /// The folder file paths from the agent are resolved against (SPEC §6.8, File root): the root of the git
    /// repository that holds the source files the agent edits. Set for one environment or for every environment of a tenant.
    /// </summary>
    internal sealed class FileRoot
    {
        /// <summary>Initializes a new instance of the <see cref="FileRoot"/> class.</summary>
        /// <param name="level"><see cref="ApprovalScope.Environment"/> or <see cref="ApprovalScope.Tenant"/>.</param>
        /// <param name="organizationId">The environment's organization ID; empty for a tenant File root.</param>
        /// <param name="environmentId">The environment ID, when known.</param>
        /// <param name="tenantId">The tenant ID.</param>
        /// <param name="name">The environment's friendly name, for display.</param>
        /// <param name="path">The folder.</param>
        public FileRoot(ApprovalScope level, Guid organizationId, string? environmentId, Guid tenantId, string name, string path)
        {
            if (level != ApprovalScope.Environment && level != ApprovalScope.Tenant)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "A File root is set for an environment or a tenant.");
            }

            this.Level = level;
            this.OrganizationId = organizationId;
            this.EnvironmentId = ConnectionIdentity.NormalizeEnvironmentId(environmentId);
            this.TenantId = tenantId;
            this.Name = name;
            this.Path = path;
        }

        /// <summary>Gets the level it was set at.</summary>
        public ApprovalScope Level { get; }

        /// <summary>Gets the organization ID of the environment it was set for; empty for a tenant File root.</summary>
        public Guid OrganizationId { get; }

        /// <summary>Gets the environment ID; empty when not known.</summary>
        public string EnvironmentId { get; }

        /// <summary>Gets the tenant ID; for an environment File root, the tenant the environment reported, when known.</summary>
        public Guid TenantId { get; }

        /// <summary>Gets the environment's friendly name (a tenant's File root keeps the name of the environment it was set from).</summary>
        public string Name { get; }

        /// <summary>Gets the folder.</summary>
        public string Path { get; }

        /// <summary>Gets the level as the lowercase word tool results use: <c>environment</c> or <c>tenant</c>.</summary>
        public string LevelLabel => this.Level == ApprovalScope.Tenant ? "tenant" : "environment";

        /// <summary>Tells whether this File root applies to a connection, at its own level.</summary>
        /// <param name="identity">The connection.</param>
        /// <returns><c>true</c> when it applies.</returns>
        public bool AppliesTo(ConnectionIdentity identity)
        {
            if (this.Level == ApprovalScope.Tenant)
            {
                return this.TenantId != Guid.Empty && identity.TenantId == this.TenantId;
            }

            return this.OrganizationId == identity.OrganizationId
                && (this.EnvironmentId.Length == 0 || identity.EnvironmentId.Length == 0 || this.EnvironmentId == identity.EnvironmentId)
                && (this.TenantId == Guid.Empty || identity.TenantId == Guid.Empty || this.TenantId == identity.TenantId);
        }
    }
}
