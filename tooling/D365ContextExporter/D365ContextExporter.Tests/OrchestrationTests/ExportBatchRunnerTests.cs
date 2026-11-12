using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Orchestration;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests
{
    [TestFixture]
    public class ExportBatchRunnerTests
    {
        private string _baseDir = string.Empty;
        private List<string> _log = null!;
        private List<string> _ran = null!;

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), "BatchTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_baseDir, "config"));
            _log = new List<string>();
            _ran = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, recursive: true);
        }

        [Test]
        public void Run_AllSucceed_RunsEverySpecInOrder()
        {
            var specs = new[] { Spec("Alpha"), Spec("Beta"), Spec("Gamma") };
            var started = new List<string>();
            var batch = new ExportBatchRunner(Record, _log.Add, (i, n, name) => started.Add($"{i}/{n} {name}"));

            var result = batch.Run(specs, _baseDir, CancellationToken.None);

            Assert.That(_ran, Is.EqualTo(new[] { "Alpha", "Beta", "Gamma" }));
            Assert.That(started, Is.EqualTo(new[] { "1/3 Alpha", "2/3 Beta", "3/3 Gamma" }));
            Assert.That(result.Succeeded, Is.EqualTo(new[] { "Alpha", "Beta", "Gamma" }));
            Assert.That(result.Failed, Is.Empty);
            Assert.That(_log.Last(), Is.EqualTo("[Batch] Finished: 3 of 3 specs succeeded."));
        }

        [Test]
        public void Run_SpecFails_OthersStillRunAndFailureIsReported()
        {
            var specs = new[] { Spec("Alpha"), Spec("Beta"), Spec("Gamma") };
            var batch = new ExportBatchRunner(
                (job, dir, ct) => job.Spec == "Beta"
                    ? throw new AggregateException("1 of 2 queries failed.", new InvalidOperationException("boom"))
                    : Record(job, dir, ct),
                _log.Add);

            var result = batch.Run(specs, _baseDir, CancellationToken.None);

            Assert.That(_ran, Is.EqualTo(new[] { "Alpha", "Gamma" }));
            Assert.That(result.Succeeded, Is.EqualTo(new[] { "Alpha", "Gamma" }));
            Assert.That(result.Failed.Select(f => f.Key), Is.EqualTo(new[] { "Beta" }));
            Assert.That(result.Failed[0].Value, Is.EqualTo("boom"));
            Assert.That(_log, Has.Some.EqualTo("[Batch] ERROR in spec 'Beta': boom"));
            Assert.That(_log.Last(), Is.EqualTo("[Batch] Finished: 2 of 3 specs succeeded. Failed: Beta."));
        }

        [Test]
        public void Run_SpecFileUnreadable_CountsAsFailedAndOthersRun()
        {
            var broken = Path.Combine(_baseDir, "config", "Broken.context-exporter-config.json");
            File.WriteAllText(broken, "{ not json");
            var specs = new[] { broken, Spec("Alpha") };

            var result = new ExportBatchRunner(Record, _log.Add).Run(specs, _baseDir, CancellationToken.None);

            Assert.That(_ran, Is.EqualTo(new[] { "Alpha" }));
            Assert.That(result.Failed.Select(f => f.Key), Is.EqualTo(new[] { "Broken" }));
        }

        [Test]
        public void Run_CancelledBetweenSpecs_StopsTheBatch()
        {
            using var cts = new CancellationTokenSource();
            var specs = new[] { Spec("Alpha"), Spec("Beta") };
            var batch = new ExportBatchRunner(
                (job, dir, ct) =>
                {
                    var runDir = Record(job, dir, ct);
                    cts.Cancel();
                    return runDir;
                },
                _log.Add);

            Assert.Throws<OperationCanceledException>(() => batch.Run(specs, _baseDir, cts.Token));

            Assert.That(_ran, Is.EqualTo(new[] { "Alpha" }));
            Assert.That(_log, Has.Some.EqualTo("[Batch] Cancelled."));
        }

        [Test]
        public void Run_CancelledInsideSpec_StopsTheBatch()
        {
            var specs = new[] { Spec("Alpha"), Spec("Beta") };
            var batch = new ExportBatchRunner((_, _, _) => throw new OperationCanceledException(), _log.Add);

            Assert.Throws<OperationCanceledException>(() => batch.Run(specs, _baseDir, CancellationToken.None));

            Assert.That(_log.Count(l => l.StartsWith("[Batch] Spec ")), Is.EqualTo(1), "second spec never started");
        }

        private string Record(ExportJob job, string baseDir, CancellationToken cancellationToken)
        {
            _ran.Add(job.Spec);
            return Path.Combine(baseDir, "runs", job.Spec);
        }

        private string Spec(string name)
        {
            var path = Path.Combine(_baseDir, "config", $"{name}.context-exporter-config.json");
            File.WriteAllText(path, $"{{ \"spec\": \"{name}\", \"transformation\": \"t.sbn\", \"queries\": [] }}");
            return path;
        }
    }
}
