// <copyright file="HostSettings.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System.Collections.Generic;

    /// <summary>
    /// The plugin settings XrmToolBox keeps between sessions (SPEC §7): host limits (H4), the bearer token
    /// encrypted with DPAPI (D8), and environment and always grants — organization IDs, friendly names and scopes
    /// only, never arguments or results. Public because XrmToolBox serializes it with <c>XmlSerializer</c>.
    /// </summary>
    public sealed class HostSettings
    {
        /// <summary>The default loopback port.</summary>
        public const int DefaultPort = 47321;

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
    }
}
