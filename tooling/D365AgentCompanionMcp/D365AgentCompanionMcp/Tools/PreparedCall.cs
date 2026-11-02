// <copyright file="PreparedCall.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    /// <summary>
    /// What a Write or Publisher tool resolved before its dialog (SPEC §6.6: resolution comes first, so the dialog shows
    /// names, not bare IDs). Preparation runs in the connection's queue, after the gates and before the approval.
    /// </summary>
    internal sealed class PreparedCall
    {
        /// <summary>Initializes a new instance of the <see cref="PreparedCall"/> class.</summary>
        /// <param name="summary">What the call will do, resolved to names, for the dialog.</param>
        /// <param name="state">What the tool resolved, handed to it when it runs.</param>
        /// <param name="confirmation">Text the person must type to approve, or <c>null</c> (SPEC §8.3).</param>
        public PreparedCall(string summary, object? state = null, string? confirmation = null)
        {
            this.Summary = summary;
            this.State = state;
            this.Confirmation = confirmation;
        }

        /// <summary>Gets what the call will do, resolved to names.</summary>
        public string Summary { get; }

        /// <summary>Gets what the tool resolved.</summary>
        public object? State { get; }

        /// <summary>Gets the text the person must type to approve, or <c>null</c>.</summary>
        public string? Confirmation { get; }
    }
}
