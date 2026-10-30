// <copyright file="IToolProvider.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp
{
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>The tools a host exposes; the HTTP layer knows nothing else about them.</summary>
    internal interface IToolProvider
    {
        /// <summary>Tool definitions, in a fixed order.</summary>
        JArray ListTools();

        bool HasTool(string name);

        Task<ToolResult> CallAsync(string name, JObject arguments, CancellationToken cancellationToken);
    }

    /// <summary>A tool outcome, shaped as the spec's result envelope (§6.2).</summary>
    internal sealed class ToolResult
    {
        private ToolResult(JObject payload, bool isError)
        {
            this.Payload = payload;
            this.IsError = isError;
        }

        public JObject Payload { get; }

        public bool IsError { get; }

        public static ToolResult Ok(JObject structured) => new ToolResult(structured, false);

        public static ToolResult Fail(string code, string message) =>
            new ToolResult(new JObject { ["error"] = new JObject { ["code"] = code, ["message"] = message } }, true);

        /// <summary>Builds the MCP tool result: structured content plus the same JSON as text.</summary>
        public JObject ToMcpResult()
        {
            var result = new JObject
            {
                ["resultType"] = "complete",
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = this.Payload.ToString(Formatting.None) }),
                ["isError"] = this.IsError,
            };

            if (!this.IsError)
            {
                result["structuredContent"] = this.Payload;
            }

            return result;
        }
    }
}
