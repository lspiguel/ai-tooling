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
    /// The one in-memory copy of <see cref="HostSettings"/>, shared by the token store, the approval store, the File
    /// root store and the panel. Every change is saved at once. A file written by an older schema is upgraded on load
    /// (SPEC §7, Saved settings).
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
            var loaded = backing.Load();
            this.current = HostLimits.Normalize(loaded ?? new HostSettings());
            if (loaded == null)
            {
                this.current.SchemaVersion = HostSettings.CurrentSchemaVersion;
            }
            else if (Upgrade(this.current))
            {
                backing.Save(this.current);
            }
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

        /// <summary>
        /// Brings settings written by an older schema up to date. The mapping from version 0 keeps every grant it can
        /// and drops the <i>always</i> dev-environment confirmation, which Phase 6 withdraws; it never touches the
        /// port, limits, bearer token or setup version. A grant it cannot carry is dropped and the person approves again.
        /// </summary>
        /// <param name="settings">The settings, changed in place.</param>
        /// <returns><c>true</c> when the settings changed.</returns>
        internal static bool Upgrade(HostSettings settings)
        {
            if (settings.SchemaVersion >= HostSettings.CurrentSchemaVersion)
            {
                return false;
            }

            settings.Grants ??= new List<PersistedGrant>();
            settings.FileRoots ??= new List<PersistedFileRoot>();
            settings.Grants.RemoveAll(g => ToGrant(g) == null);
            settings.SchemaVersion = HostSettings.CurrentSchemaVersion;
            return true;
        }

        private static Grant? ToGrant(PersistedGrant persisted)
        {
            if (!Enum.TryParse(persisted.Kind, out ApprovalKind kind)
                || !Enum.TryParse(persisted.Scope, out ApprovalScope scope)
                || !kind.Allows(scope)
                || scope == ApprovalScope.Session
                || (scope == ApprovalScope.Tenant && persisted.TenantId == Guid.Empty))
            {
                return null;
            }

            return new Grant(kind, scope, persisted.OrganizationId, persisted.OrganizationName, persisted.GrantedOnUtc, persisted.OriginTab, persisted.EnvironmentId, persisted.TenantId);
        }

        private static PersistedGrant ToPersisted(Grant grant) => new PersistedGrant
        {
            Kind = grant.Kind.ToString(),
            Scope = grant.Scope.ToString(),
            OrganizationId = grant.OrganizationId,
            OrganizationName = grant.OrganizationName,
            GrantedOnUtc = grant.GrantedOnUtc,
            OriginTab = grant.OriginTab,
            EnvironmentId = grant.EnvironmentId,
            TenantId = grant.TenantId,
        };
    }
}
