// <copyright file="McpReply.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The protocol layer's answer to one HTTP request. A reply is either complete now, or — for
    /// <c>tools/call</c> only — pending: its status is already known to be <c>200</c> and its JSON body
    /// follows when the call ends (SPEC §6.1, D9).
    /// </summary>
    internal sealed class McpReply
    {
        private McpReply(int status, JObject? body, string? plainText, Task<JObject>? pending, string label)
        {
            this.Status = status;
            this.Body = body;
            this.PlainText = plainText;
            this.Pending = pending;
            this.Label = label;
        }

        /// <summary>Gets the HTTP status.</summary>
        public int Status { get; }

        /// <summary>Gets the JSON body of a complete reply, or <c>null</c>.</summary>
        public JObject? Body { get; }

        /// <summary>Gets a plain-text body, used only for <c>401</c>.</summary>
        public string? PlainText { get; }

        /// <summary>Gets the body of a pending reply. The task never faults.</summary>
        public Task<JObject>? Pending { get; }

        /// <summary>Gets a short description for diagnostics.</summary>
        public string Label { get; }

        /// <summary>Gets the response headers to add.</summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        /// <summary>Creates a complete reply.</summary>
        /// <param name="status">The HTTP status.</param>
        /// <param name="body">The JSON body, or <c>null</c> for none.</param>
        /// <param name="label">A short description.</param>
        /// <returns>The reply.</returns>
        public static McpReply Complete(int status, JObject? body, string label) => new McpReply(status, body, null, null, label);

        /// <summary>Creates a complete reply with a plain-text body.</summary>
        /// <param name="status">The HTTP status.</param>
        /// <param name="text">The text body.</param>
        /// <param name="label">A short description.</param>
        /// <returns>The reply.</returns>
        public static McpReply Text(int status, string text, string label) => new McpReply(status, null, text, null, label);

        /// <summary>Creates a pending <c>200</c> reply.</summary>
        /// <param name="body">The task producing the JSON body.</param>
        /// <param name="label">A short description.</param>
        /// <returns>The reply.</returns>
        public static McpReply Deferred(Task<JObject> body, string label) => new McpReply(200, null, null, body, label);
    }
}
