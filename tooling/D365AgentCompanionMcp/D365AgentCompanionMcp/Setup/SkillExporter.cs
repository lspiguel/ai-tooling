// <copyright file="SkillExporter.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Setup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// <b>Export skills…</b> (SPEC §8.7): writes each embedded <c>agent-companion-*</c> skill as its own zip, holding
    /// one folder named for the skill, so the exported skills always match the plugin version. The skills are
    /// embedded at build time from <c>skills/agent-companion-*</c> under <c>Skills/&lt;skill&gt;/&lt;path&gt;</c>.
    /// </summary>
    internal static class SkillExporter
    {
        private const string ResourcePrefix = "Skills/";

        private static readonly Assembly Plugin = typeof(SkillExporter).Assembly;

        /// <summary>Lists the embedded skills.</summary>
        /// <returns>The skill names, in order.</returns>
        public static IReadOnlyList<string> SkillNames() =>
            Entries().Select(e => e.Skill).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Lists the files an export to a folder would write.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns>One zip path per skill.</returns>
        public static IReadOnlyList<string> ZipPaths(string folder) => SkillNames().Select(n => Path.Combine(folder, $"{n}.zip")).ToList();

        /// <summary>Writes one zip per skill, replacing a zip of the same name.</summary>
        /// <param name="folder">The folder the person chose.</param>
        /// <returns>The zips written.</returns>
        public static IReadOnlyList<string> Export(string folder)
        {
            var written = new List<string>();
            foreach (var skill in Entries().GroupBy(e => e.Skill, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var path = Path.Combine(folder, $"{skill.Key}.zip");
                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    foreach (var entry in skill.OrderBy(e => e.EntryName, StringComparer.Ordinal))
                    {
                        using (var source = Plugin.GetManifestResourceStream(entry.ResourceName)
                            ?? throw new InvalidOperationException($"The skill resource {entry.ResourceName} is missing from the plugin."))
                        using (var target = zip.CreateEntry(entry.EntryName, CompressionLevel.Optimal).Open())
                        {
                            source.CopyTo(target);
                        }
                    }
                }

                written.Add(path);
            }

            return written;
        }

        private static IEnumerable<SkillEntry> Entries()
        {
            foreach (var resource in Plugin.GetManifestResourceNames())
            {
                // MSBuild keeps the directory separator of the item's recursive part; zip entries use '/'.
                var normalized = resource.Replace('\\', '/');
                if (!normalized.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var entryName = normalized.Substring(ResourcePrefix.Length);
                var slash = entryName.IndexOf('/');
                if (slash > 0)
                {
                    yield return new SkillEntry(entryName.Substring(0, slash), entryName, resource);
                }
            }
        }

        private sealed class SkillEntry
        {
            public SkillEntry(string skill, string entryName, string resourceName)
            {
                this.Skill = skill;
                this.EntryName = entryName;
                this.ResourceName = resourceName;
            }

            public string Skill { get; }

            public string EntryName { get; }

            public string ResourceName { get; }
        }
    }
}
