// <copyright file="ProcessRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Helpers
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>Launches an external process, streams stdout/stderr, enforces a timeout, and returns the exit code.</summary>
    internal static class ProcessRunner
    {
        /// <summary>
        /// Runs an external executable and returns its exit code.
        /// </summary>
        /// <param name="executable">Full path or name of the executable.</param>
        /// <param name="arguments">Command-line arguments.</param>
        /// <param name="workingDirectory">Working directory for the process.</param>
        /// <param name="onStdout">Callback invoked for each stdout line.</param>
        /// <param name="onStderr">Callback invoked for each stderr line.</param>
        /// <param name="timeoutMs">Maximum milliseconds to wait before killing the process.</param>
        /// <param name="cancellationToken">Token that kills the process if cancelled.</param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown when the process does not exit within <paramref name="timeoutMs"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
        public static int Run(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> onStdout,
            Action<string> onStderr,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) onStdout(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) onStderr(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (cancellationToken.Register(() =>
            {
                try { process.Kill(); }
                catch (InvalidOperationException) { }
            }))
            {
                var completed = process.WaitForExit(timeoutMs / 2);

                cancellationToken.ThrowIfCancellationRequested();

                if (!completed)
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) { }

                    throw new TimeoutException(
                        $"Process '{executable}' timed out after {timeoutMs / 1000}s.");
                }

                // Second WaitForExit drains async stdout/stderr readers after the process exits.
                // Use a bounded wait: child processes spawned by the process (e.g. pip sub-invocations)
                // can inherit the pipe handles and keep them open, causing an infinite hang.
                process.WaitForExit(timeoutMs / 2);
                return process.ExitCode;
            }
        }
    }
}
