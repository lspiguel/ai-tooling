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
    [ExportMetadata("SmallImageBase64", "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAACRklEQVR4AaSSX0hTcRTHv/e33a35B51turm76ZZGTQMLJMMi6aGgjHyJqFY9ho6kHkLIh+wl6CEyYvXgQxLlSwzKBdGDCEJ/tigCY8SyNtkYm4UJEeRt9/66Z2PDYqzAczmc3+98z/n8zr2/y7BO+2/AQOud1n7pVt/f5/0TUGgMXM4pahwCmz3kDNylXBFUEdAvFRpFZhhtN3jh1rthFMSTao5HSCNIWUDpBAFjZl0jOg3bxRbJjM0uG7bp20SnzmqFplEdwxqjBI1I4x523Z4gycIs2OgQMXDNjVP3vWh21KBBqCIp7yUAjUSNFnuN7/z1fTBbTQeognEFfefsqHcYUScZ0TvsBOMqipYHHN06Ea5vrBo5caEbXbsl/dSN1/B4rXoqYlDxdOQj3gczeHkzgZmLUcjaQxp5HvBLzkkHfR2m7t4OhINpmOQNWJjLgjHhy6dc4mcql0Z4PI7I+GfE1SXMqxlojdlHicGEFolT8GeBr2jTvrbzWzN2wmX3dW2pbvJUzy1qTS+UmPJGXVxd4MsAx9h00m+jrj8AmgDOSSeJo9ZgrNpzpH2/KLLnMlemVvhqWK9j7icp/xWqIC8Bsqnv0K4Gnp5atPTUQdrVAKNZxPyrNLgAdyg1dDqUHNxLY2ONMVorsjo68/ADItFZ2bRjBccfdGLTsA2Tb6Ny7F12iXSqK+d5QCjpn6TRljM/rtINaP8ALh17DNoHY2ebSC/XTLk8gBY0Gr0bgZhOuEeR9qRV8hKgWESg6cTQGYrFXKX4GwAA//812w9VAAAABklEQVQDAIwk2CErIdOYAAAAAElFTkSuQmCC")]
    [ExportMetadata("BigImageBase64", "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")]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public sealed class AgentCompanionPlugin : PluginBase
    {
        /// <inheritdoc/>
        public override IXrmToolBoxPluginControl GetControl() => new AgentCompanionControl();
    }
}
