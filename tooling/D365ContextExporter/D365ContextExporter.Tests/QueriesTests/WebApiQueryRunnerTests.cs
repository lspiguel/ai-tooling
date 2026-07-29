using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Queries;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.QueriesTests
{
    [TestFixture]
    public class WebApiQueryRunnerTests
    {
        private const string EnvUrl = "https://test.crm.dynamics.com";
        private const string FakeToken = "test-bearer-token";

        private static QueryDefinition MakeQuery(string path, int? maxRecords = null) =>
            new QueryDefinition { Id = "test-query", Type = "webapi", Path = path, ResultKey = "testKey", MaxRecords = maxRecords };

        private static string MakePage(int count, string? nextLink = null)
        {
            var arr = new JArray();
            for (var i = 0; i < count; i++)
                arr.Add(new JObject { ["id"] = Guid.NewGuid().ToString() });
            var obj = new JObject { ["value"] = arr };
            if (nextLink != null)
                obj["@odata.nextLink"] = nextLink;
            return obj.ToString();
        }

        private static HttpClient BuildClient(params string[] pageJsonResponses)
        {
            var handler = new SequencedHandler(pageJsonResponses);
            return new HttpClient(handler);
        }

        [Test]
        public void Run_HappyPath_ReturnsAllRecords()
        {
            var client = BuildClient(MakePage(5));
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });

            var result = runner.Run(MakeQuery("accounts"), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(5));
        }

        [Test]
        public void Run_NextLinkPagination_AccumulatesAllPages()
        {
            var page2Url = $"{EnvUrl}/api/data/v9.2/accounts?$skiptoken=2";
            var client = BuildClient(MakePage(5, nextLink: page2Url), MakePage(3));
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });

            var result = runner.Run(MakeQuery("accounts"), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(8));
        }

        [Test]
        public void Run_MaxRecordsCap_StopsAfterCap()
        {
            var page2Url = $"{EnvUrl}/api/data/v9.2/accounts?$skiptoken=2";
            var client = BuildClient(MakePage(5, nextLink: page2Url), MakePage(5));
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });

            var result = runner.Run(MakeQuery("accounts", maxRecords: 5), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(5));
        }

        [Test]
        public void Run_HttpError_ThrowsHttpRequestException()
        {
            var handler = new ErrorHandler(HttpStatusCode.Forbidden);
            var client = new HttpClient(handler);
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });

            Assert.Throws<HttpRequestException>(() =>
                runner.Run(MakeQuery("accounts"), CancellationToken.None));
        }

        [Test]
        public void Run_ServerError_ThrowsHttpRequestException()
        {
            var handler = new ErrorHandler(HttpStatusCode.InternalServerError);
            var client = new HttpClient(handler);
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });

            Assert.Throws<HttpRequestException>(() =>
                runner.Run(MakeQuery("accounts"), CancellationToken.None));
        }

        [Test]
        public void Run_SelectClause_AppendsSelectToUrl()
        {
            var capturer = new CapturingHandler(MakePage(1));
            var client = new HttpClient(capturer);
            var query = new QueryDefinition
            {
                Id = "q",
                Type = "webapi",
                Path = "accounts",
                ResultKey = "r",
                Select = new System.Collections.Generic.List<string> { "name", "accountid" },
            };
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });
            runner.Run(query, CancellationToken.None);

            Assert.That(capturer.LastRequestUri?.ToString(), Does.Contain("$select=name,accountid"));
        }

        [Test]
        public void Run_PathAlreadyHasQueryString_UsesAmpersandSeparatorForSelect()
        {
            var capturer = new CapturingHandler(MakePage(1));
            var client = new HttpClient(capturer);
            var query = new QueryDefinition
            {
                Id = "q",
                Type = "webapi",
                Path = "accounts?$filter=name eq 'test'",
                ResultKey = "r",
                Select = new System.Collections.Generic.List<string> { "name" },
            };
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });
            runner.Run(query, CancellationToken.None);

            Assert.That(capturer.LastRequestUri?.ToString(), Does.Contain("&$select=name"));
            Assert.That(capturer.LastRequestUri?.ToString(), Does.Not.Contain("?$select"));
        }

        [Test]
        public void Run_EmptyValueArray_ReturnsEmptyJArray()
        {
            var body = new Newtonsoft.Json.Linq.JObject { ["value"] = new JArray() }.ToString();
            var client = BuildClient(body);
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });

            var result = runner.Run(MakeQuery("accounts"), CancellationToken.None);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Run_CancellationRequested_ThrowsOperationCanceledException()
        {
            var client = BuildClient(MakePage(5));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });
            Assert.Throws<OperationCanceledException>(() =>
                runner.Run(MakeQuery("accounts"), cts.Token));
        }

        [Test]
        public void Run_NoSelectClause_DoesNotAppendSelectParam()
        {
            var capturer = new CapturingHandler(MakePage(1));
            var client = new HttpClient(capturer);
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, client, _ => { });
            runner.Run(MakeQuery("accounts"), CancellationToken.None);

            Assert.That(capturer.LastRequestUri?.ToString(), Does.Not.Contain("$select"));
        }

        // --- helpers ---

        private sealed class SequencedHandler : HttpMessageHandler
        {
            private readonly string[] _responses;
            private int _index;

            public SequencedHandler(string[] responses) => _responses = responses;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = _index < _responses.Length ? _responses[_index++] : "{}";
                var msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(msg);
            }
        }

        private sealed class ErrorHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _code;

            public ErrorHandler(HttpStatusCode code) => _code = code;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(_code)
                {
                    Content = new StringContent("{\"error\":\"test error\"}", System.Text.Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly string _responseBody;
            public Uri? LastRequestUri { get; private set; }

            public CapturingHandler(string responseBody) => _responseBody = responseBody;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                var msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(msg);
            }
        }
    }
}
