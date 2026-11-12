// <copyright file="HostSettings.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System.Collections.Generic;

    /// <summary>
    /// The plugin settings XrmToolBox keeps between sessions (SPEC §7): host limits (H4), the bearer token
    /// encrypted with DPAPI (D8), environment, tenant and global grants, and File roots — IDs, friendly names, scopes
    /// and folders only, never arguments or results. Public because XrmToolBox serializes it with <c>XmlSerializer</c>.
    /// </summary>
    public sealed class HostSettings
    {
        /// <summary>The default loopback port.</summary>
        public const int DefaultPort = 47321;

        /// <summary>The schema version Phase 6 writes. A file without a version is version 0 (v1).</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Gets or sets the schema version of the file; 0 for a file written before Phase 6.</summary>
        public int SchemaVersion { get; set; }

        /// <summary>Gets or sets the loopback port.</summary>
        public int Port { get; set; } = DefaultPort;

        /// <summary>Gets or sets the most items a page may hold.</summary>
        public int MaxPageSize { get; set; } = 500;

        /// <summary>Gets or sets the response cap, in characters of the text content block (D2, D6).</summary>
        public int ResponseCapCharacters { get; set; } = 40000;

        /// <summary>Gets or sets the approval timeout, in seconds.</summary>
        public int ApprovalTimeoutSeconds { get; set; } = 120;

        /// <summary>Gets or sets the bearer token, DPAPI-protected for the current Windows user, Base64.</summary>
        public string? ProtectedToken { get; set; }

        /// <summary>Gets or sets the persisted grants.</summary>
        public List<PersistedGrant> Grants { get; set; } = new List<PersistedGrant>();

        /// <summary>Gets or sets the File roots (SPEC §6.8).</summary>
        public List<PersistedFileRoot> FileRoots { get; set; } = new List<PersistedFileRoot>();

        /// <summary>
        /// Gets or sets a value indicating whether the person turned on <b>Write call log to disk</b> (SPEC 6a.2). Off by
        /// default; only the person changes it, in the Active Approvals window, and no tool reports or changes it.
        /// </summary>
        public bool CallLogToDisk { get; set; }

        /// <summary>Gets or sets the folder the call log files are written to, or <c>null</c> when none was chosen (SPEC 6a.2).</summary>
        public string? CallLogFolder { get; set; }

        /// <summary>
        /// Gets or sets the plugin version the setup wizard was last completed for (SPEC §8.7), or <c>null</c> before
        /// the first run. Nothing else about setup is kept.
        /// </summary>
        public string? SetupCompletedVersion { get; set; }
    }
}
