// <copyright file="WelcomeForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    using System;
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

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
