using System;
using System.IO;
using System.Linq;
using System.Text;

using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.LoggingTests
{
    /// <summary>The automatic call log file's name and shape (SPEC 6a.2): valid JSON after every write, the trailer, escaping.</summary>
    [TestFixture]
    public class CallLogFileTests
    {
        private static readonly DateTime Day = new DateTime(2026, 11, 8);

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "CallLogFileTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void FileName_HasTheConnectionNameAndTheDate_WithNoTime()
        {
            Assert.That(CallLogFile.FileName("Dev A", null, Day), Is.EqualTo("AgentCompanion-Dev A-20261108.json"));
            Assert.That(CallLogFile.FileName(CallLogFile.HostName, null, Day), Is.EqualTo("AgentCompanion-host-20261108.json"));
        }

        [Test]
        public void FileName_ReplacesInvalidCharacters_AddsTheIdPrefix_AndTheSequence()
        {
            Assert.That(CallLogFile.FileName("Dev: A/B", null, Day), Is.EqualTo("AgentCompanion-Dev_ A_B-20261108.json"));
            Assert.That(CallLogFile.FileName("Dev A", CallLogFile.IdPrefix("aaaaaaaa-1111-1111-1111-111111111111"), Day), Is.EqualTo("AgentCompanion-Dev A-aaaaaaaa-20261108.json"));
            Assert.That(CallLogFile.FileName("Dev A", null, Day, 2), Is.EqualTo("AgentCompanion-Dev A-20261108-2.json"));
        }

        [Test]
        public void ANewFile_ParsesAsOneDocument_AndEndsWithTheTrailer()
        {
            var bytes = CallLogFile.NewFile(Connection("c-1"), Day, Entry("first"));
            var text = Encoding.UTF8.GetString(bytes);

            Assert.That(text, Does.EndWith("]}\n"));
            Assert.That(bytes[0], Is.EqualTo((byte)'{'), "no byte order mark");
            var document = JObject.Parse(text);
            Assert.That((int)document["schemaVersion"]!, Is.EqualTo(1));
            Assert.That((string)document["date"]!, Is.EqualTo("2026-11-08"));
            Assert.That((string)document["connection"]!["connectionId"]!, Is.EqualTo("c-1"));
            Assert.That((string)document["entries"]![0]!["tool"]!, Is.EqualTo("first"));
        }

        [Test]
        public void AppendingOverTheTrailer_KeepsTheFileValid()
        {
            var path = Path.Combine(_tempDir, "log.json");
            File.WriteAllBytes(path, CallLogFile.NewFile(null, Day, Entry("first")));

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                Assert.That(CallLogFile.EndsWithTrailer(stream), Is.True);
                stream.Seek(-CallLogFile.Trailer.Length, SeekOrigin.End);
                var append = CallLogFile.Append(Entry("second"));
                stream.Write(append, 0, append.Length);
            }

            var document = JObject.Parse(File.ReadAllText(path));
            Assert.That(document["connection"]!.Type, Is.EqualTo(JTokenType.Null), "the host file has no connection");
            Assert.That(document["entries"]!, Has.Count.EqualTo(2));
            Assert.That((string)document["entries"]![1]!["tool"]!, Is.EqualTo("second"));
            Assert.That(CallLogFile.Check(path, null), Is.EqualTo(FileCheck.InShape));
        }

        [Test]
        public void BidiCharactersAndSeparators_AreEscaped_AndControlCharactersStayEscaped()
        {
            var rlo = (char)0x202E;
            var text = CallLogFile.Serialize(new JObject { ["reason"] = $"a{rlo}b{(char)0x2028}c\r\nd" }, Formatting.None);

            Assert.That(text.IndexOf(rlo), Is.EqualTo(-1));
            Assert.That(text, Does.Contain("\\u202E").And.Contain("\\u2028").And.Contain("\\r\\n"));
            Assert.That((string)JObject.Parse(text)["reason"]!, Is.EqualTo($"a{rlo}b{(char)0x2028}c\r\nd"), "the value is unchanged");
        }

        [Test]
        public void Check_FindsATruncatedFile_AFileThatDoesNotParse_AndAnotherConnectionsFile()
        {
            var good = Path.Combine(_tempDir, "good.json");
            File.WriteAllBytes(good, CallLogFile.NewFile(Connection("c-1"), Day, Entry("first")));
            var truncated = Path.Combine(_tempDir, "truncated.json");
            var bytes = File.ReadAllBytes(good);
            File.WriteAllBytes(truncated, bytes.Take(bytes.Length - 10).ToArray());
            var broken = Path.Combine(_tempDir, "broken.json");
            File.WriteAllText(broken, "{\"schemaVersion\":1,\"entries\":[{\n]}\n");

            Assert.That(CallLogFile.Check(good, "c-1"), Is.EqualTo(FileCheck.InShape));
            Assert.That(CallLogFile.Check(good, "C-1"), Is.EqualTo(FileCheck.InShape), "connection IDs compare without case");
            Assert.That(CallLogFile.Check(good, "c-2"), Is.EqualTo(FileCheck.OtherConnection));
            Assert.That(CallLogFile.Check(good, null), Is.EqualTo(FileCheck.OtherConnection), "the host file is not a connection's");
            Assert.That(CallLogFile.Check(truncated, "c-1"), Is.EqualTo(FileCheck.NotInShape));
            Assert.That(CallLogFile.Check(broken, "c-1"), Is.EqualTo(FileCheck.NotInShape));
        }

        private static JObject Connection(string id) => new JObject { ["connectionId"] = id, ["connectionName"] = "Dev A" };

        private static JObject Entry(string tool) => new JObject { ["tool"] = tool, ["result"] = new JObject { ["text"] = "line1\nline2" } };
    }
}
