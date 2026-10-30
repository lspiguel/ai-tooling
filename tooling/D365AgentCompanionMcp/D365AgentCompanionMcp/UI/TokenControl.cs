// <copyright file="TokenControl.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;

    /// <summary>
    /// The bearer token, masked, with <b>Copy</b> and <b>Regenerate</b> (SPEC §8.1, D8). One self-contained control
    /// backed by the token store, so Phase 6 can move it into the Active Approvals window without change.
    /// </summary>
    internal sealed class TokenControl : UserControl
    {
        private readonly CompanionHost host;
        private readonly TextBox token = new TextBox { ReadOnly = true, UseSystemPasswordChar = true, Width = 220 };

        /// <summary>Initializes a new instance of the <see cref="TokenControl"/> class.</summary>
        /// <param name="host">The host whose token is shown.</param>
        public TokenControl(CompanionHost host)
        {
            this.host = host;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.Controls.Add(UiBuilder.Row(
                this.token,
                UiBuilder.Button("Copy", (s, e) => this.CopyToken()),
                UiBuilder.Button("Regenerate", (s, e) => this.Regenerate())));

            this.host.Tokens.Changed += this.OnTokenChanged;
            this.ShowToken();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.host.Tokens.Changed -= this.OnTokenChanged;
            }

            base.Dispose(disposing);
        }

        private void OnTokenChanged(object? sender, EventArgs e)
        {
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.BeginInvoke(new Action(this.ShowToken));
            }
        }

        private void ShowToken() => this.token.Text = this.host.Tokens.Token ?? string.Empty;

        private void CopyToken()
        {
            if (!string.IsNullOrEmpty(this.host.Tokens.Token))
            {
                Clipboard.SetText(this.host.Tokens.Token);
            }
        }

        private void Regenerate()
        {
            var answer = MessageBox.Show(
                this.TopLevelControl as Form,
                "Regenerating the token affects every connection and every client configuration: each client must be updated with the new token.\r\n\r\nRegenerate it?",
                "Regenerate bearer token",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
            {
                this.host.RegenerateToken();
            }
        }
    }
}
