using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Microsoft.Xrm.Sdk.Query;

using Moq;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>The tool schemas of SPEC §6.2 and §6.3.</summary>
    [TestFixture]
    public class ToolCatalogTests
    {
        private JArray _tools = null!;

        [SetUp]
        public void SetUp()
        {
            _tools = TestHost.Create().Dispatcher.ListTools();
        }

        [Test]
        public void ToolsList_IsInTheFixedOrder()
        {
            Assert.That(_tools.Select(t => (string)t["name"]!), Is.EqualTo(new[]
            {
                "list_connections", "get_connection_info", "who_am_i", "list_publishers", "list_solutions", "list_tables",
                "get_table_metadata", "get_relationships", "get_option_sets", "get_entity_ribbon", "get_application_ribbon",
                "query", "list_solution_components", "get_component_state", "get_required_components",
                "create_solution", "add_solution_components", "remove_solution_component", "create_publisher",
            }));
        }

        [Test]
        public void EveryTool_HasTitleDescriptionAndBothSchemas()
        {
            foreach (var tool in _tools)
            {
                Assert.That((string?)tool["title"], Is.Not.Empty);
                Assert.That((string?)tool["description"], Is.Not.Empty);
                Assert.That(tool["inputSchema"]!["type"]!.ToString(), Is.EqualTo("object"));
                Assert.That(tool["outputSchema"]!["type"]!.ToString(), Is.EqualTo("object"));
            }
        }

        [Test]
        public void EveryToolButListConnections_TakesConnectionFirstAndRequired()
        {
            foreach (var tool in _tools.Where(t => (string)t["name"]! != "list_connections"))
            {
                var schema = tool["inputSchema"]!;
                Assert.That(((JObject)schema["properties"]!).Properties().First().Name, Is.EqualTo("connection"), (string)tool["name"]!);
                Assert.That(schema["required"]![0]!.ToString(), Is.EqualTo("connection"), (string)tool["name"]!);
            }
        }

        [Test]
        public void ListConnections_TakesNoArguments()
        {
            var schema = _tools.Single(t => (string)t["name"]! == "list_connections")["inputSchema"]!;

            Assert.That(((JObject)schema["properties"]!).Count, Is.Zero);
        }

        [Test]
        public void InputSchemas_RejectAdditionalProperties_OutputSchemasDoNot()
        {
            foreach (var tool in _tools)
            {
                Assert.That(tool["inputSchema"]!["additionalProperties"]!.Value<bool>(), Is.False);
                Assert.That(tool["outputSchema"]!["additionalProperties"], Is.Null);
            }
        }

        [Test]
        public void ReadTools_TakeAnOptionalReasonOfAtMost500Characters()
        {
            foreach (var tool in TestHost.Create().Dispatcher.Tools.Where(t => t.Tier == ToolTier.Read))
            {
                Assert.That((int)tool.InputSchema["properties"]!["reason"]!["maxLength"]!, Is.EqualTo(500), tool.Name);
                Assert.That(tool.InputSchema["required"]!.Values<string>(), Does.Not.Contain("reason"), tool.Name);
            }
        }

        [Test]
        public void WriteAndPublisherTools_RequireANonBlankReasonLast()
        {
            var tools = TestHost.Create().Dispatcher.Tools.Where(t => t.Tier == ToolTier.Write || t.Tier == ToolTier.Publisher).ToList();

            Assert.That(tools.Select(t => t.Name), Is.EquivalentTo(new[] { "create_solution", "add_solution_components", "remove_solution_component", "create_publisher" }));
            foreach (var tool in tools)
            {
                Assert.That(tool.InputSchema["required"]!.Values<string>(), Does.Contain("reason"), tool.Name);
                Assert.That((int)tool.InputSchema["properties"]!["reason"]!["minLength"]!, Is.EqualTo(1), tool.Name);
                Assert.That(tool.InputOrder.Last(), Is.EqualTo("reason"), tool.Name);
            }
        }

        [Test]
        public void WriteToolSchema_RequiresANonEmptyReason()
        {
            var schema = InputSchema.ForConnection().WithReason(required: true).Build();

            Assert.That(schema["required"]!.Values<string>(), Does.Contain("reason"));
            Assert.That((int)schema["properties"]!["reason"]!["minLength"]!, Is.EqualTo(1));
        }

        [Test]
        public void PagedTool_TakesCursorAndPageSize_AndReturnsNextCursor()
        {
            var tool = _tools.Single(t => (string)t["name"]! == "list_publishers");

            Assert.That(tool["inputSchema"]!["properties"]!["cursor"], Is.Not.Null);
            Assert.That((int)tool["inputSchema"]!["properties"]!["pageSize"]!["default"]!, Is.EqualTo(100));
            Assert.That(tool["outputSchema"]!["required"]!.Values<string>(), Does.Contain("nextCursor"));
        }

        [Test]
        public void EscapeLike_EscapesWildcards()
        {
            Assert.That(PublisherTools.EscapeLike("a_b%c[d"), Is.EqualTo("a[_]b[%]c[[]d"));
        }

        [Test]
        public void ListPublishers_NameContains_MatchesUniqueOrFriendlyName()
        {
            QueryExpression? captured = null;
            var service = Dataverse.Mock(System.Guid.NewGuid(), System.Guid.NewGuid());
            service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Callback((QueryBase q) => captured = (QueryExpression)q)
                .Returns(new Microsoft.Xrm.Sdk.EntityCollection());
            var host = TestHost.Create();
            var tab = new FakeInstance();
            host.Attach(tab);
            var binding = TestHost.Bind(host, tab, System.Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", service.Object);
            var tool = host.Dispatcher.Tools.Single(t => t.Name == PublisherTools.ListPublishers);

            var source = tool.OpenPages!(new ToolExecution(host, binding, new JObject { ["connection"] = binding.ConnectionId, ["nameContains"] = "con_toso" }));
            source.FetchAsync(10).GetAwaiter().GetResult();

            var filter = captured!.Criteria.Filters.Single();
            Assert.That(filter.FilterOperator, Is.EqualTo(LogicalOperator.Or));
            Assert.That(filter.Conditions.Select(c => c.AttributeName), Is.EquivalentTo(new[] { "uniquename", "friendlyname" }));
            Assert.That(filter.Conditions.All(c => (string)c.Values[0] == "%con[_]toso%"), Is.True);
        }
    }
}
