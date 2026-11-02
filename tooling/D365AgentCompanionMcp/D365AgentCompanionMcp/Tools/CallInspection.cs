// <copyright file="CallInspection.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    /// <summary>What a tool's check before the approval dialog found (SPEC §8.2).</summary>
    internal sealed class CallInspection
    {
        /// <summary>Nothing for the dialog to flag.</summary>
        public static readonly CallInspection None = new CallInspection(touchesRecords: false);

        /// <summary>Initializes a new instance of the <see cref="CallInspection"/> class.</summary>
        /// <param name="touchesRecords">Whether the call reads records rather than configuration.</param>
        public CallInspection(bool touchesRecords)
        {
            this.TouchesRecords = touchesRecords;
        }

        /// <summary>Gets a value indicating whether the call reads records rather than configuration.</summary>
        public bool TouchesRecords { get; }
    }
}
