// <copyright file="FirstRunHelper.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Helpers
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Windows.Forms;

    using Microsoft.VisualBasic;

    /// <summary>Manages the lifecycle of the reference configuration in a base directory.</summary>
    internal static class FirstRunHelper
    {
        // Every entry in DeployedFiles lives under config\; backups keep the path below it.
        private const string ConfigPrefix = @"config\";
        private const string ResourcePrefix = "Lspiguel.Xrm.D365ContextExporter.";

        private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

        // Files deployed by DeployReferenceConfig, grouped by target subfolder relative to baseDir.
        private static readonly (string Resource, string RelativeDest)[] DeployedFiles =
        {
            // Queries
            ("SampleConfig.queries.app-modules.fetch.xml",            @"config\queries\app-modules.fetch.xml"),
            ("SampleConfig.queries.bpf-stages.fetch.xml",             @"config\queries\bpf-stages.fetch.xml"),
            ("SampleConfig.queries.business-process-flows.fetch.xml", @"config\queries\business-process-flows.fetch.xml"),
            ("SampleConfig.queries.cloud-flows.fetch.xml",            @"config\queries\cloud-flows.fetch.xml"),
            ("SampleConfig.queries.custom-api-responses.fetch.xml",   @"config\queries\custom-api-responses.fetch.xml"),
            ("SampleConfig.queries.custom-apis.fetch.xml",            @"config\queries\custom-apis.fetch.xml"),
            ("SampleConfig.queries.env-vars.fetch.xml",               @"config\queries\env-vars.fetch.xml"),
            ("SampleConfig.queries.plugin-assemblies.fetch.xml",      @"config\queries\plugin-assemblies.fetch.xml"),
            ("SampleConfig.queries.project-forms.fetch.xml",          @"config\queries\project-forms.fetch.xml"),
            ("SampleConfig.queries.project-tables.fetch.xml",         @"config\queries\project-tables.fetch.xml"),
            ("SampleConfig.queries.project-views.fetch.xml",          @"config\queries\project-views.fetch.xml"),
            ("SampleConfig.queries.sdk-steps.fetch.xml",              @"config\queries\sdk-steps.fetch.xml"),
            ("SampleConfig.queries.security-role-names.fetch.xml",    @"config\queries\security-role-names.fetch.xml"),
            ("SampleConfig.queries.security-roles.fetch.xml",         @"config\queries\security-roles.fetch.xml"),
            ("SampleConfig.queries.solutions-detail.fetch.xml",       @"config\queries\solutions-detail.fetch.xml"),
            ("SampleConfig.queries.workflows.fetch.xml",              @"config\queries\workflows.fetch.xml"),

            // Transformations — Scriban templates
            ("SampleConfig.transformations.entity-dictionary.sbn",    @"config\transformations\entity-dictionary.sbn"),
            ("SampleConfig.transformations.forms-and-views.sbn",      @"config\transformations\forms-and-views.sbn"),
            ("SampleConfig.transformations.optionsets.sbn",           @"config\transformations\optionsets.sbn"),
            ("SampleConfig.transformations.power-automate-flows.sbn", @"config\transformations\power-automate-flows.sbn"),
            ("SampleConfig.transformations.security-model.sbn",       @"config\transformations\security-model.sbn"),
            ("SampleConfig.transformations.solution-inventory.sbn",   @"config\transformations\solution-inventory.sbn"),
            ("SampleConfig.transformations.solutions-reference.sbn",  @"config\transformations\solutions-reference.sbn"),

            // Spec configs
            ("SampleConfig.EntityDictionary.context-exporter-config.json",   @"config\EntityDictionary.context-exporter-config.json"),
            ("SampleConfig.FormsAndViews.context-exporter-config.json",      @"config\FormsAndViews.context-exporter-config.json"),
            ("SampleConfig.Optionsets.context-exporter-config.json",         @"config\Optionsets.context-exporter-config.json"),
            ("SampleConfig.PowerAutomateFlows.context-exporter-config.json", @"config\PowerAutomateFlows.context-exporter-config.json"),
            ("SampleConfig.SecurityModel.context-exporter-config.json",      @"config\SecurityModel.context-exporter-config.json"),
            ("SampleConfig.SolutionInventory.context-exporter-config.json",  @"config\SolutionInventory.context-exporter-config.json"),
            ("SampleConfig.SolutionsReference.context-exporter-config.json", @"config\SolutionsReference.context-exporter-config.json"),

            // JSON Schema — deployed beside the spec configs so their relative "./context-exporter.schema.json" reference resolves for IDE IntelliSense.
            ("SampleConfig.context-exporter.schema.json",                    @"config\context-exporter.schema.json"),
        };

        // LEGAL.md handled separately (never overwritten on upgrade, and org-name substitution applied on first deploy).
        private const string LegalResourceSuffix = "SampleConfig.LEGAL.md";
        private const string LegalRelativeDest = "LEGAL.md";

        /// <summary>Returns <c>true</c> if <paramref name="baseDir"/> contains at least one spec config file.</summary>
        public static bool IsConfigured(string baseDir)
        {
            var configDir = Path.Combine(baseDir, "config");
            if (!Directory.Exists(configDir))
            {
                return false;
            }

            return Directory.GetFiles(configDir, "*.context-exporter-config.json", SearchOption.TopDirectoryOnly).Length > 0;
        }

        /// <summary>Shows a yes/no prompt offering to create the reference configuration. Returns <c>true</c> if the user accepted.</summary>
        public static bool OfferSetup(string baseDir, IWin32Window owner)
        {
            var result = MessageBox.Show(
                owner,
                $"The selected folder does not contain any spec configurations.\n\n" +
                $"Would you like to deploy the reference configuration (sample specs, FetchXML queries, and Scriban templates) to:\n\n{baseDir}",
                "First-Time Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }

        /// <summary>Extracts all embedded sample files to <paramref name="baseDir"/> for first-run setup; files that already exist are skipped.</summary>
        /// <param name="baseDir">The target base directory.</param>
        /// <param name="owner">Parent window for dialogs.</param>
        /// <param name="log">Log delegate.</param>
        public static void DeployReferenceConfig(string baseDir, IWin32Window owner, Action<string> log)
            => DeployReferenceConfig(baseDir, owner, log, CurrentVersion(), previousVersion: null);

        /// <summary>Checks <paramref name="baseDir"/> for a version mismatch and offers an upgrade if needed.</summary>
        /// <param name="baseDir">The base directory.</param>
        /// <param name="owner">Parent window for dialogs.</param>
        /// <param name="log">Log delegate.</param>
        public static void CheckVersion(string baseDir, IWin32Window owner, Action<string> log)
            => CheckVersion(baseDir, CurrentVersion(), (stored, current) => ConfirmUpgrade(owner, stored, current), owner, log);

        /// <summary>Checks <paramref name="baseDir"/> for a version mismatch against <paramref name="currentVersion"/> and upgrades if <paramref name="confirmUpgrade"/> accepts.</summary>
        /// <param name="baseDir">The base directory.</param>
        /// <param name="currentVersion">The plugin version to compare with <c>version.txt</c>.</param>
        /// <param name="confirmUpgrade">Called with (stored version, current version) when they differ; returns <c>true</c> to upgrade.</param>
        /// <param name="owner">Parent window for dialogs.</param>
        /// <param name="log">Log delegate.</param>
        internal static void CheckVersion(string baseDir, string currentVersion, Func<string, string, bool> confirmUpgrade, IWin32Window owner, Action<string> log)
        {
            var versionPath = Path.Combine(baseDir, "version.txt");

            if (!File.Exists(versionPath))
            {
                // Directory configured before this feature shipped — write version silently.
                File.WriteAllText(versionPath, currentVersion, Encoding.UTF8);
                return;
            }

            var stored = File.ReadAllText(versionPath).Trim();
            if (stored == currentVersion)
            {
                return;
            }

            if (confirmUpgrade(stored, currentVersion))
            {
                DeployReferenceConfig(baseDir, owner, log, currentVersion, previousVersion: stored);
            }
            else
            {
                File.WriteAllText(versionPath, currentVersion, Encoding.UTF8);
                log("[Setup] Upgrade skipped by user.");
            }
        }

        /// <summary>Extracts all embedded sample files to <paramref name="baseDir"/> and writes <c>version.txt</c>.</summary>
        /// <param name="baseDir">The target base directory.</param>
        /// <param name="owner">Parent window for dialogs.</param>
        /// <param name="log">Log delegate.</param>
        /// <param name="currentVersion">The version written to <c>version.txt</c>.</param>
        /// <param name="previousVersion">
        /// <c>null</c> for first-run setup: existing files are skipped.
        /// Otherwise an upgrade from this version: every shipped file is overwritten, and one whose content differs from
        /// the new version is first copied to <c>config\backup\&lt;previousVersion&gt;\</c>. LEGAL.md is never overwritten.
        /// </param>
        internal static void DeployReferenceConfig(string baseDir, IWin32Window owner, Action<string> log, string currentVersion, string? previousVersion)
        {
            var upgrade = previousVersion != null;
            string? backupRoot = upgrade ? FindBackupRoot(baseDir, previousVersion!) : null;

            // Ensure directories exist.
            Directory.CreateDirectory(Path.Combine(baseDir, "config", "queries"));
            Directory.CreateDirectory(Path.Combine(baseDir, "config", "transformations"));

            // Deploy all non-legal files.
            foreach (var (resourceSuffix, relativeDest) in DeployedFiles)
            {
                var destPath = Path.Combine(baseDir, relativeDest);
                var exists = File.Exists(destPath);
                if (!upgrade && exists)
                {
                    log($"[Setup] Skipped (exists): {destPath}");
                    continue;
                }

                var content = ReadResource(ResourcePrefix + resourceSuffix);
                if (exists && backupRoot != null && File.ReadAllText(destPath) != content)
                {
                    var backupPath = Path.Combine(backupRoot, relativeDest.Substring(ConfigPrefix.Length));
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupRoot);
                    File.Copy(destPath, backupPath);
                    log($"[Setup] Backed up: {destPath} -> {backupPath}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? baseDir);
                File.WriteAllText(destPath, content, Encoding.UTF8);
                log($"{(upgrade ? "[Setup] Updated" : "[Setup] Deployed")}: {destPath}");
            }

            // Deploy LEGAL.md — first-run only; never overwritten on upgrade.
            var legalDest = Path.Combine(baseDir, LegalRelativeDest);
            if (!File.Exists(legalDest))
            {
                var orgName = PromptOrgName(owner);
                var template = ReadResource(ResourcePrefix + LegalResourceSuffix);
                var content = ApplyOrgName(template, orgName);
                Directory.CreateDirectory(Path.GetDirectoryName(legalDest)!);
                File.WriteAllText(legalDest, content, Encoding.UTF8);
                log($"[Setup] Deployed: {legalDest}");
            }
            else
            {
                log($"[Setup] Skipped (exists): {legalDest}");
            }

            // Write version.txt.
            var versionPath = Path.Combine(baseDir, "version.txt");
            File.WriteAllText(versionPath, currentVersion, Encoding.UTF8);
            log($"[Setup] version.txt written: {currentVersion}");
        }

        /// <summary>Replaces all occurrences of <c>[ORGANISATION NAME]</c> in <paramref name="template"/> with <paramref name="orgName"/>.</summary>
        internal static string ApplyOrgName(string template, string orgName)
        {
            if (string.IsNullOrWhiteSpace(orgName))
            {
                return template;
            }

            return template.Replace("[ORGANISATION NAME]", orgName);
        }

        private static string PromptOrgName(IWin32Window owner)
        {
            // Skip the dialog in headless / test contexts where no real window exists.
            if (owner.Handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            return Interaction.InputBox(
                "Enter your organisation name for the legal notice:",
                "Legal Notice Setup",
                "My Organisation");
        }

        private static bool ConfirmUpgrade(IWin32Window owner, string stored, string current)
        {
            var message =
                $"The plugin has been updated from {stored} to {current}.\n\n" +
                "Would you like to update the reference configuration? The shipped queries, templates and sample specs will be replaced. " +
                "Any of them you have edited are backed up first, under config\\backup\\.\n\n" +
                "Your LEGAL.md and files with other names are not touched.";

            var result = MessageBox.Show(owner, message, "Plugin Updated", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            return result == DialogResult.Yes;
        }

        /// <summary>
        /// Returns the folder that backs up edited shipped files during an upgrade from <paramref name="previousVersion"/>:
        /// <c>config\backup\&lt;previousVersion&gt;</c>, or <c>…-2</c>, <c>…-3</c> when an earlier backup already holds that name,
        /// so an existing backup is never overwritten. The folder is created only when a file is backed up.
        /// </summary>
        private static string FindBackupRoot(string baseDir, string previousVersion)
        {
            var name = string.Join("_", previousVersion.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "unknown";
            }

            var backupDir = Path.Combine(baseDir, "config", "backup");
            var candidate = Path.Combine(backupDir, name);
            for (var n = 2; Directory.Exists(candidate); n++)
            {
                candidate = Path.Combine(backupDir, $"{name}-{n}");
            }

            return candidate;
        }

        private static string CurrentVersion() =>
            Asm.GetName().Version?.ToString() ?? "1.0.0.0";

        private static string ReadResource(string resourceName)
        {
            using var stream = Asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
