// <copyright file="ExportJob.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Models
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Newtonsoft.Json;

    /// <summary>Represents a loaded spec configuration file.</summary>
    public sealed class ExportJob
    {
        /// <summary>Gets or sets the spec name.</summary>
        [JsonProperty("spec")]
        public string Spec { get; set; } = string.Empty;

        /// <summary>Gets or sets the schema version of this config file.</summary>
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>Gets or sets the Jinja2 template filename used to transform query results.</summary>
        [JsonProperty("transformation")]
        public string Transformation { get; set; } = string.Empty;

        /// <summary>Gets or sets the list of queries to execute.</summary>
        [JsonProperty("queries")]
        public List<QueryDefinition> Queries { get; set; } = new List<QueryDefinition>();

        /// <summary>Gets or sets the Python runtime settings.</summary>
        [JsonProperty("python")]
        public PythonSettings Python { get; set; } = new PythonSettings();

        /// <summary>Gets or sets key/value pairs injected as front-matter into the output document.</summary>
        [JsonProperty("frontMatter")]
        public Dictionary<string, string> FrontMatter { get; set; } = new Dictionary<string, string>();

        /// <summary>Gets or sets the output serialization settings (deny list, etc.).</summary>
        [JsonProperty("output")]
        public OutputSettings Output { get; set; } = new OutputSettings();

        /// <summary>Gets or sets the path to a LEGAL.md file (relative to the base directory) prepended to the output context file.</summary>
        [JsonProperty("legal")]
        public string Legal { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path to the config file from which this job was loaded.</summary>
        [JsonIgnore]
        public string ConfigFilePath { get; set; } = string.Empty;

        /// <summary>Loads and deserialises an export job from a JSON config file.</summary>
        /// <param name="configFilePath">Absolute path to the <c>*.context-exporter-config.json</c> file.</param>
        /// <returns>The deserialised <see cref="ExportJob"/> with <see cref="ConfigFilePath"/> set.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the file deserialises to null.</exception>
        public static ExportJob Load(string configFilePath)
        {
            var json = File.ReadAllText(configFilePath);
            var job = JsonConvert.DeserializeObject<ExportJob>(json)
                      ?? throw new InvalidOperationException($"Failed to deserialise {configFilePath}");
            job.ConfigFilePath = configFilePath;
            return job;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.Spec} v{this.Version} ({this.Queries.Count} queries)";
    }
}
