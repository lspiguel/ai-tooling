using System;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ApprovalsTests
{
    /// <summary>Read-approval and dev-confirmation scopes, persistence, revoke and per-organization isolation (SPEC §7, C8).</summary>
    [TestFixture]
    public class ApprovalStoreTests
    {
        private static readonly Guid OrgA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid OrgB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");

        private FakeSettingsStore _settings = null!;
        private ApprovalStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = new FakeSettingsStore();
            _store = new ApprovalStore(new HostSettingsStore(_settings), new FakeClock());
        }

        [Test]
        public void Grant_CoversItsOrganizationOnly()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, OrgA, "Org A", "tab 1");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgA)?.Scope, Is.EqualTo(ApprovalScope.Session));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgB), Is.Null);
            Assert.That(_store.Find(ApprovalKind.DevEnvironment, OrgA), Is.Null, "a read approval is not a dev confirmation");
        }

        [Test]
        public void Grant_ReplacesTheOrganizationsEarlierGrantOfTheSameKind()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, OrgA, "Org A", "tab 1");
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgA, "Org A", "tab 1");

            Assert.That(_store.List().Count(g => g.Kind == ApprovalKind.ReadApproval && g.OrganizationId == OrgA), Is.EqualTo(1));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgA)?.Scope, Is.EqualTo(ApprovalScope.Environment));
        }

        [Test]
        public void Grant_RecordsWhatPhase6Lists()
        {
            _store.GrantForOrganization(ApprovalKind.DevEnvironment, ApprovalScope.Environment, OrgA, "Org A", "tab 3");

            var grant = _store.List().Single();
            Assert.That((grant.Kind, grant.Scope, grant.OrganizationId, grant.OrganizationName, grant.OriginTab), Is.EqualTo((ApprovalKind.DevEnvironment, ApprovalScope.Environment, OrgA, "Org A", "tab 3")));
            Assert.That(grant.GrantedOnUtc, Is.EqualTo(new FakeClock().UtcNow));
        }

        [Test]
        public void Grant_OnceOrAlways_IsRefused()
        {
            Assert.That(() => _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Once, OrgA, "Org A", "tab"), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Always, OrgA, "Org A", "tab"), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Always_CoversEveryOrganizationIncludingLaterOnes()
        {
            _store.SetAlways(ApprovalKind.ReadApproval, true, "settings");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgA)?.Scope, Is.EqualTo(ApprovalScope.Always));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, Guid.NewGuid())?.Scope, Is.EqualTo(ApprovalScope.Always));
            Assert.That(_store.IsAlways(ApprovalKind.ReadApproval), Is.True);

            _store.SetAlways(ApprovalKind.ReadApproval, false, "settings");
            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgA), Is.Null);
        }

        [Test]
        public void Persistence_KeepsEnvironmentAndAlways_DropsSession()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, OrgA, "Org A", "tab 1");
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgB, "Org B", "tab 2");
            _store.SetAlways(ApprovalKind.DevEnvironment, true, "settings");

            var reloaded = new ApprovalStore(new HostSettingsStore(_settings), new FakeClock());

            Assert.That(reloaded.Find(ApprovalKind.ReadApproval, OrgA), Is.Null);
            Assert.That(reloaded.Find(ApprovalKind.ReadApproval, OrgB)?.Scope, Is.EqualTo(ApprovalScope.Environment));
            Assert.That(reloaded.IsAlways(ApprovalKind.DevEnvironment), Is.True);
        }

        [Test]
        public void Persistence_StoresOnlyIdsNamesAndScopes()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgA, "Org A", "tab 1");

            var persisted = _settings.Saved!.Grants.Single();
            Assert.That((persisted.Kind, persisted.Scope, persisted.OrganizationId, persisted.OrganizationName), Is.EqualTo(("ReadApproval", "Environment", OrgA, "Org A")));
        }

        [Test]
        public void SessionOnlyChange_DoesNotRewriteSettings()
        {
            var saves = _settings.SaveCount;

            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, OrgA, "Org A", "tab 1");

            Assert.That(_settings.SaveCount, Is.EqualTo(saves));
        }

        [Test]
        public void RevokeOrganization_ClearsSessionAndEnvironment_LeavesOtherOrganizationsAndAlways()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgA, "Org A", "tab 1");
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgB, "Org B", "tab 2");
            _store.GrantForOrganization(ApprovalKind.DevEnvironment, ApprovalScope.Environment, OrgA, "Org A", "tab 1");
            _store.SetAlways(ApprovalKind.DevEnvironment, true, "settings");

            _store.RevokeOrganization(ApprovalKind.ReadApproval, OrgA);

            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgA), Is.Null);
            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgB), Is.Not.Null);
            Assert.That(_store.List().Any(g => g.Kind == ApprovalKind.DevEnvironment && g.OrganizationId == OrgA), Is.True);
            Assert.That(_store.IsAlways(ApprovalKind.DevEnvironment), Is.True);
            Assert.That(_settings.Saved!.Grants.Any(g => g.Kind == "ReadApproval" && g.OrganizationId == OrgA), Is.False);
        }

        [Test]
        public void EndSession_RemovesOnlySessionGrants()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, OrgA, "Org A", "tab 1");
            _store.GrantForOrganization(ApprovalKind.DevEnvironment, ApprovalScope.Session, OrgA, "Org A", "tab 1");
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgB, "Org B", "tab 2");

            _store.EndSession();

            Assert.That(_store.List().Select(g => (g.Kind, g.Scope, g.OrganizationId)), Is.EqualTo(new[] { (ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgB) }));
        }

        [Test]
        public void Remove_And_ClearScope_ActOnTheOneStore()
        {
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgA, "Org A", "tab 1");
            _store.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgB, "Org B", "tab 2");

            _store.Remove(_store.List().First(g => g.OrganizationId == OrgA));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, OrgA), Is.Null);

            _store.ClearScope(ApprovalScope.Environment);
            Assert.That(_store.List(), Is.Empty);
        }
    }
}
