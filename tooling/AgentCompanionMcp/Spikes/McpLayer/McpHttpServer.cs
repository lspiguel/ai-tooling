// <copyright file="McpHttpServer.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Hand-rolled, dual-era MCP Streamable HTTP endpoint (spec §6.1, decision D7).
    /// Requests carrying <c>_meta</c> protocol metadata are served statelessly under revision 2026-07-28.
    /// <c>initialize</c> opens a handshake-era session (2025-11-25 or 2025-06-18), identified by <c>Mcp-Session-Id</c>.
    /// No SSE in either era.
    /// </summary>
    internal sealed class McpHttpServer : IDisposable
    {
        private const string Base64Prefix = "=?base64?";
        private const string Base64Suffix = "?=";
        private static readonly TimeSpan SessionIdleLimit = TimeSpan.FromMinutes(30);

        private readonly HttpListener listener = new HttpListener();
        private readonly ConcurrentDictionary<string, LegacySession> sessions = new ConcurrentDictionary<string, LegacySession>();
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

        private static string CheckModernHeaders(HttpListenerRequest request, string method, JObject parameters, string version)
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
                foreach (var header in reply.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

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

            var sessionId = request.Headers[McpConstants.HeaderSessionId];
            if (request.HttpMethod == "DELETE" && sessionId != null)
            {
                // Handshake era: the client ends its session.
                return this.sessions.TryRemove(sessionId, out _) ? new Reply(200, null, "DELETE session") : new Reply(404, null, "DELETE unknown session");
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
            var modernVersion = (string)parameters["_meta"]?[McpConstants.MetaProtocolVersion];

            if (method == McpConstants.MethodInitialize)
            {
                return this.Initialize(id, parameters);
            }

            if (modernVersion == null && sessionId != null)
            {
                return await this.ProcessLegacyAsync(request, id, method, parameters, sessionId).ConfigureAwait(false);
            }

            return await this.ProcessModernAsync(request, id, method, parameters, modernVersion).ConfigureAwait(false);
        }

        private async Task<Reply> ProcessModernAsync(HttpListenerRequest request, JToken id, string method, JObject parameters, string version)
        {
            if (version != McpConstants.ProtocolVersion)
            {
                var data = new JObject { ["supported"] = new JArray(McpConstants.ProtocolVersion), ["requested"] = version };
                return Error(400, id, McpConstants.UnsupportedProtocolVersion, $"Unsupported protocol version. Send {McpConstants.ProtocolVersion} in _meta, or open a session with initialize.", data).Labelled(method);
            }

            var headerProblem = CheckModernHeaders(request, method, parameters, version);
            if (headerProblem != null)
            {
                return Error(400, id, McpConstants.HeaderMismatch, $"Header mismatch: {headerProblem}").Labelled(method);
            }

            switch (method)
            {
                case McpConstants.MethodDiscover:
                    return this.ModernResult(id, this.Discover(), method);
                case McpConstants.MethodToolsList:
                    return this.ModernResult(id, this.ToolsList(modern: true), method);
                case McpConstants.MethodToolsCall:
                    return await this.CallToolAsync(id, parameters, modern: true).ConfigureAwait(false);
                default:
                    return Error(404, id, McpConstants.MethodNotFound, $"Method not found: {method}").Labelled(method);
            }
        }

        private Reply Initialize(JToken id, JObject parameters)
        {
            var requested = (string)parameters["protocolVersion"];
            var negotiated = McpConstants.LegacyVersions.Contains(requested) ? requested : McpConstants.LegacyVersions[0];

            this.ExpireIdleSessions();
            var sessionId = Guid.NewGuid().ToString("N");
            this.sessions[sessionId] = new LegacySession(negotiated);

            var result = new JObject
            {
                ["protocolVersion"] = negotiated,
                ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
                ["serverInfo"] = new JObject { ["name"] = McpConstants.ServerName, ["version"] = this.serverVersion },
                ["instructions"] = "Spike server for the Agent Companion MCP. Not for delivery use.",
            };

            var reply = new Reply(200, new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }, $"initialize (requested {requested ?? "none"}, negotiated {negotiated})");
            reply.Headers[McpConstants.HeaderSessionId] = sessionId;
            return reply;
        }

        private async Task<Reply> ProcessLegacyAsync(HttpListenerRequest request, JToken id, string method, JObject parameters, string sessionId)
        {
            if (!this.sessions.TryGetValue(sessionId, out var session))
            {
                return Error(404, id, McpConstants.SessionNotFound, "Session not found. Send initialize to start a new session.").Labelled($"{method} (unknown session)");
            }

            session.LastUsed = DateTime.UtcNow;
            var headerVersion = request.Headers[McpConstants.HeaderProtocolVersion];
            if (headerVersion != null && headerVersion != session.Version)
            {
                return Error(400, id, McpConstants.InvalidRequest, $"{McpConstants.HeaderProtocolVersion} {headerVersion} does not match the session's {session.Version}").Labelled(method);
            }

            var label = $"{method} [{session.Version}]";
            switch (method)
            {
                case McpConstants.MethodPing:
                    return this.LegacyResult(id, new JObject(), label);
                case McpConstants.MethodToolsList:
                    return this.LegacyResult(id, this.ToolsList(modern: false), label);
                case McpConstants.MethodToolsCall:
                    return await this.CallToolAsync(id, parameters, modern: false).ConfigureAwait(false);
                default:
                    return Error(200, id, McpConstants.MethodNotFound, $"Method not found: {method}").Labelled(label);
            }
        }

        private void ExpireIdleSessions()
        {
            var cutoff = DateTime.UtcNow - SessionIdleLimit;
            foreach (var stale in this.sessions.Where(s => s.Value.LastUsed < cutoff).Select(s => s.Key).ToList())
            {
                this.sessions.TryRemove(stale, out _);
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

        private JObject ToolsList(bool modern)
        {
            var result = new JObject { ["tools"] = this.tools.ListTools() };
            if (modern)
            {
                result["resultType"] = "complete";
                result["ttlMs"] = McpConstants.ListTtlMs;
                result["cacheScope"] = "private";
            }

            return result;
        }

        private async Task<Reply> CallToolAsync(JToken id, JObject parameters, bool modern)
        {
            var name = (string)parameters["name"];
            if (name == null || !this.tools.HasTool(name))
            {
                return Error(200, id, McpConstants.InvalidParams, $"Unknown tool: {name}").Labelled($"tools/call {name}");
            }

            var arguments = parameters["arguments"] as JObject ?? new JObject();
            var outcome = await this.tools.CallAsync(name, arguments, CancellationToken.None).ConfigureAwait(false);
            var result = outcome.ToMcpResult();
            var label = $"tools/call {name}";
            if (modern)
            {
                return this.ModernResult(id, result, label);
            }

            result.Remove("resultType");
            return this.LegacyResult(id, result, $"{label} [legacy]");
        }

        private Reply ModernResult(JToken id, JObject result, string label)
        {
            result["_meta"] = new JObject
            {
                [McpConstants.MetaServerInfo] = new JObject { ["name"] = McpConstants.ServerName, ["version"] = this.serverVersion },
            };

            return new Reply(200, new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }, label);
        }

        private Reply LegacyResult(JToken id, JObject result, string label) =>
            new Reply(200, new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }, label);

        private sealed class LegacySession
        {
            public LegacySession(string version)
            {
                this.Version = version;
                this.LastUsed = DateTime.UtcNow;
            }

            public string Version { get; }

            public DateTime LastUsed { get; set; }
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

            public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

            public Reply Labelled(string label)
            {
                this.Label = label;
                return this;
            }
        }
    }
}
