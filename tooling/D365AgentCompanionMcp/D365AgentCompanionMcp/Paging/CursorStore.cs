// <copyright file="CursorStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;

    /// <summary>
    /// Host-side paging cursors (SPEC §6.2): opaque handles bound to the connection and tool of their first call,
    /// held in memory only. They expire after 10 minutes unused, when the connection's binding ends, or when the host stops.
    /// </summary>
    internal sealed class CursorStore
    {
        /// <summary>The length of every cursor handle.</summary>
        public const int CursorLength = 32;

        /// <summary>How long an unused cursor lives.</summary>
        public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(10);

        private readonly object gate = new object();
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly IClock clock;

        /// <summary>Initializes a new instance of the <see cref="CursorStore"/> class.</summary>
        /// <param name="clock">The clock for expiry.</param>
        public CursorStore(IClock clock)
        {
            this.clock = clock;
        }

        /// <summary>Gets the number of open cursors.</summary>
        public int Count
        {
            get
            {
                lock (this.gate)
                {
                    return this.entries.Count;
                }
            }
        }

        /// <summary>Opens a cursor for a paged call that has more pages.</summary>
        /// <param name="connectionId">The connection of the first call.</param>
        /// <param name="tool">The tool of the first call.</param>
        /// <param name="pager">The call's pager.</param>
        /// <returns>The cursor handle.</returns>
        public string Open(string connectionId, string tool, Pager pager)
        {
            var cursor = Guid.NewGuid().ToString("N");
            lock (this.gate)
            {
                this.ExpireIdle();
                this.entries[cursor] = new Entry(connectionId, tool, pager, this.clock.UtcNow);
            }

            return cursor;
        }

        /// <summary>Finds a live cursor for this connection and tool, and marks it used.</summary>
        /// <param name="cursor">The cursor handle.</param>
        /// <param name="connectionId">The connection the call names.</param>
        /// <param name="tool">The tool called.</param>
        /// <returns>The pager, or <c>null</c> when the cursor is unknown, expired, or belongs to another connection or tool.</returns>
        public Pager? Find(string cursor, string connectionId, string tool)
        {
            lock (this.gate)
            {
                this.ExpireIdle();
                if (!this.entries.TryGetValue(cursor, out var entry) || entry.ConnectionId != connectionId || entry.Tool != tool)
                {
                    return null;
                }

                entry.LastUsed = this.clock.UtcNow;
                return entry.Pager;
            }
        }

        /// <summary>Closes a cursor after its last page.</summary>
        /// <param name="cursor">The cursor handle.</param>
        public void Close(string cursor)
        {
            lock (this.gate)
            {
                this.entries.Remove(cursor);
            }
        }

        /// <summary>Releases every cursor of a connection whose binding ended (C5).</summary>
        /// <param name="connectionId">The connection.</param>
        public void ReleaseConnection(string connectionId)
        {
            lock (this.gate)
            {
                foreach (var key in this.entries.Where(e => e.Value.ConnectionId == connectionId).Select(e => e.Key).ToList())
                {
                    this.entries.Remove(key);
                }
            }
        }

        /// <summary>Releases every cursor, for a host stop.</summary>
        public void Clear()
        {
            lock (this.gate)
            {
                this.entries.Clear();
            }
        }

        private void ExpireIdle()
        {
            var cutoff = this.clock.UtcNow - IdleLimit;
            foreach (var key in this.entries.Where(e => e.Value.LastUsed < cutoff).Select(e => e.Key).ToList())
            {
                this.entries.Remove(key);
            }
        }

        private sealed class Entry
        {
            public Entry(string connectionId, string tool, Pager pager, DateTime lastUsed)
            {
                this.ConnectionId = connectionId;
                this.Tool = tool;
                this.Pager = pager;
                this.LastUsed = lastUsed;
            }

            public string ConnectionId { get; }

            public string Tool { get; }

            public Pager Pager { get; }

            public DateTime LastUsed { get; set; }
        }
    }
}
