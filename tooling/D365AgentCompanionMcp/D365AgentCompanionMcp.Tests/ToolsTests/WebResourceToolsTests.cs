using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Microsoft.Xrm.Sdk;

using Moq;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>
    /// <c>update_web_resources</c>: files by path under the File root, the overwrite check inside the tool, identical
    /// content left alone, a targeted publish, and the read-back (SPEC §6.8, S10).
    /// </summary>
    [TestFixture]
    public class WebResourceToolsTests
    {
        private const string Conn = "aaaaaaaa-1111-1111-1111-111111111111";
        private const string Reason = "Story 1234: account form script.";
        private static readonly Guid Org = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");
        private static readonly Guid Tenant = new Guid("11111111-0000-0000-0000-000000000001");

        private FakeClock _clock = null!;
        private CompanionHost _host = null!;
        private FakeInstance _tab = null!;
        private WebResourceData _data = null!;
        private BoundConnection _binding = null!;
        private string _repo = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _host = TestHost.Create(_clock);
            _tab = new FakeInstance();
            _host.Attach(_tab);
            var dataverse = Dataverse.Mock(Org, Guid.NewGuid());
            _data = new WebResourceData(dataverse);
            _binding = TestHost.Bind(_host, _tab, Org, Conn, dataverse.Object, "Dev A", "env-a", Tenant);
            _binding.DevConfirmedOnce = true;

            _repo = Path.Combine(Path.GetTempPath(), "ac-webres-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_repo, "Webresources", "scripts"));
            File.WriteAllText(Path.Combine(_repo, "Webresources", "scripts", "account.form.js"), "// new content");
            File.WriteAllText(Path.Combine(_repo, "Webresources", "scripts", "contact.form.js"), "// contact");
            File.WriteAllText(Path.Combine(_repo, "Webresources", "scripts", "notes.txt"), "not a web resource");
            _host.FileRoots.Set(ApprovalScope.Tenant, _binding.Identity, _repo);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_repo, true);
            }
            catch (IOException)
            {
                // Temp cleanup is best effort.
            }
        }

        // ---- the happy paths ----

        [Test]
        public async Task CreatesANewResourceInTheWip_AndPublishesJustIt()
        {
            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var created = result.Payload["results"]![0]!;
            Assert.That((string?)created["status"], Is.EqualTo("created"));
            Assert.That(created["versionBefore"]!.Type, Is.EqualTo(JTokenType.Null));
            var request = _data.Creates.Single();
            Assert.That(request.Parameters["SolutionUniqueName"], Is.EqualTo("WipA"));
            Assert.That((string)request.Target["name"], Is.EqualTo("p1_/scripts/contact.form.js"));
            Assert.That(((OptionSetValue)request.Target["webresourcetype"]).Value, Is.EqualTo(3));
            Assert.That((string)request.Target["displayname"], Is.EqualTo("contact.form.js"), "the default display name is the file name");
            Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String((string)request.Target["content"])), Is.EqualTo("// contact"));
            var id = Guid.Parse((string)created["id"]!);
            Assert.That(_data.Publishes.Single().ParameterXml, Does.Contain(id.ToString("D")).And.Not.Contain(WebResourceData.ExistingId.ToString("D")));
            Assert.That((bool)result.Payload["published"]!, Is.True);
            Assert.That((string?)created["versionAfter"], Is.EqualTo(_data.VersionOf(id)), "versionAfter is read after the publish");
            Assert.That(result.Payload["unexpectedCount"]!.Value<int>(), Is.Zero);
            AssertSchema(result);
        }

        [Test]
        public async Task UpdatesAnExistingResource_WithTheExpectedVersion()
        {
            var before = _data.VersionOf(WebResourceData.ExistingId);

            var result = await Call(Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName, expectedVersion: before));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var item = result.Payload["results"]![0]!;
            Assert.That((string?)item["status"], Is.EqualTo("updated"));
            Assert.That((string?)item["versionBefore"], Is.EqualTo(before));
            Assert.That((string?)item["versionAfter"], Is.EqualTo(_data.VersionOf(WebResourceData.ExistingId)));
            Assert.That(long.Parse((string)item["versionAfter"]!), Is.GreaterThan(long.Parse(before)));
            Assert.That(_data.Updates.Single().Id, Is.EqualTo(WebResourceData.ExistingId));
            Assert.That((string)item["sha256"]!, Is.EqualTo(Sha256("// new content")));
            Assert.That(_data.Creates, Is.Empty);
            AssertSchema(result);
        }

        [Test]
        public async Task TheDialog_ShowsTheFileRoot_EachPath_ItsSizeAndSha256()
        {
            await Call(Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName, expectedVersion: _data.VersionOf(WebResourceData.ExistingId)));

            var request = _tab.Requests.Single();
            Assert.That(request.Tier, Is.EqualTo(ToolTier.Write));
            Assert.That(request.Summary, Does.Contain(_repo).And.Contain("(tenant)"));
            Assert.That(request.Summary, Does.Contain("Webresources/scripts/account.form.js"));
            Assert.That(request.Summary, Does.Contain(Sha256("// new content")));
            Assert.That(request.Summary, Does.Contain("14 bytes"));
            Assert.That(request.Summary, Does.Contain("update  p1_/scripts/account.form.js"));
        }

        [Test]
        public async Task AnAbsolutePathInsideTheFileRoot_IsAccepted()
        {
            var absolute = Path.Combine(_repo, "Webresources", "scripts", "contact.form.js");

            var result = await Call(Item(absolute, "p1_/scripts/contact.form.js"));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
        }

        [Test]
        public async Task IdenticalContent_IsUnchanged_NotWritten_AndNothingIsPublished()
        {
            var version = _data.VersionOf(WebResourceData.ExistingId);
            _data.UnpublishedContent[WebResourceData.ExistingId] = Convert.ToBase64String(Encoding.UTF8.GetBytes("// new content"));

            var result = await Call(Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName, expectedVersion: version));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var item = result.Payload["results"]![0]!;
            Assert.That((string?)item["status"], Is.EqualTo("unchanged"));
            Assert.That((string?)item["versionAfter"], Is.EqualTo(version), "no write, so the version did not rise");
            Assert.That(_data.Updates, Is.Empty);
            Assert.That(_data.Publishes, Is.Empty);
            Assert.That((bool)result.Payload["published"]!, Is.False);
        }

        [Test]
        public async Task PublishFalse_WritesWithoutPublishing()
        {
            var result = await CallWith(
                new JObject { ["publish"] = false },
                Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName, expectedVersion: _data.VersionOf(WebResourceData.ExistingId)));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Updates, Has.Count.EqualTo(1));
            Assert.That(_data.Publishes, Is.Empty);
            Assert.That((bool)result.Payload["published"]!, Is.False);
        }

        [Test]
        public async Task AFailedPublish_IsReported_WithTheWritesKept()
        {
            _data.PublishFails = true;

            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That((bool)result.Payload["published"]!, Is.False);
            Assert.That((string?)result.Payload["publishError"], Does.Contain("publish failed"));
            Assert.That((string?)result.Payload["results"]![0]!["status"], Is.EqualTo("created"));
        }

        [Test]
        public async Task TheReadBack_ReportsAnythingTheWipGainedUnexpectedly()
        {
            _data.Stowaway = (92, Guid.NewGuid());

            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(result.Payload["unexpectedCount"]!.Value<int>(), Is.EqualTo(1));
            Assert.That((int)result.Payload["unexpected"]![0]!["componentType"]!["code"]!, Is.EqualTo(92));
            Assert.That(_tab.CallLog.Snapshot().Single().Warnings.Single(), Does.Contain("unexpected"));
        }

        // ---- refusals before the dialog ----

        [Test]
        public async Task AnExistingResource_WithoutExpectedVersion_IsInvalidArgument_WithoutADialog()
        {
            var result = await Call(Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("items[0].expectedVersion"));
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_data.Updates, Is.Empty);
        }

        [Test]
        public async Task AStaleExpectedVersion_IsVersionConflict_ListingBothVersionsAndWhoChangedIt()
        {
            var current = _data.VersionOf(WebResourceData.ExistingId);

            var result = await Call(Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName, expectedVersion: (long.Parse(current) - 1).ToString()));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.VersionConflict));
            var conflict = result.Payload["error"]!["details"]!["conflicts"]![0]!;
            Assert.That((string?)conflict["currentVersion"], Is.EqualTo(current));
            Assert.That((string?)conflict["expectedVersion"], Is.EqualTo((long.Parse(current) - 1).ToString()));
            Assert.That((string?)conflict["modifiedBy"]!["name"], Is.EqualTo("Another Developer"));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task AManagedResource_IsManagedComponent()
        {
            var result = await Call(Item("Webresources/scripts/account.form.js", WebResourceData.ManagedName, expectedVersion: _data.VersionOf(WebResourceData.ManagedId)));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ManagedComponent));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task AResourceOutsideTheWip_IsNotInSolution()
        {
            var result = await Call(Item("Webresources/scripts/account.form.js", WebResourceData.OutsideName, expectedVersion: _data.VersionOf(WebResourceData.OutsideId)));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotInSolution));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task ANewResource_MustStartWithThePublishersPrefix()
        {
            var result = await Call(Item("Webresources/scripts/contact.form.js", "other_/scripts/contact.form.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["message"]!, Does.Contain("p1_"));
        }

        [Test]
        public async Task ANewResource_WithAnExpectedVersion_IsInvalidArgument()
        {
            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js", expectedVersion: "123"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
        }

        [Test]
        public async Task ADotDotEscape_IsRefusedBeforeTheDialog_NamingTheItemTheRootAndThePath()
        {
            var result = await Call(Item("../outside.js", "p1_/scripts/outside.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            var details = result.Payload["error"]!["details"]!;
            Assert.That((string)details["argument"]!, Is.EqualTo("items[0].path"));
            Assert.That((string)details["fileRoot"]!, Is.EqualTo(_repo));
            Assert.That((string?)details["resolvedPath"], Is.Not.Null);
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task AnAbsolutePathOutsideTheRoot_IsRefused()
        {
            var outside = Path.Combine(Path.GetTempPath(), "somewhere-else.js");

            var result = await Call(Item(outside, "p1_/scripts/else.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task AnUnknownExtension_IsInvalidArgument()
        {
            var result = await Call(Item("Webresources/scripts/notes.txt", "p1_/scripts/notes.txt"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["message"]!, Does.Contain(".txt"));
        }

        [Test]
        public async Task AFileOverTheMaximumUploadSize_IsRefused()
        {
            File.WriteAllBytes(Path.Combine(_repo, "Webresources", "scripts", "big.js"), new byte[6000]);

            var result = await Call(Item("Webresources/scripts/big.js", "p1_/scripts/big.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["message"]!, Does.Contain("maximum upload size"));
        }

        [Test]
        public async Task AManagedWip_IsManagedSolution()
        {
            var result = await _host.Dispatcher.CallAsync(new McpToolCall(WebResourceTools.UpdateWebResources, Arguments(solution: "ManagedOne", Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js")), _clock.UtcNow), default);

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ManagedSolution));
        }

        [Test]
        public async Task TheSameNameTwice_IsInvalidArgument()
        {
            var result = await Call(
                Item("Webresources/scripts/contact.form.js", "p1_/scripts/a.js"),
                Item("Webresources/scripts/account.form.js", "p1_/scripts/a.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
        }

        [Test]
        public async Task ItRequiresAReason_AndAConfirmedDevelopmentEnvironment()
        {
            var noReason = Arguments(null, Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));
            noReason.Remove("reason");
            var missing = await _host.Dispatcher.CallAsync(new McpToolCall(WebResourceTools.UpdateWebResources, noReason, _clock.UtcNow), default);
            Assert.That((string)missing.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("reason"));

            _binding.DevConfirmedOnce = false;
            var unconfirmed = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));
            Assert.That(Code(unconfirmed), Is.EqualTo(ToolErrorCodes.DevEnvironmentNotConfirmed));
        }

        // ---- the overwrite check at write time ----

        [Test]
        public async Task AVersionThatChangesWhileTheDialogIsOpen_FailsTheItem_StopsTheRun_AndMarksTheRestNotAttempted()
        {
            var version = _data.VersionOf(WebResourceData.ExistingId);
            _tab.Answer = (r, c) =>
            {
                _data.Bump(WebResourceData.ExistingId);
                return Task.FromResult(ApprovalDecision.AllowOnce);
            };

            var result = await Call(
                Item("Webresources/scripts/account.form.js", WebResourceData.ExistingName, expectedVersion: version),
                Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That((string?)result.Payload["results"]![0]!["status"], Is.EqualTo("failed"));
            Assert.That((string?)result.Payload["results"]![0]!["error"]!["code"], Is.EqualTo(ToolErrorCodes.VersionConflict));
            Assert.That((string?)result.Payload["results"]![1]!["status"], Is.EqualTo("notAttempted"));
            Assert.That(_data.Updates, Is.Empty);
            Assert.That(_data.Creates, Is.Empty);
            Assert.That(_data.Publishes, Is.Empty);
        }

        // ---- the File root prompt ----

        [Test]
        public async Task WithNoFileRoot_ThePersonIsAskedFirst_BeforeTheApprovalDialog_AndTheChoiceIsKept()
        {
            _host.FileRoots.Clear(_host.FileRoots.List().Single());
            var order = new System.Collections.Generic.List<string>();
            _tab.FileRootAnswer = (r, c) =>
            {
                order.Add("file root");
                return Task.FromResult<FileRootChoice?>(new FileRootChoice(_repo, ApprovalScope.Tenant));
            };
            _tab.Answer = (r, c) =>
            {
                order.Add("approval");
                return Task.FromResult(ApprovalDecision.AllowOnce);
            };

            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(order, Is.EqualTo(new[] { "file root", "approval" }));
            var prompt = _tab.FileRootRequests.Single();
            Assert.That(prompt.PreselectedLevel, Is.EqualTo(ApprovalScope.Tenant), "the tenant is the default when it is known");
            Assert.That(prompt.TenantAvailable, Is.True);
            Assert.That(prompt.Paths, Is.EqualTo(new[] { "Webresources/scripts/contact.form.js" }));
            Assert.That(_host.FileRoots.Find(_binding.Identity)!.Path, Is.EqualTo(_repo));

            await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact2.form.js"));
            Assert.That(_tab.FileRootRequests, Has.Count.EqualTo(1), "asked once");
        }

        [Test]
        public async Task TheFileRootPrompt_PreselectsEnvironment_WhenTheConnectionReportsNoTenant()
        {
            var tab = new FakeInstance("tab D");
            _host.Attach(tab);
            var dataverse = Dataverse.Mock(Guid.NewGuid(), Guid.NewGuid());
            _ = new WebResourceData(dataverse);
            var binding = TestHost.Bind(_host, tab, Guid.NewGuid(), "dddddddd-4444-4444-4444-444444444444", dataverse.Object, "No Tenant");
            binding.DevConfirmedOnce = true;
            tab.FileRootAnswer = (r, c) => Task.FromResult<FileRootChoice?>(null);

            await _host.Dispatcher.CallAsync(new McpToolCall(WebResourceTools.UpdateWebResources, Arguments(null, Item("x.js", "p1_/x.js"), connection: "dddddddd-4444-4444-4444-444444444444"), _clock.UtcNow), default);

            var prompt = tab.FileRootRequests.Single();
            Assert.That(prompt.TenantAvailable, Is.False);
            Assert.That(prompt.PreselectedLevel, Is.EqualTo(ApprovalScope.Environment));
        }

        [Test]
        public async Task CancellingTheFileRootPrompt_FailsTheCall_AsDeniedByUser_AndLeavesTheRootUnset()
        {
            _host.FileRoots.Clear(_host.FileRoots.List().Single());
            _tab.FileRootAnswer = (r, c) => Task.FromResult<FileRootChoice?>(null);

            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DeniedByUser));
            Assert.That(_host.FileRoots.Find(_binding.Identity), Is.Null);
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_data.Creates, Is.Empty);
        }

        [Test]
        public async Task NoAnswerToTheFileRootPrompt_FailsTheCall_AsDeniedNoResponse()
        {
            _host.FileRoots.Clear(_host.FileRoots.List().Single());
            _host.Settings.Update(s => s.ApprovalTimeoutSeconds = 1);
            _tab.FileRootAnswer = FileRootNeverAnswers;

            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DeniedNoResponse));
            Assert.That(_host.FileRoots.Find(_binding.Identity), Is.Null);
        }

        [Test]
        public async Task TheFileRootPrompt_AppearsEvenWhenAWriteGrantWouldSkipTheApprovalDialog()
        {
            _host.FileRoots.Clear(_host.FileRoots.List().Single());
            _host.Approvals.Allow(ApprovalKind.Write, ApprovalScope.Environment, _binding.Identity, "tab");
            _tab.FileRootAnswer = (r, c) => Task.FromResult<FileRootChoice?>(new FileRootChoice(_repo, ApprovalScope.Environment));

            var result = await Call(Item("Webresources/scripts/contact.form.js", "p1_/scripts/contact.form.js"));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_tab.FileRootRequests, Has.Count.EqualTo(1));
            Assert.That(_tab.Requests, Is.Empty, "the Write grant covers the approval");
            Assert.That(_host.FileRoots.Find(_binding.Identity)!.Level, Is.EqualTo(ApprovalScope.Environment));
            Assert.That(_tab.CallLog.Snapshot().Single().Approval, Is.EqualTo("environment"));
        }

        [Test]
        public async Task AChangeToTheFileRoot_AppliesFromTheNextCall()
        {
            var other = Path.Combine(Path.GetTempPath(), "ac-webres-other-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(other);
            try
            {
                File.WriteAllText(Path.Combine(other, "contact.form.js"), "// from the other repository");
                var first = await Call(Item("contact.form.js", "p1_/scripts/first.js"));
                Assert.That(first.IsError, Is.True, "the first root has no contact.form.js at its top");

                _host.FileRoots.Set(ApprovalScope.Tenant, _binding.Identity, other);
                var second = await Call(Item("contact.form.js", "p1_/scripts/second.js"));

                Assert.That(second.IsError, Is.False, second.Payload.ToString());
            }
            finally
            {
                Directory.Delete(other, true);
            }
        }

        // ---- helpers ----

        private static Task<FileRootChoice?> FileRootNeverAnswers(FileRootRequest request, CancellationToken cancellation)
        {
            var completion = new TaskCompletionSource<FileRootChoice?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation.Register(() => completion.TrySetCanceled());
            return completion.Task;
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
            }
        }

        private static JObject Item(string path, string name, string? expectedVersion = null)
        {
            var item = new JObject { ["path"] = path, ["name"] = name };
            if (expectedVersion != null)
            {
                item["expectedVersion"] = expectedVersion;
            }

            return item;
        }

        private static JObject Arguments(string? solution, params JObject[] items) => Arguments(solution, items, "aaaaaaaa-1111-1111-1111-111111111111");

        private static JObject Arguments(string? solution, JObject item, string connection) => Arguments(solution, new[] { item }, connection);

        private static JObject Arguments(string? solution, JObject[] items, string connection) => new JObject
        {
            ["connection"] = connection,
            ["solutionUniqueName"] = solution ?? WebResourceData.Wip,
            ["items"] = new JArray(items),
            ["reason"] = Reason,
        };

        private void AssertSchema(ToolResult result) =>
            Assert.That(SchemaValidator.Validate(result.Payload, _host.Dispatcher.Tools.Single(t => t.Name == WebResourceTools.UpdateWebResources).OutputSchema), Is.Empty);

        private static string? Code(ToolResult result) => (string?)result.Payload["error"]?["code"];

        private Task<ToolResult> Call(params JObject[] items) => CallWith(new JObject(), items);

        private Task<ToolResult> CallWith(JObject extra, params JObject[] items)
        {
            var arguments = Arguments(null, items, Conn);
            foreach (var property in extra.Properties())
            {
                arguments[property.Name] = property.Value;
            }

            return _host.Dispatcher.CallAsync(new McpToolCall(WebResourceTools.UpdateWebResources, arguments, _clock.UtcNow), default);
        }
    }
}
