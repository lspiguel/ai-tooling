// <copyright file="ExportBatchRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Orchestration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Lspiguel.Xrm.D365ContextExporter.Helpers;
    using Lspiguel.Xrm.D365ContextExporter.Models;

    /// <summary>
    /// Runs several specs one after another. A spec that fails is logged and the rest still run;
    /// cancellation stops the batch.
    /// </summary>
    internal sealed class ExportBatchRunner
    {
        private readonly Func<ExportJob, string, CancellationToken, string> runSpec;
        private readonly Action<string> log;
        private readonly Action<int, int, string>? onSpecStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportBatchRunner"/> class.
        /// </summary>
        /// <param name="runSpec">Runs one spec — (job, baseDir, token) to run directory; normally <see cref="ExportJobRunner.Run"/>.</param>
        /// <param name="log">Delegate called for each log line; must be thread-safe.</param>
        /// <param name="onSpecStarted">Optional delegate called with (current, total, specName) before each spec runs.</param>
        public ExportBatchRunner(
            Func<ExportJob, string, CancellationToken, string> runSpec,
            Action<string> log,
            Action<int, int, string>? onSpecStarted = null)
        {
            this.runSpec = runSpec;
            this.log = log;
            this.onSpecStarted = onSpecStarted;
        }

        /// <summary>Loads and runs each spec in order.</summary>
        /// <param name="configFilePaths">The spec files to run, in order.</param>
        /// <param name="baseDir">The base directory.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests; cancelling stops the batch.</param>
        /// <returns>Which specs succeeded and which failed, with the reason.</returns>
        public ExportBatchResult Run(IReadOnlyList<string> configFilePaths, string baseDir, CancellationToken cancellationToken)
        {
            var result = new ExportBatchResult();
            var total = configFilePaths.Count;
            this.log($"[Batch] Running {total} specs.");

            for (var i = 0; i < total; i++)
            {
                var path = configFilePaths[i];
                var name = PathResolver.SpecNameFromPath(path);

                if (cancellationToken.IsCancellationRequested)
                {
                    this.log("[Batch] Cancelled.");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                this.onSpecStarted?.Invoke(i + 1, total, name);
                this.log($"[Batch] Spec {i + 1} / {total}: {name}");

                try
                {
                    var job = ExportJob.Load(path);
                    this.runSpec(job, baseDir, cancellationToken);
                    result.Succeeded.Add(name);
                }
                catch (OperationCanceledException)
                {
                    this.log("[Batch] Cancelled.");
                    throw;
                }
                catch (Exception ex)
                {
                    var reason = ex.GetBaseException().Message;
                    this.log($"[Batch] ERROR in spec '{name}': {reason}");
                    result.Failed.Add(new KeyValuePair<string, string>(name, reason));
                }
            }

            var summary = $"[Batch] Finished: {result.Succeeded.Count} of {total} specs succeeded.";
            if (result.Failed.Count > 0)
            {
                var failedNames = new List<string>();
                foreach (var failure in result.Failed)
                {
                    failedNames.Add(failure.Key);
                }

                summary += $" Failed: {string.Join(", ", failedNames)}.";
            }

            this.log(summary);
            return result;
        }
    }
}
