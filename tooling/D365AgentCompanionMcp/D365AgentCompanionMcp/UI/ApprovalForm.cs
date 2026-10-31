// <copyright file="ApprovalForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

    /// <summary>
    /// The approval dialog (SPEC §8.2), and the publisher creation dialog (§8.3) when the request carries a confirmation
    /// to type. Modeless and owned by XrmToolBox's main window, so it appears whichever tab is in front and calls for
    /// different connections can await approval at the same time (C9). Closing it denies.
    /// </summary>
    internal sealed class ApprovalForm : Form
    {
        private readonly Timer countdownTimer = new Timer { Interval = 1000 };
        private readonly Label countdown = UiBuilder.Text();
        private readonly DateTime deadlineUtc;
        private readonly string? confirmation;
        private readonly TextBox typed = new TextBox { Width = 160 };
        private bool withdrawn;
        private bool decided;

        /// <summary>Initializes a new instance of the <see cref="ApprovalForm"/> class.</summary>
        /// <param name="request">What to show.</param>
        public ApprovalForm(ApprovalRequest request)
        {
            this.deadlineUtc = request.DeadlineUtc;
            this.confirmation = request.Confirmation;
            this.Text = this.confirmation != null ? "Create publisher — read carefully" : $"Approve tool call — {request.OrganizationName}";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(720, string.IsNullOrEmpty(request.Summary) ? 520 : 700);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            var rows = new List<Control>
            {
                UiBuilder.Text($"Environment : {request.OrganizationName}  ({request.ConnectionName})"),
                UiBuilder.Text($"Tool        : {request.Tool}        Tier: {request.Tier.ToString().ToUpperInvariant()}"),
            };

            if (this.confirmation != null)
            {
                rows.Add(Warning("A second publisher, even with the same prefix, can cause layering problems that cannot be fixed later. Reuse an existing publisher unless you are certain."));
            }

            if (!string.IsNullOrEmpty(request.Summary))
            {
                rows.Add(UiBuilder.Text("What this call does (resolved)"));
                rows.Add(UiBuilder.ReadOnlyText(request.Summary, 200));
            }

            rows.Add(UiBuilder.Text("Arguments (verbatim)"));
            rows.Add(UiBuilder.ReadOnlyText(request.Arguments, string.IsNullOrEmpty(request.Summary) ? 200 : 110));

            if (request.TouchesRecords)
            {
                rows.Add(Warning("⚠ This query reads records, not only configuration."));
            }

            if (request.Reason != null || request.Tier != ToolTier.Read)
            {
                // Agent text is shown as data in its own box and never as instructions (R8).
                var reason = UiBuilder.ReadOnlyText(request.Reason ?? string.Empty, 60);
                reason.WordWrap = true;
                reason.ScrollBars = ScrollBars.Vertical;
                var box = new GroupBox { Text = "Agent-supplied — not verified", Height = 90, Dock = DockStyle.Top };
                reason.Dock = DockStyle.Fill;
                box.Controls.Add(reason);
                rows.Add(box);
            }

            if (!string.IsNullOrEmpty(request.Recommendation))
            {
                rows.Add(UiBuilder.Text(request.Recommendation));
            }

            if (this.confirmation != null)
            {
                rows.Add(UiBuilder.Row(UiBuilder.Text("Type the prefix to confirm:"), this.typed));
            }

            var buttons = request.Options.Select(o => UiBuilder.Button(this.confirmation != null ? "Create publisher" : Caption(o), (s, e) => this.Decide(o))).ToList();
            var deny = UiBuilder.Button("Deny", (s, e) => this.Decide(ApprovalDecision.Deny));
            buttons.Add(deny);
            this.CancelButton = deny;
            rows.Add(UiBuilder.Row(buttons.Concat(new Control[] { this.countdown }).ToArray()));

            this.Controls.Add(UiBuilder.Stack(rows.ToArray()));
            this.countdownTimer.Tick += (s, e) => this.UpdateCountdown();
            this.Shown += (s, e) =>
            {
                this.UpdateCountdown();
                this.countdownTimer.Start();
                NativeMethods.Flash(this.Owner?.Handle ?? IntPtr.Zero);
            };
            this.FormClosing += (s, e) =>
            {
                if (!this.decided && !this.withdrawn)
                {
                    this.Decide(ApprovalDecision.Deny);
                }
            };
        }

        /// <summary>Raised once, with the person's decision.</summary>
        public event EventHandler<ApprovalDecision>? Decided;

        /// <summary>Closes the dialog without a decision — the call timed out, was cancelled, or its connection or the host went away.</summary>
        public void Withdraw()
        {
            this.withdrawn = true;
            if (!this.IsDisposed)
            {
                this.Close();
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.countdownTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private static string Caption(ApprovalDecision decision)
        {
            switch (decision)
            {
                case ApprovalDecision.AllowSession:
                    return "Allow for this session";
                case ApprovalDecision.AllowEnvironment:
                    return "Allow for this environment";
                default:
                    return "Allow once";
            }
        }

        private static Label Warning(string text)
        {
            var label = UiBuilder.Text(text);
            label.ForeColor = UiBuilder.WarningColor;
            label.MaximumSize = new Size(690, 0);
            return label;
        }

        private void Decide(ApprovalDecision decision)
        {
            if (this.decided || this.withdrawn)
            {
                return;
            }

            // A wrong typed confirmation counts as a denial (SPEC §6.7).
            if (decision != ApprovalDecision.Deny && this.confirmation != null && !string.Equals(this.typed.Text.Trim(), this.confirmation, StringComparison.Ordinal))
            {
                decision = ApprovalDecision.Deny;
            }

            this.decided = true;
            this.Decided?.Invoke(this, decision);
            this.Close();
        }

        private void UpdateCountdown()
        {
            var remaining = Math.Max(0, (int)Math.Ceiling((this.deadlineUtc - DateTime.UtcNow).TotalSeconds));
            this.countdown.Text = $"Times out in {remaining}s";
        }
    }
}
