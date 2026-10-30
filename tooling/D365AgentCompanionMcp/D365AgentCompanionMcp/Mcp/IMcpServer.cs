// <copyright file="IMcpServer.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System;

    /// <summary>The listening endpoint, replaced by a fake listener in host tests.</summary>
    internal interface IMcpServer : IDisposable
    {
        /// <summary>Gets the endpoint URL clients use.</summary>
        string Endpoint { get; }

        /// <summary>Starts listening. Throws when the port cannot be bound — for example, when another process holds it (H2).</summary>
        void Start();

        /// <summary>Lets replies already decided finish writing, then stops listening.</summary>
        void Stop();
    }
}
