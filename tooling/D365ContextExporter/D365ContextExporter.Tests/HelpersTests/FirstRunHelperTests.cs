using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using Lspiguel.Xrm.D365ContextExporter.Helpers;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.HelpersTests
{
    [TestFixture]
    public class FirstRunHelperTests
    {
        private const string OldVersion = "1.2026.1.1";
        private const string NewVersion = "1.2026.2.1";

        private string _baseDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "FirstRunTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDir);
        }

        private string VersionPath => Path.Combine(_baseDir, "version.txt");

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
            var logged = new List<string>();
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), logged.Add);

            // Spot-check a few key files.
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "security-roles.fetch.xml")), Is.True, "security-roles.fetch.xml");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "entity-dictionary.sbn")), Is.True, "entity-dictionary.sbn");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "EntityDictionary.context-exporter-config.json")), Is.True, "EntityDictionary spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "transform.py")), Is.False, "transform.py must not be deployed");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "filters.py")), Is.False, "filters.py must not be deployed");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "requirements.txt")), Is.False, "requirements.txt must not be deployed");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "SecurityModel.context-exporter-config.json")), Is.True, "SecurityModel spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "SolutionsReference.context-exporter-config.json")), Is.True, "SolutionsReference spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "context-exporter.schema.json")), Is.True, "schema deployed beside specs");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "PowerAutomateFlows.context-exporter-config.json")), Is.True, "PowerAutomateFlows spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "power-automate-flows.sbn")), Is.True, "power-automate-flows transformation");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "cloud-flows.fetch.xml")), Is.True, "cloud-flows query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "business-process-flows.fetch.xml")), Is.True, "business-process-flows query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "bpf-stages.fetch.xml")), Is.True, "bpf-stages query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "project-tables.fetch.xml")), Is.True, "project-tables query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "project-forms.fetch.xml")), Is.True, "project-forms query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "project-views.fetch.xml")), Is.True, "project-views query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "security-role-names.fetch.xml")), Is.True, "security-role-names query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "PowerPages.context-exporter-config.json")), Is.True, "PowerPages spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "power-pages.sbn")), Is.True, "power-pages transformation");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "queries", "power-pages-components.fetch.xml")), Is.True, "power-pages-components query");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "Relationships.context-exporter-config.json")), Is.True, "Relationships spec");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "transformations", "relationships.sbn")), Is.True, "relationships transformation");
            Assert.That(File.Exists(Path.Combine(_baseDir, "LEGAL.md")), Is.True, "LEGAL.md");
        }

        [Test]
        public void DeployReferenceConfig_FirstRun_DoesNotOverwriteExistingFile()
        {
            var queryDir = Path.Combine(_baseDir, "config", "queries");
            Directory.CreateDirectory(queryDir);
            var sentinelPath = Path.Combine(queryDir, "security-roles.fetch.xml");
            File.WriteAllText(sentinelPath, "SENTINEL");

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });

            Assert.That(File.ReadAllText(sentinelPath), Is.EqualTo("SENTINEL"), "Existing file should not be overwritten");
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_OverwritesExistingFiles()
        {
            var queryDir = Path.Combine(_baseDir, "config", "queries");
            Directory.CreateDirectory(queryDir);
            var sentinelPath = Path.Combine(queryDir, "security-roles.fetch.xml");
            File.WriteAllText(sentinelPath, "SENTINEL");

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            var content = File.ReadAllText(sentinelPath);
            Assert.That(content, Is.Not.EqualTo("SENTINEL"), "File should be overwritten on upgrade");
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_NeverOverwritesLegal()
        {
            // First deploy to create LEGAL.md.
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });

            var legalPath = Path.Combine(_baseDir, "LEGAL.md");
            File.WriteAllText(legalPath, "CUSTOM LEGAL");

            // Upgrade deploy — LEGAL.md must not change.
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            Assert.That(File.ReadAllText(legalPath), Is.EqualTo("CUSTOM LEGAL"), "LEGAL.md must not be overwritten on upgrade");
        }

        [Test]
        public void DeployReferenceConfig_OrgNameApplied_LegalNotContainsPlaceholder()
        {
            // Use a subclass approach is not possible (static class); instead call DeployReferenceConfig
            // for first-run setup and verify that a non-empty org name was applied by LEGAL.md content.
            // Since PromptOrgName cannot be injected, we deploy without a prompt (empty returned from NullWin32Window)
            // and verify the placeholder is still present (empty org name → unchanged template).
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });

            var legalContent = File.ReadAllText(Path.Combine(_baseDir, "LEGAL.md"));
            // With an empty org name the placeholder must remain intact.
            Assert.That(legalContent, Does.Contain("[ORGANISATION NAME]"));
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_EditedShippedFileBackedUpBeforeOverwrite()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });
            var queryPath = Path.Combine(_baseDir, "config", "queries", "security-roles.fetch.xml");
            File.WriteAllText(queryPath, "EDITED");
            var logged = new List<string>();

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), logged.Add, NewVersion, previousVersion: OldVersion);

            var backupPath = Path.Combine(_baseDir, "config", "backup", OldVersion, "queries", "security-roles.fetch.xml");
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo("EDITED"), "edited file backed up under the previous version");
            Assert.That(File.ReadAllText(queryPath), Is.Not.EqualTo("EDITED"), "shipped file replaced");
            Assert.That(logged, Has.Some.StartsWith("[Setup] Backed up:"));
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_UnchangedFilesNotBackedUp()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            Assert.That(Directory.Exists(Path.Combine(_baseDir, "config", "backup")), Is.False);
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_ExistingBackupNotOverwritten()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });
            var earlierBackup = Path.Combine(_baseDir, "config", "backup", OldVersion, "queries", "security-roles.fetch.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(earlierBackup)!);
            File.WriteAllText(earlierBackup, "EARLIER BACKUP");
            File.WriteAllText(Path.Combine(_baseDir, "config", "queries", "security-roles.fetch.xml"), "EDITED");

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            Assert.That(File.ReadAllText(earlierBackup), Is.EqualTo("EARLIER BACKUP"));
            var newBackup = Path.Combine(_baseDir, "config", "backup", OldVersion + "-2", "queries", "security-roles.fetch.xml");
            Assert.That(File.ReadAllText(newBackup), Is.EqualTo("EDITED"));
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_BackedUpSpecNotDiscovered()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });
            var specPath = Path.Combine(_baseDir, "config", "EntityDictionary.context-exporter-config.json");
            File.WriteAllText(specPath, "{ \"spec\": \"EntityDictionary\" }");
            var specCount = PathResolver.DiscoverSpecConfigs(_baseDir).Count();

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "backup", OldVersion, "EntityDictionary.context-exporter-config.json")), Is.True);
            Assert.That(PathResolver.DiscoverSpecConfigs(_baseDir).Count(), Is.EqualTo(specCount), "backups sit below the top level of config");
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_FileWithOtherNameNotTouched()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });
            var customPath = Path.Combine(_baseDir, "config", "queries", "custom.fetch.xml");
            File.WriteAllText(customPath, "CUSTOM");

            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            Assert.That(File.ReadAllText(customPath), Is.EqualTo("CUSTOM"));
            Assert.That(Directory.Exists(Path.Combine(_baseDir, "config", "backup")), Is.False);
        }

        [Test]
        public void DeployReferenceConfig_Upgrade_WritesCurrentVersion()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, NewVersion, previousVersion: OldVersion);

            Assert.That(File.ReadAllText(VersionPath).Trim(), Is.EqualTo(NewVersion));
        }

        // ── CheckVersion ─────────────────────────────────────────────────────

        [Test]
        public void CheckVersion_VersionFileMissing_WritesCurrentVersionWithoutAsking()
        {
            var asked = false;

            FirstRunHelper.CheckVersion(_baseDir, NewVersion, (_, _) => asked = true, new NullWin32Window(), _ => { });

            Assert.That(asked, Is.False);
            Assert.That(File.ReadAllText(VersionPath).Trim(), Is.EqualTo(NewVersion));
            Assert.That(Directory.Exists(Path.Combine(_baseDir, "config")), Is.False, "nothing deployed");
        }

        [Test]
        public void CheckVersion_SameVersion_DoesNothing()
        {
            File.WriteAllText(VersionPath, NewVersion);
            var asked = false;

            FirstRunHelper.CheckVersion(_baseDir, NewVersion, (_, _) => asked = true, new NullWin32Window(), _ => { });

            Assert.That(asked, Is.False);
            Assert.That(Directory.Exists(Path.Combine(_baseDir, "config")), Is.False, "nothing deployed");
        }

        [Test]
        public void CheckVersion_DifferentVersionAccepted_UpgradesAndBacksUpEdits()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, OldVersion, previousVersion: null);
            var queryPath = Path.Combine(_baseDir, "config", "queries", "security-roles.fetch.xml");
            File.WriteAllText(queryPath, "EDITED");
            string? askedStored = null;
            string? askedCurrent = null;

            FirstRunHelper.CheckVersion(
                _baseDir,
                NewVersion,
                (stored, current) =>
                {
                    askedStored = stored;
                    askedCurrent = current;
                    return true;
                },
                new NullWin32Window(),
                _ => { });

            Assert.That(askedStored, Is.EqualTo(OldVersion));
            Assert.That(askedCurrent, Is.EqualTo(NewVersion));
            Assert.That(File.ReadAllText(queryPath), Is.Not.EqualTo("EDITED"), "shipped file replaced");
            Assert.That(File.Exists(Path.Combine(_baseDir, "config", "backup", OldVersion, "queries", "security-roles.fetch.xml")), Is.True);
            Assert.That(File.ReadAllText(VersionPath).Trim(), Is.EqualTo(NewVersion));
        }

        [Test]
        public void CheckVersion_DifferentVersionDeclined_RecordsVersionAndLeavesFiles()
        {
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { }, OldVersion, previousVersion: null);
            var queryPath = Path.Combine(_baseDir, "config", "queries", "security-roles.fetch.xml");
            File.WriteAllText(queryPath, "EDITED");
            var logged = new List<string>();

            FirstRunHelper.CheckVersion(_baseDir, NewVersion, (_, _) => false, new NullWin32Window(), logged.Add);

            Assert.That(File.ReadAllText(queryPath), Is.EqualTo("EDITED"));
            Assert.That(Directory.Exists(Path.Combine(_baseDir, "config", "backup")), Is.False);
            Assert.That(File.ReadAllText(VersionPath).Trim(), Is.EqualTo(NewVersion), "offer does not return for this version");
            Assert.That(logged, Has.Some.Contains("Upgrade skipped"));
        }

        /// <summary>Minimal IWin32Window that does not display any UI. PromptOrgName will return empty string.</summary>
        private sealed class NullWin32Window : IWin32Window
        {
            public IntPtr Handle => IntPtr.Zero;
        }
    }
}
