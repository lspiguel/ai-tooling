// <copyright file="WebApiQueryRunner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Queries
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;

    using Lspiguel.Xrm.D365ContextExporter.Models;

    using Newtonsoft.Json.Linq;

    /// <summary>Executes a <c>webapi</c> query against the Dataverse Web API with OData nextLink pagination.</summary>
    internal sealed class WebApiQueryRunner
    {
        private readonly string environmentUrl;
        private readonly Func<string> getToken;
        private readonly HttpClient httpClient;
        private readonly Action<string> log;

        /// <summary>Initializes a new instance of the <see cref="WebApiQueryRunner"/> class.</summary>
        /// <param name="environmentUrl">Base URL of the Dataverse environment (e.g. <c>https://org.crm.dynamics.com</c>).</param>
        /// <param name="getToken">Delegate that returns a valid OAuth bearer token.</param>
        /// <param name="httpClient">HTTP client to use; injected to enable testing.</param>
        /// <param name="log">Log sink.</param>
        public WebApiQueryRunner(string environmentUrl, Func<string> getToken, HttpClient httpClient, Action<string> log)
        {
            this.environmentUrl = environmentUrl.TrimEnd('/');
            this.getToken = getToken;
            this.httpClient = httpClient;
            this.log = log;
        }

        /// <summary>Executes the query and returns all records as a <see cref="JArray"/>.</summary>
        public JArray Run(QueryDefinition query, CancellationToken cancellationToken)
        {
            this.log($"[webapi] Executing query '{query.Id}'...");

            var url = BuildUrl(this.environmentUrl, query);
            var token = this.getToken();
            var accumulated = new JArray();

            while (url != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.ParseAdd("application/json; odata.metadata=minimal");
                request.Headers.TryAddWithoutValidation("OData-MaxVersion", "4.0");
                request.Headers.TryAddWithoutValidation("OData-Version", "4.0");

                var response = this.httpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    throw new HttpRequestException(
                        $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Response body: {body}");
                }

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var jobj = JObject.Parse(json);

                var value = (jobj["value"] as JArray) ?? new JArray();
                foreach (var item in value)
                {
                    accumulated.Add(item);
                }

                this.log($"[webapi] '{query.Id}': page retrieved {value.Count} records (total so far: {accumulated.Count}).");

                if (query.MaxRecords.HasValue && accumulated.Count >= query.MaxRecords.Value)
                {
                    break;
                }

                url = jobj["@odata.nextLink"]?.Value<string>();
            }

            this.log($"[webapi] '{query.Id}': complete, {accumulated.Count} total records.");
            return accumulated;
        }

        private static string BuildUrl(string baseEnvUrl, QueryDefinition query)
        {
            var url = $"{baseEnvUrl}/api/data/v9.2/{query.Path}";

            if (query.Select != null && query.Select.Count > 0)
            {
                var sep = url.IndexOf('?') >= 0 ? "&" : "?";
                url += $"{sep}$select={string.Join(",", query.Select)}";
            }

            return url;
        }
    }
}
