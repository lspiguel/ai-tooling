// <copyright file="McpHttpServer.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Hand-rolled MCP Streamable HTTP endpoint for revision 2026-07-28 only (spec §6.1).
    /// Stateless: every request carries its protocol version; no sessions, no SSE.
    /// </summary>
    internal sealed class McpHttpServer : IDisposable
    {
        private const string Base64Prefix = "=?base64?";
        private const string Base64Suffix = "?=";

        private readonly HttpListener listener = new HttpListener();
        private readonly IToolProvider tools;
        private readonly string expectedAuthorization;
        private readonly string serverVersion;
        private readonly Action<string> log;

        public McpHttpServer(int port, string token, IToolProvider tools, string serverVersion, Action<string> log)
        {
            this.Endpoint = $"http://127.0.0.1:{port}/mcp/";
            this.tools = tools;
            this.expectedAuthorization = $"Bearer {token}";
            this.serverVersion = serverVersion;
            this.log = log;
            this.listener.Prefixes.Add(this.Endpoint);
        }

        public string Endpoint { get; }

        public void Start()
        {
            this.listener.Start();
            Task.Run(this.AcceptLoopAsync);
        }

        public void Dispose() => this.listener.Close();

        private static string DecodeHeaderValue(string value)
        {
            if (value != null && value.StartsWith(Base64Prefix, StringComparison.Ordinal) && value.EndsWith(Base64Suffix, StringComparison.Ordinal))
            {
                var encoded = value.Substring(Base64Prefix.Length, value.Length - Base64Prefix.Length - Base64Suffix.Length);
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }

            return value;
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }

            return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Host == "127.0.0.1" || uri.Host == "localhost");
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }

        private static Reply Error(int status, JToken id, int code, string message, JObject data = null)
        {
            var error = new JObject { ["code"] = code, ["message"] = message };
            if (data != null)
            {
                error["data"] = data;
            }

            return new Reply(status, new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = error });
        }

        private static string CheckHeaders(HttpListenerRequest request, string method, JObject parameters, string version)
        {
            if (request.Headers[McpConstants.HeaderProtocolVersion] != version)
            {
                return $"{McpConstants.HeaderProtocolVersion} header missing or not matching the body";
            }

            if (request.Headers[McpConstants.HeaderMethod] != method)
            {
                return $"{McpConstants.HeaderMethod} header missing or not matching the body";
            }

            if (method == McpConstants.MethodToolsCall && DecodeHeaderValue(request.Headers[McpConstants.HeaderName]) != (string)parameters["name"])
            {
                return $"{McpConstants.HeaderName} header missing or not matching the body";
            }

            return null;
        }

        private async Task AcceptLoopAsync()
        {
            while (this.listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await this.listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => this.HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var started = DateTime.UtcNow;
            Reply reply;
            try
            {
                reply = await this.ProcessAsync(context.Request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.log($"Unhandled error: {ex.GetType().Name}: {ex.Message}");
                reply = Error(500, null, McpConstants.InvalidRequest, "Internal error");
            }

            var text = reply.Body?.ToString(Formatting.None);
            this.log($"{context.Request.HttpMethod} {reply.Label} -> {reply.Status}, {text?.Length ?? 0} chars, {(DateTime.UtcNow - started).TotalMilliseconds:0} ms");

            try
            {
                context.Response.StatusCode = reply.Status;
                if (text != null)
                {
                    var bytes = Encoding.UTF8.GetBytes(text);
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }

                context.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is IOException)
            {
                // The client went away while the call was pending: the spec treats this as cancellation.
                this.log($"Client disconnected before the reply was written ({ex.GetType().Name})");
            }
        }

        private async Task<Reply> ProcessAsync(HttpListenerRequest request)
        {
            if (!IsAllowedOrigin(request.Headers["Origin"]))
            {
                return new Reply(403, null, "origin");
            }

            if (!FixedTimeEquals(request.Headers["Authorization"], this.expectedAuthorization))
            {
                return new Reply(401, null, "auth");
            }

            if (request.HttpMethod != "POST")
            {
                return new Reply(405, null, request.HttpMethod);
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            JObject message;
            try
            {
                message = JObject.Parse(body);
            }
            catch (JsonException)
            {
                return Error(400, null, McpConstants.ParseError, "Parse error");
            }

            var id = message["id"];
            var method = (string)message["method"];
            if (method == null)
            {
                return Error(400, id, McpConstants.InvalidRequest, "Missing method");
            }

            if (id == null)
            {
                return new Reply(202, null, method);
            }

            var parameters = message["params"] as JObject ?? new JObject();
            var version = (string)parameters["_meta"]?[McpConstants.MetaProtocolVersion];
            if (method == McpConstants.MethodInitialize || version != McpConstants.ProtocolVersion)
            {
                var data = new JObject { ["supported"] = new JArray(McpConstants.ProtocolVersion), ["requested"] = version };
                return Error(400, id, McpConstants.UnsupportedProtocolVersion, $"Unsupported protocol version. This server supports MCP revision {McpConstants.ProtocolVersion} only.", data).Labelled(method);
            }

            var headerProblem = CheckHeaders(request, method, parameters, version);
            if (headerProblem != null)
            {
                return Error(400, id, McpConstants.HeaderMismatch, $"Header mismatch: {headerProblem}").Labelled(method);
            }

            switch (method)
            {
                case McpConstants.MethodDiscover:
                    return this.Result(id, this.Discover(), method);
                case McpConstants.MethodToolsList:
                    return this.Result(id, this.ToolsList(), method);
                case McpConstants.MethodToolsCall:
                    return await this.CallToolAsync(id, parameters).ConfigureAwait(false);
                default:
                    return Error(404, id, McpConstants.MethodNotFound, $"Method not found: {method}").Labelled(method);
            }
        }

        private JObject Discover() => new JObject
        {
            ["resultType"] = "complete",
            ["supportedVersions"] = new JArray(McpConstants.ProtocolVersion),
            ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
            ["instructions"] = "Spike server for the Agent Companion MCP. Not for delivery use.",
            ["ttlMs"] = McpConstants.ListTtlMs,
            ["cacheScope"] = "private",
        };

        private JObject ToolsList() => new JObject
        {
            ["resultType"] = "complete",
            ["tools"] = this.tools.ListTools(),
            ["ttlMs"] = McpConstants.ListTtlMs,
            ["cacheScope"] = "private",
        };

        private async Task<Reply> CallToolAsync(JToken id, JObject parameters)
        {
            var name = (string)parameters["name"];
            if (name == null || !this.tools.HasTool(name))
            {
                return Error(200, id, McpConstants.InvalidParams, $"Unknown tool: {name}").Labelled($"tools/call {name}");
            }

            var arguments = parameters["arguments"] as JObject ?? new JObject();
            var outcome = await this.tools.CallAsync(name, arguments, CancellationToken.None).ConfigureAwait(false);
            return this.Result(id, outcome.ToMcpResult(), $"tools/call {name}");
        }

        private Reply Result(JToken id, JObject result, string label)
        {
            result["_meta"] = new JObject
            {
                [McpConstants.MetaServerInfo] = new JObject { ["name"] = McpConstants.ServerName, ["version"] = this.serverVersion },
            };

            return new Reply(200, new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }, label);
        }

        private sealed class Reply
        {
            public Reply(int status, JObject body, string label = null)
            {
                this.Status = status;
                this.Body = body;
                this.Label = label ?? string.Empty;
            }

            public int Status { get; }

            public JObject Body { get; }

            public string Label { get; private set; }

            public Reply Labelled(string label)
            {
                this.Label = label;
                return this;
            }
        }
    }
}
