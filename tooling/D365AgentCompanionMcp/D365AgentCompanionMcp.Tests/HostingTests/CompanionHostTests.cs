using System;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using Microsoft.Xrm.Sdk;

using Moq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.HostingTests
{
    /// <summary>The singleton host's lifecycle (H1–H4).</summary>
    [TestFixture]
    public class CompanionHostTests
    {
        [TearDown]
        public void TearDown() => FakeServer.Taken.Clear();

        [Test]
        public void FirstTab_StartsTheHost_LaterTabsShareIt()
        {
            var host = TestHost.Create();

            host.Attach(new FakeInstance("tab 1"));
            var token = host.RunningToken;
            host.Attach(new FakeInstance("tab 2"));

            Assert.That(host.State, Is.EqualTo(HostState.Running));
            Assert.That(host.InstanceCount, Is.EqualTo(2));
            Assert.That(host.RunningToken, Is.EqualTo(token), "the second tab did not restart the host");
        }

        [Test]
        public void Attach_IsIdempotentPerTab()
        {
            var host = TestHost.Create();
            var tab = new FakeInstance();

            host.Attach(tab);
            host.Attach(tab);

            Assert.That(host.InstanceCount, Is.EqualTo(1));
        }

        [Test]
        public void ClosingTheTabThatStartedIt_DoesNotStopTheHost_ClosingTheLastDoes()
        {
            var host = TestHost.Create();
            var first = new FakeInstance("tab 1");
            var second = new FakeInstance("tab 2");
            host.Attach(first);
            host.Attach(second);

            host.Detach(first);
            Assert.That(host.State, Is.EqualTo(HostState.Running));

            host.Detach(second);
            Assert.That(host.State, Is.EqualTo(HostState.Stopped));
        }

        [Test]
        public void PortInUse_HostFailsWithAnExplanation()
        {
            FakeServer.Taken.Add(HostSettings.DefaultPort);
            var host = TestHost.Create();

            host.Attach(new FakeInstance());

            Assert.That(host.State, Is.EqualTo(HostState.Failed));
            Assert.That(host.StartError, Does.Contain($"Port {HostSettings.DefaultPort}").And.Contain("Another XrmToolBox process"));
        }

        [Test]
        public void ChangePort_RestartsOnTheNewPortAndPersists()
        {
            FakeServer.Taken.Add(HostSettings.DefaultPort);
            var store = new FakeSettingsStore();
            var host = TestHost.Create(store: store);
            host.Attach(new FakeInstance());

            host.ChangePort(47400);

            Assert.That(host.State, Is.EqualTo(HostState.Running));
            Assert.That(host.Endpoint, Is.EqualTo("http://127.0.0.1:47400/mcp"));
            Assert.That(store.Saved!.Port, Is.EqualTo(47400));
        }

        [Test]
        public void StopAndStart_FromAnyTab_ActOnTheSharedHost()
        {
            var host = TestHost.Create();
            host.Attach(new FakeInstance());
            var firstRun = host.RunningToken;

            host.Stop();
            Assert.That(host.State, Is.EqualTo(HostState.Stopped));
            Assert.That(firstRun.IsCancellationRequested, Is.True, "pending calls see server_stopping");

            host.Start();
            Assert.That(host.State, Is.EqualTo(HostState.Running));
            Assert.That(host.RunningToken.IsCancellationRequested, Is.False);
        }

        [Test]
        public void Stop_EndsSessionGrantsAndCursors_KeepsEnvironmentGrantsAndBindings()
        {
            var host = TestHost.Create();
            var tab = new FakeInstance();
            host.Attach(tab);
            var binding = TestHost.Bind(host, tab, Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", new Mock<IOrganizationService>().Object);
            var other = Guid.NewGuid();
            host.Approvals.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Session, binding.OrganizationId, "Org", "tab");
            host.Approvals.GrantForOrganization(ApprovalKind.ReadApproval, ApprovalScope.Environment, other, "Other", "tab");
            host.Approvals.GrantForOrganization(ApprovalKind.DevEnvironment, ApprovalScope.Session, binding.OrganizationId, "Org", "tab");
            host.Cursors.Open(binding.ConnectionId, "list_publishers", new Pager(new Mock<IPageSource>().Object, 10));

            host.Stop();

            Assert.That(host.Approvals.Find(ApprovalKind.ReadApproval, binding.OrganizationId), Is.Null);
            Assert.That(host.Approvals.Find(ApprovalKind.DevEnvironment, binding.OrganizationId), Is.Null);
            Assert.That(host.Approvals.Find(ApprovalKind.ReadApproval, other)?.Scope, Is.EqualTo(ApprovalScope.Environment));
            Assert.That(host.Cursors.Count, Is.Zero);
            Assert.That(host.Registry.Find(binding.ConnectionId), Is.SameAs(binding), "tabs stay bound across a stop");
        }

        [Test]
        public void ReleaseBinding_ReleasesItsCursors()
        {
            var host = TestHost.Create();
            var tab = new FakeInstance();
            host.Attach(tab);
            var binding = TestHost.Bind(host, tab, Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", new Mock<IOrganizationService>().Object);
            host.Cursors.Open(binding.ConnectionId, "list_publishers", new Pager(new Mock<IPageSource>().Object, 10));

            host.ReleaseBinding(tab);

            Assert.That(host.Cursors.Count, Is.Zero);
            Assert.That(host.Registry.Bound(), Is.Empty);
        }

        [Test]
        public void TokenAndLimits_SurviveANewHostOnTheSameSettings()
        {
            var store = new FakeSettingsStore();
            var first = TestHost.Create(store: store);
            first.ChangeLimits(250, 30000, 90);

            var second = TestHost.Create(store: store);

            Assert.That(second.Tokens.Token, Is.EqualTo(first.Tokens.Token));
            Assert.That(second.Notice, Is.Null, "no new-token notice when the saved token was read");
            Assert.That(second.Settings.Read(s => (s.MaxPageSize, s.ResponseCapCharacters, s.ApprovalTimeoutSeconds)), Is.EqualTo((250, 30000, 90)));
        }

        [Test]
        public void RegenerateToken_ChangesItAndRaisesANotice()
        {
            var host = TestHost.Create();
            var before = host.Tokens.Token;
            string? notice = null;
            host.NoticeRaised += (s, n) => notice = n;

            host.RegenerateToken();

            Assert.That(host.Tokens.Token, Is.Not.EqualTo(before));
            Assert.That(notice, Is.EqualTo(TokenStore.RegeneratedNotice));
        }
    }
}
