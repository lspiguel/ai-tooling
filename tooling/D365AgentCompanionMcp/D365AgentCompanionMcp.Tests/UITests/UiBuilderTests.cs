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
