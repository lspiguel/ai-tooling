// <copyright file="SetupGate.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Setup
{
    using System;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;

    /// <summary>
    /// Decides between the wizard, the <b>Plugin updated</b> page and the main panel from the one version the
    /// settings hold, and records that version when setup ends (SPEC §8.7).
    /// </summary>
    internal static class SetupGate
    {
        /// <summary>Decides what a tab shows first.</summary>
        /// <param name="completedVersion">The version setup was last completed for, or <c>null</c>.</param>
        /// <param name="currentVersion">This plugin's version.</param>
        /// <returns>The view; any difference, a downgrade included, counts as an update.</returns>
        public static SetupView Required(string? completedVersion, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(completedVersion))
            {
                return SetupView.FirstRun;
            }

            return string.Equals(completedVersion!.Trim(), currentVersion, StringComparison.Ordinal) ? SetupView.None : SetupView.Updated;
        }

        /// <summary>Decides what a tab shows first, from the settings.</summary>
        /// <param name="settings">The host settings.</param>
        /// <param name="currentVersion">This plugin's version.</param>
        /// <returns>The view.</returns>
        public static SetupView RequiredSetup(this HostSettingsStore settings, string currentVersion) =>
            Required(settings.CompletedSetupVersion(), currentVersion);

        /// <summary>Reads the version setup was last completed for.</summary>
        /// <param name="settings">The host settings.</param>
        /// <returns>The version, or <c>null</c> before the first run.</returns>
        public static string? CompletedSetupVersion(this HostSettingsStore settings) => settings.Read(s => s.SetupCompletedVersion);

        /// <summary>Records that setup was completed for a version — <b>Finish</b>, <b>Done</b> or <b>Skip</b>.</summary>
        /// <param name="settings">The host settings.</param>
        /// <param name="currentVersion">This plugin's version.</param>
        public static void CompleteSetup(this HostSettingsStore settings, string currentVersion) =>
            settings.Update(s => s.SetupCompletedVersion = currentVersion);
    }
}
