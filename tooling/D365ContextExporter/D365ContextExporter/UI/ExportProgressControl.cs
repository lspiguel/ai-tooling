// <copyright file="ExportProgressControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    using System;
    using System.Windows.Forms;

    /// <summary>A log panel with a cancel button and per-query progress indicator.</summary>
    public partial class ExportProgressControl : UserControl
    {
        /// <summary>Raised when the user clicks the Cancel button.</summary>
        public event EventHandler? CancelRequested;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportProgressControl"/> class.
        /// </summary>
        public ExportProgressControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Appends a line to the log, scrolling to the end.</summary>
        /// <param name="message">The text to append.</param>
        public void AppendLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(this.AppendLog), message);
                return;
            }

            this.rtbLog.AppendText(message + "\n");
            this.rtbLog.ScrollToCaret();
        }

        /// <summary>Clears all log content.</summary>
        public void ClearLog()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(this.ClearLog));
                return;
            }

            this.rtbLog.Clear();
        }

        /// <summary>Updates the query progress label.</summary>
        /// <param name="current">Current query index (1-based).</param>
        /// <param name="total">Total number of queries.</param>
        /// <param name="queryId">Identifier of the query just completed.</param>
        public void SetProgress(int current, int total, string queryId)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, int, string>(this.SetProgress), current, total, queryId);
                return;
            }

            this.lblProgress.Text = $"Query {current} / {total}";
        }

        /// <summary>Enables or disables the cancel button and toggles the status label.</summary>
        /// <param name="running"><c>true</c> while an export is in progress; <c>false</c> when idle.</param>
        public void SetRunning(bool running)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool>(this.SetRunning), running);
                return;
            }

            this.btnCancel.Enabled = running;
            this.lblProgress.Text = running ? "Running…" : string.Empty;
            this.spinnerProgress.Visible = running;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.btnCancel.Enabled = false;
            this.CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
