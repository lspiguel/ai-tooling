using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

using Moq;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>
    /// Routing, approvals, cancellation and logging of tool calls — the Phase 1 exit behaviours that do not need
    /// XrmToolBox or a live client (SPEC §5 C7–C10, §6.2, §7, §8.6, R2, R12, R13).
    /// </summary>
    [TestFixture]
    public class ToolDispatcherTests
    {
        private const string ConnA = "aaaaaaaa-1111-1111-1111-111111111111";
        private const string ConnB = "bbbbbbbb-2222-2222-2222-222222222222";
        private const string FakeWrite = "fake_write";
        private static readonly Guid OrgA = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");
        private static readonly Guid OrgB = new Guid("bbbbbbbb-0000-0000-0000-00000000000b");

        private FakeClock _clock = null!;
        private FakeSettingsStore _settings = null!;
        private CompanionHost _host = null!;
        private FakeInstance _tabA = null!;
        private FakeInstance _tabB = null!;
        private Mock<IOrganizationService> _dataverseA = null!;
        private BoundConnection _a = null!;
        private List<CallLogEntry> _hostLog = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _settings = new FakeSettingsStore();
            _host = TestHost.Create(_clock, _settings, WithFakeWrite);
            _hostLog = new List<CallLogEntry>();
            _host.HostCallLogged += (s, entry) => _hostLog.Add(entry);
            _tabA = new FakeInstance("tab A");
            _tabB = new FakeInstance("tab B");
            _host.Attach(_tabA);
            _host.Attach(_tabB);
            _dataverseA = Dataverse.Mock(OrgA, Guid.NewGuid(), publisherCount: 5);
            _a = TestHost.Bind(_host, _tabA, OrgA, ConnA, _dataverseA.Object, "Dev A");
            TestHost.Bind(_host, _tabB, OrgB, ConnB, Dataverse.Mock(OrgB, Guid.NewGuid()).Object, "Dev B");
        }

        // ---- host tool and routing ----

        [Test]
        public async Task ListConnections_ReturnsBothBindings_WithoutDialog_InTheHostLog()
        {
            var result = await Call(HostTools.ListConnections, new JObject());

            Assert.That(result.IsError, Is.False);
            Assert.That(result.Payload["connections"]!.Select(c => (string)c["connectionId"]!), Is.EqualTo(new[] { ConnA, ConnB }));
            Assert.That(result.Payload["host"]!["protocolVersion"]!.ToString(), Is.EqualTo("2026-07-28"));
            Assert.That(_tabA.Requests.Count + _tabB.Requests.Count, Is.Zero, "list_connections is never approved (R2)");
            Assert.That(_hostLog.Single().OutcomeLine, Does.StartWith("  → ok  none  "));
            Assert.That(_tabA.CallLog.Snapshot(), Is.Empty);
        }

        [Test]
        public async Task ReadCall_IsApprovedAndLoggedInTheTabBoundToItsConnection()
        {
            var result = await Call(ConnectionTools.WhoAmI, Args(ConnB));

            Assert.That(result.IsError, Is.False);
            Assert.That(_tabB.Requests.Single().Tool, Is.EqualTo(ConnectionTools.WhoAmI));
            Assert.That(_tabA.Requests, Is.Empty);
            Assert.That(_tabB.CallLog.Snapshot().Single().CallLine, Does.Contain($"who_am_i  reason —  connection={ConnB.Substring(0, 10)}…"));
            Assert.That(_tabA.CallLog.Snapshot(), Is.Empty);
            Assert.That(_hostLog, Is.Empty);
        }

        [Test]
        public async Task UnknownConnection_FailsAtOnce_WithoutDialog_InTheHostLog()
        {
            var result = await Call(ConnectionTools.WhoAmI, Args("cccccccc-3333-3333-3333-333333333333"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ConnectionNotFound));
            Assert.That(_tabA.Requests.Count + _tabB.Requests.Count, Is.Zero);
            Assert.That(_hostLog.Single().OutcomeLine, Does.StartWith("  → error connection_not_found  —  "));
        }

        [Test]
        public async Task MissingConnection_IsInvalidArgument_EvenWithOneConnectionBound()
        {
            _host.ReleaseBinding(_tabB);

            var result = await Call(ConnectionTools.WhoAmI, new JObject());

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("connection"));
            Assert.That(_hostLog, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task UnknownArgument_IsInvalidArgument_InTheConnectionsLog()
        {
            var result = await Call(ConnectionTools.WhoAmI, Args(ConnA, new JObject { ["extra"] = 1 }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(_tabA.Requests, Is.Empty);
            Assert.That(_tabA.CallLog.Snapshot().Single().CallLine, Does.EndWith("extra=1"));
        }

        // ---- read approval scopes ----

        [Test]
        public async Task AllowOnce_EndsWithTheCall()
        {
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            Assert.That(_tabA.Requests, Has.Count.EqualTo(2));
            Assert.That(_tabA.CallLog.Snapshot().Select(e => e.Approval), Is.EqualTo(new[] { "once", "once" }));
        }

        [Test]
        public async Task AllowSession_CoversLaterReads_UntilTheHostStops()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowSession);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            var second = await Call(ConnectionTools.GetConnectionInfo, Args(ConnA));

            Assert.That(_tabA.Requests, Has.Count.EqualTo(1));
            Assert.That(_tabA.CallLog.Snapshot().Last().Approval, Is.EqualTo("session"));
            Assert.That((string?)second.Payload["readApprovalScope"], Is.EqualTo("session"));

            _host.Stop();
            _host.Start();
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            Assert.That(_tabA.Requests, Has.Count.EqualTo(2), "the session scope ended with the host");
        }

        [Test]
        public async Task AllowEnvironment_IsRemembered_AndRevokeClearsIt()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowEnvironment);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            var reloaded = TestHost.Create(store: _settings);
            Assert.That(reloaded.Approvals.Find(ApprovalKind.ReadApproval, OrgA)?.Scope, Is.EqualTo(ApprovalScope.Environment));

            _host.Approvals.RevokeOrganization(ApprovalKind.ReadApproval, OrgA);
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowOnce);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            Assert.That(_tabA.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ReadApproval_ForOneOrganization_NeverCoversAnother()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowEnvironment);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));

            await Call(ConnectionTools.WhoAmI, Args(ConnB));

            Assert.That(_tabB.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ReadDialog_OffersOnceSessionAndEnvironment_AndShowsTheReasonApart()
        {
            await Call(ConnectionTools.WhoAmI, Args(ConnA, new JObject { ["reason"] = "Initials for WIP naming." }));

            var request = _tabA.Requests.Single();
            Assert.That(request.Options, Is.EqualTo(new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment }));
            Assert.That(request.Reason, Is.EqualTo("Initials for WIP naming."));
            Assert.That(request.Arguments, Does.Not.Contain("Initials"), "the reason is shown in its own box only");
            Assert.That(request.Arguments, Does.Contain(ConnA));
            Assert.That(request.ConnectionName, Is.EqualTo("Dev A"));
        }

        // ---- denial, timeout, cancellation ----

        [Test]
        public async Task Deny_ReachesTheAgentAsDeniedByUser_AndIsLogged()
        {
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.Deny);

            var result = await Call(ConnectionTools.WhoAmI, Args(ConnA));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DeniedByUser));
            Assert.That(_tabA.CallLog.Snapshot().Single().OutcomeLine, Does.StartWith("  → error denied_by_user  —  "));
            _dataverseA.Verify(s => s.Execute(It.IsAny<WhoAmIRequest>()), Times.Never);
        }

        [Test]
        public async Task NoAnswer_TimesOutAsDeniedNoResponse_AndWithdrawsTheDialog()
        {
            _host.ChangeLimits(500, 40000, 1);
            CancellationToken dialogToken = default;
            _tabA.Answer = (r, c) =>
            {
                dialogToken = c;
                return FakeInstance.NeverAnswer(r, c);
            };

            var result = await Call(ConnectionTools.WhoAmI, Args(ConnA));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DeniedNoResponse));
            Assert.That(dialogToken.IsCancellationRequested, Is.True);
            Assert.That(_tabA.CallLog.Snapshot().Single().OutcomeLine, Does.StartWith("  → error denied_no_response  —  "));
        }

        [Test]
        public async Task ClientGoesAway_WhileAwaitingApproval_IsLoggedAsCancelledByClient()
        {
            _tabA.Answer = FakeInstance.NeverAnswer;
            using var gone = new CancellationTokenSource();
            var call = Call(ConnectionTools.WhoAmI, Args(ConnA), gone.Token);

            gone.Cancel();
            var result = await call;

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.CancelledByClient));
            Assert.That(_tabA.CallLog.Snapshot().Single().OutcomeLine, Does.StartWith("  → error cancelled_by_client  —  "));
        }

        [Test]
        public async Task TabChangesConnection_WhileAwaitingApproval_IsConnectionClosed()
        {
            _tabA.Answer = FakeInstance.NeverAnswer;
            var call = Call(ConnectionTools.WhoAmI, Args(ConnA));

            _host.ReleaseBinding(_tabA);
            var result = await call;

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ConnectionClosed));
        }

        [Test]
        public async Task HostStops_WhileAwaitingApproval_IsServerStopping()
        {
            _tabA.Answer = FakeInstance.NeverAnswer;
            var call = Call(ConnectionTools.WhoAmI, Args(ConnA));

            _host.Stop();
            var result = await call;

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ServerStopping));
        }

        [Test]
        public async Task OneDialogPerConnectionAtATime_OthersQueue()
        {
            var first = new TaskCompletionSource<ApprovalDecision>();
            _tabA.Answer = (r, c) => _tabA.Requests.Count == 1 ? first.Task : Task.FromResult(ApprovalDecision.AllowOnce);

            var call1 = Call(ConnectionTools.WhoAmI, Args(ConnA));
            var call2 = Call(ConnectionTools.WhoAmI, Args(ConnA));
            await Task.Delay(100);

            Assert.That(_tabA.Requests, Has.Count.EqualTo(1));
            Assert.That(_a.Pending, Is.EqualTo(2));

            first.SetResult(ApprovalDecision.AllowOnce);
            await Task.WhenAll(call1, call2);
            Assert.That(_tabA.Requests, Has.Count.EqualTo(2));
            Assert.That((_a.Pending, _a.Running), Is.EqualTo((0, 0)));
        }

        [Test]
        public async Task DifferentConnections_CanAwaitApprovalAtTheSameTime()
        {
            var answerA = new TaskCompletionSource<ApprovalDecision>();
            var answerB = new TaskCompletionSource<ApprovalDecision>();
            _tabA.Answer = (r, c) => answerA.Task;
            _tabB.Answer = (r, c) => answerB.Task;

            var callA = Call(ConnectionTools.WhoAmI, Args(ConnA));
            var callB = Call(ConnectionTools.WhoAmI, Args(ConnB));
            await Task.Delay(100);

            Assert.That((_tabA.Requests.Count, _tabB.Requests.Count), Is.EqualTo((1, 1)));
            answerA.SetResult(ApprovalDecision.AllowOnce);
            answerB.SetResult(ApprovalDecision.AllowOnce);
            await Task.WhenAll(callA, callB);
        }

        // ---- Write tier (R12 and the dev-environment guard) ----

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \r\n")]
        public async Task Write_WithoutAReason_FailsBeforeAnyDialog(string? reason)
        {
            var arguments = Args(ConnA, new JObject { ["name"] = "x" });
            if (reason != null)
            {
                arguments["reason"] = reason;
            }

            var result = await Call(FakeWrite, arguments);

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["message"]!, Is.EqualTo(ArgumentValidator.ReasonRequired));
            Assert.That(_tabA.Requests, Is.Empty);
            Assert.That(_tabA.CallLog.Snapshot().Single().OutcomeLine, Does.EndWith("reason is required for Write and Publisher tools."));
        }

        [Test]
        public async Task Write_ReasonCheckedBeforeEveryOtherCheck()
        {
            // Unknown connection and a missing argument too: the reason is still what is reported.
            var result = await Call(FakeWrite, new JObject { ["connection"] = "unknown" });

            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("reason"));
        }

        [Test]
        public async Task Write_OnAnUnconfirmedConnection_RefusesWithoutDialog()
        {
            var result = await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story 1234." }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DevEnvironmentNotConfirmed));
            Assert.That(_tabA.Requests, Is.Empty);
        }

        [Test]
        public async Task Write_OnAConfirmedConnection_AsksOnceWithTheReason()
        {
            _a.DevConfirmedOnce = true;

            var result = await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story 1234." }));

            Assert.That(result.IsError, Is.False);
            var request = _tabA.Requests.Single();
            Assert.That(request.Options, Is.EqualTo(new[] { ApprovalDecision.AllowOnce }));
            Assert.That(request.Tier, Is.EqualTo(ToolTier.Write));
            Assert.That(request.Reason, Is.EqualTo("Story 1234."));
            Assert.That(_tabA.CallLog.Snapshot().Single().CallLine, Does.Contain("reason \"Story 1234.\""));
        }

        [Test]
        public async Task Write_EnvironmentReadApproval_DoesNotSkipTheWriteDialog()
        {
            _host.Approvals.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, OrgA, "Dev A org", "tab A");
            _host.Approvals.GrantForOrganization(ApprovalKind.DevEnvironment, ApprovalScope.Environment, OrgA, "Dev A org", "tab A");

            await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Story 1234." }));

            Assert.That(_tabA.Requests, Has.Count.EqualTo(1));
        }

        // ---- paging ----

        [Test]
        public async Task ListPublishers_PagesUnderTheFirstApproval()
        {
            var first = await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["pageSize"] = 2 }));
            var cursor = (string)first.Payload["nextCursor"]!;
            var second = await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["cursor"] = cursor }));
            var third = await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["cursor"] = (string)second.Payload["nextCursor"]! }));

            var names = new[] { first, second, third }.SelectMany(r => r.Payload["publishers"]!).Select(p => (string)p["uniqueName"]!);
            Assert.That(names, Is.EqualTo(new[] { "publisher1", "publisher2", "publisher3", "publisher4", "publisher5" }));
            Assert.That(third.Payload["nextCursor"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(_tabA.Requests, Has.Count.EqualTo(1), "continuation pages raise no dialog");
            Assert.That(_tabA.CallLog.Snapshot().Select(e => e.Approval), Is.EqualTo(new[] { "once", "page", "page" }));
        }

        [Test]
        public async Task Cursor_FromAnotherConnection_OrAfterTheLastPage_IsInvalid()
        {
            var first = await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["pageSize"] = 4 }));
            var cursor = (string)first.Payload["nextCursor"]!;

            var wrongConnection = await Call(PublisherTools.ListPublishers, Args(ConnB, new JObject { ["cursor"] = cursor }));
            Assert.That(Code(wrongConnection), Is.EqualTo(ToolErrorCodes.InvalidCursor));

            await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["cursor"] = cursor }));
            var spent = await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["cursor"] = cursor }));
            Assert.That(Code(spent), Is.EqualTo(ToolErrorCodes.InvalidCursor));
        }

        [Test]
        public async Task PageSize_AboveTheHostMaximum_IsInvalidArgument()
        {
            var result = await Call(PublisherTools.ListPublishers, Args(ConnA, new JObject { ["pageSize"] = 501 }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(_tabA.Requests, Is.Empty);
        }

        // ---- Dataverse faults ----

        [Test]
        public async Task DataverseFault_IsDataverseErrorWithHexCode()
        {
            var fault = new OrganizationServiceFault { ErrorCode = unchecked((int)0x80040217), Message = "Record does not exist." };
            _dataverseA.Setup(s => s.Execute(It.IsAny<WhoAmIRequest>())).Throws(new FaultException<OrganizationServiceFault>(fault, fault.Message));

            var result = await Call(ConnectionTools.WhoAmI, Args(ConnA));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DataverseError));
            Assert.That((string)result.Payload["error"]!["details"]!["errorCode"]!, Is.EqualTo("0x80040217"));
            Assert.That(_tabA.CallLog.Snapshot().Single().Approval, Is.EqualTo("once"));
        }

        // ---- results match their output schemas ----

        [Test]
        public async Task EveryPhase1Result_ValidatesAgainstItsOutputSchema()
        {
            var calls = new (string Tool, JObject Arguments)[]
            {
                (HostTools.ListConnections, new JObject()),
                (ConnectionTools.GetConnectionInfo, Args(ConnA)),
                (ConnectionTools.WhoAmI, Args(ConnA)),
                (PublisherTools.ListPublishers, Args(ConnA)),
            };

            foreach (var (tool, arguments) in calls)
            {
                var result = await Call(tool, arguments);
                var schema = _host.Dispatcher.Tools.Single(t => t.Name == tool).OutputSchema;
                Assert.That(result.IsError, Is.False, tool);
                Assert.That(SchemaValidator.Validate(result.Payload, schema), Is.Empty, tool);
            }
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

        private static string? Code(ToolResult result) => (string?)result.Payload["error"]?["code"];

        private Task<ToolResult> Call(string tool, JObject arguments, CancellationToken cancellation = default) =>
            _host.Dispatcher.CallAsync(new McpToolCall(tool, arguments, _clock.UtcNow), cancellation);
    }
}
