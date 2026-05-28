// <copyright file="PythonSettings.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Models
{
    using Newtonsoft.Json;

    /// <summary>Controls how the Python runtime is located and invoked.</summary>
    public sealed class PythonSettings
    {
        /// <summary>
        /// Gets or sets the interpreter resolution strategy.
        /// <c>"auto"</c> (default) searches PATH for <c>py.exe</c> then <c>python.exe</c>, skipping Windows Store stubs.
        /// Any other value is treated as an absolute path to <c>python.exe</c>.
        /// </summary>
        [JsonProperty("interpreter")]
        public string Interpreter { get; set; } = "auto";
    }
}
