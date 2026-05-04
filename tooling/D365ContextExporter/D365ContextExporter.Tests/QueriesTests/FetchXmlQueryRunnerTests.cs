using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using D365ContextExporter.Models;
using D365ContextExporter.Queries;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using Moq;

using NUnit.Framework;

namespace D365ContextExporter.Tests.QueriesTests
{
    [TestFixture]
    public class FetchXmlQueryRunnerTests
    {
        private string _tempDir = string.Empty;
        private string _queriesDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FetchXmlTests_" + Guid.NewGuid().ToString("N"));
            _queriesDir = Path.Combine(_tempDir, "config", "queries");
            Directory.CreateDirectory(_queriesDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private static QueryDefinition MakeQuery(string source, int? maxRecords = null) =>
            new QueryDefinition { Id = "test-query", Type = "fetchxml", Source = source, ResultKey = "testKey", MaxRecords = maxRecords };

        private static string WriteFetchXml(string dir, string fileName)
        {
            var xml = "<fetch><entity name=\"account\"><attribute name=\"name\"/></entity></fetch>";
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, xml);
            return path;
        }

        private static EntityCollection MakePage(int count, bool moreRecords, string? cookie = null)
        {
            var col = new EntityCollection();
            for (var i = 0; i < count; i++)
                col.Entities.Add(new Entity("account", Guid.NewGuid()));
            col.MoreRecords = moreRecords;
            col.PagingCookie = cookie;
            return col;
        }

        [Test]
        public void Run_HappyPath_ReturnsAllRecords()
        {
            WriteFetchXml(_queriesDir, "test.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(MakePage(3, moreRecords: false));

            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });
            var result = runner.Run(MakeQuery("test.fetch.xml"), _tempDir, CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(3));
        }

        [Test]
        public void Run_ThreePages_AccumulatesAllRecords()
        {
            WriteFetchXml(_queriesDir, "test.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            var callCount = 0;
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(() =>
                {
                    callCount++;
                    return callCount < 3
                        ? MakePage(5, moreRecords: true, cookie: $"cookie{callCount}")
                        : MakePage(2, moreRecords: false);
                });

            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });
            var result = runner.Run(MakeQuery("test.fetch.xml"), _tempDir, CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(12));
            Assert.That(callCount, Is.EqualTo(3));
        }

        [Test]
        public void Run_EmptyResult_ReturnsEmptyList()
        {
            WriteFetchXml(_queriesDir, "empty.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(MakePage(0, moreRecords: false));

            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });
            var result = runner.Run(MakeQuery("empty.fetch.xml"), _tempDir, CancellationToken.None);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Run_MaxRecordsCap_StopsEarly()
        {
            WriteFetchXml(_queriesDir, "test.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(MakePage(10, moreRecords: true, cookie: "cookie"));

            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });
            var result = runner.Run(MakeQuery("test.fetch.xml", maxRecords: 10), _tempDir, CancellationToken.None);

            // Should stop after first page since count >= maxRecords
            Assert.That(result.Count, Is.EqualTo(10));
            mock.Verify(s => s.RetrieveMultiple(It.IsAny<QueryBase>()), Times.Once);
        }

        [Test]
        public void Run_FileNotFound_ThrowsFileNotFoundException()
        {
            var mock = new Mock<IOrganizationService>();
            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });

            Assert.Throws<FileNotFoundException>(() =>
                runner.Run(MakeQuery("nonexistent.fetch.xml"), _tempDir, CancellationToken.None));
        }

        [Test]
        public void Run_CancellationRequested_ThrowsOperationCanceledException()
        {
            WriteFetchXml(_queriesDir, "test.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(MakePage(5, moreRecords: true, cookie: "cookie"));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });
            Assert.Throws<OperationCanceledException>(() =>
                runner.Run(MakeQuery("test.fetch.xml"), _tempDir, cts.Token));
        }

        [Test]
        public void Run_ServiceThrowsException_PropagatesException()
        {
            WriteFetchXml(_queriesDir, "test.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Throws(new InvalidOperationException("Dataverse error"));

            var runner = new FetchXmlQueryRunner(mock.Object, _ => { });
            Assert.Throws<InvalidOperationException>(() =>
                runner.Run(MakeQuery("test.fetch.xml"), _tempDir, CancellationToken.None));
        }

        [Test]
        public void Run_LogCallbackInvoked()
        {
            WriteFetchXml(_queriesDir, "test.fetch.xml");
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns(MakePage(2, moreRecords: false));

            var logMessages = new List<string>();
            var runner = new FetchXmlQueryRunner(mock.Object, msg => logMessages.Add(msg));
            runner.Run(MakeQuery("test.fetch.xml"), _tempDir, CancellationToken.None);

            Assert.That(logMessages, Has.Some.Contains("test-query"));
        }
    }
}
