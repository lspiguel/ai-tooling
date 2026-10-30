// <copyright file="HostSpikeTool.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System.ComponentModel.Composition;
    using XrmToolBox.Extensibility;
    using XrmToolBox.Extensibility.Interfaces;

    /// <summary>MEF export for the spike. Each tab gets a new control; the host behind them is shared.</summary>
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "Agent Companion MCP (spike)")]
    [ExportMetadata("Description", "Throwaway spike for the Agent Companion MCP: singleton MCP host and one tab per connection.")]
    [ExportMetadata("SmallImageBase64", null)]
    [ExportMetadata("BigImageBase64", null)]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public sealed class HostSpikeTool : PluginBase
    {
        /// <inheritdoc/>
        public override IXrmToolBoxPluginControl GetControl() => new HostSpikeControl();
    }
}
