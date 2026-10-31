using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using Moq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>
    /// The Phase 2 discovery tools against a synthetic environment: shapes, first-page fields, chunked ribbons,
    /// query rules and record detection, and component name resolution (SPEC §6.2, §6.5).
    /// </summary>
    [TestFixture]
    public class DiscoveryToolsTests
    {
        private const string Conn = "aaaaaaaa-1111-1111-1111-111111111111";
        private static readonly Guid Org = new Guid("aaaaaaaa-0000-0000-0000-00000000000a");

        private FakeClock _clock = null!;
        private CompanionHost _host = null!;
        private FakeInstance _tab = null!;
        private Mock<IOrganizationService> _dataverse = null!;
        private BoundConnection _binding = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _host = TestHost.Create(_clock);
            _tab = new FakeInstance();
            _host.Attach(_tab);
            _dataverse = DiscoveryData.Add(Dataverse.Mock(Org, Guid.NewGuid()));
            _binding = TestHost.Bind(_host, _tab, Org, Conn, _dataverse.Object);
        }

        [Test]
        public async Task EveryPhase2Result_ValidatesAgainstItsOutputSchema()
        {
            var calls = new (string Tool, JObject Arguments)[]
            {
                (SolutionTools.ListSolutions, Args()),
                (MetadataTools.ListTables, Args()),
                (MetadataTools.GetTableMetadata, Args(new JObject { ["logicalName"] = "account" })),
                (MetadataTools.GetRelationships, Args(new JObject { ["logicalName"] = "account" })),
                (MetadataTools.GetOptionSets, Args()),
                (RibbonTools.GetEntityRibbon, Args(new JObject { ["logicalName"] = "account" })),
                (RibbonTools.GetApplicationRibbon, Args()),
                (QueryTool.Query, Args(new JObject { ["fetchXml"] = FormsFetch })),
                (SolutionTools.ListSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA" })),
                (SolutionTools.GetComponentState, Args(new JObject { ["components"] = new JArray(new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString() }) })),
                (SolutionTools.GetRequiredComponents, Args(new JObject { ["components"] = new JArray(new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString() }) })),
            };

            foreach (var (tool, arguments) in calls)
            {
                var result = await Call(tool, arguments);
                var schema = _host.Dispatcher.Tools.Single(t => t.Name == tool).OutputSchema;
                Assert.That(result.IsError, Is.False, $"{tool}: {result.Payload}");
                Assert.That(SchemaValidator.Validate(result.Payload, schema), Is.Empty, tool);
            }
        }

        // ---- metadata ----

        [Test]
        public async Task ListTables_FiltersCustom_AndMatchesTheDisplayName()
        {
            var custom = await Call(MetadataTools.ListTables, Args(new JObject { ["filter"] = "custom" }));
            var byDisplayName = await Call(MetadataTools.ListTables, Args(new JObject { ["nameContains"] = "WIDG" }));

            Assert.That(custom.Payload["tables"]!.Select(t => (string)t["logicalName"]!), Is.EqualTo(new[] { "new_widget" }));
            Assert.That(byDisplayName.Payload["tables"]!.Single()["displayName"]!.ToString(), Is.EqualTo("Widget"));
        }

        [Test]
        public async Task TableMetadata_TableAndKeysComeOnTheFirstPageOnly()
        {
            var first = await Call(MetadataTools.GetTableMetadata, Args(new JObject { ["logicalName"] = "account", ["pageSize"] = 2 }));
            var second = await Call(MetadataTools.GetTableMetadata, new JObject { ["connection"] = Conn, ["cursor"] = first.Payload["nextCursor"] });

            Assert.That(first.Payload["table"]!["primaryNameAttribute"]!.ToString(), Is.EqualTo("name"));
            Assert.That(first.Payload["table"]!["ownershipType"]!.ToString(), Is.EqualTo("userOwned"));
            Assert.That(first.Payload["keys"]!.Single()["keyAttributes"]!.Single()!.ToString(), Is.EqualTo("accountnumber"));
            Assert.That(first.Payload["columns"]!.Select(c => (string)c["logicalName"]!), Is.EqualTo(new[] { "name", "new_flag" }), "ordered by logical name");
            Assert.That(second.Payload["table"], Is.Null);
            Assert.That(second.Payload["keys"], Is.Null);
            Assert.That(second.Payload["columns"]!.Select(c => (string)c["logicalName"]!), Is.EqualTo(new[] { "new_size", "parentaccountid" }));
            Assert.That(second.Payload["nextCursor"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public async Task TableMetadata_ProjectsColumnTypes()
        {
            var result = await Call(MetadataTools.GetTableMetadata, Args(new JObject { ["logicalName"] = "account" }));
            var columns = result.Payload["columns"]!.ToDictionary(c => (string)c["logicalName"]!);

            Assert.That((int)columns["name"]["maxLength"]!, Is.EqualTo(160));
            Assert.That(columns["name"]["requiredLevel"]!.ToString(), Is.EqualTo("applicationRequired"));
            Assert.That(columns["parentaccountid"]["targets"]!.Single()!.ToString(), Is.EqualTo("account"));
            Assert.That(columns["new_flag"]["optionSet"]!["options"]!.Select(o => (int)o["value"]!), Is.EqualTo(new[] { 1, 0 }), "Yes/No as its two options");
            Assert.That(columns["new_size"]["optionSet"]!["isGlobal"]!.Value<bool>(), Is.True);
            Assert.That(columns["new_size"]["optionSet"]!["options"]!.Type, Is.EqualTo(JTokenType.Null), "global choices: use get_option_sets");
        }

        [Test]
        public async Task TableMetadata_KeysOnly_OmitsColumns()
        {
            var result = await Call(MetadataTools.GetTableMetadata, Args(new JObject { ["logicalName"] = "account", ["include"] = new JArray("keys") }));

            Assert.That(result.Payload["columns"], Is.Null);
            Assert.That(result.Payload["keys"], Is.Not.Null);
        }

        [Test]
        public async Task TableMetadata_UnknownTable_IsNotFound()
        {
            var result = await Call(MetadataTools.GetTableMetadata, Args(new JObject { ["logicalName"] = "nosuchtable" }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotFound));
            Assert.That(result.Payload["error"]!["details"]!["name"]!.ToString(), Is.EqualTo("nosuchtable"));
        }

        [Test]
        public async Task IncludeWithAnUnknownValue_IsInvalidArgument()
        {
            var result = await Call(MetadataTools.GetTableMetadata, Args(new JObject { ["logicalName"] = "account", ["include"] = new JArray("forms") }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(result.Payload["error"]!["details"]!["argument"]!.ToString(), Is.EqualTo("include[0]"));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task Relationships_SelfReferenceAppearsAsBothKinds()
        {
            var all = await Call(MetadataTools.GetRelationships, Args(new JObject { ["logicalName"] = "account" }));
            var manyToMany = await Call(MetadataTools.GetRelationships, Args(new JObject { ["logicalName"] = "account", ["kinds"] = new JArray("manyToMany") }));

            Assert.That(all.Payload["logicalName"]!.ToString(), Is.EqualTo("account"));
            Assert.That(all.Payload["relationships"]!.Select(r => (string)r["kind"]!), Is.EqualTo(new[] { "oneToMany", "manyToOne", "manyToMany" }));
            Assert.That(all.Payload["relationships"]![0]!["cascade"]!["delete"]!.ToString(), Is.EqualTo("removeLink"));
            Assert.That(manyToMany.Payload["relationships"]!.Single()["intersectEntityName"]!.ToString(), Is.EqualTo("new_account_widget"));
        }

        [Test]
        public async Task OptionSets_ListNamesNotFound_OnTheFirstPage()
        {
            var result = await Call(MetadataTools.GetOptionSets, Args(new JObject { ["names"] = new JArray("NEW_SIZE", "new_missing") }));

            Assert.That(result.Payload["optionSets"]!.Single()["options"]!.Select(o => (string)o["label"]!), Is.EqualTo(new[] { "Small", "Large" }));
            Assert.That(result.Payload["notFound"]!.Select(n => (string)n!), Is.EqualTo(new[] { "new_missing" }));
        }

        // ---- ribbons ----

        [Test]
        public async Task EntityRibbon_IsChunkedWithinTheCap_AndConcatenatesToTheDocument()
        {
            const int cap = 2000;
            _host.Settings.Update(s => s.ResponseCapCharacters = cap);

            var xml = new StringBuilder();
            var arguments = Args(new JObject { ["logicalName"] = "account", ["location"] = "form", ["pageSize"] = 50 });
            var pages = 0;
            while (true)
            {
                var result = await Call(RibbonTools.GetEntityRibbon, arguments);
                Assert.That(result.IsError, Is.False, result.Payload.ToString());
                Assert.That(result.Payload.ToString(Formatting.None).Length, Is.LessThanOrEqualTo(cap), $"page {pages}");
                Assert.That(result.Payload["location"]!.ToString(), Is.EqualTo("form"));
                xml.Append((string)result.Payload["ribbonXml"]!);
                pages++;
                if (result.Payload["nextCursor"]!.Type == JTokenType.Null)
                {
                    Assert.That((int)result.Payload["sizeBytes"]!, Is.EqualTo(Encoding.UTF8.GetByteCount(DiscoveryData.RibbonXml)));
                    break;
                }

                arguments = new JObject { ["connection"] = Conn, ["cursor"] = result.Payload["nextCursor"] };
            }

            Assert.That(pages, Is.GreaterThan(3));
            Assert.That(xml.ToString(), Is.EqualTo(DiscoveryData.RibbonXml));
            Assert.That(_tab.Requests, Has.Count.EqualTo(1), "continuation pages run under the first call's approval");
            _dataverse.Verify(s => s.Execute(It.Is<RetrieveEntityRibbonRequest>(r => r.RibbonLocationFilter == RibbonLocationFilters.Form)), Times.Once);
        }

        [Test]
        public async Task ApplicationRibbon_HasNoTableOrLocation()
        {
            var result = await Call(RibbonTools.GetApplicationRibbon, Args());

            Assert.That(result.Payload["ribbonXml"]!.ToString(), Is.EqualTo("<RibbonDefinitions />"));
            Assert.That(result.Payload["logicalName"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        // ---- query ----

        [Test]
        public async Task Query_PagesWithCookies_AndSerializesValues()
        {
            FetchExpression? last = null;
            _dataverse.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns((QueryBase q) =>
            {
                last = (FetchExpression)q;
                return DiscoveryData.FetchPage(XDocument.Parse(last.Query));
            });

            var first = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = FormsFetch, ["pageSize"] = 2 }));
            var second = await Call(QueryTool.Query, new JObject { ["connection"] = Conn, ["cursor"] = first.Payload["nextCursor"] });

            Assert.That(first.Payload["entityName"]!.ToString(), Is.EqualTo("systemform"));
            Assert.That(first.Payload["touchesRecords"]!.Value<bool>(), Is.False);
            Assert.That(first.Payload["records"]![0]!["type"]!.ToString(Formatting.None), Is.EqualTo("{\"_option\":2,\"label\":\"Main\"}"));
            Assert.That(second.Payload["records"]!.Select(r => (string)r["name"]!), Is.EqualTo(new[] { "Form 3", "Form 4" }));

            // The pager reads one batch ahead, so after two pages the last request is for page 3, with page 2's cookie.
            var fetch = XDocument.Parse(last!.Query).Root!;
            Assert.That(fetch.Attribute("page")!.Value, Is.EqualTo("3"));
            Assert.That(fetch.Attribute("count")!.Value, Is.EqualTo("2"));
            Assert.That(fetch.Attribute("paging-cookie")!.Value, Is.EqualTo("<cookie page=\"2\" />"));
        }

        [Test]
        public async Task Query_WithTop_IsRefusedBeforeAnyDialog()
        {
            var result = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = "<fetch top=\"5\"><entity name=\"systemform\"><attribute name=\"name\" /></entity></fetch>" }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string)result.Payload["error"]!["message"]!, Does.Contain("pageSize"));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task Query_WithAllAttributes_IsRefused()
        {
            var result = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = "<fetch><entity name=\"systemform\"><all-attributes /></entity></fetch>" }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task Query_OnRecords_IsFlaggedInTheDialog()
        {
            var result = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = AccountsFetch }));

            Assert.That(result.IsError, Is.False);
            Assert.That(result.Payload["touchesRecords"]!.Value<bool>(), Is.True);
            Assert.That(_tab.Requests.Single().TouchesRecords, Is.True);
        }

        [Test]
        public async Task Query_LinkToARecordTable_TouchesRecords()
        {
            var fetch = "<fetch><entity name=\"systemform\"><attribute name=\"name\" /><link-entity name=\"contact\" from=\"contactid\" to=\"ownerid\"><attribute name=\"fullname\" /></link-entity></entity></fetch>";

            var result = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = fetch }));

            Assert.That(_tab.Requests.Single().TouchesRecords, Is.True);
            Assert.That(result.Payload["touchesRecords"]!.Value<bool>(), Is.True);
        }

        [Test]
        public async Task Query_OnRecords_WhenSwitchedOff_IsToolDisabledWithoutDialog()
        {
            _binding.RecordQueriesEnabled = false;

            var records = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = AccountsFetch }));
            var configuration = await Call(QueryTool.Query, Args(new JObject { ["fetchXml"] = FormsFetch }));

            Assert.That(Code(records), Is.EqualTo(ToolErrorCodes.ToolDisabled));
            Assert.That(configuration.IsError, Is.False, "configuration queries still run");
            Assert.That(_tab.Requests, Has.Count.EqualTo(1));
            Assert.That(_tab.CallLog.Snapshot().First().OutcomeLine, Does.StartWith("  → error tool_disabled"));
        }

        // ---- solutions ----

        [Test]
        public async Task ListSolutions_HidesHiddenSolutions_AndFiltersByPublisherOnTheLink()
        {
            QueryExpression? captured = null;
            _dataverse.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Callback((QueryBase q) => captured = (QueryExpression)q).Returns(new EntityCollection());

            await Call(SolutionTools.ListSolutions, Args(new JObject { ["publisherUniqueName"] = "publisher1", ["managed"] = "unmanaged" }));

            Assert.That(captured!.Criteria.Conditions.Select(c => c.AttributeName), Is.EquivalentTo(new[] { "ismanaged", "isvisible" }));
            Assert.That(captured.LinkEntities.Single().LinkCriteria.Conditions.Single().Values.Single(), Is.EqualTo("publisher1"));
        }

        [Test]
        public async Task ListSolutions_ReadsThePublisherFromTheLink()
        {
            var result = await Call(SolutionTools.ListSolutions, Args());

            var solution = result.Payload["solutions"]!.Single();
            Assert.That(solution["publisher"]!["customizationPrefix"]!.ToString(), Is.EqualTo("p1"));
            Assert.That(solution["installedOn"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public async Task SolutionComponents_ResolveNamesAndBehaviours()
        {
            var result = await Call(SolutionTools.ListSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "WipA" }));
            var components = result.Payload["components"]!;

            Assert.That(result.Payload["solution"]!["uniqueName"]!.ToString(), Is.EqualTo("WipA"));
            Assert.That(components.Select(c => (string?)c["name"]), Is.EqualTo(new[] { "account", "account.name", "Account", null }));
            Assert.That(components[0]!["rootComponentBehavior"]!.ToString(), Is.EqualTo("includeAsShellOnly"));
            Assert.That(components[1]!["rootComponentBehavior"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(components[3]!["componentType"]!["name"]!.ToString(), Is.EqualTo("Workflow"));
        }

        [Test]
        public async Task SolutionComponents_UnknownSolution_IsNotFound()
        {
            var result = await Call(SolutionTools.ListSolutionComponents, Args(new JObject { ["solutionUniqueName"] = "Nope" }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotFound));
        }

        [Test]
        public async Task ComponentState_ReportsVersionsAsDecimalStrings_AndMissingAsNotFound()
        {
            var components = new JArray(
                new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString("B").ToUpperInvariant() },
                new JObject { ["type"] = 60, ["id"] = DiscoveryData.MissingFormId.ToString() });

            var result = await Call(SolutionTools.GetComponentState, Args(new JObject { ["components"] = components }));
            var states = result.Payload["components"]!;

            Assert.That(states[0]!["found"]!.Value<bool>(), Is.True);
            Assert.That(states[0]!["id"]!.ToString(), Is.EqualTo(DiscoveryData.MainFormId.ToString()));
            Assert.That(states[0]!["versionNumber"]!.ToString(), Is.EqualTo("9007199254740993"));
            Assert.That(states[0]!["modifiedBy"]!["name"]!.ToString(), Is.EqualTo("Test User"));
            Assert.That(states[1]!["found"]!.Value<bool>(), Is.False);
        }

        [Test]
        public async Task ComponentState_OtherTypes_AreInvalidArgumentNamingTheEntries()
        {
            var components = new JArray(
                new JObject { ["type"] = 60, ["id"] = DiscoveryData.MainFormId.ToString() },
                new JObject { ["type"] = 1, ["id"] = DiscoveryData.AccountId.ToString() });

            var result = await Call(SolutionTools.GetComponentState, Args(new JObject { ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(result.Payload["error"]!["details"]!["entries"]!.Select(e => (int)e), Is.EqualTo(new[] { 1 }));
            Assert.That(_tab.Requests, Is.Empty, "refused before the dialog");
        }

        [Test]
        public async Task ComponentState_TooManyEntries_IsInvalidArgument()
        {
            var components = new JArray(Enumerable.Range(0, 201).Select(i => new JObject { ["type"] = 60, ["id"] = Guid.NewGuid().ToString() }));

            var result = await Call(SolutionTools.GetComponentState, Args(new JObject { ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(_tab.Requests, Is.Empty);
        }

        [Test]
        public async Task RequiredComponents_ResolveSelectorsByName_AndListHoldingSolutionsOnce()
        {
            var components = new JArray(new JObject { ["type"] = 2, ["table"] = "account", ["logicalName"] = "name" });

            var result = await Call(SolutionTools.GetRequiredComponents, Args(new JObject { ["components"] = components }));
            var dependency = result.Payload["dependencies"]!.Single();

            Assert.That(dependency["dependent"]!["id"]!.ToString(), Is.EqualTo(DiscoveryData.NameColumnId.ToString()));
            Assert.That(dependency["dependent"]!["name"]!.ToString(), Is.EqualTo("account.name"));
            Assert.That(dependency["required"]!["name"]!.ToString(), Is.EqualTo("account"));
            Assert.That(dependency["required"]!["solutions"]!.Select(s => (string)s["uniqueName"]!), Is.EqualTo(new[] { "Base" }));
            Assert.That(dependency["dependencyType"]!.ToString(), Is.EqualTo("published"));
        }

        [Test]
        public async Task RequiredComponents_UnknownName_FailsTheWholeCall()
        {
            var components = new JArray(
                new JObject { ["type"] = 1, ["logicalName"] = "account" },
                new JObject { ["type"] = 9, ["name"] = "new_missing" });

            var result = await Call(SolutionTools.GetRequiredComponents, Args(new JObject { ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.NotFound));
            _dataverse.Verify(s => s.Execute(It.IsAny<RetrieveRequiredComponentsRequest>()), Times.Never);
        }

        [Test]
        public async Task RequiredComponents_SelectorByNameForOtherTypes_IsInvalidArgument()
        {
            var components = new JArray(new JObject { ["type"] = 60, ["name"] = "Account" });

            var result = await Call(SolutionTools.GetRequiredComponents, Args(new JObject { ["components"] = components }));

            Assert.That(Code(result), Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(result.Payload["error"]!["details"]!["argument"]!.ToString(), Is.EqualTo("components[0]"));
        }

        private const string FormsFetch = "<fetch><entity name=\"systemform\"><attribute name=\"formid\" /><attribute name=\"name\" /><attribute name=\"type\" /><attribute name=\"objecttypecode\" /></entity></fetch>";

        private const string AccountsFetch = "<fetch><entity name=\"account\"><attribute name=\"name\" /></entity></fetch>";

        private static JObject Args(JObject? more = null)
        {
            var arguments = new JObject { ["connection"] = Conn };
            foreach (var property in more?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                arguments[property.Name] = property.Value;
            }

            return arguments;
        }

        private static string? Code(ToolResult result) => (string?)result.Payload["error"]?["code"];

        private Task<ToolResult> Call(string tool, JObject arguments) =>
            _host.Dispatcher.CallAsync(new McpToolCall(tool, arguments, _clock.UtcNow), default);
    }
}
