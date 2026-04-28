// <copyright file="QueryDefinition.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Models
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    /// <summary>Describes a single query to execute against Dataverse.</summary>
    public sealed class QueryDefinition
    {
        /// <summary>Gets or sets the unique identifier for this query within the job.</summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the query type. One of: <c>fetchxml</c>, <c>webapi</c>, <c>metadata</c>.</summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the <c>.fetch.xml</c> file for fetchxml queries.
        /// Relative to <c>config/queries/</c>.
        /// </summary>
        [JsonProperty("source")]
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the OData resource path for webapi queries
        /// (e.g. <c>GlobalOptionSetDefinitions</c>).
        /// </summary>
        [JsonProperty("path")]
        public string? Path { get; set; }

        /// <summary>Gets or sets the key under which this query's results are stored in the intermediate JSON.</summary>
        [JsonProperty("resultKey")]
        public string ResultKey { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional row cap for large result sets.</summary>
        [JsonProperty("maxRecords")]
        public int? MaxRecords { get; set; }

        /// <summary>
        /// Gets or sets the list of OData field names for a <c>$select</c> clause on webapi queries.
        /// When set, appended to <see cref="Path"/> as <c>$select=field1,field2</c>.
        /// </summary>
        [JsonProperty("select")]
        public List<string>? Select { get; set; }

        /// <summary>
        /// Gets or sets the metadata dispatch key for <c>metadata</c> queries.
        /// One of: <c>entities</c>, <c>attributes</c>, <c>optionsets</c>, <c>relationships</c>.
        /// </summary>
        [JsonProperty("metadataTarget")]
        public string? MetadataTarget { get; set; }
    }
}
