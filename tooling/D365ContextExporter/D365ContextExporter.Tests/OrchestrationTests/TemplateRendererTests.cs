using System;
using System.IO;

using Lspiguel.Xrm.D365ContextExporter.Orchestration;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests
{
    [TestFixture]
    public class TemplateRendererTests
    {
        private TemplateHarness _harness = null!;

        [SetUp]
        public void SetUp() => _harness = new TemplateHarness();

        [TearDown]
        public void TearDown() => _harness.Dispose();

        [Test]
        public void Render_SpecVariable_RendersSpecName()
        {
            var output = _harness.Render("Spec: {{ _spec }}");

            Assert.That(output, Is.EqualTo($"Spec: {TemplateHarness.SpecName}"));
        }

        [Test]
        public void Render_MetaFromIntermediate_IsAvailable()
        {
            var output = _harness.Render(
                "{{ _meta.environment.orgName }} {{ _meta.frontMatter.purpose }}",
                "{ \"_meta\": { \"environment\": { \"orgName\": \"Org\" }, \"frontMatter\": { \"purpose\": \"Test\" } } }");

            Assert.That(output, Is.EqualTo("Org Test"));
        }

        [Test]
        public void Render_NestedObjectsAndArrays_AreNavigable()
        {
            var output = _harness.Render(
                "{{ data.items[1].name }} {{ data.items.size }} {{ for i in data.items }}{{ i.tags[0] }}{{ end }}",
                "{ \"data\": { \"items\": [ { \"name\": \"a\", \"tags\": [\"x\"] }, { \"name\": \"b\", \"tags\": [\"y\", \"z\"] } ] } }");

            Assert.That(output, Is.EqualTo("b 2 xy"));
        }

        [Test]
        public void Render_IncludeResolvesFromTransformations()
        {
            _harness.WriteTemplate("part.sbn", "PART {{ _spec }}");

            var output = _harness.Render("[{{ include 'part.sbn' }}]");

            Assert.That(output, Is.EqualTo($"[PART {TemplateHarness.SpecName}]"));
        }

        [Test]
        public void Render_ParseError_ReportsFileLineAndColumn()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => _harness.Render("line one\nline two\n{{ if }}"));

            Assert.That(ex!.Message, Does.Contain("Template parse errors in 'main.sbn'"));
            // "{{ if }}" fails at the closing braces: line 3, column 7, counted from one.
            Assert.That(ex.Message, Does.Contain("main.sbn(3,7): "));
        }

        [Test]
        public void Render_UndefinedVariable_Fails()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => _harness.Render("{{ not_defined }}"));

            Assert.That(ex!.Message, Does.StartWith("Template render error in 'main.sbn'"));
        }

        [Test]
        public void Render_MissingTemplate_ThrowsFileNotFound()
        {
            File.WriteAllText(Path.Combine(_harness.RunDir, "intermediate.json"), "{}");

            Assert.Throws<FileNotFoundException>(() =>
                new TemplateRenderer(_ => { }).Render(_harness.Job("missing.sbn"), _harness.BaseDir, _harness.RunDir));
        }

        [Test]
        public void Render_WritesUtf8WithoutBom()
        {
            _harness.Render("é");

            var bytes = File.ReadAllBytes(_harness.OutputPath);
            Assert.That(bytes[0], Is.Not.EqualTo(0xEF), "no UTF-8 BOM");
            Assert.That(File.ReadAllText(_harness.OutputPath), Is.EqualTo("é"));
        }
    }
}
