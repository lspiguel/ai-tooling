// <copyright file="McpHttpServer.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Mcp
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// MCP Streamable HTTP on <see cref="HttpListener"/>, bound to <c>127.0.0.1</c> only (SPEC §5).
    /// Replies are single <c>application/json</c> bodies. A reply not ready within the keep-alive interval is
    /// committed as a chunked body and a space is written each interval until the JSON follows; a failed write
    /// means the client left, and the call is cancelled (D9).
    /// </summary>
    internal sealed class McpHttpServer : IMcpServer
    {
        private static readonly byte[] KeepAliveBytes = { (byte)' ' };
        private static readonly TimeSpan DrainLimit = TimeSpan.FromSeconds(2);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly HttpListener listener = new HttpListener();
        private readonly McpProtocol protocol;
        private readonly Action<string> diagnostics;
        private readonly TimeSpan keepAliveInterval;
        private int inFlight;

        /// <summary>Initializes a new instance of the <see cref="McpHttpServer"/> class.</summary>
        /// <param name="port">The loopback port.</param>
        /// <param name="protocol">The protocol logic.</param>
        /// <param name="diagnostics">Receives transport diagnostics (disconnects, failed writes).</param>
        /// <param name="keepAliveInterval">How long a reply may take before keep-alive starts, and the interval between spaces. Default 2 seconds.</param>
        public McpHttpServer(int port, McpProtocol protocol, Action<string> diagnostics, TimeSpan? keepAliveInterval = null)
        {
            this.Endpoint = $"http://127.0.0.1:{port}/mcp/";
            this.protocol = protocol;
            this.diagnostics = diagnostics;
            this.keepAliveInterval = keepAliveInterval ?? TimeSpan.FromSeconds(2);
            this.listener.Prefixes.Add(this.Endpoint);
        }

        /// <inheritdoc/>
        public string Endpoint { get; }

        /// <inheritdoc/>
        public void Start()
        {
            this.listener.Start();
            _ = Task.Run(this.AcceptLoopAsync);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (!this.listener.IsListening)
            {
                return;
            }

            var watch = Stopwatch.StartNew();
            while (Volatile.Read(ref this.inFlight) > 0 && watch.Elapsed < DrainLimit)
            {
                Thread.Sleep(20);
            }

            this.listener.Close();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.listener.IsListening)
            {
                this.listener.Close();
            }
        }

        private static bool IsDisconnect(Exception ex) => ex is HttpListenerException || ex is ObjectDisposedException || ex is IOException || ex is InvalidOperationException;

        private static void Cancel(CancellationTokenSource source)
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already finished.
            }
        }

        private static async Task WriteCompleteAsync(HttpListenerResponse response, McpReply reply, string? json)
        {
            response.StatusCode = reply.Status;
            var text = json ?? reply.PlainText;
            if (text != null)
            {
                var bytes = Utf8.GetBytes(text);
                response.ContentType = json != null ? "application/json; charset=utf-8" : "text/plain; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }

            response.Close();
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
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(() => this.HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            Interlocked.Increment(ref this.inFlight);
            try
            {
                using (var clientGone = new CancellationTokenSource())
                {
                    McpReply reply;
                    try
                    {
                        var body = string.Empty;
                        if (context.Request.HasEntityBody)
                        {
                            using (var reader = new StreamReader(context.Request.InputStream, Utf8))
                            {
                                body = await reader.ReadToEndAsync().ConfigureAwait(false);
                            }
                        }

                        reply = this.protocol.Process(new McpRequest(context.Request.HttpMethod, context.Request.Headers, body), clientGone.Token);
                    }
                    catch (Exception ex)
                    {
                        this.diagnostics($"Unhandled error: {ex.GetType().Name}: {ex.Message}");
                        reply = McpReply.Complete(500, null, "internal error");
                    }

                    await this.WriteAsync(context.Response, reply, clientGone).ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Decrement(ref this.inFlight);
            }
        }

        private async Task WriteAsync(HttpListenerResponse response, McpReply reply, CancellationTokenSource clientGone)
        {
            try
            {
                foreach (var header in reply.Headers)
                {
                    response.Headers[header.Key] = header.Value;
                }

                if (reply.Pending == null)
                {
                    await WriteCompleteAsync(response, reply, reply.Body?.ToString(Formatting.None)).ConfigureAwait(false);
                    return;
                }

                if (await Task.WhenAny(reply.Pending, Task.Delay(this.keepAliveInterval)).ConfigureAwait(false) == reply.Pending)
                {
                    await WriteCompleteAsync(response, reply, reply.Pending.Result.ToString(Formatting.None)).ConfigureAwait(false);
                    return;
                }

                await this.WriteWithKeepAliveAsync(response, reply, clientGone).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsDisconnect(ex))
            {
                this.diagnostics($"{reply.Label}: the client went away before the reply was written ({ex.GetType().Name})");
                Cancel(clientGone);
            }
        }

        private async Task WriteWithKeepAliveAsync(HttpListenerResponse response, McpReply reply, CancellationTokenSource clientGone)
        {
            var pending = reply.Pending!;
            response.StatusCode = 200;
            response.ContentType = "application/json; charset=utf-8";
            response.SendChunked = true;
            var output = response.OutputStream;

            do
            {
                try
                {
                    await output.WriteAsync(KeepAliveBytes, 0, KeepAliveBytes.Length).ConfigureAwait(false);
                    await output.FlushAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (IsDisconnect(ex))
                {
                    this.diagnostics($"{reply.Label}: client closed the request; cancelling the call");
                    Cancel(clientGone);
                    await pending.ConfigureAwait(false);
                    response.Abort();
                    return;
                }
            }
            while (await Task.WhenAny(pending, Task.Delay(this.keepAliveInterval)).ConfigureAwait(false) != pending);

            var bytes = Utf8.GetBytes(pending.Result.ToString(Formatting.None));
            await output.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            response.Close();
        }
    }
}
