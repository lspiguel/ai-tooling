// <copyright file="SystemClock.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Common
{
    using System;

    /// <summary>The machine clock.</summary>
    internal sealed class SystemClock : IClock
    {
        /// <summary>The shared instance.</summary>
        public static readonly SystemClock Instance = new SystemClock();

        private SystemClock()
        {
        }

        /// <inheritdoc/>
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
