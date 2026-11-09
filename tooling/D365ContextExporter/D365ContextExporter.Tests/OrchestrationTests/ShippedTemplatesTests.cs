using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Lspiguel.Xrm.D365ContextExporter.Helpers;
using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Orchestration;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests
{
    /// <summary>
    /// Renders every shipped spec through its shipped template, deployed from the embedded reference configuration,
    /// against one synthetic intermediate that carries every result key the specs use. Pins the customization scope
    /// (which tables, forms, views and roles are included) and the type labels.
    /// </summary>
    [TestFixture]
    public class ShippedTemplatesTests
    {
        private const string ProjectTableId = "11111111-0000-0000-0000-000000000001";
        private const string AccountId = "11111111-0000-0000-0000-000000000002";
        private const string ContactId = "11111111-0000-0000-0000-000000000003";
        private const string UntouchedId = "11111111-0000-0000-0000-000000000004";
        private const string BpfId = "22222222-0000-0000-0000-000000000001";

        // Synthetic data in the shapes the query runners produce: Web API metadata keeps its casing;
        // FetchXML rows are flat, with link-entity columns keyed alias.column.
        private const string Intermediate = @"{
  ""_meta"": { ""exportedAtUtc"": ""2026-01-01T00:00:00Z"", ""environment"": { ""url"": ""https://contoso.example"", ""orgName"": ""contoso"" }, ""spec"": ""Test"", ""frontMatter"": { ""generatedBy"": ""test"" } },
  ""solutions"": [
    { ""friendlyname"": ""Contoso Base"", ""uniquename"": ""ContosoBase"", ""ismanaged"": false, ""version"": ""1.0.0.0"", ""installedon"": ""2026-01-02T00:00:00Z"", ""publisherfriendlyname"": ""Contoso"", ""publisheruniquename"": ""contoso"" },
    { ""friendlyname"": ""Sales"", ""uniquename"": ""msdynce_Sales"", ""ismanaged"": true, ""version"": ""9.0.0.0"", ""publisherfriendlyname"": ""Dynamics 365"", ""publisheruniquename"": ""microsoftdynamics"" }
  ],
  ""entities"": [
    { ""MetadataId"": """ + ProjectTableId + @""", ""LogicalName"": ""con_project"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Project"" } }, ""IsCustomEntity"": true, ""IsManaged"": false, ""IsIntersect"": false, ""TableType"": ""Standard"", ""PrimaryIdAttribute"": ""con_projectid"", ""PrimaryNameAttribute"": ""con_name"", ""OwnershipType"": ""UserOwned"",
      ""Attributes"": [
        { ""LogicalName"": ""con_name"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Name"" } }, ""AttributeType"": ""String"", ""AttributeTypeName"": { ""Value"": ""StringType"" }, ""RequiredLevel"": { ""Value"": ""ApplicationRequired"" }, ""IsCustomAttribute"": true, ""IsManaged"": false },
        { ""LogicalName"": ""con_tags"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Tags"" } }, ""AttributeType"": ""Virtual"", ""AttributeTypeName"": { ""Value"": ""MultiSelectPicklistType"" }, ""RequiredLevel"": { ""Value"": ""Recommended"" }, ""IsCustomAttribute"": true, ""IsManaged"": false },
        { ""LogicalName"": ""con_budget_base"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Budget (Base)"" } }, ""AttributeType"": ""Money"", ""AttributeTypeName"": { ""Value"": ""MoneyType"" }, ""RequiredLevel"": { ""Value"": ""None"" }, ""IsCustomAttribute"": true, ""IsManaged"": false }
      ] },
    { ""MetadataId"": """ + AccountId + @""", ""LogicalName"": ""account"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Account"" } }, ""IsCustomEntity"": false, ""IsManaged"": true, ""IsIntersect"": false, ""TableType"": ""Standard"",
      ""Attributes"": [
        { ""LogicalName"": ""con_tier"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Tier"" } }, ""AttributeType"": ""Picklist"", ""AttributeTypeName"": { ""Value"": ""PicklistType"" }, ""RequiredLevel"": { ""Value"": ""None"" }, ""IsCustomAttribute"": true, ""IsManaged"": false }
      ] },
    { ""MetadataId"": """ + ContactId + @""", ""LogicalName"": ""contact"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Contact"" } }, ""IsCustomEntity"": false, ""IsManaged"": true, ""IsIntersect"": false, ""TableType"": ""Standard"", ""Attributes"": [] },
    { ""MetadataId"": """ + UntouchedId + @""", ""LogicalName"": ""msdyn_untouched"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Untouched"" } }, ""IsCustomEntity"": true, ""IsManaged"": true, ""IsIntersect"": false, ""TableType"": ""Standard"", ""Attributes"": [] },
    { ""MetadataId"": ""11111111-0000-0000-0000-000000000005"", ""LogicalName"": ""con_vt_feed"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Feed"" } }, ""IsCustomEntity"": true, ""IsManaged"": false, ""IsIntersect"": false, ""TableType"": ""Virtual"", ""Attributes"": [] },
    { ""MetadataId"": ""11111111-0000-0000-0000-000000000006"", ""LogicalName"": ""con_account_contact"", ""DisplayName"": null, ""IsCustomEntity"": true, ""IsManaged"": false, ""IsIntersect"": true, ""TableType"": ""Standard"", ""Attributes"": [] }
  ],
  ""projectTables"": [ { ""objectid"": """ + ContactId + @""" } ],
  ""forms"": [
    { ""name"": ""Project Main"", ""type"": 2, ""objecttypecode"": ""con_project"", ""isdefault"": true, ""ismanaged"": false },
    { ""name"": ""Contact QV"", ""type"": 6, ""objecttypecode"": ""contact"", ""isdefault"": false, ""ismanaged"": true }
  ],
  ""views"": [
    { ""name"": ""Active Projects"", ""querytype"": 0, ""returnedtypecode"": ""con_project"", ""isdefault"": true, ""ismanaged"": false },
    { ""name"": ""Project Lookup"", ""querytype"": 64, ""returnedtypecode"": ""con_project"", ""isdefault"": false, ""ismanaged"": false }
  ],
  ""globalOptionSets"": [
    { ""Name"": ""con_status"", ""DisplayName"": { ""UserLocalizedLabel"": { ""Label"": ""Status"" } }, ""OptionSetType"": ""Picklist"",
      ""Options"": [ { ""Value"": 2, ""Label"": { ""UserLocalizedLabel"": { ""Label"": ""Second"" } } }, { ""Value"": 1, ""Label"": { ""UserLocalizedLabel"": { ""Label"": ""First"" } } } ] }
  ],
  ""pluginAssemblies"": [ { ""name"": ""Contoso.Plugins"", ""pt.typename"": ""Contoso.Plugins.ProjectPlugin"" } ],
  ""sdkSteps"": [ { ""name"": ""Contoso.Plugins.ProjectPlugin: Update of con_project"", ""flt.primaryobjecttypecode"": ""con_project"", ""msg.name"": ""Update"", ""stage"": 40, ""mode"": 0, ""statecode"": 1, ""filteringattributes"": ""con_name"" } ],
  ""customApiRequests"": [ { ""uniquename"": ""con_Recalculate"", ""req.uniquename"": ""Target"", ""req.type"": 5 } ],
  ""customApiResponses"": [ { ""uniquename"": ""con_Recalculate"", ""resp.uniquename"": ""Total"", ""resp.type"": 2 } ],
  ""workflows"": [
    { ""name"": ""Project - Notify"", ""category"": 0, ""primaryentity"": ""con_project"", ""statecode"": 0, ""triggeroncreate"": true },
    { ""name"": ""Project rule"", ""category"": 2, ""primaryentity"": ""con_project"", ""statecode"": 1 },
    { ""name"": ""Project flow"", ""category"": 5, ""primaryentity"": ""none"", ""statecode"": 1 }
  ],
  ""businessProcessFlows"": [ { ""workflowid"": """ + BpfId + @""", ""name"": ""Project Process"", ""primaryentity"": ""con_project"", ""statuscode"": 1 } ],
  ""bpfStages"": [ { ""stagename"": ""Plan"", ""processid"": { ""id"": """ + BpfId + @""", ""logicalName"": ""workflow"", ""name"": ""Project Process"" }, ""primaryentitytypecode"": ""con_project"" } ],
  ""appModules"": [ { ""name"": ""Contoso App"", ""uniquename"": ""con_App"", ""ismanaged"": false } ],
  ""envVars"": [ { ""schemaname"": ""con_ApiUrl"", ""displayname"": ""API URL"", ""type"": 100000000 } ],
  ""securityRoles"": [
    { ""name"": ""Contoso Manager"", ""rp.privilegedepthmask"": 8, ""priv.name"": ""prvReadcon_project"" },
    { ""name"": ""Contoso Manager"", ""rp.privilegedepthmask"": 1, ""priv.name"": ""prvWritecon_project"" }
  ],
  ""flows"": [ { ""name"": ""Project flow"", ""statecode"": 1, ""primaryentity"": ""none"", ""ismanaged"": false, ""clientdata"": ""{}"", ""createdon"": ""2026-01-01T00:00:00Z"", ""modifiedon"": ""2026-01-01T00:00:00Z"" } ]
}";

        private string _baseDir = string.Empty;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "ShippedTemplates_" + Guid.NewGuid().ToString("N"));
            FirstRunHelper.DeployReferenceConfig(_baseDir, new NullWin32Window(), _ => { });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        [Test]
        public void EveryShippedSpec_RendersAgainstSyntheticData()
        {
            var specs = PathResolver.DiscoverSpecConfigs(_baseDir).ToList();
            Assert.That(specs, Has.Count.EqualTo(7));

            foreach (var spec in specs)
            {
                var job = ExportJob.Load(spec);
                Assert.DoesNotThrow(() => Render(job), job.Spec);
            }
        }

        [Test]
        public void SolutionsReference_IncludesProjectAndCustomizedTablesOnly()
        {
            var output = RenderSpec("SolutionsReference");

            Assert.That(output, Does.Contain("### con_project — \"Project\""), "unmanaged table");
            Assert.That(output, Does.Contain("### account — \"Account\""), "managed table with project columns");
            Assert.That(output, Does.Contain("### contact — \"Contact\""), "managed table in a project solution");
            Assert.That(output, Does.Not.Contain("msdyn_untouched"), "managed table nobody customized");
            Assert.That(output, Does.Not.Contain("### con_account_contact"), "intersect table");
            Assert.That(output.IndexOf("### con_vt_feed", StringComparison.Ordinal), Is.GreaterThan(output.IndexOf("## Virtual Tables", StringComparison.Ordinal)), "virtual table in its own section");
        }

        [Test]
        public void SolutionsReference_ColumnsFormsAndViews()
        {
            var output = RenderSpec("SolutionsReference");

            Assert.That(output, Does.Contain("| con_tags | Tags | multiopt | r |"), "multi-select type from AttributeTypeName");
            Assert.That(output, Does.Contain("| con_name | Name | str | **R** |"));
            Assert.That(output, Does.Not.Contain("con_budget_base"), "currency base column");
            Assert.That(output, Does.Contain("**Forms:** Project Main (main)"));
            Assert.That(output, Does.Contain("**Views:** Active Projects, Project Lookup (lookup)"));
            Assert.That(output, Does.Contain("**Forms:** Contact QV (quick view)"));
            Assert.That(output, Does.Contain("_No project columns._"), "contact has no project columns");
        }

        [Test]
        public void SolutionsReference_SolutionsProcessesAndState()
        {
            var output = RenderSpec("SolutionsReference");

            Assert.That(output, Does.Contain("| Contoso Base | `ContosoBase` | Contoso | 1.0.0.0 | 2026-01-02 |"), "publisher shown");
            Assert.That(output, Does.Not.Contain("msdynce_Sales"), "managed solutions are not project solutions");
            Assert.That(output, Does.Contain("| con_name | **Disabled** |"), "disabled step");
            Assert.That(output, Does.Contain("| Project - Notify | Create | **Draft** |"), "draft classic workflow");
            Assert.That(output, Does.Contain("Second (2), First (1)"), "options in defined order");
            Assert.That(output, Does.Contain("- Contoso Manager"));
        }

        [Test]
        public void FormsAndViews_UsesDocumentedTypeLabels()
        {
            var output = RenderSpec("FormsAndViews");

            Assert.That(output, Does.Contain("| Project Main | Main | ✓ | No |"));
            Assert.That(output, Does.Contain("| Contact QV | Quick View |  | Yes |"));
            Assert.That(output, Does.Contain("| Active Projects | Public | ✓ | No |"));
            Assert.That(output, Does.Contain("| Project Lookup | Lookup |  | No |"));
        }

        [Test]
        public void EntityDictionary_IncludesCustomizedTablesWithColumnTypes()
        {
            var output = RenderSpec("EntityDictionary");

            Assert.That(output, Does.Contain("## con_project"));
            Assert.That(output, Does.Contain("## account"));
            Assert.That(output, Does.Contain("## contact"));
            Assert.That(output, Does.Not.Contain("## msdyn_untouched"));
            Assert.That(output, Does.Contain("| `con_tags` | Tags | MultiSelectPicklist | r | ✓ | ✓ |"));
        }

        [Test]
        public void SecurityModel_ListsEachRoleOnceWithDepth()
        {
            var output = RenderSpec("SecurityModel");

            Assert.That(output.Split(new[] { "## Contoso Manager" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(output, Does.Contain("| prvReadcon_project | Organization |"));
            Assert.That(output, Does.Contain("| prvWritecon_project | User |"));
        }

        [Test]
        public void SolutionInventory_ShowsPublisher()
        {
            var output = RenderSpec("SolutionInventory");

            Assert.That(output, Does.Contain("| Contoso Base | Contoso | 1.0.0.0 | No | 2026-01-02 |"));
            Assert.That(output, Does.Contain("**Publisher:** Contoso (`contoso`)"));
        }

        [Test]
        public void Optionsets_KeepsDefinedOrder()
        {
            var output = RenderSpec("Optionsets");

            Assert.That(output.IndexOf("| 2 | Second |", StringComparison.Ordinal), Is.LessThan(output.IndexOf("| 1 | First |", StringComparison.Ordinal)));
        }

        private string RenderSpec(string spec) =>
            Render(ExportJob.Load(Path.Combine(_baseDir, "config", $"{spec}.context-exporter-config.json")));

        private string Render(ExportJob job)
        {
            var runDir = Path.Combine(_baseDir, "runs", job.Spec);
            Directory.CreateDirectory(runDir);
            // EntityDictionary names its table result entityAttributes; the other specs name it entities.
            var data = JObject.Parse(Intermediate);
            data["entityAttributes"] = data["entities"]!.DeepClone();
            File.WriteAllText(Path.Combine(runDir, "intermediate.json"), data.ToString());
            new TemplateRenderer(_ => { }).Render(job, _baseDir, runDir);
            return File.ReadAllText(Path.Combine(runDir, "output.md"));
        }

        private sealed class NullWin32Window : IWin32Window
        {
            public IntPtr Handle => IntPtr.Zero;
        }
    }
}
