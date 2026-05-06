// <copyright file="PythonBootstrapHelper.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Helpers
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Lspiguel.Xrm.D365ContextExporter.Models;

    /// <summary>Verifies that a usable Python interpreter with Jinja2 installed is available.</summary>
    internal static class PythonBootstrapHelper
    {
        private static readonly char[] PackageNameSeparators = ['=', '>', '<', '~', '!', '[', ';', ' '];

        /// <summary>
        /// Runs <c>python -c "import jinja2"</c> to confirm Python and Jinja2 are available.
        /// Throws <see cref="InvalidOperationException"/> with a human-readable message if the check fails.
        /// </summary>
        /// <param name="settings">The Python settings from the project config.</param>
        /// <param name="log">Log delegate for status messages.</param>
        public static void Check(PythonSettings settings, Action<string> log)
        {
            var interpreter = ResolveExecutable(settings);

            var requirementsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "D365ContextExporter",
                "python",
                "requirements.txt");

            var packages = ReadPackageNames(requirementsPath);
            var exitCode = ProcessRunner.Run(
                executable: interpreter,
                arguments: $"-m pip show {string.Join(" ", packages)}",
                workingDirectory: Path.GetTempPath(),
                onStdout: _ => { },
                onStderr: msg => log($"[Python] {msg}"),
                timeoutMs: 10_000,
                cancellationToken: CancellationToken.None);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    "Python or one or more required packages are not available.\n" +
                    $"Ensure Python is on PATH and run: {interpreter} -m pip install -r \"{requirementsPath}\"");
            }

            log($"[Python] OK ({interpreter})");
        }

        private static string[] ReadPackageNames(string requirementsPath)
        {
            if (!File.Exists(requirementsPath))
            {
                return [];
            }

            return [..File.ReadAllLines(requirementsPath)
                .Select(l => l.Trim())
                .Where(t => t.Length > 0 && !t.StartsWith("#"))
                .Select(t => { var i = t.IndexOfAny(PackageNameSeparators);
                    return i < 0 ? t : t.Substring(0, i);
                })];
        }

        /// <summary>Returns the interpreter executable: the configured path, or <c>"python"</c> when set to auto.</summary>
        internal static string ResolveExecutable(PythonSettings settings) =>
            string.Equals(settings.Interpreter, "auto", StringComparison.OrdinalIgnoreCase)
                ? "python"
                : settings.Interpreter;
    }
}
