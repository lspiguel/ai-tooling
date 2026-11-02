using System;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Microsoft.Xrm.Sdk;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>Record values (SPEC §6.5 <c>query</c>), XML chunking (SPEC §6.2) and FetchXML rules (F15).</summary>
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void Chunks_CountTheEscapedLength_AndRejoin()
        {
            var text = string.Concat(Enumerable.Repeat("<a b=\"c\\d\">\t</a>", 50));

            var chunks = TextChunker.Split(text, 40);

            Assert.That(string.Concat(chunks), Is.EqualTo(text));
            foreach (var chunk in chunks)
            {
                Assert.That(JsonConvert.ToString(chunk).Length - 2, Is.LessThanOrEqualTo(40));
            }
        }

        [Test]
        public void Chunks_NeverSplitASurrogatePair()
        {
            var text = string.Concat(Enumerable.Repeat("a\U0001F600", 20));

            var chunks = TextChunker.Split(text, 4);

            Assert.That(chunks.All(c => !char.IsHighSurrogate(c[c.Length - 1])), Is.True);
            Assert.That(string.Concat(chunks), Is.EqualTo(text));
        }

        [Test]
        public void EmptyText_IsOneEmptyChunk()
        {
            Assert.That(TextChunker.Split(string.Empty, 10), Is.EqualTo(new[] { string.Empty }));
        }

        [Test]
        public void Record_UsesTypedWrappers_AndOmitsMissingValues()
        {
            var id = new Guid("11111111-2222-3333-4444-555555555555");
            var record = new Entity("account")
            {
                ["name"] = "Contoso",
                ["empty"] = null,
                ["statecode"] = new OptionSetValue(0),
                ["categories"] = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) },
                ["ownerid"] = new EntityReference("systemuser", id),
                ["revenue"] = new Money(12.5m),
                ["createdon"] = new DateTime(2026, 10, 25, 14, 3, 31, DateTimeKind.Utc),
                ["link.fullname"] = new AliasedValue("contact", "fullname", "Pat"),
                FormattedValues =
                {
                    ["statecode"] = "Active",
                    ["categories"] = "One; Two",
                    ["ownerid"] = "Test User",
                    ["revenue"] = "$12.50",
                },
            };

            var json = RecordValues.Serialize(record, includeFormattedValues: true);

            Assert.That(json["empty"], Is.Null);
            Assert.That(json["statecode"]!.ToString(Formatting.None), Is.EqualTo("{\"_option\":0,\"label\":\"Active\"}"));
            Assert.That(json["categories"]!.ToString(Formatting.None), Is.EqualTo("{\"_options\":[1,2],\"labels\":[\"One\",\"Two\"]}"));
            Assert.That(json["ownerid"]!.ToString(Formatting.None), Is.EqualTo("{\"_ref\":{\"entity\":\"systemuser\",\"id\":\"11111111-2222-3333-4444-555555555555\"},\"name\":\"Test User\"}"));
            Assert.That(json["revenue"]!.ToString(Formatting.None), Is.EqualTo("{\"_money\":12.5,\"formatted\":\"$12.50\"}"));
            Assert.That(json["createdon"]!.ToString(), Is.EqualTo("2026-10-25T14:03:31Z"));
            Assert.That(json["link.fullname"]!.ToString(), Is.EqualTo("Pat"), "aliased values unwrapped");
        }

        [Test]
        public void Record_WithoutFormattedValues_HasNoLabelsOrNames()
        {
            var record = new Entity("account")
            {
                ["statecode"] = new OptionSetValue(0),
                ["ownerid"] = new EntityReference("systemuser", Guid.NewGuid()) { Name = "Test User" },
                FormattedValues = { ["statecode"] = "Active" },
            };

            var json = RecordValues.Serialize(record, includeFormattedValues: false);

            Assert.That(((JObject)json["statecode"]!).Properties().Select(p => p.Name), Is.EqualTo(new[] { "_option" }));
            Assert.That(((JObject)json["ownerid"]!).Properties().Select(p => p.Name), Is.EqualTo(new[] { "_ref" }));
        }

        [Test]
        public void FetchXml_CollectsEveryTable_AndDropsCallerPaging()
        {
            var query = FetchXmlQuery.Parse("<fetch page=\"3\" count=\"9\"><entity name=\"SystemForm\"><attribute name=\"name\" /><link-entity name=\"solutioncomponent\" from=\"objectid\" to=\"formid\"><link-entity name=\"new_widget\" from=\"a\" to=\"b\" /></link-entity></entity></fetch>");

            Assert.That(query.EntityName, Is.EqualTo("systemform"));
            Assert.That(query.RecordTables, Is.EqualTo(new[] { "new_widget" }));
            Assert.That(query.Document.Root!.Attribute("page"), Is.Null);
            Assert.That(query.Document.Root!.Attribute("count"), Is.Null);
        }

        [TestCase("not xml")]
        [TestCase("<query />")]
        [TestCase("<fetch />")]
        [TestCase("<fetch><entity name=\"a\" /><entity name=\"b\" /></fetch>")]
        [TestCase("<fetch><entity name=\"systemform\"><link-entity from=\"a\" to=\"b\" /></entity></fetch>")]
        public void FetchXml_Malformed_IsInvalidArgument(string fetchXml)
        {
            var ex = Assert.Throws<ToolException>(() => FetchXmlQuery.Parse(fetchXml));

            Assert.That(ex!.Code, Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That(ex.Details!["argument"]!.ToString(), Is.EqualTo("fetchXml"));
        }
    }
}
