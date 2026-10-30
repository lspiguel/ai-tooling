// <copyright file="ToolResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>A tool outcome, shaped as the result envelope of SPEC §6.2.</summary>
    internal sealed class ToolResult
    {
        private ToolResult(JObject payload, bool isError)
        {
            this.Payload = payload;
            this.IsError = isError;
            this.Text = payload.ToString(Formatting.None);
        }

        /// <summary>Gets the structured result, or the <c>ToolError</c> object.</summary>
        public JObject Payload { get; }

        /// <summary>Gets a value indicating whether the call failed.</summary>
        public bool IsError { get; }

        /// <summary>Gets the text content block — the copy the response cap is measured on (D6).</summary>
        public string Text { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="structured">The result object matching the tool's output schema.</param>
        /// <returns>The result.</returns>
        public static ToolResult Ok(JObject structured) => new ToolResult(structured, false);

        /// <summary>Creates a tool error.</summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The plain-text message.</param>
        /// <param name="details">Optional details.</param>
        /// <returns>The result.</returns>
        public static ToolResult Fail(string code, string message, JObject? details = null)
        {
            var error = new JObject { ["code"] = code, ["message"] = message };
            if (details != null)
            {
                error["details"] = details;
            }

            return new ToolResult(new JObject { ["error"] = error }, true);
        }

        /// <summary>Builds the MCP tool result: structured content plus the same JSON as one text block.</summary>
        /// <param name="modern">Whether the call came under revision 2026-07-28, which adds <c>resultType</c>.</param>
        /// <returns>The <c>result</c> object.</returns>
        public JObject ToMcpResult(bool modern)
        {
            var result = new JObject();
            if (modern)
            {
                result["resultType"] = "complete";
            }

            result["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = this.Text });
            if (!this.IsError)
            {
                result["structuredContent"] = this.Payload;
            }

            result["isError"] = this.IsError;
            return result;
        }
    }
}
