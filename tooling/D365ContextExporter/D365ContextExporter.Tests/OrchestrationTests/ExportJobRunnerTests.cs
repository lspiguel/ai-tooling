using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;

using Lspiguel.Xrm.D365ContextExporter.Helpers;
using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Orchestration;
using Lspiguel.Xrm.D365ContextExporter.Queries;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using Moq;
using Moq.Contrib.HttpClient;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests
{
    [TestFixture]
    public class ExportJobRunnerTests
    {
        private const string EnvUrl = "https://test.crm.dynamics.com";
        private const string SpecName = "RunnerSpec";
        private const string LegalText = "> LEGAL NOTICE";

        private string _baseDir = string.Empty;
        private Mock<IOrganizationService> _service = null!;
        private Mock<HttpMessageHandler> _http = null!;
        private List<string> _log = null!;

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "RunnerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_baseDir, "config", "queries"));
            Directory.CreateDirectory(Path.Combine(_baseDir, "config", "transformations"));
            File.WriteAllText(
                Path.Combine(_baseDir, "config", "queries", "accounts.fetch.xml"),
                "<fetch><entity name=\"account\"><attribute name=\"name\"/></entity></fetch>");
            File.WriteAllText(
                Path.Combine(_baseDir, "config", "transformations", "runner.sbn"),
                "accounts={{ accounts.size }} contacts={{ contacts.size }} org={{ _meta.environment.orgName }}");
            File.WriteAllText(Path.Combine(_baseDir, "LEGAL.md"), LegalText);

            var page = new EntityCollection { MoreRecords = false };
            page.Entities.Add(new Entity("account", Guid.NewGuid()) { ["name"] = "A" });
            page.Entities.Add(new Entity("account", Guid.NewGuid()) { ["name"] = "B" });
            _service = new Mock<IOrganizationService>();
            _service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(page);

            _http = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _http.SetupAnyRequest().ReturnsResponse(
                HttpStatusCode.OK,
                "{ \"value\": [ { \"fullname\": \"X\" }, { \"fullname\": \"Y\" }, { \"fullname\": \"Z\" } ] }",
                "application/json");

            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        [Test]
        public void Run_Success_RendersAndCopiesToOutput()
        {
            var runDir = CreateRunner().Run(Job(), _baseDir, CancellationToken.None);

            Assert.That(File.Exists(Path.Combine(runDir, "output.accounts.fetch.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(runDir, "output.contacts.fetch.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(runDir, "intermediate.json")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(runDir, "output.md")), Is.EqualTo("accounts=2 contacts=3 org=Test Org"));
            Assert.That(File.Exists(DeliveredPath), Is.True);
        }

        [Test]
        public void Run_WithLegal_NoticeInDeliveredFileOnly()
        {
            var runDir = CreateRunner().Run(Job(), _baseDir, CancellationToken.None);

            Assert.That(File.ReadAllText(DeliveredPath), Is.EqualTo($"{LegalText}\n\naccounts=2 contacts=3 org=Test Org"));
            Assert.That(File.ReadAllText(Path.Combine(runDir, "output.md")), Does.Not.Contain(LegalText));
        }

        [Test]
        public void Run_WithoutLegal_DeliveredFileMatchesRunOutput()
        {
            var job = Job();
            job.Legal = string.Empty;

            var runDir = CreateRunner().Run(job, _baseDir, CancellationToken.None);

            Assert.That(File.ReadAllText(DeliveredPath), Is.EqualTo(File.ReadAllText(Path.Combine(runDir, "output.md"))));
        }

        [Test]
        public void Run_FailedQuery_RemainingQueriesRunAndNothingIsRendered()
        {
            _http.Reset();
            _http.SetupAnyRequest().ReturnsResponse(HttpStatusCode.InternalServerError, "boom");
            var job = Job();
            job.Queries.Reverse(); // webapi first, so the fetchxml query runs after the failure

            var ex = Assert.Throws<AggregateException>(() => CreateRunner().Run(job, _baseDir, CancellationToken.None));

            Assert.That(ex!.Message, Does.StartWith("1 of 2 queries failed."));
            Assert.That(_log, Has.Some.StartsWith("[Export] ERROR in query 'contacts'"));
            var runDir = SingleRunDir();
            Assert.That(File.Exists(Path.Combine(runDir, "output.accounts.fetch.json")), Is.True, "later query still ran");
            Assert.That(File.Exists(Path.Combine(runDir, "intermediate.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(runDir, "output.md")), Is.False);
            Assert.That(File.Exists(DeliveredPath), Is.False);
        }

        [Test]
        public void Run_CancelledBetweenQueries_StopsWithoutRendering()
        {
            using var cts = new CancellationTokenSource();
            var runner = CreateRunner(onProgress: (_, _, _) => cts.Cancel());

            Assert.Throws<OperationCanceledException>(() => runner.Run(Job(), _baseDir, cts.Token));

            Assert.That(_log, Has.Some.EqualTo("[Export] Cancelled."));
            _http.VerifyAnyRequest(Times.Never());
            var runDir = SingleRunDir();
            Assert.That(File.Exists(Path.Combine(runDir, "intermediate.json")), Is.False);
            Assert.That(File.Exists(DeliveredPath), Is.False);
        }

        [Test]
        public void Run_ReportsProgressAfterEachQuery()
        {
            var progress = new List<string>();

            CreateRunner(onProgress: (i, n, id) => progress.Add($"{i}/{n} {id}")).Run(Job(), _baseDir, CancellationToken.None);

            Assert.That(progress, Is.EqualTo(new[] { "1/2 accounts", "2/2 contacts" }));
        }

        [Test]
        public void Run_NoQueries_CreatesRunFolderOnly()
        {
            var job = Job();
            job.Queries.Clear();

            var runDir = CreateRunner().Run(job, _baseDir, CancellationToken.None);

            Assert.That(Directory.Exists(runDir), Is.True);
            Assert.That(Directory.GetFiles(runDir), Is.Empty);
            Assert.That(File.Exists(DeliveredPath), Is.False);
        }

        [Test]
        public void Run_InvalidSpec_FailsValidationBeforeCreatingRunFolder()
        {
            var job = Job();
            job.Transformation = "missing.sbn";

            Assert.Throws<ConfigValidationException>(() => CreateRunner().Run(job, _baseDir, CancellationToken.None));

            Assert.That(Directory.Exists(Path.Combine(_baseDir, "runs")), Is.False);
        }

        private string DeliveredPath => Path.Combine(_baseDir, "output", $"{SpecName}.context.md");

        private ExportJob Job() => new ExportJob
        {
            Spec = SpecName,
            Transformation = "runner.sbn",
            Legal = "LEGAL.md",
            Queries = new List<QueryDefinition>
            {
                new QueryDefinition { Id = "accounts", Type = "fetchxml", Source = "accounts.fetch.xml", ResultKey = "accounts" },
                new QueryDefinition { Id = "contacts", Type = "webapi", Path = "contacts", ResultKey = "contacts" },
            },
        };

        private ExportJobRunner CreateRunner(Action<int, int, string>? onProgress = null) =>
            new ExportJobRunner(
                new FetchXmlQueryRunner(_service.Object, _log.Add),
                new WebApiQueryRunner(EnvUrl, () => "token", _http.CreateClient(), _log.Add),
                new MetadataQueryRunner(_service.Object, _log.Add),
                EnvUrl,
                "Test Org",
                _log.Add,
                onProgress);

        private string SingleRunDir() => Directory.GetDirectories(Path.Combine(_baseDir, "runs")).Single();
    }
}
