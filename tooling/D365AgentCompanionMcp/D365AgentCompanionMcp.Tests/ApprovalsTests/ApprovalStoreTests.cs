using System;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ApprovalsTests
{
    /// <summary>Grant kinds, scopes, identity matching, persistence, revoke and reset (SPEC §7, Phase 6 — scopes and grants).</summary>
    [TestFixture]
    public class ApprovalStoreTests
    {
        private static readonly Guid OrgA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid OrgB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
        private static readonly Guid OrgC = new Guid("cccccccc-0000-0000-0000-000000000003");
        private static readonly Guid Tenant1 = new Guid("11111111-0000-0000-0000-000000000001");
        private static readonly Guid Tenant2 = new Guid("22222222-0000-0000-0000-000000000002");

        private static readonly ConnectionIdentity A = new ConnectionIdentity(OrgA, "env-a", Tenant1, "Org A");
        private static readonly ConnectionIdentity B = new ConnectionIdentity(OrgB, "env-b", Tenant1, "Org B");
        private static readonly ConnectionIdentity C = new ConnectionIdentity(OrgC, "env-c", Tenant2, "Org C");
        private static readonly ConnectionIdentity NoTenant = new ConnectionIdentity(Guid.NewGuid(), null, Guid.Empty, "No tenant");

        private FakeSettingsStore _settings = null!;
        private ApprovalStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = new FakeSettingsStore();
            _store = new ApprovalStore(new HostSettingsStore(_settings), new FakeClock());
        }

        // ---- reach ----

        [Test]
        public void EnvironmentGrant_CoversItsEnvironmentOnly_AndOnlyItsKind()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab 1");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A)?.Scope, Is.EqualTo(ApprovalScope.Session));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, B), Is.Null);
            Assert.That(_store.Find(ApprovalKind.DevEnvironment, A), Is.Null, "a read approval is not a dev confirmation");
            Assert.That(_store.Find(ApprovalKind.RecordRead, A), Is.Null, "a read approval does not cover record reads");
            Assert.That(_store.Find(ApprovalKind.Write, A), Is.Null, "a read approval does not cover writes");
        }

        [Test]
        public void TenantGrant_CoversEveryEnvironmentOfTheTenant_IncludingOnesBoundLater_NotAnotherTenant()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, A, "tab 1");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A)?.Scope, Is.EqualTo(ApprovalScope.Tenant));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, B)?.Scope, Is.EqualTo(ApprovalScope.Tenant), "bound afterwards, same tenant");
            Assert.That(_store.Find(ApprovalKind.ReadApproval, C), Is.Null, "another tenant");
            Assert.That(_store.Find(ApprovalKind.ReadApproval, NoTenant), Is.Null, "a connection that reports no tenant ID gets no tenant grants");
        }

        [Test]
        public void TenantGrant_NeedsATenantId()
        {
            Assert.That(() => _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, NoTenant, "tab"), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void GlobalGrant_CoversEveryEnvironmentInEveryTenant()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, A, "tab 1");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, C)?.Scope, Is.EqualTo(ApprovalScope.Always));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, NoTenant)?.Scope, Is.EqualTo(ApprovalScope.Always));
            Assert.That(_store.IsGlobal(ApprovalKind.ReadApproval), Is.True);
            var grant = _store.List().Single();
            Assert.That((grant.OrganizationId, grant.EnvironmentId, grant.TenantId), Is.EqualTo((Guid.Empty, string.Empty, Guid.Empty)), "global grants hold no IDs");
        }

        [Test]
        public void Find_ReturnsTheBroadestGrant()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, A, "tab 1");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A)?.Scope, Is.EqualTo(ApprovalScope.Tenant));
        }

        [TestCase("Write", "Tenant")]
        [TestCase("Write", "Always")]
        [TestCase("DevEnvironment", "Tenant")]
        [TestCase("DevEnvironment", "Always")]
        [TestCase("ReadApproval", "Once")]
        [TestCase("RecordRead", "Once")]
        public void Allow_RefusesAScopeTheKindDoesNotAllow(string kind, string scope)
        {
            Assert.That(() => _store.Allow((ApprovalKind)Enum.Parse(typeof(ApprovalKind), kind), (ApprovalScope)Enum.Parse(typeof(ApprovalScope), scope), A, "tab"), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Allow_ReplacesTheEnvironmentsEarlierGrantOfTheSameKind()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, A, "tab 1");

            Assert.That(_store.List().Count(g => g.Kind == ApprovalKind.ReadApproval && g.OrganizationId == OrgA), Is.EqualTo(1));
            Assert.That(_store.Find(ApprovalKind.ReadApproval, A)?.Scope, Is.EqualTo(ApprovalScope.Environment));
        }

        [Test]
        public void Allow_RecordsWhatTheWindowLists()
        {
            _store.Allow(ApprovalKind.Write, ApprovalScope.Environment, A, "tab 3");

            var grant = _store.List().Single();
            Assert.That(
                (grant.Kind, grant.Scope, grant.OrganizationId, grant.EnvironmentId, grant.TenantId, grant.OrganizationName, grant.OriginTab),
                Is.EqualTo((ApprovalKind.Write, ApprovalScope.Environment, OrgA, "env-a", Tenant1, "Org A", "tab 3")));
            Assert.That(grant.GrantedOnUtc, Is.EqualTo(new FakeClock().UtcNow));
        }

        // ---- identity ----

        [Test]
        public void V1Grant_KnowsOnlyItsOrganization_AndApplies()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(OrgA, null, Guid.Empty, "Org A"), "tab");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A), Is.Not.Null, "the organization ID is enough to apply a grant");
        }

        [Test]
        public void FillIdentity_AddsTheIdsAConnectionReports_AndSaves()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(OrgA, null, Guid.Empty, "Org A"), "tab");

            _store.FillIdentity(A);

            var grant = _store.List().Single();
            Assert.That((grant.EnvironmentId, grant.TenantId), Is.EqualTo(("env-a", Tenant1)));
            Assert.That((_settings.Saved!.Grants.Single().EnvironmentId, _settings.Saved.Grants.Single().TenantId), Is.EqualTo(("env-a", Tenant1)));
        }

        [Test]
        public void FillIdentity_LeavesAGrantOfAnotherOrganizationAlone()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(OrgB, null, Guid.Empty, "Org B"), "tab");

            _store.FillIdentity(A);

            Assert.That(_store.List().Single().EnvironmentId, Is.Empty);
        }

        [Test]
        public void ADifferentEnvironmentId_MakesAGrantStale_AndItIsNotApplied()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, A, "tab");
            var copied = new ConnectionIdentity(OrgA, "env-a-restored", Tenant1, "Org A");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, copied), Is.Null);
            Assert.That(_store.Stale(ApprovalKind.ReadApproval, copied), Has.Count.EqualTo(1));
            Assert.That(_store.Stale(ApprovalKind.ReadApproval, A), Is.Empty);
        }

        [Test]
        public void ADifferentTenantId_MakesAGrantStale()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab");
            var moved = new ConnectionIdentity(OrgA, "env-a", Tenant2, "Org A");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, moved), Is.Null);
            Assert.That(_store.Stale(ApprovalKind.ReadApproval, moved), Has.Count.EqualTo(1));
        }

        [Test]
        public void EnvironmentIds_AreComparedWithoutRegardToCase()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(OrgA, "ENV-A", Tenant1, "Org A"), "tab");

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A), Is.Not.Null);
        }

        // ---- persistence ----

        [Test]
        public void Persistence_KeepsEnvironmentTenantAndGlobal_DropsSession()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, B, "tab 2");
            _store.Allow(ApprovalKind.RecordRead, ApprovalScope.Tenant, C, "tab 3");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, A, "tab 4");

            var reloaded = new ApprovalStore(new HostSettingsStore(_settings), new FakeClock());

            Assert.That(reloaded.List().Select(g => (g.Kind, g.Scope)), Is.EquivalentTo(new[]
            {
                (ApprovalKind.ReadApproval, ApprovalScope.Environment),
                (ApprovalKind.RecordRead, ApprovalScope.Tenant),
                (ApprovalKind.ReadApproval, ApprovalScope.Always),
            }));
        }

        [Test]
        public void Persistence_StoresOnlyIdsNamesAndScopes()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, A, "tab 1");

            var persisted = _settings.Saved!.Grants.Single();
            Assert.That(
                (persisted.Kind, persisted.Scope, persisted.OrganizationId, persisted.EnvironmentId, persisted.TenantId, persisted.OrganizationName),
                Is.EqualTo(("ReadApproval", "Environment", OrgA, "env-a", Tenant1, "Org A")));
        }

        [Test]
        public void SessionOnlyChange_DoesNotRewriteSettings()
        {
            var saves = _settings.SaveCount;

            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab 1");

            Assert.That(_settings.SaveCount, Is.EqualTo(saves));
        }

        [Test]
        public void Load_DropsAGrantWhoseKindDoesNotAllowItsScope()
        {
            var settings = new FakeSettingsStore
            {
                Saved = new HostSettings
                {
                    SchemaVersion = HostSettings.CurrentSchemaVersion,
                    Grants =
                    {
                        new PersistedGrant { Kind = "Write", Scope = "Tenant", TenantId = Tenant1, OrganizationName = "x" },
                        new PersistedGrant { Kind = "Write", Scope = "Environment", OrganizationId = OrgA, OrganizationName = "x" },
                        new PersistedGrant { Kind = "ReadApproval", Scope = "Tenant", OrganizationName = "x" },
                    },
                },
            };

            var store = new ApprovalStore(new HostSettingsStore(settings), new FakeClock());

            Assert.That(store.List().Select(g => (g.Kind, g.Scope)), Is.EqualTo(new[] { (ApprovalKind.Write, ApprovalScope.Environment) }),
                "a Write grant stops at one environment, and a tenant grant needs its tenant");
        }

        // ---- revoke and reset ----

        [Test]
        public void RevokeEnvironment_ClearsThatEnvironmentsGrantsOfOneKind_LeavesOthers()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, B, "tab 2");
            _store.Allow(ApprovalKind.DevEnvironment, ApprovalScope.Environment, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, C, "tab 3");

            _store.RevokeEnvironment(ApprovalKind.ReadApproval, A);

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A), Is.Null);
            Assert.That(_store.Find(ApprovalKind.ReadApproval, B), Is.Not.Null);
            Assert.That(_store.Find(ApprovalKind.DevEnvironment, A), Is.Not.Null);
            Assert.That(_store.Find(ApprovalKind.ReadApproval, C), Is.Not.Null, "a tab's revoke leaves broader grants to the Active Approvals window");
            Assert.That(_settings.Saved!.Grants.Any(g => g.Kind == "ReadApproval" && g.OrganizationId == OrgA), Is.False);
        }

        [Test]
        public void EndSession_RemovesOnlySessionGrants()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab 1");
            _store.Allow(ApprovalKind.DevEnvironment, ApprovalScope.Session, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, B, "tab 2");

            _store.EndSession();

            Assert.That(_store.List().Select(g => (g.Kind, g.Scope, g.OrganizationId)), Is.EqualTo(new[] { (ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgB) }));
        }

        [Test]
        public void ClearGlobal_RevokesOnlyGlobalGrants()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, A, "tab");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, A, "tab");

            _store.ClearGlobal();

            Assert.That(_store.List().Select(g => g.Scope), Is.EqualTo(new[] { ApprovalScope.Tenant }));
        }

        [Test]
        public void ClearTenant_RevokesTheTenantsGrantsAndThoseOfItsEnvironments_NotAnotherTenantsOrGlobal()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, A, "tab");
            _store.Allow(ApprovalKind.Write, ApprovalScope.Environment, A, "tab");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, B, "tab");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, C, "tab");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, A, "tab");

            _store.ClearTenant(Tenant1);

            Assert.That(_store.List().Select(g => (g.Scope, g.OrganizationId)), Is.EquivalentTo(new[] { (ApprovalScope.Environment, OrgC), (ApprovalScope.Always, Guid.Empty) }));
        }

        [Test]
        public void ClearEnvironment_RevokesThatEnvironmentsEnvironmentAndSessionGrants_NotItsTenants()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, A, "tab");
            _store.Allow(ApprovalKind.Write, ApprovalScope.Environment, A, "tab");
            _store.Allow(ApprovalKind.RecordRead, ApprovalScope.Session, A, "tab");
            _store.Allow(ApprovalKind.Write, ApprovalScope.Environment, B, "tab");

            _store.ClearEnvironment(_store.List().First(g => g.OrganizationId == OrgA).EnvironmentKey);

            Assert.That(_store.List().Select(g => (g.Kind, g.Scope, g.OrganizationId)), Is.EquivalentTo(new[] { (ApprovalKind.ReadApproval, ApprovalScope.Tenant, Guid.Empty), (ApprovalKind.Write, ApprovalScope.Environment, OrgB) }));
        }

        [Test]
        public void ClearAll_RevokesEverything_AndSaves()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, A, "tab");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, A, "tab");
            _store.Allow(ApprovalKind.Write, ApprovalScope.Session, A, "tab");

            _store.ClearAll();

            Assert.That(_store.List(), Is.Empty);
            Assert.That(_settings.Saved!.Grants, Is.Empty);
        }

        [Test]
        public void Remove_TakesOneGrant()
        {
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, A, "tab 1");
            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, B, "tab 2");

            _store.Remove(_store.List().First(g => g.OrganizationId == OrgA));

            Assert.That(_store.Find(ApprovalKind.ReadApproval, A), Is.Null);
            Assert.That(_store.Find(ApprovalKind.ReadApproval, B), Is.Not.Null);
        }

        [Test]
        public void Changed_IsRaisedForARealChangeOnly()
        {
            var raised = 0;
            _store.Changed += (s, e) => raised++;

            _store.ClearAll();
            Assert.That(raised, Is.Zero);

            _store.Allow(ApprovalKind.ReadApproval, ApprovalScope.Session, A, "tab");
            Assert.That(raised, Is.EqualTo(1));
        }
    }
}
