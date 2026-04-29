// <copyright file="OutputPreviewControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.UI
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Windows.Forms;

    /// <summary>Displays the output files produced by a successful export run with actions to open, reveal, or copy their paths.</summary>
    public partial class OutputPreviewControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputPreviewControl"/> class.
        /// </summary>
        public OutputPreviewControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Populates the control with the paths to the two output files.</summary>
        /// <param name="runOutputPath">Path to output.md in the run directory.</param>
        /// <param name="projectOutputPath">Path to the project-level *.context.md file.</param>
        public void ShowResult(string runOutputPath, string projectOutputPath)
        {
            this.txtRunOutput.Text = runOutputPath;
            this.txtProjectOutput.Text = projectOutputPath;

            var fileName = Path.GetFileName(projectOutputPath);
            this.lblBanner.Text =
                $"Upload {fileName} to claude.ai, ChatGPT, or your AI assistant as a grounding file.";
        }

        private void btnOpenRun_Click(object sender, EventArgs e) =>
            ShellOpen(this.txtRunOutput.Text);

        private void btnRevealRun_Click(object sender, EventArgs e) =>
            RevealInExplorer(this.txtRunOutput.Text);

        private void btnCopyRun_Click(object sender, EventArgs e) =>
            CopyToClipboard(this.txtRunOutput.Text);

        private void btnOpenProject_Click(object sender, EventArgs e) =>
            ShellOpen(this.txtProjectOutput.Text);

        private void btnRevealProject_Click(object sender, EventArgs e) =>
            RevealInExplorer(this.txtProjectOutput.Text);

        private void btnCopyProject_Click(object sender, EventArgs e) =>
            CopyToClipboard(this.txtProjectOutput.Text);

        private static void ShellOpen(string path)
        {
            if (!File.Exists(path)) return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
