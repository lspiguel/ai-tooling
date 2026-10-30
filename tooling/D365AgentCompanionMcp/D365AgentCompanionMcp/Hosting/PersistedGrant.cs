// <copyright file="PersistedGrant.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;

    /// <summary>An environment or always grant as stored in the plugin settings.</summary>
    public sealed class PersistedGrant
    {
        /// <summary>Gets or sets the kind, as the enum name.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Gets or sets the scope, as the enum name.</summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>Gets or sets the organization ID; empty for an always grant.</summary>
        public Guid OrganizationId { get; set; }

        /// <summary>Gets or sets the organization's friendly name.</summary>
        public string OrganizationName { get; set; } = string.Empty;

        /// <summary>Gets or sets when it was granted, UTC.</summary>
        public DateTime GrantedOnUtc { get; set; }

        /// <summary>Gets or sets where it was granted from.</summary>
        public string OriginTab { get; set; } = string.Empty;
    }
}
