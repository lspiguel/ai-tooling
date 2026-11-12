// <copyright file="OutputPreviewControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Windows.Forms;

    /// <summary>Displays the output file of a successful export run, with actions to open, reveal, or copy its path; after a batch run, the output folder with an action to open it.</summary>
    public partial class OutputPreviewControl : UserControl
    {
        private bool showsFolder;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputPreviewControl"/> class.
        /// </summary>
        public OutputPreviewControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Populates the control with the path to the output context file.</summary>
        /// <param name="projectOutputPath">Path to the project-level *.context.md file.</param>
        public void ShowResult(string projectOutputPath)
        {
            this.showsFolder = false;
            this.lblProjectOutput.Text = "Context file:";
            this.btnOpenProject.Text = "Open";
            this.btnOpenProject.Width = 51;
            this.btnRevealProject.Visible = true;
            this.btnCopyProject.Visible = true;
            this.txtProjectOutput.Text = projectOutputPath;

            var fileName = Path.GetFileName(projectOutputPath);
            this.lblBanner.Text =
                $"Upload {fileName} to claude.ai, ChatGPT, or your AI assistant as a grounding file.";
        }

        /// <summary>Points to the output folder after a batch run, with a single action to open it.</summary>
        /// <param name="outputDirectory">The base directory's <c>output</c> folder.</param>
        /// <param name="succeeded">Number of specs that ran to completion.</param>
        /// <param name="failed">Number of specs that failed.</param>
        public void ShowFolder(string outputDirectory, int succeeded, int failed)
        {
            this.showsFolder = true;
            this.lblProjectOutput.Text = "Output folder:";
            this.btnOpenProject.Text = "Open Folder";
            this.btnOpenProject.Width = 80;
            this.btnRevealProject.Visible = false;
            this.btnCopyProject.Visible = false;
            this.txtProjectOutput.Text = outputDirectory;

            var outcome = failed == 0
                ? $"{succeeded} specs exported."
                : $"{succeeded} of {succeeded + failed} specs exported; {failed} failed (see the log).";
            this.lblBanner.Text =
                $"{outcome} Upload the context files in the output folder to claude.ai, ChatGPT, or your AI assistant as grounding files.";
        }

        private void btnOpenProject_Click(object sender, EventArgs e)
        {
            if (this.showsFolder)
            {
                OpenFolder(this.txtProjectOutput.Text);
            }
            else
            {
                ShellOpen(this.txtProjectOutput.Text);
            }
        }

        private void btnRevealProject_Click(object sender, EventArgs e) =>
            RevealInExplorer(this.txtProjectOutput.Text);

        private void btnCopyProject_Click(object sender, EventArgs e) =>
            CopyToClipboard(this.txtProjectOutput.Text);

        private static void ShellOpen(string path)
        {
            if (!File.Exists(path)) return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            Process.Start("explorer.exe", $"\"{path}\"");
        }

        private static void RevealInExplorer(string path)
        {
            if (!File.Exists(path)) return;
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }

        private static void CopyToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }
    }
}
