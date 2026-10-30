// <copyright file="XrmToolBoxSettingsStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Diagnostics;
    using XrmToolBox.Extensibility;

    /// <summary>Keeps <see cref="HostSettings"/> in XrmToolBox's plugin settings folder.</summary>
    internal sealed class XrmToolBoxSettingsStore : ISettingsStore
    {
        /// <inheritdoc/>
        public HostSettings? Load() =>
            SettingsManager.Instance.TryLoad(typeof(AgentCompanionPlugin), out HostSettings settings) ? settings : null;

        /// <inheritdoc/>
        public void Save(HostSettings settings)
        {
            try
            {
                SettingsManager.Instance.Save(typeof(AgentCompanionPlugin), settings);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Agent Companion MCP: settings not saved: {ex.Message}");
            }
        }
    }
}
