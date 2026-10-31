using System;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Setup;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.SetupTests
{
    /// <summary>
    /// SPEC §8.7: every prompt is an embedded text resource, filled completely; only the two MCP prompts hold the
    /// bearer token; the PAC authentication prompt carries the tab's organization URL when the tab is bound.
    /// </summary>
    [TestFixture]
    public class SetupPromptsTests
    {
        private const string Token = "tok-0123456789abcdef";
        private const string Endpoint = "http://127.0.0.1:47321/mcp";

        // Names, not the internal enum: a public test method cannot take an internal type.
        private static readonly string[] AllPrompts = Enum.GetNames(typeof(SetupPrompt));

        [TestCaseSource(nameof(AllPrompts))]
        public void EveryPrompt_IsEmbedded_AndFilledCompletely(string name)
        {
            var text = SetupPrompts.Compose(Parse(name), Values());

            Assert.That(text, Is.Not.Empty);
            Assert.That(text, Does.Not.Contain("{{"), "an unknown or unfilled placeholder");
            Assert.That(text, Does.Contain("Report back"), "prompts name what to report back");
        }

        [TestCaseSource(nameof(AllPrompts))]
        public void OnlyTheMcpPrompts_HoldTheToken(string name)
        {
            var text = SetupPrompts.Compose(Parse(name), Values());

            Assert.That(text.Contains(Token), Is.EqualTo(SetupPrompts.HoldsToken(Parse(name))));
        }

        [Test]
        public void TheMcpPrompts_AreTheTwoThatHoldTheToken() =>
            Assert.That(AllPrompts.Select(Parse).Where(SetupPrompts.HoldsToken), Is.EquivalentTo(new[] { SetupPrompt.McpRegister, SetupPrompt.McpUpdate }));

        [Test]
        public void PacAuth_NamesTheBoundOrganization_OrAsksWhenUnbound()
        {
            var bound = Values();
            bound.OrganizationUrl = "https://[org].crm.dynamics.com";
            Assert.That(SetupPrompts.Compose(SetupPrompt.PacAuth, bound), Does.Contain("https://[org].crm.dynamics.com"));

            Assert.That(SetupPrompts.Compose(SetupPrompt.PacAuth, Values()), Does.Contain(SetupPrompts.NoOrganization));
        }

        [TestCase(nameof(SetupPrompt.SkillsInstall))]
        [TestCase(nameof(SetupPrompt.SkillsUpdate))]
        public void SkillsPrompts_NameTheExportFolder_AndEverySkill(string name)
        {
            var prompt = Parse(name);
            var values = Values();
            Assert.That(SetupPrompts.Compose(prompt, values), Does.Contain(SetupPrompts.NoSkillsFolder));

            values.SkillsFolder = @"C:\Setup\Skills";
            var text = SetupPrompts.Compose(prompt, values);

            Assert.That(text, Does.Contain(@"C:\Setup\Skills"));
            Assert.That(text, Does.Contain("agent-companion-context, agent-companion-wip-shaping"));
        }

        [TestCase(nameof(SetupPrompt.SkillsUpdate))]
        [TestCase(nameof(SetupPrompt.McpUpdate))]
        public void UpdatePrompts_NameBothVersions(string name)
        {
            var prompt = Parse(name);
            var values = Values();
            values.PreviousVersion = "1.2026.10.1";

            var text = SetupPrompts.Compose(prompt, values);

            Assert.That(text, Does.Contain("1.2026.10.1"));
            Assert.That(text, Does.Contain("1.2026.11.1"));
        }

        [Test]
        public void ClientConfig_HoldsTheEndpointAndToken_ForEachClient()
        {
            var config = ClientConfig.Compose(Endpoint, Token);

            Assert.That(config, Does.Contain($"Endpoint: {Endpoint}"));
            Assert.That(config, Does.Contain($"Bearer token: {Token}"));
            Assert.That(config, Does.Contain($"claude mcp add --transport http d365-agent-companion {Endpoint} --header \"Authorization: Bearer {Token}\""));
            Assert.That(config, Does.Contain($"\"url\": \"{Endpoint}\""));
        }

        private static SetupPrompt Parse(string name) => (SetupPrompt)Enum.Parse(typeof(SetupPrompt), name);

        private static PromptValues Values() => new PromptValues
        {
            PluginVersion = "1.2026.11.1",
            Endpoint = Endpoint,
            ClientConfig = ClientConfig.Compose(Endpoint, Token),
            SkillNames = new[] { "agent-companion-context", "agent-companion-wip-shaping" },
        };
    }
}
