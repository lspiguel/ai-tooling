using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.FilesTests
{
    /// <summary>The File root: levels, precedence, and the path checks that run before any dialog (SPEC §6.8).</summary>
    [TestFixture]
    public class FileRootTests
    {
        private static readonly Guid OrgA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid OrgB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
        private static readonly Guid Tenant1 = new Guid("11111111-0000-0000-0000-000000000001");
        private static readonly Guid Tenant2 = new Guid("22222222-0000-0000-0000-000000000002");

        private string _root = null!;
        private string _sibling = null!;
        private string _outside = null!;

        [SetUp]
        public void SetUp()
        {
            var parent = Path.Combine(Path.GetTempPath(), "ac-fileroot-" + Guid.NewGuid().ToString("N"));
            _root = Path.Combine(parent, "repo");
            _sibling = Path.Combine(parent, "repo2");
            _outside = Path.Combine(parent, "outside");
            foreach (var folder in new[] { Path.Combine(_root, "Webresources", "scripts"), _sibling, _outside })
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(Path.Combine(_root, "Webresources", "scripts", "account.form.js"), "// x");
            File.WriteAllText(Path.Combine(_sibling, "other.js"), "// y");
            File.WriteAllText(Path.Combine(_outside, "secret.js"), "// z");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                var junction = Path.Combine(_root, "link");
                if (Directory.Exists(junction))
                {
                    Directory.Delete(junction);
                }

                Directory.Delete(Path.GetDirectoryName(_root)!, true);
            }
            catch (IOException)
            {
                // Temp cleanup is best effort.
            }
        }

        // ---- the store ----

        [Test]
        public void AnEnvironmentFileRoot_TakesPrecedenceOverItsTenants()
        {
            var store = new FileRootStore(new HostSettingsStore(new FakeSettingsStore()));
            var a = new ConnectionIdentity(OrgA, "e-a", Tenant1, "A");
            var b = new ConnectionIdentity(OrgB, "e-b", Tenant1, "B");
            store.Set(ApprovalScope.Tenant, a, @"C:\Repos\tenant");
            store.Set(ApprovalScope.Environment, b, @"C:\Repos\env-b");

            Assert.That(store.Find(a)!.Path, Is.EqualTo(@"C:\Repos\tenant"));
            Assert.That(store.Find(a)!.Level, Is.EqualTo(ApprovalScope.Tenant));
            Assert.That(store.Find(b)!.Path, Is.EqualTo(@"C:\Repos\env-b"));
        }

        [Test]
        public void ATenantFileRoot_ServesAnEnvironmentBoundLater_NotAnotherTenant()
        {
            var store = new FileRootStore(new HostSettingsStore(new FakeSettingsStore()));
            store.Set(ApprovalScope.Tenant, new ConnectionIdentity(OrgA, "e-a", Tenant1, "A"), @"C:\Repos\tenant");

            Assert.That(store.Find(new ConnectionIdentity(OrgB, "e-b", Tenant1, "B"))?.Path, Is.EqualTo(@"C:\Repos\tenant"));
            Assert.That(store.Find(new ConnectionIdentity(Guid.NewGuid(), "e-c", Tenant2, "C")), Is.Null);
            Assert.That(store.Find(new ConnectionIdentity(Guid.NewGuid(), null, Guid.Empty, "D")), Is.Null);
        }

        [Test]
        public void ATenantFileRoot_NeedsATenantId()
        {
            var store = new FileRootStore(new HostSettingsStore(new FakeSettingsStore()));

            Assert.That(() => store.Set(ApprovalScope.Tenant, new ConnectionIdentity(OrgA, null, Guid.Empty, "A"), @"C:\Repos\x"), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void SettingAgain_ReplacesTheRootAtThatLevel_AndClearFallsBackToTheTenants()
        {
            var store = new FileRootStore(new HostSettingsStore(new FakeSettingsStore()));
            var a = new ConnectionIdentity(OrgA, "e-a", Tenant1, "A");
            store.Set(ApprovalScope.Tenant, a, @"C:\Repos\tenant");
            store.Set(ApprovalScope.Environment, a, @"C:\Repos\one");
            store.Set(ApprovalScope.Environment, a, @"C:\Repos\two");

            Assert.That(store.List(), Has.Count.EqualTo(2));
            Assert.That(store.Find(a)!.Path, Is.EqualTo(@"C:\Repos\two"));

            store.Clear(store.Find(a)!);
            Assert.That(store.Find(a)!.Path, Is.EqualTo(@"C:\Repos\tenant"));
        }

        [Test]
        public void FileRoots_SurviveAReload_AndRaiseChanged()
        {
            var backing = new FakeSettingsStore();
            var store = new FileRootStore(new HostSettingsStore(backing));
            var raised = 0;
            store.Changed += (s, e) => raised++;

            store.Set(ApprovalScope.Tenant, new ConnectionIdentity(OrgA, "e-a", Tenant1, "A"), @"C:\Repos\tenant");

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(new FileRootStore(new HostSettingsStore(backing)).List().Single().Path, Is.EqualTo(@"C:\Repos\tenant"));
        }

        // ---- the path checks ----

        [Test]
        public void ARelativePath_IsReadFromTheRoot_WithEitherSlash()
        {
            var expected = Path.Combine(_root, "Webresources", "scripts", "account.form.js");

            Assert.That(FilePathResolver.Resolve(_root, "Webresources/scripts/account.form.js", "items[0].path"), Is.EqualTo(expected));
            Assert.That(FilePathResolver.Resolve(_root, @"Webresources\scripts\account.form.js", "items[0].path"), Is.EqualTo(expected));
        }

        [Test]
        public void AnAbsolutePathInsideTheRoot_IsAccepted_WithoutRegardToCase()
        {
            var absolute = Path.Combine(_root, "Webresources", "scripts", "account.form.js");

            Assert.That(FilePathResolver.Resolve(_root, absolute, "items[0].path"), Is.EqualTo(absolute));
            Assert.That(FilePathResolver.Resolve(_root, absolute.ToUpperInvariant(), "items[0].path"), Is.EqualTo(absolute.ToUpperInvariant()));
        }

        [Test]
        public void ADotDotThatClimbsOutOfTheRoot_IsRefused()
        {
            var error = Assert.Throws<ToolException>(() => FilePathResolver.Resolve(_root, "../outside/secret.js", "items[0].path"))!;

            Assert.That(error.Code, Is.EqualTo(ToolErrorCodes.InvalidArgument));
            Assert.That((string?)error.Details!["argument"], Is.EqualTo("items[0].path"));
            Assert.That((string?)error.Details["fileRoot"], Is.EqualTo(_root));
            Assert.That((string?)error.Details["resolvedPath"], Is.EqualTo(Path.Combine(_outside, "secret.js")));
        }

        [Test]
        public void ADotDotThatStaysInside_IsAccepted()
        {
            Assert.That(FilePathResolver.Resolve(_root, "Webresources/../Webresources/scripts/account.form.js", "p"), Does.EndWith("account.form.js"));
        }

        [Test]
        public void AnAbsolutePathOutsideTheRoot_IsRefused()
        {
            var error = Assert.Throws<ToolException>(() => FilePathResolver.Resolve(_root, Path.Combine(_outside, "secret.js"), "items[0].path"))!;

            Assert.That(error.Code, Is.EqualTo(ToolErrorCodes.InvalidArgument));
        }

        [Test]
        public void ASiblingFolderWhoseNameStartsWithTheRootsName_IsOutside()
        {
            Assert.That(() => FilePathResolver.Resolve(_root, Path.Combine(_sibling, "other.js"), "p"), Throws.InstanceOf<ToolException>());
            Assert.That(() => FilePathResolver.Resolve(_root, "../repo2/other.js", "p"), Throws.InstanceOf<ToolException>());
        }

        [Test]
        public void TheRootItself_AndAMissingFile_AreRefused()
        {
            Assert.That(() => FilePathResolver.Resolve(_root, _root, "p"), Throws.InstanceOf<ToolException>());
            Assert.That(() => FilePathResolver.Resolve(_root, "Webresources/scripts/missing.js", "p"), Throws.InstanceOf<ToolException>());
            Assert.That(() => FilePathResolver.Resolve(_root, "Webresources", "p"), Throws.InstanceOf<ToolException>(), "a folder is not a file");
        }

        [Test]
        public void AnInvalidPath_IsRefused()
        {
            Assert.That(() => FilePathResolver.Resolve(_root, "bad\0name.js", "p"), Throws.InstanceOf<ToolException>());
        }

        [Test]
        public void APathThroughAJunction_IsRefused_EvenWhenItsTextStaysInside()
        {
            var junction = Path.Combine(_root, "link");
            if (!MakeJunction(junction, _outside))
            {
                Assert.Ignore("A junction could not be created here.");
            }

            var error = Assert.Throws<ToolException>(() => FilePathResolver.Resolve(_root, "link/secret.js", "items[0].path"))!;

            Assert.That(error.Message, Does.Contain("link"));
        }

        [Test]
        public void Relative_ShowsThePathFromTheRoot()
        {
            var full = Path.Combine(_root, "Webresources", "scripts", "account.form.js");

            Assert.That(FilePathResolver.Relative(_root, full), Is.EqualTo("Webresources/scripts/account.form.js"));
        }

        private static bool MakeJunction(string link, string target)
        {
            try
            {
                var info = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using (var process = Process.Start(info)!)
                {
                    process.WaitForExit(10000);
                    return process.ExitCode == 0 && Directory.Exists(link);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
    }
}
