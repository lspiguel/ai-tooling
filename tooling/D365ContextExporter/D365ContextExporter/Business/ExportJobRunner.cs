using System;
using System.Threading;

using D365ContextExporter.Models;

using Microsoft.Xrm.Sdk;

namespace D365ContextExporter.Business
{
    /// <summary>Orchestrates a single export run. Phase 1: stub that logs the loaded config.</summary>
    internal sealed class ExportJobRunner
    {
        private readonly IOrganizationService _service;
        private readonly Action<string> _log;

        /// <summary>Initialises a new runner bound to the given Dataverse connection and log sink.</summary>
        /// <param name="service">The connected Dataverse service (not used in Phase 1).</param>
        /// <param name="log">Delegate called for each log line; must be thread-safe.</param>
        public ExportJobRunner(IOrganizationService service, Action<string> log)
        {
            _service = service;
            _log = log;
        }

        /// <summary>Executes the export job (stub: logs config details without querying Dataverse).</summary>
        /// <param name="job">The loaded project configuration.</param>
        /// <param name="baseDir">The project base directory (used in Phase 2 for resolving query files).</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        public void Run(ExportJob job, string baseDir, CancellationToken cancellationToken)
        {
            _log($"[Phase 1 stub] Starting export for project: {job.Project}");
            _log($"  Config: {job.ConfigFilePath}");
            _log($"  Queries defined: {job.Queries.Count}");

            foreach (var query in job.Queries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _log($"  - [{query.Type}] {query.Id} → resultKey: {query.ResultKey}");
            }

            _log("[Phase 1 stub] Run complete. (No queries executed; Phase 2 will implement this.)");
        }
    }
}
