// <copyright file="ExportJobRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Orchestration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading;

    using D365ContextExporter.Helpers;
    using D365ContextExporter.Models;
    using D365ContextExporter.Queries;

    using McTools.Xrm.Connection;

    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Tooling.Connector;

    /// <summary>Orchestrates a single export run: executes all queries and writes the intermediate outputs.</summary>
    internal sealed class ExportJobRunner
    {
        private readonly IOrganizationService service;
        private readonly ConnectionDetail connectionDetail;
        private readonly Action<string> log;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportJobRunner"/> class.
        /// </summary>
        /// <param name="service">The connected Dataverse service.</param>
        /// <param name="connectionDetail">The XrmToolBox connection detail (supplies environment URL and org name).</param>
        /// <param name="log">Delegate called for each log line; must be thread-safe.</param>
        public ExportJobRunner(IOrganizationService service, ConnectionDetail connectionDetail, Action<string> log)
        {
            this.service = service;
            this.connectionDetail = connectionDetail;
            this.log = log;
        }

        /// <summary>Executes all queries defined in <paramref name="job"/> and writes the run outputs.</summary>
        /// <param name="job">The loaded project configuration.</param>
        /// <param name="baseDir">The project base directory.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        public void Run(ExportJob job, string baseDir, CancellationToken cancellationToken)
        {
            this.log($"[Export] Starting project '{job.Project}' ({job.Queries.Count} queries).");

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

            foreach (var query in job.Queries)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    this.log("[Export] Cancelled.");
                    return;
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
                }
                catch (OperationCanceledException)
                {
                    this.log("[Export] Cancelled.");
                    return;
                }
                catch (Exception ex)
                {
                    this.log($"[Export] ERROR in query '{query.Id}': {ex.Message}");
                    failures.Add(new Exception($"Query '{query.Id}' failed: {ex.Message}", ex));
                }
            }

            if (results.Count > 0)
            {
                var intermediatePath = IntermediateJsonBuilder.WriteIntermediate(
                    runDir, job, environmentUrl, orgName, results);
                this.log($"[Export] Intermediate JSON written: {intermediatePath}");
            }

            if (failures.Count > 0)
            {
                throw new AggregateException($"{failures.Count} of {job.Queries.Count} queries failed.", failures);
            }

            this.log($"[Export] Run complete. Outputs in: {runDir}");
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
