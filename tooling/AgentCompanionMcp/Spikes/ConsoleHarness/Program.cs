// <copyright file="Program.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Console host for the MCP layer. Usage: <c>AgentCompanionMcp.ConsoleHarness.exe [port] [token]</c>.
    /// Without a token it generates one and prints it. Runs until Ctrl+C, so it also works without a console.
    /// </summary>
    internal static class Program
    {
        private const int DefaultPort = 47321;

        private static int Main(string[] args)
        {
            var port = args.Length > 0 ? int.Parse(args[0]) : DefaultPort;
            var token = args.Length > 1 ? args[1] : Guid.NewGuid().ToString("N");

            using (var stop = new ManualResetEventSlim())
            using (var server = new McpHttpServer(port, token, new FakeTools(), "0.0.1-harness", line => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {line}")))
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
            }

            return 0;
        }

        /// <summary>Tools that exercise the protocol without Dataverse.</summary>
        private sealed class FakeTools : IToolProvider
        {
            private static readonly string[] Names = { "hello", "wait", "big" };

            public JArray ListTools() => new JArray(
                Tool("hello", "Returns process and time details. No arguments.", new JObject()),
                Tool("wait", "Waits the given number of seconds before answering, to imitate a pending approval.", new JObject
                {
                    ["seconds"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 600 },
                }, "seconds"),
                Tool("big", "Returns a text field of the given number of characters, to test client output limits.", new JObject
                {
                    ["characters"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 1000000 },
                }, "characters"));

            public bool HasTool(string name) => Array.IndexOf(Names, name) >= 0;

            public async Task<ToolResult> CallAsync(string name, JObject arguments, CancellationToken cancellationToken)
            {
                switch (name)
                {
                    case "hello":
                        return ToolResult.Ok(new JObject
                        {
                            ["processId"] = Process.GetCurrentProcess().Id,
                            ["utcNow"] = DateTime.UtcNow,
                            ["runtime"] = Environment.Version.ToString(),
                        });
                    case "wait":
                        var seconds = (int?)arguments["seconds"] ?? 0;
                        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
                        return ToolResult.Ok(new JObject { ["waitedSeconds"] = seconds });
                    default:
                        var characters = (int?)arguments["characters"] ?? 0;
                        return ToolResult.Ok(new JObject { ["characters"] = characters, ["text"] = new string('x', characters) });
                }
            }

            private static JObject Tool(string name, string description, JObject properties, params string[] required)
            {
                var output = new JObject { ["type"] = "object" };
                return new JObject
                {
                    ["name"] = name,
                    ["title"] = name,
                    ["description"] = description,
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = new JArray(required),
                        ["additionalProperties"] = false,
                    },
                    ["outputSchema"] = output,
                };
            }
        }
    }
}
