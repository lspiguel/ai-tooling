using System;

using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.LoggingTests
{
    /// <summary>The call line and outcome line of SPEC §8.6.</summary>
    [TestFixture]
    public class CallLogFormatterTests
    {
        private static readonly DateTime Received = new DateTime(2026, 10, 30, 14, 3, 5, DateTimeKind.Local);
        private static readonly string[] CreateSolutionOrder = { "connection", "uniqueName", "friendlyName", "publisherUniqueName", "reason" };

        [Test]
        public void Truncate_TenCharacters_ShownWhole()
        {
            Assert.That(CallLogFormatter.Truncate("0123456789"), Is.EqualTo("0123456789"));
        }

        [Test]
        public void Truncate_ElevenCharacters_CutToTenWithEllipsis()
        {
            Assert.That(CallLogFormatter.Truncate("0123456789A"), Is.EqualTo("0123456789…"));
        }

        [Test]
        public void CallLine_MatchesTheSpecExample()
        {
            var arguments = new JObject
            {
                ["reason"] = "Base solution for story 1234.",
                ["connection"] = "00000000-0000-0000-0000-000000000000",
                ["uniqueName"] = "prefix_WIP_1234",
                ["friendlyName"] = "WIP 1234",
                ["publisherUniqueName"] = "PublisherName",
            };

            var line = CallLogFormatter.CallLine(Received, "create_solution", arguments, CreateSolutionOrder);

            Assert.That(line, Is.EqualTo("2026-10-30 14:03:05  create_solution  reason \"Base solution for story 1234.\"  connection=00000000-0…  uniqueName=prefix_WIP…  friendlyName=WIP 1234  publisherUniqueName=PublisherN…"));
        }

        [Test]
        public void CallLine_ParametersFollowSchemaOrderWithConnectionFirst()
        {
            var arguments = new JObject { ["friendlyName"] = "b", ["connection"] = "a", ["uniqueName"] = "c" };

            var line = CallLogFormatter.CallLine(Received, "create_solution", arguments, CreateSolutionOrder);

            Assert.That(line, Does.EndWith("reason —  connection=a  uniqueName=c  friendlyName=b"));
        }

        [Test]
        public void CallLine_UnknownArgumentsFollowTheKnownOnes()
        {
            var arguments = new JObject { ["extra"] = 1, ["connection"] = "a" };

            var line = CallLogFormatter.CallLine(Received, "create_solution", arguments, CreateSolutionOrder);

            Assert.That(line, Does.EndWith("connection=a  extra=1"));
        }

        [Test]
        public void CallLine_ReasonWithLineBreaksStaysOneLineAndIsNotTruncated()
        {
            var reason = "First line\r\n2026-10-30 14:00:00  create_publisher  reason \"fake\"\tand a tab";
            var arguments = new JObject { ["reason"] = reason, ["connection"] = "a" };

            var line = CallLogFormatter.CallLine(Received, "who_am_i", arguments, new[] { "connection", "reason" });

            Assert.That(line, Does.Not.Contain("\r").And.Not.Contain("\n").And.Not.Contain("\t"));
            Assert.That(line, Does.Contain("reason \"First line  2026-10-30 14:00:00  create_publisher  reason \"fake\" and a tab\""));
        }

        [Test]
        public void Sanitize_ReplacesBidirectionalFormattingCharacters()
        {
            var text = "abc\u202Edef\u2066ghi\u200Fjkl\u2028mno";

            Assert.That(CallLogFormatter.Sanitize(text), Is.EqualTo("abc def ghi jkl mno"));
        }

        [Test]
        public void Value_SanitizedBeforeTruncation()
        {
            // Ten characters after sanitizing: the line break becomes a space and the value is shown whole.
            Assert.That(CallLogFormatter.Value(new JValue("abcd\nefghi")), Is.EqualTo("abcd efghi"));
        }

        [Test]
        public void Value_NonStringsAreJsonLiterals()
        {
            Assert.That(CallLogFormatter.Value(new JValue(true)), Is.EqualTo("true"));
            Assert.That(CallLogFormatter.Value(JValue.CreateNull()), Is.EqualTo("null"));
            Assert.That(CallLogFormatter.Value(new JValue(42)), Is.EqualTo("42"));
            Assert.That(CallLogFormatter.Value(new JObject { ["a"] = 1 }), Is.EqualTo("{\"a\":1}"));
            Assert.That(CallLogFormatter.Value(new JArray(1, 2)), Is.EqualTo("[1,2]"));
        }

        [Test]
        public void OutcomeOk_ShowsApprovalDurationAndFieldsInOrder()
        {
            var result = new JObject { ["solutionId"] = "00000000-0000-0000-0000-000000000000", ["version"] = "1.0.0.0", ["nextCursor"] = null };

            var line = CallLogFormatter.OutcomeOk("once", TimeSpan.FromMilliseconds(800), result);

            Assert.That(line, Is.EqualTo("  → ok  once  0.8 s  solutionId=00000000-0…  version=1.0.0.0  nextCursor=null"));
        }

        [Test]
        public void OutcomeError_ShowsCodeAndFullMessage()
        {
            var line = CallLogFormatter.OutcomeError("invalid_argument", CallLogFormatter.NotRun, TimeSpan.Zero, "reason is required for Write and Publisher tools.");

            Assert.That(line, Is.EqualTo("  → error invalid_argument  —  0.0 s  reason is required for Write and Publisher tools."));
        }

        [Test]
        public void Unexpected_ShowsTheNameInFull()
        {
            Assert.That(CallLogFormatter.Unexpected("Relationship", "prefix_account_contact_long_name"), Is.EqualTo("  ⚠ unexpected Relationship prefix_account_contact_long_name"));
        }
    }
}
