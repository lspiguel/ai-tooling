// <copyright file="ClientConfig.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;

    /// <summary>
    /// The client configuration <b>Copy client config</b> puts on the clipboard (SPEC §8.1), and the MCP setup and
    /// update prompts hold (§8.7): the endpoint, the bearer token, and a registration for each tested client.
    /// </summary>
    internal static class ClientConfig
    {
        /// <summary>Composes the configuration text.</summary>
        /// <param name="endpoint">The MCP endpoint URL.</param>
        /// <param name="token">The bearer token, or <c>null</c> when none is loaded.</param>
        /// <returns>The text, holding the token.</returns>
        public static string Compose(string endpoint, string? token)
        {
            var bearer = token ?? "<token>";
            return $"Endpoint: {endpoint}{Environment.NewLine}"
                + $"Bearer token: {bearer}{Environment.NewLine}{Environment.NewLine}"
                + $"Claude Code:{Environment.NewLine}"
                + $"claude mcp add --transport http d365-agent-companion {endpoint} --header \"Authorization: Bearer {bearer}\"{Environment.NewLine}{Environment.NewLine}"
                + $"VS Code (mcp.json) — paste the token when VS Code asks for it:{Environment.NewLine}"
                + "{ \"inputs\": [{ \"type\": \"promptString\", \"id\": \"d365-agent-companion-token\", \"description\": \"Agent Companion MCP bearer token\", \"password\": true }],"
                + $" \"servers\": {{ \"d365-agent-companion\": {{ \"type\": \"http\", \"url\": \"{endpoint}\", \"headers\": {{ \"Authorization\": \"Bearer ${{input:d365-agent-companion-token}}\" }} }} }} }}";
        }
    }
}
