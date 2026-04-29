// <copyright file="ContextExporterPluginControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using D365ContextExporter.Helpers;
    using D365ContextExporter.Models;
    using D365ContextExporter.Orchestration;

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
            this.projectPicker.LoadProjects(this.dirPicker.SelectedDirectory);
        }

        private void ContextExporterPluginControl_Load(object sender, EventArgs e)
        {
            this.dirPicker.LoadSettings();
            this.initialized = true;
            if (!string.IsNullOrEmpty(this.dirPicker.SelectedDirectory))
            {
                this.projectPicker.LoadProjects(this.dirPicker.SelectedDirectory);
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
            this.projectPicker.LoadProjects(newDir);
            this.progressControl.AppendLog($"Base directory set: {newDir}");
        }

        private void projectPicker_ProjectSelected(object sender, ExportJob? job)
        {
            this.btnRun.Enabled = job != null && this.Service != null;
            if (job != null)
            {
                this.progressControl.AppendLog($"Project selected: {job}");
            }
        }

        private void progressControl_CancelRequested(object sender, EventArgs e)
        {
            this.cts?.Cancel();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (this.Service == null || this.projectPicker.SelectedJob == null)
            {
                return;
            }

            var job = this.projectPicker.SelectedJob;
            var baseDir = this.dirPicker.SelectedDirectory;

            // Bootstrap check runs on UI thread so MessageBox is modal to the main window.
            try
            {
                PythonBootstrapHelper.Check(
                    job.Python,
                    msg => this.progressControl.AppendLog(msg));
            }
            catch (Exception ex)
            {
                this.progressControl.AppendLog($"[Bootstrap] ERROR: {ex.Message}");
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
                            this.progressControl.AppendLog(
                                $"ERROR: {t.Exception?.GetBaseException().Message}");
                        }
                        else if (!t.IsCanceled && t.Result != null)
                        {
                            var runDir = t.Result;
                            var runOutputPath = Path.Combine(runDir, "output.md");
                            var projectOutputPath = Path.Combine(
                                baseDir, "output", $"{job.Project}.context.md");

                            this.outputPreview.ShowResult(runOutputPath, projectOutputPath);
                            this.outputPreview.Visible = true;
                        }
                    }));
                });
        }
    }
}
