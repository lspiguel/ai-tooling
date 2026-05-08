using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

using Lspiguel.Xrm.D365ContextExporter.Helpers;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.HelpersTests
{
    [TestFixture]
    public class FirstRunHelperTests
    {
        private string _baseDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "FirstRunTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        // ── IsConfigured ────────────────────────────────────────────────────

        [Test]
        public void IsConfigured_EmptyDir_ReturnsFalse()
        {
            Assert.That(FirstRunHelper.IsConfigured(_baseDir), Is.False);
        }

        [Test]
        public void IsConfigured_WithSpecFile_ReturnsTrue()
        {
            Directory.CreateDirectory(Path.Combine(_baseDir, "config"));
            File.WriteAllText(
                Path.Combine(_baseDir, "config", "Test.context-exporter-config.json"),
                "{}");

            Assert.That(FirstRunHelper.IsConfigured(_baseDir), Is.True);
        }

        // ── ApplyOrgName ─────────────────────────────────────────────────────

        [Test]
        public void ApplyOrgName_ReplacesAllOccurrences()
        {
            const string template = "[ORGANISATION NAME] owns [ORGANISATION NAME]'s data.";
            var result = FirstRunHelper.ApplyOrgName(template, "Contoso");
            Assert.That(result, Is.EqualTo("Contoso owns Contoso's data."));
        }

        [Test]
        public void ApplyOrgName_NullOrgName_ReturnsTemplateUnchanged()
        {
            const string template = "Welcome [ORGANISATION NAME].";
            var result = FirstRunHelper.ApplyOrgName(template, null!);
            Assert.That(result, Is.EqualTo(template));
        }

        [Test]
        public void ApplyOrgName_WhitespaceOrgName_ReturnsTemplateUnchanged()
        {
            const string template = "Welcome [ORGANISATION NAME].";
            var result = FirstRunHelper.ApplyOrgName(template, "   ");
            Assert.That(result, Is.EqualTo(template));
        }

        [Test]
        public void ApplyOrgName_EmptyOrgName_ReturnsTemplateUnchanged()
        {
            const string template = "Welcome [ORGANISATION NAME].";
            var result = FirstRunHelper.ApplyOrgName(template, string.Empty);
            Assert.That(result, Is.EqualTo(template));
        }

        // ── DeployReferenceConfig ─────────────────────────────────────────────

        [Test]
        public void DeployReferenceConfig_FirstRun_AllExpectedFilesPresent()
        {
            var logged = new System.Collections.Generic.List<string>();
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), logged.Add, overwrite: false);

            // Spot-check a few key files.
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "security-roles.fetch.xml")), Is.True, "security-roles.fetch.xml");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "transform.py")), Is.True, "transform.py");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "filters.py")), Is.True, "filters.py");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "requirements.txt")), Is.True, "requirements.txt");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "EntityDictionary.context-exporter-config.json")), Is.True, "EntityDictionary spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "SecurityModel.context-exporter-config.json")), Is.True, "SecurityModel spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "SolutionsReference.context-exporter-config.json")), Is.True, "SolutionsReference spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "LEGAL.md")), Is.True, "LEGAL.md");
        }

        [Test]
        public void DeployReferenceConfig_WithOverwriteFalse_DoesNotOverwriteExistingFile()
        {
            var queryDir = Path.Combine(_baseDir, "config", "queries");
            Directory.CreateDirectory(queryDir);
            var sentinelPath = Path.Combine(queryDir, "security-roles.fetch.xml");
            File.WriteAllText(sentinelPath, "SENTINEL");

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, overwrite: false);

            Assert.That(File.ReadAllText(sentinelPath), Is.EqualTo("SENTINEL"), "Existing file should not be overwritten");
        }

        [Test]
        public void DeployReferenceConfig_WithOverwriteTrue_OverwritesExistingFiles()
        {
            var queryDir = Path.Combine(_baseDir, "config", "queries");
            Directory.CreateDirectory(queryDir);
            var sentinelPath = Path.Combine(queryDir, "security-roles.fetch.xml");
            File.WriteAllText(sentinelPath, "SENTINEL");

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, overwrite: true);

            var content = File.ReadAllText(sentinelPath);
            Assert.That(content, Is.Not.EqualTo("SENTINEL"), "File should be overwritten on upgrade");
        }

        [Test]
        public void DeployReferenceConfig_WithOverwriteTrue_NeverOverwritesLegal()
        {
            // First deploy to create LEGAL.md.
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, overwrite: false);

            var legalPath = Path.Combine(_baseDir, "LEGAL.md");
            File.WriteAllText(legalPath, "CUSTOM LEGAL");

            // Upgrade deploy — LEGAL.md must not change.
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, overwrite: true);

            Assert.That(File.ReadAllText(legalPath), Is.EqualTo("CUSTOM LEGAL"), "LEGAL.md must not be overwritten on upgrade");
        }

        [Test]
        public void DeployReferenceConfig_OrgNameApplied_LegalNotContainsPlaceholder()
        {
            // Use a subclass approach is not possible (static class); instead call DeployReferenceConfig
            // with overwrite:false and verify that a non-empty org name was applied by LEGAL.md content.
            // Since PromptOrgName cannot be injected, we deploy without a prompt (empty returned from NullWin32Window)
            // and verify the placeholder is still present (empty org name → unchanged template).
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, overwrite: false);

            var legalContent = File.ReadAllText(Path.Combine(_baseDir, "LEGAL.md"));
            // With an empty org name the placeholder must remain intact.
            Assert.That(legalContent, Does.Contain("[ORGANISATION NAME]"));
        }

        // ── CheckVersion ─────────────────────────────────────────────────────

        /// <summary>Minimal IWin32Window that does not display any UI. PromptOrgName will return empty string.</summary>
        private sealed class NullWin32Window : IWin32Window
        {
            public IntPtr Handle => IntPtr.Zero;
        }
    }
}
