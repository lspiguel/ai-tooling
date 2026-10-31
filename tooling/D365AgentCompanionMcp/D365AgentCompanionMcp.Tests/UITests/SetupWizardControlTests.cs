using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Lspiguel.Xrm.D365AgentCompanionMcp.Setup;
using Lspiguel.Xrm.D365AgentCompanionMcp.UI;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.UITests
{
    /// <summary>
    /// SPEC §8.7: three pages with Next and Cancel, Finish on the last; the update page with Skip and Done, both
    /// completing; the prompts read the current values when copied.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class SetupWizardControlTests
    {
        [Test]
        public void FirstRun_HasThreePages_NextThenFinish()
        {
            using var wizard = Wizard(SetupView.FirstRun);
            var completed = 0;
            wizard.Completed += (s, e) => completed++;

            Assert.That(wizard.PageTitle, Does.Contain("1 of 3"));
            Assert.That(Buttons(wizard), Does.Contain("Next").And.Contain("Cancel").And.Not.Contain("Finish"));
            Assert.That(Buttons(wizard), Does.Not.Contain("Export skills…"), "page 1 is the introduction only");

            Click(wizard, "Next");
            Assert.That(wizard.PageTitle, Does.Contain("2 of 3 · Preconditions"));
            Assert.That(Buttons(wizard).Count(b => b == "Copy setup prompt"), Is.EqualTo(4));
            Assert.That(Buttons(wizard), Does.Contain("Export skills…"));

            Click(wizard, "Next");
            Assert.That(wizard.PageTitle, Does.Contain("3 of 3 · Test"));
            Assert.That(Buttons(wizard), Does.Contain("Finish").And.Contain("Copy test prompt").And.Not.Contain("Next"));
            Assert.That(completed, Is.Zero);

            Click(wizard, "Finish");
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_RaisesCancelled_AndNotCompleted()
        {
            using var wizard = Wizard(SetupView.FirstRun);
            var completed = 0;
            var cancelled = 0;
            wizard.Completed += (s, e) => completed++;
            wizard.Cancelled += (s, e) => cancelled++;

            Click(wizard, "Next");
            Click(wizard, "Cancel");

            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
        }

        [TestCase("Skip")]
        [TestCase("Done")]
        public void UpdatePage_SkipAndDone_BothComplete(string button)
        {
            using var wizard = Wizard(SetupView.Updated);
            var completed = 0;
            wizard.Completed += (s, e) => completed++;

            Assert.That(wizard.PageTitle, Is.EqualTo("Plugin updated — 1.2026.10.1 → 1.2026.11.1"));
            Assert.That(Buttons(wizard), Does.Contain("Export skills…").And.Contain("Copy test prompt").And.Not.Contain("Cancel"));
            Assert.That(Buttons(wizard).Count(b => b == "Copy update prompt"), Is.EqualTo(2));

            Click(wizard, button);

            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void Prompts_ReadTheCurrentValues_WhenComposed()
        {
            var organization = default(string);
            using var wizard = new SetupWizardControl(SetupView.Updated, "1.2026.10.1", "1.2026.11.1", () => new PromptValues
            {
                PluginVersion = "1.2026.11.1",
                Endpoint = "http://127.0.0.1:47321/mcp",
                ClientConfig = "config",
                OrganizationUrl = organization,
                SkillNames = new[] { "agent-companion-context" },
            });

            Assert.That(wizard.PromptText(SetupPrompt.PacAuth), Does.Contain(SetupPrompts.NoOrganization));
            organization = "https://[org].crm.dynamics.com";
            Assert.That(wizard.PromptText(SetupPrompt.PacAuth), Does.Contain(organization));
            Assert.That(wizard.PromptText(SetupPrompt.SkillsUpdate), Does.Contain("from version 1.2026.10.1 to 1.2026.11.1"));
        }

        [Test]
        public void ShowStatus_AppearsOnTheTestPage()
        {
            using var wizard = Wizard(SetupView.FirstRun);
            wizard.ShowStatus("● Running   http://127.0.0.1:47321/mcp", "Not connected — connect this tab to an environment.");

            Click(wizard, "Next");
            Click(wizard, "Next");

            Assert.That(All(wizard).OfType<Label>().Select(l => l.Text), Does.Contain("Host: ● Running   http://127.0.0.1:47321/mcp"));
        }

        private static SetupWizardControl Wizard(SetupView view) =>
            new SetupWizardControl(view, "1.2026.10.1", "1.2026.11.1", () => new PromptValues { PluginVersion = "1.2026.11.1" });

        private static IEnumerable<Control> All(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in All(child))
                {
                    yield return descendant;
                }
            }
        }

        private static List<string> Buttons(Control root) => All(root).OfType<Button>().Select(b => b.Text).ToList();

        private static void Click(Control root, string text) => All(root).OfType<Button>().First(b => b.Text == text).PerformClick();
    }
}
