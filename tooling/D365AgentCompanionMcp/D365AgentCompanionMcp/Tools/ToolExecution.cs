// <copyright file="ToolExecution.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Microsoft.Xrm.Sdk;
    using Newtonsoft.Json.Linq;

    /// <summary>What an approved tool call runs with.</summary>
    internal sealed class ToolExecution
    {
        /// <summary>Initializes a new instance of the <see cref="ToolExecution"/> class.</summary>
        /// <param name="host">The host.</param>
        /// <param name="binding">The connection the call names.</param>
        /// <param name="arguments">The validated arguments.</param>
        /// <param name="prepared">What the tool resolved before its dialog, if it prepares.</param>
        public ToolExecution(CompanionHost host, BoundConnection binding, JObject arguments, PreparedCall? prepared = null)
        {
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
    }
}
