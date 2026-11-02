// <copyright file="HostSettingsStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;

    /// <summary>
    /// The one in-memory copy of <see cref="HostSettings"/>, shared by the token store, the approval store and the
    /// panel. Every change is saved at once.
    /// </summary>
    internal sealed class HostSettingsStore : IGrantPersistence
    {
        private readonly object gate = new object();
        private readonly ISettingsStore backing;
        private readonly HostSettings current;

        /// <summary>Initializes a new instance of the <see cref="HostSettingsStore"/> class.</summary>
        /// <param name="backing">Where the settings are kept.</param>
        public HostSettingsStore(ISettingsStore backing)
        {
            this.backing = backing;
            this.current = HostLimits.Normalize(backing.Load() ?? new HostSettings());
        }

        /// <summary>Raised after a change.</summary>
        public event EventHandler? Changed;

        /// <summary>Reads a value.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="read">Reads from the settings.</param>
        /// <returns>The value.</returns>
        public T Read<T>(Func<HostSettings, T> read)
        {
            lock (this.gate)
            {
                return read(this.current);
            }
        }

        /// <summary>Changes the settings, normalizes them and saves them.</summary>
        /// <param name="change">Changes the settings.</param>
        public void Update(Action<HostSettings> change)
        {
            lock (this.gate)
            {
                change(this.current);
                HostLimits.Normalize(this.current);
                this.backing.Save(this.current);
            }

            this.Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public IReadOnlyList<Grant> LoadGrants() => this.Read(s => s.Grants.Select(ToGrant).Where(g => g != null).Cast<Grant>().ToList());

        /// <inheritdoc/>
        public void SaveGrants(IReadOnlyList<Grant> grants) => this.Update(s => s.Grants = grants.Select(ToPersisted).ToList());

        private static Grant? ToGrant(PersistedGrant persisted)
        {
            if (!Enum.TryParse(persisted.Kind, out ApprovalKind kind)
                || !Enum.TryParse(persisted.Scope, out ApprovalScope scope)
                || (scope != ApprovalScope.Environment && scope != ApprovalScope.Always))
            {
                return null;
            }

            return new Grant(kind, scope, persisted.OrganizationId, persisted.OrganizationName, persisted.GrantedOnUtc, persisted.OriginTab);
        }

        private static PersistedGrant ToPersisted(Grant grant) => new PersistedGrant
        {
            Kind = grant.Kind.ToString(),
            Scope = grant.Scope.ToString(),
            OrganizationId = grant.OrganizationId,
            OrganizationName = grant.OrganizationName,
            GrantedOnUtc = grant.GrantedOnUtc,
            OriginTab = grant.OriginTab,
        };
    }
}
