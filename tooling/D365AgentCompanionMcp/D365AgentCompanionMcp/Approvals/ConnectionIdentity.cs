// <copyright file="ConnectionIdentity.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;

    /// <summary>
    /// The three IDs that say which environment a connection is (SPEC §7, Identity): the organization ID from the
    /// identity call, and the environment and tenant IDs from the connection when it reports them.
    /// </summary>
    internal sealed class ConnectionIdentity
    {
        /// <summary>Initializes a new instance of the <see cref="ConnectionIdentity"/> class.</summary>
        /// <param name="organizationId">The organization ID.</param>
        /// <param name="environmentId">The environment ID, or <c>null</c> or empty when the connection does not report it.</param>
        /// <param name="tenantId">The tenant ID, or <see cref="Guid.Empty"/> when the connection does not report it.</param>
        /// <param name="name">The environment's friendly name, for display.</param>
        public ConnectionIdentity(Guid organizationId, string? environmentId, Guid tenantId, string name)
        {
            this.OrganizationId = organizationId;
            this.EnvironmentId = NormalizeEnvironmentId(environmentId);
            this.TenantId = tenantId;
            this.Name = name;
        }

        /// <summary>Gets the organization ID.</summary>
        public Guid OrganizationId { get; }

        /// <summary>Gets the environment ID, lowercase; empty when unknown.</summary>
        public string EnvironmentId { get; }

        /// <summary>Gets the tenant ID; <see cref="Guid.Empty"/> when unknown.</summary>
        public Guid TenantId { get; }

        /// <summary>Gets the environment's friendly name.</summary>
        public string Name { get; }

        /// <summary>Normalizes an environment ID: trimmed and lowercase, so IDs compare the same whatever the source's case.</summary>
        /// <param name="environmentId">The ID.</param>
        /// <returns>The normalized ID, or an empty string.</returns>
        public static string NormalizeEnvironmentId(string? environmentId) => (environmentId ?? string.Empty).Trim().ToLowerInvariant();
    }
}
