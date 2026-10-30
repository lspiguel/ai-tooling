// <copyright file="McpConstants.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp
{
    /// <summary>Protocol constants for MCP revision 2026-07-28 (spike S1).</summary>
    internal static class McpConstants
    {
        public const string ProtocolVersion = "2026-07-28";

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
