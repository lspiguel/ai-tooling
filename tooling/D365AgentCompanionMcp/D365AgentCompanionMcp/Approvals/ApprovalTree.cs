// <copyright file="ApprovalTree.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Files;

    /// <summary>
    /// The Active Approvals window's tree (SPEC §8.8), built in one testable function apart from the window: Global
    /// first, then each tenant with its File root, its <i>Per tenant</i> grants and its environments, then
    /// <b>No tenant</b>. Each environment lists its own File root, its <i>Per environment</i> grants, then its
    /// <i>Session only</i> grants. Environments are in alphabetical order and tenants follow their first environment.
    /// </summary>
    internal sealed class ApprovalTree
    {
        private ApprovalTree(IReadOnlyList<Grant> global, IReadOnlyList<TenantNode> tenants, TenantNode? noTenant)
        {
            this.Global = global;
            this.Tenants = tenants;
            this.NoTenant = noTenant;
        }

        /// <summary>Gets the global grants, in kind order.</summary>
        public IReadOnlyList<Grant> Global { get; }

        /// <summary>Gets the tenants, in the order of their first environment's name.</summary>
        public IReadOnlyList<TenantNode> Tenants { get; }

        /// <summary>Gets the environments that gave no tenant ID, or <c>null</c> when there are none.</summary>
        public TenantNode? NoTenant { get; }

        /// <summary>Builds the tree.</summary>
        /// <param name="grants">Every grant in the store.</param>
        /// <param name="fileRoots">Every File root.</param>
        /// <param name="bound">The connections bound to a tab now; an environment is listed for them when no File root covers it.</param>
        /// <returns>The tree.</returns>
        public static ApprovalTree Build(IReadOnlyList<Grant> grants, IReadOnlyList<FileRoot> fileRoots, IReadOnlyList<ConnectionIdentity> bound)
        {
            var global = grants.Where(g => g.Scope == ApprovalScope.Always).OrderBy(g => KindOrder(g.Kind)).ToList();
            var tenantGrants = grants.Where(g => g.Scope == ApprovalScope.Tenant).ToList();

            var environments = new Dictionary<string, EnvironmentBuilder>(StringComparer.Ordinal);
            EnvironmentBuilder Environment(string key, string name, Guid tenantId, Guid organizationId, string environmentId)
            {
                if (!environments.TryGetValue(key, out var found))
                {
                    found = new EnvironmentBuilder(key, name, tenantId, organizationId, environmentId);
                    environments[key] = found;
                }
                else if (found.TenantId == Guid.Empty && tenantId != Guid.Empty)
                {
                    found.TenantId = tenantId;
                }

                return found;
            }

            foreach (var grant in grants.Where(g => g.Scope == ApprovalScope.Session || g.Scope == ApprovalScope.Environment))
            {
                var environment = Environment(grant.EnvironmentKey, grant.OrganizationName, grant.TenantId, grant.OrganizationId, grant.EnvironmentId);
                environment.Grants.Add(grant);
            }

            foreach (var root in fileRoots.Where(r => r.Level == ApprovalScope.Environment))
            {
                var key = root.EnvironmentId.Length > 0 ? root.EnvironmentId : "org:" + root.OrganizationId.ToString("D");
                Environment(key, root.Name, root.TenantId, root.OrganizationId, root.EnvironmentId).OwnRoot = root;
            }

            foreach (var identity in bound)
            {
                var key = identity.EnvironmentId.Length > 0 ? identity.EnvironmentId : "org:" + identity.OrganizationId.ToString("D");
                var covered = fileRoots.Any(r => r.AppliesTo(identity));
                if (environments.ContainsKey(key) || !covered)
                {
                    Environment(key, identity.Name, identity.TenantId, identity.OrganizationId, identity.EnvironmentId);
                }
            }

            var tenantIds = environments.Values.Select(e => e.TenantId)
                .Concat(tenantGrants.Select(g => g.TenantId))
                .Concat(fileRoots.Where(r => r.Level == ApprovalScope.Tenant).Select(r => r.TenantId))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var tenants = tenantIds
                .Select(id => BuildTenant(id, tenantGrants, fileRoots, environments.Values.Where(e => e.TenantId == id)))
                .OrderBy(t => t.SortName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.TenantId)
                .ToList();

            var orphans = environments.Values.Where(e => e.TenantId == Guid.Empty).ToList();
            var noTenant = orphans.Count == 0 ? null : BuildTenant(Guid.Empty, Array.Empty<Grant>(), Array.Empty<FileRoot>(), orphans);
            return new ApprovalTree(global, tenants, noTenant);
        }

        /// <summary>The order grants are listed in within a group: Read, Records, Write, Dev env.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The position.</returns>
        public static int KindOrder(ApprovalKind kind)
        {
            switch (kind)
            {
                case ApprovalKind.ReadApproval:
                    return 0;
                case ApprovalKind.RecordRead:
                    return 1;
                case ApprovalKind.Write:
                    return 2;
                default:
                    return 3;
            }
        }

        private static TenantNode BuildTenant(Guid tenantId, IReadOnlyList<Grant> tenantGrants, IReadOnlyList<FileRoot> fileRoots, IEnumerable<EnvironmentBuilder> environments)
        {
            var nodes = environments
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => e.ToNode(tenantId == Guid.Empty))
                .ToList();
            var own = tenantGrants.Where(g => g.TenantId == tenantId).OrderBy(g => KindOrder(g.Kind)).ToList();
            var root = tenantId == Guid.Empty ? null : fileRoots.FirstOrDefault(r => r.Level == ApprovalScope.Tenant && r.TenantId == tenantId);
            var sortName = nodes.Count > 0 ? nodes[0].Name : own.Select(g => g.OrganizationName).FirstOrDefault() ?? root?.Name ?? string.Empty;
            return new TenantNode(tenantId, sortName, root, own, nodes);
        }

        /// <summary>A tenant in the tree, or <b>No tenant</b> when <see cref="TenantId"/> is empty.</summary>
        internal sealed class TenantNode
        {
            /// <summary>Initializes a new instance of the <see cref="TenantNode"/> class.</summary>
            /// <param name="tenantId">The tenant, or empty for <b>No tenant</b>.</param>
            /// <param name="sortName">The first environment's name, which orders the tenants.</param>
            /// <param name="fileRoot">The tenant's File root, or <c>null</c> for <i>Not set</i>.</param>
            /// <param name="grants">The tenant's own <i>Per tenant</i> grants.</param>
            /// <param name="environments">The environments, in order.</param>
            public TenantNode(Guid tenantId, string sortName, FileRoot? fileRoot, IReadOnlyList<Grant> grants, IReadOnlyList<EnvironmentNode> environments)
            {
                this.TenantId = tenantId;
                this.SortName = sortName;
                this.FileRoot = fileRoot;
                this.Grants = grants;
                this.Environments = environments;
            }

            /// <summary>Gets the tenant ID; empty for <b>No tenant</b>.</summary>
            public Guid TenantId { get; }

            /// <summary>Gets the first environment's name.</summary>
            public string SortName { get; }

            /// <summary>Gets the tenant's File root, or <c>null</c> when not set.</summary>
            public FileRoot? FileRoot { get; }

            /// <summary>Gets the <i>Per tenant</i> grants, in kind order.</summary>
            public IReadOnlyList<Grant> Grants { get; }

            /// <summary>Gets the environments, in alphabetical order.</summary>
            public IReadOnlyList<EnvironmentNode> Environments { get; }

            /// <summary>Gets the heading: the shortened tenant ID, or <i>No tenant</i>.</summary>
            public string Label => this.TenantId == Guid.Empty ? "No tenant" : "Tenant " + this.TenantId.ToString("D").Substring(0, 8) + "…";

            /// <summary>Gets every grant a reset of this node revokes: its own and those of its environments.</summary>
            public IReadOnlyList<Grant> AllGrants => this.Grants.Concat(this.Environments.SelectMany(e => e.Grants)).ToList();
        }

        /// <summary>An environment in the tree.</summary>
        internal sealed class EnvironmentNode
        {
            /// <summary>Initializes a new instance of the <see cref="EnvironmentNode"/> class.</summary>
            /// <param name="key">The environment key.</param>
            /// <param name="name">The friendly name.</param>
            /// <param name="organizationId">The organization ID.</param>
            /// <param name="environmentId">The environment ID, when known.</param>
            /// <param name="tenantId">The tenant ID, when known.</param>
            /// <param name="ownRoot">The File root set for this environment, or <c>null</c>.</param>
            /// <param name="showFileRootLine">Whether the File root line shows: when it has its own, and always under <b>No tenant</b>.</param>
            /// <param name="grants">The environment's grants: <i>Per environment</i>, then <i>Session only</i>.</param>
            public EnvironmentNode(string key, string name, Guid organizationId, string environmentId, Guid tenantId, FileRoot? ownRoot, bool showFileRootLine, IReadOnlyList<Grant> grants)
            {
                this.Key = key;
                this.Name = name;
                this.OrganizationId = organizationId;
                this.EnvironmentId = environmentId;
                this.TenantId = tenantId;
                this.OwnRoot = ownRoot;
                this.ShowFileRootLine = showFileRootLine;
                this.Grants = grants;
            }

            /// <summary>Gets the environment key, as <see cref="Grant.EnvironmentKey"/> names it.</summary>
            public string Key { get; }

            /// <summary>Gets the friendly name.</summary>
            public string Name { get; }

            /// <summary>Gets the organization ID.</summary>
            public Guid OrganizationId { get; }

            /// <summary>Gets the environment ID; empty when not known.</summary>
            public string EnvironmentId { get; }

            /// <summary>Gets the tenant ID; empty when not known.</summary>
            public Guid TenantId { get; }

            /// <summary>Gets the File root set for this environment, or <c>null</c>.</summary>
            public FileRoot? OwnRoot { get; }

            /// <summary>Gets a value indicating whether the File root line shows.</summary>
            public bool ShowFileRootLine { get; }

            /// <summary>Gets the grants: <i>Per environment</i>, then <i>Session only</i>, each in kind order.</summary>
            public IReadOnlyList<Grant> Grants { get; }
        }

        private sealed class EnvironmentBuilder
        {
            public EnvironmentBuilder(string key, string name, Guid tenantId, Guid organizationId, string environmentId)
            {
                this.Key = key;
                this.Name = name;
                this.TenantId = tenantId;
                this.OrganizationId = organizationId;
                this.EnvironmentId = environmentId;
            }

            public string Key { get; }

            public string Name { get; }

            public Guid TenantId { get; set; }

            public Guid OrganizationId { get; }

            public string EnvironmentId { get; }

            public FileRoot? OwnRoot { get; set; }

            public List<Grant> Grants { get; } = new List<Grant>();

            public EnvironmentNode ToNode(bool noTenant)
            {
                var ordered = this.Grants
                    .OrderBy(g => g.Scope == ApprovalScope.Environment ? 0 : 1)
                    .ThenBy(g => KindOrder(g.Kind))
                    .ToList();
                return new EnvironmentNode(this.Key, this.Name, this.OrganizationId, this.EnvironmentId, this.TenantId, this.OwnRoot, noTenant || this.OwnRoot != null, ordered);
            }
        }
    }
}
