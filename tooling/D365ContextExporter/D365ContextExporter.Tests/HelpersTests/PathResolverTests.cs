using System;
using System.IO;
using System.Linq;

using D365ContextExporter.Helpers;

using NUnit.Framework;

namespace D365ContextExporter.Tests.HelpersTests
{
    [TestFixture]
    public class PathResolverTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PathResolverTests_" + Guid.NewGuid().ToString("N"));
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
        public void Resolve_AbsolutePath_ReturnedUnchanged()
        {
            var absolute = @"C:\abs\path";
            var result = PathResolver.Resolve(absolute, @"C:\base");
            Assert.That(result, Is.EqualTo(absolute));
        }

        [Test]
        public void Resolve_RelativePath_JoinedToBaseDir()
        {
            var result = PathResolver.Resolve(@"queries\foo.xml", @"C:\base");
            var expected = Path.GetFullPath(@"C:\base\queries\foo.xml");
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_ExpandsEnvironmentVariable()
        {
            var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var result = PathResolver.Resolve(@"%TEMP%\foo", @"C:\base");
            Assert.That(result, Does.StartWith(temp));
            Assert.That(result, Does.EndWith("foo"));
        }

        [Test]
        public void DiscoverProjectConfigs_NoConfigDir_ReturnsEmpty()
        {
            var result = PathResolver.DiscoverProjectConfigs(_tempDir);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void DiscoverProjectConfigs_FindsTwoConfigs()
        {
            var configDir = Path.Combine(_tempDir, "config");
            Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "Alpha.context-exporter-config.json"), "{}");
            File.WriteAllText(Path.Combine(configDir, "Beta.context-exporter-config.json"), "{}");
            File.WriteAllText(Path.Combine(configDir, "other.json"), "{}");

            var result = PathResolver.DiscoverProjectConfigs(_tempDir).ToList();

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result, Has.Some.EndsWith("Alpha.context-exporter-config.json"));
            Assert.That(result, Has.Some.EndsWith("Beta.context-exporter-config.json"));
        }

        [Test]
        public void ProjectNameFromPath_StandardSuffix_ReturnsProjectName()
        {
            var path = Path.Combine("config", "Contoso.context-exporter-config.json");
            var result = PathResolver.ProjectNameFromPath(path);
            Assert.That(result, Is.EqualTo("Contoso"));
        }

        [Test]
        public void ProjectNameFromPath_NoSuffix_ReturnsFileNameWithoutExtension()
        {
            var path = Path.Combine("config", "SomethingElse.json");
            var result = PathResolver.ProjectNameFromPath(path);
            Assert.That(result, Is.EqualTo("SomethingElse"));
        }
    }
}
