// <copyright file="Program.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.ConsoleHarness
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Console host for the plugin's MCP layer. Usage: <c>D365AgentCompanionMcp.ConsoleHarness.exe [port] [token]</c>.
    /// Without a token it generates one and prints it. Runs until Ctrl+C.
    /// </summary>
    internal static class Program
    {
        private const int DefaultPort = 47321;

        private static int Main(string[] args)
        {
            var port = args.Length > 0 ? int.Parse(args[0]) : DefaultPort;
            var token = args.Length > 1 ? args[1] : Guid.NewGuid().ToString("N");

            var protocol = new McpProtocol(new FakeTools(), () => token, "0.0.0-harness", SystemClock.Instance);
            using (var stop = new ManualResetEventSlim())
            using (var server = new McpHttpServer(port, protocol, line => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {line}")))
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    stop.Set();
                };

                server.Start();
                Console.WriteLine($"Endpoint: {server.Endpoint}");
                Console.WriteLine($"Token:    {token}");
                Console.WriteLine("Press Ctrl+C to stop.");
                stop.Wait();
                server.Stop();
            }

            return 0;
        }

        /// <summary>Tools that exercise the protocol without Dataverse.</summary>
        private sealed class FakeTools : IToolHost
        {
            private static readonly string[] Names = { "hello", "wait", "big" };

            public JArray ListTools() => new JArray(
                Tool("hello", "Returns process and time details. No arguments.", new JObject()),
                Tool("wait", "Waits the given number of seconds before answering, to imitate a pending approval. Cancelled when the client goes away.", new JObject
                {
                    ["seconds"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 600 },
                }),
                Tool("big", "Returns a text field of the given number of characters, to test client output limits.", new JObject
                {
                    ["characters"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 1000000 },
                }));

            public bool HasTool(string name) => Array.IndexOf(Names, name) >= 0;

            public async Task<ToolResult> CallAsync(McpToolCall call, CancellationToken cancellation)
            {
                switch (call.Name)
                {
                    case "hello":
                        return ToolResult.Ok(new JObject
                        {
                            ["processId"] = Process.GetCurrentProcess().Id,
                            ["utcNow"] = ValueFormats.FormatDateTime(DateTime.UtcNow),
                            ["runtime"] = Environment.Version.ToString(),
                        });
                    case "wait":
                        var seconds = (int?)call.Arguments?["seconds"] ?? 0;
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(seconds), cancellation).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine($"{DateTime.Now:HH:mm:ss} wait cancelled: the client went away");
                            return ToolResult.Fail("cancelled_by_client", "The client closed the request before the call ran.");
                        }

                        return ToolResult.Ok(new JObject { ["waitedSeconds"] = seconds });
                    default:
                        var characters = (int?)call.Arguments?["characters"] ?? 0;
                        return ToolResult.Ok(new JObject { ["characters"] = characters, ["text"] = new string('x', characters) });
                }
            }

            private static JObject Tool(string name, string description, JObject properties) => new JObject
            {
                ["name"] = name,
                ["title"] = name,
                ["description"] = description,
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["additionalProperties"] = false,
                },
                ["outputSchema"] = new JObject { ["type"] = "object" },
            };
        }
    }
}
