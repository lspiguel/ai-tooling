// <copyright file="ToolCatalog.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System.Collections.Generic;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;

    /// <summary>
    /// The tools <c>tools/list</c> returns, in the fixed order of SPEC §6.3. Phase 1 lists the tools it implements;
    /// later phases insert theirs at their place in that order.
    /// </summary>
    internal static class ToolCatalog
    {
        /// <summary>Creates the tool list.</summary>
        /// <param name="host">The host.</param>
        /// <returns>The tools, in order.</returns>
        public static IReadOnlyList<ToolDefinition> Create(CompanionHost host) => new[]
        {
            HostTools.ListConnectionsTool(host),
            ConnectionTools.GetConnectionInfoTool(),
            ConnectionTools.WhoAmITool(),
            PublisherTools.ListPublishersTool(),
        };
    }
}
