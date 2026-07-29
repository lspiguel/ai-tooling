// <copyright file="BaseDirectoryPickerControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    using System;
    using System.IO;
    using System.Windows.Forms;

    using Lspiguel.Xrm.D365ContextExporter.Properties;

    /// <summary>
    /// A label, a read-only text box, and a Browse button that lets the user pick
    /// the Context-Exporter base directory. Persists the selection across sessions.
    /// </summary>
    public partial class BaseDirectoryPickerControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDirectoryPickerControl"/> class.Initialises a new instance of <see cref="BaseDirectoryPickerControl"/>.
        /// </summary>
        public BaseDirectoryPickerControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Raised when the user selects a new directory. The event argument is the chosen path.</summary>
        public event EventHandler<string>? DirectoryChanged;

        /// <summary>Gets the currently selected base directory, or an empty string when none is set.</summary>
        public string SelectedDirectory { get; private set; } = string.Empty;

        /// <summary>Restores the previously persisted base directory from user settings.</summary>
        public void LoadSettings()
        {
            var saved = Settings.Default.BaseDirectory;
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            {
                this.SetDirectory(saved);
            }
        }

        /// <summary>Persists the current base directory to user settings.</summary>
        public void SaveSettings()
        {
            Settings.Default.BaseDirectory = this.SelectedDirectory;
            Settings.Default.Save();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select the Context-Exporter base directory",
                SelectedPath = this.SelectedDirectory,
                ShowNewFolderButton = false,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                this.SetDirectory(dialog.SelectedPath);
                this.SaveSettings();
            }
        }

        private void SetDirectory(string path)
        {
            this.SelectedDirectory = path;
            this.txtBaseDir.Text = path;
            this.DirectoryChanged?.Invoke(this, path);
        }
    }
}
