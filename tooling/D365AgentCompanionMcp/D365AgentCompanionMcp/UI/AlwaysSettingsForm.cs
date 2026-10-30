// <copyright file="AlwaysSettingsForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System.Drawing;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;

    /// <summary>
    /// The only place the <i>always</i> scopes are set (SPEC §7): Read approval for every organization, and the
    /// dev-environment guard switched off for every organization. Both persist and are shown on every tab while on.
    /// </summary>
    internal sealed class AlwaysSettingsForm : Form
    {
        private readonly CheckBox readsAlways = new CheckBox { Text = "Approve every Read call on every organization, including organizations bound later", AutoSize = true };
        private readonly CheckBox devAlways = new CheckBox { Text = "Treat every organization as a development environment (the dev-environment guard is off)", AutoSize = true };

        /// <summary>Initializes a new instance of the <see cref="AlwaysSettingsForm"/> class.</summary>
        /// <param name="store">The approval store.</param>
        /// <param name="origin">Recorded as where the setting was made.</param>
        public AlwaysSettingsForm(ApprovalStore store, string origin)
        {
            this.Text = "Always scopes — read carefully";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ClientSize = new Size(640, 260);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            this.readsAlways.Checked = store.IsAlways(ApprovalKind.ReadApproval);
            this.devAlways.Checked = store.IsAlways(ApprovalKind.DevEnvironment);

            var warning = UiBuilder.Text(
                "Always removes you from these decisions on every environment, including production environments bound later. "
                + "Recommended instead: approve reads per environment for development environments and per session for others, "
                + "and confirm development environments one at a time. Write calls still need their own approval.");
            warning.MaximumSize = new Size(600, 0);
            warning.ForeColor = UiBuilder.WarningColor;

            var save = UiBuilder.Button("Save", (s, e) =>
            {
                store.SetAlways(ApprovalKind.ReadApproval, this.readsAlways.Checked, origin);
                store.SetAlways(ApprovalKind.DevEnvironment, this.devAlways.Checked, origin);
                this.DialogResult = DialogResult.OK;
            });
            var cancel = UiBuilder.Button("Cancel", (s, e) => this.DialogResult = DialogResult.Cancel);
            this.CancelButton = cancel;

            this.Controls.Add(UiBuilder.Stack(warning, this.readsAlways, this.devAlways, UiBuilder.Row(save, cancel)));
        }
    }
}
