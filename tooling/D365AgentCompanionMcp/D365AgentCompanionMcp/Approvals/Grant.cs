// <copyright file="Grant.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;

    /// <summary>
    /// One standing approval or confirmation (SPEC §7). Records what the Active Approvals window lists: kind, scope,
    /// the organization, environment and tenant IDs, the environment's friendly name, grant time and originating tab.
    /// Immutable: learning an ID replaces the grant.
    /// </summary>
    internal sealed class Grant : IEquatable<Grant>
    {
        /// <summary>Initializes a new instance of the <see cref="Grant"/> class.</summary>
        /// <param name="kind">What the grant covers.</param>
        /// <param name="scope">How far and how long it reaches; never <see cref="ApprovalScope.Once"/>.</param>
        /// <param name="organizationId">The organization; <see cref="Guid.Empty"/> for tenant and global grants.</param>
        /// <param name="organizationName">The environment's friendly name, for display.</param>
        /// <param name="grantedOnUtc">When it was granted.</param>
        /// <param name="originTab">The tab or place it was granted from.</param>
        /// <param name="environmentId">The environment ID, when known.</param>
        /// <param name="tenantId">The tenant ID, when known.</param>
        public Grant(ApprovalKind kind, ApprovalScope scope, Guid organizationId, string organizationName, DateTime grantedOnUtc, string originTab, string? environmentId = null, Guid tenantId = default)
        {
            this.Kind = kind;
            this.Scope = scope;
            this.OrganizationId = organizationId;
            this.OrganizationName = organizationName;
            this.GrantedOnUtc = grantedOnUtc;
            this.OriginTab = originTab;
            this.EnvironmentId = ConnectionIdentity.NormalizeEnvironmentId(environmentId);
            this.TenantId = tenantId;
        }

        /// <summary>Gets what the grant covers.</summary>
        public ApprovalKind Kind { get; }

        /// <summary>Gets how far and how long it reaches.</summary>
        public ApprovalScope Scope { get; }

        /// <summary>Gets the organization; <see cref="Guid.Empty"/> for tenant and global grants.</summary>
        public Guid OrganizationId { get; }

        /// <summary>Gets the environment's friendly name (or the tenant's first environment's, or a label for a global grant).</summary>
        public string OrganizationName { get; }

        /// <summary>Gets when it was granted.</summary>
        public DateTime GrantedOnUtc { get; }

        /// <summary>Gets where it was granted from.</summary>
        public string OriginTab { get; }

        /// <summary>Gets the environment ID; empty when not known yet. A tenant or global grant holds none.</summary>
        public string EnvironmentId { get; }

        /// <summary>Gets the tenant ID; <see cref="Guid.Empty"/> when not known yet. A global grant holds none.</summary>
        public Guid TenantId { get; }

        /// <summary>Gets a value indicating whether the grant is kept between sessions.</summary>
        public bool IsPersistent => this.Scope != ApprovalScope.Session && this.Scope != ApprovalScope.Once;

        /// <summary>Gets a key that groups the grants of one environment: its environment ID, else its organization ID.</summary>
        public string EnvironmentKey => this.EnvironmentId.Length > 0 ? this.EnvironmentId : "org:" + this.OrganizationId.ToString("D");

        /// <summary>
        /// Tells whether this environment-level grant names the same environment as a connection: the organization IDs
        /// match, and so do the environment and tenant IDs wherever both sides know them.
        /// </summary>
        /// <param name="identity">The connection.</param>
        /// <returns><c>true</c> when the grant belongs to the connection's environment.</returns>
        public bool Matches(ConnectionIdentity identity) =>
            this.OrganizationId == identity.OrganizationId && this.IdsAgree(identity);

        /// <summary>
        /// Tells whether this grant is stale for a connection: it matches on the organization ID but differs on an
        /// environment or tenant ID — an environment that was copied, restored or reset. A stale grant is not applied.
        /// </summary>
        /// <param name="identity">The connection.</param>
        /// <returns><c>true</c> when stale.</returns>
        public bool IsStale(ConnectionIdentity identity) =>
            (this.Scope == ApprovalScope.Session || this.Scope == ApprovalScope.Environment)
            && this.OrganizationId == identity.OrganizationId && !this.IdsAgree(identity);

        /// <summary>Tells whether this grant covers a connection.</summary>
        /// <param name="kind">The kind asked for.</param>
        /// <param name="identity">The connection.</param>
        /// <returns><c>true</c> when it applies.</returns>
        public bool Covers(ApprovalKind kind, ConnectionIdentity identity)
        {
            if (this.Kind != kind)
            {
                return false;
            }

            switch (this.Scope)
            {
                case ApprovalScope.Always:
                    return true;
                case ApprovalScope.Tenant:
                    // A connection that reports no tenant ID gets no tenant grants.
                    return this.TenantId != Guid.Empty && identity.TenantId == this.TenantId;
                case ApprovalScope.Session:
                case ApprovalScope.Environment:
                    return this.Matches(identity);
                default:
                    return false;
            }
        }

        /// <summary>Makes a copy that also knows the IDs a connection reports, where this grant did not know them.</summary>
        /// <param name="identity">The connection this grant matches.</param>
        /// <returns>The completed grant, or this grant when nothing was missing.</returns>
        public Grant WithIdentity(ConnectionIdentity identity)
        {
            var environmentId = this.EnvironmentId.Length == 0 ? identity.EnvironmentId : this.EnvironmentId;
            var tenantId = this.TenantId == Guid.Empty ? identity.TenantId : this.TenantId;
            return environmentId == this.EnvironmentId && tenantId == this.TenantId
                ? this
                : new Grant(this.Kind, this.Scope, this.OrganizationId, this.OrganizationName, this.GrantedOnUtc, this.OriginTab, environmentId, tenantId);
        }

        /// <inheritdoc/>
        public bool Equals(Grant? other) =>
            other != null
            && this.Kind == other.Kind
            && this.Scope == other.Scope
            && this.OrganizationId == other.OrganizationId
            && this.OrganizationName == other.OrganizationName
            && this.GrantedOnUtc == other.GrantedOnUtc
            && this.OriginTab == other.OriginTab
            && this.EnvironmentId == other.EnvironmentId
            && this.TenantId == other.TenantId;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => this.Equals(obj as Grant);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)this.Kind;
                hash = (hash * 31) + (int)this.Scope;
                hash = (hash * 31) + this.OrganizationId.GetHashCode();
                hash = (hash * 31) + this.EnvironmentId.GetHashCode();
                return (hash * 31) + this.TenantId.GetHashCode();
            }
        }

        private bool IdsAgree(ConnectionIdentity identity) =>
            (this.EnvironmentId.Length == 0 || identity.EnvironmentId.Length == 0 || this.EnvironmentId == identity.EnvironmentId)
            && (this.TenantId == Guid.Empty || identity.TenantId == Guid.Empty || this.TenantId == identity.TenantId);
    }
}
