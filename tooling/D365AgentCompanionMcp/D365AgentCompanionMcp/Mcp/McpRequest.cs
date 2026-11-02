// <copyright file="McpRequest.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System.Collections.Specialized;

    /// <summary>An HTTP request as the protocol layer sees it, independent of <c>HttpListener</c>.</summary>
    internal sealed class McpRequest
    {
        /// <summary>Initializes a new instance of the <see cref="McpRequest"/> class.</summary>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="headers">The request headers; lookups must be case-insensitive.</param>
        /// <param name="body">The request body, or an empty string.</param>
        public McpRequest(string httpMethod, NameValueCollection headers, string body)
        {
            this.HttpMethod = httpMethod;
            this.Headers = headers;
            this.Body = body;
        }

        /// <summary>Gets the HTTP method.</summary>
        public string HttpMethod { get; }

        /// <summary>Gets the request headers.</summary>
        public NameValueCollection Headers { get; }

        /// <summary>Gets the request body.</summary>
        public string Body { get; }
    }
}
