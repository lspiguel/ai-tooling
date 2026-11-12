using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>How the dispatcher applies scoped grants: tenant and global reach, Write grants, and what no grant can skip (SPEC §7, Phase 6).</summary>
    [TestFixture]
    public class ScopedApprovalTests
    {
        private const string ConnA = "aaaaaaaa-1111-1111-1111-111111111111";
        private const string ConnB = "bbbbbbbb-2222-2222-2222-222222222222";
        private const string ConnC = "cccccccc-3333-3333-3333-333333333333";
        private const string FakeWrite = "fake_write";
        private static readonly Guid OrgA = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");
        private static readonly Guid OrgB = new Guid("bbbbbbbb-0000-0000-0000-00000000000b");
        private static readonly Guid OrgC = new Guid("cccccccc-0000-0000-0000-00000000000c");
        private static readonly Guid Tenant1 = new Guid("11111111-0000-0000-0000-000000000001");
        private static readonly Guid Tenant2 = new Guid("22222222-0000-0000-0000-000000000002");

        private CompanionHost _host = null!;
        private FakeInstance _tabA = null!;
        private FakeInstance _tabB = null!;
        private FakeInstance _tabC = null!;
        private BoundConnection _a = null!;
        private BoundConnection _b = null!;
        private BoundConnection _c = null!;

        [SetUp]
        public void SetUp()
        {
            _host = TestHost.Create(new FakeClock(), new FakeSettingsStore(), WithFakeWrite);
            _tabA = new FakeInstance("tab A");
            _tabB = new FakeInstance("tab B");
            _tabC = new FakeInstance("tab C");
            _host.Attach(_tabA);
            _host.Attach(_tabB);
            _host.Attach(_tabC);
            _a = TestHost.Bind(_host, _tabA, OrgA, ConnA, Dataverse.Mock(OrgA, Guid.NewGuid()).Object, "Dev A", "env-a", Tenant1);
            _b = TestHost.Bind(_host, _tabB, OrgB, ConnB, Dataverse.Mock(OrgB, Guid.NewGuid()).Object, "Test B", "env-b", Tenant1);
            _c = TestHost.Bind(_host, _tabC, OrgC, ConnC, Dataverse.Mock(OrgC, Guid.NewGuid()).Object, "Other C", "env-c", Tenant2);
        }

        // ---- what a dialog offers ----

        [Test]
        public async Task ARead_OffersEveryScope_WhenTheConnectionReportsATenant()
        {
            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            var request = _tabA.Requests.Single();
            Assert.That(
                request.Options,
                Is.EqualTo(new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment, ApprovalDecision.AllowTenant, ApprovalDecision.AllowGlobal }));
            Assert.That(request.TenantDisabledReason, Is.Null);
            Assert.That(request.Kind, Is.EqualTo(ApprovalKind.ReadApproval));
        }

        [Test]
        public async Task APerTenantChoice_IsUnavailable_WhenTheConnectionReportsNoTenant()
        {
            var tab = new FakeInstance("tab D");
            _host.Attach(tab);
            TestHost.Bind(_host, tab, Guid.NewGuid(), "dddddddd-4444-4444-4444-444444444444", Dataverse.Mock(Guid.NewGuid(), Guid.NewGuid()).Object, "No Tenant");

            await _host.Dispatcher.CallAsync(new McpToolCall(ConnectionTools.WhoAmI, Args("dddddddd-4444-4444-4444-444444444444"), DateTime.UtcNow), default);

            var request = tab.Requests.Single();
            Assert.That(request.Options, Does.Not.Contain(ApprovalDecision.AllowTenant));
            Assert.That(request.TenantDisabledReason, Does.Contain("no tenant ID"));
        }

        [Test]
        public async Task AWrite_OffersOnceSessionAndEnvironmentOnly()
        {
            _a.DevConfirmedOnce = true;

            await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story." }));

            Assert.That(_tabA.Requests.Single().Options, Is.EqualTo(new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment }));
        }

        // ---- reach ----

        [Test]
        public async Task AllowTenant_CoversASecondEnvironmentOfTheTenant_NotTheOtherTenant()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowTenant);
            var first = await Call(ConnectionTools.WhoAmI, Args(ConnA));
            Assert.That(_tabA.CallLog.Snapshot().Single().Approval, Is.EqualTo("tenant"));

            await Call(ConnectionTools.WhoAmI, Args(ConnB));
            await Call(ConnectionTools.WhoAmI, Args(ConnC));

            Assert.That(first.IsError, Is.False);
            Assert.That(_tabB.Requests, Is.Empty, "the same tenant, covered without a dialog");
            Assert.That(_tabB.CallLog.Snapshot().Single().Approval, Is.EqualTo("tenant"));
            Assert.That(_tabC.Requests, Has.Count.EqualTo(1), "the other tenant asks");
        }

        [Test]
        public async Task AllowGlobal_CoversEveryEnvironment_AndIsLoggedAsGlobal()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowGlobal);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            await Call(ConnectionTools.WhoAmI, Args(ConnC));

            Assert.That(_tabC.Requests, Is.Empty);
            Assert.That(_tabC.CallLog.Snapshot().Single().Approval, Is.EqualTo("global"));
            Assert.That(_host.Approvals.IsGlobal(ApprovalKind.ReadApproval), Is.True);
        }

        [Test]
        public async Task ARevokedGrant_AppliesFromTheNextCall()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowEnvironment);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            Assert.That(_tabA.Requests, Has.Count.EqualTo(1));

            _host.Approvals.Remove(_host.Approvals.List().Single());
            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            Assert.That(_tabA.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task AStaleGrant_IsNotApplied()
        {
            _host.Approvals.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(OrgA, "env-a-restored", Tenant1, "Dev A"), "tab");

            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            Assert.That(_tabA.Requests, Has.Count.EqualTo(1), "the organization matches but the environment ID differs");
        }

        [Test]
        public void BindingFillsInTheIdsOfAV1Grant()
        {
            var tab = new FakeInstance("tab D");
            var org = Guid.NewGuid();
            _host.Approvals.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(org, null, Guid.Empty, "Old"), "tab");
            _host.Attach(tab);

            TestHost.Bind(_host, tab, org, "dddddddd-4444-4444-4444-444444444444", Dataverse.Mock(org, Guid.NewGuid()).Object, "Old", "env-d", Tenant2);

            var grant = _host.Approvals.List().Single(g => g.OrganizationId == org);
            Assert.That((grant.EnvironmentId, grant.TenantId), Is.EqualTo(("env-d", Tenant2)));
        }

        // ---- Write grants ----

        [Test]
        public async Task AllowEnvironmentOnAWrite_RunsTheNextWriteWithoutADialog_ButWithItsLogPair()
        {
            _a.DevConfirmedOnce = true;
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowEnvironment);
            await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "one", ["reason"] = "Story 1." }));

            var second = await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "two", ["reason"] = "Story 1." }));

            Assert.That(second.IsError, Is.False);
            Assert.That(_tabA.Requests, Has.Count.EqualTo(1));
            var log = _tabA.CallLog.Snapshot();
            Assert.That(log, Has.Count.EqualTo(2), "every call is logged (R13)");
            Assert.That(log[1].Approval, Is.EqualTo("environment"));
            Assert.That(log[1].CallLine, Does.Contain("reason \"Story 1.\""));
        }

        [Test]
        public async Task AWriteGrant_DoesNotSkipTheReason()
        {
            _a.DevConfirmedOnce = true;
            _host.Approvals.Allow(ApprovalKind.Write, ApprovalScope.Environment, _a.Identity, "tab");

            var result = await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x" }));

            Assert.That((string?)result.Payload["error"]!["code"], Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("reason"));
        }

        [Test]
        public async Task AWriteGrant_DoesNotSkipTheDevEnvironmentConfirmation()
        {
            _host.Approvals.Allow(ApprovalKind.Write, ApprovalScope.Environment, _a.Identity, "tab");

            var result = await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story." }));

            Assert.That((string?)result.Payload["error"]!["code"], Is.EqualTo(ToolErrorCodes.DevEnvironmentNotConfirmed));
        }

        [Test]
        public async Task AWriteGrant_ForOneEnvironment_NeverCoversAnother()
        {
            _a.DevConfirmedOnce = true;
            _b.DevConfirmedOnce = true;
            _host.Approvals.Allow(ApprovalKind.Write, ApprovalScope.Environment, _a.Identity, "tab");

            await Call(FakeWrite, Args(ConnB, new JObject { ["name"] = "x", ["reason"] = "Story." }));

            Assert.That(_tabB.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AReadGrant_DoesNotCoverAWrite_AndAWriteGrantDoesNotCoverARead()
        {
            _a.DevConfirmedOnce = true;
            _host.Approvals.Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, _a.Identity, "tab");
            await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story." }));
            Assert.That(_tabA.Requests, Has.Count.EqualTo(1), "a global read approval still asks for a write");

            _host.Approvals.Remove(_host.Approvals.List());
            _host.Approvals.Allow(ApprovalKind.Write, ApprovalScope.Environment, _a.Identity, "tab");
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            Assert.That(_tabA.Requests, Has.Count.EqualTo(2), "a write grant still asks for a read");
        }

        [Test]
        public async Task AnAnswerTheDialogDidNotOffer_IsADenial()
        {
            _a.DevConfirmedOnce = true;
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowGlobal);

            var result = await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story." }));

            Assert.That((string?)result.Payload["error"]!["code"], Is.EqualTo(ToolErrorCodes.DeniedByUser));
            Assert.That(_host.Approvals.List(), Is.Empty);
        }

        [Test]
        public async Task TheDevEnvironmentConfirmation_CannotBeGlobalOrPerTenant()
        {
            Assert.That(() => _host.Approvals.Allow(ApprovalKind.DevEnvironment, ApprovalScope.Always, _a.Identity, "tab"), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => _host.Approvals.Allow(ApprovalKind.DevEnvironment, ApprovalScope.Tenant, _a.Identity, "tab"), Throws.InstanceOf<ArgumentOutOfRangeException>());
            await Task.CompletedTask;
        }

        // ---- connection info ----

        [Test]
        public async Task GetConnectionInfo_ReportsTheIdsAndEveryGrantKind()
        {
            _host.Approvals.Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, _a.Identity, "tab");
            _host.Approvals.Allow(ApprovalKind.RecordRead, ApprovalScope.Session, _a.Identity, "tab");
            _host.Approvals.Allow(ApprovalKind.Write, ApprovalScope.Environment, _a.Identity, "tab");

            var result = await Call(ConnectionTools.GetConnectionInfo, Args(ConnA));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var payload = result.Payload;
            Assert.That((string?)payload["organization"]!["environmentId"], Is.EqualTo("env-a"));
            Assert.That((string?)payload["organization"]!["tenantId"], Is.EqualTo(Tenant1.ToString("D")));
            Assert.That((string?)payload["readApprovalScope"], Is.EqualTo("tenant"));
            Assert.That((string?)payload["recordReadScope"], Is.EqualTo("session"));
            Assert.That((string?)payload["writeScope"], Is.EqualTo("environment"));
            Assert.That(payload["settings"]!["recordQueriesEnabled"], Is.Null, "the Record queries switch is gone");
            Assert.That(payload["settings"]!["fileRoot"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(payload["settings"]!["fileRootScope"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public async Task GetConnectionInfo_ReportsTheFileRootAndItsLevel()
        {
            _host.FileRoots.Set(ApprovalScope.Tenant, _a.Identity, @"C:\Repos\client");

            var result = await Call(ConnectionTools.GetConnectionInfo, Args(ConnB));

            Assert.That((string?)result.Payload["settings"]!["fileRoot"], Is.EqualTo(@"C:\Repos\client"));
            Assert.That((string?)result.Payload["settings"]!["fileRootScope"], Is.EqualTo("tenant"));
        }

        private static IReadOnlyList<ToolDefinition> WithFakeWrite(CompanionHost host) => ToolCatalog.Create(host)
            .Concat(new[]
            {
                ToolDefinition.Simple(
                    FakeWrite,
                    ToolTier.Write,
                    "A Write tool for tests.",
                    InputSchema.ForConnection().String("name", "A name.", required: true).WithReason(required: true),
                    OutputSchema.Object(("done", OutputSchema.Boolean())),
                    execution => new JObject { ["done"] = true }),
            })
            .ToList();

        private static JObject Args(string connection, JObject? more = null)
        {
            var arguments = new JObject { ["connection"] = connection };
            foreach (var property in more?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                arguments[property.Name] = property.Value;
            }

            return arguments;
        }

        private Task<ToolResult> Call(string tool, JObject arguments, CancellationToken cancellation = default) =>
            _host.Dispatcher.CallAsync(new McpToolCall(tool, arguments, DateTime.UtcNow), cancellation);
    }
}
