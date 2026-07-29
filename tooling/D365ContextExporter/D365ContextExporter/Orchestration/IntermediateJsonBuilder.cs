// <copyright file="IntermediateJsonBuilder.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Orchestration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using Lspiguel.Xrm.D365ContextExporter.Models;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>Writes per-query raw files and assembles the combined <c>intermediate.json</c>.</summary>
    internal static class IntermediateJsonBuilder
    {
        /// <summary>
        /// Writes the raw result of a single query to <c>output.&lt;queryId&gt;.fetch.json</c> in the run directory.
        /// </summary>
        /// <param name="runDir">The timestamped run directory.</param>
        /// <param name="queryId">The query <c>id</c> (used in the filename).</param>
        /// <param name="result">Either a <see cref="JArray"/> or a <see cref="IEnumerable{T}"/> of dictionaries.</param>
        public static void WriteQueryResult(string runDir, string queryId, object result)
        {
            var path = Path.Combine(runDir, $"output.{queryId}.fetch.json");
            var token = result is JToken jt ? jt : JToken.FromObject(result);

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(stream, new UTF8Encoding(false));
            using var writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };
            token.WriteTo(writer);
        }

        /// <summary>
        /// Assembles all query results into <c>intermediate.json</c> and writes it to the run directory.
        /// </summary>
        /// <param name="runDir">The timestamped run directory.</param>
        /// <param name="job">The loaded export job (supplies spec name and front-matter).</param>
        /// <param name="environmentUrl">The Dataverse environment URL stored in <c>_meta</c>.</param>
        /// <param name="orgName">The organisation friendly name stored in <c>_meta</c>.</param>
        /// <param name="results">Map from each query's <c>resultKey</c> to its result object.</param>
        /// <returns>Absolute path to the written <c>intermediate.json</c>.</returns>
        public static string WriteIntermediate(
            string runDir,
            ExportJob job,
            string environmentUrl,
            string orgName,
            IReadOnlyDictionary<string, object> results)
        {
            var path = Path.Combine(runDir, "intermediate.json");

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(stream, new UTF8Encoding(false));
            using var writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };

            writer.WriteStartObject();

            // _meta
            writer.WritePropertyName("_meta");
            writer.WriteStartObject();
            writer.WritePropertyName("exportedAtUtc");
            writer.WriteValue(DateTime.UtcNow.ToString("o"));
            writer.WritePropertyName("environment");
            writer.WriteStartObject();
            writer.WritePropertyName("url");
            writer.WriteValue(environmentUrl);
            writer.WritePropertyName("orgName");
            writer.WriteValue(orgName);
            writer.WriteEndObject();
            writer.WritePropertyName("spec");
            writer.WriteValue(job.Spec);
            writer.WritePropertyName("frontMatter");
            JToken.FromObject(job.FrontMatter).WriteTo(writer);
            writer.WriteEndObject();

            // per-query results
            foreach (var kv in results)
            {
                writer.WritePropertyName(kv.Key);
                var token = kv.Value is JToken jt ? jt : JToken.FromObject(kv.Value);
                token.WriteTo(writer);
            }

            writer.WriteEndObject();
            return path;
        }
    }
}
