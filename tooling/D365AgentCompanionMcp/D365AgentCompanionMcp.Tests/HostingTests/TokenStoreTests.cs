using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.HostingTests
{
    /// <summary>The bearer token store (D8): generate once, DPAPI round trip, regenerate, missing or corrupt setting.</summary>
    [TestFixture]
    public class TokenStoreTests
    {
        [Test]
        public void Load_NothingSaved_GeneratesSavesAndSaysSo()
        {
            var store = new FakeSettingsStore();
            var tokens = new TokenStore(new HostSettingsStore(store), new FakeTokenProtector());

            var notice = tokens.Load();

            Assert.That(notice, Is.EqualTo(TokenStore.GeneratedNotice));
            Assert.That(tokens.Token, Is.Not.Null.And.Length.GreaterThanOrEqualTo(40));
            Assert.That(store.Saved!.ProtectedToken, Does.StartWith("protected:"));
            Assert.That(store.Saved.ProtectedToken, Does.Not.Contain(tokens.Token!), "the token is never stored in clear");
        }

        [Test]
        public void Load_Saved_ReturnsTheSameTokenWithoutNotice()
        {
            var store = new FakeSettingsStore();
            var first = new TokenStore(new HostSettingsStore(store), new FakeTokenProtector());
            first.Load();

            var second = new TokenStore(new HostSettingsStore(store), new FakeTokenProtector());

            Assert.That(second.Load(), Is.Null);
            Assert.That(second.Token, Is.EqualTo(first.Token));
        }

        [Test]
        public void Load_Unreadable_GeneratesANewTokenAndSaysSo()
        {
            var store = new FakeSettingsStore { Saved = new HostSettings { ProtectedToken = "written-by-another-windows-user" } };
            var tokens = new TokenStore(new HostSettingsStore(store), new FakeTokenProtector());

            Assert.That(tokens.Load(), Is.EqualTo(TokenStore.UnreadableNotice));
            Assert.That(tokens.Token, Is.Not.Null);
            Assert.That(store.Saved!.ProtectedToken, Does.StartWith("protected:"));
        }

        [Test]
        public void Regenerate_ReplacesAndPersistsAndRaisesChanged()
        {
            var store = new FakeSettingsStore();
            var tokens = new TokenStore(new HostSettingsStore(store), new FakeTokenProtector());
            tokens.Load();
            var before = tokens.Token;
            var changed = false;
            tokens.Changed += (s, e) => changed = true;

            tokens.Regenerate();

            Assert.That(tokens.Token, Is.Not.EqualTo(before));
            Assert.That(changed, Is.True);
            Assert.That(new FakeTokenProtector().Unprotect(store.Saved!.ProtectedToken!), Is.EqualTo(tokens.Token));
        }

        [Test]
        public void Dpapi_RoundTripsForTheCurrentUser()
        {
            var protector = new DpapiTokenProtector();

            var protectedToken = protector.Protect("a-token-value");

            Assert.That(protectedToken, Does.Not.Contain("a-token-value"));
            Assert.That(protector.Unprotect(protectedToken), Is.EqualTo("a-token-value"));
        }

        [Test]
        public void Dpapi_CorruptValue_IsReadAsUnreadable()
        {
            var store = new FakeSettingsStore { Saved = new HostSettings { ProtectedToken = "bm90LWRwYXBp" } };
            var tokens = new TokenStore(new HostSettingsStore(store), new DpapiTokenProtector());

            Assert.That(tokens.Load(), Is.EqualTo(TokenStore.UnreadableNotice));
        }
    }
}
