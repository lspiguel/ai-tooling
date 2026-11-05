// <copyright file="ToolExecution.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Microsoft.Xrm.Sdk;
    using Newtonsoft.Json.Linq;

    /// <summary>What an approved tool call runs with.</summary>
    internal sealed class ToolExecution
    {
        private readonly Func<IReadOnlyList<string>, FileRoot>? fileRoot;

        /// <summary>Initializes a new instance of the <see cref="ToolExecution"/> class.</summary>
        /// <param name="host">The host.</param>
        /// <param name="binding">The connection the call names.</param>
        /// <param name="arguments">The validated arguments.</param>
        /// <param name="prepared">What the tool resolved before its dialog, if it prepares.</param>
        /// <param name="fileRoot">Finds the File root for the call, asking the person when none is set (SPEC §6.8); <c>null</c> in tests of tools that take no files.</param>
        public ToolExecution(CompanionHost host, BoundConnection binding, JObject arguments, PreparedCall? prepared = null, Func<IReadOnlyList<string>, FileRoot>? fileRoot = null)
        {
            this.fileRoot = fileRoot;
            this.Host = host;
            this.Binding = binding;
            this.Arguments = arguments;
            this.Prepared = prepared;
        }

        /// <summary>Gets what the tool resolved before its dialog, or <c>null</c>.</summary>
        public PreparedCall? Prepared { get; }

        /// <summary>Gets the host.</summary>
        public CompanionHost Host { get; }

        /// <summary>Gets the connection.</summary>
        public BoundConnection Binding { get; }

        /// <summary>Gets the arguments.</summary>
        public JObject Arguments { get; }

        /// <summary>Gets the connection's service.</summary>
        public IOrganizationService Service => this.Binding.Service!;

        /// <summary>
        /// Finds the File root that applies to the connection. When none is set the person is asked first, before any
        /// approval dialog; cancelling fails the call with <c>denied_by_user</c> and no answer with <c>denied_no_response</c>.
        /// </summary>
        /// <param name="paths">The paths in the call, which the folder picker starts from.</param>
        /// <returns>The File root.</returns>
        public FileRoot RequireFileRoot(IReadOnlyList<string> paths) =>
            this.fileRoot != null
                ? this.fileRoot(paths)
                : this.Host.FileRoots.Find(this.Binding.Identity)
                    ?? throw new ToolException(ToolErrorCodes.DeniedByUser, "No File root is set and the person cannot be asked here.");
    }
}
