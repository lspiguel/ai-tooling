// <copyright file="McpProtocol.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The dual-era MCP endpoint logic (SPEC §6.1, D7), independent of the HTTP transport.
    /// Requests carrying <c>_meta</c> protocol metadata are served statelessly under revision 2026-07-28;
    /// <c>initialize</c> opens a handshake-era session (2025-11-25 or 2025-06-18) identified by <c>Mcp-Session-Id</c>.
    /// Every check that can answer with a status other than <c>200</c> runs before a tool call starts, so a
    /// pending reply can be committed early and kept alive (D9).
    /// </summary>
    internal sealed class McpProtocol
    {
        private const string Base64Prefix = "=?base64?";
        private const string Base64Suffix = "?=";
        private static readonly TimeSpan SessionIdleLimit = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, LegacySession> sessions = new ConcurrentDictionary<string, LegacySession>(StringComparer.Ordinal);
        private readonly IToolHost tools;
        private readonly Func<string?> currentToken;
        private readonly string serverVersion;
        private readonly IClock clock;

        /// <summary>Initializes a new instance of the <see cref="McpProtocol"/> class.</summary>
        /// <param name="tools">The tools behind the endpoint.</param>
        /// <param name="currentToken">Returns the current bearer token, read on every request so that Regenerate takes effect at once.</param>
        /// <param name="serverVersion">The plugin version reported in server info.</param>
        /// <param name="clock">The clock for session expiry and call receipt times.</param>
        public McpProtocol(IToolHost tools, Func<string?> currentToken, string serverVersion, IClock clock)
        {
            this.tools = tools;
            this.currentToken = currentToken;
            this.serverVersion = serverVersion;
            this.clock = clock;
        }

        /// <summary>Gets the number of open handshake-era sessions.</summary>
        public int SessionCount => this.sessions.Count;

        /// <summary>Answers one HTTP request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="clientGone">Signalled by the transport when the client closes the request.</param>
        /// <returns>The reply, complete or pending.</returns>
        public McpReply Process(McpRequest request, CancellationToken clientGone)
        {
            if (!IsAllowedOrigin(request.Headers["Origin"]))
            {
                return McpReply.Complete(403, null, "origin");
            }

            var token = this.currentToken();
            if (token == null || !FixedTimeEquals(request.Headers["Authorization"], $"Bearer {token}"))
            {
                return McpReply.Text(401, McpConstants.UnauthorizedBody, "auth");
            }

            var sessionId = request.Headers[McpConstants.HeaderSessionId];
            if (request.HttpMethod == "DELETE" && sessionId != null)
            {
                return this.EndSession(sessionId);
            }

            if (request.HttpMethod != "POST")
            {
                return McpReply.Complete(405, null, request.HttpMethod);
            }

            JObject message;
            try
            {
                message = JObject.Parse(request.Body);
            }
            catch (JsonException)
            {
                return Error(400, null, McpConstants.ParseError, "Parse error", "parse");
            }

            var id = message["id"];
            var method = message["method"]?.Type == JTokenType.String ? (string?)message["method"] : null;
            if (method == null)
            {
                return Error(400, id, McpConstants.InvalidRequest, "Missing method", "no method");
            }

            var parameters = message["params"] as JObject ?? new JObject();
            if (id == null)
            {
                this.Notify(method, parameters, sessionId);
                return McpReply.Complete(202, null, method);
            }

            if (method == McpConstants.MethodInitialize)
            {
                return this.Initialize(id, parameters);
            }

            var modernVersion = parameters["_meta"]?[McpConstants.MetaProtocolVersion]?.Type == JTokenType.String
                ? (string?)parameters["_meta"]![McpConstants.MetaProtocolVersion]
                : null;
            if (modernVersion == null && sessionId != null)
            {
                return this.ProcessLegacy(request, id, method, parameters, sessionId, clientGone);
            }

            return this.ProcessModern(request, id, method, parameters, modernVersion, clientGone);
        }

        /// <summary>Cancels every pending call of every session, for a host stop.</summary>
        public void EndAllSessions()
        {
            foreach (var key in this.sessions.Keys.ToList())
            {
                if (this.sessions.TryRemove(key, out var session))
                {
                    session.CancelAll();
                }
            }
        }

        private static string? DecodeHeaderValue(string? value)
        {
            if (value != null && value.StartsWith(Base64Prefix, StringComparison.Ordinal) && value.EndsWith(Base64Suffix, StringComparison.Ordinal))
            {
                var encoded = value.Substring(Base64Prefix.Length, value.Length - Base64Prefix.Length - Base64Suffix.Length);
                try
                {
                    return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                }
                catch (FormatException)
                {
                    return null;
                }
            }

            return value;
        }

        private static bool IsAllowedOrigin(string? origin)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }

            return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Host == "127.0.0.1" || uri.Host == "localhost" || uri.Host == "[::1]");
        }

        private static bool FixedTimeEquals(string? left, string right)
        {
            if (left == null || left.Length != right.Length)
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

        private static JObject ErrorBody(JToken? id, int code, string message, JObject? data = null)
        {
            var error = new JObject { ["code"] = code, ["message"] = message };
            if (data != null)
            {
                error["data"] = data;
            }

            return new JObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone() ?? JValue.CreateNull(), ["error"] = error };
        }

        private static McpReply Error(int status, JToken? id, int code, string message, string label, JObject? data = null) =>
            McpReply.Complete(status, ErrorBody(id, code, message, data), label);

        private static string? CheckModernHeaders(McpRequest request, string method, JObject parameters, string version)
        {
            if (request.Headers[McpConstants.HeaderProtocolVersion] != version)
            {
                return $"{McpConstants.HeaderProtocolVersion} header missing or not matching the body";
            }

            if (request.Headers[McpConstants.HeaderMethod] != method)
            {
                return $"{McpConstants.HeaderMethod} header missing or not matching the body";
            }

            if (method == McpConstants.MethodToolsCall
                && DecodeHeaderValue(request.Headers[McpConstants.HeaderName]) != parameters["name"]?.ToString())
            {
                return $"{McpConstants.HeaderName} header missing or not matching the body";
            }

            return null;
        }

        private static JObject Envelope(JToken id, JObject result) =>
            new JObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result };

        private static string RequestKey(JToken id) => id.ToString(Formatting.None);

        private McpReply ProcessModern(McpRequest request, JToken id, string method, JObject parameters, string? version, CancellationToken clientGone)
        {
            if (version != McpConstants.ProtocolVersion)
            {
                var data = new JObject { ["supported"] = new JArray(McpConstants.ProtocolVersion), ["requested"] = version };
                return Error(400, id, McpConstants.UnsupportedProtocolVersion, $"Unsupported protocol version. Send {McpConstants.ProtocolVersion} in _meta, or open a session with initialize.", method, data);
            }

            var headerProblem = CheckModernHeaders(request, method, parameters, version);
            if (headerProblem != null)
            {
                return Error(400, id, McpConstants.HeaderMismatch, $"Header mismatch: {headerProblem}", method);
            }

            switch (method)
            {
                case McpConstants.MethodDiscover:
                    return McpReply.Complete(200, this.ModernEnvelope(id, this.Discover()), method);
                case McpConstants.MethodToolsList:
                    return McpReply.Complete(200, this.ModernEnvelope(id, this.ToolsList(modern: true)), method);
                case McpConstants.MethodToolsCall:
                    return this.CallTool(id, parameters, null, clientGone);
                default:
                    return Error(404, id, McpConstants.MethodNotFound, $"Method not found: {method}", method);
            }
        }

        private McpReply ProcessLegacy(McpRequest request, JToken id, string method, JObject parameters, string sessionId, CancellationToken clientGone)
        {
            if (!this.TryGetSession(sessionId, out var session))
            {
                return Error(404, id, McpConstants.SessionNotFound, "Session not found. Send initialize to start a new session.", $"{method} (unknown session)");
            }

            var headerVersion = request.Headers[McpConstants.HeaderProtocolVersion];
            if (headerVersion != null && headerVersion != session.Version)
            {
                return Error(400, id, McpConstants.InvalidRequest, $"{McpConstants.HeaderProtocolVersion} {headerVersion} does not match the session's {session.Version}", method);
            }

            var label = $"{method} [{session.Version}]";
            switch (method)
            {
                case McpConstants.MethodPing:
                    return McpReply.Complete(200, Envelope(id, new JObject()), label);
                case McpConstants.MethodToolsList:
                    return McpReply.Complete(200, Envelope(id, this.ToolsList(modern: false)), label);
                case McpConstants.MethodToolsCall:
                    return this.CallTool(id, parameters, session, clientGone);
                default:
                    return Error(200, id, McpConstants.MethodNotFound, $"Method not found: {method}", label);
            }
        }

        private McpReply Initialize(JToken id, JObject parameters)
        {
            var requested = parameters["protocolVersion"]?.Type == JTokenType.String ? (string?)parameters["protocolVersion"] : null;
            var negotiated = McpConstants.LegacyVersions.Contains(requested) ? requested! : McpConstants.LegacyVersions[0];

            this.ExpireIdleSessions();
            var sessionId = Guid.NewGuid().ToString("N");
            this.sessions[sessionId] = new LegacySession(negotiated, this.clock.UtcNow);

            var result = new JObject
            {
                ["protocolVersion"] = negotiated,
                ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
                ["serverInfo"] = this.ServerInfo(),
                ["instructions"] = McpConstants.Instructions,
            };

            var reply = McpReply.Complete(200, Envelope(id, result), $"initialize (requested {requested ?? "none"}, negotiated {negotiated})");
            reply.Headers[McpConstants.HeaderSessionId] = sessionId;
            return reply;
        }

        private McpReply EndSession(string sessionId)
        {
            if (!this.sessions.TryRemove(sessionId, out var session))
            {
                return McpReply.Complete(404, null, "DELETE unknown session");
            }

            session.CancelAll();
            return McpReply.Complete(200, null, "DELETE session");
        }

        private void Notify(string method, JObject parameters, string? sessionId)
        {
            // In 2026-07-28 a client cancels by closing the request; in a session it may also send notifications/cancelled.
            if (method != McpConstants.MethodCancelled || sessionId == null || parameters["requestId"] == null)
            {
                return;
            }

            if (this.TryGetSession(sessionId, out var session))
            {
                session.Cancel(RequestKey(parameters["requestId"]!));
            }
        }

        private bool TryGetSession(string sessionId, out LegacySession session)
        {
            if (this.sessions.TryGetValue(sessionId, out session!))
            {
                var now = this.clock.UtcNow;
                if (now - session.LastUsed <= SessionIdleLimit)
                {
                    session.LastUsed = now;
                    return true;
                }

                this.sessions.TryRemove(sessionId, out _);
                session.CancelAll();
            }

            return false;
        }

        private void ExpireIdleSessions()
        {
            var cutoff = this.clock.UtcNow - SessionIdleLimit;
            foreach (var stale in this.sessions.Where(s => s.Value.LastUsed < cutoff).Select(s => s.Key).ToList())
            {
                if (this.sessions.TryRemove(stale, out var session))
                {
                    session.CancelAll();
                }
            }
        }

        private JObject ServerInfo() => new JObject { ["name"] = McpConstants.ServerName, ["version"] = this.serverVersion };

        private JObject Discover() => new JObject
        {
            ["resultType"] = "complete",
            ["supportedVersions"] = new JArray(McpConstants.ProtocolVersion),
            ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
            ["instructions"] = McpConstants.Instructions,
            ["ttlMs"] = McpConstants.ListTtlMs,
            ["cacheScope"] = "private",
        };

        private JObject ToolsList(bool modern)
        {
            var result = new JObject();
            if (modern)
            {
                result["resultType"] = "complete";
            }

            result["tools"] = this.tools.ListTools();
            if (modern)
            {
                result["ttlMs"] = McpConstants.ListTtlMs;
                result["cacheScope"] = "private";
            }

            return result;
        }

        private McpReply CallTool(JToken id, JObject parameters, LegacySession? session, CancellationToken clientGone)
        {
            var name = parameters["name"]?.Type == JTokenType.String ? (string?)parameters["name"] : null;
            if (name == null || !this.tools.HasTool(name))
            {
                return Error(200, id, McpConstants.InvalidParams, $"Unknown tool: {name}", $"tools/call {name}");
            }

            var arguments = parameters["arguments"];
            var call = new McpToolCall(name, arguments == null || arguments.Type == JTokenType.Null ? new JObject() : arguments as JObject, this.clock.UtcNow);
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(clientGone);
            var key = RequestKey(id);
            session?.Track(key, cancellation);

            var label = session == null ? $"tools/call {name}" : $"tools/call {name} [{session.Version}]";
            return McpReply.Deferred(this.RunToolAsync(id, call, session, key, cancellation), label);
        }

        private async Task<JObject> RunToolAsync(JToken id, McpToolCall call, LegacySession? session, string key, CancellationTokenSource cancellation)
        {
            try
            {
                var outcome = await this.tools.CallAsync(call, cancellation.Token).ConfigureAwait(false);
                var result = outcome.ToMcpResult(modern: session == null);
                return session == null ? this.ModernEnvelope(id, result) : Envelope(id, result);
            }
            catch (Exception ex)
            {
                return ErrorBody(id, McpConstants.InternalError, $"Internal error: {ex.GetType().Name}");
            }
            finally
            {
                session?.Untrack(key, cancellation);
                cancellation.Dispose();
            }
        }

        private JObject ModernEnvelope(JToken id, JObject result)
        {
            result["_meta"] = new JObject { [McpConstants.MetaServerInfo] = this.ServerInfo() };
            return Envelope(id, result);
        }

        /// <summary>A handshake-era session: the agreed revision, a last-used time and its pending calls.</summary>
        private sealed class LegacySession
        {
            private readonly ConcurrentDictionary<string, CancellationTokenSource> pending = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

            public LegacySession(string version, DateTime now)
            {
                this.Version = version;
                this.LastUsed = now;
            }

            public string Version { get; }

            public DateTime LastUsed { get; set; }

            public void Track(string key, CancellationTokenSource cancellation) => this.pending[key] = cancellation;

            public void Untrack(string key, CancellationTokenSource cancellation) =>
                ((ICollection<KeyValuePair<string, CancellationTokenSource>>)this.pending).Remove(new KeyValuePair<string, CancellationTokenSource>(key, cancellation));

            public void Cancel(string key)
            {
                if (this.pending.TryGetValue(key, out var cancellation))
                {
                    SafeCancel(cancellation);
                }
            }

            public void CancelAll()
            {
                foreach (var cancellation in this.pending.Values)
                {
                    SafeCancel(cancellation);
                }
            }

            private static void SafeCancel(CancellationTokenSource cancellation)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The call ended between the lookup and the cancel.
                }
            }
        }
    }
}
