// <copyright file="HostSpikeControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using McTools.Xrm.Connection;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using XrmToolBox.Extensibility;
    using Label = System.Windows.Forms.Label;

    /// <summary>
    /// One tab of the spike. Binds its XrmToolBox connection to the shared host (spec C2–C6) and logs
    /// what spike S6 needs to observe: hook order, connection IDs, conflicts and dialog ownership.
    /// </summary>
    public sealed class HostSpikeControl : PluginControlBase, IConnectionTab
    {
        private static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(120);

        private readonly Label status = new Label { Dock = DockStyle.Top, Height = 64, Padding = new Padding(6) };
        private readonly Button copyToken = new Button { Dock = DockStyle.Top, Height = 28, Text = "Copy bearer token" };
        private readonly TextBox log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font(FontFamily.GenericMonospace, 9f) };
        private readonly Action<string> hostLogHandler;
        private BoundConnection binding;
        private bool attached;

        public HostSpikeControl()
        {
            this.Controls.Add(this.log);
            this.Controls.Add(this.copyToken);
            this.Controls.Add(this.status);

            this.hostLogHandler = line => this.Log($"[host] {line}");
            this.copyToken.Click += (sender, e) =>
            {
                if (CompanionHost.Token != null)
                {
                    Clipboard.SetText(CompanionHost.Token);
                }
            };
            this.Load += this.OnLoad;
        }

        IOrganizationService IConnectionTab.Service => this.Service;

        private Form MainWindow => this.TopLevelControl as Form ?? this.ParentForm;

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            this.Log($"UpdateConnection: '{detail?.ConnectionName}', connectionId {detail?.ConnectionId?.ToString() ?? "null"}, action '{actionName}'");

            // C5: the old binding ends before the new connection is checked.
            CompanionHost.Registry.Unbind(this);
            this.binding = null;
            this.RefreshStatus();

            if (newService == null || detail == null)
            {
                return;
            }

            Task.Run(() => (WhoAmIResponse)newService.Execute(new WhoAmIRequest()))
                .ContinueWith(who => this.Bind(detail, who), TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override void ClosingPlugin(PluginCloseInfo info)
        {
            // No "Are you sure?" prompt in the spike; unbind and release the host (C6, H3).
            info.Cancel = false;
            this.Log("ClosingPlugin");
            CompanionHost.Registry.Unbind(this);
            if (this.attached)
            {
                this.attached = false;
                CompanionHost.Detach();
                CompanionHost.HostLog -= this.hostLogHandler;
            }
        }

        public Task<bool> RequestApprovalAsync(string tool, string summary)
        {
            var completion = new TaskCompletionSource<bool>();
            this.BeginInvoke(new Action(() =>
            {
                var tabVisible = this.Visible;
                using (var dialog = new ChoiceForm($"Approve tool call — {this.binding?.OrganizationFriendlyName}", $"Tool: {tool}\r\n{summary}\r\nConnection: {this.binding?.ConnectionName}", "Allow once", "Deny", ApprovalTimeout))
                {
                    var result = dialog.ShowDialog(this.MainWindow);
                    this.Log($"Approval for {tool}: {result}; owner {this.MainWindow?.GetType().FullName ?? "none"}; tab visible when asked: {tabVisible}");
                    completion.SetResult(result == DialogResult.Yes);
                }
            }));

            return completion.Task;
        }

        public void Log(string line)
        {
            var text = $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}";
            if (this.log.IsHandleCreated && this.log.InvokeRequired)
            {
                this.log.BeginInvoke(new Action(() => this.log.AppendText(text)));
            }
            else
            {
                this.log.AppendText(text);
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            CompanionHost.HostLog += this.hostLogHandler;
            CompanionHost.Attach();
            this.attached = true;
            this.Log($"Load: process {Process.GetCurrentProcess().Id}, AppDomain {AppDomain.CurrentDomain.Id}, host loaded {CompanionHost.LoadedUtc:O}");
            this.RefreshStatus();
        }

        private void Bind(ConnectionDetail detail, Task<WhoAmIResponse> who)
        {
            if (who.IsFaulted)
            {
                this.Log($"WhoAmI failed: {who.Exception?.GetBaseException().Message}");
                return;
            }

            var hasId = detail.ConnectionId.HasValue && detail.ConnectionId.Value != Guid.Empty;
            var candidate = new BoundConnection
            {
                ConnectionId = hasId ? detail.ConnectionId.Value.ToString() : Guid.NewGuid().ToString(),
                ConnectionIdMinted = !hasId,
                ConnectionName = detail.ConnectionName,
                OrganizationId = who.Result.OrganizationId,
                OrganizationFriendlyName = detail.OrganizationFriendlyName,
                Url = detail.WebApplicationUrl,
                BoundOn = DateTime.UtcNow,
                Tab = this,
            };

            var conflict = CompanionHost.Registry.TryBind(candidate);
            if (conflict == null)
            {
                this.binding = candidate;
                this.Log($"Bound as {candidate.ConnectionId}{(candidate.ConnectionIdMinted ? " (minted)" : string.Empty)}");
                this.RefreshStatus();
                return;
            }

            var reason = conflict.ConnectionId == candidate.ConnectionId ? "the same connection" : "the same organization";
            this.Log($"Conflict: '{conflict.ConnectionName}' already uses {reason}");
            this.ShowAlreadyActive(candidate, conflict, reason);
        }

        private void ShowAlreadyActive(BoundConnection candidate, BoundConnection conflict, string reason)
        {
            var message = $"'{candidate.ConnectionName}' is already active in another Agent Companion tab ({reason} as '{conflict.ConnectionName}').\r\n\r\n"
                + "Each environment has one tab, so every call to it is approved and logged in one place.";

            using (var dialog = new ChoiceForm("Connection already active", message, "Close this tab", "Choose another connection"))
            {
                var choice = dialog.ShowDialog(this.MainWindow);
                this.Log($"Already-active dialog: {choice}; owner {this.MainWindow?.GetType().FullName ?? "none"}");
                if (choice == DialogResult.Yes)
                {
                    this.CloseTool();
                }
                else if (choice == DialogResult.No)
                {
                    this.RaiseRequestConnectionEvent(new RequestConnectionEventArgs { ActionName = string.Empty, Control = this });
                }
            }
        }

        private void RefreshStatus()
        {
            var host = CompanionHost.Endpoint ?? $"not running — {CompanionHost.StartError}";
            var tab = this.binding == null ? "unbound" : $"{this.binding.ConnectionName} → {this.binding.ConnectionId}";
            this.status.Text = $"Host: {host}{Environment.NewLine}This tab: {tab}{Environment.NewLine}Tabs open: {CompanionHost.Instances}";
        }
    }
}
