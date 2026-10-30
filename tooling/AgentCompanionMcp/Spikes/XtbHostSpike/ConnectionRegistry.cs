// <copyright file="ConnectionRegistry.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;

    /// <summary>What the host needs from a bound tab (spec C7–C9).</summary>
    internal interface IConnectionTab
    {
        IOrganizationService Service { get; }

        Task<bool> RequestApprovalAsync(string tool, string summary);

        void Log(string line);
    }

    /// <summary>A connection bound to one tab.</summary>
    internal sealed class BoundConnection
    {
        public string ConnectionId { get; set; }

        public bool ConnectionIdMinted { get; set; }

        public string ConnectionName { get; set; }

        public Guid OrganizationId { get; set; }

        public string OrganizationFriendlyName { get; set; }

        public string Url { get; set; }

        public DateTime BoundOn { get; set; }

        public IConnectionTab Tab { get; set; }
    }

    /// <summary>One tab per connection: duplicates by connection ID or organization ID are refused (spec C3).</summary>
    internal sealed class ConnectionRegistry
    {
        private readonly object gate = new object();
        private readonly List<BoundConnection> bound = new List<BoundConnection>();

        /// <summary>Binds the connection, or returns the binding it conflicts with.</summary>
        public BoundConnection TryBind(BoundConnection connection)
        {
            lock (this.gate)
            {
                var conflict = this.bound.FirstOrDefault(b => b.Tab != connection.Tab
                    && (b.ConnectionId == connection.ConnectionId || b.OrganizationId == connection.OrganizationId));
                if (conflict != null)
                {
                    return conflict;
                }

                this.bound.RemoveAll(b => b.Tab == connection.Tab);
                this.bound.Add(connection);
                return null;
            }
        }

        public void Unbind(IConnectionTab tab)
        {
            lock (this.gate)
            {
                this.bound.RemoveAll(b => b.Tab == tab);
            }
        }

        public BoundConnection Find(string connectionId)
        {
            lock (this.gate)
            {
                return this.bound.FirstOrDefault(b => b.ConnectionId == connectionId);
            }
        }

        public List<BoundConnection> Snapshot()
        {
            lock (this.gate)
            {
                return this.bound.OrderBy(b => b.BoundOn).ToList();
            }
        }
    }
}
