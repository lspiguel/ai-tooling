using System;
using System.Collections.Generic;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Files;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ApprovalsTests
{
    /// <summary>The Active Approvals tree: its fixed order and what it lists (SPEC §8.8).</summary>
    [TestFixture]
    public class ApprovalTreeTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Guid TenantX = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid TenantY = new Guid("11111111-0000-0000-0000-0000000000a2");

        private static Grant G(ApprovalKind kind, ApprovalScope scope, string name, Guid tenant, string environment = "", Guid? org = null) =>
            new Grant(kind, scope, scope == ApprovalScope.Always || scope == ApprovalScope.Tenant ? Guid.Empty : org ?? Guid.NewGuid(), name, Now, "tab", environment, tenant);

        private static ApprovalTree Build(IReadOnlyList<Grant> grants, IReadOnlyList<FileRoot>? roots = null, IReadOnlyList<ConnectionIdentity>? bound = null) =>
            ApprovalTree.Build(grants, roots ?? new FileRoot[0], bound ?? new ConnectionIdentity[0]);

        [Test]
        public void Global_ComesFirstInKindOrder_AndAnEmptyGlobalIsEmpty()
        {
            var tree = Build(new[]
            {
                G(ApprovalKind.RecordRead, ApprovalScope.Always, "all", Guid.Empty),
                G(ApprovalKind.ReadApproval, ApprovalScope.Always, "all", Guid.Empty),
            });

            Assert.That(tree.Global.Select(g => g.Kind), Is.EqualTo(new[] { ApprovalKind.ReadApproval, ApprovalKind.RecordRead }));
            Assert.That(Build(new Grant[0]).Global, Is.Empty);
        }

        [Test]
        public void Environments_AreAlphabetical_AndTenantsFollowTheirFirstEnvironment()
        {
            var tree = Build(new[]
            {
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Zulu DEV", TenantX, "e1"),
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Alpha UAT", TenantX, "e2"),
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Beta DEV", TenantY, "e3"),
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Mike DEV", TenantY, "e4"),
            });

            Assert.That(tree.Tenants.Select(t => t.TenantId), Is.EqualTo(new[] { TenantX, TenantY }), "TenantX starts with Alpha, TenantY with Beta");
            Assert.That(tree.Tenants[0].Environments.Select(e => e.Name), Is.EqualTo(new[] { "Alpha UAT", "Zulu DEV" }));
            Assert.That(tree.Tenants[1].Environments.Select(e => e.Name), Is.EqualTo(new[] { "Beta DEV", "Mike DEV" }));
        }

        [Test]
        public void TenantsThenTheirEnvironments_ThenNoTenantLast()
        {
            var tree = Build(new[]
            {
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Aardvark", Guid.Empty, "e0"),
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Zulu", TenantX, "e1"),
            });

            Assert.That(tree.Tenants.Select(t => t.TenantId), Is.EqualTo(new[] { TenantX }));
            Assert.That(tree.NoTenant, Is.Not.Null);
            Assert.That(tree.NoTenant!.Environments.Single().Name, Is.EqualTo("Aardvark"));
            Assert.That(tree.NoTenant.Label, Is.EqualTo("No tenant"));
        }

        [Test]
        public void AnEnvironmentLists_PerEnvironmentGrantsThenSessionGrants_EachInKindOrder()
        {
            var org = Guid.NewGuid();
            var tree = Build(new[]
            {
                G(ApprovalKind.DevEnvironment, ApprovalScope.Session, "Dev", TenantX, "e1", org),
                G(ApprovalKind.Write, ApprovalScope.Environment, "Dev", TenantX, "e1", org),
                G(ApprovalKind.RecordRead, ApprovalScope.Session, "Dev", TenantX, "e1", org),
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Dev", TenantX, "e1", org),
            });

            var grants = tree.Tenants.Single().Environments.Single().Grants;
            Assert.That(
                grants.Select(g => (g.Scope, g.Kind)),
                Is.EqualTo(new[]
                {
                    (ApprovalScope.Environment, ApprovalKind.ReadApproval),
                    (ApprovalScope.Environment, ApprovalKind.Write),
                    (ApprovalScope.Session, ApprovalKind.RecordRead),
                    (ApprovalScope.Session, ApprovalKind.DevEnvironment),
                }));
        }

        [Test]
        public void TenantGrants_SitUnderTheirTenant_BeforeItsEnvironments()
        {
            var tree = Build(new[]
            {
                G(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "Alpha DEV", TenantX),
                G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Alpha DEV", TenantX, "e1"),
            });

            var tenant = tree.Tenants.Single();
            Assert.That(tenant.Grants.Single().Scope, Is.EqualTo(ApprovalScope.Tenant));
            Assert.That(tenant.Environments, Has.Count.EqualTo(1));
            Assert.That(tenant.AllGrants, Has.Count.EqualTo(2), "a reset of a tenant covers its environments");
        }

        [Test]
        public void ATenantWithOnlyAFileRoot_IsListed_AndShowsIt()
        {
            var root = new FileRoot(ApprovalScope.Tenant, Guid.Empty, null, TenantX, "Alpha DEV", @"C:\Repos\a");

            var tree = Build(new Grant[0], new[] { root });

            Assert.That(tree.Tenants.Single().FileRoot, Is.SameAs(root));
        }

        [Test]
        public void AnEnvironmentFileRoot_ShowsUnderItsEnvironment_AndNotUnderTenantsWithout()
        {
            var org = Guid.NewGuid();
            var root = new FileRoot(ApprovalScope.Environment, org, "e1", TenantX, "Alpha SBX", @"C:\Repos\poc");

            var tree = Build(new Grant[0], new[] { root });

            var environment = tree.Tenants.Single().Environments.Single();
            Assert.That(environment.OwnRoot, Is.SameAs(root));
            Assert.That(environment.ShowFileRootLine, Is.True);
        }

        [Test]
        public void ABoundEnvironmentNoFileRootCovers_IsListed_WithNotSet()
        {
            var bound = new ConnectionIdentity(Guid.NewGuid(), "e9", Guid.Empty, "Gamma TRN");

            var tree = Build(new Grant[0], null, new[] { bound });

            var environment = tree.NoTenant!.Environments.Single();
            Assert.That(environment.Name, Is.EqualTo("Gamma TRN"));
            Assert.That(environment.ShowFileRootLine, Is.True, "under No tenant the File root line always shows");
            Assert.That(environment.OwnRoot, Is.Null);
        }

        [Test]
        public void ABoundEnvironmentACoveredByATenantFileRoot_IsNotListedForIt()
        {
            var bound = new ConnectionIdentity(Guid.NewGuid(), "e9", TenantX, "Gamma DEV");
            var root = new FileRoot(ApprovalScope.Tenant, Guid.Empty, null, TenantX, "Gamma DEV", @"C:\Repos\g");

            var tree = Build(new Grant[0], new[] { root }, new[] { bound });

            Assert.That(tree.Tenants.Single().Environments, Is.Empty);
        }

        [Test]
        public void AGrantFromAV1File_IsListedUnderNoTenant_UntilItsIdsAreFilledIn()
        {
            var org = Guid.NewGuid();
            var v1 = G(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Old", Guid.Empty, string.Empty, org);
            Assert.That(Build(new[] { v1 }).NoTenant!.Environments.Single().Grants, Has.Count.EqualTo(1));

            var filled = v1.WithIdentity(new ConnectionIdentity(org, "e1", TenantX, "Old"));
            var tree = Build(new[] { filled });

            Assert.That(tree.NoTenant, Is.Null);
            Assert.That(tree.Tenants.Single().Environments.Single().Grants, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheLabelShortensTheTenantId()
        {
            var tree = Build(new[] { G(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "x", TenantY) });

            Assert.That(tree.Tenants.Single().Label, Is.EqualTo("Tenant 11111111…"));
        }
    }
}
