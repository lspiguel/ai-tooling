// <copyright file="CallDetailForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

    /// <summary>One call in full (SPEC §8.4). Held in memory only; nothing here is ever saved.</summary>
    internal sealed class CallDetailForm : Form
    {
        /// <summary>Initializes a new instance of the <see cref="CallDetailForm"/> class.</summary>
        /// <param name="entry">The call.</param>
        public CallDetailForm(CallLogEntry entry)
        {
            this.Text = "Call detail";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(760, 560);
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            var arguments = UiBuilder.ReadOnlyText(entry.ArgumentsJson, 180);
            var result = UiBuilder.ReadOnlyText(entry.ResultJson, 260);
            var size = string.Format(CultureInfo.InvariantCulture, "Response size {0:0.0} KB · 1 page", entry.ResultJson.Length / 1024.0);

            this.Controls.Add(UiBuilder.Stack(
                UiBuilder.Text($"{entry.ReceivedLocal:HH:mm:ss}   {entry.Tool}   {entry.Tier}   {Describe(entry.Approval)}"),
                UiBuilder.Row(UiBuilder.Text("Arguments (verbatim)"), UiBuilder.Button("Copy", (s, e) => Clipboard.SetText(entry.ArgumentsJson))),
                arguments,
                UiBuilder.Row(UiBuilder.Text("Result"), UiBuilder.Button("Copy", (s, e) => Clipboard.SetText(entry.ResultJson))),
                result,
                UiBuilder.Text(size),
                UiBuilder.Row(UiBuilder.Button("Close", (s, e) => this.Close()))));
        }

        private static string Describe(string approval)
        {
            switch (approval)
            {
                case "once":
                    return "Allowed once";
                case "session":
                    return "Allowed for this session";
                case "environment":
                    return "Allowed for this environment";
                case "always":
                    return "Allowed always (settings)";
                case "none":
                    return "Host tool — logged, not approved";
                case "page":
                    return "Continuation page — under its first call's approval";
                default:
                    return "Did not run";
            }
        }
    }
}
