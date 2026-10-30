// <copyright file="CallLog.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// An in-memory log of calls, kept for the life of a tab. Each entry is added whole, so another call's lines
    /// never come between a call line and its outcome line (SPEC §8.6).
    /// </summary>
    internal sealed class CallLog
    {
        private readonly object gate = new object();
        private readonly List<CallLogEntry> entries = new List<CallLogEntry>();

        /// <summary>Raised after an entry is added, on the thread that added it.</summary>
        public event EventHandler<CallLogEntry>? EntryAdded;

        /// <summary>Raised after the log is cleared.</summary>
        public event EventHandler? Cleared;

        /// <summary>Adds an entry.</summary>
        /// <param name="entry">The entry.</param>
        public void Add(CallLogEntry entry)
        {
            lock (this.gate)
            {
                this.entries.Add(entry);
            }

            this.EntryAdded?.Invoke(this, entry);
        }

        /// <summary>Empties the log.</summary>
        public void Clear()
        {
            lock (this.gate)
            {
                this.entries.Clear();
            }

            this.Cleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Returns the entries in order.</summary>
        /// <returns>A copy of the entries.</returns>
        public IReadOnlyList<CallLogEntry> Snapshot()
        {
            lock (this.gate)
            {
                return this.entries.ToList();
            }
        }

        /// <summary>Returns every line, as shown in the panel.</summary>
        /// <returns>The lines.</returns>
        public IReadOnlyList<string> Lines() => this.Snapshot().SelectMany(e => e.Lines).ToList();
    }
}
