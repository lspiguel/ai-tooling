// <copyright file="ProjectPickerControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.UI
{
    using System;
    using System.Windows.Forms;

    using D365ContextExporter.Helpers;
    using D365ContextExporter.Models;

    /// <summary>
    /// A label and a combo box that lists available projects discovered under a base directory.
    /// Raises <see cref="ProjectSelected"/> when the user picks a project.
    /// </summary>
    public partial class ProjectPickerControl : UserControl
    {
        private string lastBaseDir = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectPickerControl"/> class.Initialises a new instance of <see cref="ProjectPickerControl"/>.
        /// </summary>
        public ProjectPickerControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Raised when the user selects a project. The event argument is the loaded job, or <see langword="null"/> when no project is selected.</summary>
        public event EventHandler<ExportJob?>? ProjectSelected;

        /// <summary>Gets the currently loaded export job, or <see langword="null"/> when none is selected.</summary>
        public ExportJob? SelectedJob { get; private set; }

        /// <summary>Rescans the <c>config/</c> directory under <paramref name="baseDir"/> and repopulates the combo box.</summary>
        /// <param name="baseDir">The project root directory to scan.</param>
        public void LoadProjects(string baseDir)
        {
            this.lastBaseDir = baseDir;
            this.cmbProjects.SelectedIndexChanged -= this.cmbProjects_SelectedIndexChanged;

            try
            {
                this.cmbProjects.Items.Clear();
                this.SelectedJob = null;

                var configs = PathResolver.DiscoverProjectConfigs(baseDir);
                foreach (var path in configs)
                {
                    var name = PathResolver.ProjectNameFromPath(path);
                    this.cmbProjects.Items.Add(new ProjectItem(name, path));
                }

                if (this.cmbProjects.Items.Count == 0)
                {
                    this.cmbProjects.Items.Add("(no projects found)");
                    this.cmbProjects.SelectedIndex = 0;
                    this.cmbProjects.Enabled = false;
                }
                else
                {
                    this.cmbProjects.Enabled = true;
                    this.cmbProjects.SelectedIndex = -1;
                }
            }
            finally
            {
                this.cmbProjects.SelectedIndexChanged += this.cmbProjects_SelectedIndexChanged;
            }

            this.ProjectSelected?.Invoke(this, null);
        }

        private void cmbProjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.cmbProjects.SelectedItem is not ProjectItem item)
            {
                this.SelectedJob = null;
                this.ProjectSelected?.Invoke(this, null);
                return;
            }

            try
            {
                this.SelectedJob = ExportJob.Load(item.ConfigFilePath);
                this.ProjectSelected?.Invoke(this, this.SelectedJob);
            }
            catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is System.IO.IOException)
            {
                this.SelectedJob = null;
                this.cmbProjects.SelectedIndex = -1;
                this.ProjectSelected?.Invoke(this, null);
                MessageBox.Show(
                    $"Failed to load project config:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            this.LoadProjects(this.lastBaseDir);
        }

        private sealed class ProjectItem
        {
            public ProjectItem(string displayName, string configFilePath)
            {
                this.DisplayName = displayName;
                this.ConfigFilePath = configFilePath;
            }

            public string ConfigFilePath { get; }

            public string DisplayName { get; }

            public override string ToString() => this.DisplayName;
        }
    }
}
