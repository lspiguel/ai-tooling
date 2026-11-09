// <copyright file="CallLogFolderForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

    /// <summary>
    /// The folder prompt shown when <b>Write call log to disk</b> is ticked (SPEC 6a.2). It explains the rules, says
    /// plainly that the files can hold client data and are not encrypted, and only accepts a folder outside every Git
    /// working tree.
    /// </summary>
    internal sealed class CallLogFolderForm : Form
    {
        /// <summary>The rules the prompt explains.</summary>
        internal const string Rules =
            "While this is on, every call the agent makes — to every connection, and every host call — is written when it ends to one JSON file per connection and day in this folder, named AgentCompanion-[ConnectionName]-yyyyMMdd.json.\r\n"
            + "• The files hold full arguments and results. They can contain client data, including record values, and they are not encrypted.\r\n"
            + "• A folder that is, or is inside, a Git working tree is refused. Choose a folder outside every repository.\r\n"
            + "• The plugin never deletes or rotates the files. The agent is never told that the log is on.\r\n"
            + "• The setting applies to every tab and is kept the next time XrmToolBox opens, until you untick it.";

        private readonly TextBox folder = new TextBox { Width = 460 };
        private readonly Label refusal = UiBuilder.Text();

        /// <summary>Initializes a new instance of the <see cref="CallLogFolderForm"/> class.</summary>
        /// <param name="start">The folder to start at — the previous folder, if there was one.</param>
        internal CallLogFolderForm(string? start)
        {
            this.Text = "Write call log to disk — choose a folder";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(760, 380);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            var rules = UiBuilder.Text(Rules);
            rules.MaximumSize = new Size(730, 0);
            this.refusal.ForeColor = UiBuilder.WarningColor;
            this.refusal.MaximumSize = new Size(730, 0);
            this.folder.Text = start ?? string.Empty;

            var ok = UiBuilder.Button("Turn on", (s, e) => this.Decide());
            var cancel = UiBuilder.Button("Cancel", (s, e) => this.Close());
            cancel.DialogResult = DialogResult.Cancel;
            this.CancelButton = cancel;

            this.Controls.Add(UiBuilder.Stack(
                rules,
                UiBuilder.Row(UiBuilder.Text("Folder"), this.folder, UiBuilder.Button("Browse…", (s, e) => this.Browse())),
                this.refusal,
                UiBuilder.Row(ok, cancel)));
        }

        /// <summary>Gets the folder chosen, or <c>null</c> when the person cancelled.</summary>
        public string? Chosen { get; private set; }

        /// <summary>Asks for a folder.</summary>
        /// <param name="owner">The window to centre on.</param>
        /// <param name="start">The folder to start at.</param>
        /// <returns>The folder, or <c>null</c> when the person cancelled.</returns>
        public static string? Ask(IWin32Window? owner, string? start)
        {
            using (var form = new CallLogFolderForm(start))
            {
                return form.ShowDialog(owner) == DialogResult.OK ? form.Chosen : null;
            }
        }

        private void Browse()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose a folder outside every Git working tree for the call log files.", ShowNewFolderButton = true })
            {
                if (Directory.Exists(this.folder.Text))
                {
                    dialog.SelectedPath = this.folder.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.folder.Text = dialog.SelectedPath;
                }
            }
        }

        private void Decide()
        {
            var path = this.folder.Text.Trim();
            var reason = DiskCallLog.Refusal(path);
            if (reason != null)
            {
                this.refusal.Text = "⚠ Refused: " + reason;
                return;
            }

            this.Chosen = Path.GetFullPath(path);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
