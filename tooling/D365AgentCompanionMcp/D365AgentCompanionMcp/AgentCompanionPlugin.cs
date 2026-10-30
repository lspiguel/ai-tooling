// <copyright file="AgentCompanionPlugin.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp
{
    using System.ComponentModel.Composition;
    using XrmToolBox.Extensibility;
    using XrmToolBox.Extensibility.Interfaces;

    /// <summary>
    /// The XrmToolBox export. Every tab gets a new control; all of them share the one host in the process (H1).
    /// </summary>
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "Agent Companion MCP")]
    [ExportMetadata("Description", "A local MCP endpoint for coding agents over XrmToolBox connections. Every call is approved by a person in the plugin tab bound to its connection.")]
    [ExportMetadata("SmallImageBase64", null)]
    [ExportMetadata("BigImageBase64", null)]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public sealed class AgentCompanionPlugin : PluginBase
    {
        /// <inheritdoc/>
        public override IXrmToolBoxPluginControl GetControl() => new AgentCompanionControl();
    }
}
