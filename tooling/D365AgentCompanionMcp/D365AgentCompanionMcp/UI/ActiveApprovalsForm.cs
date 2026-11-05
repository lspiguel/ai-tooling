// <copyright file="ActiveApprovalsForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;

    /// <summary>
    /// The Active Approvals window (SPEC §8.8): the bearer token, then every grant and File root across all
    /// environments and tenants as one tree. One window for all tabs. Resets and clears ask for confirmation inside the
    /// window, in place of the rows they would affect.
    /// </summary>
    internal sealed class ActiveApprovalsForm : Form
    {
        private static ActiveApprovalsForm? open;

        private readonly CompanionHost host;
        private readonly string originLabel;
        private readonly FlowLayoutPanel body = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill };
        private readonly FlowLayoutPanel footerHost = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        private string? confirming;

        /// <summary>Initializes a new instance of the <see cref="ActiveApprovalsForm"/> class.</summary>
        /// <param name="host">The host.</param>
        /// <param name="originLabel">The tab label recorded with File roots set from here.</param>
        internal ActiveApprovalsForm(CompanionHost host, string originLabel)
        {
            this.host = host;
            this.originLabel = originLabel;
            this.Text = "Active Approvals — every environment and tenant";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(900, 640);
            this.MinimumSize = new Size(640, 360);
            this.ShowInTaskbar = false;

            var token = UiBuilder.Row(UiBuilder.Caption("Token"), new TokenControl(host), UiBuilder.Text("One token for every client — regenerating affects all."));
            token.Dock = DockStyle.Top;

            this.Controls.Add(this.body);
            this.Controls.Add(this.footerHost);
            this.Controls.Add(token);
            this.body.SizeChanged += (s, e) => this.FitWidth();

            this.host.Approvals.Changed += this.OnChanged;
            this.host.FileRoots.Changed += this.OnChanged;
            this.host.Registry.Changed += this.OnChanged;
            this.Render();
        }

        /// <summary>Opens the window, or brings the open one to the front: there is one window for all tabs.</summary>
        /// <param name="host">The host.</param>
        /// <param name="owner">The XrmToolBox main window.</param>
        /// <param name="originLabel">The tab label recorded with File roots set from here.</param>
        public static void ShowFor(CompanionHost host, IWin32Window? owner, string originLabel)
        {
            if (open != null && !open.IsDisposed)
            {
                open.Activate();
                open.BringToFront();
                return;
            }

            open = new ActiveApprovalsForm(host, originLabel);
            open.FormClosed += (s, e) =>
            {
                open.Dispose();
                open = null;
            };
            open.Show(owner);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.host.Approvals.Changed -= this.OnChanged;
                this.host.FileRoots.Changed -= this.OnChanged;
                this.host.Registry.Changed -= this.OnChanged;
            }

            base.Dispose(disposing);
        }

        private static string When(Grant grant) =>
            grant.Scope == ApprovalScope.Session
                ? grant.GrantedOnUtc.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                : grant.GrantedOnUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        private void OnChanged(object? sender, EventArgs e)
        {
            if (this.IsDisposed)
            {
                return;
            }

            // Grants change on the dispatcher's threads: marshal to the UI thread once the window exists.
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(this.Render));
            }
            else
            {
                this.Render();
            }
        }

        private void FitWidth()
        {
            var width = Math.Max(300, this.body.ClientSize.Width - 24);
            foreach (Control control in this.body.Controls)
            {
                control.Width = width;
            }
        }

        private void Render()
        {
            var bound = this.host.Registry.Bound().Select(b => b.Identity).ToList();
            var tree = ApprovalTree.Build(this.host.Approvals.List(), this.host.FileRoots.List(), bound);

            this.body.SuspendLayout();
            foreach (Control control in this.body.Controls)
            {
                control.Dispose();
            }

            this.body.Controls.Clear();

            this.AddHeader("Global", "global", tree.Global);
            if (this.confirming != "global")
            {
                if (tree.Global.Count == 0)
                {
                    this.AddLine("None");
                }

                foreach (var grant in tree.Global)
                {
                    this.AddGrant(grant, bound, 0);
                }
            }

            foreach (var tenant in tree.Tenants.Concat(tree.NoTenant != null ? new[] { tree.NoTenant } : Array.Empty<ApprovalTree.TenantNode>()))
            {
                this.AddTenant(tenant, bound);
            }

            this.body.ResumeLayout(true);
            this.FitWidth();
            this.RenderFooter();
        }

        private void RenderFooter()
        {
            this.footerHost.Controls.Clear();
            if (this.confirming == "all")
            {
                this.footerHost.Controls.Add(UiBuilder.Text("Revoke every grant? Calls already running finish under their approval. File roots are kept."));
                this.footerHost.Controls.Add(UiBuilder.Button("Reset all", (s, e) => this.Apply(() => this.host.Approvals.ClearAll())));
                this.footerHost.Controls.Add(UiBuilder.Button("Cancel", (s, e) => this.Confirm(null)));
                return;
            }

            this.footerHost.Controls.Add(UiBuilder.Button("Reset all approvals", (s, e) => this.Confirm("all")));
            this.footerHost.Controls.Add(UiBuilder.Button("Close", (s, e) => this.Close()));
        }

        private void AddTenant(ApprovalTree.TenantNode tenant, IReadOnlyList<ConnectionIdentity> bound)
        {
            var key = "tenant:" + tenant.TenantId.ToString("D");
            var envGrants = tenant.Environments.Count;
            this.AddHeader(tenant.Label, key, tenant.AllGrants, $"Revoke the grants of {tenant.Label} and of its {envGrants} environment{(envGrants == 1 ? string.Empty : "s")}?");
            if (this.confirming == key)
            {
                return;
            }

            if (tenant.TenantId != Guid.Empty)
            {
                this.AddFileRootLine(tenant.FileRoot, 0, tenant.FileRoot == null ? "Set" : "Change", () => this.ChooseTenantRoot(tenant));
                foreach (var grant in tenant.Grants)
                {
                    this.AddGrant(grant, bound, 0);
                }
            }

            foreach (var environment in tenant.Environments)
            {
                this.AddEnvironment(environment, bound);
            }
        }

        private void AddEnvironment(ApprovalTree.EnvironmentNode environment, IReadOnlyList<ConnectionIdentity> bound)
        {
            var key = "env:" + environment.Key;
            var heading = UiBuilder.Text("▸ " + environment.Name);
            heading.Font = new Font(heading.Font, FontStyle.Bold);
            var buttons = new List<Control> { heading };
            if (environment.OwnRoot == null && !environment.ShowFileRootLine)
            {
                buttons.Add(UiBuilder.Button("File root", (s, e) => this.ChooseEnvironmentRoot(environment)));
            }

            if (environment.Grants.Count > 0)
            {
                buttons.Add(UiBuilder.Button("Reset", (s, e) => this.Confirm(key)));
            }

            if (this.confirming == key)
            {
                this.body.Controls.Add(UiBuilder.Row(heading));
                this.AddQuestion($"Revoke the grants of {environment.Name}?", () => this.host.Approvals.Remove(environment.Grants));
                return;
            }

            this.body.Controls.Add(UiBuilder.Row(buttons.ToArray()));
            if (environment.ShowFileRootLine)
            {
                this.AddFileRootLine(environment.OwnRoot, 1, environment.OwnRoot == null ? "Set" : "Change", () => this.ChooseEnvironmentRoot(environment));
            }

            foreach (var grant in environment.Grants)
            {
                this.AddGrant(grant, bound, 1);
            }
        }

        private void AddHeader(string text, string key, IReadOnlyList<Grant> grants, string? question = null)
        {
            var label = UiBuilder.Text(text);
            label.Font = new Font(label.Font, FontStyle.Bold);
            label.BackColor = SystemColors.ControlLight;
            var buttons = new List<Control> { label };
            if (grants.Count > 0 || key.StartsWith("tenant:", StringComparison.Ordinal))
            {
                buttons.Add(UiBuilder.Button("Reset", (s, e) => this.Confirm(key)));
            }

            if (this.confirming == key)
            {
                this.body.Controls.Add(UiBuilder.Row(label));
                this.AddQuestion(question ?? "Revoke every global grant?", () => this.Reset(key, grants));
                return;
            }

            this.body.Controls.Add(UiBuilder.Row(buttons.ToArray()));
        }

        private void Reset(string key, IReadOnlyList<Grant> grants)
        {
            if (key == "global")
            {
                this.host.Approvals.ClearGlobal();
            }
            else if (key == "tenant:" + Guid.Empty.ToString("D"))
            {
                this.host.Approvals.Remove(grants);
            }
            else if (key.StartsWith("tenant:", StringComparison.Ordinal))
            {
                this.host.Approvals.ClearTenant(Guid.Parse(key.Substring("tenant:".Length)));
            }
        }

        private void AddQuestion(string question, Action apply, string confirmCaption = "Reset")
        {
            this.AddLine(question);
            this.AddLine("Calls already running finish under their approval.");
            this.body.Controls.Add(UiBuilder.Row(
                UiBuilder.Button(confirmCaption, (s, e) => this.Apply(apply)),
                UiBuilder.Button("Cancel", (s, e) => this.Confirm(null))));
        }

        private void AddGrant(Grant grant, IReadOnlyList<ConnectionIdentity> bound, int indent)
        {
            var text = $"{grant.Kind.ToDisplay(),-8} {grant.Scope.ToDisplay(),-16} {When(grant),-17} {grant.OriginTab}";
            var line = UiBuilder.Text(new string(' ', indent * 2) + text);
            line.Font = UiBuilder.Monospace();
            this.body.Controls.Add(UiBuilder.Row(line, UiBuilder.Button("Revoke", (s, e) => this.host.Approvals.Remove(grant))));

            // A grant that matches an environment on the organization ID but not on another ID is not applied.
            if (bound.Any(b => grant.IsStale(b)) && !bound.Any(b => grant.Matches(b)))
            {
                this.AddLine(new string(' ', (indent * 2) + 2) + "⚠ Stale — environment ID differs from the one granted; not applied", UiBuilder.WarningColor);
            }
        }

        private void AddFileRootLine(FileRoot? root, int indent, string action, Action choose)
        {
            var text = new string(' ', indent * 2) + "File root  " + (root == null ? "Not set" : $"{root.Path}   ({root.LevelLabel})");
            var line = UiBuilder.Text(text);
            line.Font = UiBuilder.Monospace();
            var controls = new List<Control> { line, UiBuilder.Button(action, (s, e) => choose()) };
            if (root != null)
            {
                controls.Add(UiBuilder.Button("Clear", (s, e) => this.Confirm("clear:" + root.Level + ":" + (root.Level == ApprovalScope.Tenant ? root.TenantId.ToString("D") : root.OrganizationId.ToString("D") + root.EnvironmentId))));
            }

            var clearKey = root == null ? null : "clear:" + root.Level + ":" + (root.Level == ApprovalScope.Tenant ? root.TenantId.ToString("D") : root.OrganizationId.ToString("D") + root.EnvironmentId);
            if (clearKey != null && this.confirming == clearKey)
            {
                this.AddLine(text);
                this.AddQuestion("Clear this File root? The environments it covered fall back to their tenant's, if any, or are asked again on the next call that takes files.", () => this.host.FileRoots.Clear(root!), "Clear");
                return;
            }

            this.body.Controls.Add(UiBuilder.Row(controls.ToArray()));
        }

        private void AddLine(string text, Color? color = null)
        {
            var label = UiBuilder.Text(text);
            label.Font = UiBuilder.Monospace();
            if (color.HasValue)
            {
                label.ForeColor = color.Value;
            }

            this.body.Controls.Add(label);
        }

        private void Confirm(string? key)
        {
            this.confirming = key;
            this.Render();
        }

        private void Apply(Action change)
        {
            this.confirming = null;
            change();
            this.Render();
        }

        private void ChooseTenantRoot(ApprovalTree.TenantNode tenant) =>
            this.PromptRoot(
                tenant.Environments.Select(e => e.Name).FirstOrDefault() ?? tenant.Label,
                tenant.Label,
                tenantAvailable: true,
                environmentAvailable: false,
                ApprovalScope.Tenant,
                tenant.FileRoot?.Path,
                choice => this.host.FileRoots.Set(choice.Level, new ConnectionIdentity(Guid.Empty, null, tenant.TenantId, tenant.SortName), choice.Path));

        private void ChooseEnvironmentRoot(ApprovalTree.EnvironmentNode environment) =>
            this.PromptRoot(
                environment.Name,
                environment.Name,
                tenantAvailable: environment.TenantId != Guid.Empty,
                environmentAvailable: true,
                ApprovalScope.Environment,
                environment.OwnRoot?.Path,
                choice => this.host.FileRoots.Set(choice.Level, new ConnectionIdentity(environment.OrganizationId, environment.EnvironmentId, environment.TenantId, environment.Name), choice.Path));

        private void PromptRoot(string environmentName, string connectionName, bool tenantAvailable, bool environmentAvailable, ApprovalScope preselected, string? current, Action<FileRootChoice> save)
        {
            var form = new FileRootForm(new FileRootRequest
            {
                ConnectionName = connectionName,
                OrganizationName = environmentName,
                TenantAvailable = tenantAvailable,
                EnvironmentAvailable = environmentAvailable,
                PreselectedLevel = preselected,
                CurrentPath = current,
            });
            form.Decided += (s, choice) =>
            {
                if (choice != null)
                {
                    save(choice);
                }
            };
            form.FormClosed += (s, e) => form.Dispose();
            form.Show(this);
        }
    }
}
