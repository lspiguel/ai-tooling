// <copyright file="ExportProgressControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.UI
{
    using System.Windows.Forms;

    /// <summary>
    /// A read-only log panel. Phase 1 stub — progress bars and cancel button are deferred to Phase 3.
    /// </summary>
    public partial class ExportProgressControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportProgressControl"/> class.Initialises a new instance of <see cref="ExportProgressControl"/>.
        /// </summary>
        public ExportProgressControl()
        {
            this.InitializeComponent();
        }

        /// <summary>Appends a line to the log, scrolling to the end.</summary>
        /// <param name="message">The text to append.</param>
        public void AppendLog(string message)
        {
            this.rtbLog.AppendText(message + "\n");
            this.rtbLog.ScrollToCaret();
        }

        /// <summary>Clears all log content.</summary>
        public void ClearLog()
        {
            this.rtbLog.Clear();
        }
    }
}
