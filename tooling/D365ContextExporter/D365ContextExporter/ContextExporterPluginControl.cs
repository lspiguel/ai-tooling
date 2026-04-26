// <copyright file="ContextExporterPluginControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
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
        /// Initializes a new instance of the <see cref="ContextExporterPluginControl"/> class.Initialises a new instance of <see cref="ContextExporterPluginControl"/>.
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

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (this.Service == null || this.projectPicker.SelectedJob == null)
            {
                return;
            }

            this.btnRun.Enabled = false;
            this.cts = new CancellationTokenSource();

            var job = this.projectPicker.SelectedJob;
            var baseDir = this.dirPicker.SelectedDirectory;

            Task.Run(
                () =>
                {
                    var runner = new ExportJobRunner(
                        this.Service,
                        msg => this.BeginInvoke((Action)(() => this.progressControl.AppendLog(msg))));
                    runner.Run(job, baseDir, this.cts.Token);
                },
                this.cts.Token).ContinueWith(t =>
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        this.btnRun.Enabled = true;
                        if (t.IsFaulted)
                        {
                            this.progressControl.AppendLog($"ERROR: {t.Exception?.GetBaseException().Message}");
                        }
                    }));
                });
        }
    }
}
