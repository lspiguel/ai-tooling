// <copyright file="WelcomeForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Windows.Forms;

    /// <summary>Modal welcome dialog that displays the Quick Start guide on first plugin load.</summary>
    internal sealed partial class WelcomeForm : Form
    {
        private const string ResourceName =
            "Lspiguel.Xrm.D365ContextExporter.Resources.WelcomeQuickStart.html";

        /// <summary>
        /// Initializes a new instance of the <see cref="WelcomeForm"/> class.
        /// </summary>
        public WelcomeForm()
        {
            this.InitializeComponent();
            this.webBrowser.Navigating += this.WebBrowser_Navigating;
            this.LoadHtml();
        }

        private void LoadHtml()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                this.webBrowser.DocumentText = "<p>Welcome to D365 CE Context Exporter!</p>";
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            this.webBrowser.DocumentText = reader.ReadToEnd();
        }

        private void WebBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            var url = e.Url.ToString();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
