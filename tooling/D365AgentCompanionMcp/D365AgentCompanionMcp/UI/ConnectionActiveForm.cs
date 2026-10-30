// <copyright file="ConnectionActiveForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// The <i>Connection already active</i> dialog (SPEC §8.5, C3). Returns <see cref="DialogResult.Abort"/> for
    /// <b>Close this tab</b> and <see cref="DialogResult.Retry"/> for <b>Choose another connection</b>; closing it leaves the tab unbound.
    /// </summary>
    internal sealed class ConnectionActiveForm : Form
    {
        /// <summary>Initializes a new instance of the <see cref="ConnectionActiveForm"/> class.</summary>
        /// <param name="connectionName">The connection this tab received.</param>
        /// <param name="organizationName">Its organization.</param>
        /// <param name="otherConnectionName">The connection already active in another tab.</param>
        /// <param name="sameOrganization">Whether the match is on organization rather than connection ID.</param>
        public ConnectionActiveForm(string connectionName, string organizationName, string otherConnectionName, bool sameOrganization)
        {
            this.Text = "Connection already active";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ClientSize = new Size(520, 200);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            var first = sameOrganization
                ? $"{connectionName} connects to the same organization as {otherConnectionName}, which is active in another Agent Companion tab."
                : $"{connectionName} ({organizationName}) is already active in another Agent Companion tab.";
            var message = new Label
            {
                Text = $"{first}\r\n\r\nEach environment has one tab, so every call to it is approved and logged in one place. Use the existing tab, or choose another connection for this one.",
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8) };
            buttons.Controls.Add(new Button { Text = "Close this tab", DialogResult = DialogResult.Abort, AutoSize = true });
            buttons.Controls.Add(new Button { Text = "Choose another connection", DialogResult = DialogResult.Retry, AutoSize = true });

            this.Controls.Add(message);
            this.Controls.Add(buttons);
        }
    }
}
