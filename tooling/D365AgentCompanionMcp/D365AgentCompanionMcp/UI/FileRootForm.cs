// <copyright file="FileRootForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

    /// <summary>
    /// The File root prompt (SPEC §6.8, When no File root is set): explains the rules, asks for the folder and the
    /// level, and warns when the folder is not the root of a git working tree. Modeless and owned by XrmToolBox's main
    /// window like the approval dialog (C9). Closing it cancels.
    /// </summary>
    internal sealed class FileRootForm : Form
    {
        private readonly Timer countdownTimer = new Timer { Interval = 1000 };
        private readonly Label countdown = UiBuilder.Text();
        private readonly DateTime deadlineUtc;
        private readonly TextBox folder = new TextBox { Width = 460 };
        private readonly RadioButton tenantChoice = new RadioButton { Text = "Every environment in this tenant (the usual case)", AutoSize = true };
        private readonly RadioButton environmentChoice = new RadioButton { Text = "This environment only", AutoSize = true };
        private readonly Label gitWarning = UiBuilder.Text();
        private bool withdrawn;
        private bool decided;

        /// <summary>Initializes a new instance of the <see cref="FileRootForm"/> class.</summary>
        /// <param name="request">What to show.</param>
        public FileRootForm(FileRootRequest request)
        {
            this.deadlineUtc = request.DeadlineUtc;
            this.Text = $"Choose a File root — {request.OrganizationName}";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(760, 520);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            var rules = UiBuilder.Text(
                "The File root is the root folder of the git repository that holds the source files the agent edits.\r\n"
                + "• A relative path from the agent is read from this folder.\r\n"
                + "• An absolute path must lie inside it. Links (symbolic links, junctions) are refused.\r\n"
                + "• The choice is kept in the plugin settings and can be changed in Active Approvals.");
            rules.MaximumSize = new Size(730, 0);

            this.folder.Text = request.CurrentPath ?? StartFolder(request) ?? string.Empty;
            this.folder.TextChanged += (s, e) => this.UpdateGitWarning();
            var browse = UiBuilder.Button("Browse…", (s, e) => this.Browse(request));

            this.tenantChoice.Enabled = request.TenantAvailable;
            this.tenantChoice.Checked = request.TenantAvailable && request.PreselectedLevel == ApprovalScope.Tenant;
            this.environmentChoice.Enabled = request.EnvironmentAvailable;
            this.environmentChoice.Checked = !this.tenantChoice.Checked;
            this.gitWarning.ForeColor = UiBuilder.WarningColor;
            this.gitWarning.MaximumSize = new Size(730, 0);

            var ok = UiBuilder.Button("Use this folder", (s, e) => this.Decide(true));
            var cancel = UiBuilder.Button("Cancel", (s, e) => this.Decide(false));
            this.CancelButton = cancel;

            var controls = new System.Collections.Generic.List<Control>
            {
                UiBuilder.Text($"Environment : {request.OrganizationName}  ({request.ConnectionName})"),
                rules,
            };

            if (request.Paths.Count > 0)
            {
                controls.Add(UiBuilder.Text("Paths in the call"));
                controls.Add(UiBuilder.ReadOnlyText(string.Join("\r\n", request.Paths.Select(CallLogFormatter.Sanitize)), 80));
            }

            controls.Add(UiBuilder.Row(UiBuilder.Text("Folder"), this.folder, browse));
            controls.Add(this.gitWarning);
            controls.Add(this.tenantChoice);
            if (!request.TenantAvailable)
            {
                controls.Add(UiBuilder.Text("Every environment in this tenant is unavailable: this connection reports no tenant ID."));
            }

            controls.Add(this.environmentChoice);
            controls.Add(UiBuilder.Row(ok, cancel, this.countdown));
            this.Controls.Add(UiBuilder.Stack(controls.ToArray()));

            this.countdownTimer.Tick += (s, e) => this.UpdateCountdown();
            this.Shown += (s, e) =>
            {
                this.UpdateGitWarning();
                this.UpdateCountdown();
                if (this.deadlineUtc != default)
                {
                    this.countdownTimer.Start();
                }

                NativeMethods.Flash(this.Owner?.Handle ?? IntPtr.Zero);
            };
            this.FormClosing += (s, e) =>
            {
                if (!this.decided && !this.withdrawn)
                {
                    this.Decide(false);
                }
            };
        }

        /// <summary>Raised once, with the choice, or <c>null</c> when the person cancelled.</summary>
        public event EventHandler<FileRootChoice?>? Decided;

        /// <summary>Closes the prompt without a choice — the call timed out or was cancelled.</summary>
        public void Withdraw()
        {
            this.withdrawn = true;
            if (!this.IsDisposed)
            {
                this.Close();
            }
        }

        /// <summary>Finds the root of the git working tree that holds a path, if there is one.</summary>
        /// <param name="path">A file or folder path.</param>
        /// <returns>The working tree root, or <c>null</c>.</returns>
        internal static string? GitRootOf(string path)
        {
            try
            {
                var current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
                while (current != null)
                {
                    var git = Path.Combine(current.FullName, ".git");
                    if (Directory.Exists(git) || File.Exists(git))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Not a usable path.
            }

            return null;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.countdownTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private static string? StartFolder(FileRootRequest request)
        {
            // When the paths are absolute, start at the root of the git working tree that holds the first one.
            var first = request.Paths.FirstOrDefault(p => Path.IsPathRooted(p));
            return first == null ? null : GitRootOf(first);
        }

        private void Browse(FileRootRequest request)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose the root folder of the git repository that holds the source files.", ShowNewFolderButton = false })
            {
                if (Directory.Exists(this.folder.Text))
                {
                    dialog.SelectedPath = this.folder.Text;
                }
                else if (StartFolder(request) is string start)
                {
                    dialog.SelectedPath = start;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.folder.Text = dialog.SelectedPath;
                }
            }
        }

        private void UpdateGitWarning()
        {
            var path = this.folder.Text.Trim();
            this.gitWarning.Text = path.Length == 0 || !Directory.Exists(path)
                ? string.Empty
                : GitRootOf(path) is string root && string.Equals(Path.GetFullPath(root).TrimEnd('\\'), Path.GetFullPath(path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : "⚠ This folder is not the root of a git working tree (no .git entry). It is accepted, but the File root should be the repository root.";
        }

        private void Decide(bool use)
        {
            if (this.decided || this.withdrawn)
            {
                return;
            }

            FileRootChoice? choice = null;
            var path = this.folder.Text.Trim();
            if (use)
            {
                if (path.Length == 0 || !Directory.Exists(path))
                {
                    MessageBox.Show(this, "Choose an existing folder.", "File root", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                choice = new FileRootChoice(Path.GetFullPath(path), this.tenantChoice.Checked && this.tenantChoice.Enabled ? ApprovalScope.Tenant : ApprovalScope.Environment);
            }

            this.decided = true;
            this.Decided?.Invoke(this, choice);
            this.Close();
        }

        private void UpdateCountdown()
        {
            if (this.deadlineUtc == default)
            {
                this.countdown.Text = string.Empty;
                return;
            }

            var remaining = Math.Max(0, (int)Math.Ceiling((this.deadlineUtc - DateTime.UtcNow).TotalSeconds));
            this.countdown.Text = $"Times out in {remaining}s";
        }
    }
}
