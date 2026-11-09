// <copyright file="ContextExporterPluginControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;

    using Lspiguel.Xrm.D365ContextExporter.Helpers;
    using Lspiguel.Xrm.D365ContextExporter.Models;
    using Lspiguel.Xrm.D365ContextExporter.Orchestration;
    using Lspiguel.Xrm.D365ContextExporter.Properties;
    using Lspiguel.Xrm.D365ContextExporter.UI;

    using McTools.Xrm.Connection;

    using Microsoft.Xrm.Sdk;

    using XrmToolBox.Extensibility;

    /// <summary>Root plugin control hosted by XrmToolBox.</summary>
    public partial class ContextExporterPluginControl : PluginControlBase
    {
        private CancellationTokenSource? cts;
        private bool initialized;
        private bool running;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextExporterPluginControl"/> class.
        /// </summary>
        public ContextExporterPluginControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Called by XrmToolBox when the user connects to an org or switches connection.</summary>
        /// <inheritdoc/>
        public override void UpdateConnection(
            IOrganizationService newService,
            ConnectionDetail detail,
            string actionName,
            object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            this.progressControl.AppendLog($"Connected: {detail.WebApplicationUrl}");
            this.specPicker.LoadSpecs(this.dirPicker.SelectedDirectory);
        }

        private void ContextExporterPluginControl_Load(object sender, EventArgs e)
        {
            this.ShowWelcomeIfFirstLoad();
            this.dirPicker.LoadSettings();
            this.initialized = true;
            if (!string.IsNullOrEmpty(this.dirPicker.SelectedDirectory))
            {
                this.specPicker.LoadSpecs(this.dirPicker.SelectedDirectory);
                this.CheckAndOfferFirstRun(this.dirPicker.SelectedDirectory);
            }
        }

        private void ContextExporterPluginControl_VisibleChanged(object sender, EventArgs e)
        {
            if (this.initialized && !this.Visible)
            {
                this.dirPicker.SaveSettings();
            }
        }

        private void dirPicker_DirectoryChanged(object sender, string newDir)
        {
            this.specPicker.LoadSpecs(newDir);
            this.progressControl.AppendLog($"Base directory set: {newDir}");
            this.CheckAndOfferFirstRun(newDir);
        }

        private void specPicker_SpecSelected(object sender, ExportJob? job)
        {
            this.UpdateRunButtons();
            if (job != null)
            {
                this.progressControl.AppendLog($"Spec selected: {job}");
            }
        }

        private void progressControl_CancelRequested(object sender, EventArgs e)
        {
            this.cts?.Cancel();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (this.specPicker.SelectedJob == null)
            {
                return;
            }

            if (this.Service == null)
            {
                this.ExecuteMethod(this.StartExport);
                return;
            }

            this.StartExport();
        }

        private void btnRunAll_Click(object sender, EventArgs e)
        {
            var specs = this.specPicker.SpecConfigPaths;
            if (specs.Count == 0 || !this.ConfirmRunAll(specs))
            {
                return;
            }

            if (this.Service == null)
            {
                this.ExecuteMethod(this.StartRunAll);
                return;
            }

            this.StartRunAll();
        }

        private bool ConfirmRunAll(IReadOnlyList<string> specs)
        {
            var names = new StringBuilder();
            foreach (var path in specs)
            {
                names.AppendLine($"  • {PathResolver.SpecNameFromPath(path)}");
            }

            var outputDir = Path.Combine(this.dirPicker.SelectedDirectory, "output");
            var message =
                $"Run all {specs.Count} specs, one after another?\n\n{names}\n" +
                "This can take a long time: each spec queries the environment and renders its template, " +
                "and a large environment can take several minutes per spec. The plugin stays busy until the last spec ends.\n\n" +
                "A spec that fails is logged and the others still run. Cancel stops the batch.\n\n" +
                $"Each spec gets its own run folder, and its context file in:\n{outputDir}";

            return MessageBox.Show(
                this,
                message,
                "Run all specs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void StartRunAll()
        {
            var specs = this.specPicker.SpecConfigPaths;
            if (this.Service == null || specs.Count == 0)
            {
                return;
            }

            var baseDir = this.dirPicker.SelectedDirectory;
            this.BeginRun();

            this.WorkAsync(new WorkAsyncInfo
            {
                Message = string.Empty,
                Work = (worker, args) =>
                {
                    var runner = this.CreateJobRunner();
                    var batch = new ExportBatchRunner(
                        runner.Run,
                        msg => this.BeginInvoke((Action)(() => this.progressControl.AppendLog(msg))),
                        (current, total, spec) =>
                            this.BeginInvoke((Action)(() =>
                                this.progressControl.SetBatchProgress(current, total, spec))));
                    args.Result = batch.Run(specs, baseDir, this.cts!.Token);
                },
                PostWorkCallBack = e =>
                {
                    this.EndRun();

                    if (e.Error is OperationCanceledException)
                    {
                        // user cancelled — specs already finished keep their output
                    }
                    else if (e.Error != null)
                    {
                        this.progressControl.AppendLog($"ERROR: {e.Error.GetBaseException().Message}");
                    }
                    else if (e.Result is ExportBatchResult result && result.Succeeded.Count > 0)
                    {
                        this.outputPreview.ShowFolder(
                            Path.Combine(baseDir, "output"),
                            result.Succeeded.Count,
                            result.Failed.Count);
                        this.outputPreview.Visible = true;
                    }
                },
            });
        }

        private void StartExport()
        {
            if (this.Service == null || this.specPicker.SelectedJob == null)
            {
                return;
            }

            var job = this.specPicker.SelectedJob;
            var baseDir = this.dirPicker.SelectedDirectory;

            // Validate config before starting the background task.
            try
            {
                ConfigValidator.Validate(job, baseDir);
            }
            catch (ConfigValidationException ex)
            {
                foreach (var violation in ex.Violations)
                {
                    this.progressControl.AppendLog($"[Validation] {violation}");
                }

                return;
            }

            this.BeginRun();

            this.WorkAsync(new WorkAsyncInfo
            {
                Message = string.Empty,
                Work = (worker, args) =>
                {
                    args.Result = this.CreateJobRunner().Run(job, baseDir, this.cts!.Token);
                },
                PostWorkCallBack = e =>
                {
                    this.EndRun();

                    if (e.Error is OperationCanceledException)
                    {
                        // user cancelled — nothing more to do
                    }
                    else if (e.Error != null)
                    {
                        this.progressControl.AppendLog($"ERROR: {e.Error.GetBaseException().Message}");
                    }
                    else if (e.Result is string)
                    {
                        var specOutputPath = Path.Combine(baseDir, "output", $"{job.Spec}.context.md");
                        this.outputPreview.ShowResult(specOutputPath);
                        this.outputPreview.Visible = true;
                    }
                },
            });
        }

        private ExportJobRunner CreateJobRunner() =>
            new ExportJobRunner(
                this.Service,
                this.ConnectionDetail,
                msg => this.BeginInvoke((Action)(() => this.progressControl.AppendLog(msg))),
                (current, total, queryId) =>
                    this.BeginInvoke((Action)(() =>
                        this.progressControl.SetProgress(current, total, queryId))));

        private void BeginRun()
        {
            this.running = true;
            this.UpdateRunButtons();
            this.outputPreview.Visible = false;
            this.cts = new CancellationTokenSource();

            this.progressControl.ClearLog();
            this.progressControl.SetRunning(true);
        }

        private void EndRun()
        {
            this.running = false;
            this.progressControl.SetRunning(false);
            this.UpdateRunButtons();
        }

        private void UpdateRunButtons()
        {
            this.btnRun.Enabled = !this.running && this.specPicker.SelectedJob != null;
            this.btnRunAll.Enabled = !this.running && this.specPicker.SpecConfigPaths.Count > 0;
        }

        private void ShowWelcomeIfFirstLoad()
        {
            if (Settings.Default.WelcomeShown)
            {
                return;
            }

            // Flip before showing — prevents re-trigger if ShowDialog throws.
            Settings.Default.WelcomeShown = true;
            Settings.Default.Save();

            using var form = new WelcomeForm();
            form.ShowDialog(this);
        }

        private void CheckAndOfferFirstRun(string dir)
        {
            var log = (Action<string>)(msg => this.progressControl.AppendLog(msg));

            if (!FirstRunHelper.IsConfigured(dir) && FirstRunHelper.OfferSetup(dir, this))
            {
                FirstRunHelper.DeployReferenceConfig(dir, this, log);
                this.specPicker.LoadSpecs(dir);
            }

            FirstRunHelper.CheckVersion(dir, this, log);
        }
    }
}
