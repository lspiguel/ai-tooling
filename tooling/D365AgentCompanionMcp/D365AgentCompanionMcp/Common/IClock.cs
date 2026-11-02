// <copyright file="IClock.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Common
{
    using System;

    /// <summary>Source of the current time, replaced by a fake clock in tests.</summary>
    internal interface IClock
    {
        /// <summary>Gets the current time in UTC.</summary>
        DateTime UtcNow { get; }
    }
}
