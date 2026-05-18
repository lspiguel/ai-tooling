// <copyright file="ExportJobRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Orchestration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading;

    using Lspiguel.Xrm.D365ContextExporter.Helpers;
    using Lspiguel.Xrm.D365ContextExporter.Models;
    using Lspiguel.Xrm.D365ContextExporter.Queries;

    using McTools.Xrm.Connection;

    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Tooling.Connector;

    /// <summary>Orchestrates a single export run: executes all queries, writes intermediate outputs, and invokes Python.</summary>
    internal sealed class ExportJobRunner
    {
        private readonly IOrganizationService service;
        private readonly ConnectionDetail connectionDetail;
        private readonly Action<string> log;
        private readonly Action<int, int, string>? onProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportJobRunner"/> class.
        /// </summary>
        /// <param name="service">The connected Dataverse service.</param>
        /// <param name="connectionDetail">The XrmToolBox connection detail (supplies environment URL and org name).</param>
        /// <param name="log">Delegate called for each log line; must be thread-safe.</param>
        /// <param name="onProgress">Optional delegate called with (current, total, queryId) after each query completes.</param>
        public ExportJobRunner(
            IOrganizationService service,
            ConnectionDetail connectionDetail,
            Action<string> log,
            Action<int, int, string>? onProgress = null)
        {
            this.service = service;
            this.connectionDetail = connectionDetail;
            this.log = log;
            this.onProgress = onProgress;
        }

        /// <summary>Executes all queries, writes intermediate JSON, and invokes Python to produce output.md.</summary>
        /// <param name="job">The loaded spec configuration.</param>
        /// <param name="baseDir">The base directory.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>The run directory path where output.md was written.</returns>
        public string Run(ExportJob job, string baseDir, CancellationToken cancellationToken)
        {
            ConfigValidator.Validate(job, baseDir);

            this.log($"[Export] Starting spec '{job.Spec}' ({job.Queries.Count} queries).");

            var runDir = Path.Combine(baseDir, "runs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(runDir);
            this.log($"[Export] Run directory: {runDir}");

            var environmentUrl = this.connectionDetail.WebApplicationUrl?.TrimEnd('/') ?? string.Empty;
            var orgName = this.connectionDetail.OrganizationFriendlyName ?? this.connectionDetail.Organization ?? string.Empty;

            var fetchRunner = new FetchXmlQueryRunner(this.service, this.log);
            var webApiRunner = new WebApiQueryRunner(environmentUrl, this.GetToken, new HttpClient(), this.log);
            var metaRunner = new MetadataQueryRunner(this.service, this.log);

            var results = new Dictionary<string, object>();
            var failures = new List<Exception>();
            var total = job.Queries.Count;

            for (var i = 0; i < total; i++)
            {
                var query = job.Queries[i];

                if (cancellationToken.IsCancellationRequested)
                {
                    this.log("[Export] Cancelled.");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    object result = query.Type switch
                    {
                        "fetchxml" => EntityJsonSerializer.SerializeEntities(
                            fetchRunner.Run(query, baseDir, cancellationToken),
                            job.Output),
                        "webapi" => webApiRunner.Run(query, cancellationToken),
                        "metadata" => metaRunner.Run(query, cancellationToken),
                        _ => throw new NotSupportedException($"Unknown query type '{query.Type}' for query '{query.Id}'."),
                    };

                    IntermediateJsonBuilder.WriteQueryResult(runDir, query.Id, result);
                    results[query.ResultKey] = result;
                    this.log($"[Export] '{query.Id}' completed in {sw.Elapsed.TotalSeconds:F1}s.");
                    this.onProgress?.Invoke(i + 1, total, query.Id);
                }
                catch (OperationCanceledException)
                {
                    this.log("[Export] Cancelled.");
                    throw;
                }
                catch (Exception ex)
                {
                    this.log($"[Export] ERROR in query '{query.Id}': {ex.Message}");
                    failures.Add(new Exception($"Query '{query.Id}' failed: {ex.Message}", ex));
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException($"{failures.Count} of {job.Queries.Count} queries failed.", failures);
            }

            if (results.Count > 0)
            {
                var intermediatePath = IntermediateJsonBuilder.WriteIntermediate(
                    runDir, job, environmentUrl, orgName, results);
                this.log($"[Export] Intermediate JSON written: {intermediatePath}");

                var invoker = new PythonInvoker(this.log);
                invoker.Invoke(job, baseDir, runDir, cancellationToken);
                this.CopyOutputToSpecDir(runDir, baseDir, job.Spec);
                this.PrependLegalNotice(job, baseDir);
            }

            this.log($"[Export] Run complete. Outputs in: {runDir}");
            return runDir;
        }

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

        private void PrependLegalNotice(ExportJob job, string baseDir)
        {
            if (string.IsNullOrEmpty(job.Legal))
            {
                return;
            }

            var legalPath = PathResolver.Resolve(job.Legal, baseDir);
            if (!File.Exists(legalPath))
            {
                this.log($"[Legal] Warning: LEGAL.md not found at '{legalPath}' — skipping legal notice prepend.");
                return;
            }

            var destFile = Path.Combine(baseDir, "output", $"{job.Spec}.context.md");
            if (!File.Exists(destFile))
            {
                return;
            }

            var legalContent = File.ReadAllText(legalPath);
            var existingContent = File.ReadAllText(destFile);
            File.WriteAllText(destFile, legalContent + "\n\n" + existingContent);
            this.log($"[Legal] Legal notice prepended to: {destFile}");
        }

        private string GetToken()
        {
            var csc = this.connectionDetail.ServiceClient as CrmServiceClient
                   ?? this.service as CrmServiceClient;

            var token = csc?.CurrentAccessToken;
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException(
                    "Could not acquire an OAuth access token from the current connection. " +
                    "Ensure you are connected to a Dataverse environment using modern authentication.");
            }

            return token;
        }
    }
}
