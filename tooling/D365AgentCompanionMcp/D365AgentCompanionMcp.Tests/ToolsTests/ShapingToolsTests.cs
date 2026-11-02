using System;
using System.Linq;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
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
    /// The Phase 3 Write and Publisher tools: resolution before the dialog, the fixed flags (R1), the read-back and
    /// unexpected components, the gates and the publisher's typed confirmation (SPEC §6.6, §6.7, §8.3, R3, R12).
    /// </summary>
    [TestFixture]
    public class ShapingToolsTests
    {
        private const string Conn = "aaaaaaaa-1111-1111-1111-111111111111";
        private const string Reason = "Story 1234: base solution.";
        private static readonly Guid Org = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");

        private FakeClock _clock = null!;
        private CompanionHost _host = null!;
        private FakeInstance _tab = null!;
        private Mock<IOrganizationService> _dataverse = null!;
        private ShapingData _data = null!;
        private BoundConnection _binding = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _host = TestHost.Create(_clock);
            _tab = new FakeInstance();
            _host.Attach(_tab);
            _dataverse = Dataverse.Mock(Org, Guid.NewGuid());
            _data = new ShapingData(_dataverse);
            _binding = TestHost.Bind(_host, _tab, Org, Conn, _dataverse.Object);
            _binding.DevConfirmedOnce = true;
        }

        // ---- create_solution ----

        [Test]
        public async Task CreateSolution_ShowsThePublisherPrefix_AndCreatesUnderIt()
        {
            var result = await Call(SolutionShapingTools.CreateSolution, Args(new JObject { ["uniqueName"] = "p1_Wip1234", ["friendlyName"] = "WIP 1234", ["publisherUniqueName"] = "publisher1" }));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_tab.Requests.Single().Summary, Does.Contain("prefix p1"));
            Assert.That(_tab.Requests.Single().Options, Is.EqualTo(new[] { ApprovalDecision.AllowOnce }));
            var created = _data.Created.Single();
            Assert.That(created.GetAttributeValue<EntityReference>("publisherid").Id, Is.EqualTo(DiscoveryData.PublisherId));
            Assert.That(created["version"], Is.EqualTo("1.0.0.0"));
            Assert.That(result.Payload["publisher"]!["customizationPrefix"]!.ToString(), Is.EqualTo("p1"));
            AssertSchema(SolutionShapingTools.CreateSolution, result);
        }

        [Test]
        public async Task CreateSolution_NameTaken_IsAlreadyExists_WithoutDialog()
        {
            var result = await Call(SolutionShapingTools.CreateSolution, Args(new JObject { ["uniqueName"] = "WipA", ["friendlyName"] = "WIP A", ["publisherUniqueName"] = "publisher1" }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.AlreadyExists));
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_data.Created, Is.Empty);
        }

        [Test]
        public async Task CreateSolution_UnknownPublisher_IsNotFound_WithoutDialog()
        {
            var result = await Call(SolutionShapingTools.CreateSolution, Args(new JObject { ["uniqueName"] = "p1_Wip", ["friendlyName"] = "WIP", ["publisherUniqueName"] = "nobody" }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [TestCase("uniqueName", "1abc")]
        [TestCase("uniqueName", "has space")]
        [TestCase("version", "1.0.0")]
        public async Task CreateSolution_BadNameOrVersion_IsInvalidArgument(string argument, string value)
        {
            var arguments = new JObject { ["uniqueName"] = "p1_Wip", ["friendlyName"] = "WIP", ["publisherUniqueName"] = "publisher1" };
            arguments[argument] = value;

            var result = await Call(SolutionShapingTools.CreateSolution, Args(arguments));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo(argument));
        }

        [Test]
        public async Task WriteTool_WithoutReason_FailsFirst_AndWithoutDevConfirmation_IsRefused()
        {
            var arguments = new JObject { ["connection"] = Conn, ["uniqueName"] = "p1_Wip", ["friendlyName"] = "WIP", ["publisherUniqueName"] = "publisher1", ["reason"] = "  " };
            var blank = await Call(SolutionShapingTools.CreateSolution, arguments);

            _binding.DevConfirmedOnce = false;
            var unconfirmed = await Call(SolutionShapingTools.CreateSolution, Args(new JObject { ["uniqueName"] = "p1_Wip", ["friendlyName"] = "WIP", ["publisherUniqueName"] = "publisher1" }));

            Assert.That((string)blank.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("reason"));
            Assert.That(Code(unconfirmed), Is.EqualTo(ToolErrorCodes.DevEnvironmentNotConfirmed));
            Assert.That(_tab.Requests, Is.Empty);
        }

        // ---- add_solution_components ----

        [Test]
        public async Task Add_UsesTheFixedFlags_AndShowsResolvedNamesInTheDialog()
        {
            var components = new JArray(
                new JObject { ["type"] = 1, ["logicalName"] = "account" },
                new JObject { ["type"] = 2, ["table"] = "account", ["logicalName"] = "name" },
                new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString() });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var summary = _tab.Requests.Single().Summary;
            Assert.That(summary, Does.Contain("Table         account   (metadata only, no subcomponents)"));
            Assert.That(summary, Does.Contain("Column        account.name"));
            Assert.That(summary, Does.Contain("Form          Account   on account"));
            Assert.That(summary, Does.Contain("required components OFF · subcomponents OFF"));

            Assert.That(_data.Adds.All(a => !a.AddRequiredComponents), Is.True, "R1: never required components");
            Assert.That(_data.Adds.Select(a => (a.ComponentType, a.DoNotIncludeSubcomponents)), Is.EqualTo(new[] { (1, true), (2, false), (60, false) }));
            Assert.That(_data.Adds[0].IncludedComponentSettingsValues, Is.Null, "metadata → settings unset → behaviour 1");
            Assert.That(result.Payload["results"]!.Select(r => (string)r["status"]!), Is.EqualTo(new[] { "added", "added", "added" }));
            Assert.That((int)result.Payload["addedCount"]!, Is.EqualTo(3));
            Assert.That((int)result.Payload["unexpectedCount"]!, Is.Zero);
            AssertSchema(SolutionShapingTools.AddSolutionComponents, result);
        }

        [Test]
        public async Task Add_Shell_SendsEmptySettings()
        {
            var components = new JArray(new JObject { ["type"] = 1, ["logicalName"] = "account" });

            await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components, ["tableBehavior"] = "shell" }));

            Assert.That(_data.Adds.Single().IncludedComponentSettingsValues, Is.Empty, "shell → settings [] → behaviour 2");
            Assert.That(_data.Adds.Single().DoNotIncludeSubcomponents, Is.True);
            Assert.That(_tab.Requests.Single().Summary, Does.Contain("shell only"));
        }

        [Test]
        public async Task Add_FormAlone_ReportsItsTableAsParentShell_NotUnexpected()
        {
            var components = new JArray(new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString() });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            var results = result.Payload["results"]!;
            Assert.That(results.Count(), Is.EqualTo(2));
            Assert.That((string)results[1]!["name"]!, Does.Contain("parent shell of entry 0"));
            Assert.That((int)results[1]!["type"]!, Is.EqualTo(1));
            Assert.That((int)result.Payload["unexpectedCount"]!, Is.Zero);
            Assert.That((int)result.Payload["addedCount"]!, Is.EqualTo(1), "only requested components count as added");
            Assert.That(_tab.CallLog.Snapshot().Single().Warnings, Is.Empty);
        }

        [Test]
        public async Task Add_SomethingElseArrives_IsUnexpected_AndWarnedInTheLog()
        {
            _data.Stowaway = (10, DiscoveryData.ParentRelationshipId, 0);
            var components = new JArray(new JObject { ["type"] = 1, ["logicalName"] = "account" });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            var unexpected = result.Payload["unexpected"]!.Single();
            Assert.That((int)unexpected["componentType"]!["code"]!, Is.EqualTo(10));
            Assert.That((string)unexpected["rootComponentBehavior"]!, Is.EqualTo("includeSubcomponents"));
            Assert.That((int)result.Payload["unexpectedCount"]!, Is.EqualTo(1));
            Assert.That(_tab.CallLog.Snapshot().Single().Warnings.Single(), Does.StartWith("  ⚠ unexpected EntityRelationship "));
        }

        [Test]
        public async Task Add_AShellArrivingForAnUnrequestedTable_IsUnexpected()
        {
            _data.Stowaway = (1, DiscoveryData.WidgetId, 2);
            var components = new JArray(new JObject { ["type"] = 2, ["table"] = "account", ["logicalName"] = "name" });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            Assert.That((string)result.Payload["unexpected"]!.Single()["name"]!, Is.EqualTo("new_widget"));
        }

        [Test]
        public async Task Add_AlreadyPresent_IsNotAddedAgain()
        {
            _data.Components.Add((1, DiscoveryData.AccountId, 1));
            var components = new JArray(new JObject { ["type"] = 1, ["logicalName"] = "account" }, new JObject { ["type"] = 9, ["name"] = "new_size" });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            Assert.That(result.Payload["results"]!.Select(r => (string)r["status"]!), Is.EqualTo(new[] { "alreadyPresent", "added" }));
            Assert.That(_data.Adds.Select(a => a.ComponentType), Is.EqualTo(new[] { 9 }));
        }

        [Test]
        public async Task Add_FirstFailureStopsTheRun_LaterEntriesAreNotAttempted()
        {
            _data.Failing.Add(DiscoveryData.NameColumnId);
            var components = new JArray(
                new JObject { ["type"] = 1, ["logicalName"] = "account" },
                new JObject { ["type"] = 2, ["table"] = "account", ["logicalName"] = "name" },
                new JObject { ["type"] = 9, ["name"] = "new_size" });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            Assert.That(result.IsError, Is.False, "a partial run is a successful result");
            Assert.That(result.Payload["results"]!.Select(r => (string)r["status"]!), Is.EqualTo(new[] { "added", "failed", "notAttempted" }));
            Assert.That((string)result.Payload["results"]![1]!["error"]!["code"]!, Is.EqualTo(ToolErrorCodes.DataverseError));
            Assert.That(_data.Adds, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Add_ToAManagedSolution_IsRefusedBeforeTheDialog()
        {
            var components = new JArray(new JObject { ["type"] = 1, ["logicalName"] = "account" });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "ManagedOne", ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ManagedSolution));
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_tab.CallLog.Snapshot().Single().Approval, Is.EqualTo("—"));
        }

        [TestCase("{\"type\":1,\"logicalName\":\"nosuchtable\"}")]
        [TestCase("{\"type\":60,\"id\":\"f0000000-0000-0000-0000-0000000000ff\"}")]
        public async Task Add_AnythingUnresolved_FailsTheWholeCallBeforeTheDialog(string missing)
        {
            var components = new JArray(new JObject { ["type"] = 9, ["name"] = "new_size" }, JObject.Parse(missing));

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_data.Adds, Is.Empty, "nothing is added");
        }

        [Test]
        public async Task Add_AnUnacceptedType_IsInvalidArgument()
        {
            var components = new JArray(new JObject { ["type"] = 29, ["id"] = ShapingData.WorkflowId.ToString() });

            var result = await Call(SolutionShapingTools.AddSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA", ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("components[0]"));
        }

        // ---- remove_solution_component ----

        [Test]
        public async Task Remove_TakesTheComponentOutOfTheSolutionOnly()
        {
            _data.Components.Add((60, DiscoveryData.MainFormId, null));

            var result = await Call(SolutionShapingTools.RemoveSolutionComponent, Args(new JObject { ["solutionUniqueName"] = "WipA", ["component"] = new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString() } }));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var remove = _data.Removes.Single();
            Assert.That((remove.ComponentType, remove.ComponentId, remove.SolutionUniqueName), Is.EqualTo((60, DiscoveryData.MainFormId, "WipA")));
            Assert.That((string)result.Payload["component"]!["name"]!, Is.EqualTo("Account"));
            AssertSchema(SolutionShapingTools.RemoveSolutionComponent, result);
        }

        [Test]
        public async Task Remove_NotInTheSolution_IsNotFound_WithoutDialog()
        {
            var result = await Call(SolutionShapingTools.RemoveSolutionComponent, Args(new JObject { ["solutionUniqueName"] = "WipA", ["component"] = new JObject { ["type"] = 1, ["logicalName"] = "account" } }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_data.Removes, Is.Empty);
        }

        // ---- create_publisher ----

        [Test]
        public async Task CreatePublisher_IsDisabledByDefault()
        {
            var result = await Call(PublisherTools.CreatePublisher, Args(PublisherArguments("p2", 10002)));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.ToolDisabled));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task CreatePublisher_WhenEnabled_AsksForTheTypedPrefix_AndWarnsOnASharedOptionPrefix()
        {
            _binding.PublisherCreationEnabled = true;

            var result = await Call(PublisherTools.CreatePublisher, Args(PublisherArguments("p2", 10001)));

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            var request = _tab.Requests.Single();
            Assert.That(request.Confirmation, Is.EqualTo("p2"));
            Assert.That(request.Options, Is.EqualTo(new[] { ApprovalDecision.AllowOnce }), "never for a session (R3)");
            Assert.That(request.Summary, Does.Contain("publisher1"));
            Assert.That(request.Summary, Does.Contain("Option pfx    10001   ⚠ same as an existing publisher"));
            Assert.That(request.Summary, Does.Not.Contain("same prefix"));
            Assert.That(_data.Created.Single()["customizationprefix"], Is.EqualTo("p2"));
            AssertSchema(PublisherTools.CreatePublisher, result);
        }

        [Test]
        public async Task CreatePublisher_WrongConfirmation_IsDeniedByUser()
        {
            _binding.PublisherCreationEnabled = true;
            _tab.Answer = (request, cancellation) => Task.FromResult(ApprovalDecision.Deny);

            var result = await Call(PublisherTools.CreatePublisher, Args(PublisherArguments("p2", 10002)));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.DeniedByUser));
            Assert.That(_data.Created, Is.Empty);
        }

        [Test]
        public async Task CreatePublisher_NameTaken_IsAlreadyExists()
        {
            _binding.PublisherCreationEnabled = true;
            var arguments = PublisherArguments("p2", 10002);
            arguments["uniqueName"] = "PUBLISHER1";

            var result = await Call(PublisherTools.CreatePublisher, Args(arguments));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.AlreadyExists));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [TestCase("prefix", "mscrmx")]
        [TestCase("prefix", "p")]
        [TestCase("prefix", "2abc")]
        [TestCase("prefix", "toolongprefix")]
        public async Task CreatePublisher_BadPrefix_IsInvalidArgument(string argument, string value)
        {
            _binding.PublisherCreationEnabled = true;
            var arguments = PublisherArguments("p2", 10002);
            arguments[argument] = value;

            var result = await Call(PublisherTools.CreatePublisher, Args(arguments));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [TestCase(9999)]
        [TestCase(100000)]
        public async Task CreatePublisher_OptionPrefixOutOfRange_IsInvalidArgument(int optionValuePrefix)
        {
            _binding.PublisherCreationEnabled = true;

            var result = await Call(PublisherTools.CreatePublisher, Args(PublisherArguments("p2", optionValuePrefix)));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["details"]!["argument"]!, Is.EqualTo("optionValuePrefix"));
        }

        private static JObject PublisherArguments(string prefix, int optionValuePrefix) => new JObject
        {
            ["uniqueName"] = "publisher2",
            ["friendlyName"] = "Publisher 2",
            ["prefix"] = prefix,
            ["optionValuePrefix"] = optionValuePrefix,
        };

        private static JObject Args(JObject more)
        {
            var arguments = new JObject { ["connection"] = Conn };
            foreach (var property in more.Properties())
            {
                arguments[property.Name] = property.Value;
            }

            arguments["reason"] = Reason;
            return arguments;
        }

        private static string? Code(ToolResult result) => (string?)result.Payload["error"]?["code"];

        private void AssertSchema(string tool, ToolResult result) =>
            Assert.That(SchemaValidator.Validate(result.Payload, _host.Dispatcher.Tools.Single(t => t.Name == tool).OutputSchema), Is.Empty, tool);

        private Task<ToolResult> Call(string tool, JObject arguments) =>
            _host.Dispatcher.CallAsync(new McpToolCall(tool, arguments, _clock.UtcNow), default);
    }
}
