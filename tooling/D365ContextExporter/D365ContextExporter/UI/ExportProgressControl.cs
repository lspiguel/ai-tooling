using System.Windows.Forms;

namespace D365ContextExporter.UI
{
    /// <summary>
    /// A read-only log panel. Phase 1 stub — progress bars and cancel button are deferred to Phase 3.
    /// </summary>
    public partial class ExportProgressControl : UserControl
    {
        /// <summary>Initialises a new instance of <see cref="ExportProgressControl"/>.</summary>
        public ExportProgressControl()
        {
            InitializeComponent();
        }

        /// <summary>Appends a line to the log, scrolling to the end.</summary>
        /// <param name="message">The text to append.</param>
        public void AppendLog(string message)
        {
            rtbLog.AppendText(message + "\n");
            rtbLog.ScrollToCaret();
        }

        /// <summary>Clears all log content.</summary>
        public void ClearLog()
        {
            rtbLog.Clear();
        }
    }
}
