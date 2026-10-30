// <copyright file="McpToolCall.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System;
    using Newtonsoft.Json.Linq;

    /// <summary>One <c>tools/call</c> request, as handed to the tool host.</summary>
    internal sealed class McpToolCall
    {
        /// <summary>Initializes a new instance of the <see cref="McpToolCall"/> class.</summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">The arguments; <c>null</c> when the client sent something other than an object.</param>
        /// <param name="receivedUtc">When the host received the call.</param>
        public McpToolCall(string name, JObject? arguments, DateTime receivedUtc)
        {
            this.Name = name;
            this.Arguments = arguments;
            this.ReceivedUtc = receivedUtc;
        }

        /// <summary>Gets the tool name.</summary>
        public string Name { get; }

        /// <summary>Gets the arguments, or <c>null</c> when they were not a JSON object.</summary>
        public JObject? Arguments { get; }

        /// <summary>Gets when the host received the call.</summary>
        public DateTime ReceivedUtc { get; }
    }
}
