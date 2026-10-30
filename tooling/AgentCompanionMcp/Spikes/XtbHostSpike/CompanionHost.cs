// <copyright file="CompanionHost.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.AgentCompanionMcp.Spikes
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Threading;
    using Lspiguel.Xrm.AgentCompanionMcp.Spikes.Mcp;

    /// <summary>
    /// Process-wide singleton host (spec H1–H3): started by the first tab, shared by every later tab,
    /// stopped when the last tab closes.
    /// </summary>
    internal static class CompanionHost
    {
        public const int Port = 47321;

        private const int LogCapacity = 500;

        private static readonly object Gate = new object();
        private static readonly Queue<string> RecentLines = new Queue<string>();
        private static McpHttpServer server;
        private static int instances;
        private static int tabCounter;

        /// <summary>Raised for every host-level log line; tabs show it.</summary>
        public static event Action<string> HostLog;

        /// <summary>When this static class was initialized — one value per process if the singleton holds.</summary>
        public static DateTime LoadedUtc { get; } = DateTime.UtcNow;

        public static ConnectionRegistry Registry { get; } = new ConnectionRegistry();

        public static string Token { get; private set; }

        public static string Endpoint => server?.Endpoint;

        public static string StartError { get; private set; }

        public static int Instances
        {
            get
            {
                lock (Gate)
                {
                    return instances;
                }
            }
        }

        public static void Attach()
        {
            lock (Gate)
            {
                instances++;
                if (server != null)
                {
                    Write($"Tab attached to the running host ({instances} tabs)");
                    return;
                }

                Token = NewToken();
                var candidate = new McpHttpServer(Port, Token, new SpikeTools(), typeof(CompanionHost).Assembly.GetName().Version.ToString(), Write);
                try
                {
                    candidate.Start();
                    server = candidate;
                    StartError = null;
                    Write($"Host started at {server.Endpoint} by the first tab");
                }
                catch (Exception ex)
                {
                    candidate.Dispose();
                    StartError = ex.Message;
                    Write($"Host failed to start: {ex.Message}");
                }
            }
        }

        public static void Detach()
        {
            lock (Gate)
            {
                instances--;
                if (instances > 0 || server == null)
                {
                    Write($"Tab detached ({instances} tabs remain)");
                    return;
                }

                server.Dispose();
                server = null;
                Write("Last tab closed: host stopped");
            }
        }

        /// <summary>A short label for a new tab, so log lines from several tabs can be told apart.</summary>
        public static string NextTabLabel() => $"tab{Interlocked.Increment(ref tabCounter)}";

        /// <summary>Keeps a line from any tab or the host for the spike's <c>get_log</c> tool.</summary>
        public static void Record(string line)
        {
            lock (RecentLines)
            {
                RecentLines.Enqueue($"{DateTime.Now:HH:mm:ss} {line}");
                while (RecentLines.Count > LogCapacity)
                {
                    RecentLines.Dequeue();
                }
            }
        }

        public static string[] RecentLog()
        {
            lock (RecentLines)
            {
                return RecentLines.ToArray();
            }
        }

        private static void Write(string line)
        {
            Record($"[host] {line}");
            HostLog?.Invoke(line);
        }

        private static string NewToken()
        {
            var bytes = new byte[24];
            using (var random = new RNGCryptoServiceProvider())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
