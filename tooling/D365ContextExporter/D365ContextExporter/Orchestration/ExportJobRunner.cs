// <copyright file="ExportJobRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Orchestration
{
    using System;
    using System.Threading;

    using D365ContextExporter.Models;

    using Microsoft.Xrm.Sdk;

    /// <summary>Orchestrates a single export run. Phase 1: stub that logs the loaded config.</summary>
    internal sealed class ExportJobRunner
    {
        private readonly IOrganizationService service;
        private readonly Action<string> log;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportJobRunner"/> class.Initialises a new runner bound to the given Dataverse connection and log sink.
        /// </summary>
        /// <param name="service">The connected Dataverse service (not used in Phase 1).</param>
        /// <param name="log">Delegate called for each log line; must be thread-safe.</param>
        public ExportJobRunner(IOrganizationService service, Action<string> log)
        {
            this.service = service;
            this.log = log;
        }

        /// <summary>Executes the export job (stub: logs config details without querying Dataverse).</summary>
        /// <param name="job">The loaded project configuration.</param>
        /// <param name="baseDir">The project base directory (used in Phase 2 for resolving query files).</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        public void Run(ExportJob job, string baseDir, CancellationToken cancellationToken)
        {
            this.log($"[Phase 1 stub] Starting export for project: {job.Project}");
            this.log($"  Config: {job.ConfigFilePath}");
            this.log($"  Queries defined: {job.Queries.Count}");

            foreach (var query in job.Queries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.log($"  - [{query.Type}] {query.Id} → resultKey: {query.ResultKey}");
            }

            this.log("[Phase 1 stub] Run complete. (No queries executed; Phase 2 will implement this.)");
        }
    }
}
