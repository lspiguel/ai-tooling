using System.Collections.Generic;
using System.IO;

using Lspiguel.Xrm.D365ContextExporter.Helpers;
using Lspiguel.Xrm.D365ContextExporter.Models;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.HelpersTests
{
    [TestFixture]
    public class ConfigValidatorTests
    {
        private string _baseDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "ValidatorTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_baseDir, "config", "transformations"));
            Directory.CreateDirectory(Path.Combine(_baseDir, "config", "queries"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        private static ExportJob MakeValidJob(string baseDir)
        {
            var transformPath = Path.Combine(baseDir, "config", "transformations", "test.j2");
            File.WriteAllText(transformPath, "{{ entities }}");

            var fetchPath = Path.Combine(baseDir, "config", "queries", "test.fetch.xml");
            File.WriteAllText(fetchPath, "<fetch/>");

            return new ExportJob
            {
                Spec = "TestSpec",
                Transformation = "test.j2",
                Queries = new List<QueryDefinition>
                {
                    new QueryDefinition { Id = "q1", Type = "fetchxml", Source = "test.fetch.xml", ResultKey = "entities" },
                },
            };
        }

        [Test]
        public void Validate_HappyPath_DoesNotThrow()
        {
            var job = MakeValidJob(_baseDir);
            Assert.DoesNotThrow(() => ConfigValidator.Validate(job, _baseDir));
        }

        [Test]
        public void Validate_MissingSpec_ThrowsWithSpecMention()
        {
            var job = MakeValidJob(_baseDir);
            job.Spec = string.Empty;

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("spec"));
        }

        [Test]
        public void Validate_MissingTransformation_ThrowsWithTransformationMention()
        {
            var job = MakeValidJob(_baseDir);
            job.Transformation = string.Empty;

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("transformation"));
        }

        [Test]
        public void Validate_TransformationFileNotFound_ThrowsWithPath()
        {
            var job = MakeValidJob(_baseDir);
            job.Transformation = "nonexistent.j2";

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("nonexistent.j2"));
        }

        [Test]
        public void Validate_MissingResultKey_ThrowsNamingQuery()
        {
            var job = MakeValidJob(_baseDir);
            job.Queries[0].ResultKey = string.Empty;

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("q1"));
            Assert.That(ex.Message, Does.Contain("resultKey"));
        }

        [Test]
        public void Validate_FetchXmlSourceNotFound_ThrowsNamingPath()
        {
            var job = MakeValidJob(_baseDir);
            job.Queries[0].Source = "missing.fetch.xml";

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("missing.fetch.xml"));
        }

        [Test]
        public void Validate_MissingLegalFile_ThrowsNamingFile()
        {
            var job = MakeValidJob(_baseDir);
            job.Legal = "LEGAL.md";  // file does not exist in temp dir

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("legal"));
        }

        [Test]
        public void Validate_LegalFilePresent_DoesNotThrow()
        {
            var job = MakeValidJob(_baseDir);
            var legalPath = Path.Combine(_baseDir, "LEGAL.md");
            File.WriteAllText(legalPath, "Legal notice");
            job.Legal = "LEGAL.md";

            Assert.DoesNotThrow(() => ConfigValidator.Validate(job, _baseDir));
        }

        [Test]
        public void Validate_DuplicateResultKey_ThrowsListingBothIds()
        {
            var job = MakeValidJob(_baseDir);
            var fetchPath = Path.Combine(_baseDir, "config", "queries", "test2.fetch.xml");
            File.WriteAllText(fetchPath, "<fetch/>");

            job.Queries.Add(new QueryDefinition
            {
                Id = "q2",
                Type = "fetchxml",
                Source = "test2.fetch.xml",
                ResultKey = "entities",  // duplicate
            });

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Message, Does.Contain("q1").Or.Contain("q2"));
            Assert.That(ex.Message, Does.Contain("entities"));
        }

        [Test]
        public void Validate_ReportsAllViolationsAtOnce()
        {
            var job = new ExportJob
            {
                Spec = string.Empty,
                Transformation = string.Empty,
                Queries = new List<QueryDefinition>(),
            };

            var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.Validate(job, _baseDir));
            Assert.That(ex!.Violations.Count, Is.GreaterThanOrEqualTo(2));
        }
    }
}
