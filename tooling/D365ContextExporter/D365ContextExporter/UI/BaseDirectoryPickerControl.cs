using System;
using System.IO;
using System.Windows.Forms;

using D365ContextExporter.Properties;

namespace D365ContextExporter.UI
{
    /// <summary>
    /// A label, a read-only text box, and a Browse button that lets the user pick
    /// the Context-Exporter base directory. Persists the selection across sessions.
    /// </summary>
    public partial class BaseDirectoryPickerControl : UserControl
    {
        /// <summary>Raised when the user selects a new directory. The event argument is the chosen path.</summary>
        public event EventHandler<string>? DirectoryChanged;

        /// <summary>Gets the currently selected base directory, or an empty string when none is set.</summary>
        public string SelectedDirectory { get; private set; } = string.Empty;

        /// <summary>Initialises a new instance of <see cref="BaseDirectoryPickerControl"/>.</summary>
        public BaseDirectoryPickerControl()
        {
            InitializeComponent();
        }

        /// <summary>Restores the previously persisted base directory from user settings.</summary>
        public void LoadSettings()
        {
            var saved = Settings.Default.BaseDirectory;
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            {
                SetDirectory(saved);
            }
        }

        /// <summary>Persists the current base directory to user settings.</summary>
        public void SaveSettings()
        {
            Settings.Default.BaseDirectory = SelectedDirectory;
            Settings.Default.Save();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select the Context-Exporter base directory",
                SelectedPath = SelectedDirectory,
                ShowNewFolderButton = false,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SetDirectory(dialog.SelectedPath);
                SaveSettings();
            }
        }

        private void SetDirectory(string path)
        {
            SelectedDirectory = path;
            txtBaseDir.Text = path;
            DirectoryChanged?.Invoke(this, path);
        }
    }
}
