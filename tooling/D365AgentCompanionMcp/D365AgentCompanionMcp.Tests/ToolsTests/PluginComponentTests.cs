using System;
using System.Linq;
using System.Threading.Tasks;

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
    /// Plug-in assemblies, steps and custom APIs accepted by <c>add_solution_components</c> (SPEC §6.6, Phase 6):
    /// selectors by name and by <c>componentTable</c>, codes read per environment, and the read-back. Registration
    /// itself stays a person's.
    /// </summary>
    [TestFixture]
    public class PluginComponentTests
    {
        private const string Conn = "aaaaaaaa-1111-1111-1111-111111111111";
        private static readonly Guid Org = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");

        private FakeClock _clock = null!;
        private CompanionHost _host = null!;
        private FakeInstance _tab = null!;
        private ShapingData _data = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _host = TestHost.Create(_clock);
            _tab = new FakeInstance();
            _host.Attach(_tab);
            var dataverse = Dataverse.Mock(Org, Guid.NewGuid());
            _data = new ShapingData(dataverse);
            TestHost.Bind(_host, _tab, Org, Conn, dataverse.Object).DevConfirmedOnce = true;
        }

        [Test]
        public async Task AnAssembly_IsAddedByName_WhenOnlyOneVersionIsRegistered()
        {
            var result = await Add(new JObject { ["type"] = 91, ["name"] = "Solo.Plugins" });

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Adds.Single().ComponentType, Is.EqualTo(91));
            Assert.That(_data.Adds.Single().ComponentId, Is.EqualTo(ShapingData.SoloAssemblyId));
            Assert.That((string?)result.Payload["results"]![0]!["name"], Is.EqualTo("Solo.Plugins 1.0.0.0"));
            Assert.That(_data.Adds.Single().AddRequiredComponents, Is.False, "required components stay off (R1)");
        }

        [Test]
        public async Task AnAssemblyWithSeveralVersions_NeedsAVersion_AndTheErrorListsThem()
        {
            var ambiguous = await Add(new JObject { ["type"] = 91, ["name"] = "Acme.Plugins" });

            Assert.That(Code(ambiguous), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(ambiguous.Payload["error"]!["details"]!["versions"]!.Select(v => (string?)v), Is.EqualTo(new[] { "1.0.0.0", "2.0.0.0" }));
            Assert.That(_data.Adds, Is.Empty);

            var chosen = await Add(new JObject { ["type"] = 91, ["name"] = "Acme.Plugins", ["version"] = "2.0.0.0" });

            Assert.That(chosen.IsError, Is.False, chosen.Payload.ToString());
            Assert.That(_data.Adds.Single().ComponentId, Is.EqualTo(ShapingData.AssemblyV2Id));
        }

        [Test]
        public async Task AnUnknownAssemblyOrVersion_IsNotFound_AndNothingIsAdded()
        {
            Assert.That(Code(await Add(new JObject { ["type"] = 91, ["name"] = "Missing.Plugins" })), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(Code(await Add(new JObject { ["type"] = 91, ["name"] = "Acme.Plugins", ["version"] = "9.0.0.0" })), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(_data.Adds, Is.Empty);
        }

        [Test]
        public async Task AStep_IsAddedByIdOnly_AndNamedInTheDialog()
        {
            var result = await Add(new JObject { ["type"] = 92, ["id"] = ShapingData.StepId.ToString() });

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Adds.Single().ComponentType, Is.EqualTo(92));
            Assert.That(_tab.Requests.Single().Summary, Does.Contain("Acme.Plugins.AccountPlugin: Update of account"));

            var byName = await Add(new JObject { ["type"] = 92, ["name"] = "step" });
            Assert.That(Code(byName), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)byName.Payload["error"]!["message"]!, Does.Contain("by id"));
        }

        [Test]
        public async Task ACustomApi_IsAddedByUniqueName_WithTheTypeCodeReadFromTheEnvironment()
        {
            var result = await Add(new JObject { ["componentTable"] = "customapi", ["uniqueName"] = "new_Api" });

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Adds.Single().ComponentType, Is.EqualTo(ShapingData.CustomApiCode));
            Assert.That(_data.Adds.Single().ComponentId, Is.EqualTo(ShapingData.CustomApiId));
            var item = result.Payload["results"]![0]!;
            Assert.That((int)item["type"]!, Is.EqualTo(ShapingData.CustomApiCode), "the code is reported in results");
            Assert.That((string?)item["name"], Is.EqualTo("new_Api"));
            Assert.That(_tab.Requests.Single().Summary, Does.Contain("CustomAPI").And.Contain("new_Api"));
        }

        [Test]
        public async Task ARequestParameterAndAResponseProperty_AreAddedByTheirApiAndUniqueName()
        {
            var result = await Add(
                new JObject { ["componentTable"] = "customapirequestparameter", ["customApi"] = "new_Api", ["uniqueName"] = "p1" },
                new JObject { ["componentTable"] = "customapiresponseproperty", ["customApi"] = "new_Api", ["uniqueName"] = "out1" });

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Adds.Select(a => (a.ComponentType, a.ComponentId)), Is.EqualTo(new[]
            {
                (ShapingData.ParameterCode, ShapingData.ParameterId),
                (ShapingData.PropertyCode, ShapingData.PropertyId),
            }), "the parameter of the named API, not the same-named parameter of another");
            Assert.That(result.Payload["results"]!.Select(r => (string?)r["name"]), Is.EqualTo(new[] { "new_Api.p1", "new_Api.out1" }));
        }

        [Test]
        public async Task ACustomApiComponent_CanBeSelectedByIdThroughItsTable()
        {
            var result = await Add(new JObject { ["componentTable"] = "customapirequestparameter", ["id"] = ShapingData.ParameterId.ToString() });

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Adds.Single().ComponentType, Is.EqualTo(ShapingData.ParameterCode));
        }

        [Test]
        public async Task ANumericCodeOf10000OrAbove_IsRefused_BecauseItMeansSomethingElseElsewhere()
        {
            var result = await Add(new JObject { ["type"] = ShapingData.CustomApiCode, ["id"] = ShapingData.CustomApiId.ToString() });

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["message"]!, Does.Contain("componentTable"));
            Assert.That(_tab.Requests, Is.Empty);
            Assert.That(_data.Adds, Is.Empty);
        }

        [Test]
        public async Task AnUnknownCustomApiOrParameter_IsNotFound_AndNothingIsAdded()
        {
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "customapi", ["uniqueName"] = "new_Missing" })), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "customapirequestparameter", ["customApi"] = "new_Api", ["uniqueName"] = "nope" })), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "customapirequestparameter", ["customApi"] = "new_Missing", ["uniqueName"] = "p1" })), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(_data.Adds, Is.Empty);
        }

        [Test]
        public async Task ABadComponentTable_OrMissingFields_IsInvalidArgument()
        {
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "account", ["id"] = Guid.NewGuid().ToString() })), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "customapi" })), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "customapirequestparameter", ["uniqueName"] = "p1" })), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(Code(await Add(new JObject { ["componentTable"] = "customapi", ["type"] = 1, ["uniqueName"] = "new_Api" })), Is.EqualTo(ToolErrorCodes.InvalidArgument));
        }

        [Test]
        public async Task WhatThePlatformAddsWithACustomApi_IsReportedAsUnexpected_NamedByItsTable()
        {
            _data.Stowaway = (ShapingData.ParameterCode, ShapingData.ParameterId, null);

            var result = await Add(new JObject { ["componentTable"] = "customapi", ["uniqueName"] = "new_Api" });

            Assert.That(result.Payload["unexpectedCount"]!.Value<int>(), Is.EqualTo(1));
            var unexpected = result.Payload["unexpected"]![0]!;
            Assert.That((int)unexpected["componentType"]!["code"]!, Is.EqualTo(ShapingData.ParameterCode));
            Assert.That((string?)unexpected["componentType"]!["name"], Is.EqualTo("customapirequestparameter"));
            Assert.That(_tab.CallLog.Snapshot().Single().Warnings.Single(), Does.Contain("customapirequestparameter"));
        }

        [Test]
        public async Task ACustomApiAlreadyInTheWip_IsAlreadyPresent_NotAddedAgain()
        {
            _data.Components.Add((ShapingData.CustomApiCode, ShapingData.CustomApiId, null));

            var result = await Add(new JObject { ["componentTable"] = "customapi", ["uniqueName"] = "new_Api" });

            Assert.That((string?)result.Payload["results"]![0]!["status"], Is.EqualTo("alreadyPresent"));
            Assert.That(_data.Adds, Is.Empty);
        }

        [Test]
        public async Task TheSameSelectorsServeRemove()
        {
            _data.Components.Add((ShapingData.CustomApiCode, ShapingData.CustomApiId, null));

            var result = await _host.Dispatcher.CallAsync(
                new McpToolCall(
                    SolutionShapingTools.RemoveSolutionComponent,
                    new JObject
                    {
                        ["connection"] = Conn,
                        ["solutionUniqueName"] = "WipA",
                        ["component"] = new JObject { ["componentTable"] = "customapi", ["uniqueName"] = "new_Api" },
                        ["reason"] = "Story 1234.",
                    },
                    _clock.UtcNow),
                default);

            Assert.That(result.IsError, Is.False, result.Payload.ToString());
            Assert.That(_data.Removes.Single().ComponentType, Is.EqualTo(ShapingData.CustomApiCode));
        }

        [Test]
        public async Task TheSelectorsServeGetRequiredComponents()
        {
            var result = await _host.Dispatcher.CallAsync(
                new McpToolCall(
                    SolutionTools.GetRequiredComponents,
                    new JObject
                    {
                        ["connection"] = Conn,
                        ["components"] = new JArray(new JObject { ["type"] = 91, ["name"] = "Solo.Plugins" }),
                    },
                    _clock.UtcNow),
                default);

            Assert.That(Code(result), Is.Not.EqualTo(ToolErrorCodes.InvalidArgument), "the selector parses and resolves");
        }

        private static string? Code(ToolResult result) => (string?)result.Payload["error"]?["code"];

        private Task<ToolResult> Add(params JObject[] components) =>
            _host.Dispatcher.CallAsync(
                new McpToolCall(
                    SolutionShapingTools.AddSolutionComponents,
                    new JObject
                    {
                        ["connection"] = Conn,
                        ["solutionUniqueName"] = "WipA",
                        ["components"] = new JArray(components),
                        ["reason"] = "Story 1234: register the plug-in components.",
                    },
                    _clock.UtcNow),
                default);
    }
}
