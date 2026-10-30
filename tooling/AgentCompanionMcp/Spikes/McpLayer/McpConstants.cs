// <copyright file="McpConstants.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp
{
    /// <summary>
    /// Protocol constants. Revision 2026-07-28 is primary; the handshake revisions 2025-11-25 and 2025-06-18
    /// are served through <c>initialize</c> as a fallback for older clients (decision D7).
    /// </summary>
    internal static class McpConstants
    {
        public const string ProtocolVersion = "2026-07-28";

        /// <summary>Handshake-era revisions with structured tool output, preferred first.</summary>
        public static readonly string[] LegacyVersions = { "2025-11-25", "2025-06-18" };

        public const string HeaderSessionId = "Mcp-Session-Id";
        public const string MethodPing = "ping";
        public const int SessionNotFound = -32001;

        public const string MetaProtocolVersion = "io.modelcontextprotocol/protocolVersion";
        public const string MetaServerInfo = "io.modelcontextprotocol/serverInfo";

        public const string HeaderProtocolVersion = "MCP-Protocol-Version";
        public const string HeaderMethod = "Mcp-Method";
        public const string HeaderName = "Mcp-Name";

        public const string MethodDiscover = "server/discover";
        public const string MethodToolsList = "tools/list";
        public const string MethodToolsCall = "tools/call";
        public const string MethodInitialize = "initialize";

        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int HeaderMismatch = -32020;
        public const int UnsupportedProtocolVersion = -32022;

        public const string ServerName = "agent-companion-mcp-spike";

        public const int ListTtlMs = 300000;
    }
}
