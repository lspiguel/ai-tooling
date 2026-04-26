using Newtonsoft.Json;

namespace D365ContextExporter.Models
{
    /// <summary>Controls how the Python runtime is located and invoked.</summary>
    public sealed class PythonSettings
    {
        /// <summary>
        /// Gets or sets the interpreter resolution strategy.
        /// <c>"auto"</c> (default) uses discovery order: explicit path → venv → py launcher → PATH.
        /// Any other value is treated as an absolute path to <c>python.exe</c>.
        /// </summary>
        [JsonProperty("interpreter")]
        public string Interpreter { get; set; } = "auto";

        /// <summary>
        /// Gets or sets the path to the virtual environment directory.
        /// Supports <c>%LOCALAPPDATA%</c> and similar environment variable tokens.
        /// </summary>
        [JsonProperty("venv")]
        public string Venv { get; set; } = @"%LOCALAPPDATA%\D365ContextExporter\venv";
    }
}
