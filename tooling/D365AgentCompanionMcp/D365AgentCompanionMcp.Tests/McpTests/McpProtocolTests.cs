using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.McpTests
{
    /// <summary>
    /// The thirty-one checks of the spike's Invoke-ProtocolChecks.ps1 (SPEC §6.1, §6.2), run against the protocol
    /// layer without a socket, plus session expiry and handshake-era cancellation.
    /// </summary>
    [TestFixture]
    public class McpProtocolTests
    {
        private const string Token = "test-token";
        private const string Modern = "2026-07-28";

        private FakeClock _clock = null!;
        private ProtocolTools _tools = null!;
        private McpProtocol _protocol = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _tools = new ProtocolTools();
            _protocol = new McpProtocol(_tools, () => Token, "1.0.0.0", _clock);
        }

        // ---- 2026-07-28 ----

        [Test]
        public async Task Discover_Returns200WithVersionAndServerInfo()
        {
            var (status, body) = await SendAsync(ModernBody("server/discover"), ModernHeaders("server/discover"));

            Assert.That(status, Is.EqualTo(200));
            Assert.That(body!["result"]!["supportedVersions"]![0]!.ToString(), Is.EqualTo(Modern));
            Assert.That(body["result"]!["resultType"]!.ToString(), Is.EqualTo("complete"));
            Assert.That(body["result"]!["cacheScope"]!.ToString(), Is.EqualTo("private"));
            Assert.That(body["result"]!["_meta"]!["io.modelcontextprotocol/serverInfo"]!["name"]!.ToString(), Is.EqualTo("d365-agent-companion-mcp"));
        }

        [Test]
        public async Task ToolsList_Returns200WithTools()
        {
            var (status, body) = await SendAsync(ModernBody("tools/list"), ModernHeaders("tools/list"));

            Assert.That(status, Is.EqualTo(200));
            Assert.That(body!["result"]!["tools"]![0]!["name"]!.ToString(), Is.EqualTo("hello"));
            Assert.That(body["result"]!["ttlMs"], Is.Not.Null);
        }

        [Test]
        public async Task ToolsCall_Returns200WithStructuredAndTextContent()
        {
            var (status, body) = await SendAsync(ModernBody("tools/call", CallParams("hello")), ModernHeaders("tools/call", "hello"));

            Assert.That(status, Is.EqualTo(200));
            var result = body!["result"]!;
            Assert.That(result["resultType"]!.ToString(), Is.EqualTo("complete"));
            Assert.That(result["isError"]!.Value<bool>(), Is.False);
            Assert.That(result["structuredContent"]!["greeting"]!.ToString(), Is.EqualTo("hi"));
            Assert.That(JObject.Parse(result["content"]![0]!["text"]!.ToString())["greeting"]!.ToString(), Is.EqualTo("hi"));
            Assert.That(result["_meta"], Is.Not.Null);
        }

        [Test]
        public async Task ToolsCall_McpNameBase64Sentinel_Accepted()
        {
            var encoded = "=?base64?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("hello")) + "?=";
            var (status, _) = await SendAsync(ModernBody("tools/call", CallParams("hello")), ModernHeaders("tools/call", encoded));

            Assert.That(status, Is.EqualTo(200));
        }

        [Test]
        public async Task ToolsCall_UnknownTool_Returns200InvalidParams()
        {
            var (status, body) = await SendAsync(ModernBody("tools/call", CallParams("nope")), ModernHeaders("tools/call", "nope"));

            Assert.That((status, ErrorCode(body)), Is.EqualTo((200, -32602)));
        }

        [Test]
        public async Task UnknownMethod_Returns404MethodNotFound()
        {
            var (status, body) = await SendAsync(ModernBody("resources/list"), ModernHeaders("resources/list"));

            Assert.That((status, ErrorCode(body)), Is.EqualTo((404, -32601)));
        }

        [Test]
        public async Task RequestWithoutMetaOrSession_Returns400UnsupportedVersion()
        {
            var (status, body) = await SendAsync(LegacyBody("tools/list"), new Dictionary<string, string>());

            Assert.That((status, ErrorCode(body)), Is.EqualTo((400, -32022)));
            Assert.That(body!["error"]!["data"]!["supported"]![0]!.ToString(), Is.EqualTo(Modern));
        }

        [Test]
        public async Task UnsupportedVersionInMeta_Returns400UnsupportedVersion()
        {
            var headers = new Dictionary<string, string> { ["MCP-Protocol-Version"] = "2025-11-25", ["Mcp-Method"] = "tools/list" };
            var (status, body) = await SendAsync(ModernBody("tools/list", version: "2025-11-25"), headers);

            Assert.That((status, ErrorCode(body)), Is.EqualTo((400, -32022)));
            Assert.That(body!["error"]!["data"]!["requested"]!.ToString(), Is.EqualTo("2025-11-25"));
        }

        [Test]
        public async Task MissingMcpMethodHeader_Returns400HeaderMismatch()
        {
            var (status, body) = await SendAsync(ModernBody("tools/list"), new Dictionary<string, string> { ["MCP-Protocol-Version"] = Modern });

            Assert.That((status, ErrorCode(body)), Is.EqualTo((400, -32020)));
        }

        [Test]
        public async Task McpNameNotMatchingBody_Returns400HeaderMismatch()
        {
            var (status, body) = await SendAsync(ModernBody("tools/call", CallParams("hello")), ModernHeaders("tools/call", "slow"));

            Assert.That((status, ErrorCode(body)), Is.EqualTo((400, -32020)));
        }

        [Test]
        public async Task Notification_Returns202WithoutBody()
        {
            var body = ModernBody("notifications/cancelled");
            body.Remove("id");
            var (status, reply) = await SendAsync(body, ModernHeaders("notifications/cancelled"));

            Assert.That(status, Is.EqualTo(202));
            Assert.That(reply, Is.Null);
        }

        [Test]
        public async Task Get_Returns405()
        {
            var (status, _) = await SendAsync(null, new Dictionary<string, string>(), "GET");

            Assert.That(status, Is.EqualTo(405));
        }

        [Test]
        public async Task DeleteWithoutSession_Returns405()
        {
            var (status, _) = await SendAsync(null, new Dictionary<string, string>(), "DELETE");

            Assert.That(status, Is.EqualTo(405));
        }

        [Test]
        public void NoBearerToken_Returns401WithPlainTextExplanation()
        {
            var reply = _protocol.Process(new McpRequest("POST", Headers(ModernHeaders("tools/list"), auth: false), ModernBody("tools/list").ToString()), CancellationToken.None);

            Assert.That(reply.Status, Is.EqualTo(401));
            Assert.That(reply.PlainText, Does.Contain("bearer token missing or not current"));
        }

        [Test]
        public void StaleBearerToken_Returns401()
        {
            var headers = Headers(ModernHeaders("tools/list"), auth: false);
            headers["Authorization"] = "Bearer an-old-token";

            var reply = _protocol.Process(new McpRequest("POST", headers, ModernBody("tools/list").ToString()), CancellationToken.None);

            Assert.That(reply.Status, Is.EqualTo(401));
        }

        [Test]
        public async Task ForeignOrigin_Returns403()
        {
            var headers = ModernHeaders("tools/list");
            headers["Origin"] = "https://example.com";
            var (status, _) = await SendAsync(ModernBody("tools/list"), headers);

            Assert.That(status, Is.EqualTo(403));
        }

        [Test]
        public async Task LocalOrigin_Accepted()
        {
            var headers = ModernHeaders("tools/list");
            headers["Origin"] = "http://localhost:3000";
            var (status, _) = await SendAsync(ModernBody("tools/list"), headers);

            Assert.That(status, Is.EqualTo(200));
        }

        [Test]
        public async Task SessionIdOnModernRequest_Ignored()
        {
            var headers = ModernHeaders("tools/list");
            headers["Mcp-Session-Id"] = "abc";
            var (status, _) = await SendAsync(ModernBody("tools/list"), headers);

            Assert.That(status, Is.EqualTo(200));
        }

        // ---- handshake-era fallback ----

        [Test]
        public async Task Initialize_2025_11_25_AgreedWithSessionIssued()
        {
            var reply = Initialize("2025-11-25");
            var body = await BodyAsync(reply);

            Assert.That(reply.Status, Is.EqualTo(200));
            Assert.That(body!["result"]!["protocolVersion"]!.ToString(), Is.EqualTo("2025-11-25"));
            Assert.That(reply.Headers.ContainsKey("Mcp-Session-Id"), Is.True);
            Assert.That(body["result"]!["serverInfo"]!["name"]!.ToString(), Is.EqualTo("d365-agent-companion-mcp"));
        }

        [Test]
        public async Task Session_InitializedPingListAndCall_AllServed()
        {
            var session = SessionHeaders(Initialize("2025-11-25"), "2025-11-25");

            var initialized = LegacyBody("notifications/initialized");
            initialized.Remove("id");
            Assert.That((await SendAsync(initialized, session)).Status, Is.EqualTo(202));
            Assert.That((await SendAsync(LegacyBody("ping"), session)).Status, Is.EqualTo(200));

            var (listStatus, list) = await SendAsync(LegacyBody("tools/list"), session);
            Assert.That(listStatus, Is.EqualTo(200));
            Assert.That(list!["result"]!["resultType"], Is.Null, "no modern-only fields");
            Assert.That(list["result"]!["ttlMs"], Is.Null, "no modern-only fields");

            var (callStatus, call) = await SendAsync(LegacyBody("tools/call", CallParams("hello")), session);
            Assert.That(callStatus, Is.EqualTo(200));
            Assert.That(call!["result"]!["structuredContent"], Is.Not.Null);
            Assert.That(call["result"]!["resultType"], Is.Null);
            Assert.That(call["result"]!["_meta"], Is.Null);
        }

        [Test]
        public async Task Session_HeaderVersionMismatch_Returns400InvalidRequest()
        {
            var session = SessionHeaders(Initialize("2025-11-25"), "2025-06-18");
            var (status, body) = await SendAsync(LegacyBody("ping"), session);

            Assert.That((status, ErrorCode(body)), Is.EqualTo((400, -32600)));
        }

        [Test]
        public async Task UnknownSession_Returns404SessionNotFound()
        {
            var headers = new Dictionary<string, string> { ["Mcp-Session-Id"] = "nope", ["MCP-Protocol-Version"] = "2025-11-25" };
            var (status, body) = await SendAsync(LegacyBody("ping"), headers);

            Assert.That((status, ErrorCode(body)), Is.EqualTo((404, -32001)));
        }

        [Test]
        public async Task Initialize_OlderRevision_Offers2025_11_25()
        {
            var body = await BodyAsync(Initialize("2025-03-26"));

            Assert.That(body!["result"]!["protocolVersion"]!.ToString(), Is.EqualTo("2025-11-25"));
        }

        [Test]
        public async Task Initialize_2025_06_18_Agreed()
        {
            var body = await BodyAsync(Initialize("2025-06-18"));

            Assert.That(body!["result"]!["protocolVersion"]!.ToString(), Is.EqualTo("2025-06-18"));
        }

        [Test]
        public async Task DeleteSession_Returns200AndEndsSession()
        {
            var session = SessionHeaders(Initialize("2025-11-25"), "2025-11-25");

            Assert.That((await SendAsync(null, session, "DELETE")).Status, Is.EqualTo(200));
            var (status, body) = await SendAsync(LegacyBody("ping"), session);
            Assert.That((status, ErrorCode(body)), Is.EqualTo((404, -32001)));
        }

        [Test]
        public async Task Session_IdleThirtyMinutes_Expires()
        {
            var session = SessionHeaders(Initialize("2025-11-25"), "2025-11-25");
            _clock.Advance(TimeSpan.FromMinutes(29));
            Assert.That((await SendAsync(LegacyBody("ping"), session)).Status, Is.EqualTo(200));

            _clock.Advance(TimeSpan.FromMinutes(31));
            var (status, body) = await SendAsync(LegacyBody("ping"), session);
            Assert.That((status, ErrorCode(body)), Is.EqualTo((404, -32001)));
        }

        // ---- pending calls and cancellation (D9) ----

        [Test]
        public void ToolsCall_ReplyIsPendingWith200Committed()
        {
            var reply = _protocol.Process(new McpRequest("POST", Headers(ModernHeaders("tools/call", "slow")), ModernBody("tools/call", CallParams("slow")).ToString()), CancellationToken.None);

            Assert.That(reply.Status, Is.EqualTo(200));
            Assert.That(reply.Pending, Is.Not.Null);
            Assert.That(reply.Pending!.IsCompleted, Is.False);
            _tools.Release.SetResult(true);
        }

        [Test]
        public async Task ClientGone_CancelsTheToolCall()
        {
            using var gone = new CancellationTokenSource();
            var reply = _protocol.Process(new McpRequest("POST", Headers(ModernHeaders("tools/call", "slow")), ModernBody("tools/call", CallParams("slow")).ToString()), gone.Token);

            gone.Cancel();
            var body = await reply.Pending!;

            Assert.That(_tools.SawCancellation, Is.True);
            Assert.That(body["result"]!["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public async Task NotificationsCancelled_InSession_CancelsPendingCall()
        {
            var session = SessionHeaders(Initialize("2025-11-25"), "2025-11-25");
            var call = LegacyBody("tools/call", CallParams("slow"));
            call["id"] = 7;
            var reply = _protocol.Process(new McpRequest("POST", Headers(session), call.ToString()), CancellationToken.None);

            var cancel = LegacyBody("notifications/cancelled", new JObject { ["requestId"] = 7, ["reason"] = "user pressed Escape" });
            cancel.Remove("id");
            Assert.That((await SendAsync(cancel, session)).Status, Is.EqualTo(202));

            await reply.Pending!;
            Assert.That(_tools.SawCancellation, Is.True);
        }

        [Test]
        public async Task ToolHostThrows_ReturnsInternalErrorNotFault()
        {
            var (status, body) = await SendAsync(ModernBody("tools/call", CallParams("boom")), ModernHeaders("tools/call", "boom"));

            Assert.That((status, ErrorCode(body)), Is.EqualTo((200, -32603)));
        }

        [Test]
        public async Task MalformedJson_Returns400ParseError()
        {
            var reply = _protocol.Process(new McpRequest("POST", Headers(ModernHeaders("tools/list")), "{not json"), CancellationToken.None);
            var body = await BodyAsync(reply);

            Assert.That((reply.Status, ErrorCode(body)), Is.EqualTo((400, -32700)));
        }

        // ---- helpers ----

        private static JObject CallParams(string name) => new JObject { ["name"] = name, ["arguments"] = new JObject() };

        private static JObject ModernBody(string method, JObject? parameters = null, string version = Modern)
        {
            var p = parameters ?? new JObject();
            p["_meta"] = new JObject { ["io.modelcontextprotocol/protocolVersion"] = version };
            return new JObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = p };
        }

        private static JObject LegacyBody(string method, JObject? parameters = null) =>
            new JObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = parameters ?? new JObject() };

        private static Dictionary<string, string> ModernHeaders(string method, string? name = null)
        {
            var headers = new Dictionary<string, string> { ["MCP-Protocol-Version"] = Modern, ["Mcp-Method"] = method };
            if (name != null)
            {
                headers["Mcp-Name"] = name;
            }

            return headers;
        }

        private static WebHeaderCollection Headers(Dictionary<string, string> values, bool auth = true)
        {
            var headers = new WebHeaderCollection();
            if (auth)
            {
                headers["Authorization"] = $"Bearer {Token}";
            }

            foreach (var pair in values)
            {
                headers[pair.Key] = pair.Value;
            }

            return headers;
        }

        private static int? ErrorCode(JObject? body) => (int?)body?["error"]?["code"];

        private static async Task<JObject?> BodyAsync(McpReply reply) => reply.Pending != null ? await reply.Pending : reply.Body;

        private static Dictionary<string, string> SessionHeaders(McpReply initialize, string version) =>
            new Dictionary<string, string> { ["Mcp-Session-Id"] = initialize.Headers["Mcp-Session-Id"], ["MCP-Protocol-Version"] = version };

        private McpReply Initialize(string version)
        {
            var body = LegacyBody("initialize", new JObject
            {
                ["protocolVersion"] = version,
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "protocol-checks", ["version"] = "0" },
            });
            return _protocol.Process(new McpRequest("POST", Headers(new Dictionary<string, string>()), body.ToString()), CancellationToken.None);
        }

        private async Task<(int Status, JObject? Body)> SendAsync(JObject? body, Dictionary<string, string> headers, string method = "POST")
        {
            var reply = _protocol.Process(new McpRequest(method, Headers(headers), body?.ToString() ?? string.Empty), CancellationToken.None);
            return (reply.Status, await BodyAsync(reply));
        }

        /// <summary>hello answers at once; slow waits for Release or cancellation; boom throws.</summary>
        private sealed class ProtocolTools : IToolHost
        {
            public TaskCompletionSource<bool> Release { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool SawCancellation { get; private set; }

            public JArray ListTools() => new JArray(
                new JObject { ["name"] = "hello", ["inputSchema"] = new JObject { ["type"] = "object" } },
                new JObject { ["name"] = "slow", ["inputSchema"] = new JObject { ["type"] = "object" } },
                new JObject { ["name"] = "boom", ["inputSchema"] = new JObject { ["type"] = "object" } });

            public bool HasTool(string name) => name == "hello" || name == "slow" || name == "boom";

            public async Task<ToolResult> CallAsync(McpToolCall call, CancellationToken cancellation)
            {
                switch (call.Name)
                {
                    case "hello":
                        return ToolResult.Ok(new JObject { ["greeting"] = "hi" });
                    case "boom":
                        throw new InvalidOperationException("boom");
                    default:
                        var cancelled = new TaskCompletionSource<bool>();
                        using (cancellation.Register(() => cancelled.TrySetResult(true)))
                        {
                            if (await Task.WhenAny(Release.Task, cancelled.Task) == cancelled.Task)
                            {
                                SawCancellation = true;
                                return ToolResult.Fail("cancelled_by_client", "gone");
                            }
                        }

                        return ToolResult.Ok(new JObject());
                }
            }
        }
    }
}
