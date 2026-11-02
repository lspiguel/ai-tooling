// <copyright file="BoundConnection.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Threading;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// One XrmToolBox connection bound to one tab (C1–C9): its identity, its per-connection settings and once-scoped
    /// confirmation, and the queue every call to it waits in.
    /// </summary>
    internal sealed class BoundConnection
    {
        private readonly CancellationTokenSource ended = new CancellationTokenSource();
        private int pending;
        private int running;

        /// <summary>Initializes a new instance of the <see cref="BoundConnection"/> class, in the claimed state.</summary>
        /// <param name="owner">The tab.</param>
        /// <param name="connectionId">The XrmToolBox connection ID, or one the host minted (D4).</param>
        /// <param name="connectionIdMinted">Whether the host minted the ID.</param>
        /// <param name="connectionName">The name in the XrmToolBox connection manager.</param>
        /// <param name="organizationUniqueName">The organization's unique name.</param>
        /// <param name="organizationFriendlyName">The organization's friendly name.</param>
        /// <param name="organizationUrl">The web application URL.</param>
        public BoundConnection(IHostInstance owner, string connectionId, bool connectionIdMinted, string connectionName, string organizationUniqueName, string organizationFriendlyName, string organizationUrl)
        {
            this.Owner = owner;
            this.ConnectionId = ConnectionRegistry.Normalize(connectionId);
            this.ConnectionIdMinted = connectionIdMinted;
            this.ConnectionName = connectionName;
            this.OrganizationUniqueName = organizationUniqueName;
            this.OrganizationFriendlyName = organizationFriendlyName;
            this.OrganizationUrl = organizationUrl;
        }

        /// <summary>Raised when the pending or running count changes.</summary>
        public event EventHandler? ActivityChanged;

        /// <summary>Gets the tab.</summary>
        public IHostInstance Owner { get; }

        /// <summary>Gets the connection ID agents pass as <c>connection</c>.</summary>
        public string ConnectionId { get; }

        /// <summary>Gets a value indicating whether the host minted the ID.</summary>
        public bool ConnectionIdMinted { get; }

        /// <summary>Gets the connection name.</summary>
        public string ConnectionName { get; }

        /// <summary>Gets the organization's unique name.</summary>
        public string OrganizationUniqueName { get; }

        /// <summary>Gets the organization's friendly name.</summary>
        public string OrganizationFriendlyName { get; }

        /// <summary>Gets the web application URL.</summary>
        public string OrganizationUrl { get; }

        /// <summary>Gets the organization ID, known once bound.</summary>
        public Guid OrganizationId { get; private set; }

        /// <summary>Gets the connected user's ID, known once bound.</summary>
        public Guid UserId { get; private set; }

        /// <summary>Gets a value indicating whether the connection impersonates another user.</summary>
        public bool Impersonating { get; private set; }

        /// <summary>Gets the service calls run with — a clone of the tab's, where XrmToolBox allows it.</summary>
        public IOrganizationService? Service { get; private set; }

        /// <summary>Gets when the connection was bound, UTC.</summary>
        public DateTime? BoundOnUtc { get; private set; }

        /// <summary>Gets the binding state.</summary>
        public BindingState State { get; private set; } = BindingState.Claimed;

        /// <summary>Gets or sets a value indicating whether queries that touch records may run (per connection, C8).</summary>
        public bool RecordQueriesEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether <c>create_publisher</c> may run (per connection, off by default, R3).</summary>
        public bool PublisherCreationEnabled { get; set; }

        /// <summary>Gets or sets a value indicating whether the person confirmed this binding as development, once-scoped.</summary>
        public bool DevConfirmedOnce { get; set; }

        /// <summary>Gets the queue every call to this connection waits in; one call at a time (SPEC §5 Threading).</summary>
        public SemaphoreSlim Queue { get; } = new SemaphoreSlim(1, 1);

        /// <summary>Gets a token signalled when the binding ends.</summary>
        public CancellationToken Ended => this.ended.Token;

        /// <summary>Gets the number of calls queued or awaiting approval.</summary>
        public int Pending => Volatile.Read(ref this.pending);

        /// <summary>Gets the number of calls running.</summary>
        public int Running => Volatile.Read(ref this.running);

        /// <summary>Records the identity that completes the binding.</summary>
        /// <param name="organizationId">The organization ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <param name="impersonating">Whether the connection impersonates another user.</param>
        /// <param name="service">The service calls run with.</param>
        /// <param name="boundOnUtc">When it was bound.</param>
        public void MarkBound(Guid organizationId, Guid userId, bool impersonating, IOrganizationService service, DateTime boundOnUtc)
        {
            this.OrganizationId = organizationId;
            this.UserId = userId;
            this.Impersonating = impersonating;
            this.Service = service;
            this.BoundOnUtc = boundOnUtc;
            this.State = BindingState.Bound;
        }

        /// <summary>Ends the binding: queued and pending calls fail with <c>connection_closed</c> and the once-scoped confirmation ends (C5).</summary>
        public void End()
        {
            this.State = BindingState.Ended;
            this.DevConfirmedOnce = false;
            this.ended.Cancel();
        }

        /// <summary>Counts a call entering the queue.</summary>
        public void BeginPending() => this.Change(ref this.pending, 1);

        /// <summary>Counts a call leaving the queue or its approval.</summary>
        public void EndPending() => this.Change(ref this.pending, -1);

        /// <summary>Counts a call starting to run.</summary>
        public void BeginRunning() => this.Change(ref this.running, 1);

        /// <summary>Counts a call that finished running.</summary>
        public void EndRunning() => this.Change(ref this.running, -1);

        private void Change(ref int counter, int delta)
        {
            Interlocked.Add(ref counter, delta);
            this.ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
