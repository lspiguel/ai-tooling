// <copyright file="ContextExporterPluginControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Lspiguel.Xrm.D365ContextExporter.Helpers;
    using Lspiguel.Xrm.D365ContextExporter.Models;
    using Lspiguel.Xrm.D365ContextExporter.Orchestration;

    using McTools.Xrm.Connection;

    using Microsoft.Xrm.Sdk;

    using XrmToolBox.Extensibility;

    /// <summary>Root plugin control hosted by XrmToolBox.</summary>
    public partial class ContextExporterPluginControl : PluginControlBase
    {
        private CancellationTokenSource? cts;
        private bool initialized;

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
            this.btnRun.Enabled = job != null;
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

        private void StartExport()
        {
            if (this.Service == null || this.specPicker.SelectedJob == null)
            {
                return;
            }

            var job = this.specPicker.SelectedJob;
            var baseDir = this.dirPicker.SelectedDirectory;

            // Bootstrap check runs on UI thread so MessageBox is modal to the main window.
            try
            {
                PythonBootstrapHelper.Check(
                    job.Python,
                    baseDir,
                    msg => this.progressControl.AppendLog(msg));
            }
            catch (Exception ex)
            {
                this.progressControl.AppendLog($"[Bootstrap] ERROR: {ex.Message}");
                return;
            }

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

            this.btnRun.Enabled = false;
            this.outputPreview.Visible = false;
            this.cts = new CancellationTokenSource();

            this.progressControl.ClearLog();
            this.progressControl.SetRunning(true);

            Task.Run<string?>(
                () =>
                {
                    var runner = new ExportJobRunner(
                        this.Service,
                        this.ConnectionDetail,
                        msg => this.BeginInvoke((Action)(() => this.progressControl.AppendLog(msg))),
                        (current, total, queryId) =>
                            this.BeginInvoke((Action)(() =>
                                this.progressControl.SetProgress(current, total, queryId))));
                    return runner.Run(job, baseDir, this.cts.Token);
                },
                this.cts.Token).ContinueWith(t =>
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        this.progressControl.SetRunning(false);
                        this.btnRun.Enabled = true;

                        if (t.IsFaulted)
                        {
                            var msg = t.Exception is AggregateException agg
                                ? agg.Message
                                : t.Exception?.GetBaseException().Message;
                            this.progressControl.AppendLog($"ERROR: {msg}");
                        }
                        else if (!t.IsCanceled && t.Result != null)
                        {
                            var specOutputPath = Path.Combine(
                                baseDir, "output", $"{job.Spec}.context.md");

                            this.outputPreview.ShowResult(specOutputPath);
                            this.outputPreview.Visible = true;
                        }
                    }));
                });
        }

        private void CheckAndOfferFirstRun(string dir)
        {
            var log = (Action<string>)(msg => this.progressControl.AppendLog(msg));

            if (!FirstRunHelper.IsConfigured(dir) && FirstRunHelper.OfferSetup(dir, this))
            {
                FirstRunHelper.DeployReferenceConfig(dir, this, log, overwrite: false);
                this.specPicker.LoadSpecs(dir);
            }

            FirstRunHelper.CheckVersion(dir, this, log);
        }
    }
}
