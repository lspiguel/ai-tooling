using System.Collections.Generic;

using Newtonsoft.Json;

namespace D365ContextExporter.Models
{
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
