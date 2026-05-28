using System;
using System.Collections.Generic;
using System.IO;

using Lspiguel.Xrm.D365ContextExporter.Models;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.ModelsTests
{
    [TestFixture]
    public class ExportJobTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ExportJobTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private string WriteConfig(string json)
        {
            var path = Path.Combine(_tempDir, "test.context-exporter-config.json");
            File.WriteAllText(path, json);
            return path;
        }

        [Test]
        public void Load_ValidJson_DeserializesSpec()
        {
            var path = WriteConfig("{\"spec\":\"MySpec\",\"version\":\"2.0.0\"}");
            var job = ExportJob.Load(path);
            Assert.That(job.Spec, Is.EqualTo("MySpec"));
            Assert.That(job.Version, Is.EqualTo("2.0.0"));
        }

        [Test]
        public void Load_SetsConfigFilePath()
        {
            var path = WriteConfig("{\"spec\":\"X\"}");
            var job = ExportJob.Load(path);
            Assert.That(job.ConfigFilePath, Is.EqualTo(path));
        }

        [Test]
        public void Load_ValidJson_DeserializesQueries()
        {
            var json = "{\"spec\":\"S\",\"queries\":[{\"id\":\"q1\",\"type\":\"fetchxml\",\"resultKey\":\"r1\"}]}";
            var path = WriteConfig(json);
            var job = ExportJob.Load(path);
            Assert.That(job.Queries, Has.Count.EqualTo(1));
            Assert.That(job.Queries[0].Id, Is.EqualTo("q1"));
            Assert.That(job.Queries[0].Type, Is.EqualTo("fetchxml"));
        }

        [Test]
        public void Load_EmptyObject_ReturnsDefaults()
        {
            var path = WriteConfig("{}");
            var job = ExportJob.Load(path);
            Assert.That(job.Spec, Is.EqualTo(string.Empty));
            Assert.That(job.Version, Is.EqualTo("1.0.0"));
            Assert.That(job.Queries, Is.Empty);
            Assert.That(job.Python.Interpreter, Is.EqualTo("auto"));
        }

        [Test]
        public void Load_NullJson_ThrowsInvalidOperationException()
        {
            var path = WriteConfig("null");
            Assert.Throws<InvalidOperationException>(() => ExportJob.Load(path));
        }

        [Test]
        public void Load_WithFrontMatter_DeserializesFrontMatter()
        {
            var json = "{\"spec\":\"S\",\"frontMatter\":{\"purpose\":\"testing\",\"owner\":\"team\"}}";
            var path = WriteConfig(json);
            var job = ExportJob.Load(path);
            Assert.That(job.FrontMatter["purpose"], Is.EqualTo("testing"));
            Assert.That(job.FrontMatter["owner"], Is.EqualTo("team"));
        }

        [Test]
        public void Load_WithOutputSettings_DeserializesAttributeDenyList()
        {
            // Newtonsoft.Json appends to pre-initialized lists; verify custom item is present
            var json = "{\"spec\":\"S\",\"output\":{\"attributeDenyList\":[\"customsensitive\"]}}";
            var path = WriteConfig(json);
            var job = ExportJob.Load(path);
            Assert.That(job.Output.AttributeDenyList, Contains.Item("customsensitive"));
        }

        [Test]
        public void ToString_ReturnsExpectedFormat()
        {
            var job = new ExportJob
            {
                Spec = "TestSpec",
                Version = "1.2.3",
                Queries = new List<QueryDefinition> { new QueryDefinition(), new QueryDefinition() },
            };
            Assert.That(job.ToString(), Is.EqualTo("TestSpec v1.2.3 (2 queries)"));
        }

        [Test]
        public void OutputSettings_DefaultDenyList_ContainsExpectedEntries()
        {
            var settings = new OutputSettings();
            Assert.That(settings.AttributeDenyList, Contains.Item("password"));
            Assert.That(settings.AttributeDenyList, Contains.Item("secret"));
            Assert.That(settings.AttributeDenyList, Contains.Item("token"));
            Assert.That(settings.AttributeDenyList, Contains.Item("key"));
        }

        [Test]
        public void PythonSettings_DefaultInterpreter_IsAuto()
        {
            var settings = new PythonSettings();
            Assert.That(settings.Interpreter, Is.EqualTo("auto"));
        }

        [Test]
        public void QueryDefinition_DefaultValues_AreEmpty()
        {
            var q = new QueryDefinition();
            Assert.That(q.Id, Is.EqualTo(string.Empty));
            Assert.That(q.Type, Is.EqualTo(string.Empty));
            Assert.That(q.ResultKey, Is.EqualTo(string.Empty));
            Assert.That(q.Source, Is.Null);
            Assert.That(q.Path, Is.Null);
            Assert.That(q.MaxRecords, Is.Null);
            Assert.That(q.Select, Is.Null);
            Assert.That(q.MetadataTarget, Is.Null);
        }
    }
}
