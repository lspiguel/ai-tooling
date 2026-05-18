// <copyright file="PythonInvoker.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Orchestration
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;

    using IronPython.Hosting;

    using Lspiguel.Xrm.D365ContextExporter.Helpers;
    using Lspiguel.Xrm.D365ContextExporter.Models;

    using Microsoft.Scripting.Hosting;

    /// <summary>Thrown when transform.py fails during IronPython execution.</summary>
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

        /// <summary>Gets the exit code (always -1 for IronPython failures).</summary>
        public int ExitCode { get; }

        /// <summary>Gets the Python traceback detail from IronPython.</summary>
        public string StderrSnippet { get; }
    }

    /// <summary>Invokes transform.py in-process via IronPython 3.</summary>
    internal sealed class PythonInvoker
    {
        private readonly Action<string> log;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonInvoker"/> class.
        /// </summary>
        /// <param name="log">Log delegate; called for each output line.</param>
        public PythonInvoker(Action<string> log)
        {
            this.log = log;
        }

        /// <summary>Invokes transform.py from the base directory's config\transformations\ folder via IronPython.</summary>
        /// <param name="job">The spec configuration.</param>
        /// <param name="baseDir">The base directory containing the config/ subdirectory.</param>
        /// <param name="runDir">The run-specific directory containing intermediate.json.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public void Invoke(ExportJob job, string baseDir, string runDir, CancellationToken cancellationToken)
        {
            var transformationsDir = Path.Combine(baseDir, "config", "transformations");
            var transformScript = Path.Combine(transformationsDir, "transform.py");

            if (!File.Exists(transformScript))
            {
                throw new FileNotFoundException(
                    $"transform.py not found at: {transformScript}. Run first-time setup to deploy it.");
            }

            var templatePath = Path.Combine(transformationsDir, job.Transformation);
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    $"Template '{job.Transformation}' not found at: {templatePath}");
            }

            var engine = Python.CreateEngine();

            var stdoutStream = new LoggingStream(line => this.log($"[Python] {line}"));
            var stderrStream = new LoggingStream(line => this.log($"[Python:ERR] {line}"));
            engine.Runtime.IO.SetOutput(stdoutStream, Encoding.UTF8);
            engine.Runtime.IO.SetErrorOutput(stderrStream, Encoding.UTF8);

            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            var libDir = Path.Combine(pluginDir, "Lib");
            var pylibsZip = Path.Combine(transformationsDir, "pylibs.zip");

            var paths = engine.GetSearchPaths();
            if (Directory.Exists(libDir))
            {
                paths.Add(libDir);
            }

            if (File.Exists(pylibsZip))
            {
                paths.Add(pylibsZip);
            }

            paths.Add(transformationsDir);
            engine.SetSearchPaths(paths);

            var scope = engine.CreateScope();
            scope.SetVariable("input_path", Path.Combine(runDir, "intermediate.json"));
            scope.SetVariable("template", templatePath);
            scope.SetVariable("out_dir", runDir);
            scope.SetVariable("spec", job.Spec);

            this.log("[Python] Running transform.py via IronPython ...");
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                engine.ExecuteFile(transformScript, scope);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var ops = engine.GetService<ExceptionOperations>();
                var detail = ops != null ? ops.FormatException(ex) : ex.ToString();
                this.log($"[Python:ERR] {detail}");
                throw new PythonInvocationException(-1,
                    "IronPython execution failed. Check the log for details.", detail);
            }
            finally
            {
                stdoutStream.Flush();
                stderrStream.Flush();
            }
        }
    }
}
