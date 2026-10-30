// <copyright file="ConnectionRegistry.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// One tab per connection (C3). A tab claims its connection ID synchronously, in XrmToolBox's hook order, so the
    /// first tab of a toolbar connect wins; the organization is checked only after the asynchronous identity call.
    /// </summary>
    internal sealed class ConnectionRegistry
    {
        private readonly object gate = new object();
        private readonly List<BoundConnection> entries = new List<BoundConnection>();

        /// <summary>Raised after a binding is added, completed or released.</summary>
        public event EventHandler? Changed;

        /// <summary>Normalizes a connection ID: GUIDs lowercase and hyphenated, whatever the input's case or braces.</summary>
        /// <param name="connectionId">The ID.</param>
        /// <returns>The normalized ID.</returns>
        public static string Normalize(string connectionId) =>
            Guid.TryParse(connectionId, out var id) ? id.ToString("D") : connectionId.Trim();

        /// <summary>Claims a connection ID for a tab. The tab must have released any earlier binding first (C5).</summary>
        /// <param name="candidate">The claimed binding.</param>
        /// <returns>The binding it conflicts with, or <c>null</c> when claimed.</returns>
        public BoundConnection? Claim(BoundConnection candidate)
        {
            lock (this.gate)
            {
                var conflict = this.entries.FirstOrDefault(e => e.Owner != candidate.Owner && e.ConnectionId == candidate.ConnectionId);
                if (conflict != null)
                {
                    return conflict;
                }

                this.entries.RemoveAll(e => e.Owner == candidate.Owner);
                this.entries.Add(candidate);
            }

            this.Changed?.Invoke(this, EventArgs.Empty);
            return null;
        }

        /// <summary>Completes a claim once the identity call has returned.</summary>
        /// <param name="claimed">The claimed binding.</param>
        /// <param name="organizationId">The organization ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <param name="impersonating">Whether the connection impersonates another user.</param>
        /// <param name="service">The service calls run with.</param>
        /// <param name="nowUtc">The binding time.</param>
        /// <param name="conflict">The binding to the same organization, on <see cref="BindOutcome.OrganizationConflict"/>.</param>
        /// <returns>The outcome.</returns>
        public BindOutcome Complete(BoundConnection claimed, Guid organizationId, Guid userId, bool impersonating, IOrganizationService service, DateTime nowUtc, out BoundConnection? conflict)
        {
            lock (this.gate)
            {
                conflict = null;
                if (!this.entries.Contains(claimed) || claimed.State != BindingState.Claimed)
                {
                    return BindOutcome.Abandoned;
                }

                conflict = this.entries.FirstOrDefault(e => e != claimed && e.State == BindingState.Bound && e.OrganizationId == organizationId);
                if (conflict != null)
                {
                    this.entries.Remove(claimed);
                    claimed.End();
                }
                else
                {
                    claimed.MarkBound(organizationId, userId, impersonating, service, nowUtc);
                }
            }

            this.Changed?.Invoke(this, EventArgs.Empty);
            return conflict == null ? BindOutcome.Bound : BindOutcome.OrganizationConflict;
        }

        /// <summary>Ends a tab's claim or binding (C5, C6).</summary>
        /// <param name="owner">The tab.</param>
        /// <returns>The binding that ended, or <c>null</c>.</returns>
        public BoundConnection? Release(IHostInstance owner)
        {
            BoundConnection? released;
            lock (this.gate)
            {
                released = this.entries.FirstOrDefault(e => e.Owner == owner);
                if (released == null)
                {
                    return null;
                }

                this.entries.Remove(released);
                released.End();
            }

            this.Changed?.Invoke(this, EventArgs.Empty);
            return released;
        }

        /// <summary>Finds the bound connection a call names (C7).</summary>
        /// <param name="connectionId">The <c>connection</c> argument.</param>
        /// <returns>The binding, or <c>null</c> when none is bound to it.</returns>
        public BoundConnection? Find(string connectionId)
        {
            var normalized = Normalize(connectionId);
            lock (this.gate)
            {
                return this.entries.FirstOrDefault(e => e.State == BindingState.Bound && e.ConnectionId == normalized);
            }
        }

        /// <summary>Finds a tab's claim or binding.</summary>
        /// <param name="owner">The tab.</param>
        /// <returns>The binding, or <c>null</c>.</returns>
        public BoundConnection? ForOwner(IHostInstance owner)
        {
            lock (this.gate)
            {
                return this.entries.FirstOrDefault(e => e.Owner == owner);
            }
        }

        /// <summary>Lists the bound connections, ordered by binding time.</summary>
        /// <returns>The bound connections.</returns>
        public IReadOnlyList<BoundConnection> Bound()
        {
            lock (this.gate)
            {
                return this.entries.Where(e => e.State == BindingState.Bound).OrderBy(e => e.BoundOnUtc).ToList();
            }
        }
    }
}
