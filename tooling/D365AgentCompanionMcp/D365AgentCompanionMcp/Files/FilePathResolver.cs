// <copyright file="FilePathResolver.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Files
{
    using System;
    using System.IO;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Turns a path from the agent into a file under the File root, or refuses it (SPEC §6.8, File root). The checks
    /// run while a call is preparing, before any dialog: normalized first, inside the root, no links, and the file exists.
    /// </summary>
    internal static class FilePathResolver
    {
        /// <summary>Resolves a path.</summary>
        /// <param name="fileRoot">The File root folder.</param>
        /// <param name="path">The agent's path: relative to the File root, or absolute and inside it.</param>
        /// <param name="argument">The argument the path came from, such as <c>items[0].path</c>, for the error.</param>
        /// <returns>The full path of the file.</returns>
        public static string Resolve(string fileRoot, string path, string argument)
        {
            string root;
            string full;
            try
            {
                root = EnsureSeparator(Path.GetFullPath(fileRoot));
                var normalized = path.Replace('/', Path.DirectorySeparatorChar);
                full = Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(root, normalized));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                throw Refuse(argument, fileRoot, path, null, $"{argument}: '{path}' is not a valid path.");
            }

            if (full.Length <= root.Length || !full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw Refuse(argument, fileRoot, path, full, $"{argument}: '{path}' is outside the File root {fileRoot}. Pass a path relative to it, or an absolute path inside it.");
            }

            // No link anywhere between the File root and the file: a link can lead outside while the path text stays inside.
            var current = root.TrimEnd(Path.DirectorySeparatorChar);
            foreach (var segment in full.Substring(root.Length).Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    throw Refuse(argument, fileRoot, path, full, $"{argument}: '{path}' does not exist (resolved to {full}).");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw Refuse(argument, fileRoot, path, full, $"{argument}: '{path}' cannot be read: {ex.Message}");
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw Refuse(argument, fileRoot, path, full, $"{argument}: '{path}' goes through a symbolic link or junction ({current}), which can lead outside the File root.");
                }
            }

            if (!File.Exists(full))
            {
                throw Refuse(argument, fileRoot, path, full, $"{argument}: '{path}' is not a file (resolved to {full}).");
            }

            return full;
        }

        /// <summary>Makes a path relative to the File root for display.</summary>
        /// <param name="fileRoot">The File root.</param>
        /// <param name="full">A full path inside it.</param>
        /// <returns>The path relative to the root, with forward slashes.</returns>
        public static string Relative(string fileRoot, string full)
        {
            var root = EnsureSeparator(Path.GetFullPath(fileRoot));
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length).Replace('\\', '/') : full;
        }

        private static string EnsureSeparator(string folder) =>
            folder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? folder : folder + Path.DirectorySeparatorChar;

        private static ToolException Refuse(string argument, string fileRoot, string path, string? resolved, string message) =>
            new ToolException(
                ToolErrorCodes.InvalidArgument,
                message,
                new JObject { ["argument"] = argument, ["fileRoot"] = fileRoot, ["path"] = path, ["resolvedPath"] = resolved });
    }
}
