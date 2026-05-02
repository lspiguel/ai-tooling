// <copyright file="PythonInvoker.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Orchestration
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;

    using D365ContextExporter.Helpers;
    using D365ContextExporter.Models;

    /// <summary>Thrown when transform.py exits with a non-zero code.</summary>
    internal sealed class PythonInvocationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PythonInvocationException"/> class.
        /// </summary>
        public PythonInvocationException(int exitCode, string message, string stderrSnippet)
            : base(message)
        {
            this.ExitCode = exitCode;
            this.StderrSnippet = stderrSnippet;
        }

        /// <summary>Gets the exit code returned by the Python process.</summary>
        public int ExitCode { get; }

        /// <summary>Gets the last ~500 characters of stderr output.</summary>
        public string StderrSnippet { get; }
    }

    /// <summary>Resolves a Python interpreter and invokes transform.py as a child process.</summary>
    internal sealed class PythonInvoker
    {
        private const int DefaultTimeoutMs = 5 * 60 * 1000;

        private readonly Action<string> log;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonInvoker"/> class.
        /// </summary>
        /// <param name="log">Log delegate; called for each output line.</param>
        public PythonInvoker(Action<string> log)
        {
            this.log = log;
        }

        /// <summary>Invokes transform.py and copies the output to the spec directory.</summary>
        /// <param name="job">The spec configuration.</param>
        /// <param name="baseDir">The base directory containing the config/ and output/ subdirectories.</param>
        /// <param name="runDir">The run-specific directory containing intermediate.json.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public void Invoke(ExportJob job, string baseDir, string runDir, CancellationToken cancellationToken)
        {
            var interpreter = ResolveInterpreter(job.Python, baseDir);
            this.log($"[Python] Using interpreter: {interpreter}");

            var pythonDir = EnsureScripts();
            var transformScript = Path.Combine(pythonDir, "transform.py");

            var templatePath = Path.Combine(baseDir, "config", "transformations", job.Transformation);
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    $"Template '{job.Transformation}' not found at: {templatePath}");
            }

            var intermediatePath = Path.Combine(runDir, "intermediate.json");
            var arguments = $"\"{transformScript}\""
                + $" --input \"{intermediatePath}\""
                + $" --template \"{templatePath}\""
                + $" --out \"{runDir}\""
                + $" --spec \"{job.Spec}\"";

            this.log($"[Python] Running transform.py ...");

            var stderr = new StringBuilder();
            var exitCode = ProcessRunner.Run(
                executable: interpreter,
                arguments: arguments,
                workingDirectory: runDir,
                onStdout: line => this.log($"[Python] {line}"),
                onStderr: line =>
                {
                    this.log($"[Python:ERR] {line}");
                    stderr.AppendLine(line);
                },
                timeoutMs: DefaultTimeoutMs,
                cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                var snippet = stderr.ToString();
                if (snippet.Length > 500)
                    snippet = snippet.Substring(snippet.Length - 500);
                throw new PythonInvocationException(
                    exitCode,
                    $"transform.py exited with code {exitCode}. Check the log for details.",
                    snippet);
            }

            this.CopyOutputToSpecDir(runDir, baseDir, job.Spec);
        }

        private static string EnsureScripts()
        {
            var pythonDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "D365ContextExporter",
                "python");
            Directory.CreateDirectory(pythonDir);
            ExtractResource("D365ContextExporter.python.transform.py", Path.Combine(pythonDir, "transform.py"));
            ExtractResource("D365ContextExporter.python.filters.py", Path.Combine(pythonDir, "filters.py"));
            ExtractResource("D365ContextExporter.python.requirements.txt", Path.Combine(pythonDir, "requirements.txt"));
            return pythonDir;
        }

        private static void ExtractResource(string resourceName, string destPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            File.WriteAllText(destPath, reader.ReadToEnd(), Encoding.UTF8);
        }

        private static string ResolveInterpreter(PythonSettings settings, string baseDir) =>
            PythonBootstrapHelper.ResolveExecutable(settings);

        private void CopyOutputToSpecDir(string runDir, string baseDir, string specName)
        {
            var sourceFile = Path.Combine(runDir, "output.md");
            if (!File.Exists(sourceFile))
            {
                this.log("[Python] Warning: output.md not found in run directory after transform.");
                return;
            }

            var outputDir = Path.Combine(baseDir, "output");
            Directory.CreateDirectory(outputDir);

            var destFile = Path.Combine(outputDir, $"{specName}.context.md");
            File.Copy(sourceFile, destFile, overwrite: true);
            this.log($"[Python] Output copied to: {destFile}");
        }
    }
}
