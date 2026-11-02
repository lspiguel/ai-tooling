// <copyright file="LogFileWriter.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;

    /// <summary>
    /// <b>Save as…</b>: the only way the log reaches disk (SPEC §8.6, R13). The file holds the lines exactly as
    /// shown, truncation included — never full arguments or results.
    /// </summary>
    internal static class LogFileWriter
    {
        /// <summary>The heading above the host log in a saved file.</summary>
        public const string HostLogHeading = "Host log";

        /// <summary>Builds the default file name, <c>AgentCompanion-[ConnectionName]-yyyyMMdd-HHmmss.txt</c>.</summary>
        /// <param name="connectionName">The tab's connection name, or <c>null</c> when unbound.</param>
        /// <param name="savedLocal">The save time, local.</param>
        /// <returns>A file name safe for Windows.</returns>
        public static string DefaultFileName(string? connectionName, DateTime savedLocal)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((connectionName ?? "unbound").Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return $"AgentCompanion-{name}-{savedLocal.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.txt";
        }

        /// <summary>Tells whether a folder is inside a Git working tree: it, or a parent, contains <c>.git</c>.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns><c>true</c> when the folder is inside a working tree.</returns>
        public static bool IsInsideGitWorkingTree(string folder)
        {
            for (var directory = new DirectoryInfo(folder); directory != null; directory = directory.Parent)
            {
                var marker = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(marker) || File.Exists(marker))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Composes the file content: a header line, the tab's call log, then the host log under its heading.</summary>
        /// <param name="connectionName">The tab's connection name, or <c>null</c> when unbound.</param>
        /// <param name="organizationName">The organization's friendly name, or <c>null</c> when unbound.</param>
        /// <param name="savedLocal">The save time, local.</param>
        /// <param name="callLines">The call log lines.</param>
        /// <param name="hostLines">The host log lines.</param>
        /// <returns>The file content.</returns>
        public static string Compose(string? connectionName, string? organizationName, DateTime savedLocal, IEnumerable<string> callLines, IEnumerable<string> hostLines)
        {
            var header = $"Agent Companion MCP call log · {CallLogFormatter.Sanitize(connectionName ?? "no connection")} · {CallLogFormatter.Sanitize(organizationName ?? "no organization")} · saved {ValueFormats.FormatLocalTimestamp(savedLocal)}";
            var content = new StringBuilder().AppendLine(header).AppendLine();
            foreach (var line in callLines)
            {
                content.AppendLine(line);
            }

            content.AppendLine().AppendLine(HostLogHeading);
            foreach (var line in hostLines)
            {
                content.AppendLine(line);
            }

            return content.ToString();
        }

        /// <summary>Writes the content as UTF-8.</summary>
        /// <param name="path">The file the person chose.</param>
        /// <param name="content">The content from <see cref="Compose"/>.</param>
        public static void Write(string path, string content) => File.WriteAllText(path, content, new UTF8Encoding(true));
    }
}
