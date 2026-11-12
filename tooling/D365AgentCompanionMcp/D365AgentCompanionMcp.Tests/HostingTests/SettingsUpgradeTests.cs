using System;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.HostingTests
{
    /// <summary>How the settings file is carried between schema versions (SPEC §7, Saved settings).</summary>
    [TestFixture]
    public class SettingsUpgradeTests
    {
        private static readonly Guid Org = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

        private static HostSettings V1() => new HostSettings
        {
            Port = 50123,
            MaxPageSize = 100,
            ProtectedToken = "protected:abc",
            SetupCompletedVersion = "1.2026.10.1",
            Grants =
            {
                new PersistedGrant { Kind = "ReadApproval", Scope = "Environment", OrganizationId = Org, OrganizationName = "Dev" },
                new PersistedGrant { Kind = "ReadApproval", Scope = "Always", OrganizationName = "All organizations" },
                new PersistedGrant { Kind = "DevEnvironment", Scope = "Always", OrganizationName = "All organizations" },
                new PersistedGrant { Kind = "DevEnvironment", Scope = "Environment", OrganizationId = Org, OrganizationName = "Dev" },
            },
        };

        [Test]
        public void AFileWithoutASchemaVersion_IsVersionZero()
        {
            Assert.That(V1().SchemaVersion, Is.Zero);
        }

        [Test]
        public void Upgrade_KeepsReadGrants_DropsTheAlwaysDevEnvironmentConfirmation_AndWritesVersionOne()
        {
            var settings = new FakeSettingsStore { Saved = V1() };

            var store = new HostSettingsStore(settings);

            var grants = store.Read(s => s.Grants.Select(g => (g.Kind, g.Scope)).ToList());
            Assert.That(grants, Is.EquivalentTo(new[]
            {
                ("ReadApproval", "Environment"),
                ("ReadApproval", "Always"),
                ("DevEnvironment", "Environment"),
            }));
            Assert.That(store.Read(s => s.SchemaVersion), Is.EqualTo(HostSettings.CurrentSchemaVersion));
            Assert.That(settings.Saved!.SchemaVersion, Is.EqualTo(HostSettings.CurrentSchemaVersion), "the upgrade is saved");
        }

        [Test]
        public void Upgrade_NeverTouchesTheTokenPortLimitsOrSetupVersion()
        {
            var store = new HostSettingsStore(new FakeSettingsStore { Saved = V1() });

            Assert.That(store.Read(s => (s.Port, s.MaxPageSize, s.ProtectedToken, s.SetupCompletedVersion)), Is.EqualTo((50123, 100, "protected:abc", "1.2026.10.1")));
        }

        [Test]
        public void Upgrade_LeavesFileRootsAlone()
        {
            var saved = V1();
            saved.SchemaVersion = 0;
            saved.FileRoots.Add(new PersistedFileRoot { Scope = "Tenant", TenantId = Guid.NewGuid(), Path = @"C:\Repos\a" });

            var store = new HostSettingsStore(new FakeSettingsStore { Saved = saved });

            Assert.That(store.Read(s => s.FileRoots.Count), Is.EqualTo(1));
        }

        [Test]
        public void AUpgradedFile_LoadsTheV1GrantsWithoutEnvironmentOrTenantIds()
        {
            var store = new ApprovalStore(new HostSettingsStore(new FakeSettingsStore { Saved = V1() }), new FakeClock());

            var grant = store.List().Single(g => g.Kind == ApprovalKind.ReadApproval && g.Scope == ApprovalScope.Environment);
            Assert.That((grant.EnvironmentId, grant.TenantId), Is.EqualTo((string.Empty, Guid.Empty)));
            Assert.That(store.Find(ApprovalKind.ReadApproval, new ConnectionIdentity(Org, "e1", Guid.NewGuid(), "Dev")), Is.Not.Null);
        }

        [Test]
        public void ACurrentFile_IsNotRewrittenOnLoad()
        {
            var current = V1();
            current.SchemaVersion = HostSettings.CurrentSchemaVersion;
            var settings = new FakeSettingsStore { Saved = current };

            _ = new HostSettingsStore(settings);

            Assert.That(settings.SaveCount, Is.Zero);
        }

        [Test]
        public void AFreshInstall_StartsAtTheCurrentVersion_WithoutSaving()
        {
            var settings = new FakeSettingsStore();

            var store = new HostSettingsStore(settings);

            Assert.That(store.Read(s => s.SchemaVersion), Is.EqualTo(HostSettings.CurrentSchemaVersion));
            Assert.That(settings.SaveCount, Is.Zero);
        }

        [Test]
        public void SettingsRoundTripThroughTheXmlSerializer_WithGrantIdsAndFileRoots()
        {
            var settings = new FakeSettingsStore();
            var store = new HostSettingsStore(settings);
            var tenant = Guid.NewGuid();
            new ApprovalStore(store, new FakeClock()).Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, new ConnectionIdentity(Org, "e1", tenant, "Dev"), "tab");
            store.Update(s => s.FileRoots.Add(new PersistedFileRoot { Scope = "Tenant", TenantId = tenant, Name = "Dev", Path = @"C:\Repos\a" }));

            var reloaded = new HostSettingsStore(settings);

            Assert.That(reloaded.Read(s => s.Grants.Single().TenantId), Is.EqualTo(tenant));
            Assert.That(reloaded.Read(s => s.FileRoots.Single().Path), Is.EqualTo(@"C:\Repos\a"));
        }
    }
}
