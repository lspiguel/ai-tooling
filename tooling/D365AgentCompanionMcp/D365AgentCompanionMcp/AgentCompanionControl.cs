// <copyright file="AgentCompanionControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Setup;
    using Lspiguel.Xrm.D365AgentCompanionMcp.UI;
    using McTools.Xrm.Connection;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Tooling.Connector;
    using XrmToolBox.Extensibility;
    using Label = System.Windows.Forms.Label;

    /// <summary>
    /// One tab of the plugin — an instance (SPEC §5). It binds its XrmToolBox connection to the shared host, is the
    /// window where a person approves every call to that connection, and shows its call log and the host log (§8.1).
    /// </summary>
    public sealed class AgentCompanionControl : PluginControlBase, IHostInstance
    {
        /// <summary>The host log's fixed height, in pixels.</summary>
        private const int HostLogHeight = 160;

        private static int tabCounter;

        private readonly CompanionHost host = CompanionHost.Shared;
        private readonly string tabLabel = $"tab {Interlocked.Increment(ref tabCounter)}";
        private readonly CallLog callLog = new CallLog();
        private readonly CallLog hostLog = new CallLog();
        private readonly List<Action> deferred = new List<Action>();

        private readonly Label connectionText = UiBuilder.Text();
        private readonly Label urlText = UiBuilder.Text();
        private readonly Button connectButton;
        private readonly Label devText = UiBuilder.Text();
        private readonly Button confirmDevButton;
        private readonly Button clearDevButton;
        private readonly CheckBox publisherCreation = new CheckBox { Text = "Publisher creation", AutoSize = true };
        private readonly Label fileRootText = UiBuilder.Text();
        private readonly Label readsText = UiBuilder.Text();
        private readonly Label recordsText = UiBuilder.Text();
        private readonly Label writesText = UiBuilder.Text();
        private readonly Button revokeButton;

        private readonly Label serverText = UiBuilder.Text();
        private readonly Button startStopButton;
        private readonly NumericUpDown port = UiBuilder.Number(HostLimits.MinPort, HostLimits.MaxPort);
        private readonly Label boundText = UiBuilder.Text();
        private readonly NumericUpDown maxPageSize = UiBuilder.Number(HostLimits.MinPageSize, HostLimits.MaxPageSize);
        private readonly NumericUpDown responseCap = UiBuilder.Number(HostLimits.MinResponseCap, HostLimits.MaxResponseCap, 90);
        private readonly NumericUpDown approvalTimeout = UiBuilder.Number(HostLimits.MinPanelApprovalTimeout, HostLimits.MaxApprovalTimeout, 60);
        private readonly Label alwaysText = UiBuilder.Text();
        private readonly Label noticeText = UiBuilder.Text();

        private readonly LogView callLogView = new LogView();
        private readonly LogView hostLogView = new LogView();
        private readonly Label countsText = UiBuilder.Text();

        private readonly TableLayoutPanel mainPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };

        private BoundConnection? binding;
        private SetupWizardControl? wizard;
        private bool attached;
        private bool closingFromDialog;

        /// <summary>Initializes a new instance of the <see cref="AgentCompanionControl"/> class.</summary>
        public AgentCompanionControl()
        {
            this.connectButton = UiBuilder.Button("Connect…", (s, e) => this.RequestConnection());
            this.confirmDevButton = UiBuilder.Button("Confirm…", (s, e) => this.PromptDevConfirmation());
            this.clearDevButton = UiBuilder.Button("Clear", (s, e) => this.ClearDevConfirmation());
            this.revokeButton = UiBuilder.Button("Revoke", (s, e) => this.RevokeReads());
            this.startStopButton = UiBuilder.Button("Stop server — all connections", (s, e) => this.StartOrStop());
            this.noticeText.ForeColor = UiBuilder.WarningColor;
            this.alwaysText.ForeColor = UiBuilder.WarningColor;

            this.publisherCreation.CheckedChanged += (s, e) => this.ApplyConnectionSettings();
            this.callLog.EntryAdded += (s, entry) => this.RunOnUi(() => this.callLogView.Append(entry));
            this.callLog.Cleared += (s, e) => this.RunOnUi(this.callLogView.ClearLines);
            this.hostLog.EntryAdded += (s, entry) => this.RunOnUi(() => this.hostLogView.Append(entry));
            this.hostLog.Cleared += (s, e) => this.RunOnUi(this.hostLogView.ClearLines);

            this.BuildLayout();
            this.ShowSetupIfNeeded();
            this.Load += (s, e) =>
            {
                this.AttachToHost();
                this.RefreshPanel();
            };
            this.HandleCreated += (s, e) => this.FlushDeferred();
        }

        /// <inheritdoc/>
        string IHostInstance.TabLabel => this.tabLabel;

        /// <inheritdoc/>
        CallLog IHostInstance.CallLog => this.callLog;

        private Form? MainWindow => this.TopLevelControl as Form ?? this.ParentForm;

        /// <inheritdoc/>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            // XrmToolBox can deliver the connection before Load (S6), so attach on whichever comes first (H1).
            this.AttachToHost();

            // C5: the old binding ends before the new connection is checked.
            this.host.ReleaseBinding(this);
            this.binding = null;

            if (newService == null || detail == null)
            {
                this.RefreshPanel();
                return;
            }

            var hasId = detail.ConnectionId.HasValue && detail.ConnectionId.Value != Guid.Empty;
            var candidate = new BoundConnection(
                this,
                hasId ? detail.ConnectionId!.Value.ToString() : Guid.NewGuid().ToString(),
                !hasId,
                detail.ConnectionName ?? "(unnamed connection)",
                detail.Organization ?? string.Empty,
                detail.OrganizationFriendlyName ?? detail.ConnectionName ?? string.Empty,
                detail.WebApplicationUrl ?? detail.OriginalUrl ?? string.Empty);

            // C3: claim the connection ID synchronously, in XrmToolBox's hook order, so the first tab of a toolbar connect wins.
            var conflict = this.host.Registry.Claim(candidate);
            if (conflict != null)
            {
                this.RunOnUi(() => this.ShowAlreadyActive(candidate, conflict, sameOrganization: false));
                this.RefreshPanel();
                return;
            }

            this.binding = candidate;
            this.RefreshPanel();
            var ui = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() => Identify(newService)).ContinueWith(identity => this.CompleteBinding(candidate, identity), ui);
        }

        /// <inheritdoc/>
        public override void ClosingPlugin(PluginCloseInfo info)
        {
            if (!this.closingFromDialog)
            {
                base.ClosingPlugin(info);
            }

            if (info.Cancel)
            {
                return;
            }

            // C6, H3: closing ends the binding; the last tab stops the host.
            this.DetachFromHost();
        }

        /// <inheritdoc/>
        Task<ApprovalDecision> IApprovalPrompt.PromptAsync(ApprovalRequest request, CancellationToken cancellation)
        {
            var completion = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            ApprovalForm? form = null;
            var registration = cancellation.Register(() =>
            {
                completion.TrySetCanceled();
                this.RunOnUi(() => form?.Withdraw());
            });
            completion.Task.ContinueWith(t => registration.Dispose(), TaskScheduler.Default);

            this.RunOnUi(() =>
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                form = new ApprovalForm(request);
                form.Decided += (s, decision) => completion.TrySetResult(decision);
                form.FormClosed += (s, e) =>
                {
                    completion.TrySetResult(ApprovalDecision.Deny);
                    form.Dispose();
                };

                // Owned by the main window, so it appears whichever tab is in front (C9).
                form.Show(this.MainWindow);
            });

            return completion.Task;
        }

        /// <inheritdoc/>
        Task<FileRootChoice?> IFileRootPrompt.PromptFileRootAsync(FileRootRequest request, CancellationToken cancellation)
        {
            var completion = new TaskCompletionSource<FileRootChoice?>(TaskCreationOptions.RunContinuationsAsynchronously);
            FileRootForm? form = null;
            var registration = cancellation.Register(() =>
            {
                completion.TrySetCanceled();
                this.RunOnUi(() => form?.Withdraw());
            });
            completion.Task.ContinueWith(t => registration.Dispose(), TaskScheduler.Default);

            this.RunOnUi(() =>
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                form = new FileRootForm(request);
                form.Decided += (s, choice) => completion.TrySetResult(choice);
                form.FormClosed += (s, e) =>
                {
                    completion.TrySetResult(null);
                    form.Dispose();
                };

                // Owned by the main window, like the approval dialog, so it appears whichever tab is in front (C9).
                form.Show(this.MainWindow);
            });

            return completion.Task;
        }

        private static Identity Identify(IOrganizationService service)
        {
            // Calls run on a clone of the tab's service where XrmToolBox allows it (SPEC §5 Threading).
            var callService = service;
            var impersonating = false;
            string? environmentId = null;
            var tenantId = Guid.Empty;
            if (service is CrmServiceClient client)
            {
                impersonating = client.CallerId != Guid.Empty;

                // The environment and tenant IDs say which environment this is, beyond the organization ID (SPEC 7, Identity).
                environmentId = client.EnvironmentId;
                tenantId = client.TenantId;
                try
                {
                    var clone = client.Clone();
                    if (clone != null && clone.IsReady)
                    {
                        clone.CallerId = client.CallerId;
                        callService = clone;
                    }
                }
                catch (Exception)
                {
                    // Some authentication types cannot be cloned; calls are serialized per connection anyway.
                }
            }

            var who = (WhoAmIResponse)callService.Execute(new WhoAmIRequest());
            return new Identity(callService, who.OrganizationId, who.UserId, impersonating, environmentId, tenantId);
        }

        private static string DescribeDev(ApprovalScope? scope)
        {
            switch (scope)
            {
                case ApprovalScope.Once:
                    return "Confirmed — this tab's binding";
                case ApprovalScope.Session:
                    return "Confirmed — this session";
                case ApprovalScope.Environment:
                    return "Confirmed — this environment, remembered";
                default:
                    return "Not confirmed — Write tools refuse on this connection";
            }
        }

        private static string Describe(ApprovalScope? scope, string none)
        {
            switch (scope)
            {
                case ApprovalScope.Session:
                    return "Approved — this session";
                case ApprovalScope.Environment:
                    return "Approved — this environment, remembered";
                case ApprovalScope.Tenant:
                    return "Approved — every environment in this tenant";
                case ApprovalScope.Always:
                    return "Approved — Global, every environment";
                default:
                    return none;
            }
        }

        private void BuildLayout()
        {
            var tabGroup = UiBuilder.Group(
                "This tab",
                UiBuilder.Row(UiBuilder.Caption("This tab"), this.connectionText, this.connectButton),
                UiBuilder.Row(UiBuilder.Caption(string.Empty), this.urlText),
                UiBuilder.Row(UiBuilder.Caption("Dev env"), this.devText, this.confirmDevButton, this.clearDevButton),
                UiBuilder.Row(UiBuilder.Caption("Settings"), this.publisherCreation),
                UiBuilder.Row(UiBuilder.Caption("File root"), this.fileRootText),
                UiBuilder.Row(UiBuilder.Caption("Reads"), this.readsText, this.revokeButton),
                UiBuilder.Row(UiBuilder.Caption("Records"), this.recordsText),
                UiBuilder.Row(UiBuilder.Caption("Writes"), this.writesText));

            var hostGroup = UiBuilder.Group(
                "Host — shared by all tabs",
                UiBuilder.Row(UiBuilder.Caption("Server"), this.serverText, this.startStopButton),
                UiBuilder.Row(UiBuilder.Caption("Port"), this.port, UiBuilder.Button("Apply port — all clients", (s, e) => this.ApplyPort())),
                UiBuilder.Row(UiBuilder.Caption("Clients"), UiBuilder.Button("Copy client config", (s, e) => this.CopyClientConfig()), UiBuilder.Button("Active Approvals…", (s, e) => this.ShowActiveApprovals())),
                UiBuilder.Row(UiBuilder.Caption("Bound"), this.boundText),
                UiBuilder.Row(
                    UiBuilder.Caption("Limits"),
                    UiBuilder.Text("Max page size"),
                    this.maxPageSize,
                    UiBuilder.Text("Response cap"),
                    this.responseCap,
                    UiBuilder.Text("Timeout (s)"),
                    this.approvalTimeout,
                    UiBuilder.Button("Apply", (s, e) => this.ApplyLimits())),
                UiBuilder.Row(this.alwaysText),
                UiBuilder.Row(this.noticeText));

            var footer = UiBuilder.Row(
                this.countsText,
                UiBuilder.Button("Save as…", (s, e) => this.SaveLog()),
                UiBuilder.Button("Clear", (s, e) =>
                {
                    this.callLog.Clear();
                    this.hostLog.Clear();
                }));

            var table = this.mainPanel;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            void Add(Control control, RowStyle style)
            {
                table.RowStyles.Add(style);
                control.Dock = DockStyle.Fill;
                table.Controls.Add(control);
            }

            Add(tabGroup, new RowStyle(SizeType.AutoSize));
            Add(hostGroup, new RowStyle(SizeType.AutoSize));
            Add(UiBuilder.Text("Call log — this connection   (no wrap; double-click a line for its detail)"), new RowStyle(SizeType.AutoSize));

            // The call log takes whatever height is left; the host log keeps a fixed height of about ten lines.
            Add(this.callLogView, new RowStyle(SizeType.Percent, 100));
            Add(UiBuilder.Text("Host log — list_connections and calls naming an unknown connection"), new RowStyle(SizeType.AutoSize));
            Add(this.hostLogView, new RowStyle(SizeType.Absolute, HostLogHeight));
            Add(footer, new RowStyle(SizeType.AutoSize));
            this.Controls.Add(table);
        }

        private void ShowSetupIfNeeded()
        {
            // §8.7: the wizard on a first run, the Plugin updated page after an update. The host still starts as usual (H2).
            var view = this.host.Settings.RequiredSetup(this.host.Version);
            if (view == SetupView.None)
            {
                return;
            }

            this.wizard = new SetupWizardControl(view, this.host.Settings.CompletedSetupVersion(), this.host.Version, this.CurrentPromptValues);
            this.wizard.Completed += (s, e) => this.CompleteSetup();
            this.wizard.Cancelled += (s, e) =>
            {
                // Nothing is recorded, so the wizard shows again the next time the plugin opens.
                this.closingFromDialog = true;
                this.CloseTool();
            };
            this.mainPanel.Visible = false;
            this.Controls.Add(this.wizard);
            this.wizard.BringToFront();
        }

        private void CompleteSetup()
        {
            this.host.Settings.CompleteSetup(this.host.Version);
            if (this.wizard != null)
            {
                this.Controls.Remove(this.wizard);
                this.wizard.Dispose();
                this.wizard = null;
            }

            this.mainPanel.Visible = true;
            this.RefreshPanel();
        }

        private PromptValues CurrentPromptValues()
        {
            var bound = this.binding?.State == BindingState.Bound ? this.binding : null;
            return new PromptValues
            {
                PluginVersion = this.host.Version,
                Endpoint = this.host.Endpoint,
                ClientConfig = ClientConfig.Compose(this.host.Endpoint, this.host.Tokens.Token),
                OrganizationUrl = bound?.OrganizationUrl,
                SkillNames = SkillExporter.SkillNames(),
            };
        }

        private void AttachToHost()
        {
            if (this.attached)
            {
                return;
            }

            this.attached = true;
            this.host.StateChanged += this.OnHostChanged;
            this.host.HostCallLogged += this.OnHostCallLogged;
            this.host.NoticeRaised += this.OnHostNotice;
            this.host.Attach(this);
        }

        private void DetachFromHost()
        {
            if (!this.attached)
            {
                return;
            }

            this.attached = false;
            this.host.StateChanged -= this.OnHostChanged;
            this.host.HostCallLogged -= this.OnHostCallLogged;
            this.host.NoticeRaised -= this.OnHostNotice;
            if (this.binding != null)
            {
                this.binding.ActivityChanged -= this.OnActivityChanged;
            }

            this.binding = null;
            this.host.Detach(this);
        }

        private void OnHostChanged(object? sender, EventArgs e) => this.RunOnUi(this.RefreshPanel);

        private void OnHostCallLogged(object? sender, CallLogEntry entry) => this.hostLog.Add(entry);

        private void OnHostNotice(object? sender, string notice) => this.RunOnUi(this.RefreshPanel);

        private void OnActivityChanged(object? sender, EventArgs e) => this.RunOnUi(this.RefreshCounts);

        private void CompleteBinding(BoundConnection candidate, Task<Identity> identity)
        {
            if (!ReferenceEquals(candidate, this.binding))
            {
                // Superseded: the tab changed connection or closed while the identity call ran.
                return;
            }

            if (identity.IsFaulted)
            {
                this.host.ReleaseBinding(this);
                this.binding = null;
                this.noticeText.Text = $"This tab could not identify its connection: {identity.Exception?.GetBaseException().Message}";
                this.RefreshPanel();
                return;
            }

            var result = identity.Result;
            var outcome = this.host.CompleteBinding(candidate, result.OrganizationId, result.EnvironmentId, result.TenantId, result.UserId, result.Impersonating, result.Service, DateTime.UtcNow, out var conflict);
            if (outcome != BindOutcome.Bound)
            {
                this.binding = null;
                this.RefreshPanel();
                if (outcome == BindOutcome.OrganizationConflict && conflict != null)
                {
                    this.ShowAlreadyActive(candidate, conflict, sameOrganization: true);
                }

                return;
            }

            candidate.ActivityChanged += this.OnActivityChanged;
            this.RefreshPanel();
            if (this.host.DevEnvironmentScope(candidate) == null)
            {
                this.PromptDevConfirmation();
            }
        }

        private void ShowAlreadyActive(BoundConnection candidate, BoundConnection conflict, bool sameOrganization)
        {
            DialogResult choice;
            using (var dialog = new ConnectionActiveForm(candidate.ConnectionName, candidate.OrganizationFriendlyName, conflict.ConnectionName, sameOrganization))
            {
                choice = dialog.ShowDialog(this.MainWindow);
            }

            if (choice == DialogResult.Abort)
            {
                this.closingFromDialog = true;
                this.CloseTool();
            }
            else if (choice == DialogResult.Retry)
            {
                // The new choice arrives through UpdateConnection and goes through the same check (C3).
                this.RequestConnection();
            }
        }

        private void RequestConnection() =>
            this.RaiseRequestConnectionEvent(new RequestConnectionEventArgs { ActionName = string.Empty, Control = this });

        private void PromptDevConfirmation()
        {
            var current = this.binding;
            if (current == null || current.State != BindingState.Bound)
            {
                return;
            }

            ApprovalScope? choice;
            using (var dialog = new DevConfirmationForm(current.ConnectionName, current.OrganizationFriendlyName, current.OrganizationUrl))
            {
                if (dialog.ShowDialog(this.MainWindow) != DialogResult.OK)
                {
                    return;
                }

                choice = dialog.Choice;
            }

            if (choice == ApprovalScope.Once)
            {
                current.DevConfirmedOnce = true;
            }
            else if (choice.HasValue)
            {
                this.host.Approvals.Allow(ApprovalKind.DevEnvironment, choice.Value, current.Identity, this.tabLabel);
            }

            this.RefreshPanel();
        }

        private void ClearDevConfirmation()
        {
            if (this.binding?.State == BindingState.Bound)
            {
                this.binding.DevConfirmedOnce = false;
                this.host.Approvals.RevokeEnvironment(ApprovalKind.DevEnvironment, this.binding.Identity);
                this.RefreshPanel();
            }
        }

        private void RevokeReads()
        {
            if (this.binding?.State == BindingState.Bound)
            {
                // This tab's grants for its own environment; tenant and Global grants are revoked in Active Approvals.
                foreach (var kind in new[] { ApprovalKind.ReadApproval, ApprovalKind.RecordRead, ApprovalKind.Write })
                {
                    this.host.Approvals.RevokeEnvironment(kind, this.binding.Identity);
                }

                this.RefreshPanel();
            }
        }

        private void ApplyConnectionSettings()
        {
            if (this.binding?.State == BindingState.Bound)
            {
                this.binding.PublisherCreationEnabled = this.publisherCreation.Checked;
            }
        }

        private void StartOrStop()
        {
            if (this.host.State == HostState.Running)
            {
                this.host.Stop();
            }
            else
            {
                this.host.Start();
            }
        }

        private void ApplyPort()
        {
            if ((int)this.port.Value != this.host.Port)
            {
                this.host.ChangePort((int)this.port.Value);
            }
        }

        private void ApplyLimits() =>
            this.host.ChangeLimits((int)this.maxPageSize.Value, (int)this.responseCap.Value, (int)this.approvalTimeout.Value);

        private void ShowActiveApprovals() => ActiveApprovalsForm.ShowFor(this.host, this.MainWindow, this.tabLabel);

        private void CopyClientConfig() => Clipboard.SetText(ClientConfig.Compose(this.host.Endpoint, this.host.Tokens.Token));

        private void SaveLog()
        {
            var current = this.binding?.State == BindingState.Bound ? this.binding : null;
            using (var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                FileName = LogFileWriter.DefaultFileName(current?.ConnectionName, DateTime.Now),
                OverwritePrompt = true,
            })
            {
                if (dialog.ShowDialog(this.MainWindow) != DialogResult.OK)
                {
                    return;
                }

                var folder = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                if (LogFileWriter.IsInsideGitWorkingTree(folder)
                    && MessageBox.Show(
                        this.MainWindow,
                        "This folder is inside a Git working tree. A call log can hold names and IDs from the environment, which must stay out of repositories.\r\n\r\nSave it here anyway?",
                        "Save call log",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    return;
                }

                var content = LogFileWriter.Compose(current?.ConnectionName, current?.OrganizationFriendlyName, DateTime.Now, this.callLog.Lines(), this.hostLog.Lines());
                LogFileWriter.Write(dialog.FileName, content);
            }
        }

        private void RefreshPanel()
        {
            var current = this.binding;
            var bound = current?.State == BindingState.Bound ? current : null;

            this.connectionText.Text = current == null
                ? "Not connected — connect this tab to an environment."
                : bound == null ? $"{current.ConnectionName} — identifying…" : $"{bound.ConnectionName} → {bound.OrganizationFriendlyName}";
            this.urlText.Text = bound?.OrganizationUrl ?? string.Empty;
            this.connectButton.Visible = current == null;

            var devScope = bound == null ? null : this.host.DevEnvironmentScope(bound);
            this.devText.Text = bound == null ? "—" : DescribeDev(devScope);
            this.confirmDevButton.Enabled = bound != null && devScope == null;
            this.clearDevButton.Enabled = bound != null && devScope != null;
            this.publisherCreation.Enabled = bound != null;
            this.publisherCreation.Checked = bound?.PublisherCreationEnabled ?? false;
            var identity = bound?.Identity;
            var fileRoot = identity == null ? null : this.host.FileRoots.Find(identity);
            this.fileRootText.Text = bound == null
                ? "—"
                : fileRoot == null ? "Not set — asked for on first use" : $"{fileRoot.Path}   {fileRoot.LevelLabel} · read-only here, change it in Active Approvals";
            var readScope = identity == null ? null : this.host.Approvals.Find(ApprovalKind.ReadApproval, identity)?.Scope;
            var recordScope = identity == null ? null : this.host.Approvals.Find(ApprovalKind.RecordRead, identity)?.Scope;
            var writeScope = identity == null ? null : this.host.Approvals.Find(ApprovalKind.Write, identity)?.Scope;
            this.readsText.Text = bound == null ? "—" : Describe(readScope, "Every Read call prompts");
            this.recordsText.Text = bound == null ? "—" : Describe(recordScope, "Every query that reads records prompts");
            this.writesText.Text = bound == null ? "—" : Describe(writeScope, "Every Write call prompts");
            this.revokeButton.Enabled = bound != null && new[] { readScope, recordScope, writeScope }.Any(s => s == ApprovalScope.Session || s == ApprovalScope.Environment);

            switch (this.host.State)
            {
                case HostState.Running:
                    this.serverText.Text = $"● Running   {this.host.Endpoint}";
                    this.startStopButton.Text = "Stop server — all connections";
                    break;
                case HostState.Failed:
                    this.serverText.Text = $"⚠ Not running — {this.host.StartError}";
                    this.startStopButton.Text = "Start server — all connections";
                    break;
                default:
                    this.serverText.Text = "○ Stopped";
                    this.startStopButton.Text = "Start server — all connections";
                    break;
            }

            this.port.Value = this.host.Port;
            this.host.Settings.Read(s =>
            {
                this.maxPageSize.Value = s.MaxPageSize;
                this.responseCap.Value = s.ResponseCapCharacters;
                this.approvalTimeout.Value = Math.Max(HostLimits.MinPanelApprovalTimeout, s.ApprovalTimeoutSeconds);
                return 0;
            });

            var all = this.host.Registry.Bound();
            this.boundText.Text = all.Count == 0
                ? "0"
                : $"{all.Count} — {string.Join(", ", all.Select(b => ReferenceEquals(b, bound) ? $"{b.OrganizationFriendlyName} (this tab)" : b.OrganizationFriendlyName))}";

            var global = new List<string>();
            if (this.host.Approvals.IsGlobal(ApprovalKind.ReadApproval))
            {
                global.Add("⚠ Reads are approved on every environment (Global).");
            }

            if (this.host.Approvals.IsGlobal(ApprovalKind.RecordRead))
            {
                global.Add("⚠ Record reads are approved on every environment (Global).");
            }

            this.alwaysText.Text = string.Join("  ", global);
            if (this.host.Notice != null)
            {
                this.noticeText.Text = this.host.Notice;
            }

            this.wizard?.ShowStatus(this.serverText.Text, bound == null ? this.connectionText.Text : $"{this.connectionText.Text}   {bound.OrganizationUrl}");
            this.RefreshCounts();
        }

        private void RefreshCounts()
        {
            var current = this.binding;
            this.countsText.Text = $"Pending approvals: {current?.Pending ?? 0}   Running: {current?.Running ?? 0}";
        }

        private void RunOnUi(Action action)
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (this.IsHandleCreated)
            {
                this.BeginInvoke(action);
                return;
            }

            lock (this.deferred)
            {
                this.deferred.Add(action);
            }
        }

        private void FlushDeferred()
        {
            List<Action> pending;
            lock (this.deferred)
            {
                pending = this.deferred.ToList();
                this.deferred.Clear();
            }

            foreach (var action in pending)
            {
                this.BeginInvoke(action);
            }
        }

        private sealed class Identity
        {
            public Identity(IOrganizationService service, Guid organizationId, Guid userId, bool impersonating, string? environmentId, Guid tenantId)
            {
                this.Service = service;
                this.OrganizationId = organizationId;
                this.UserId = userId;
                this.Impersonating = impersonating;
                this.EnvironmentId = environmentId;
                this.TenantId = tenantId;
            }

            public string? EnvironmentId { get; }

            public Guid TenantId { get; }

            public IOrganizationService Service { get; }

            public Guid OrganizationId { get; }

            public Guid UserId { get; }

            public bool Impersonating { get; }
        }
    }
}
