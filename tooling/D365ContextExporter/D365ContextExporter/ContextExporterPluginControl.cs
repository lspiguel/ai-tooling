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
        private CancellationTokenSource? _cts;
        private bool _initialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextExporterPluginControl"/> class.Initialises a new instance of <see cref="ContextExporterPluginControl"/>.
        /// </summary>
        public ContextExporterPluginControl()
        {
            InitializeComponent();
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
            progressControl.AppendLog($"Connected: {detail.WebApplicationUrl}");
            projectPicker.LoadProjects(dirPicker.SelectedDirectory);
        }

        private void ContextExporterPluginControl_Load(object sender, EventArgs e)
        {
            dirPicker.LoadSettings();
            _initialized = true;
            if (!string.IsNullOrEmpty(dirPicker.SelectedDirectory))
            {
                projectPicker.LoadProjects(dirPicker.SelectedDirectory);
            }
        }

        private void ContextExporterPluginControl_VisibleChanged(object sender, EventArgs e)
        {
            if (_initialized && !Visible)
            {
                dirPicker.SaveSettings();
            }
        }

        private void dirPicker_DirectoryChanged(object sender, string newDir)
        {
            projectPicker.LoadProjects(newDir);
            progressControl.AppendLog($"Base directory set: {newDir}");
        }

        private void projectPicker_ProjectSelected(object sender, ExportJob? job)
        {
            btnRun.Enabled = job != null && Service != null;
            if (job != null)
            {
                progressControl.AppendLog($"Project selected: {job}");
            }
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (Service == null || projectPicker.SelectedJob == null)
            {
                return;
            }

            btnRun.Enabled = false;
            _cts = new CancellationTokenSource();

            var job = projectPicker.SelectedJob;
            var baseDir = dirPicker.SelectedDirectory;

            Task.Run(
                () =>
                {
                    var runner = new ExportJobRunner(
                        Service,
                        msg => BeginInvoke((Action)(() => progressControl.AppendLog(msg))));
                    runner.Run(job, baseDir, _cts.Token);
                },
                _cts.Token).ContinueWith(t =>
                {
                    BeginInvoke((Action)(() =>
                    {
                        btnRun.Enabled = true;
                        if (t.IsFaulted)
                        {
                            progressControl.AppendLog($"ERROR: {t.Exception?.GetBaseException().Message}");
                        }
                    }));
                });
        }
    }
}
