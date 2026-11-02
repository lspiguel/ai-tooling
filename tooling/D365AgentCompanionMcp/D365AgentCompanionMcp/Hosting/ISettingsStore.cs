// <copyright file="ISettingsStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    /// <summary>Loads and saves <see cref="HostSettings"/>; replaced by a fake store in tests.</summary>
    internal interface ISettingsStore
    {
        /// <summary>Loads the settings.</summary>
        /// <returns>The settings, or <c>null</c> when none are saved or they cannot be read.</returns>
        HostSettings? Load();

        /// <summary>Saves the settings.</summary>
        /// <param name="settings">The settings.</param>
        void Save(HostSettings settings);
    }
}
