// <copyright file="ApprovalStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;

    /// <summary>
    /// Every standing grant — session, environment and always, for Read approval and dev-environment confirmation —
    /// in one listable store, held per organization (SPEC §7, C8). A tab's Revoke and Clear, the always settings,
    /// and a host stop are all calls on this store. Once scopes are never stored: they end with their call or binding.
    /// </summary>
    internal sealed class ApprovalStore
    {
        private readonly object gate = new object();
        private readonly List<Grant> grants = new List<Grant>();
        private readonly IGrantPersistence persistence;
        private readonly IClock clock;

        /// <summary>Initializes a new instance of the <see cref="ApprovalStore"/> class and loads the persisted grants.</summary>
        /// <param name="persistence">Where environment and always grants are kept.</param>
        /// <param name="clock">The clock for grant times.</param>
        public ApprovalStore(IGrantPersistence persistence, IClock clock)
        {
            this.persistence = persistence;
            this.clock = clock;
            this.grants.AddRange(persistence.LoadGrants().Where(g => g.IsPersistent));
        }

        /// <summary>Raised after any change.</summary>
        public event EventHandler? Changed;

        /// <summary>Lists every grant.</summary>
        /// <returns>A copy of the grants.</returns>
        public IReadOnlyList<Grant> List()
        {
            lock (this.gate)
            {
                return this.grants.ToList();
            }
        }

        /// <summary>Finds the broadest grant covering an organization: always, then environment, then session.</summary>
        /// <param name="kind">The kind asked for.</param>
        /// <param name="organizationId">The organization.</param>
        /// <returns>The grant, or <c>null</c>.</returns>
        public Grant? Find(ApprovalKind kind, Guid organizationId)
        {
            lock (this.gate)
            {
                return this.grants.Where(g => g.Covers(kind, organizationId)).OrderByDescending(g => g.Scope).FirstOrDefault();
            }
        }

        /// <summary>Tells whether an always grant of this kind is on.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns><c>true</c> when on.</returns>
        public bool IsAlways(ApprovalKind kind)
        {
            lock (this.gate)
            {
                return this.grants.Any(g => g.Kind == kind && g.Scope == ApprovalScope.Always);
            }
        }

        /// <summary>Grants a session or environment scope for one organization, replacing a grant of the same kind for it.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="scope"><see cref="ApprovalScope.Session"/> or <see cref="ApprovalScope.Environment"/>.</param>
        /// <param name="organizationId">The organization.</param>
        /// <param name="organizationName">Its friendly name.</param>
        /// <param name="originTab">The tab the person granted it from.</param>
        public void GrantForOrganization(ApprovalKind kind, ApprovalScope scope, Guid organizationId, string organizationName, string originTab)
        {
            if (scope != ApprovalScope.Session && scope != ApprovalScope.Environment)
            {
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Only session and environment scopes are granted per organization.");
            }

            this.Mutate(() =>
            {
                this.grants.RemoveAll(g => g.Kind == kind && g.Scope != ApprovalScope.Always && g.OrganizationId == organizationId);
                this.grants.Add(new Grant(kind, scope, organizationId, organizationName, this.clock.UtcNow, originTab));
            });
        }

        /// <summary>Switches an always grant on or off. Only the settings dialog calls this.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="on">Whether it is on.</param>
        /// <param name="origin">Where it was set.</param>
        public void SetAlways(ApprovalKind kind, bool on, string origin)
        {
            this.Mutate(() =>
            {
                this.grants.RemoveAll(g => g.Kind == kind && g.Scope == ApprovalScope.Always);
                if (on)
                {
                    this.grants.Add(new Grant(kind, ApprovalScope.Always, Guid.Empty, "All organizations", this.clock.UtcNow, origin));
                }
            });
        }

        /// <summary>Removes the session and environment grants of one kind for one organization — a tab's Revoke or Clear.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="organizationId">The organization.</param>
        public void RevokeOrganization(ApprovalKind kind, Guid organizationId) =>
            this.Mutate(() => this.grants.RemoveAll(g => g.Kind == kind && g.Scope != ApprovalScope.Always && g.OrganizationId == organizationId));

        /// <summary>Removes one grant.</summary>
        /// <param name="grant">The grant.</param>
        public void Remove(Grant grant) => this.Mutate(() => this.grants.Remove(grant));

        /// <summary>Removes every grant of one scope.</summary>
        /// <param name="scope">The scope.</param>
        public void ClearScope(ApprovalScope scope) => this.Mutate(() => this.grants.RemoveAll(g => g.Scope == scope));

        /// <summary>Ends every session-scoped grant, for a host stop (H3).</summary>
        public void EndSession() => this.ClearScope(ApprovalScope.Session);

        private void Mutate(Action change)
        {
            List<Grant> persistent;
            lock (this.gate)
            {
                var before = this.grants.Where(g => g.IsPersistent).ToList();
                change();
                persistent = this.grants.Where(g => g.IsPersistent).ToList();
                if (!before.SequenceEqual(persistent))
                {
                    this.persistence.SaveGrants(persistent);
                }
            }

            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
