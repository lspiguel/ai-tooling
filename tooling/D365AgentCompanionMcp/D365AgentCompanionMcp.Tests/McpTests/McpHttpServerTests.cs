using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.McpTests
{
    /// <summary>
    /// The transport over a real <see cref="HttpListener"/> on a free loopback port: fast replies are plain bodies,
    /// slow replies are kept alive with leading spaces and still parse as one JSON document, and a client that goes
    /// away cancels the call (SPEC §6.1, D9).
    /// </summary>
    [TestFixture]
    public class McpHttpServerTests
    {
        private const string Token = "transport-token";
        private static readonly TimeSpan KeepAlive = TimeSpan.FromMilliseconds(100);

        private TransportTools _tools = null!;
        private McpHttpServer _server = null!;
        private HttpClient _client = null!;
        private int _port;

        [SetUp]
        public void SetUp()
        {
            _port = FreePort();
            _tools = new TransportTools();
            _server = new McpHttpServer(_port, new McpProtocol(_tools, () => Token, "1.0.0.0", SystemClock.Instance), _ => { }, KeepAlive);
            _server.Start();
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        }

        [TearDown]
        public void TearDown()
        {
            _tools.Release.TrySetResult(true);
            _client.Dispose();
            _server.Stop();
            _server.Dispose();
        }

        [Test]
        public async Task FastReply_IsAPlainJsonBodyWithContentLength()
        {
            using var response = await _client.SendAsync(CallRequest("hello"));
            var text = await response.Content.ReadAsStringAsync();

            Assert.That((int)response.StatusCode, Is.EqualTo(200));
            Assert.That(response.Headers.TransferEncodingChunked, Is.Not.True);
            Assert.That(text, Does.StartWith("{"));
            Assert.That(JObject.Parse(text)["result"]!["structuredContent"]!["greeting"]!.ToString(), Is.EqualTo("hi"));
        }

        [Test]
        public async Task SlowReply_IsChunkedWithLeadingSpacesAndStillOneJsonDocument()
        {
            var send = _client.SendAsync(CallRequest("slow"));
            await Task.Delay(TimeSpan.FromMilliseconds(450));
            _tools.Release.SetResult(true);

            using var response = await send;
            var text = await response.Content.ReadAsStringAsync();

            Assert.That((int)response.StatusCode, Is.EqualTo(200));
            Assert.That(response.Headers.TransferEncodingChunked, Is.True);
            Assert.That(text, Does.StartWith(" "), "keep-alive spaces come first");
            Assert.That(JObject.Parse(text)["result"]!["isError"]!.Value<bool>(), Is.False);
        }

        [Test]
        public async Task ClientDisconnectsWhileWaiting_CallIsCancelled()
        {
            using (var socket = new TcpClient())
            {
                await socket.ConnectAsync(IPAddress.Loopback, _port);
                var stream = socket.GetStream();
                var body = Encoding.UTF8.GetBytes(CallBody("slow"));
                var head = $"POST /mcp HTTP/1.1\r\nHost: 127.0.0.1:{_port}\r\nAuthorization: Bearer {Token}\r\nContent-Type: application/json\r\n"
                    + $"MCP-Protocol-Version: 2026-07-28\r\nMcp-Method: tools/call\r\nMcp-Name: slow\r\nContent-Length: {body.Length}\r\n\r\n";
                var headBytes = Encoding.ASCII.GetBytes(head);
                await stream.WriteAsync(headBytes, 0, headBytes.Length);
                await stream.WriteAsync(body, 0, body.Length);

                // Wait for the committed headers and a keep-alive space, then walk away.
                var buffer = new byte[256];
                await stream.ReadAsync(buffer, 0, buffer.Length);
            }

            var cancelled = await Task.WhenAny(_tools.Cancelled.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.That(cancelled, Is.SameAs(_tools.Cancelled.Task), "the call was not cancelled after the client left");
        }

        [Test]
        public async Task MissingToken_Returns401WithPlainTextBody()
        {
            var request = CallRequest("hello");
            request.Headers.Remove("Authorization");
            using var response = await _client.SendAsync(request);

            Assert.That((int)response.StatusCode, Is.EqualTo(401));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Copy the token from the plugin panel"));
        }

        [Test]
        public async Task WellKnownOAuthPath_Returns404()
        {
            using var response = await _client.GetAsync($"http://127.0.0.1:{_port}/.well-known/oauth-protected-resource/mcp");

            Assert.That((int)response.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public void SecondServerOnSamePort_FailsToStart()
        {
            using var second = new McpHttpServer(_port, new McpProtocol(_tools, () => Token, "1.0.0.0", SystemClock.Instance), _ => { });

            Assert.That(() => second.Start(), Throws.InstanceOf<HttpListenerException>());
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string CallBody(string tool) => new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JObject
            {
                ["name"] = tool,
                ["arguments"] = new JObject(),
                ["_meta"] = new JObject { ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28" },
            },
        }.ToString(Newtonsoft.Json.Formatting.None);

        private HttpRequestMessage CallRequest(string tool)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_port}/mcp")
            {
                Content = new StringContent(CallBody(tool), Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Token}");
            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");
            request.Headers.TryAddWithoutValidation("Mcp-Method", "tools/call");
            request.Headers.TryAddWithoutValidation("Mcp-Name", tool);
            return request;
        }

        private sealed class TransportTools : IToolHost
        {
            public TaskCompletionSource<bool> Release { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> Cancelled { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public JArray ListTools() => new JArray();

            public bool HasTool(string name) => name == "hello" || name == "slow";

            public async Task<ToolResult> CallAsync(McpToolCall call, CancellationToken cancellation)
            {
                if (call.Name == "hello")
                {
                    return ToolResult.Ok(new JObject { ["greeting"] = "hi" });
                }

                using (cancellation.Register(() => Cancelled.TrySetResult(true)))
                {
                    await Task.WhenAny(Release.Task, Cancelled.Task);
                }

                return Cancelled.Task.IsCompleted ? ToolResult.Fail("cancelled_by_client", "gone") : ToolResult.Ok(new JObject());
            }
        }
    }
}
