using Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.HelpersTests
{
    /// <summary>
    /// Exercises every function registered by <c>TemplateFilters.RegisterAll</c> the way templates use them:
    /// rendered through <c>TemplateRenderer</c>, with data from an intermediate JSON.
    /// </summary>
    [TestFixture]
    public class TemplateFiltersTests
    {
        private TemplateHarness _harness = null!;

        [SetUp]
        public void SetUp() => _harness = new TemplateHarness();

        [TearDown]
        public void TearDown() => _harness.Dispose();

        // ── Naming and labels ─────────────────────────────────────────────

        [TestCase("MyCustomField", "My Custom Field")]
        [TestCase("new_MyField", "New My Field")]
        [TestCase("", "")]
        public void SchemanameToTitle(string input, string expected)
        {
            Assert.That(Render($"{{{{ schemaname_to_title '{input}' }}}}"), Is.EqualTo(expected));
        }

        [Test]
        public void DisplayLabel_UserLocalizedLabel_ReturnsLabel()
        {
            var output = Render(
                "{{ display_label e.DisplayName 'fb' }}",
                "{ \"e\": { \"DisplayName\": { \"UserLocalizedLabel\": { \"Label\": \"Account\" } } } }");

            Assert.That(output, Is.EqualTo("Account"));
        }

        [TestCase("{ \"e\": { \"DisplayName\": null } }")]
        [TestCase("{ \"e\": { \"DisplayName\": { \"UserLocalizedLabel\": null } } }")]
        [TestCase("{ \"e\": { \"DisplayName\": { \"UserLocalizedLabel\": { \"Label\": \"\" } } } }")]
        public void DisplayLabel_NoLabel_ReturnsFallback(string json)
        {
            Assert.That(Render("{{ display_label e.DisplayName 'fb' }}", json), Is.EqualTo("fb"));
        }

        [Test]
        public void OptionsetLabel_MatchingValue_ReturnsLabel()
        {
            Assert.That(Render("{{ optionset_label options 1 }}", Options), Is.EqualTo("Active"));
        }

        [Test]
        public void OptionsetLabel_NoMatch_ReturnsValue()
        {
            Assert.That(Render("{{ optionset_label options 2 }}", Options), Is.EqualTo("2"));
        }

        // ── Collections ───────────────────────────────────────────────────

        [Test]
        public void MarkdownTable_Rows_RendersPipeTable()
        {
            var output = Render(
                "{{ markdown_table rows ['name', 'n'] }}",
                "{ \"rows\": [ { \"name\": \"A\", \"n\": 1 }, { \"name\": \"B\" } ] }");

            Assert.That(output, Is.EqualTo("| name | n |\n| --- | --- |\n| A | 1 |\n| B |  |"));
        }

        [Test]
        public void MarkdownTable_Empty_ReturnsNoData()
        {
            Assert.That(Render("{{ markdown_table [] ['name'] }}"), Is.EqualTo("_No data._"));
        }

        [Test]
        public void CsvList_WithAttribute_JoinsThatAttribute()
        {
            Assert.That(Render("{{ csv_list rows 'k' }}", Rows), Is.EqualTo("a, b, a, "));
        }

        [Test]
        public void CsvList_WithoutAttribute_JoinsItems()
        {
            Assert.That(Render("{{ csv_list ['x', 'y'] '' }}"), Is.EqualTo("x, y"));
        }

        [Test]
        public void CsvList_Empty_ReturnsEmpty()
        {
            Assert.That(Render("{{ csv_list [] 'k' }}"), Is.Empty);
        }

        [Test]
        public void Pluck_ReturnsUniqueNonNullValuesInOrder()
        {
            Assert.That(Render("{{ pluck rows 'k' | array.join ',' }}", Rows), Is.EqualTo("a,b"));
        }

        [Test]
        public void GroupByKey_ReturnsKeyAndItemsInFirstSeenOrder()
        {
            var output = Render("{{ for g in (group_by_key rows 'k') }}{{ g.key }}={{ g.items.size }};{{ end }}", Rows);

            Assert.That(output, Is.EqualTo("a=2;b=1;=1;"));
        }

        // ── Attributes ────────────────────────────────────────────────────

        [TestCase("Lookup", "lkp")]
        [TestCase("Picklist", "opt")]
        [TestCase("Customer", "lkp")]
        [TestCase("SomethingNew", "SomethingNew")]
        [TestCase("", "-")]
        public void AttrTypeAbbrev(string input, string expected)
        {
            Assert.That(Render($"{{{{ attr_type_abbrev '{input}' }}}}"), Is.EqualTo(expected));
        }

        [Test]
        public void AttrTypeAbbrev_Null_ReturnsDash()
        {
            Assert.That(Render("{{ attr_type_abbrev null }}"), Is.EqualTo("-"));
        }

        [TestCase("Required", "**R**")]
        [TestCase("ApplicationRequired", "**R**")]
        [TestCase("Recommended", "r")]
        [TestCase("SystemRequired", "-")]
        [TestCase("None", "-")]
        public void ReqIndicator(string input, string expected)
        {
            Assert.That(Render($"{{{{ req_indicator '{input}' }}}}"), Is.EqualTo(expected));
        }

        [Test]
        public void ReqIndicator_ManagedPropertyObject_ReadsValue()
        {
            Assert.That(Render("{{ req_indicator a.RequiredLevel }}", "{ \"a\": { \"RequiredLevel\": { \"Value\": \"Required\" } } }"), Is.EqualTo("**R**"));
        }

        // ── Dates and component types ─────────────────────────────────────

        [TestCase("2026-03-04T10:20:30Z", "2026-03-04")]
        [TestCase("2026-03-04", "2026-03-04")]
        [TestCase("", "")]
        public void IsoDate(string input, string expected)
        {
            Assert.That(Render($"{{{{ iso_date '{input}' }}}}"), Is.EqualTo(expected));
        }

        [TestCase(1, "Table (Entity)")]
        [TestCase(92, "Cloud Flow")]
        [TestCase(9999, "Unknown (9999)")]
        public void ComponentTypeName(int input, string expected)
        {
            Assert.That(Render($"{{{{ component_type_name {input} }}}}"), Is.EqualTo(expected));
        }

        // ── Forms and views ───────────────────────────────────────────────

        [Test]
        public void FormatForms_GroupsTypesByName()
        {
            var output = Render(
                "{{ format_forms forms }}",
                "{ \"forms\": [ { \"Name\": \"Account\", \"Type\": 8 }, { \"Name\": \"Account\", \"Type\": 7 }, { \"Name\": \"Account\", \"Type\": 8 }, { \"Name\": \"Card\", \"Type\": 11 } ] }");

            Assert.That(output, Is.EqualTo("Account(main,quick), Card(card)"));
        }

        [Test]
        public void FormatForms_Empty_ReturnsDash()
        {
            Assert.That(Render("{{ format_forms [] }}"), Is.EqualTo("—"));
        }

        [Test]
        public void FormatViews_SkipsSpecialPurposeTypesAndDuplicates()
        {
            var output = Render(
                "{{ format_views views }}",
                "{ \"views\": [ { \"Name\": \"Active\", \"QueryType\": 0 }, { \"Name\": \"Lookup\", \"QueryType\": 64 }, { \"Name\": \"Active\", \"QueryType\": 0 }, { \"Name\": \"Mine\", \"QueryType\": 0 } ] }");

            Assert.That(output, Is.EqualTo("Active, Mine"));
        }

        [Test]
        public void FormatViews_Empty_ReturnsDash()
        {
            Assert.That(Render("{{ format_views [] }}"), Is.EqualTo("—"));
        }

        [Test]
        public void EntityForms_FiltersByObjectTypeCode()
        {
            var output = Render(
                "{{ (entity_forms forms 'account').size }}",
                "{ \"forms\": [ { \"objecttypecode\": \"account\" }, { \"objecttypecode\": \"contact\" }, { \"objecttypecode\": \"Account\" } ] }");

            Assert.That(output, Is.EqualTo("2"));
        }

        [Test]
        public void EntityViews_FiltersByReturnedTypeCode()
        {
            var output = Render(
                "{{ (entity_views views 'contact').size }}",
                "{ \"views\": [ { \"returnedtypecode\": \"account\" }, { \"returnedtypecode\": \"contact\" } ] }");

            Assert.That(output, Is.EqualTo("1"));
        }

        // ── Plug-ins, processes and other codes ───────────────────────────

        [TestCase(10, "PreVal")]
        [TestCase(20, "Pre")]
        [TestCase(40, "Post")]
        [TestCase(45, "Post")]
        public void PluginStage(int input, string expected)
        {
            Assert.That(Render($"{{{{ plugin_stage {input} }}}}"), Is.EqualTo(expected));
        }

        [TestCase(0, "Sync")]
        [TestCase(1, "Async")]
        public void PluginMode(int input, string expected)
        {
            Assert.That(Render($"{{{{ plugin_mode {input} }}}}"), Is.EqualTo(expected));
        }

        [TestCase("When an HTTP request is received", "HTTP")]
        [TestCase("Daily account sync", "Sched")]
        [TestCase("Child - update totals", "Child")]
        [TestCase("Manual export", "Manual")]
        [TestCase("On account create", "Auto")]
        public void FlowTrigger(string input, string expected)
        {
            Assert.That(Render($"{{{{ flow_trigger '{input}' }}}}"), Is.EqualTo(expected));
        }

        [Test]
        public void ClassicTrigger_FlagsSet_ListsTriggers()
        {
            var output = Render(
                "{{ classic_trigger wf }}",
                "{ \"wf\": { \"triggeroncreate\": true, \"triggeronupdateattributelist\": \"name\", \"triggerondelete\": false } }");

            Assert.That(output, Is.EqualTo("Create/Update"));
        }

        [Test]
        public void ClassicTrigger_NoFlags_ReturnsManual()
        {
            Assert.That(Render("{{ classic_trigger wf }}", "{ \"wf\": { } }"), Is.EqualTo("Manual"));
        }

        [TestCase(100000000, "String")]
        [TestCase(100000003, "JSON")]
        [TestCase(100000005, "Secret")]
        public void EnvvarType(int input, string expected)
        {
            Assert.That(Render($"{{{{ envvar_type {input} }}}}"), Is.EqualTo(expected));
        }

        [TestCase(0, "bool")]
        [TestCase(5, "EntityReference")]
        [TestCase(10, "str")]
        [TestCase(11, "str[]")]
        public void ApiParamType(int input, string expected)
        {
            Assert.That(Render($"{{{{ api_param_type {input} }}}}"), Is.EqualTo(expected));
        }

        [TestCase(0, "None")]
        [TestCase(1, "User")]
        [TestCase(2, "Business Unit")]
        [TestCase(4, "Parent: Child BUs")]
        [TestCase(8, "Organization")]
        public void PrivDepth(int input, string expected)
        {
            Assert.That(Render($"{{{{ priv_depth {input} }}}}"), Is.EqualTo(expected));
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private const string Options =
            "{ \"options\": [ { \"Value\": 1, \"Label\": { \"UserLocalizedLabel\": { \"Label\": \"Active\" } } } ] }";

        private const string Rows =
            "{ \"rows\": [ { \"k\": \"a\" }, { \"k\": \"b\" }, { \"k\": \"a\" }, { \"k\": null } ] }";

        private string Render(string template, string intermediateJson = "{}") => _harness.Render(template, intermediateJson);
    }
}
