// <copyright file="LogView.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Windows.Forms;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

    /// <summary>
    /// Shows a call log: monospaced, no wrapping, scrolling both ways (SPEC §8.6). Double-clicking a line opens the
    /// call's detail (SPEC §8.4).
    /// </summary>
    internal sealed class LogView : ListBox
    {
        /// <summary>Initializes a new instance of the <see cref="LogView"/> class.</summary>
        public LogView()
        {
            this.Font = UiBuilder.Monospace();
            this.HorizontalScrollbar = true;
            this.IntegralHeight = false;
            this.SelectionMode = SelectionMode.One;
            this.Dock = DockStyle.Fill;
            this.DoubleClick += this.OnDoubleClickLine;
        }

        /// <summary>Appends an entry's lines and scrolls to them.</summary>
        /// <param name="entry">The entry.</param>
        public void Append(CallLogEntry entry)
        {
            this.BeginUpdate();
            foreach (var line in entry.Lines)
            {
                this.Items.Add(new Line(line, entry));
                this.HorizontalExtent = Math.Max(this.HorizontalExtent, TextRenderer.MeasureText(line, this.Font).Width + 8);
            }

            this.EndUpdate();
            this.TopIndex = Math.Max(0, this.Items.Count - 1);
        }

        /// <summary>Removes every line.</summary>
        public void ClearLines()
        {
            this.Items.Clear();
            this.HorizontalExtent = 0;
        }

        private void OnDoubleClickLine(object? sender, EventArgs e)
        {
            if (this.SelectedItem is Line line)
            {
                using (var detail = new CallDetailForm(line.Entry))
                {
                    detail.ShowDialog(this.TopLevelControl as Form);
                }
            }
        }

        private sealed class Line
        {
            public Line(string text, CallLogEntry entry)
            {
                this.Text = text;
                this.Entry = entry;
            }

            public string Text { get; }

            public CallLogEntry Entry { get; }

            public override string ToString() => this.Text;
        }
    }
}
