using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.LoggingTests
{
    /// <summary>
    /// <b>Write call log to disk</b> (SPEC 6a.2), through the dispatcher: what is written, where, when, and that the
    /// agent never sees a difference. The temp folder is outside every Git working tree.
    /// </summary>
    [TestFixture]
    public class DiskCallLogTests
    {
        private const string ConnA = "aaaaaaaa-1111-1111-1111-111111111111";
        private const string ConnB = "bbbbbbbb-2222-2222-2222-222222222222";
        private const string FakeWrite = "fake_write";
        private static readonly Guid OrgA = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");
        private static readonly Guid OrgB = new Guid("bbbbbbbb-0000-0000-0000-00000000000b");
        private static readonly Guid Tenant = new Guid("cccccccc-0000-0000-0000-00000000000c");

        private string _folder = string.Empty;
        private FakeClock _clock = null!;
        private FakeSettingsStore _settings = null!;
        private CompanionHost _host = null!;
        private FakeInstance _tabA = null!;
        private FakeInstance _tabB = null!;
        private BoundConnection _a = null!;
        private List<string> _warnings = null!;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "DiskCallLogTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            Assume.That(LogFileWriter.IsInsideGitWorkingTree(_folder), Is.False, "the temp folder must be outside every Git working tree");
            _clock = new FakeClock();
            _settings = new FakeSettingsStore();
            StartHost();
        }

        [TearDown]
        public void TearDown()
        {
            FakeServer.Taken.Clear();
            if (Directory.Exists(_folder))
            {
                foreach (var file in Directory.GetFiles(_folder))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_folder, recursive: true);
            }
        }

        // ---- the setting ----

        [Test]
        public async Task Off_ByDefault_NothingIsWritten()
        {
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(_host.DiskLog.IsOn, Is.False);
            Assert.That(Directory.GetFiles(_folder), Is.Empty);
        }

        [Test]
        public void AFolderInsideAGitWorkingTree_OrMissing_IsRefused_AndTheLogStaysOff()
        {
            var repository = Path.Combine(_folder, "repo");
            Directory.CreateDirectory(Path.Combine(repository, ".git"));
            Directory.CreateDirectory(Path.Combine(repository, "logs"));

            Assert.That(_host.DiskLog.TurnOn(Path.Combine(repository, "logs")), Does.Contain("Git working tree"));
            Assert.That(_host.DiskLog.TurnOn(Path.Combine(_folder, "missing")), Does.Contain("does not exist"));
            Assert.That(_host.DiskLog.IsOn, Is.False);
            Assert.That(_settings.Saved?.CallLogToDisk ?? false, Is.False);
        }

        [Test]
        public async Task TurningOn_IsSaved_AndTheNextStartResumesForAConnectionBoundAfterwards()
        {
            Assert.That(_host.DiskLog.TurnOn(_folder), Is.Null);
            Assert.That(_settings.Saved!.CallLogToDisk, Is.True);
            Assert.That(_settings.Saved.CallLogFolder, Is.EqualTo(Path.GetFullPath(_folder)));

            StartHost();
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(_host.DiskLog.IsOn, Is.True);
            Assert.That(File.Exists(PathFor("Dev A")), Is.True);
        }

        [Test]
        public void ASavedFolderThatIsGone_KeepsTheLogOffForTheSession_WithAWarning_AndKeepsTheSavedValues()
        {
            var gone = Path.Combine(_folder, "gone");
            Directory.CreateDirectory(gone);
            _host.DiskLog.TurnOn(gone);
            Directory.Delete(gone);

            StartHost();

            Assert.That(_host.DiskLog.IsOn, Is.False);
            Assert.That(_host.DiskLog.StartupWarning, Does.Contain("off for this session"));
            Assert.That(_settings.Saved!.CallLogToDisk, Is.True, "the saved values are kept");
            Assert.That(_settings.Saved.CallLogFolder, Is.EqualTo(Path.GetFullPath(gone)));
        }

        [Test]
        public void TurningOff_IsSaved_AndKeepsTheFolderForTheNextPrompt()
        {
            _host.DiskLog.TurnOn(_folder);

            _host.DiskLog.TurnOff();

            Assert.That(_host.DiskLog.IsOn, Is.False);
            Assert.That(_settings.Saved!.CallLogToDisk, Is.False);
            Assert.That(_host.DiskLog.SavedFolder, Is.EqualTo(Path.GetFullPath(_folder)));
        }

        [Test]
        public void ResetAllApprovals_DoesNotTouchTheSetting()
        {
            _host.DiskLog.TurnOn(_folder);

            _host.Approvals.ClearAll();

            Assert.That(_host.DiskLog.IsOn, Is.True);
        }

        // ---- what is written ----

        [Test]
        public async Task AReadCall_AndADeniedWriteCall_EachAddAnEntry_AndTheFileParsesAfterEach()
        {
            _host.DiskLog.TurnOn(_folder);
            _a.DevConfirmedOnce = true;

            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();
            var afterRead = Read("Dev A");
            Assert.That(afterRead["entries"]!, Has.Count.EqualTo(1));

            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.Deny);
            await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = "x", ["reason"] = "Testing the log." }));
            await _host.DiskLog.Flush();
            var document = Read("Dev A");

            Assert.That((int)document["schemaVersion"]!, Is.EqualTo(1));
            Assert.That((string)document["connection"]!["connectionId"]!, Is.EqualTo(ConnA));
            Assert.That((string)document["connection"]!["connectionName"]!, Is.EqualTo("Dev A"));
            Assert.That((string)document["connection"]!["organization"]!["id"]!, Is.EqualTo(OrgA.ToString("D")));
            Assert.That((string)document["connection"]!["organization"]!["tenantId"]!, Is.EqualTo(Tenant.ToString("D")));
            Assert.That((string)document["date"]!, Is.EqualTo(_clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd")));

            var read = document["entries"]![0]!;
            Assert.That((string)read["tool"]!, Is.EqualTo(ConnectionTools.WhoAmI));
            Assert.That((string)read["tier"]!, Is.EqualTo("read"));
            Assert.That(read["reason"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)read["outcome"]!["status"]!, Is.EqualTo("ok"));
            Assert.That((string)read["outcome"]!["approval"]!, Is.EqualTo("once"));
            Assert.That((int)read["outcome"]!["pages"]!, Is.EqualTo(1));
            Assert.That((string)read["callLine"]!, Is.EqualTo(_tabA.CallLog.Snapshot()[0].CallLine), "the panel's line, as shown");
            Assert.That((string)read["outcome"]!["outcomeLine"]!, Is.EqualTo(_tabA.CallLog.Snapshot()[0].OutcomeLine));
            Assert.That(read["result"]!.Type, Is.EqualTo(JTokenType.Object), "the full result");

            var denied = document["entries"]![1]!;
            Assert.That((string)denied["tier"]!, Is.EqualTo("write"));
            Assert.That((string)denied["reason"]!, Is.EqualTo("Testing the log."));
            Assert.That(denied["arguments"]!["reason"], Is.Null, "the reason is not repeated in the arguments");
            Assert.That((string)denied["arguments"]!["name"]!, Is.EqualTo("x"));
            Assert.That((string)denied["outcome"]!["status"]!, Is.EqualTo("error"));
            Assert.That((string)denied["outcome"]!["errorCode"]!, Is.EqualTo(ToolErrorCodes.DeniedByUser));
            Assert.That(denied["outcome"]!["approval"]!.Type, Is.EqualTo(JTokenType.Null), "the call never ran");
            Assert.That(denied["result"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public async Task ResultsAreWrittenInFull_WhereThePanelTruncates()
        {
            _host.DiskLog.TurnOn(_folder);

            await Call(ConnectionTools.GetConnectionInfo, Args(ConnA));
            await _host.DiskLog.Flush();

            var entry = Read("Dev A")["entries"]![0]!;
            Assert.That((string)entry["result"]!["connectionId"]!, Is.EqualTo(ConnA), "untruncated");
            Assert.That((string)entry["outcome"]!["outcomeLine"]!, Does.Contain("…"), "the panel line stays truncated");
        }

        [Test]
        public async Task AHostCall_GoesToTheHostFile_WithNoConnection()
        {
            _host.DiskLog.TurnOn(_folder);

            await Call(HostTools.ListConnections, new JObject());
            await Call(ConnectionTools.WhoAmI, Args("99999999-9999-9999-9999-999999999999"));
            await _host.DiskLog.Flush();

            var document = Read(CallLogFile.HostName);
            Assert.That(document["connection"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(document["entries"]!.Select(e => (string)e["tool"]!), Is.EqualTo(new[] { HostTools.ListConnections, ConnectionTools.WhoAmI }));
            Assert.That((string)document["entries"]![0]!["outcome"]!["approval"]!, Is.EqualTo("none"));
            Assert.That((string)document["entries"]![1]!["outcome"]!["errorCode"]!, Is.EqualTo(ToolErrorCodes.ConnectionNotFound));
        }

        [Test]
        public async Task TwoConnections_EachGetTheirOwnFile()
        {
            _host.DiskLog.TurnOn(_folder);

            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await Call(ConnectionTools.WhoAmI, Args(ConnB));
            await _host.DiskLog.Flush();

            Assert.That((string)Read("Dev A")["connection"]!["connectionId"]!, Is.EqualTo(ConnA));
            Assert.That((string)Read("Dev B")["connection"]!["connectionId"]!, Is.EqualTo(ConnB));
        }

        [Test]
        public async Task AfterARestartOnTheSameDay_CallsAppendToTheSameFile()
        {
            _host.DiskLog.TurnOn(_folder);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            StartHost();
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(1));
            Assert.That(Read("Dev A")["entries"]!, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ACallAfterMidnight_StartsTheNextDaysFile()
        {
            _host.DiskLog.TurnOn(_folder);
            var local = _clock.UtcNow.ToLocalTime();
            _clock.UtcNow = DateTime.SpecifyKind(local.Date.AddHours(23).AddMinutes(59), DateTimeKind.Local).ToUniversalTime();
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            _clock.Advance(TimeSpan.FromMinutes(2));
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            var first = Read("Dev A", local.Date);
            var second = Read("Dev A", local.Date.AddDays(1));
            Assert.That(first["entries"]!, Has.Count.EqualTo(1));
            Assert.That(second["entries"]!, Has.Count.EqualTo(1));
            Assert.That((string)second["date"]!, Is.EqualTo(local.Date.AddDays(1).ToString("yyyy-MM-dd")));
        }

        [Test]
        public async Task ATruncatedFile_IsLeftUntouched_AndASecondFileStarts_WithAWarning()
        {
            _host.DiskLog.TurnOn(_folder);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();
            var path = PathFor("Dev A");
            var truncated = File.ReadAllBytes(path).Take(40).ToArray();
            File.WriteAllBytes(path, truncated);

            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(truncated), "never modified");
            Assert.That(Read("Dev A", sequence: 2)["entries"]!, Has.Count.EqualTo(1));
            Assert.That(_warnings.Single(), Does.Contain("not in the expected shape"));
        }

        [Test]
        public async Task AHandEditedFileFoundAtStartup_IsLeftUntouched_AndASecondFileStarts()
        {
            _host.DiskLog.TurnOn(_folder);
            var path = PathFor("Dev A");
            File.WriteAllText(path, "{ \"edited\": true }\n");

            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(File.ReadAllText(path), Is.EqualTo("{ \"edited\": true }\n"));
            Assert.That(File.Exists(PathFor("Dev A", sequence: 2)), Is.True);
        }

        [Test]
        public async Task TwoConnectionsWithOneName_GetTheIdPrefixInTheirFileNames()
        {
            var tabC = new FakeInstance("tab C");
            var tabD = new FakeInstance("tab D");
            _host.Attach(tabC);
            _host.Attach(tabD);
            const string ConnC = "cccccccc-3333-3333-3333-333333333333";
            const string ConnD = "dddddddd-4444-4444-4444-444444444444";
            TestHost.Bind(_host, tabC, Guid.NewGuid(), ConnC, Dataverse.Mock(Guid.NewGuid(), Guid.NewGuid()).Object, "Twin");
            TestHost.Bind(_host, tabD, Guid.NewGuid(), ConnD, Dataverse.Mock(Guid.NewGuid(), Guid.NewGuid()).Object, "Twin");
            _host.DiskLog.TurnOn(_folder);

            await Call(ConnectionTools.WhoAmI, Args(ConnC));
            await Call(ConnectionTools.WhoAmI, Args(ConnD));
            await _host.DiskLog.Flush();

            Assert.That(File.Exists(Path.Combine(_folder, CallLogFile.FileName("Twin", "cccccccc", Today))), Is.True);
            Assert.That(File.Exists(Path.Combine(_folder, CallLogFile.FileName("Twin", "dddddddd", Today))), Is.True);
            Assert.That(File.Exists(PathFor("Twin")), Is.False);
        }

        [Test]
        public async Task APlainFileOfAnotherConnectionWithTheSameName_IsNotAppendedTo()
        {
            _host.DiskLog.TurnOn(_folder);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            // Dev A's tab closes; another connection named Dev A is bound later the same day.
            _host.ReleaseBinding(_tabA);
            const string ConnE = "eeeeeeee-5555-5555-5555-555555555555";
            TestHost.Bind(_host, _tabA, Guid.NewGuid(), ConnE, Dataverse.Mock(Guid.NewGuid(), Guid.NewGuid()).Object, "Dev A");
            await Call(ConnectionTools.WhoAmI, Args(ConnE));
            await _host.DiskLog.Flush();

            Assert.That(Read("Dev A")["entries"]!, Has.Count.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_folder, CallLogFile.FileName("Dev A", "eeeeeeee", Today))), Is.True);
        }

        [Test]
        public async Task AFailedWrite_NeverFailsTheCall_WarnsOnce_AndTheLogStaysOn()
        {
            _host.DiskLog.TurnOn(_folder);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();
            var before = File.ReadAllBytes(PathFor("Dev A"));
            File.SetAttributes(PathFor("Dev A"), FileAttributes.ReadOnly);

            var first = await Call(ConnectionTools.WhoAmI, Args(ConnA));
            var second = await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(first.IsError || second.IsError, Is.False);
            Assert.That(_host.DiskLog.IsOn, Is.True, "the checkbox stays ticked");
            Assert.That(_warnings.Single(), Does.Contain("could not be written"), "warned once");
            Assert.That(File.ReadAllBytes(PathFor("Dev A")), Is.EqualTo(before));
        }

        [Test]
        public async Task AFolderRemovedWhileOn_WarnsOnce_AndTheNextSuccessfulWriteResetsTheWarning()
        {
            var folder = Path.Combine(_folder, "logs");
            Directory.CreateDirectory(folder);
            _host.DiskLog.TurnOn(folder);
            Directory.Delete(folder);

            var result = await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();
            Assert.That(result.IsError, Is.False);
            Assert.That(_warnings, Has.Count.EqualTo(1));
            Assert.That(_warnings[0], Does.Contain("could not be written"));

            Directory.CreateDirectory(folder);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();
            Assert.That(Directory.GetFiles(folder), Has.Length.EqualTo(1), "the write succeeded");
            Directory.Delete(folder, recursive: true);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(_warnings, Has.Count.EqualTo(2), "warned again after a write succeeded");
        }

        [Test]
        public async Task AFolderMovedIntoAGitWorkingTree_IsRefusedWhenTheFileIsOpened()
        {
            _host.DiskLog.TurnOn(_folder);
            Directory.CreateDirectory(Path.Combine(_folder, ".git"));

            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(File.Exists(PathFor("Dev A")), Is.False);
            Assert.That(_warnings.Single(), Does.Contain("Git working tree"));
        }

        [Test]
        public async Task AfterUnticking_NothingMoreIsWritten_ButACallAlreadyRunningIs()
        {
            _host.DiskLog.TurnOn(_folder);
            var answer = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tabA.Answer = (r, c) => answer.Task;
            var running = Call(ConnectionTools.WhoAmI, Args(ConnA));
            await WaitFor(() => _tabA.Requests.Count == 1);

            _host.DiskLog.TurnOff();
            answer.SetResult(ApprovalDecision.AllowOnce);
            await running;
            _tabA.Answer = (r, c) => Task.FromResult(ApprovalDecision.AllowOnce);
            await Call(ConnectionTools.WhoAmI, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(Read("Dev A")["entries"]!, Has.Count.EqualTo(1), "only the call that was running");
        }

        [Test]
        public async Task BidiCharactersFromTheAgent_AreEscapedInTheFile()
        {
            _host.DiskLog.TurnOn(_folder);
            _a.DevConfirmedOnce = true;
            var rlo = (char)0x202E;

            await Call(FakeWrite, Args(ConnA, new JObject { ["name"] = $"abc{rlo}def", ["reason"] = $"Reason{rlo}." }));
            await _host.DiskLog.Flush();

            var text = File.ReadAllText(PathFor("Dev A"));
            Assert.That(text.IndexOf(rlo), Is.EqualTo(-1));
            Assert.That(text, Does.Contain("\\u202E"));
            Assert.That((string)JObject.Parse(text)["entries"]![0]!["reason"]!, Is.EqualTo($"Reason{rlo}."));
        }

        [Test]
        public async Task ToolResponses_AreIdenticalWithTheLogOnOrOff_IncludingWhenAWriteFails()
        {
            var off = await Call(ConnectionTools.GetConnectionInfo, Args(ConnA));
            _host.DiskLog.TurnOn(_folder);
            var on = await Call(ConnectionTools.GetConnectionInfo, Args(ConnA));
            await _host.DiskLog.Flush();
            Directory.CreateDirectory(Path.Combine(_folder, ".git"));
            var failing = await Call(ConnectionTools.GetConnectionInfo, Args(ConnA));
            await _host.DiskLog.Flush();

            Assert.That(on.Text, Is.EqualTo(off.Text));
            Assert.That(failing.Text, Is.EqualTo(off.Text));
            Assert.That(off.Text, Does.Not.Contain(_folder.Replace("\\", "\\\\")).And.Not.Contain("AgentCompanion-"));
        }

        // ---- helpers ----

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

        private static async Task WaitFor(Func<bool> condition)
        {
            for (var i = 0; i < 200 && !condition(); i++)
            {
                await Task.Delay(10);
            }

            Assert.That(condition(), Is.True, "timed out waiting");
        }

        private DateTime Today => _clock.UtcNow.ToLocalTime().Date;

        /// <summary>Starts a host on the same settings, as XrmToolBox does when it opens again.</summary>
        private void StartHost()
        {
            FakeServer.Taken.Clear();
            _host = TestHost.Create(_clock, _settings, WithFakeWrite);
            _warnings = new List<string>();
            _host.DiskLog.Warned += (s, w) =>
            {
                lock (_warnings)
                {
                    _warnings.Add(w);
                }
            };
            _tabA = new FakeInstance("tab A");
            _tabB = new FakeInstance("tab B");
            _host.Attach(_tabA);
            _host.Attach(_tabB);
            _a = TestHost.Bind(_host, _tabA, OrgA, ConnA, Dataverse.Mock(OrgA, Guid.NewGuid()).Object, "Dev A", "env-a", Tenant);
            TestHost.Bind(_host, _tabB, OrgB, ConnB, Dataverse.Mock(OrgB, Guid.NewGuid()).Object, "Dev B", "env-b", Tenant);
        }

        private string PathFor(string name, DateTime? date = null, int sequence = 1) =>
            Path.Combine(_folder, CallLogFile.FileName(name, null, date ?? Today, sequence));

        private JObject Read(string name, DateTime? date = null, int sequence = 1) =>
            JObject.Parse(File.ReadAllText(PathFor(name, date, sequence)));

        private Task<ToolResult> Call(string tool, JObject arguments, CancellationToken cancellation = default) =>
            _host.Dispatcher.CallAsync(new McpToolCall(tool, arguments, _clock.UtcNow), cancellation);
    }
}
