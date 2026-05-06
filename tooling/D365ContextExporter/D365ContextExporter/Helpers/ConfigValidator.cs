// <copyright file="ConfigValidator.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using D365ContextExporter.Models;

    /// <summary>Thrown when an <see cref="ExportJob"/> fails validation.</summary>
    internal sealed class ConfigValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigValidationException"/> class.
        /// </summary>
        public ConfigValidationException(List<string> violations)
            : base(string.Join(Environment.NewLine, violations))
        {
            this.Violations = violations;
        }

        /// <summary>Gets the list of individual validation violations.</summary>
        public List<string> Violations { get; }
    }

    /// <summary>Validates a loaded <see cref="ExportJob"/> against the file system and config constraints.</summary>
    internal static class ConfigValidator
    {
        /// <summary>Validates <paramref name="job"/> and throws <see cref="ConfigValidationException"/> listing all violations.</summary>
        /// <param name="job">The job to validate.</param>
        /// <param name="baseDir">The base directory used to resolve file paths.</param>
        /// <exception cref="ConfigValidationException">Thrown when one or more violations are found.</exception>
        public static void Validate(ExportJob job, string baseDir)
        {
            var violations = new List<string>();

            if (string.IsNullOrWhiteSpace(job.Spec))
            {
                violations.Add("Config error: 'spec' is required and must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(job.Transformation))
            {
                violations.Add("Config error: 'transformation' is required and must not be empty.");
            }
            else
            {
                var transformPath = Path.Combine(baseDir, "config", "transformations", job.Transformation);
                if (!File.Exists(transformPath))
                {
                    violations.Add($"Config error: transformation file not found: {transformPath}");
                }
            }

            if (!string.IsNullOrEmpty(job.Legal))
            {
                var legalPath = PathResolver.Resolve(job.Legal, baseDir);
                if (!File.Exists(legalPath))
                {
                    violations.Add($"Config error: legal file not found: {legalPath}");
                }
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenResultKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var query in job.Queries)
            {
                if (string.IsNullOrWhiteSpace(query.Id))
                {
                    violations.Add("Config error: a query is missing the required 'id' field.");
                    continue;
                }

                if (!seenIds.Add(query.Id))
                {
                    violations.Add($"Config error: duplicate query id '{query.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(query.ResultKey))
                {
                    violations.Add($"Config error: query '{query.Id}' is missing the required 'resultKey' field.");
                }
                else if (seenResultKeys.TryGetValue(query.ResultKey, out var conflictId))
                {
                    violations.Add($"Config error: queries '{conflictId}' and '{query.Id}' share the same resultKey '{query.ResultKey}'.");
                }
                else
                {
                    seenResultKeys[query.ResultKey] = query.Id;
                }

                if (string.Equals(query.Type, "fetchxml", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(query.Source))
                    {
                        violations.Add($"Config error: query '{query.Id}' (fetchxml) is missing the required 'source' field.");
                    }
                    else
                    {
                        var fetchPath = Path.Combine(baseDir, "config", "queries", query.Source);
                        if (!File.Exists(fetchPath))
                        {
                            violations.Add($"Config error: FetchXML file not found for query '{query.Id}': {fetchPath}");
                        }
                    }
                }
            }

            if (violations.Count > 0)
            {
                throw new ConfigValidationException(violations);
            }
        }
    }
}
