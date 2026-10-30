using System;
using System.IO;
using System.Linq;
using System.Text;

using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.LoggingTests
{
    /// <summary><b>Save as…</b> (SPEC §8.6): content, default name, and the Git working-tree check.</summary>
    [TestFixture]
    public class LogFileWriterTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "LogFileWriterTests_" + Guid.NewGuid().ToString("N"));
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
        public void DefaultFileName_FollowsThePattern()
        {
            var name = LogFileWriter.DefaultFileName("Dev A", new DateTime(2026, 10, 30, 14, 3, 5));

            Assert.That(name, Is.EqualTo("AgentCompanion-Dev A-20261030-140305.txt"));
        }

        [Test]
        public void DefaultFileName_ReplacesCharactersWindowsRejects()
        {
            var name = LogFileWriter.DefaultFileName("Dev: A/B", new DateTime(2026, 10, 30, 14, 3, 5));

            Assert.That(name, Is.EqualTo("AgentCompanion-Dev_ A_B-20261030-140305.txt"));
        }

        [Test]
        public void SavedFile_HoldsHeaderCallLogThenHostLog_LinesUnchanged()
        {
            var callLines = new[]
            {
                "2026-10-30 14:02:11  list_publishers  reason —  connection=00000000-0…",
                "  → ok  environment  0.3 s  publishers=[{\"publish…  nextCursor=null",
            };
            var hostLines = new[] { "2026-10-30 14:01:58  list_connections  reason —", "  → ok  none  0.0 s  host={\"version\"…" };
            var path = Path.Combine(_tempDir, "log.txt");

            LogFileWriter.Write(path, LogFileWriter.Compose("Dev A", "Dev A org", new DateTime(2026, 10, 30, 14, 5, 0), callLines, hostLines));
            var lines = File.ReadAllLines(path, Encoding.UTF8);

            Assert.That(lines[0], Is.EqualTo("Agent Companion MCP call log · Dev A · Dev A org · saved 2026-10-30 14:05:00"));
            Assert.That(lines.Skip(2).Take(2), Is.EqualTo(callLines));
            Assert.That(lines[5], Is.EqualTo("Host log"));
            Assert.That(lines.Skip(6).Take(2), Is.EqualTo(hostLines));
        }

        [Test]
        public void IsInsideGitWorkingTree_FolderUnderDotGitParent_True()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
            var nested = Directory.CreateDirectory(Path.Combine(_tempDir, "a", "b")).FullName;

            Assert.That(LogFileWriter.IsInsideGitWorkingTree(nested), Is.True);
        }

        [Test]
        public void IsInsideGitWorkingTree_GitFileForWorktrees_True()
        {
            File.WriteAllText(Path.Combine(_tempDir, ".git"), "gitdir: elsewhere");

            Assert.That(LogFileWriter.IsInsideGitWorkingTree(_tempDir), Is.True);
        }

        [Test]
        public void IsInsideGitWorkingTree_PlainFolder_False()
        {
            Assert.That(LogFileWriter.IsInsideGitWorkingTree(_tempDir), Is.False);
        }
    }
}
