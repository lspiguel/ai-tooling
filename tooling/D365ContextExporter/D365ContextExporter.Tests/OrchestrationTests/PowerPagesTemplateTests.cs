using System;
using System.IO;
using System.Windows.Forms;

using Lspiguel.Xrm.D365ContextExporter.Helpers;
using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Orchestration;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests
{
    /// <summary>
    /// Renders the shipped PowerPages spec against one synthetic enhanced-data-model site, in the shapes the
    /// FetchXML runner produces: lookups as { id, logicalName, name }, option sets as integers, and each
    /// component's settings as a JSON string in <c>content</c>.
    /// </summary>
    [TestFixture]
    public class PowerPagesTemplateTests
    {
        private const string Site = "50000000-0000-0000-0000-000000000001";
        private const string Published = "50000000-0000-0000-0000-000000000002";
        private const string RoleAuthenticated = "50000000-0000-0000-0000-000000000011";
        private const string RolePartner = "50000000-0000-0000-0000-000000000012";
        private const string Home = "50000000-0000-0000-0000-000000000021";
        private const string Cases = "50000000-0000-0000-0000-000000000022";
        private const string CasesContent = "50000000-0000-0000-0000-000000000023";
        private const string NewCase = "50000000-0000-0000-0000-000000000024";
        private const string PermCases = "50000000-0000-0000-0000-000000000031";
        private const string PermNotes = "50000000-0000-0000-0000-000000000032";
        private const string BasicForm = "50000000-0000-0000-0000-000000000041";
        private const string List = "50000000-0000-0000-0000-000000000042";
        private const string MultiForm = "50000000-0000-0000-0000-000000000043";
        private const string StepA = "50000000-0000-0000-0000-000000000044";
        private const string StepB = "50000000-0000-0000-0000-000000000045";
        private const string PageTemplate = "50000000-0000-0000-0000-000000000051";
        private const string WebTemplate = "50000000-0000-0000-0000-000000000052";
        private const string LinkSet = "50000000-0000-0000-0000-000000000061";

        private string _baseDir = string.Empty;
        private string _output = string.Empty;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "PowerPagesTemplate_" + Guid.NewGuid().ToString("N"));
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });

            var runDir = Path.Combine(_baseDir, "runs", "pp");
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "intermediate.json"), Intermediate().ToString());
            var job = ExportJob.Load(Path.Combine(_baseDir, "config", "PowerPages.context-exporter-config.json"));
            new TemplateRenderer(_ => { }).Render(job, _baseDir, runDir);
            _output = File.ReadAllText(Path.Combine(runDir, "output.md"));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        [Test]
        public void PageMap_PathsHostsAccessAndCode()
        {
            Assert.That(_output, Does.Contain("| `/` | Home | Default | Published | Yes |"));
            Assert.That(_output, Does.Contain("| `/cases` | Cases | — | Published | Yes | List: Case List | Restrict read (Partner) | JS | English |"));
            Assert.That(_output, Does.Contain("| `/cases/new` | New Case | — | Published | Hidden | Basic form: New Case |"));
        }

        [Test]
        public void WebRoles_AndWhatTheyCanDo()
        {
            Assert.That(_output, Does.Contain("| Authenticated Users | Authenticated users | 1 | 0 | 0 |"));
            Assert.That(_output, Does.Contain("| Partner | — (assigned to contacts) | 0 | 1 | 1 |"));
            Assert.That(_output, Does.Contain("- **Authenticated Users** — `incident` Contact: Read, Write"));
            Assert.That(_output, Does.Contain("- **Partner** — no table permissions"));
        }

        [Test]
        public void TablePermissions_ScopeRelationshipAndParent()
        {
            Assert.That(_output, Does.Contain("| Cases - Contact | `incident` | Contact | Read, Write | Authenticated Users | contact_as_customer |"));
            Assert.That(_output, Does.Contain("| Notes | `annotation` | Parent | Read | (through parent) | Parent: Cases - Contact via Incident_Annotation |"));
        }

        [Test]
        public void FormsListsAndMultistepForms()
        {
            Assert.That(_output, Does.Contain("| New Case | `incident` | Portal Case | Insert | Enforced | New Case | Yes (2 lines) |"));
            Assert.That(_output, Does.Contain("| Case List | `incident` | New Case | — | Yes | **Not enforced** | Cases | — |"));
            Assert.That(_output, Does.Contain("| 1 | Start | Load form | `con_claim` | Claim Start | Insert | — |"));
            Assert.That(_output, Does.Contain("| 2 | Details | Load tab | — | — | — | — |"));
        }

        [Test]
        public void CloudFlowsAndWebApi()
        {
            Assert.That(_output, Does.Contain("| Get Claims | Partner |"));
            Assert.That(_output, Does.Contain("| `incident` | true | title,description |"));
        }

        [Test]
        public void SiteSettings_SecretsAreNotExported()
        {
            Assert.That(_output, Does.Contain("| `Authentication/Registration/Enabled` | true |"));
            Assert.That(_output, Does.Contain("| `Custom/ApiKeyHint` | (value withheld) |"), "masked in the template even if a sensitive setting slips past the query");
            Assert.That(_output, Does.Not.Contain("do-not-print"));
            Assert.That(_output, Does.Contain("**Values not exported** (names suggest secrets): Authentication/OpenIdConnect/Azure/ClientSecret"));
        }

        [Test]
        public void TemplatesContentAndNavigation()
        {
            Assert.That(_output, Does.Contain("| Default | Web template | Layout 1 Column | 1 |"));
            Assert.That(_output, Does.Contain("| Layout 1 Column | Default |"));
            Assert.That(_output, Does.Contain("**Content snippets:** Footer"));
            Assert.That(_output, Does.Contain("**Web files:** 1 — logo.png"));
            Assert.That(_output, Does.Contain("- **Primary Navigation** (1 links): Cases → Cases"));
        }

        private static JObject Intermediate() => new JObject
        {
            ["_meta"] = new JObject
            {
                ["exportedAtUtc"] = "2026-01-01T00:00:00Z",
                ["environment"] = new JObject { ["url"] = "https://contoso.example", ["orgName"] = "contoso" },
                ["spec"] = "PowerPages",
                ["frontMatter"] = new JObject { ["generatedBy"] = "test" },
            },
            ["ppSites"] = new JArray
            {
                new JObject { ["powerpagesiteid"] = Site, ["name"] = "Contoso Portal", ["primarydomainname"] = "contoso.example", ["datamodelversion"] = "2.0" },
            },
            ["ppComponents"] = new JArray
            {
                Component(Published, "Published", 1, new JObject()),
                Component(RoleAuthenticated, "Authenticated Users", 11, new JObject { ["anonymoususersrole"] = false, ["authenticatedusersrole"] = true }),
                Component(RolePartner, "Partner", 11, new JObject { ["anonymoususersrole"] = false, ["authenticatedusersrole"] = false }),
                Component(Home, "Home", 2, new JObject { ["isroot"] = true, ["partialurl"] = "/", ["pagetemplateid"] = PageTemplate, ["publishingstateid"] = Published, ["hiddenfromsitemap"] = false }),
                Component(Cases, "Cases", 2, new JObject { ["isroot"] = true, ["partialurl"] = "cases", ["parentpageid"] = Home, ["publishingstateid"] = Published, ["entitylist"] = List, ["hiddenfromsitemap"] = false }),
                Component(CasesContent, "Cases", 2, new JObject { ["isroot"] = false, ["rootwebpageid"] = Cases, ["customjavascript"] = "console.log(1);" }, "English"),
                Component(NewCase, "New Case", 2, new JObject { ["isroot"] = true, ["partialurl"] = "new", ["parentpageid"] = Cases, ["publishingstateid"] = Published, ["entityform"] = BasicForm, ["hiddenfromsitemap"] = true }),
                Component("50000000-0000-0000-0000-000000000025", "Restrict Cases", 10, new JObject { ["right"] = 2, ["scope"] = 1, ["webpageid"] = Cases, ["adx_webpageaccesscontrolrule_webrole"] = new JArray(RolePartner) }),
                Component(PermCases, "Cases - Contact", 18, new JObject { ["entitylogicalname"] = "incident", ["scope"] = 756150001, ["read"] = true, ["write"] = true, ["contactrelationship"] = "contact_as_customer", ["adx_entitypermission_webrole"] = new JArray(RoleAuthenticated) }),
                Component(PermNotes, "Notes", 18, new JObject { ["entitylogicalname"] = "annotation", ["scope"] = 756150003, ["read"] = true, ["parententitypermission"] = PermCases, ["parentrelationship"] = "Incident_Annotation", ["adx_entitypermission_webrole"] = new JArray() }),
                Component(BasicForm, "New Case", 15, new JObject { ["entityname"] = "incident", ["formname"] = "Portal Case", ["mode"] = 100000000, ["entitypermissionsenabled"] = true, ["registerstartupscript"] = "$(function () {\n});" }),
                Component(List, "Case List", 17, new JObject { ["entityname"] = "incident", ["webpagefordetailsview"] = NewCase, ["searchenabled"] = true, ["entitypermissionsenabled"] = false }),
                Component(MultiForm, "Claim", 19, new JObject { ["startstep"] = StepA, ["authenticationrequired"] = true }),
                Component(StepB, "Details", 20, new JObject { ["type"] = 100000002, ["webform"] = MultiForm }),
                Component(StepA, "Start", 20, new JObject { ["type"] = 100000001, ["targetentitylogicalname"] = "con_claim", ["formname"] = "Claim Start", ["mode"] = 100000000, ["webform"] = MultiForm, ["nextstep"] = StepB }),
                Component("50000000-0000-0000-0000-000000000046", "Get Claims", 33, new JObject { ["processid"] = "50000000-0000-0000-0000-000000000099", ["adx_CloudFlowConsumer_adx_webrole"] = new JArray(RolePartner) }),
                Component("50000000-0000-0000-0000-000000000071", "Webapi/incident/enabled", 9, new JObject { ["value"] = "true" }),
                Component("50000000-0000-0000-0000-000000000072", "Webapi/incident/fields", 9, new JObject { ["value"] = "title,description" }),
                Component("50000000-0000-0000-0000-000000000073", "Authentication/Registration/Enabled", 9, new JObject { ["value"] = "true" }),
                Component("50000000-0000-0000-0000-000000000074", "Custom/ApiKeyHint", 9, new JObject { ["value"] = "do-not-print" }),
                Component(PageTemplate, "Default", 6, new JObject { ["type"] = 756150001, ["webtemplateid"] = WebTemplate }),
                Component("50000000-0000-0000-0000-000000000081", "Footer", 7, new JObject { ["value"] = "<p>Footer</p>" }),
                Component(LinkSet, "Primary Navigation", 4, new JObject()),
                Component("50000000-0000-0000-0000-000000000062", "Cases", 5, new JObject { ["weblinksetid"] = LinkSet, ["pageid"] = Cases }),
            },
            ["ppComponentNames"] = new JArray
            {
                Component(WebTemplate, "Layout 1 Column", 8, null),
                Component("50000000-0000-0000-0000-000000000091", "logo.png", 3, null),
                Component("50000000-0000-0000-0000-000000000092", "Authentication/OpenIdConnect/Azure/ClientSecret", 9, null),
            },
        };

        private static JObject Component(string id, string name, int type, JObject? content, string? language = null)
        {
            var row = new JObject
            {
                ["_id"] = id,
                ["powerpagecomponentid"] = id,
                ["name"] = name,
                ["powerpagecomponenttype"] = type,
                ["powerpagesiteid"] = new JObject { ["id"] = Site, ["logicalName"] = "powerpagesite", ["name"] = "Contoso Portal" },
            };
            if (language != null)
                row["powerpagesitelanguageid"] = new JObject { ["id"] = "50000000-0000-0000-0000-0000000000aa", ["logicalName"] = "powerpagesitelanguage", ["name"] = language };
            if (content != null)
                row["content"] = content.ToString(Formatting.None);
            return row;
        }

        private sealed class NullWin32Window : IWin32Window
        {
            public IntPtr Handle => IntPtr.Zero;
        }
    }
}
