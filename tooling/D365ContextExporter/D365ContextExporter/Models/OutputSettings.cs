// <copyright file="OutputSettings.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Models
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    /// <summary>Controls how query results are serialised to the intermediate JSON file.</summary>
    public sealed class OutputSettings
    {
        /// <summary>
        /// Gets or sets attribute logical name fragments that are stripped before JSON serialisation.
        /// Any attribute whose name contains one of these substrings is omitted from the output.
        /// </summary>
        [JsonProperty("attributeDenyList")]
        public List<string> AttributeDenyList { get; set; } = new List<string>
        {
            "password",
            "secret",
            "token",
            "key",
        };
    }
}
