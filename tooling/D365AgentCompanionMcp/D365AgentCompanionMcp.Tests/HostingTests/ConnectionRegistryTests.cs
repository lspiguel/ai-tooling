using System;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using Microsoft.Xrm.Sdk;

using Moq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.HostingTests
{
    /// <summary>One tab per connection (C1–C7) and the binding order found in spike S6.</summary>
    [TestFixture]
    public class ConnectionRegistryTests
    {
        private static readonly IOrganizationService Service = new Mock<IOrganizationService>().Object;

        private ConnectionRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new ConnectionRegistry();
        }

        [Test]
        public void Claim_IsNotListedOrRoutableUntilCompleted()
        {
            var tab = new FakeInstance();
            var claim = Candidate(tab, "11111111-1111-1111-1111-111111111111");

            Assert.That(_registry.Claim(claim), Is.Null);
            Assert.That(_registry.Bound(), Is.Empty);
            Assert.That(_registry.Find(claim.ConnectionId), Is.Null);

            Complete(claim, Guid.NewGuid());
            Assert.That(_registry.Bound(), Is.EquivalentTo(new[] { claim }));
            Assert.That(_registry.Find(claim.ConnectionId), Is.SameAs(claim));
        }

        [Test]
        public void Claim_SameConnectionIdInAnotherTab_ReturnsTheExistingBinding()
        {
            var first = Candidate(new FakeInstance("tab 1"), "11111111-1111-1111-1111-111111111111");
            _registry.Claim(first);

            var conflict = _registry.Claim(Candidate(new FakeInstance("tab 2"), "{11111111-1111-1111-1111-111111111111}"));

            Assert.That(conflict, Is.SameAs(first));
        }

        [Test]
        public void ToolbarConnectToSeveralTabs_FirstInHookOrderWinsEvenIfItsIdentityReturnsLast()
        {
            // XrmToolBox updates tabs one after another; each claims synchronously (C3, S6).
            var id = "22222222-2222-2222-2222-222222222222";
            var tab1 = Candidate(new FakeInstance("tab 1"), id);
            var tab3 = Candidate(new FakeInstance("tab 3"), id);
            var tab4 = Candidate(new FakeInstance("tab 4"), id);

            Assert.That(_registry.Claim(tab1), Is.Null);
            Assert.That(_registry.Claim(tab3), Is.SameAs(tab1));
            Assert.That(_registry.Claim(tab4), Is.SameAs(tab1));

            // The identity call for tab 1 returns after everything else; it still binds.
            Assert.That(Complete(tab1, Guid.NewGuid()), Is.EqualTo(BindOutcome.Bound));
        }

        [Test]
        public void Complete_SameOrganizationThroughAnotherSavedConnection_Conflicts()
        {
            var organization = Guid.NewGuid();
            var first = Candidate(new FakeInstance("tab 1"), "11111111-1111-1111-1111-111111111111");
            var second = Candidate(new FakeInstance("tab 2"), "33333333-3333-3333-3333-333333333333");
            _registry.Claim(first);
            _registry.Claim(second);
            Complete(first, organization);

            var outcome = _registry.Complete(second, organization, Guid.NewGuid(), false, Service, DateTime.UtcNow, out var conflict);

            Assert.That(outcome, Is.EqualTo(BindOutcome.OrganizationConflict));
            Assert.That(conflict, Is.SameAs(first));
            Assert.That(second.State, Is.EqualTo(BindingState.Ended));
            Assert.That(_registry.ForOwner(second.Owner), Is.Null);
        }

        [Test]
        public void Complete_AfterTheTabChangedConnection_IsAbandoned()
        {
            var tab = new FakeInstance();
            var claim = Candidate(tab, "11111111-1111-1111-1111-111111111111");
            _registry.Claim(claim);
            _registry.Release(tab);

            Assert.That(Complete(claim, Guid.NewGuid()), Is.EqualTo(BindOutcome.Abandoned));
            Assert.That(_registry.Bound(), Is.Empty);
        }

        [Test]
        public void Release_EndsTheBindingAndFreesTheConnectionForAnotherTab()
        {
            var tab1 = new FakeInstance("tab 1");
            var binding = Candidate(tab1, "11111111-1111-1111-1111-111111111111");
            _registry.Claim(binding);
            Complete(binding, Guid.NewGuid());

            var released = _registry.Release(tab1);

            Assert.That(released, Is.SameAs(binding));
            Assert.That(binding.Ended.IsCancellationRequested, Is.True);
            Assert.That(_registry.Find(binding.ConnectionId), Is.Null);
            Assert.That(_registry.Claim(Candidate(new FakeInstance("tab 2"), binding.ConnectionId)), Is.Null);
        }

        [Test]
        public void Release_EndsTheOnceScopedConfirmation()
        {
            var tab = new FakeInstance();
            var binding = Candidate(tab, "11111111-1111-1111-1111-111111111111");
            _registry.Claim(binding);
            Complete(binding, Guid.NewGuid());
            binding.DevConfirmedOnce = true;

            _registry.Release(tab);

            Assert.That(binding.DevConfirmedOnce, Is.False);
        }

        [Test]
        public void Find_AcceptsAnyCaseAndBraces()
        {
            var binding = Candidate(new FakeInstance(), "abcdefab-1111-1111-1111-111111111111");
            _registry.Claim(binding);
            Complete(binding, Guid.NewGuid());

            Assert.That(_registry.Find("{ABCDEFAB-1111-1111-1111-111111111111}"), Is.SameAs(binding));
            Assert.That(_registry.Find("not-a-connection"), Is.Null);
        }

        [Test]
        public void Bound_IsOrderedByBindingTime()
        {
            var later = Candidate(new FakeInstance("tab 1"), "11111111-1111-1111-1111-111111111111");
            var earlier = Candidate(new FakeInstance("tab 2"), "22222222-2222-2222-2222-222222222222");
            _registry.Claim(later);
            _registry.Claim(earlier);
            _registry.Complete(later, Guid.NewGuid(), Guid.NewGuid(), false, Service, new DateTime(2026, 10, 30, 12, 5, 0, DateTimeKind.Utc), out _);
            _registry.Complete(earlier, Guid.NewGuid(), Guid.NewGuid(), false, Service, new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc), out _);

            Assert.That(_registry.Bound().Select(b => b.ConnectionName), Is.EqualTo(new[] { earlier.ConnectionName, later.ConnectionName }));
        }

        private static BoundConnection Candidate(IHostInstance owner, string connectionId) =>
            new BoundConnection(owner, connectionId, false, $"{((FakeInstance)owner).TabLabel} connection", "orgunique", "Org", "https://org.example.invalid");

        private BindOutcome Complete(BoundConnection claim, Guid organization) =>
            _registry.Complete(claim, organization, Guid.NewGuid(), false, Service, DateTime.UtcNow, out _);
    }
}
