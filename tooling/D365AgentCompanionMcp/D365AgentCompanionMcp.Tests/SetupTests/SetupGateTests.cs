using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Setup;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.SetupTests
{
    /// <summary>
    /// SPEC §8.7: a first run gets the wizard, a version change the Plugin updated page, and setup records the
    /// plugin version it was completed for — nothing else.
    /// </summary>
    [TestFixture]
    public class SetupGateTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void NoVersionRecorded_IsAFirstRun(string? recorded) =>
            Assert.That(SetupGate.Required(recorded, "1.2026.11.1"), Is.EqualTo(SetupView.FirstRun));

        [Test]
        public void SameVersion_ShowsTheMainPanel() =>
            Assert.That(SetupGate.Required("1.2026.11.1", "1.2026.11.1"), Is.EqualTo(SetupView.None));

        [TestCase("1.2026.10.1")]
        [TestCase("1.2026.12.1")]
        public void DifferentVersion_IsAnUpdate_EvenADowngrade(string recorded) =>
            Assert.That(SetupGate.Required(recorded, "1.2026.11.1"), Is.EqualTo(SetupView.Updated));

        [Test]
        public void CompleteSetup_RecordsTheVersion_AndSavesIt()
        {
            var backing = new FakeSettingsStore();
            var settings = new HostSettingsStore(backing);
            Assert.That(settings.RequiredSetup("1.2026.11.1"), Is.EqualTo(SetupView.FirstRun));

            settings.CompleteSetup("1.2026.11.1");

            Assert.That(backing.Saved!.SetupCompletedVersion, Is.EqualTo("1.2026.11.1"));
            Assert.That(new HostSettingsStore(backing).RequiredSetup("1.2026.11.1"), Is.EqualTo(SetupView.None));
            Assert.That(new HostSettingsStore(backing).RequiredSetup("1.2026.11.2"), Is.EqualTo(SetupView.Updated));
        }

        [Test]
        public void CompleteSetup_LeavesTheOtherSettingsAlone()
        {
            var backing = new FakeSettingsStore { Saved = new HostSettings { Port = 47400, ProtectedToken = "protected:abc" } };
            var settings = new HostSettingsStore(backing);

            settings.CompleteSetup("1.2026.11.1");

            Assert.That(backing.Saved!.Port, Is.EqualTo(47400));
            Assert.That(backing.Saved.ProtectedToken, Is.EqualTo("protected:abc"));
        }
    }
}
