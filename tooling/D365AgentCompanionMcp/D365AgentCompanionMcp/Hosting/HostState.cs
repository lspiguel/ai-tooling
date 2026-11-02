// <copyright file="HostState.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    /// <summary>Whether the host is listening.</summary>
    internal enum HostState
    {
        /// <summary>Not listening.</summary>
        Stopped,

        /// <summary>Listening.</summary>
        Running,

        /// <summary>Could not bind its port (H2).</summary>
        Failed,
    }
}
