// <copyright file="PythonBootstrapHelper.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Helpers
{
    using System;
    using System.IO;
    using System.Threading;

    using D365ContextExporter.Models;

    /// <summary>Verifies that a usable Python interpreter with Jinja2 installed is available.</summary>
    internal static class PythonBootstrapHelper
    {
        /// <summary>
        /// Runs <c>python -c "import jinja2"</c> to confirm Python and Jinja2 are available.
        /// Throws <see cref="InvalidOperationException"/> with a human-readable message if the check fails.
        /// </summary>
        /// <param name="settings">The Python settings from the project config.</param>
        /// <param name="log">Log delegate for status messages.</param>
        public static void Check(PythonSettings settings, Action<string> log)
        {
            var interpreter = ResolveExecutable(settings);

            var exitCode = ProcessRunner.Run(
                executable: interpreter,
                arguments: "-c \"import jinja2\"",
                workingDirectory: Path.GetTempPath(),
                onStdout: _ => { },
                onStderr: msg => log($"[Python] {msg}"),
                timeoutMs: 10_000,
                cancellationToken: CancellationToken.None);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    "Python or Jinja2 is not available.\n" +
                    $"Ensure Python is on PATH and run: {interpreter} -m pip install jinja2 pyyaml python-dateutil");
            }

            log($"[Python] OK ({interpreter})");
        }

        /// <summary>Returns the interpreter executable: the configured path, or <c>"python"</c> when set to auto.</summary>
        internal static string ResolveExecutable(PythonSettings settings) =>
            string.Equals(settings.Interpreter, "auto", StringComparison.OrdinalIgnoreCase)
                ? "python"
                : settings.Interpreter;
    }
}
