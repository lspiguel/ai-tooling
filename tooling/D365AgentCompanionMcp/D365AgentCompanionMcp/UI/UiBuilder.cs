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

        /// <summary>The frame and text colour of the accent button.</summary>
        public static readonly Color AccentColor = Color.FromArgb(0x00, 0x5A, 0x9E);

        /// <summary>The background of the accent button.</summary>
        public static readonly Color AccentBackColor = Color.FromArgb(0xE5, 0xF1, 0xFB);

        /// <summary>The background of the accent button under the pointer.</summary>
        public static readonly Color AccentHoverColor = Color.FromArgb(0xCC, 0xE4, 0xF7);

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
        /// Creates a row of controls, left to right, wrapping when narrow, with one control held apart at the right
        /// end. The left controls wrap in the space left of it, so it stays on the row's first line.
        /// </summary>
        /// <param name="trailing">The control at the right end.</param>
        /// <param name="controls">The controls on the left.</param>
        /// <returns>The row.</returns>
        public static Panel RowWithTrailing(Control trailing, params Control[] controls) => new TrailingRow(Row(controls), trailing);

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

        /// <summary>
        /// Creates a button that stands out from the others: a flat accent frame, a tinted background and bold text.
        /// Kept for the one action a person must be able to find at a glance.
        /// </summary>
        /// <param name="text">The caption.</param>
        /// <param name="click">The click handler.</param>
        /// <returns>The button.</returns>
        public static Button AccentButton(string text, EventHandler click)
        {
            var button = Button(text, click);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = AccentColor;
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
            button.FlatAppearance.MouseDownBackColor = AccentHoverColor;
            button.BackColor = AccentBackColor;
            button.ForeColor = AccentColor;
            button.UseVisualStyleBackColor = false;
            button.Font = new Font(button.Font, FontStyle.Bold);
            button.Padding = new Padding(8, 2, 8, 2);
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

        /// <summary>
        /// Shows a modeless window centred on its owner. <see cref="FormStartPosition.CenterParent"/> applies only to
        /// <see cref="Form.ShowDialog(IWin32Window)"/>; a window opened with <see cref="Form.Show(IWin32Window)"/> would
        /// otherwise go wherever Windows places it. The position is set on <see cref="Form.Load"/>, when the window's
        /// size is final and before it is visible. A minimized or missing owner leaves it centred on the screen.
        /// </summary>
        /// <param name="form">The window.</param>
        /// <param name="owner">The owner, or <c>null</c>.</param>
        public static void ShowCentered(this Form form, Form? owner)
        {
            if (owner == null || owner.WindowState == FormWindowState.Minimized)
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show(owner);
                return;
            }

            form.StartPosition = FormStartPosition.Manual;
            form.Load += (s, e) => form.Location = CenteredOn(owner.Bounds, form.Size, Screen.FromControl(owner).WorkingArea);
            form.Show(owner);
        }

        /// <summary>Gets the location that centres a window on its owner, kept inside the owner's screen.</summary>
        /// <param name="owner">The owner's bounds.</param>
        /// <param name="size">The window's size.</param>
        /// <param name="workingArea">The working area of the owner's screen.</param>
        /// <returns>The window's location.</returns>
        internal static Point CenteredOn(Rectangle owner, Size size, Rectangle workingArea)
        {
            var x = owner.Left + ((owner.Width - size.Width) / 2);
            var y = owner.Top + ((owner.Height - size.Height) / 2);
            x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - size.Width));
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - size.Height));
            return new Point(x, y);
        }

        /// <summary>
        /// A wrapping row with one control at its right end. Laid out by hand: a table measures the wrapping part as if
        /// each control sat on its own line, the same fault <see cref="Group"/> works around.
        /// </summary>
        private sealed class TrailingRow : Panel
        {
            private readonly FlowLayoutPanel start;
            private readonly Control trailing;

            internal TrailingRow(FlowLayoutPanel start, Control trailing)
            {
                this.start = start;
                this.trailing = trailing;
                this.AutoSize = true;
                this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                this.Margin = new Padding(0);
                this.Controls.Add(start);
                this.Controls.Add(trailing);
            }

            /// <inheritdoc/>
            public override Size GetPreferredSize(Size proposedSize)
            {
                var trailingSize = this.TrailingSize();
                if (proposedSize.Width <= 0 || proposedSize.Width == int.MaxValue)
                {
                    var single = this.start.GetPreferredSize(Size.Empty);
                    return new Size(single.Width + trailingSize.Width, Math.Max(single.Height, trailingSize.Height));
                }

                var startHeight = this.start.GetPreferredSize(new Size(Math.Max(1, proposedSize.Width - trailingSize.Width), 0)).Height;
                return new Size(proposedSize.Width, Math.Max(startHeight, trailingSize.Height));
            }

            /// <inheritdoc/>
            protected override void OnLayout(LayoutEventArgs levent)
            {
                var size = this.trailing.GetPreferredSize(Size.Empty);
                var margin = this.trailing.Margin;
                this.trailing.SetBounds(this.ClientSize.Width - margin.Right - size.Width, margin.Top, size.Width, size.Height);

                var startWidth = Math.Max(1, this.ClientSize.Width - this.TrailingSize().Width);
                this.start.SetBounds(0, 0, startWidth, this.start.GetPreferredSize(new Size(startWidth, 0)).Height);
            }

            private Size TrailingSize()
            {
                var size = this.trailing.GetPreferredSize(Size.Empty);
                return new Size(size.Width + this.trailing.Margin.Horizontal, size.Height + this.trailing.Margin.Vertical);
            }
        }
    }
}
