// <copyright file="CallLogEntry.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One call in a log: its line pair, any warning lines, and the detail shown on double-click (SPEC §8.4).
    /// The detail is held in memory only and never saved.
    /// </summary>
    internal sealed class CallLogEntry
    {
        /// <summary>Initializes a new instance of the <see cref="CallLogEntry"/> class.</summary>
        /// <param name="receivedLocal">When the host received the call, in local time.</param>
        /// <param name="tool">The tool name.</param>
        /// <param name="tier">The tier label, for example <c>READ</c>.</param>
        /// <param name="approval">How the call was authorized, as on the outcome line.</param>
        /// <param name="callLine">The call line.</param>
        /// <param name="outcomeLine">The outcome line.</param>
        /// <param name="warnings">Warning lines that follow the outcome line.</param>
        /// <param name="argumentsJson">The full arguments, verbatim.</param>
        /// <param name="resultJson">The full result or tool error.</param>
        public CallLogEntry(DateTime receivedLocal, string tool, string tier, string approval, string callLine, string outcomeLine, IReadOnlyList<string> warnings, string argumentsJson, string resultJson)
        {
            this.ReceivedLocal = receivedLocal;
            this.Tool = tool;
            this.Tier = tier;
            this.Approval = approval;
            this.CallLine = callLine;
            this.OutcomeLine = outcomeLine;
            this.Warnings = warnings;
            this.ArgumentsJson = argumentsJson;
            this.ResultJson = resultJson;
        }

        /// <summary>Gets when the host received the call, in local time.</summary>
        public DateTime ReceivedLocal { get; }

        /// <summary>Gets the tool name.</summary>
        public string Tool { get; }

        /// <summary>Gets the tier label.</summary>
        public string Tier { get; }

        /// <summary>Gets how the call was authorized.</summary>
        public string Approval { get; }

        /// <summary>Gets the call line.</summary>
        public string CallLine { get; }

        /// <summary>Gets the outcome line.</summary>
        public string OutcomeLine { get; }

        /// <summary>Gets the warning lines.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Gets the full arguments, verbatim.</summary>
        public string ArgumentsJson { get; }

        /// <summary>Gets the full result or tool error.</summary>
        public string ResultJson { get; }

        /// <summary>Gets every line of the entry, in display order.</summary>
        public IEnumerable<string> Lines
        {
            get
            {
                yield return this.CallLine;
                yield return this.OutcomeLine;
                foreach (var warning in this.Warnings)
                {
                    yield return warning;
                }
            }
        }
    }
}
