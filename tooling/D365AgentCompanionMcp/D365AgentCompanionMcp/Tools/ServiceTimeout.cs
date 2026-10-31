// <copyright file="ServiceTimeout.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Tooling.Connector;

    /// <summary>
    /// Runs one slow request with a longer timeout on the call's service, then restores it. Calls are serialized per
    /// connection (SPEC §5 Threading), so no other request runs on that service meanwhile. The process-wide
    /// <c>CrmServiceClient.MaxConnectionTimeout</c> is never changed: it would reach every tool in XrmToolBox.
    /// </summary>
    internal static class ServiceTimeout
    {
        /// <summary>The timeout for building a table's whole ribbon, which the platform can take minutes over (L3).</summary>
        public static readonly TimeSpan Ribbon = TimeSpan.FromMinutes(5);

        /// <summary>Runs a request with at least the given timeout.</summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="service">The call's service.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="run">The request.</param>
        /// <returns>The result.</returns>
        public static T With<T>(IOrganizationService service, TimeSpan timeout, Func<T> run)
        {
            if (!(service is CrmServiceClient client))
            {
                return run();
            }

            // OAuth and certificate connections go through the web proxy; older authentication types through the service proxy.
            var channel = client.OrganizationWebProxyClient?.InnerChannel;
            var proxy = client.OrganizationServiceProxy;
            var channelTimeout = channel?.OperationTimeout;
            var proxyTimeout = proxy?.Timeout;
            try
            {
                if (channel != null && channel.OperationTimeout < timeout)
                {
                    channel.OperationTimeout = timeout;
                }

                if (proxy != null && proxy.Timeout < timeout)
                {
                    proxy.Timeout = timeout;
                }

                return run();
            }
            finally
            {
                if (channel != null && channelTimeout.HasValue)
                {
                    channel.OperationTimeout = channelTimeout.Value;
                }

                if (proxy != null && proxyTimeout.HasValue)
                {
                    proxy.Timeout = proxyTimeout.Value;
                }
            }
        }
    }
}
