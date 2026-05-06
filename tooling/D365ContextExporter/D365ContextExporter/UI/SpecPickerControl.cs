// <copyright file="SpecPickerControl.cs" company="Luciano Spiguel">
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
    /// A label and a combo box that lists available specs discovered under a base directory.
    /// Raises <see cref="SpecSelected"/> when the user picks a spec.
    /// </summary>
    public partial class SpecPickerControl : UserControl
    {
        private string lastBaseDir = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecPickerControl"/> class.
        /// </summary>
        public SpecPickerControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Raised when the user selects a spec. The event argument is the loaded job, or <see langword="null"/> when no spec is selected.</summary>
        public event EventHandler<ExportJob?>? SpecSelected;

        /// <summary>Gets the currently loaded export job, or <see langword="null"/> when none is selected.</summary>
        public ExportJob? SelectedJob { get; private set; }

        /// <summary>Rescans the <c>config/</c> directory under <paramref name="baseDir"/> and repopulates the combo box.</summary>
        /// <param name="baseDir">The base directory to scan.</param>
        public void LoadSpecs(string baseDir)
        {
            this.lastBaseDir = baseDir;
            this.cmbSpecs.SelectedIndexChanged -= this.cmbSpecs_SelectedIndexChanged;

            try
            {
                this.cmbSpecs.Items.Clear();
                this.SelectedJob = null;

                var configs = PathResolver.DiscoverSpecConfigs(baseDir);
                foreach (var path in configs)
                {
                    var name = PathResolver.SpecNameFromPath(path);
                    this.cmbSpecs.Items.Add(new SpecItem(name, path));
                }

                if (this.cmbSpecs.Items.Count == 0)
                {
                    this.cmbSpecs.Items.Add("(no specs found)");
                    this.cmbSpecs.SelectedIndex = 0;
                    this.cmbSpecs.Enabled = false;
                }
                else
                {
                    this.cmbSpecs.Enabled = true;
                    this.cmbSpecs.SelectedIndex = -1;
                }
            }
            finally
            {
                this.cmbSpecs.SelectedIndexChanged += this.cmbSpecs_SelectedIndexChanged;
            }

            this.SpecSelected?.Invoke(this, null);
        }

        private void cmbSpecs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.cmbSpecs.SelectedItem is not SpecItem item)
            {
                this.SelectedJob = null;
                this.SpecSelected?.Invoke(this, null);
                return;
            }

            try
            {
                this.SelectedJob = ExportJob.Load(item.ConfigFilePath);
                this.SpecSelected?.Invoke(this, this.SelectedJob);
            }
            catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is System.IO.IOException)
            {
                this.SelectedJob = null;
                this.cmbSpecs.SelectedIndex = -1;
                this.SpecSelected?.Invoke(this, null);
                MessageBox.Show(
                    $"Failed to load spec config:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            this.LoadSpecs(this.lastBaseDir);
        }

        private sealed class SpecItem
        {
            public SpecItem(string displayName, string configFilePath)
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
