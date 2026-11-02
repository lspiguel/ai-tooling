// <copyright file="UiBuilder.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>Small builders for the code-built panel and dialogs.</summary>
    internal static class UiBuilder
    {
        /// <summary>The colour of warnings.</summary>
        public static readonly Color WarningColor = Color.FromArgb(0xB0, 0x3A, 0x00);

        /// <summary>Creates the monospaced font used for logs and arguments.</summary>
        /// <returns>The font.</returns>
        public static Font Monospace() => new Font(FontFamily.GenericMonospace, 9f);

        /// <summary>Creates a single-column table whose rows size to their content.</summary>
        /// <param name="controls">The rows, top to bottom.</param>
        /// <returns>The table.</returns>
        public static TableLayoutPanel Stack(params Control[] controls)
        {
            var table = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var control in controls)
            {
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                control.Dock = control.Dock == DockStyle.None ? DockStyle.Top : control.Dock;
                table.Controls.Add(control);
            }

            return table;
        }

        /// <summary>Creates a row of controls, left to right, wrapping when narrow.</summary>
        /// <param name="controls">The controls.</param>
        /// <returns>The row.</returns>
        public static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0) };
            row.Controls.AddRange(controls);
            return row;
        }

        /// <summary>
        /// Creates a group box holding rows, as tall as its rows at its current width. An auto-sized table measures
        /// wrapping rows as if each control sat on its own line, which made the groups about twice as tall as their
        /// content; the height is computed here instead, and follows the width as rows wrap.
        /// </summary>
        /// <param name="title">The group title.</param>
        /// <param name="rows">The rows.</param>
        /// <returns>The group.</returns>
        public static GroupBox Group(string title, params Control[] rows)
        {
            var group = new GroupBox { Text = title, Dock = DockStyle.Top, Padding = new Padding(6) };
            var stack = Stack(rows);
            stack.AutoSize = false;
            group.Controls.Add(stack);

            var fitting = false;
            void Fit()
            {
                if (fitting)
                {
                    return;
                }

                fitting = true;
                try
                {
                    var width = Math.Max(1, group.DisplayRectangle.Width);
                    var content = 0;
                    foreach (Control row in stack.Controls)
                    {
                        content += row.GetPreferredSize(new Size(width - row.Margin.Horizontal, 0)).Height + row.Margin.Vertical;
                    }

                    var height = (group.Height - group.DisplayRectangle.Height) + content;
                    if (group.Height != height)
                    {
                        group.Height = height;
                    }
                }
                finally
                {
                    fitting = false;
                }
            }

            group.Layout += (s, e) => Fit();
            foreach (Control row in rows)
            {
                // Rows change height when a label's text changes or a row wraps.
                row.SizeChanged += (s, e) => Fit();
            }

            return group;
        }

        /// <summary>Creates a fixed-width caption, such as <c>Dev env</c>.</summary>
        /// <param name="text">The caption.</param>
        /// <returns>The label.</returns>
        public static Label Caption(string text) => new Label { Text = text, Width = 90, Height = 23, TextAlign = ContentAlignment.MiddleLeft };

        /// <summary>Creates a label that sizes to its text.</summary>
        /// <param name="text">The initial text.</param>
        /// <returns>The label.</returns>
        public static Label Text(string text = "") => new Label { Text = text, AutoSize = true, Padding = new Padding(0, 6, 6, 0) };

        /// <summary>Creates a button that sizes to its text.</summary>
        /// <param name="text">The caption.</param>
        /// <param name="click">The click handler.</param>
        /// <returns>The button.</returns>
        public static Button Button(string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            button.Click += click;
            return button;
        }

        /// <summary>Creates a read-only, scrolling, monospaced text box.</summary>
        /// <param name="text">The text.</param>
        /// <param name="height">The height.</param>
        /// <returns>The text box.</returns>
        public static TextBox ReadOnlyText(string text, int height) => new TextBox
        {
            Text = text,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = Monospace(),
            Height = height,
            Dock = DockStyle.Top,
        };

        /// <summary>Creates a number box.</summary>
        /// <param name="minimum">The smallest value.</param>
        /// <param name="maximum">The largest value.</param>
        /// <param name="width">The width.</param>
        /// <returns>The number box.</returns>
        public static NumericUpDown Number(int minimum, int maximum, int width = 80) =>
            new NumericUpDown { Minimum = minimum, Maximum = maximum, Width = width, ThousandsSeparator = true };
    }
}
