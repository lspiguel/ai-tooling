using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Lspiguel.Xrm.D365AgentCompanionMcp.UI;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.UITests
{
    /// <summary>
    /// Panel groups are as tall as their rows (SPEC §8.1). An auto-sized table measured wrapping rows one control per
    /// line, which made the two top groups about twice the height of their content.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class UiBuilderTests
    {
        [Test]
        public void Group_FitsItsRows_WideAndNarrow()
        {
            var group = UiBuilder.Group(
                "Host",
                UiBuilder.Row(UiBuilder.Caption("Server"), UiBuilder.Text("Listening"), UiBuilder.Button("Stop server — all connections", (s, e) => { })),
                UiBuilder.Row(
                    UiBuilder.Caption("Limits"),
                    UiBuilder.Text("Max page size"),
                    UiBuilder.Number(1, 5000),
                    UiBuilder.Text("Response cap"),
                    UiBuilder.Number(1000, 500000, 90),
                    UiBuilder.Text("Timeout (s)"),
                    UiBuilder.Number(10, 600, 60),
                    UiBuilder.Button("Apply", (s, e) => { }),
                    UiBuilder.Button("Always settings…", (s, e) => { })),
                UiBuilder.Row(UiBuilder.Text(string.Empty)));

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.Dock = DockStyle.Fill;
            table.Controls.Add(group);
            table.Controls.Add(new TextBox { Multiline = true, Dock = DockStyle.Fill });

            using var form = new Form { Width = 1200, Height = 800, StartPosition = FormStartPosition.Manual, Left = -3000, ShowInTaskbar = false };
            form.Controls.Add(table);
            form.Show();
            Application.DoEvents();
            var wide = AssertFits(group);

            form.Width = 450;
            Application.DoEvents();
            var narrow = AssertFits(group);

            Assert.That(narrow, Is.GreaterThan(wide), "rows wrap when narrow, and the group grows with them");
        }

        [Test]
        public void RowWithTrailing_KeepsTheTrailingControlAtTheRightEnd_WideAndNarrow()
        {
            var accent = UiBuilder.AccentButton("Active Approvals…", (s, e) => { });
            var stop = UiBuilder.Button("Stop server — all connections", (s, e) => { });
            var row = UiBuilder.RowWithTrailing(accent, UiBuilder.Caption("Server"), UiBuilder.Text("● Running   http://127.0.0.1:47321/mcp"), stop);
            var group = UiBuilder.Group("Host", row, UiBuilder.Row(UiBuilder.Caption("Clients"), UiBuilder.Button("Copy client config", (s, e) => { })));

            using var form = new Form { Width = 1200, Height = 600, StartPosition = FormStartPosition.Manual, Left = -3000, ShowInTaskbar = false };
            form.Controls.Add(group);
            form.Show();
            Application.DoEvents();
            AssertAtRightEnd(row, accent);
            Assert.That(stop.Top, Is.LessThan(stop.Height), "wide, the row does not wrap");
            Assert.That(row.Height, Is.LessThanOrEqualTo(accent.Height + accent.Margin.Vertical), "wide, the row is one line tall");
            var wide = AssertFits(group);

            form.Width = 450;
            Application.DoEvents();
            AssertAtRightEnd(row, accent);
            Assert.That(stop.Right, Is.LessThanOrEqualTo(accent.Left), "the left controls wrap left of the trailing control");
            Assert.That(AssertFits(group), Is.GreaterThan(wide), "the left controls wrap when narrow, and the group grows with them");

            Assert.That(accent.Font.Bold, Is.True);
            Assert.That(accent.FlatStyle, Is.EqualTo(FlatStyle.Flat));
            Assert.That(accent.FlatAppearance.BorderColor, Is.EqualTo(UiBuilder.AccentColor));
        }

        [Test]
        public void CenteredOn_CentresOnTheOwner_AndKeepsTheWindowOnItsScreen()
        {
            var screen = new System.Drawing.Rectangle(0, 0, 1920, 1040);
            var size = new System.Drawing.Size(800, 600);

            Assert.That(UiBuilder.CenteredOn(new System.Drawing.Rectangle(100, 50, 1600, 900), size, screen), Is.EqualTo(new System.Drawing.Point(500, 200)));
            Assert.That(UiBuilder.CenteredOn(new System.Drawing.Rectangle(1500, 700, 400, 300), size, screen), Is.EqualTo(new System.Drawing.Point(1120, 440)), "clamped to the right and bottom");
            Assert.That(UiBuilder.CenteredOn(new System.Drawing.Rectangle(-100, -50, 300, 200), size, screen), Is.EqualTo(new System.Drawing.Point(0, 0)), "clamped to the left and top");
        }

        [Test]
        public void ShowCentered_OpensAModelessWindowCentredOnItsOwner()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            using var owner = new Form { StartPosition = FormStartPosition.Manual, Bounds = new System.Drawing.Rectangle(area.Left + 40, area.Top + 30, 1000, 700), ShowInTaskbar = false };
            owner.Show();
            using var dialog = new Form { StartPosition = FormStartPosition.CenterParent, ClientSize = new System.Drawing.Size(400, 300), ShowInTaskbar = false };

            dialog.ShowCentered(owner);
            Application.DoEvents();

            var ownerCentre = new System.Drawing.Point(owner.Left + (owner.Width / 2), owner.Top + (owner.Height / 2));
            var dialogCentre = new System.Drawing.Point(dialog.Left + (dialog.Width / 2), dialog.Top + (dialog.Height / 2));
            Assert.That(Math.Abs(dialogCentre.X - ownerCentre.X), Is.LessThanOrEqualTo(1), $"owner {owner.Bounds}, dialog {dialog.Bounds}");
            Assert.That(Math.Abs(dialogCentre.Y - ownerCentre.Y), Is.LessThanOrEqualTo(1), $"owner {owner.Bounds}, dialog {dialog.Bounds}");
            Assert.That(dialog.Owner, Is.SameAs(owner));
        }

        private static void AssertAtRightEnd(Control row, Control trailing)
        {
            Assert.That(row.Right, Is.EqualTo(row.Parent.ClientSize.Width), "the row spans its group");
            Assert.That(row.ClientSize.Width - trailing.Right - trailing.Margin.Right, Is.LessThanOrEqualTo(1), "the trailing control sits at the right end");
            Assert.That(trailing.Top, Is.LessThanOrEqualTo(trailing.Margin.Top), "on the row's first line");
        }

        private static int AssertFits(GroupBox group)
        {
            var stack = group.Controls[0];
            var rows = stack.Controls.Cast<Control>().ToList();
            var content = rows.Sum(r => r.Height + r.Margin.Vertical);

            Assert.That(rows.Last().Bottom, Is.LessThanOrEqualTo(stack.ClientSize.Height), "no row is clipped");
            Assert.That(stack.ClientSize.Height, Is.EqualTo(content), "no empty space below the rows");
            return group.Height;
        }
    }
}
