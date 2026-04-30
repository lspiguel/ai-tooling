// <copyright file="PathResolver.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>File-system helpers used to locate spec config files and resolve paths.</summary>
    public static class PathResolver
    {
        /// <summary>Expands <c>%VAR%</c> tokens, then resolves <paramref name="path"/> relative to <paramref name="baseDir"/>.</summary>
        /// <param name="path">The path to expand and resolve. May be absolute or relative.</param>
        /// <param name="baseDir">The directory used as the root when <paramref name="path"/> is relative.</param>
        /// <returns>The fully-qualified, expanded path.</returns>
        public static string Resolve(string path, string baseDir)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }

        /// <summary>Returns all <c>*.context-exporter-config.json</c> files directly under <c>&lt;baseDir&gt;/config/</c>.</summary>
        /// <param name="baseDir">The base directory.</param>
        /// <returns>File paths, or an empty sequence when the <c>config/</c> subfolder does not exist.</returns>
        public static IEnumerable<string> DiscoverSpecConfigs(string baseDir)
        {
            var configDir = Path.Combine(baseDir, "config");
            if (!Directory.Exists(configDir))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(configDir, "*.context-exporter-config.json", SearchOption.TopDirectoryOnly);
        }

        /// <summary>Derives the spec display name from a config file path by stripping the well-known suffix.</summary>
        /// <param name="configFilePath">Full path to a <c>*.context-exporter-config.json</c> file.</param>
        /// <returns>The spec name (e.g. <c>"Contoso"</c> for <c>"Contoso.context-exporter-config.json"</c>).</returns>
        public static string SpecNameFromPath(string configFilePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(configFilePath);
            const string suffix = ".context-exporter-config";
            return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - suffix.Length)
                : fileName;
        }
    }
}
