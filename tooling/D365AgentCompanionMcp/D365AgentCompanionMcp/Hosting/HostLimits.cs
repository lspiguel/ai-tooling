// <copyright file="HostLimits.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Collections.Generic;

    /// <summary>The ranges the host-level settings may take (H4).</summary>
    internal static class HostLimits
    {
        /// <summary>The lowest port accepted.</summary>
        public const int MinPort = 1024;

        /// <summary>The highest port accepted.</summary>
        public const int MaxPort = 65535;

        /// <summary>The smallest maximum page size.</summary>
        public const int MinPageSize = 1;

        /// <summary>The largest maximum page size.</summary>
        public const int MaxPageSize = 5000;

        /// <summary>The smallest response cap, in characters.</summary>
        public const int MinResponseCap = 1000;

        /// <summary>Claude Code accepts up to 500,000 characters when a tool declares it; the default stays well under its 50,000 threshold.</summary>
        public const int MaxResponseCap = 500000;

        /// <summary>The shortest approval timeout stored. The panel offers 10 seconds at least; one second is accepted so that tests run quickly.</summary>
        public const int MinApprovalTimeout = 1;

        /// <summary>The shortest approval timeout the panel offers.</summary>
        public const int MinPanelApprovalTimeout = 10;

        /// <summary>The longest approval timeout, in seconds.</summary>
        public const int MaxApprovalTimeout = 600;

        /// <summary>Brings every value into its range.</summary>
        /// <param name="settings">The settings, changed in place.</param>
        /// <returns>The same settings.</returns>
        public static HostSettings Normalize(HostSettings settings)
        {
            settings.Port = Clamp(settings.Port, MinPort, MaxPort, HostSettings.DefaultPort);
            settings.MaxPageSize = Clamp(settings.MaxPageSize, MinPageSize, MaxPageSize, 500);
            settings.ResponseCapCharacters = Clamp(settings.ResponseCapCharacters, MinResponseCap, MaxResponseCap, 40000);
            settings.ApprovalTimeoutSeconds = Clamp(settings.ApprovalTimeoutSeconds, MinApprovalTimeout, MaxApprovalTimeout, 120);
            settings.Grants ??= new List<PersistedGrant>();
            return settings;
        }

        private static int Clamp(int value, int min, int max, int fallback) => value == 0 ? fallback : Math.Min(max, Math.Max(min, value));
    }
}
