// <copyright file="DevConfirmationForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System.Drawing;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;

    /// <summary>
    /// Asks whether a newly bound connection is a development environment where the agent may shape solutions
    /// (SPEC §5 Environment guard, §7). Offers once, session and environment; <i>always</i> is set only in settings.
    /// </summary>
    internal sealed class DevConfirmationForm : Form
    {
        /// <summary>Initializes a new instance of the <see cref="DevConfirmationForm"/> class.</summary>
        /// <param name="connectionName">The connection name.</param>
        /// <param name="organizationName">The organization's friendly name.</param>
        /// <param name="url">The organization URL.</param>
        public DevConfirmationForm(string connectionName, string organizationName, string url)
        {
            this.Text = $"Development environment? — {organizationName}";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ClientSize = new Size(600, 220);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            var message = new Label
            {
                Text = $"Is {connectionName} ({organizationName}, {url}) a development environment where the agent may create solutions and add components to them?\r\n\r\n"
                    + "Write tools refuse on this connection until you confirm. Leave build, test and production environments unconfirmed: the agent can still read them, with your approval.",
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8) };
            buttons.Controls.Add(UiBuilder.Button("Confirm for this tab's binding", (s, e) => this.Choose(ApprovalScope.Once)));
            buttons.Controls.Add(UiBuilder.Button("Confirm for this session", (s, e) => this.Choose(ApprovalScope.Session)));
            buttons.Controls.Add(UiBuilder.Button("Confirm and remember this environment", (s, e) => this.Choose(ApprovalScope.Environment)));
            buttons.Controls.Add(UiBuilder.Button("Not a development environment", (s, e) => this.Choose(null)));

            this.Controls.Add(message);
            this.Controls.Add(buttons);
        }

        /// <summary>Gets the confirmation scope chosen, or <c>null</c> when the environment stays unconfirmed.</summary>
        public ApprovalScope? Choice { get; private set; }

        private void Choose(ApprovalScope? scope)
        {
            this.Choice = scope;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
