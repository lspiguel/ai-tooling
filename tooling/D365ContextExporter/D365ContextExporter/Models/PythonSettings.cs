// <copyright file="PythonSettings.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Models
{
    using Newtonsoft.Json;

    /// <summary>
    /// The former Python runtime settings. Ignored: templates are rendered in-process with Scriban.
    /// Kept so config files written before the move to Scriban still deserialize.
    /// </summary>
    public sealed class PythonSettings
    {
        /// <summary>Gets or sets the former interpreter setting. Ignored.</summary>
        [JsonProperty("interpreter")]
        public string Interpreter { get; set; } = "auto";
    }
}
