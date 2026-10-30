// <copyright file="ChoiceForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// Two-button modal used for the approval and the "Connection already active" dialogs.
    /// Returns <see cref="DialogResult.Yes"/> for the first button, <see cref="DialogResult.No"/> for the second,
    /// and <see cref="DialogResult.Abort"/> when the optional timeout expires.
    /// </summary>
    internal sealed class ChoiceForm : Form
    {
        private readonly Timer timer = new Timer { Interval = 1000 };
        private readonly Label countdown = new Label { Dock = DockStyle.Bottom, Height = 20, TextAlign = ContentAlignment.MiddleRight };
        private int remainingSeconds;

        public ChoiceForm(string title, string message, string first, string second, TimeSpan? timeout = null)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(460, 190);
            this.ShowInTaskbar = false;

            var text = new Label { Dock = DockStyle.Fill, Padding = new Padding(12), Text = message };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            buttons.Controls.Add(new Button { Text = second, DialogResult = DialogResult.No, AutoSize = true });
            buttons.Controls.Add(new Button { Text = first, DialogResult = DialogResult.Yes, AutoSize = true });

            this.Controls.Add(text);
            this.Controls.Add(this.countdown);
            this.Controls.Add(buttons);

            if (timeout.HasValue)
            {
                this.remainingSeconds = (int)timeout.Value.TotalSeconds;
                this.timer.Tick += this.OnTick;
                this.Shown += (sender, e) => this.timer.Start();
                this.UpdateCountdown();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.timer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnTick(object sender, EventArgs e)
        {
            this.remainingSeconds--;
            this.UpdateCountdown();
            if (this.remainingSeconds <= 0)
            {
                this.timer.Stop();
                this.DialogResult = DialogResult.Abort;
            }
        }

        private void UpdateCountdown() => this.countdown.Text = $"Times out in {this.remainingSeconds}s ";
    }
}
