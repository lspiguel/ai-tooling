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
    /// Every standing grant — session, environment, tenant and global, for Read approval, record reads, Write and
    /// dev-environment confirmation — in one listable store (SPEC §7, C8). A tab's Revoke and Clear, the Active
    /// Approvals window's Revoke and Reset, and a host stop are all calls on this store. Once scopes are never
    /// stored: they end with their call or binding.
    /// </summary>
    internal sealed class ApprovalStore
    {
        private readonly object gate = new object();
        private readonly List<Grant> grants = new List<Grant>();
        private readonly IGrantPersistence persistence;
        private readonly IClock clock;

        /// <summary>Initializes a new instance of the <see cref="ApprovalStore"/> class and loads the persisted grants.</summary>
        /// <param name="persistence">Where environment, tenant and global grants are kept.</param>
        /// <param name="clock">The clock for grant times.</param>
        public ApprovalStore(IGrantPersistence persistence, IClock clock)
        {
            this.persistence = persistence;
            this.clock = clock;
            this.grants.AddRange(persistence.LoadGrants().Where(g => g.IsPersistent && g.Kind.Allows(g.Scope)));
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

        /// <summary>Finds the broadest grant covering a connection: global, then tenant, environment and session.</summary>
        /// <param name="kind">The kind asked for.</param>
        /// <param name="identity">The connection.</param>
        /// <returns>The grant, or <c>null</c>.</returns>
        public Grant? Find(ApprovalKind kind, ConnectionIdentity identity)
        {
            lock (this.gate)
            {
                return this.grants.Where(g => g.Covers(kind, identity)).OrderByDescending(g => g.Scope).FirstOrDefault();
            }
        }

        /// <summary>Lists the grants of a kind that match a connection's organization but differ on an environment or tenant ID.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="identity">The connection.</param>
        /// <returns>The stale grants.</returns>
        public IReadOnlyList<Grant> Stale(ApprovalKind kind, ConnectionIdentity identity)
        {
            lock (this.gate)
            {
                return this.grants.Where(g => g.Kind == kind && g.IsStale(identity)).ToList();
            }
        }

        /// <summary>Tells whether a global grant of this kind is on.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns><c>true</c> when on.</returns>
        public bool IsGlobal(ApprovalKind kind)
        {
            lock (this.gate)
            {
                return this.grants.Any(g => g.Kind == kind && g.Scope == ApprovalScope.Always);
            }
        }

        /// <summary>
        /// Records a grant the person gave in a dialog, replacing a grant of the same kind and reach: for one
        /// environment, the earlier session or environment grant of that kind; for a tenant, the tenant's; for global, the global one.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="scope">The scope; <see cref="ApprovalScope.Once"/> is never stored, and Write and the dev-environment confirmation stop at <see cref="ApprovalScope.Environment"/>.</param>
        /// <param name="identity">The connection the person was asked about.</param>
        /// <param name="originTab">The tab the person granted it from.</param>
        public void Allow(ApprovalKind kind, ApprovalScope scope, ConnectionIdentity identity, string originTab)
        {
            if (!kind.Allows(scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope), scope, $"A {kind} grant cannot have this scope.");
            }

            if (scope == ApprovalScope.Tenant && identity.TenantId == Guid.Empty)
            {
                throw new ArgumentException("A tenant grant needs the connection's tenant ID.", nameof(identity));
            }

            this.Mutate(() =>
            {
                var now = this.clock.UtcNow;
                switch (scope)
                {
                    case ApprovalScope.Always:
                        this.grants.RemoveAll(g => g.Kind == kind && g.Scope == ApprovalScope.Always);
                        this.grants.Add(new Grant(kind, scope, Guid.Empty, "All environments", now, originTab));
                        break;
                    case ApprovalScope.Tenant:
                        this.grants.RemoveAll(g => g.Kind == kind && g.Scope == ApprovalScope.Tenant && g.TenantId == identity.TenantId);
                        this.grants.Add(new Grant(kind, scope, Guid.Empty, identity.Name, now, originTab, null, identity.TenantId));
                        break;
                    default:
                        this.grants.RemoveAll(g => g.Kind == kind && (g.Scope == ApprovalScope.Session || g.Scope == ApprovalScope.Environment) && g.Matches(identity));
                        this.grants.Add(new Grant(kind, scope, identity.OrganizationId, identity.Name, now, originTab, identity.EnvironmentId, identity.TenantId));
                        break;
                }
            });
        }

        /// <summary>
        /// Fills in the environment and tenant IDs a connection reports into the session and environment grants that
        /// match its organization and did not know them (SPEC §7, Identity). Called when a connection binds.
        /// </summary>
        /// <param name="identity">The connection.</param>
        public void FillIdentity(ConnectionIdentity identity)
        {
            if (identity.EnvironmentId.Length == 0 && identity.TenantId == Guid.Empty)
            {
                return;
            }

            this.Mutate(() =>
            {
                for (var i = 0; i < this.grants.Count; i++)
                {
                    var grant = this.grants[i];
                    if ((grant.Scope == ApprovalScope.Session || grant.Scope == ApprovalScope.Environment) && grant.Matches(identity))
                    {
                        this.grants[i] = grant.WithIdentity(identity);
                    }
                }
            });
        }

        /// <summary>
        /// Removes the session and environment grants of one kind that cover exactly a connection's environment — a
        /// tab's Revoke or Clear. Tenant and global grants are broader and are revoked in the Active Approvals window.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="identity">The connection.</param>
        public void RevokeEnvironment(ApprovalKind kind, ConnectionIdentity identity) =>
            this.Mutate(() => this.grants.RemoveAll(g => g.Kind == kind && (g.Scope == ApprovalScope.Session || g.Scope == ApprovalScope.Environment) && g.Matches(identity)));

        /// <summary>Removes one grant.</summary>
        /// <param name="grant">The grant.</param>
        public void Remove(Grant grant) => this.Mutate(() => this.grants.Remove(grant));

        /// <summary>Removes the given grants — a reset of an environment or a tenant, whose members the window lists.</summary>
        /// <param name="toRemove">The grants.</param>
        public void Remove(IEnumerable<Grant> toRemove)
        {
            var set = new HashSet<Grant>(toRemove);
            this.Mutate(() => this.grants.RemoveAll(set.Contains));
        }

        /// <summary>Removes every global grant.</summary>
        public void ClearGlobal() => this.Mutate(() => this.grants.RemoveAll(g => g.Scope == ApprovalScope.Always));

        /// <summary>Removes a tenant's own grants and the grants of every environment recorded under it.</summary>
        /// <param name="tenantId">The tenant.</param>
        public void ClearTenant(Guid tenantId) =>
            this.Mutate(() => this.grants.RemoveAll(g => g.Scope != ApprovalScope.Always && g.TenantId == tenantId));

        /// <summary>Removes the session and environment grants of one environment.</summary>
        /// <param name="environmentKey">The environment, as <see cref="Grant.EnvironmentKey"/> names it.</param>
        public void ClearEnvironment(string environmentKey) =>
            this.Mutate(() => this.grants.RemoveAll(g => (g.Scope == ApprovalScope.Session || g.Scope == ApprovalScope.Environment) && g.EnvironmentKey == environmentKey));

        /// <summary>Removes every grant — Reset all approvals.</summary>
        public void ClearAll() => this.Mutate(() => this.grants.Clear());

        /// <summary>Ends every session-scoped grant, for a host stop (H3).</summary>
        public void EndSession() => this.Mutate(() => this.grants.RemoveAll(g => g.Scope == ApprovalScope.Session));

        private void Mutate(Action change)
        {
            bool changed;
            List<Grant> persistent;
            lock (this.gate)
            {
                var before = this.grants.ToList();
                var persistentBefore = before.Where(g => g.IsPersistent).ToList();
                change();
                persistent = this.grants.Where(g => g.IsPersistent).ToList();
                changed = !before.SequenceEqual(this.grants);
                if (!persistentBefore.SequenceEqual(persistent))
                {
                    this.persistence.SaveGrants(persistent);
                }
            }

            if (changed)
            {
                this.Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
