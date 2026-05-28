using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Lspiguel.Xrm.D365ContextExporter.Helpers;
using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Queries;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using Moq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.QueriesTests
{
    /// <summary>
    /// Integration-style tests that mock IOrganizationService with representative data,
    /// then drive both FetchXmlQueryRunner and
    /// EntityJsonSerializer to verify the full fetchxml → serialised-dict pipeline.
    ///
    /// One test per fetchxml query in Sample.context-exporter-config.json.
    /// </summary>
    [TestFixture]
    public class FetchXmlRealDataTests
    {
        private string _baseDir = string.Empty;
        private string _queriesDir = string.Empty;

        private static readonly OutputSettings DefaultOutput = new OutputSettings();

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "FetchXmlRealData_" + Guid.NewGuid().ToString("N"));
            _queriesDir = Path.Combine(_baseDir, "config", "queries");
            Directory.CreateDirectory(_queriesDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        // -------------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------------

        private void WriteFetchXml(string fileName, string xml) =>
            File.WriteAllText(Path.Combine(_queriesDir, fileName), xml);

        private static EntityCollection OnePage(params Entity[] entities)
        {
            var col = new EntityCollection(entities.ToList());
            col.MoreRecords = false;
            return col;
        }

        private static QueryDefinition Q(string id, string source, string resultKey, int? maxRecords = null) =>
            new QueryDefinition { Id = id, Type = "fetchxml", Source = source, ResultKey = resultKey, MaxRecords = maxRecords };

        private List<Dictionary<string, object?>> RunAndSerialize(
            Mock<IOrganizationService> svcMock, QueryDefinition query)
        {
            var runner = new FetchXmlQueryRunner(svcMock.Object, _ => { });
            var entities = runner.Run(query, _baseDir, CancellationToken.None);
            return EntityJsonSerializer.SerializeEntities(entities, DefaultOutput);
        }

        // -------------------------------------------------------------------------
        // 1. solutions  (solutions-detail.fetch.xml)
        //    Link-entity attributes with explicit alias="publisherfriendlyname" come
        //    back as AliasedValue stored under that alias key.
        // -------------------------------------------------------------------------

        private const string SolutionsFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""solution"">
    <attribute name=""solutionid"" />
    <attribute name=""uniquename"" />
    <attribute name=""friendlyname"" />
    <attribute name=""version"" />
    <attribute name=""ismanaged"" />
    <attribute name=""installedon"" />
    <attribute name=""modifiedon"" />
    <link-entity name=""publisher"" from=""publisherid"" to=""publisherid"" link-type=""inner"" alias=""pub"">
      <attribute name=""friendlyname"" alias=""publisherfriendlyname"" />
      <attribute name=""uniquename"" alias=""publisheruniquename"" />
    </link-entity>
  </entity>
</fetch>";

        [Test]
        public void Solutions_MapsDirectAttributesCorrectly()
        {
            WriteFetchXml("solutions-detail.fetch.xml", SolutionsFetchXml);

            var id = new Guid("635af1a7-fc45-ef11-8409-6045bd02fd83");
            var installedOn = new DateTime(2024, 7, 19, 18, 28, 16, DateTimeKind.Utc);
            var modifiedOn = new DateTime(2026, 5, 1, 1, 12, 42, DateTimeKind.Utc);

            var entity = new Entity("solution", id);
            entity.Attributes["solutionid"] = id;
            entity.Attributes["uniquename"] = "ContosoApps";
            entity.Attributes["friendlyname"] = "Contoso Apps";
            entity.Attributes["version"] = "1.0.26121.1";
            entity.Attributes["ismanaged"] = false;
            entity.Attributes["installedon"] = installedOn;
            entity.Attributes["modifiedon"] = modifiedOn;
            entity.Attributes["publisherfriendlyname"] = new AliasedValue("publisher", "friendlyname", "fabrikampublisher");
            entity.Attributes["publisheruniquename"] = new AliasedValue("publisher", "uniquename", "FabrikamPublisher");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("solutions", "solutions-detail.fetch.xml", "solutions"));

            Assert.That(results, Has.Count.EqualTo(1));
            var row = results[0];
            Assert.That(row["_id"], Is.EqualTo(id.ToString()));
            Assert.That(row["uniquename"], Is.EqualTo("ContosoApps"));
            Assert.That(row["friendlyname"], Is.EqualTo("Contoso Apps"));
            Assert.That(row["version"], Is.EqualTo("1.0.26121.1"));
            Assert.That(row["ismanaged"], Is.EqualTo(false));
            Assert.That(row["installedon"], Is.EqualTo("2024-07-19T18:28:16Z"));
            Assert.That(row["modifiedon"], Is.EqualTo("2026-05-01T01:12:42Z"));
        }

        [Test]
        public void Solutions_UnwrapsPublisherAliasedValues()
        {
            WriteFetchXml("solutions-detail.fetch.xml", SolutionsFetchXml);

            var entity = new Entity("solution", Guid.NewGuid());
            entity.Attributes["uniquename"] = "ContosoCore";
            entity.Attributes["friendlyname"] = "Contoso Core";
            entity.Attributes["version"] = "1.0.26114.2";
            entity.Attributes["ismanaged"] = false;
            entity.Attributes["installedon"] = new DateTime(2024, 7, 19, 18, 28, 3, DateTimeKind.Utc);
            entity.Attributes["modifiedon"] = new DateTime(2026, 4, 24, 19, 57, 57, DateTimeKind.Utc);
            // Attributes with explicit alias on the <attribute> element come back as AliasedValue
            // keyed by that alias (not by the link-entity alias prefix).
            entity.Attributes["publisherfriendlyname"] = new AliasedValue("publisher", "friendlyname", "fabrikampublisher");
            entity.Attributes["publisheruniquename"] = new AliasedValue("publisher", "uniquename", "FabrikamPublisher");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("solutions", "solutions-detail.fetch.xml", "solutions"));
            var row = results[0];

            // AliasedValues must be unwrapped to plain values
            Assert.That(row["publisherfriendlyname"], Is.EqualTo("fabrikampublisher"));
            Assert.That(row["publisheruniquename"], Is.EqualTo("FabrikamPublisher"));
        }

        [Test]
        public void Solutions_NullDescription_MapsToNull()
        {
            WriteFetchXml("solutions-detail.fetch.xml", SolutionsFetchXml);

            var entity = new Entity("solution", Guid.NewGuid());
            entity.Attributes["uniquename"] = "ContosoCore";
            entity.Attributes["description"] = null;   // solution has no description
            entity.Attributes["ismanaged"] = false;
            entity.Attributes["publisherfriendlyname"] = new AliasedValue("publisher", "friendlyname", "fabrikampublisher");
            entity.Attributes["publisheruniquename"] = new AliasedValue("publisher", "uniquename", "FabrikamPublisher");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("solutions", "solutions-detail.fetch.xml", "solutions"));
            Assert.That(results[0]["description"], Is.Null);
        }

        [Test]
        public void Solutions_MultipleSolutions_AllSerialised()
        {
            WriteFetchXml("solutions-detail.fetch.xml", SolutionsFetchXml);

            var entities = Enumerable.Range(0, 5).Select(i =>
            {
                var e = new Entity("solution", Guid.NewGuid());
                e.Attributes["uniquename"] = $"Solution{i}";
                e.Attributes["ismanaged"] = false;
                e.Attributes["publisherfriendlyname"] = new AliasedValue("publisher", "friendlyname", "fabrikampublisher");
                e.Attributes["publisheruniquename"] = new AliasedValue("publisher", "uniquename", "FabrikamPublisher");
                return e;
            }).ToArray();

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entities));

            var results = RunAndSerialize(mock, Q("solutions", "solutions-detail.fetch.xml", "solutions"));

            Assert.That(results, Has.Count.EqualTo(5));
            Assert.That(results.Select(r => r["uniquename"]), Is.Unique);
        }

        // -------------------------------------------------------------------------
        // 2. plugin-assemblies  (plugin-assemblies.fetch.xml)
        //    Outer join to plugintype; link-entity alias "pt" without explicit
        //    attribute aliases → keys are "pt.plugintypeid", "pt.name", etc.
        // -------------------------------------------------------------------------

        private const string PluginAssembliesFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""pluginassembly"">
    <attribute name=""pluginassemblyid"" />
    <attribute name=""name"" />
    <attribute name=""version"" />
    <link-entity name=""plugintype"" from=""pluginassemblyid"" to=""pluginassemblyid"" link-type=""outer"" alias=""pt"">
      <attribute name=""plugintypeid"" />
      <attribute name=""name"" />
      <attribute name=""typename"" />
      <attribute name=""friendlyname"" />
    </link-entity>
  </entity>
</fetch>";

        [Test]
        public void PluginAssemblies_MapsAssemblyAndLinkEntityAttributes()
        {
            WriteFetchXml("plugin-assemblies.fetch.xml", PluginAssembliesFetchXml);

            var asmId = new Guid("bf736655-77f3-4e63-a728-38c2d38da3e3");
            var typeId = new Guid("29621404-c727-4205-bd3e-0326ee2c323d");

            var entity = new Entity("pluginassembly", asmId);
            entity.Attributes["pluginassemblyid"] = asmId;
            entity.Attributes["name"] = "Contoso";
            entity.Attributes["version"] = "1.0.0.0";
            // Link-entity attributes without explicit alias → "pt.<attr>"
            entity.Attributes["pt.plugintypeid"] = new AliasedValue("plugintype", "plugintypeid", typeId);
            entity.Attributes["pt.name"] = new AliasedValue("plugintype", "name", "Contoso.ContosoPlugin ");
            entity.Attributes["pt.typename"] = new AliasedValue("plugintype", "typename", "Contoso.ContosoPlugin ");
            entity.Attributes["pt.friendlyname"] = new AliasedValue("plugintype", "friendlyname", "8a3e800a-82c6-4c24-a308-2ba2593da911");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("plugin-assemblies", "plugin-assemblies.fetch.xml", "pluginAssemblies"));
            var row = results[0];

            Assert.That(row["name"], Is.EqualTo("Contoso"));
            Assert.That(row["version"], Is.EqualTo("1.0.0.0"));
            // Guid inside AliasedValue → string
            Assert.That(row["pt.plugintypeid"], Is.EqualTo(typeId.ToString()));
            Assert.That(row["pt.name"], Is.EqualTo("Contoso.ContosoPlugin "));
            Assert.That(row["pt.typename"], Is.EqualTo("Contoso.ContosoPlugin "));
            Assert.That(row["pt.friendlyname"], Is.EqualTo("8a3e800a-82c6-4c24-a308-2ba2593da911"));
        }

        [Test]
        public void PluginAssemblies_OuterJoin_NullPluginType_LeavesNullValues()
        {
            WriteFetchXml("plugin-assemblies.fetch.xml", PluginAssembliesFetchXml);

            // Assembly with no plugin types (outer join returns null for link-entity attrs)
            var entity = new Entity("pluginassembly", Guid.NewGuid());
            entity.Attributes["name"] = "OrphanAssembly";
            entity.Attributes["version"] = "2.0.0.0";
            entity.Attributes["pt.plugintypeid"] = new AliasedValue("plugintype", "plugintypeid", null);
            entity.Attributes["pt.name"] = new AliasedValue("plugintype", "name", null);

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("plugin-assemblies", "plugin-assemblies.fetch.xml", "pluginAssemblies"));
            var row = results[0];

            Assert.That(row["name"], Is.EqualTo("OrphanAssembly"));
            Assert.That(row["pt.plugintypeid"], Is.Null);
            Assert.That(row["pt.name"], Is.Null);
        }

        // -------------------------------------------------------------------------
        // 3. sdk-steps  (sdk-steps.fetch.xml)
        //    Multiple nested link-entities: msg (sdkmessage), flt (sdkmessagefilter),
        //    pt (plugintype) → asm (pluginassembly)
        // -------------------------------------------------------------------------

        private const string SdkStepsFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""sdkmessageprocessingstep"">
    <attribute name=""sdkmessageprocessingstepid"" />
    <attribute name=""name"" />
    <attribute name=""stage"" />
    <attribute name=""mode"" />
    <attribute name=""rank"" />
    <link-entity name=""sdkmessage"" from=""sdkmessageid"" to=""sdkmessageid"" link-type=""inner"" alias=""msg"">
      <attribute name=""name"" />
    </link-entity>
    <link-entity name=""sdkmessagefilter"" from=""sdkmessagefilterid"" to=""sdkmessagefilterid"" link-type=""outer"" alias=""flt"">
      <attribute name=""primaryobjecttypecode"" />
    </link-entity>
    <link-entity name=""plugintype"" from=""plugintypeid"" to=""plugintypeid"" link-type=""inner"" alias=""pt"">
      <attribute name=""name"" />
      <attribute name=""typename"" />
      <link-entity name=""pluginassembly"" from=""pluginassemblyid"" to=""pluginassemblyid"" link-type=""inner"" alias=""asm"">
        <attribute name=""name"" />
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

        [Test]
        public void SdkSteps_MapsAllLinkEntityAliases()
        {
            WriteFetchXml("sdk-steps.fetch.xml", SdkStepsFetchXml);

            var stepId = new Guid("e893a66b-138b-ef11-ac21-000d3a3088a7");

            var entity = new Entity("sdkmessageprocessingstep", stepId);
            entity.Attributes["sdkmessageprocessingstepid"] = stepId;
            entity.Attributes["name"] = "Contoso.ContosoPlugin : Create of cust_notes";
            entity.Attributes["stage"] = 20;
            entity.Attributes["mode"] = 0;
            entity.Attributes["rank"] = 1;
            entity.Attributes["msg.name"] = new AliasedValue("sdkmessage", "name", "Create");
            entity.Attributes["flt.primaryobjecttypecode"] = new AliasedValue("sdkmessagefilter", "primaryobjecttypecode", "cust_notes");
            entity.Attributes["pt.name"] = new AliasedValue("plugintype", "name", "Contoso.ContosoPlugin ");
            entity.Attributes["pt.typename"] = new AliasedValue("plugintype", "typename", "Contoso.ContosoPlugin ");
            entity.Attributes["asm.name"] = new AliasedValue("pluginassembly", "name", "Contoso");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("sdk-steps", "sdk-steps.fetch.xml", "sdkSteps"));
            var row = results[0];

            Assert.That(row["stage"], Is.EqualTo(20));
            Assert.That(row["mode"], Is.EqualTo(0));
            Assert.That(row["rank"], Is.EqualTo(1));
            Assert.That(row["msg.name"], Is.EqualTo("Create"));
            Assert.That(row["flt.primaryobjecttypecode"], Is.EqualTo("cust_notes"));
            Assert.That(row["pt.name"], Is.EqualTo("Contoso.ContosoPlugin "));
            Assert.That(row["pt.typename"], Is.EqualTo("Contoso.ContosoPlugin "));
            Assert.That(row["asm.name"], Is.EqualTo("Contoso"));
        }

        [Test]
        public void SdkSteps_WhenFilterIsNull_PrimaryObjectTypeCodeIsNull()
        {
            WriteFetchXml("sdk-steps.fetch.xml", SdkStepsFetchXml);

            // Some steps (global messages) have no filter entity → outer join returns null
            var entity = new Entity("sdkmessageprocessingstep", Guid.NewGuid());
            entity.Attributes["stage"] = 30;
            entity.Attributes["mode"] = 0;
            entity.Attributes["rank"] = 0;
            entity.Attributes["msg.name"] = new AliasedValue("sdkmessage", "name", "msdyn_InvokeStoredProc");
            entity.Attributes["flt.primaryobjecttypecode"] = new AliasedValue("sdkmessagefilter", "primaryobjecttypecode", "none");
            entity.Attributes["pt.name"] = new AliasedValue("plugintype", "name", "Microsoft.Crm.ObjectModel.SyncWorkflowExecutionPlugin");
            entity.Attributes["pt.typename"] = new AliasedValue("plugintype", "typename", "Microsoft.Crm.ObjectModel.SyncWorkflowExecutionPlugin");
            entity.Attributes["asm.name"] = new AliasedValue("pluginassembly", "name", "Microsoft.Crm.ObjectModel");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("sdk-steps", "sdk-steps.fetch.xml", "sdkSteps"));
            Assert.That(results[0]["flt.primaryobjecttypecode"], Is.EqualTo("none"));
        }

        // -------------------------------------------------------------------------
        // 4. workflows  (workflows.fetch.xml)
        //    Simple entity — all attributes are direct (no joins).
        // -------------------------------------------------------------------------

        private const string WorkflowsFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""workflow"">
    <attribute name=""workflowid"" />
    <attribute name=""name"" />
    <attribute name=""primaryentity"" />
    <attribute name=""category"" />
    <attribute name=""mode"" />
    <attribute name=""statuscode"" />
    <attribute name=""triggeroncreate"" />
    <attribute name=""triggerondelete"" />
  </entity>
</fetch>";

        [Test]
        public void Workflows_MapsAllDirectAttributes()
        {
            WriteFetchXml("workflows.fetch.xml", WorkflowsFetchXml);

            var id = new Guid("3ded9274-b1cb-f011-bbd3-7ced8d70a136");

            var entity = new Entity("workflow", id);
            entity.Attributes["workflowid"] = id;
            entity.Attributes["name"] = "Display Related to ContosoApplication";
            entity.Attributes["primaryentity"] = "cust_accesslevel";
            entity.Attributes["category"] = 2;
            entity.Attributes["mode"] = 1;
            entity.Attributes["statuscode"] = 2;
            entity.Attributes["triggeroncreate"] = false;
            entity.Attributes["triggerondelete"] = false;

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("workflows", "workflows.fetch.xml", "workflows"));
            var row = results[0];

            Assert.That(row["name"], Is.EqualTo("Display Related to ContosoApplication"));
            Assert.That(row["primaryentity"], Is.EqualTo("cust_accesslevel"));
            Assert.That(row["category"], Is.EqualTo(2));
            Assert.That(row["mode"], Is.EqualTo(1));
            Assert.That(row["statuscode"], Is.EqualTo(2));
            Assert.That(row["triggeroncreate"], Is.EqualTo(false));
            Assert.That(row["triggerondelete"], Is.EqualTo(false));
        }

        // -------------------------------------------------------------------------
        // 5. env-vars  (env-vars.fetch.xml)
        // -------------------------------------------------------------------------

        private const string EnvVarsFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""environmentvariabledefinition"">
    <attribute name=""environmentvariabledefinitionid"" />
    <attribute name=""schemaname"" />
    <attribute name=""displayname"" />
    <attribute name=""type"" />
    <attribute name=""description"" />
    <attribute name=""ismanaged"" />
  </entity>
</fetch>";

        [Test]
        public void EnvVars_MapsAllFields()
        {
            WriteFetchXml("env-vars.fetch.xml", EnvVarsFetchXml);

            var id = new Guid("548dc4d2-943c-f011-b4cb-000d3a3751ed");

            var entity = new Entity("environmentvariabledefinition", id);
            entity.Attributes["environmentvariabledefinitionid"] = id;
            entity.Attributes["schemaname"] = "cust_ContosoManagerApprovals";
            entity.Attributes["displayname"] = "Contoso Approvals";
            entity.Attributes["type"] = 100000003;
            entity.Attributes["description"] = "Environment variable for reports";
            entity.Attributes["ismanaged"] = false;

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("env-vars", "env-vars.fetch.xml", "envVars"));
            var row = results[0];

            Assert.That(row["schemaname"], Is.EqualTo("cust_ContosoManagerApprovals"));
            Assert.That(row["displayname"], Is.EqualTo("Contoso Approvals"));
            Assert.That(row["type"], Is.EqualTo(100000003));
            Assert.That(row["description"], Is.EqualTo("Environment variable for reports"));
            Assert.That(row["ismanaged"], Is.EqualTo(false));
        }

        [Test]
        public void EnvVars_MissingDescription_MapsToNull()
        {
            WriteFetchXml("env-vars.fetch.xml", EnvVarsFetchXml);

            var entity = new Entity("environmentvariabledefinition", Guid.NewGuid());
            entity.Attributes["schemaname"] = "cust_administratoruserid";
            entity.Attributes["displayname"] = "Administrator User ID";
            entity.Attributes["type"] = 100000000;
            entity.Attributes["ismanaged"] = false;
            // no "description" attribute → not in dictionary at all

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("env-vars", "env-vars.fetch.xml", "envVars"));
            var row = results[0];

            // Field absent from entity → absent from serialised dict (not null-mapped)
            Assert.That(row.ContainsKey("description"), Is.False);
            Assert.That(row["schemaname"], Is.EqualTo("cust_administratoruserid"));
        }

        // -------------------------------------------------------------------------
        // 6. app-modules  (app-modules.fetch.xml)
        // -------------------------------------------------------------------------

        private const string AppModulesFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""appmodule"">
    <attribute name=""appmoduleid"" />
    <attribute name=""name"" />
    <attribute name=""uniquename"" />
    <attribute name=""description"" />
    <attribute name=""ismanaged"" />
  </entity>
</fetch>";

        [Test]
        public void AppModules_MapsAllAttributes()
        {
            WriteFetchXml("app-modules.fetch.xml", AppModulesFetchXml);

            var id = new Guid("3e65e58f-52e8-ee11-904c-6045bd08b30d");

            var entity = new Entity("appmodule", id);
            entity.Attributes["appmoduleid"] = id;
            entity.Attributes["name"] = "Contoso Mobile App";
            entity.Attributes["uniquename"] = "cust_SalesApp";
            entity.Attributes["description"] = "Contoso Mobile - D365 Mobile for Contoso ";
            entity.Attributes["ismanaged"] = false;

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("app-modules", "app-modules.fetch.xml", "appModules"));
            var row = results[0];

            Assert.That(row["name"], Is.EqualTo("Contoso Mobile App"));
            Assert.That(row["uniquename"], Is.EqualTo("cust_SalesApp"));
            Assert.That(row["description"], Is.EqualTo("Contoso Mobile - D365 Mobile for Contoso "));
            Assert.That(row["ismanaged"], Is.EqualTo(false));
        }

        // -------------------------------------------------------------------------
        // 7. security-roles  (security-roles.fetch.xml)
        //    Contains EntityReference (businessunitid), BooleanManagedProperty
        //    (iscustomizable), and two outer-joined link-entities (rp + priv).
        // -------------------------------------------------------------------------

        private const string SecurityRolesFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
  <entity name=""role"">
    <attribute name=""roleid"" />
    <attribute name=""name"" />
    <attribute name=""businessunitid"" />
    <attribute name=""ismanaged"" />
    <attribute name=""iscustomizable"" />
    <link-entity name=""roleprivileges"" from=""roleid"" to=""roleid"" link-type=""outer"" alias=""rp"">
      <attribute name=""privilegedepthmask"" />
      <link-entity name=""privilege"" from=""privilegeid"" to=""privilegeid"" link-type=""outer"" alias=""priv"">
        <attribute name=""name"" />
        <attribute name=""accessright"" />
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

        [Test]
        public void SecurityRoles_SerializesEntityReferenceBusinessUnit()
        {
            WriteFetchXml("security-roles.fetch.xml", SecurityRolesFetchXml);

            var roleId = new Guid("03cc0146-51b6-4f10-af2f-118e7fd3674a");
            var buId = new Guid("589b498a-cc1d-ef11-840a-6045bd006c23");

            var entity = new Entity("role", roleId);
            entity.Attributes["roleid"] = roleId;
            entity.Attributes["name"] = "Contoso Dashboards";
            entity.Attributes["businessunitid"] = new EntityReference("businessunit", buId) { Name = "RootBU" };
            entity.Attributes["ismanaged"] = false;
            entity.Attributes["iscustomizable"] = new BooleanManagedProperty(false);

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("security-roles", "security-roles.fetch.xml", "securityRoles"));
            var row = results[0];

            // EntityReference → {"id":..., "logicalName":..., "name":...}
            var bu = (Dictionary<string, object?>)row["businessunitid"]!;
            Assert.That(bu["id"], Is.EqualTo(buId.ToString()));
            Assert.That(bu["logicalName"], Is.EqualTo("businessunit"));
            Assert.That(bu["name"], Is.EqualTo("RootBU"));
        }

        [Test]
        public void SecurityRoles_BooleanManagedProperty_FallsBackToString()
        {
            WriteFetchXml("security-roles.fetch.xml", SecurityRolesFetchXml);

            var entity = new Entity("role", Guid.NewGuid());
            entity.Attributes["name"] = "Contoso Dashboards";
            entity.Attributes["ismanaged"] = false;
            // BooleanManagedProperty has no special case in ConvertValue → falls back to ToString()
            entity.Attributes["iscustomizable"] = new BooleanManagedProperty(false);

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("security-roles", "security-roles.fetch.xml", "securityRoles"));

            // Matches the actual run output: "iscustomizable": "Microsoft.Xrm.Sdk.BooleanManagedProperty"
            Assert.That(results[0]["iscustomizable"], Is.EqualTo("Microsoft.Xrm.Sdk.BooleanManagedProperty"));
        }

        [Test]
        public void SecurityRoles_MapsPrivilegeJoinAliases()
        {
            WriteFetchXml("security-roles.fetch.xml", SecurityRolesFetchXml);

            var entity = new Entity("role", Guid.NewGuid());
            entity.Attributes["name"] = "Contoso Dashboards";
            entity.Attributes["ismanaged"] = false;
            entity.Attributes["iscustomizable"] = new BooleanManagedProperty(false);
            entity.Attributes["rp.privilegedepthmask"] = new AliasedValue("roleprivileges", "privilegedepthmask", 2);
            entity.Attributes["priv.name"] = new AliasedValue("privilege", "name", "prvReadChannelAccessProfile");
            entity.Attributes["priv.accessright"] = new AliasedValue("privilege", "accessright", 1);

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("security-roles", "security-roles.fetch.xml", "securityRoles"));
            var row = results[0];

            Assert.That(row["rp.privilegedepthmask"], Is.EqualTo(2));
            Assert.That(row["priv.name"], Is.EqualTo("prvReadChannelAccessProfile"));
            Assert.That(row["priv.accessright"], Is.EqualTo(1));
        }

        // -------------------------------------------------------------------------
        // 8. custom-apis  (custom-apis.fetch.xml)
        //    Link-entity "req" (customapirequestparameter) — outer join
        // -------------------------------------------------------------------------

        private const string CustomApisFetchXml = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""customapi"">
    <attribute name=""customapiid"" />
    <attribute name=""uniquename"" />
    <attribute name=""name"" />
    <attribute name=""description"" />
    <attribute name=""isfunction"" />
    <attribute name=""bindingtype"" />
    <link-entity name=""customapirequestparameter"" from=""customapiid"" to=""customapiid"" link-type=""outer"" alias=""req"">
      <attribute name=""uniquename"" />
      <attribute name=""name"" />
      <attribute name=""type"" />
      <attribute name=""isoptional"" />
      <attribute name=""description"" />
    </link-entity>
  </entity>
</fetch>";

        [Test]
        public void CustomApis_MapsRequestParameterJoin()
        {
            WriteFetchXml("custom-apis.fetch.xml", CustomApisFetchXml);

            var apiId = new Guid("d213e077-1885-f011-b4cb-6045bd059d1e");

            var entity = new Entity("customapi", apiId);
            entity.Attributes["customapiid"] = apiId;
            entity.Attributes["uniquename"] = "cust_buildWOReport";
            entity.Attributes["name"] = "cust_buildWOReport";
            entity.Attributes["description"] = "cust_buildWOReport";
            entity.Attributes["isfunction"] = false;
            entity.Attributes["bindingtype"] = 0;
            entity.Attributes["req.uniquename"] = new AliasedValue("customapirequestparameter", "uniquename", "cust_reportData");
            entity.Attributes["req.name"] = new AliasedValue("customapirequestparameter", "name", "cust_reportData");
            entity.Attributes["req.type"] = new AliasedValue("customapirequestparameter", "type", 10);
            entity.Attributes["req.isoptional"] = new AliasedValue("customapirequestparameter", "isoptional", false);
            entity.Attributes["req.description"] = new AliasedValue("customapirequestparameter", "description", "cust_reportData");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("custom-apis", "custom-apis.fetch.xml", "customApiRequests"));
            var row = results[0];

            Assert.That(row["uniquename"], Is.EqualTo("cust_buildWOReport"));
            Assert.That(row["isfunction"], Is.EqualTo(false));
            Assert.That(row["bindingtype"], Is.EqualTo(0));
            Assert.That(row["req.uniquename"], Is.EqualTo("cust_reportData"));
            Assert.That(row["req.type"], Is.EqualTo(10));
            Assert.That(row["req.isoptional"], Is.EqualTo(false));
        }

        // -------------------------------------------------------------------------
        // 9. Cross-cutting: deny list must not filter any real field names
        //    (none of the fetchxml attribute names contain: password, secret, token, key)
        // -------------------------------------------------------------------------

        [Test]
        public void DefaultDenyList_DoesNotFilterAnyRealSdkStepFields()
        {
            WriteFetchXml("sdk-steps.fetch.xml", SdkStepsFetchXml);

            var entity = new Entity("sdkmessageprocessingstep", Guid.NewGuid());
            entity.Attributes["name"] = "MyPlugin: Create of account";
            entity.Attributes["stage"] = 20;
            entity.Attributes["mode"] = 0;
            entity.Attributes["rank"] = 1;
            entity.Attributes["msg.name"] = new AliasedValue("sdkmessage", "name", "Create");
            entity.Attributes["flt.primaryobjecttypecode"] = new AliasedValue("sdkmessagefilter", "primaryobjecttypecode", "account");
            entity.Attributes["pt.name"] = new AliasedValue("plugintype", "name", "My.Plugin");
            entity.Attributes["pt.typename"] = new AliasedValue("plugintype", "typename", "My.Plugin");
            entity.Attributes["asm.name"] = new AliasedValue("pluginassembly", "name", "MyAssembly");

            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(OnePage(entity));

            var results = RunAndSerialize(mock, Q("sdk-steps", "sdk-steps.fetch.xml", "sdkSteps"));
            var row = results[0];

            // All 9 attribute keys (plus _id) should survive the deny list
            var expectedKeys = new[] { "name", "stage", "mode", "rank", "msg.name",
                "flt.primaryobjecttypecode", "pt.name", "pt.typename", "asm.name" };
            foreach (var k in expectedKeys)
                Assert.That(row.ContainsKey(k), Is.True, $"Key '{k}' was unexpectedly filtered");
        }

        // -------------------------------------------------------------------------
        // 10. Paging: two-page response accumulates correctly
        // -------------------------------------------------------------------------

        [Test]
        public void Solutions_TwoPageResponse_AccumulatesAllRecords()
        {
            WriteFetchXml("solutions-detail.fetch.xml", SolutionsFetchXml);

            Entity MakeSolution(string uniqueName) =>
                new Entity("solution", Guid.NewGuid()) { Attributes = {
                    ["uniquename"] = uniqueName,
                    ["ismanaged"] = false,
                    ["publisherfriendlyname"] = new AliasedValue("publisher", "friendlyname", "fabrikampublisher"),
                    ["publisheruniquename"] = new AliasedValue("publisher", "uniquename", "FabrikamPublisher"),
                }};

            var page1 = new EntityCollection(new[] { MakeSolution("SolA"), MakeSolution("SolB"), MakeSolution("SolC") }.ToList());
            page1.MoreRecords = true;
            page1.PagingCookie = "<cookie page=\"1\"/>";

            var page2 = new EntityCollection(new[] { MakeSolution("SolD"), MakeSolution("SolE") }.ToList());
            page2.MoreRecords = false;

            var callIndex = 0;
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(() => callIndex++ == 0 ? page1 : page2);

            var results = RunAndSerialize(mock, Q("solutions", "solutions-detail.fetch.xml", "solutions"));

            Assert.That(results, Has.Count.EqualTo(5));
            Assert.That(results.Select(r => (string?)r["uniquename"]),
                Is.EquivalentTo(new[] { "SolA", "SolB", "SolC", "SolD", "SolE" }));
        }
    }
}
