// <copyright file="IToolHost.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    /// <summary>The tools behind the endpoint; the protocol layer knows nothing else about them.</summary>
    internal interface IToolHost
    {
        /// <summary>Lists the tool definitions, in a fixed order.</summary>
        /// <returns>The <c>tools</c> array of <c>tools/list</c>.</returns>
        JArray ListTools();

        /// <summary>Tells whether a tool exists.</summary>
        /// <param name="name">The tool name.</param>
        /// <returns><c>true</c> when the tool exists.</returns>
        bool HasTool(string name);

        /// <summary>Runs one tool call. Tool failures are results, not exceptions.</summary>
        /// <param name="call">The call.</param>
        /// <param name="cancellation">Signalled when the client closes or cancels the request.</param>
        /// <returns>The tool result.</returns>
        Task<ToolResult> CallAsync(McpToolCall call, CancellationToken cancellation);
    }
}
