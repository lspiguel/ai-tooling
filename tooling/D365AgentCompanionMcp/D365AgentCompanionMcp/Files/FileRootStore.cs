// <copyright file="FileRootStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Files
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;

    /// <summary>
    /// The File roots, kept in the plugin settings (SPEC §6.8). A File root belongs to an environment or a tenant, not
    /// to a tab, and an environment's takes precedence over its tenant's. A File root is a setting, not a grant: no
    /// reset in the Active Approvals window clears it.
    /// </summary>
    internal sealed class FileRootStore
    {
        private readonly HostSettingsStore settings;

        /// <summary>Initializes a new instance of the <see cref="FileRootStore"/> class.</summary>
        /// <param name="settings">The plugin settings.</param>
        public FileRootStore(HostSettingsStore settings)
        {
            this.settings = settings;
        }

        /// <summary>Raised after a File root is set or cleared.</summary>
        public event EventHandler? Changed;

        /// <summary>Lists every File root.</summary>
        /// <returns>The File roots.</returns>
        public IReadOnlyList<FileRoot> List() => this.settings.Read(s => s.FileRoots.Select(ToRoot).Where(r => r != null).Cast<FileRoot>().ToList());

        /// <summary>Finds the File root that applies to a connection: its environment's, else its tenant's.</summary>
        /// <param name="identity">The connection.</param>
        /// <returns>The File root, or <c>null</c> when none covers the connection.</returns>
        public FileRoot? Find(ConnectionIdentity identity)
        {
            var all = this.List();
            return all.FirstOrDefault(r => r.Level == ApprovalScope.Environment && r.AppliesTo(identity))
                ?? all.FirstOrDefault(r => r.Level == ApprovalScope.Tenant && r.AppliesTo(identity));
        }

        /// <summary>Sets a File root for a connection's environment or tenant, replacing the one already set at that level.</summary>
        /// <param name="level"><see cref="ApprovalScope.Environment"/> or <see cref="ApprovalScope.Tenant"/>.</param>
        /// <param name="identity">The connection the person chose it from.</param>
        /// <param name="path">The folder.</param>
        /// <returns>The File root.</returns>
        public FileRoot Set(ApprovalScope level, ConnectionIdentity identity, string path)
        {
            if (level == ApprovalScope.Tenant && identity.TenantId == Guid.Empty)
            {
                throw new ArgumentException("A tenant File root needs the connection's tenant ID.", nameof(identity));
            }

            var root = new FileRoot(
                level,
                level == ApprovalScope.Tenant ? Guid.Empty : identity.OrganizationId,
                level == ApprovalScope.Tenant ? null : identity.EnvironmentId,
                identity.TenantId,
                identity.Name,
                path);
            this.settings.Update(s =>
            {
                s.FileRoots.RemoveAll(p => Same(ToRoot(p), root));
                s.FileRoots.Add(ToPersisted(root));
            });
            this.Changed?.Invoke(this, EventArgs.Empty);
            return root;
        }

        /// <summary>Clears a File root; the environments it covered fall back to their tenant's, if any.</summary>
        /// <param name="root">The File root.</param>
        public void Clear(FileRoot root)
        {
            this.settings.Update(s => s.FileRoots.RemoveAll(p => Same(ToRoot(p), root)));
            this.Changed?.Invoke(this, EventArgs.Empty);
        }

        private static bool Same(FileRoot? a, FileRoot b)
        {
            if (a == null || a.Level != b.Level)
            {
                return false;
            }

            return a.Level == ApprovalScope.Tenant
                ? a.TenantId == b.TenantId
                : a.OrganizationId == b.OrganizationId && a.EnvironmentId == b.EnvironmentId;
        }

        private static FileRoot? ToRoot(PersistedFileRoot persisted)
        {
            if (!Enum.TryParse(persisted.Scope, out ApprovalScope level)
                || (level != ApprovalScope.Environment && level != ApprovalScope.Tenant)
                || string.IsNullOrWhiteSpace(persisted.Path)
                || (level == ApprovalScope.Tenant && persisted.TenantId == Guid.Empty))
            {
                return null;
            }

            return new FileRoot(level, persisted.OrganizationId, persisted.EnvironmentId, persisted.TenantId, persisted.Name, persisted.Path);
        }

        private static PersistedFileRoot ToPersisted(FileRoot root) => new PersistedFileRoot
        {
            Scope = root.Level.ToString(),
            OrganizationId = root.OrganizationId,
            EnvironmentId = root.EnvironmentId,
            TenantId = root.TenantId,
            Name = root.Name,
            Path = root.Path,
        };
    }
}
