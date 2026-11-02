// <copyright file="McpConstants.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    /// <summary>
    /// Protocol constants. Revision 2026-07-28 is primary; the handshake revisions 2025-11-25 and 2025-06-18
    /// are served through <c>initialize</c> as a fallback for older clients (SPEC §6.1, D7).
    /// </summary>
    internal static class McpConstants
    {
        /// <summary>The primary, stateless protocol revision.</summary>
        public const string ProtocolVersion = "2026-07-28";

        /// <summary>The name reported in server info.</summary>
        public const string ServerName = "d365-agent-companion-mcp";

        /// <summary>The short instructions returned by <c>server/discover</c> and <c>initialize</c>.</summary>
        public const string Instructions =
            "Agent Companion MCP: Dataverse discovery and solution shaping through XrmToolBox connections. "
            + "Call list_connections first and pass a connectionId as `connection` to every other tool. "
            + "A person approves every call in XrmToolBox; a denial is final unless the person says otherwise. "
            + "Large results are paged: pass nextCursor back as `cursor`.";

        /// <summary>The handshake-era session header.</summary>
        public const string HeaderSessionId = "Mcp-Session-Id";

        /// <summary>The protocol version header.</summary>
        public const string HeaderProtocolVersion = "MCP-Protocol-Version";

        /// <summary>The 2026-07-28 method header.</summary>
        public const string HeaderMethod = "Mcp-Method";

        /// <summary>The 2026-07-28 tool-name header.</summary>
        public const string HeaderName = "Mcp-Name";

        /// <summary>The <c>_meta</c> key carrying the protocol version.</summary>
        public const string MetaProtocolVersion = "io.modelcontextprotocol/protocolVersion";

        /// <summary>The <c>_meta</c> key carrying server info.</summary>
        public const string MetaServerInfo = "io.modelcontextprotocol/serverInfo";

        /// <summary>The 2026-07-28 discovery method.</summary>
        public const string MethodDiscover = "server/discover";

        /// <summary>Lists the tools.</summary>
        public const string MethodToolsList = "tools/list";

        /// <summary>Calls a tool.</summary>
        public const string MethodToolsCall = "tools/call";

        /// <summary>Opens a handshake-era session.</summary>
        public const string MethodInitialize = "initialize";

        /// <summary>Handshake-era liveness check.</summary>
        public const string MethodPing = "ping";

        /// <summary>Handshake-era cancellation of a pending request.</summary>
        public const string MethodCancelled = "notifications/cancelled";

        /// <summary>JSON-RPC: the body is not JSON.</summary>
        public const int ParseError = -32700;

        /// <summary>JSON-RPC: not a valid request.</summary>
        public const int InvalidRequest = -32600;

        /// <summary>JSON-RPC: unknown method.</summary>
        public const int MethodNotFound = -32601;

        /// <summary>JSON-RPC: invalid parameters, including an unknown tool.</summary>
        public const int InvalidParams = -32602;

        /// <summary>JSON-RPC: internal error.</summary>
        public const int InternalError = -32603;

        /// <summary>MCP: unknown or expired session.</summary>
        public const int SessionNotFound = -32001;

        /// <summary>MCP 2026-07-28: a header does not match the body.</summary>
        public const int HeaderMismatch = -32020;

        /// <summary>MCP 2026-07-28: unsupported protocol version.</summary>
        public const int UnsupportedProtocolVersion = -32022;

        /// <summary>Time-to-live for cached discovery and tool lists, in milliseconds.</summary>
        public const int ListTtlMs = 300000;

        /// <summary>The plain-text body of every <c>401</c> (SPEC §6.2, D8).</summary>
        public const string UnauthorizedBody = "Agent Companion MCP: bearer token missing or not current. Copy the token from the plugin panel and update the client.";

        /// <summary>Gets the handshake-era revisions with structured tool output, preferred first.</summary>
        public static string[] LegacyVersions { get; } = { "2025-11-25", "2025-06-18" };
    }
}
