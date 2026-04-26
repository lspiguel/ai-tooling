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
        private string _lastBaseDir = string.Empty;

        /// <summary>Raised when the user selects a project. The event argument is the loaded job, or <see langword="null"/> when no project is selected.</summary>
        public event EventHandler<ExportJob?>? ProjectSelected;

        /// <summary>Gets the currently loaded export job, or <see langword="null"/> when none is selected.</summary>
        public ExportJob? SelectedJob { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectPickerControl"/> class.Initialises a new instance of <see cref="ProjectPickerControl"/>.
        /// </summary>
        public ProjectPickerControl()
        {
            InitializeComponent();
        }

        /// <summary>Rescans the <c>config/</c> directory under <paramref name="baseDir"/> and repopulates the combo box.</summary>
        /// <param name="baseDir">The project root directory to scan.</param>
        public void LoadProjects(string baseDir)
        {
            _lastBaseDir = baseDir;
            cmbProjects.SelectedIndexChanged -= cmbProjects_SelectedIndexChanged;

            try
            {
                cmbProjects.Items.Clear();
                SelectedJob = null;

                var configs = PathResolver.DiscoverProjectConfigs(baseDir);
                foreach (var path in configs)
                {
                    var name = PathResolver.ProjectNameFromPath(path);
                    cmbProjects.Items.Add(new ProjectItem(name, path));
                }

                if (cmbProjects.Items.Count == 0)
                {
                    cmbProjects.Items.Add("(no projects found)");
                    cmbProjects.SelectedIndex = 0;
                    cmbProjects.Enabled = false;
                }
                else
                {
                    cmbProjects.Enabled = true;
                    cmbProjects.SelectedIndex = -1;
                }
            }
            finally
            {
                cmbProjects.SelectedIndexChanged += cmbProjects_SelectedIndexChanged;
            }

            ProjectSelected?.Invoke(this, null);
        }

        private void cmbProjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbProjects.SelectedItem is not ProjectItem item)
            {
                SelectedJob = null;
                ProjectSelected?.Invoke(this, null);
                return;
            }

            try
            {
                SelectedJob = ExportJob.Load(item.ConfigFilePath);
                ProjectSelected?.Invoke(this, SelectedJob);
            }
            catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is System.IO.IOException)
            {
                SelectedJob = null;
                cmbProjects.SelectedIndex = -1;
                ProjectSelected?.Invoke(this, null);
                MessageBox.Show(
                    $"Failed to load project config:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadProjects(_lastBaseDir);
        }

        private sealed class ProjectItem
        {
            public string ConfigFilePath { get; }

            public ProjectItem(string displayName, string configFilePath)
            {
                DisplayName = displayName;
                ConfigFilePath = configFilePath;
            }

            public string DisplayName { get; }

            public override string ToString() => DisplayName;
        }
    }
}
