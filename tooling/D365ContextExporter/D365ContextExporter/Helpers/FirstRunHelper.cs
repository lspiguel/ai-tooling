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
        private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

        // Files deployed by DeployReferenceConfig, grouped by target subfolder relative to baseDir.
        private static readonly (string Resource, string RelativeDest)[] DeployedFiles =
        {
            // Queries
            ("SampleConfig.queries.app-modules.fetch.xml",            @"config\queries\app-modules.fetch.xml"),
            ("SampleConfig.queries.custom-api-responses.fetch.xml",   @"config\queries\custom-api-responses.fetch.xml"),
            ("SampleConfig.queries.custom-apis.fetch.xml",            @"config\queries\custom-apis.fetch.xml"),
            ("SampleConfig.queries.env-vars.fetch.xml",               @"config\queries\env-vars.fetch.xml"),
            ("SampleConfig.queries.plugin-assemblies.fetch.xml",      @"config\queries\plugin-assemblies.fetch.xml"),
            ("SampleConfig.queries.sdk-steps.fetch.xml",              @"config\queries\sdk-steps.fetch.xml"),
            ("SampleConfig.queries.security-roles.fetch.xml",         @"config\queries\security-roles.fetch.xml"),
            ("SampleConfig.queries.solutions-and-entities.fetch.xml", @"config\queries\solutions-and-entities.fetch.xml"),
            ("SampleConfig.queries.solutions-components.fetch.xml",   @"config\queries\solutions-components.fetch.xml"),
            ("SampleConfig.queries.solutions-detail.fetch.xml",       @"config\queries\solutions-detail.fetch.xml"),
            ("SampleConfig.queries.solutions.fetch.xml",              @"config\queries\solutions.fetch.xml"),
            ("SampleConfig.queries.workflows.fetch.xml",              @"config\queries\workflows.fetch.xml"),

            // Transformations — templates
            ("SampleConfig.transformations.entity-dictionary.j2",    @"config\transformations\entity-dictionary.j2"),
            ("SampleConfig.transformations.forms-and-views.j2",      @"config\transformations\forms-and-views.j2"),
            ("SampleConfig.transformations.optionsets.j2",           @"config\transformations\optionsets.j2"),
            ("SampleConfig.transformations.security-model.j2",       @"config\transformations\security-model.j2"),
            ("SampleConfig.transformations.solution-inventory.j2",   @"config\transformations\solution-inventory.j2"),
            ("SampleConfig.transformations.solutions-reference.j2",  @"config\transformations\solutions-reference.j2"),

            // Spec configs
            ("SampleConfig.EntityDictionary.context-exporter-config.json",  @"config\EntityDictionary.context-exporter-config.json"),
            ("SampleConfig.SecurityModel.context-exporter-config.json",     @"config\SecurityModel.context-exporter-config.json"),
            ("SampleConfig.SolutionsReference.context-exporter-config.json", @"config\SolutionsReference.context-exporter-config.json"),
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

        /// <summary>Extracts all embedded sample files to <paramref name="baseDir"/>.</summary>
        /// <param name="baseDir">The target base directory.</param>
        /// <param name="owner">Parent window for dialogs.</param>
        /// <param name="log">Log delegate.</param>
        /// <param name="overwrite">
        /// When <c>false</c> (first-run): existing files are skipped.
        /// When <c>true</c> (upgrade): all files except LEGAL.md are overwritten.
        /// </param>
        public static void DeployReferenceConfig(string baseDir, IWin32Window owner, Action<string> log, bool overwrite = false)
        {
            // Ensure directories exist.
            Directory.CreateDirectory(Path.Combine(baseDir, "config", "queries"));
            Directory.CreateDirectory(Path.Combine(baseDir, "config", "transformations"));

            // Deploy all non-legal files.
            foreach (var (resourceSuffix, relativeDest) in DeployedFiles)
            {
                var destPath = Path.Combine(baseDir, relativeDest);
                if (!overwrite && File.Exists(destPath))
                {
                    log($"[Setup] Skipped (exists): {destPath}");
                    continue;
                }

                var resourceName = "Lspiguel.Xrm.D365ContextExporter." + resourceSuffix;
                WriteResource(resourceName, destPath, log, overwrite ? "[Setup] Updated" : "[Setup] Deployed");
            }

            // Deploy LEGAL.md — first-run only; never overwritten on upgrade.
            var legalDest = Path.Combine(baseDir, LegalRelativeDest);
            if (!File.Exists(legalDest))
            {
                var orgName = PromptOrgName(owner);
                var legalResource = "Lspiguel.Xrm.D365ContextExporter." + LegalResourceSuffix;
                var template = ReadResource(legalResource);
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
            File.WriteAllText(versionPath, CurrentVersion(), Encoding.UTF8);
            log($"[Setup] version.txt written: {CurrentVersion()}");
        }

        /// <summary>Checks <paramref name="baseDir"/> for a version mismatch and offers an upgrade if needed.</summary>
        public static void CheckVersion(string baseDir, IWin32Window owner, Action<string> log)
        {
            var versionPath = Path.Combine(baseDir, "version.txt");
            var current = CurrentVersion();

            if (!File.Exists(versionPath))
            {
                // Directory configured before this feature shipped — write version silently.
                File.WriteAllText(versionPath, current, Encoding.UTF8);
                return;
            }

            var stored = File.ReadAllText(versionPath).Trim();
            if (stored == current)
            {
                return;
            }

            var result = MessageBox.Show(
                owner,
                $"The plugin has been updated from {stored} to {current}.\n\n" +
                "Would you like to update the reference configuration files (queries, templates, and sample specs)? " +
                "Your LEGAL.md and any custom files will not be modified.",
                "Plugin Updated",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                DeployReferenceConfig(baseDir, owner, log, overwrite: true);
            }
            else
            {
                File.WriteAllText(versionPath, current, Encoding.UTF8);
                log("[Setup] Upgrade skipped by user.");
            }
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

        private static string CurrentVersion() =>
            Asm.GetName().Version?.ToString() ?? "1.0.0.0";

        private static string ReadResource(string resourceName)
        {
            using var stream = Asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static void WriteResource(string resourceName, string destPath, Action<string> log, string logPrefix)
        {
            var content = ReadResource(resourceName);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllText(destPath, content, Encoding.UTF8);
            log($"{logPrefix}: {destPath}");
        }
    }
}
