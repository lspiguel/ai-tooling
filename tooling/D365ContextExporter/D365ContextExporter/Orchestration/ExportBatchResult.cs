// <copyright file="ExportBatchResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Orchestration
{
    using System.Collections.Generic;

    /// <summary>The outcome of an <see cref="ExportBatchRunner"/> run.</summary>
    internal sealed class ExportBatchResult
    {
        /// <summary>Gets the names of the specs that ran to completion, in order.</summary>
        public List<string> Succeeded { get; } = new List<string>();

        /// <summary>Gets the specs that failed, as (spec name, reason), in order.</summary>
        public List<KeyValuePair<string, string>> Failed { get; } = new List<KeyValuePair<string, string>>();
    }
}
