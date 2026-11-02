// <copyright file="SetupPrompt.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Setup
{
    /// <summary>The prompts the setup pages copy (SPEC §8.7), each an embedded text resource.</summary>
    internal enum SetupPrompt
    {
        /// <summary>Page 2 (b): check, install or update PAC CLI.</summary>
        PacCli,

        /// <summary>Page 2 (c): a working <c>pac auth</c> profile per environment.</summary>
        PacAuth,

        /// <summary>Page 2 (d): unpack the exported skill zips.</summary>
        SkillsInstall,

        /// <summary>Page 2 (e): register the endpoint. Holds the bearer token.</summary>
        McpRegister,

        /// <summary>Page 3 and the update page: the setup test.</summary>
        Test,

        /// <summary>Update page: replace the installed agent-companion skills.</summary>
        SkillsUpdate,

        /// <summary>Update page: check the endpoint registration. Holds the bearer token.</summary>
        McpUpdate,
    }
}
