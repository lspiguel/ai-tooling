// <copyright file="SetupWizardControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Setup;

    /// <summary>
    /// The first-run setup wizard and the <b>Plugin updated</b> page (SPEC §8.7), shown in a tab before its main
    /// panel. It only copies prompts and exports the skills; the tab records the version when it completes.
    /// </summary>
    internal sealed class SetupWizardControl : UserControl
    {
        private const string TokenWarning = "This prompt holds the bearer token. Paste it only into your local agent.";

        private readonly string? previousVersion;
        private readonly string version;
        private readonly Func<PromptValues> values;
        private readonly Label title = new Label { AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        private readonly Panel body = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        private readonly Label feedback = UiBuilder.Text();
        private readonly Label hostStatus = UiBuilder.Text();
        private readonly Label tabStatus = UiBuilder.Text();
        private readonly List<Label> paragraphs = new List<Label>();
        private readonly List<(string Title, Control Page)> pages = new List<(string Title, Control Page)>();
        private readonly Button next;
        private int page;
        private string? skillsFolder;

        /// <summary>Initializes a new instance of the <see cref="SetupWizardControl"/> class.</summary>
        /// <param name="view">The wizard or the update page; never <see cref="SetupView.None"/>.</param>
        /// <param name="previousVersion">The version setup was last completed for, on the update page.</param>
        /// <param name="version">This plugin's version.</param>
        /// <param name="values">Reads the current endpoint, token and organization when a prompt is copied.</param>
        public SetupWizardControl(SetupView view, string? previousVersion, string version, Func<PromptValues> values)
        {
            this.previousVersion = previousVersion;
            this.version = version;
            this.values = values;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(12);
            this.title.Font = new Font(this.Font.FontFamily, 12f, FontStyle.Bold);
            this.body.SizeChanged += (s, e) => this.WrapParagraphs();

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
            if (view == SetupView.Updated)
            {
                this.next = UiBuilder.Button("Done", (s, e) => this.Completed?.Invoke(this, EventArgs.Empty));
                buttons.Controls.Add(this.next);
                buttons.Controls.Add(UiBuilder.Button("Skip", (s, e) => this.Completed?.Invoke(this, EventArgs.Empty)));
                this.pages.Add(($"Plugin updated — {previousVersion} → {version}", this.UpdatePage()));
            }
            else
            {
                this.next = UiBuilder.Button("Next", (s, e) => this.Advance());
                buttons.Controls.Add(UiBuilder.Button("Cancel", (s, e) => this.Cancelled?.Invoke(this, EventArgs.Empty)));
                buttons.Controls.Add(this.next);
                this.pages.Add(("Setup · 1 of 3 · Introduction", this.IntroductionPage()));
                this.pages.Add(("Setup · 2 of 3 · Preconditions", this.PreconditionsPage()));
                this.pages.Add(("Setup · 3 of 3 · Test", this.TestPage()));
            }

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(this.title);
            layout.Controls.Add(this.body);
            layout.Controls.Add(this.feedback);
            layout.Controls.Add(buttons);
            this.Controls.Add(layout);
            this.ShowPage(0);
        }

        /// <summary>Raised by <b>Finish</b>, <b>Done</b> or <b>Skip</b>.</summary>
        public event EventHandler? Completed;

        /// <summary>Raised by <b>Cancel</b>; the tab closes and nothing is recorded.</summary>
        public event EventHandler? Cancelled;

        /// <summary>Gets the title of the page shown.</summary>
        internal string PageTitle => this.title.Text;

        private Form? Owner => this.TopLevelControl as Form;

        /// <summary>Shows the host status and the tab's binding on the test page.</summary>
        /// <param name="host">The host status, as on the main panel.</param>
        /// <param name="tab">The tab's connection, as on the main panel.</param>
        public void ShowStatus(string host, string tab)
        {
            this.hostStatus.Text = $"Host: {host}";
            this.tabStatus.Text = $"This tab: {tab}";
        }

        /// <summary>Composes a prompt as its button would copy it.</summary>
        /// <param name="prompt">The prompt.</param>
        /// <returns>The prompt text.</returns>
        internal string PromptText(SetupPrompt prompt)
        {
            var current = this.values();
            current.PreviousVersion = this.previousVersion;
            current.SkillsFolder = this.skillsFolder;
            return SetupPrompts.Compose(prompt, current);
        }

        private static Label Warning(string text)
        {
            var label = UiBuilder.Text(text);
            label.ForeColor = UiBuilder.WarningColor;
            return label;
        }

        private void Advance()
        {
            if (this.page == this.pages.Count - 1)
            {
                this.Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            this.ShowPage(this.page + 1);
        }

        private void ShowPage(int index)
        {
            this.page = index;
            this.title.Text = this.pages[index].Title;
            this.body.Controls.Clear();
            this.body.Controls.Add(this.pages[index].Page);
            this.feedback.Text = string.Empty;
            if (this.pages.Count > 1)
            {
                this.next.Text = index == this.pages.Count - 1 ? "Finish" : "Next";
            }

            this.WrapParagraphs();
        }

        private Control IntroductionPage() => this.Page(
            this.Paragraph(
                "This plugin gives a coding agent on this machine — GitHub Copilot, Cursor or Claude Code — a small set of Dataverse operations "
                + "PAC CLI cannot perform: describing tables, forms, views and ribbons, listing what solutions hold, creating an empty solution and "
                + "adding existing components to it, without required components or subcomponents. With them the agent can discover the components "
                + "a change touches and shape small work-in-progress solutions for each story."),
            this.Paragraph(
                "Every call is approved by you in the plugin tab bound to its connection, and logged there. Solution export, editing and import "
                + "stay with PAC CLI and the agent's skills. The next page lists what a working setup needs, each with a prompt to copy to your "
                + "agent; the last page tests the setup."));

        private Control PreconditionsPage() => this.Page(
            this.Item(
                "a · A local agent",
                "GitHub Copilot, Cursor or Claude Code, reaching MCP servers over HTTP, at the minimum versions in the plugin's README."),
            this.Item(
                "b · PAC CLI installed",
                "The agent checks for PAC CLI, installs or updates it, and confirms that it runs.",
                this.CopyButton("Copy setup prompt", SetupPrompt.PacCli, "PAC CLI setup prompt")),
            this.Item(
                "c · A working pac auth profile for each environment to be used",
                "The agent lists the profiles, creates one for each environment you name, and checks each with pac org who. Connect this tab first and the prompt includes its organization URL.",
                this.CopyButton("Copy setup prompt", SetupPrompt.PacAuth, "PAC authentication prompt")),
            this.Item(
                "d · The agent-companion skills installed for the agent",
                "Export the skills to a folder outside any repository — one zip per skill — then copy the prompt, which names that folder.",
                UiBuilder.Button("Export skills…", (s, e) => this.ExportSkills()),
                this.CopyButton("Copy setup prompt", SetupPrompt.SkillsInstall, "skills setup prompt")),
            this.Item(
                "e · The plugin's MCP endpoint registered with the agent",
                "The agent registers the endpoint from the client configuration in the prompt, then reloads so the tools appear.",
                this.CopyButton("Copy setup prompt", SetupPrompt.McpRegister, "endpoint registration prompt")),
            Warning(TokenWarning));

        private Control TestPage() => this.Page(
            this.Paragraph(
                "The test prompt asks the agent to confirm that the agent-companion skills are in place, run pac org who, call list_connections "
                + "and get_connection_info and compare the organization URL with PAC's, then call list_solutions as a test. Approve that call "
                + "in this tab when its dialog appears; after Finish it shows in the tab's call log."),
            this.Paragraph("Connect this tab to a development environment first, if it is not connected yet."),
            this.hostStatus,
            this.tabStatus,
            UiBuilder.Row(this.CopyButton("Copy test prompt", SetupPrompt.Test, "test prompt")));

        private Control UpdatePage() => this.Page(
            this.Paragraph(
                $"The plugin was updated from {this.previousVersion} to {this.version}. The agent-companion skills and the MCP client configuration "
                + "may have changed with it. Reinstalling them through your agent is optional: Skip goes to the main panel, and this page does not "
                + "come back until the next update."),
            this.Item(
                "Skills",
                "Export the new skills to a folder outside any repository, then copy the prompt: the agent replaces its agent-companion skills, and only those.",
                UiBuilder.Button("Export skills…", (s, e) => this.ExportSkills()),
                this.CopyButton("Copy update prompt", SetupPrompt.SkillsUpdate, "skills update prompt")),
            this.Item(
                "MCP configuration",
                "The agent checks its registration of the endpoint against the current client configuration and updates it if it differs.",
                this.CopyButton("Copy update prompt", SetupPrompt.McpUpdate, "endpoint update prompt")),
            Warning(TokenWarning),
            this.Item(
                "Check",
                "The setup test: skills, PAC CLI, the connection, and one list_solutions call to approve in this tab.",
                this.CopyButton("Copy test prompt", SetupPrompt.Test, "test prompt")));

        private Control Page(params Control[] rows)
        {
            var stack = UiBuilder.Stack(rows);
            stack.Dock = DockStyle.Top;
            return stack;
        }

        private Control Item(string heading, string text, params Control[] buttons)
        {
            var label = UiBuilder.Text(heading);
            label.Font = new Font(this.Font, FontStyle.Bold);
            label.Padding = new Padding(0, 10, 0, 0);
            var rows = new List<Control> { label, this.Paragraph(text) };
            if (buttons.Length > 0)
            {
                rows.Add(UiBuilder.Row(buttons));
            }

            var stack = UiBuilder.Stack(rows.ToArray());
            stack.Dock = DockStyle.Top;
            return stack;
        }

        private Label Paragraph(string text)
        {
            var label = UiBuilder.Text(text);
            this.paragraphs.Add(label);
            return label;
        }

        private Button CopyButton(string caption, SetupPrompt prompt, string description) =>
            UiBuilder.Button(caption, (s, e) => this.Copy(prompt, description));

        private void WrapParagraphs()
        {
            var width = Math.Max(200, this.body.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
            foreach (var label in this.paragraphs)
            {
                label.MaximumSize = new Size(width, 0);
            }
        }

        private void Copy(SetupPrompt prompt, string description)
        {
            Clipboard.SetText(this.PromptText(prompt));
            this.feedback.ForeColor = SetupPrompts.HoldsToken(prompt) ? UiBuilder.WarningColor : SystemColors.ControlText;
            this.feedback.Text = SetupPrompts.HoldsToken(prompt)
                ? $"Copied the {description} to the clipboard. {TokenWarning}"
                : $"Copied the {description} to the clipboard.";
        }

        private void ExportSkills()
        {
            string folder;
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Choose the folder for the agent-companion skill zips — outside any repository.",
                ShowNewFolderButton = true,
                SelectedPath = this.skillsFolder ?? string.Empty,
            })
            {
                if (dialog.ShowDialog(this.Owner) != DialogResult.OK)
                {
                    return;
                }

                folder = dialog.SelectedPath;
            }

            if (LogFileWriter.IsInsideGitWorkingTree(folder)
                && MessageBox.Show(
                    this.Owner,
                    "This folder is inside a Git working tree. The skill zips are setup files for your agent, not project source, and should stay out of repositories.\r\n\r\nExport them here anyway?",
                    "Export skills",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            var existing = SkillExporter.ZipPaths(folder).Where(File.Exists).Select(Path.GetFileName).ToList();
            if (existing.Count > 0
                && MessageBox.Show(
                    this.Owner,
                    $"These files are already in the folder and will be replaced:\r\n\r\n{string.Join("\r\n", existing)}\r\n\r\nReplace them?",
                    "Export skills",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                var written = SkillExporter.Export(folder);
                this.skillsFolder = folder;
                this.feedback.ForeColor = SystemColors.ControlText;
                this.feedback.Text = $"Wrote {written.Count} skill zips to {folder}. The skills prompt now names this folder.";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                this.feedback.ForeColor = UiBuilder.WarningColor;
                this.feedback.Text = $"⚠ The skills could not be exported: {ex.Message}";
            }
        }
    }
}
