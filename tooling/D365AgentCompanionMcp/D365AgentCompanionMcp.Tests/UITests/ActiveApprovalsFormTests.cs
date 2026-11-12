using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tools;
using Lspiguel.Xrm.D365AgentCompanionMcp.UI;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.UITests
{
    /// <summary>
    /// The Active Approvals window, the approval dialog and the File root prompt (SPEC §8.2, §8.8): what they list,
    /// the confirmation inside the window, and what each button does. Forms are built but never shown.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ActiveApprovalsFormTests
    {
        private static readonly Guid TenantX = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid TenantY = new Guid("11111111-0000-0000-0000-0000000000a2");

        private CompanionHost _host = null!;

        [SetUp]
        public void SetUp()
        {
            _host = TestHost.Create();
        }

        // ---- Active Approvals ----

        [Test]
        public void TheWindow_ListsGlobalFirst_ThenTenantsWithTheirEnvironments_AndShowsTheToken()
        {
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, "Anywhere", Guid.Empty);
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "Zulu DEV", TenantX);
            Allow(ApprovalKind.Write, ApprovalScope.Environment, "Zulu DEV", TenantX, "e-z");
            Allow(ApprovalKind.RecordRead, ApprovalScope.Session, "Alpha UAT", TenantX, "e-a");
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Beta DEV", TenantY, "e-b");

            using var form = new ActiveApprovalsForm(_host, "tab 1");

            var lines = Texts(form);
            var global = lines.IndexOf("Global");
            var tenantX = lines.IndexOf("Tenant 00000000…");
            var alpha = lines.IndexOf("▸ Alpha UAT");
            var zulu = lines.IndexOf("▸ Zulu DEV");
            var tenantY = lines.IndexOf("Tenant 11111111…");
            Assert.That(new[] { global, tenantX, alpha, zulu, tenantY }, Is.Ordered.And.All.GreaterThanOrEqualTo(0), string.Join(" | ", lines));
            Assert.That(Buttons(form), Does.Contain("Copy").And.Contain("Regenerate"), "the token moved here");
            Assert.That(Buttons(form), Does.Contain("Reset all approvals"));
        }

        [Test]
        public void AnEmptyGlobal_ShowsNone()
        {
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Assert.That(Texts(form), Does.Contain("None"));
        }

        [Test]
        public void ARowShowsKindScopeAndTheTabItCameFrom_AndRevokeRemovesIt()
        {
            Allow(ApprovalKind.Write, ApprovalScope.Environment, "Zulu DEV", TenantX, "e-z");
            using var form = new ActiveApprovalsForm(_host, "tab 1");
            Assert.That(Texts(form).Any(t => t.Contains("Write") && t.Contains("Per environment") && t.Contains("tab 7")), Is.True, string.Join(" | ", Texts(form)));

            Click(form, "Revoke");

            Assert.That(_host.Approvals.List(), Is.Empty);
            Assert.That(Texts(form).Any(t => t.Contains("Per environment")), Is.False, "the window follows the store");
        }

        [Test]
        public void ResetOnATenant_AsksFirst_NamingItsEnvironments_ThenRevokesThemAll_ButNotOtherTenants()
        {
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "Zulu DEV", TenantX);
            Allow(ApprovalKind.Write, ApprovalScope.Environment, "Zulu DEV", TenantX, "e-z");
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, "Zzz DEV", TenantY, "e-b");
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Click(form, "Reset", index: 0);

            Assert.That(Texts(form).Any(t => t.StartsWith("Revoke the grants of Tenant 00000000… and of its 1 environment")), Is.True, string.Join(" | ", Texts(form)));
            Assert.That(_host.Approvals.List(), Has.Count.EqualTo(3), "nothing is revoked until the person confirms");

            Click(form, "Reset", index: 0);

            Assert.That(_host.Approvals.List().Select(g => g.TenantId), Is.EqualTo(new[] { TenantY }));
        }

        [Test]
        public void CancellingAConfirmation_RevokesNothing_AndRestoresTheRows()
        {
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, "Anywhere", Guid.Empty);
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Click(form, "Reset");
            Assert.That(Buttons(form), Does.Contain("Cancel"));
            Click(form, "Cancel");

            Assert.That(_host.Approvals.List(), Has.Count.EqualTo(1));
            Assert.That(Buttons(form), Does.Not.Contain("Cancel"));
        }

        [Test]
        public void ResetAllApprovals_AsksInTheWindow_ThenRevokesEverything_AndKeepsFileRoots()
        {
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, "Anywhere", Guid.Empty);
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "Zulu DEV", TenantX);
            _host.FileRoots.Set(ApprovalScope.Tenant, new ConnectionIdentity(Guid.NewGuid(), "e-z", TenantX, "Zulu DEV"), @"C:\Repos\z");
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Click(form, "Reset all approvals");
            Assert.That(Texts(form).Any(t => t.StartsWith("Revoke every grant?")), Is.True);
            Click(form, "Reset all");

            Assert.That(_host.Approvals.List(), Is.Empty);
            Assert.That(_host.FileRoots.List(), Has.Count.EqualTo(1), "a File root is a setting, not a grant");
        }

        [Test]
        public void Close_StandsAtTheRightEndOfTheFooter_AndHidesWhileResetAllAsks()
        {
            using var form = new ActiveApprovalsForm(_host, "tab 1") { StartPosition = FormStartPosition.Manual, Left = -3000 };
            form.Show();
            Application.DoEvents();

            var close = Walk<Button>(form).Single(b => b.Text == "Close");
            var reset = Walk<Button>(form).Single(b => b.Text == "Reset all approvals");
            var closeRight = form.RectangleToClient(close.RectangleToScreen(close.ClientRectangle)).Right;
            Assert.That(form.ClientSize.Width - closeRight, Is.LessThanOrEqualTo(16), "Close is at the right edge");
            Assert.That(form.RectangleToClient(reset.RectangleToScreen(reset.ClientRectangle)).Left, Is.LessThan(form.ClientSize.Width / 2), "Reset all approvals stays on the left");
            Assert.That(close.Height, Is.EqualTo(reset.Height), "Close keeps its natural height");

            Click(form, "Reset all approvals");
            Assert.That(close.Visible, Is.False, "the confirmation offers Reset all or Cancel only");
            Click(form, "Cancel");
            Assert.That(close.Visible, Is.True);
        }

        [Test]
        public void AFileRoot_ShowsUnderItsTenant_AndClearAsksFirst_ThenRemovesIt()
        {
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "Zulu DEV", TenantX);
            _host.FileRoots.Set(ApprovalScope.Tenant, new ConnectionIdentity(Guid.NewGuid(), "e-z", TenantX, "Zulu DEV"), @"C:\Repos\z");
            using var form = new ActiveApprovalsForm(_host, "tab 1");
            Assert.That(Texts(form).Any(t => t.Contains(@"C:\Repos\z") && t.Contains("tenant")), Is.True, string.Join(" | ", Texts(form)));
            Assert.That(Buttons(form), Does.Contain("Change"));

            Click(form, "Clear");
            Assert.That(Texts(form).Any(t => t.StartsWith("Clear this File root?")), Is.True);
            Assert.That(_host.FileRoots.List(), Has.Count.EqualTo(1));
            Click(form, "Clear");

            Assert.That(_host.FileRoots.List(), Is.Empty);
        }

        [Test]
        public void ATenantWithoutAFileRoot_ShowsNotSet_WithSet()
        {
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Tenant, "Zulu DEV", TenantX);
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Assert.That(Texts(form).Any(t => t.Contains("File root") && t.Contains("Not set")), Is.True, string.Join(" | ", Texts(form)));
            Assert.That(Buttons(form), Does.Contain("Set"));
        }

        [Test]
        public void AStaleGrant_IsMarked_WhenABoundEnvironmentDiffersOnItsId()
        {
            var tab = new FakeInstance();
            _host.Attach(tab);
            var org = Guid.NewGuid();
            TestHost.Bind(_host, tab, org, "aaaaaaaa-1111-1111-1111-111111111111", new Moq.Mock<Microsoft.Xrm.Sdk.IOrganizationService>().Object, "Dev", "e-new", TenantX);
            _host.Approvals.Allow(ApprovalKind.ReadApproval, ApprovalScope.Environment, new ConnectionIdentity(org, "e-old", TenantX, "Dev"), "tab");
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Assert.That(Texts(form).Any(t => t.Contains("Stale")), Is.True, string.Join(" | ", Texts(form)));
        }

        // ---- Write call log to disk (SPEC 6a.2) ----

        [Test]
        public void TickingTheCallLog_AsksForTheFolder_StartingAtThePreviousOne_AndCancelLeavesItOff()
        {
            using var folder = new TempFolder();
            _host.DiskLog.TurnOn(folder.Path);
            _host.DiskLog.TurnOff();
            using var form = new ActiveApprovalsForm(_host, "tab 1");
            var asked = new List<string?>();
            form.ChooseLogFolder = start =>
            {
                asked.Add(start);
                return null;
            };

            CallLogBox(form).Checked = true;

            Assert.That(asked, Is.EqualTo(new[] { System.IO.Path.GetFullPath(folder.Path) }));
            Assert.That(CallLogBox(form).Checked, Is.False);
            Assert.That(_host.DiskLog.IsOn, Is.False);
            Assert.That(Texts(form).Any(t => t.Contains("no folder was chosen")), Is.True, string.Join(" | ", Texts(form)));
        }

        [Test]
        public void AFolderInsideAGitWorkingTree_IsRefused_WithTheReasonShown()
        {
            using var folder = new TempFolder();
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(folder.Path, ".git"));
            using var form = new ActiveApprovalsForm(_host, "tab 1");
            form.ChooseLogFolder = start => folder.Path;

            CallLogBox(form).Checked = true;

            Assert.That(CallLogBox(form).Checked, Is.False);
            Assert.That(_host.DiskLog.IsOn, Is.False);
            Assert.That(Texts(form).Any(t => t.Contains("Git working tree")), Is.True, string.Join(" | ", Texts(form)));
        }

        [Test]
        public void AFolderThatPasses_TurnsTheLogOn_ShowsTheFolder_AndUntickingTurnsItOff_AndTickingAgainAsksAgain()
        {
            using var folder = new TempFolder();
            using var form = new ActiveApprovalsForm(_host, "tab 1");
            var asked = 0;
            form.ChooseLogFolder = start =>
            {
                asked++;
                return folder.Path;
            };

            CallLogBox(form).Checked = true;
            Assert.That(_host.DiskLog.IsOn, Is.True);
            Assert.That(CallLogBox(form).Checked, Is.True);
            Assert.That(Texts(form).Any(t => t.Contains(System.IO.Path.GetFullPath(folder.Path))), Is.True, string.Join(" | ", Texts(form)));

            CallLogBox(form).Checked = false;
            Assert.That(_host.DiskLog.IsOn, Is.False);

            CallLogBox(form).Checked = true;
            Assert.That(asked, Is.EqualTo(2));
        }

        [Test]
        public void ResetAllApprovals_LeavesTheCallLogOn()
        {
            using var folder = new TempFolder();
            _host.DiskLog.TurnOn(folder.Path);
            Allow(ApprovalKind.ReadApproval, ApprovalScope.Always, "Anywhere", Guid.Empty);
            using var form = new ActiveApprovalsForm(_host, "tab 1");

            Click(form, "Reset all approvals");
            Click(form, "Reset all");

            Assert.That(_host.Approvals.List(), Is.Empty);
            Assert.That(_host.DiskLog.IsOn, Is.True);
            Assert.That(CallLogBox(form).Checked, Is.True);
        }

        [Test]
        public void AStartupWarning_ShowsInTheWindow()
        {
            using var folder = new TempFolder();
            var store = new FakeSettingsStore { Saved = new HostSettings { SchemaVersion = 1, CallLogToDisk = true, CallLogFolder = System.IO.Path.Combine(folder.Path, "gone") } };
            var host = TestHost.Create(store: store);
            using var form = new ActiveApprovalsForm(host, "tab 1");

            Assert.That(CallLogBox(form).Checked, Is.False);
            Assert.That(Texts(form).Any(t => t.Contains("off for this session")), Is.True, string.Join(" | ", Texts(form)));
        }

        // ---- the approval dialog ----

        [Test]
        public void ARead_OffersEveryScope_WithWarningsOnTenantAndGlobal()
        {
            using var form = new ApprovalForm(Request(ToolTier.Read, ApprovalKind.ReadApproval, tenantKnown: true));

            Assert.That(Buttons(form), Is.EquivalentTo(new[] { "Allow once", "Allow for this session", "Allow for this environment", "Allow for this tenant", "Allow globally", "Deny" }));
            Assert.That(Texts(form).Any(t => t.Contains("Per tenant and Global also cover environments bound later")), Is.True);
        }

        [Test]
        public void APerTenantButton_IsDisabledWithTheReason_WhenNoTenantIdIsReported()
        {
            using var form = new ApprovalForm(Request(ToolTier.Read, ApprovalKind.ReadApproval, tenantKnown: false));

            var tenant = All<Button>(form).Single(b => b.Text == "Allow for this tenant");
            Assert.That(tenant.Enabled, Is.False);
            Assert.That(Texts(form).Any(t => t.Contains("no tenant ID")), Is.True);
        }

        [Test]
        public void AWrite_OffersOnceSessionAndEnvironmentOnly_NoTenantWarning()
        {
            using var form = new ApprovalForm(Request(ToolTier.Write, ApprovalKind.Write, tenantKnown: true));

            Assert.That(Buttons(form), Is.EquivalentTo(new[] { "Allow once", "Allow for this session", "Allow for this environment", "Deny" }));
            Assert.That(Texts(form).Any(t => t.Contains("Per tenant and Global")), Is.False);
        }

        [Test]
        public void ARecordQuery_SaysItsScopesGrantRecordReadsOnly()
        {
            using var form = new ApprovalForm(Request(ToolTier.Read, ApprovalKind.RecordRead, tenantKnown: true, touchesRecords: true));

            Assert.That(Texts(form).Any(t => t.Contains("reads records") && t.Contains("record reads only")), Is.True);
        }

        [Test]
        public void ClickingAScope_RaisesThatDecision()
        {
            using var form = new ApprovalForm(Request(ToolTier.Read, ApprovalKind.ReadApproval, tenantKnown: true));
            ApprovalDecision? decided = null;
            form.Decided += (s, d) => decided = d;

            Click(form, "Allow for this tenant");

            Assert.That(decided, Is.EqualTo(ApprovalDecision.AllowTenant));
        }

        // ---- the File root prompt ----

        [Test]
        public void TheFileRootPrompt_PreselectsTheTenant_AndDisablesItWhenUnavailable()
        {
            using var known = new FileRootForm(new FileRootRequest { OrganizationName = "Dev", TenantAvailable = true, PreselectedLevel = ApprovalScope.Tenant });
            using var unknown = new FileRootForm(new FileRootRequest { OrganizationName = "Dev", TenantAvailable = false, PreselectedLevel = ApprovalScope.Environment });

            var tenantChoice = All<RadioButton>(known).Single(r => r.Text.StartsWith("Every environment"));
            Assert.That((tenantChoice.Enabled, tenantChoice.Checked), Is.EqualTo((true, true)));
            var disabled = All<RadioButton>(unknown).Single(r => r.Text.StartsWith("Every environment"));
            Assert.That((disabled.Enabled, disabled.Checked), Is.EqualTo((false, false)));
            Assert.That(All<RadioButton>(unknown).Single(r => r.Text == "This environment only").Checked, Is.True);
            Assert.That(Texts(unknown).Any(t => t.Contains("no tenant ID")), Is.True);
        }

        [Test]
        public void TheFileRootPrompt_ReturnsTheFolderAndLevel_OrNullOnCancel()
        {
            var folder = System.IO.Path.GetTempPath().TrimEnd('\\');
            using var form = new FileRootForm(new FileRootRequest { OrganizationName = "Dev", TenantAvailable = true, PreselectedLevel = ApprovalScope.Tenant, CurrentPath = folder });
            FileRootChoice? choice = null;
            var raised = 0;
            form.Decided += (s, c) =>
            {
                choice = c;
                raised++;
            };

            Click(form, "Use this folder");

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(choice!.Level, Is.EqualTo(ApprovalScope.Tenant));
            Assert.That(choice.Path, Is.EqualTo(folder));

            using var cancelled = new FileRootForm(new FileRootRequest { OrganizationName = "Dev", TenantAvailable = true });
            FileRootChoice? none = new FileRootChoice("x", ApprovalScope.Tenant);
            cancelled.Decided += (s, c) => none = c;
            Click(cancelled, "Cancel");
            Assert.That(none, Is.Null);
        }

        [Test]
        public void GitRootOf_FindsTheWorkingTreeRoot()
        {
            var repo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ac-git-" + Guid.NewGuid().ToString("N"));
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(repo, ".git"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(repo, "src", "deep"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(repo, "src", "deep", "a.js"), "x");

                Assert.That(FileRootForm.GitRootOf(System.IO.Path.Combine(repo, "src", "deep", "a.js")), Is.EqualTo(repo));
                Assert.That(FileRootForm.GitRootOf(System.IO.Path.Combine(repo, "src")), Is.EqualTo(repo));
            }
            finally
            {
                System.IO.Directory.Delete(repo, true);
            }
        }

        // ---- helpers ----

        private static ApprovalRequest Request(ToolTier tier, ApprovalKind kind, bool tenantKnown, bool touchesRecords = false) => new ApprovalRequest
        {
            Tool = "x",
            Tier = tier,
            Kind = kind,
            ConnectionName = "Conn",
            OrganizationName = "Dev",
            Arguments = "{}",
            Reason = "Story.",
            TouchesRecords = touchesRecords,
            TenantDisabledReason = tier == ToolTier.Read && !tenantKnown ? "this connection reports no tenant ID" : null,
            Options = tier == ToolTier.Read
                ? (tenantKnown
                    ? new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment, ApprovalDecision.AllowTenant, ApprovalDecision.AllowGlobal }
                    : new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment, ApprovalDecision.AllowGlobal })
                : new[] { ApprovalDecision.AllowOnce, ApprovalDecision.AllowSession, ApprovalDecision.AllowEnvironment },
            DeadlineUtc = DateTime.UtcNow.AddMinutes(2),
        };

        private void Allow(ApprovalKind kind, ApprovalScope scope, string name, Guid tenant, string environment = "")
        {
            // A fixed organization per name keeps one environment's grants together.
            var org = new Guid(name.GetHashCode(), 0, 0, new byte[8]);
            _host.Approvals.Allow(kind, scope, new ConnectionIdentity(org, environment, tenant, name), "tab 7");
        }

        private static IEnumerable<T> Walk<T>(Control root)
            where T : Control
        {
            foreach (Control child in root.Controls)
            {
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Walk<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static List<T> All<T>(Control root)
            where T : Control => Walk<T>(root).ToList();

        private static List<string> Texts(Control root) => Walk<Label>(root).Select(l => l.Text).Where(t => t.Length > 0).ToList();

        private static List<string> Buttons(Control root) => Walk<Button>(root).Select(b => b.Text).ToList();

        private static CheckBox CallLogBox(Control root) => Walk<CheckBox>(root).Single(c => c.Text == "Write call log to disk");

        private static void Click(Control root, string caption, int index = 0)
        {
            // PerformClick does nothing on a control that is not visible, and these forms are never shown.
            var button = Walk<Button>(root).Where(b => b.Text == caption && b.Enabled).ElementAt(index);
            typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, new object[] { EventArgs.Empty });
        }
    }
}
