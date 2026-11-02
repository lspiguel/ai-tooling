// <copyright file="PromptValues.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Setup
{
    using System.Collections.Generic;

    /// <summary>The values a setup prompt's placeholders are filled with, read when its button is pressed.</summary>
    internal sealed class PromptValues
    {
        /// <summary>Gets or sets this plugin's version.</summary>
        public string PluginVersion { get; set; } = string.Empty;

        /// <summary>Gets or sets the version setup was last completed for, on the update page.</summary>
        public string? PreviousVersion { get; set; }

        /// <summary>Gets or sets the MCP endpoint URL.</summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>Gets or sets the client configuration, holding the bearer token (<see cref="Hosting.ClientConfig"/>).</summary>
        public string ClientConfig { get; set; } = string.Empty;

        /// <summary>Gets or sets this tab's organization URL, or <c>null</c> when the tab is not bound.</summary>
        public string? OrganizationUrl { get; set; }

        /// <summary>Gets or sets the folder the skill zips were last exported to from this page, or <c>null</c>.</summary>
        public string? SkillsFolder { get; set; }

        /// <summary>Gets or sets the names of the embedded skills.</summary>
        public IReadOnlyList<string> SkillNames { get; set; } = new string[0];
    }
}
