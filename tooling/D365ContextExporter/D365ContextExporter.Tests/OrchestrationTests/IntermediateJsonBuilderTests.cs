using System;
using System.Collections.Generic;
using System.IO;

using D365ContextExporter.Models;
using D365ContextExporter.Orchestration;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace D365ContextExporter.Tests.OrchestrationTests
{
    [TestFixture]
    public class IntermediateJsonBuilderTests
    {
        private string _runDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _runDir = Path.Combine(Path.GetTempPath(), "IntermediateTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_runDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_runDir))
                Directory.Delete(_runDir, recursive: true);
        }

        private static ExportJob MakeJob() => new ExportJob
        {
            Spec = "TestProject",
            FrontMatter = new Dictionary<string, string> { ["purpose"] = "testing" },
        };

        [Test]
        public void WriteQueryResult_WritesFileWithCorrectName()
        {
            var data = new JArray { new JObject { ["id"] = "1" } };
            IntermediateJsonBuilder.WriteQueryResult(_runDir, "entity-attributes", data);

            var expected = Path.Combine(_runDir, "output.entity-attributes.fetch.json");
            Assert.That(File.Exists(expected), Is.True);
        }

        [Test]
        public void WriteQueryResult_FileContentIsValidJson()
        {
            var data = new JArray { new JObject { ["name"] = "account" } };
            IntermediateJsonBuilder.WriteQueryResult(_runDir, "test-query", data);

            var json = File.ReadAllText(Path.Combine(_runDir, "output.test-query.fetch.json"));
            var parsed = JToken.Parse(json);
            Assert.That(parsed.Type, Is.EqualTo(JTokenType.Array));
            Assert.That(((JArray)parsed).Count, Is.EqualTo(1));
        }

        [Test]
        public void WriteIntermediate_ReturnsCorrectPath()
        {
            var results = new Dictionary<string, object>
            {
                ["entityAttributes"] = new JArray(),
            };
            var path = IntermediateJsonBuilder.WriteIntermediate(
                _runDir, MakeJob(), "https://org.crm.dynamics.com", "MyOrg", results);

            Assert.That(path, Is.EqualTo(Path.Combine(_runDir, "intermediate.json")));
            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void WriteIntermediate_MetaBlockIsPopulated()
        {
            var results = new Dictionary<string, object> { ["r1"] = new JArray() };
            var path = IntermediateJsonBuilder.WriteIntermediate(
                _runDir, MakeJob(), "https://test.crm.dynamics.com", "TestOrg", results);

            var jobj = JObject.Parse(File.ReadAllText(path));
            var meta = jobj["_meta"]!;
            Assert.That(meta["spec"]!.Value<string>(), Is.EqualTo("TestProject"));
            Assert.That(meta["environment"]!["url"]!.Value<string>(), Is.EqualTo("https://test.crm.dynamics.com"));
            Assert.That(meta["environment"]!["orgName"]!.Value<string>(), Is.EqualTo("TestOrg"));
            Assert.That(meta["frontMatter"]!["purpose"]!.Value<string>(), Is.EqualTo("testing"));
            Assert.That(meta["exportedAtUtc"]!.Value<string>(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void WriteIntermediate_ResultKeysPresentAtTopLevel()
        {
            var arr1 = new JArray { new JObject { ["id"] = "a" } };
            var arr2 = new JArray { new JObject { ["id"] = "b" }, new JObject { ["id"] = "c" } };
            var results = new Dictionary<string, object>
            {
                ["entities"] = arr1,
                ["roles"] = arr2,
            };
            var path = IntermediateJsonBuilder.WriteIntermediate(
                _runDir, MakeJob(), "https://x.crm.dynamics.com", "X", results);

            var jobj = JObject.Parse(File.ReadAllText(path));
            Assert.That(jobj["entities"]?.Type, Is.EqualTo(JTokenType.Array));
            Assert.That(((JArray)jobj["entities"]!).Count, Is.EqualTo(1));
            Assert.That(((JArray)jobj["roles"]!).Count, Is.EqualTo(2));
        }

        [Test]
        public void WriteIntermediate_AcceptsDictionaryListResult()
        {
            var data = new List<Dictionary<string, object?>>
            {
                new Dictionary<string, object?> { ["name"] = "account", ["_id"] = Guid.NewGuid().ToString() },
            };
            var results = new Dictionary<string, object> { ["securityRoles"] = data };
            var path = IntermediateJsonBuilder.WriteIntermediate(
                _runDir, MakeJob(), "https://x.crm.dynamics.com", "X", results);

            var jobj = JObject.Parse(File.ReadAllText(path));
            var arr = (JArray)jobj["securityRoles"]!;
            Assert.That(arr.Count, Is.EqualTo(1));
            Assert.That(arr[0]["name"]!.Value<string>(), Is.EqualTo("account"));
        }
    }
}
