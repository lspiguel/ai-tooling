using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Setup;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.SetupTests
{
    /// <summary>
    /// SPEC §8.7: <b>Export skills…</b> writes one zip per <c>agent-companion-*</c> skill, and the embedded skills are
    /// the repository's <c>skills/agent-companion-*</c> folders, file for file and byte for byte.
    /// </summary>
    [TestFixture]
    public class SkillExporterTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AgentCompanionSkillExport-" + Guid.NewGuid().ToString("N"));
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
        public void EmbeddedSkills_AreTheRepositorysAgentCompanionSkills()
        {
            var expected = SkillFolders().Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal);

            Assert.That(SkillExporter.SkillNames(), Is.EqualTo(expected));
            Assert.That(SkillExporter.SkillNames(), Has.All.StartWith("agent-companion-"));
        }

        [Test]
        public void Export_WritesOneZipPerSkill_HoldingTheSkillFolderAsInTheRepository()
        {
            var written = SkillExporter.Export(_tempDir);

            Assert.That(written, Is.EqualTo(SkillExporter.ZipPaths(_tempDir)));
            Assert.That(Directory.GetFiles(_tempDir), Is.EquivalentTo(written));

            foreach (var folder in SkillFolders())
            {
                var zipPath = Path.Combine(_tempDir, $"{folder.Name}.zip");
                using (var zip = ZipFileOpen(zipPath))
                {
                    var expected = folder.GetFiles("*", SearchOption.AllDirectories)
                        .Select(f => $"{folder.Name}/{f.FullName.Substring(folder.FullName.Length + 1).Replace('\\', '/')}")
                        .OrderBy(n => n, StringComparer.Ordinal);
                    Assert.That(zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal), Is.EqualTo(expected), folder.Name);
                    Assert.That(zip.GetEntry($"{folder.Name}/SKILL.md"), Is.Not.Null, $"{folder.Name} has its SKILL.md at the folder root");

                    foreach (var entry in zip.Entries)
                    {
                        var source = Path.Combine(folder.Parent!.FullName, entry.FullName.Replace('/', '\\'));
                        using (var stream = entry.Open())
                        using (var copy = new MemoryStream())
                        {
                            stream.CopyTo(copy);
                            Assert.That(copy.ToArray(), Is.EqualTo(File.ReadAllBytes(source)), entry.FullName);
                        }
                    }
                }
            }
        }

        [Test]
        public void Export_ReplacesAnEarlierExport()
        {
            var first = SkillExporter.Export(_tempDir);
            File.WriteAllText(first[0], "stale");

            SkillExporter.Export(_tempDir);

            using (var zip = ZipFileOpen(first[0]))
            {
                Assert.That(zip.Entries, Is.Not.Empty);
            }
        }

        private static ZipArchive ZipFileOpen(string path) => new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);

        private static DirectoryInfo[] SkillFolders()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                var skills = new DirectoryInfo(Path.Combine(directory.FullName, "skills"));
                if (skills.Exists && File.Exists(Path.Combine(directory.FullName, "INSTRUCTIONS.md")))
                {
                    return skills.GetDirectories("agent-companion-*");
                }
            }

            throw new InvalidOperationException("The repository's skills folder was not found above the test directory.");
        }
    }
}
