using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
using Lspiguel.Xrm.D365AgentCompanionMcp.Mcp;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using Microsoft.Xrm.Sdk;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes
{
    /// <summary>A clock the test moves by hand.</summary>
    internal sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan by) => UtcNow += by;
    }

    /// <summary>Keeps settings in memory, as XrmToolBox's XML serializer would — a copy per save.</summary>
    internal sealed class FakeSettingsStore : ISettingsStore
    {
        public HostSettings? Saved { get; set; }

        public int SaveCount { get; private set; }

        public HostSettings? Load() => Saved == null ? null : Copy(Saved);

        public void Save(HostSettings settings)
        {
            Saved = Copy(settings);
            SaveCount++;
        }

        private static HostSettings Copy(HostSettings settings)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(HostSettings));
            using var stream = new System.IO.MemoryStream();
            serializer.Serialize(stream, settings);
            stream.Position = 0;
            return (HostSettings)serializer.Deserialize(stream);
        }
    }

    /// <summary>Reversible, recognisable "protection" so tests can see what was stored.</summary>
    internal sealed class FakeTokenProtector : ITokenProtector
    {
        private const string Prefix = "protected:";

        public string Protect(string token) => Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(token));

        public string Unprotect(string protectedToken)
        {
            if (!protectedToken.StartsWith(Prefix, StringComparison.Ordinal))
            {
                throw new CryptographicException("Not protected by this user.");
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedToken.Substring(Prefix.Length)));
        }
    }

    /// <summary>A listener that never opens a socket. Ports listed in <see cref="Taken"/> fail to start.</summary>
    internal sealed class FakeServer : IMcpServer
    {
        public static readonly HashSet<int> Taken = new HashSet<int>();

        public FakeServer(int port, McpProtocol protocol)
        {
            Port = port;
            Protocol = protocol;
            Endpoint = $"http://127.0.0.1:{port}/mcp/";
        }

        public int Port { get; }

        public McpProtocol Protocol { get; }

        public string Endpoint { get; }

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public void Start()
        {
            if (Taken.Contains(Port))
            {
                throw new System.Net.HttpListenerException(183, "Cannot create a file when that file already exists");
            }

            Started = true;
        }

        public void Stop() => Stopped = true;

        public void Dispose()
        {
        }
    }

    /// <summary>A tab whose approval dialog is a delegate.</summary>
    internal sealed class FakeInstance : IHostInstance
    {
        public FakeInstance(string label = "tab test")
        {
            TabLabel = label;
        }

        public string TabLabel { get; }

        public CallLog CallLog { get; } = new CallLog();

        public List<ApprovalRequest> Requests { get; } = new List<ApprovalRequest>();

        /// <summary>Answers each dialog; by default allows once.</summary>
        public Func<ApprovalRequest, CancellationToken, Task<ApprovalDecision>> Answer { get; set; } =
            (request, cancellation) => Task.FromResult(ApprovalDecision.AllowOnce);

        public Task<ApprovalDecision> PromptAsync(ApprovalRequest request, CancellationToken cancellation)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            return Answer(request, cancellation);
        }

        /// <summary>A dialog nobody answers: it ends only when withdrawn.</summary>
        public static Task<ApprovalDecision> NeverAnswer(ApprovalRequest request, CancellationToken cancellation)
        {
            var completion = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation.Register(() => completion.TrySetCanceled());
            return completion.Task;
        }
    }

    /// <summary>Builds hosts with fakes, and binds fake connections the way a tab does.</summary>
    internal static class TestHost
    {
        public static CompanionHost Create(FakeClock? clock = null, FakeSettingsStore? store = null, Func<CompanionHost, IReadOnlyList<ToolDefinition>>? tools = null)
        {
            return new CompanionHost(
                new HostSettingsStore(store ?? new FakeSettingsStore()),
                new FakeTokenProtector(),
                clock ?? new FakeClock(),
                (port, protocol) => new FakeServer(port, protocol),
                "1.2026.10.1",
                tools);
        }

        public static BoundConnection Bind(CompanionHost host, IHostInstance owner, Guid organizationId, string connectionId, IOrganizationService service, string name = "Dev A")
        {
            var binding = new BoundConnection(owner, connectionId, false, name, $"org{name.Replace(" ", string.Empty).ToLowerInvariant()}", $"{name} org", $"https://{name.Replace(" ", string.Empty).ToLowerInvariant()}.example.invalid");
            if (host.Registry.Claim(binding) != null)
            {
                throw new InvalidOperationException("Claim conflict in test setup.");
            }

            var outcome = host.Registry.Complete(binding, organizationId, Guid.NewGuid(), false, service, DateTime.UtcNow, out _);
            if (outcome != BindOutcome.Bound)
            {
                throw new InvalidOperationException($"Binding failed in test setup: {outcome}.");
            }

            return binding;
        }
    }
}
