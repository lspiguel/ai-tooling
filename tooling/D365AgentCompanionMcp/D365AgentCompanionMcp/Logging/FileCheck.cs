// <copyright file="FileCheck.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    /// <summary>What <see cref="CallLogFile.Check"/> found in a call log file already on disk (SPEC 6a.2).</summary>
    internal enum FileCheck
    {
        /// <summary>In shape and for the same connection: the plugin appends to it.</summary>
        InShape,

        /// <summary>Not in shape — a trailer that is not <c>]}</c>, or a parse failure. Never modified.</summary>
        NotInShape,

        /// <summary>In shape, but written for another connection of the same name. Never modified.</summary>
        OtherConnection,
    }
}
