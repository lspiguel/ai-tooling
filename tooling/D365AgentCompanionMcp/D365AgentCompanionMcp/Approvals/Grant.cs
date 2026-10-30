// <copyright file="Grant.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;

    /// <summary>
    /// One standing approval or confirmation. Records what Phase 6's Active Approvals window lists: kind, scope,
    /// organization ID and friendly name, grant time and originating tab.
    /// </summary>
    internal sealed class Grant
    {
        /// <summary>Initializes a new instance of the <see cref="Grant"/> class.</summary>
        /// <param name="kind">What the grant covers.</param>
        /// <param name="scope">How long it lasts; never <see cref="ApprovalScope.Once"/>.</param>
        /// <param name="organizationId">The organization; <see cref="Guid.Empty"/> for <see cref="ApprovalScope.Always"/>.</param>
        /// <param name="organizationName">The organization's friendly name, for display.</param>
        /// <param name="grantedOnUtc">When it was granted.</param>
        /// <param name="originTab">The tab or place it was granted from.</param>
        public Grant(ApprovalKind kind, ApprovalScope scope, Guid organizationId, string organizationName, DateTime grantedOnUtc, string originTab)
        {
            this.Kind = kind;
            this.Scope = scope;
            this.OrganizationId = organizationId;
            this.OrganizationName = organizationName;
            this.GrantedOnUtc = grantedOnUtc;
            this.OriginTab = originTab;
        }

        /// <summary>Gets what the grant covers.</summary>
        public ApprovalKind Kind { get; }

        /// <summary>Gets how long it lasts.</summary>
        public ApprovalScope Scope { get; }

        /// <summary>Gets the organization; <see cref="Guid.Empty"/> for an always grant.</summary>
        public Guid OrganizationId { get; }

        /// <summary>Gets the organization's friendly name.</summary>
        public string OrganizationName { get; }

        /// <summary>Gets when it was granted.</summary>
        public DateTime GrantedOnUtc { get; }

        /// <summary>Gets where it was granted from.</summary>
        public string OriginTab { get; }

        /// <summary>Gets a value indicating whether the grant is kept between sessions.</summary>
        public bool IsPersistent => this.Scope == ApprovalScope.Environment || this.Scope == ApprovalScope.Always;

        /// <summary>Tells whether this grant covers an organization.</summary>
        /// <param name="kind">The kind asked for.</param>
        /// <param name="organizationId">The organization.</param>
        /// <returns><c>true</c> when it applies.</returns>
        public bool Covers(ApprovalKind kind, Guid organizationId) =>
            this.Kind == kind && (this.Scope == ApprovalScope.Always || this.OrganizationId == organizationId);
    }
}
