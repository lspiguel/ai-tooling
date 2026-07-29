using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;

using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Queries;

using Moq;
using Moq.Contrib.HttpClient;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.QueriesTests
{
    /// <summary>
    /// Integration-style tests that mock HTTP interactions with Moq.Contrib.HttpClient using
    /// representative Dataverse Web API response shapes. Covers the three webapi query types
    /// defined in Sample.context-exporter-config.json: entity-attributes, optionsets-global,
    /// forms-and-views.
    /// </summary>
    [TestFixture]
    public class WebApiRealDataTests
    {
        private const string EnvUrl = "https://contoso.dynamics.com";
        private const string FakeToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.fake";
        private const string BaseApiPath = "https://contoso.dynamics.com/api/data/v9.2/";

        // -------------------------------------------------------------------------
        // Real-shaped JSON helpers (Dataverse WebAPI OData response format)
        // -------------------------------------------------------------------------

        private static string EntityDefinitionsPage(IEnumerable<object> entities, string? nextLink = null)
        {
            var arr = JArray.FromObject(entities);
            var obj = new JObject { ["value"] = arr };
            if (nextLink != null)
                obj["@odata.nextLink"] = nextLink;
            return obj.ToString();
        }

        private static JObject MakeEntityDef(string logicalName, string label, bool isCustom, params JObject[] attributes)
        {
            var attrArr = new JArray();
            foreach (var a in attributes)
                attrArr.Add(a);

            return new JObject
            {
                ["LogicalName"] = logicalName,
                ["DisplayName"] = new JObject { ["UserLocalizedLabel"] = new JObject { ["Label"] = label } },
                ["PrimaryIdAttribute"] = logicalName + "id",
                ["PrimaryNameAttribute"] = "name",
                ["OwnershipType"] = "UserOwned",
                ["IsCustomEntity"] = isCustom,
                ["Attributes"] = attrArr,
            };
        }

        private static JObject MakeAttribute(string logicalName, string label, string attrType, bool isCustom, string requiredLevel)
        {
            return new JObject
            {
                ["LogicalName"] = logicalName,
                ["DisplayName"] = new JObject { ["UserLocalizedLabel"] = new JObject { ["Label"] = label } },
                ["AttributeType"] = attrType,
                ["IsCustomAttribute"] = isCustom,
                ["RequiredLevel"] = new JObject { ["Value"] = requiredLevel },
            };
        }

        private static JObject MakeGlobalOptionSet(string name, string label, params (int value, string label)[] options)
        {
            var opts = new JArray();
            foreach (var (v, l) in options)
                opts.Add(new JObject { ["Value"] = v, ["Label"] = new JObject { ["UserLocalizedLabel"] = new JObject { ["Label"] = l } } });

            return new JObject
            {
                ["Name"] = name,
                ["DisplayName"] = new JObject { ["UserLocalizedLabel"] = new JObject { ["Label"] = label } },
                ["OptionSetType"] = "Picklist",
                ["Options"] = opts,
            };
        }

        private static JObject MakeEntityDefWithForms(string logicalName, string label)
        {
            return new JObject
            {
                ["LogicalName"] = logicalName,
                ["DisplayName"] = new JObject { ["UserLocalizedLabel"] = new JObject { ["Label"] = label } },
                ["SystemForms"] = new JArray
                {
                    new JObject { ["Name"] = "Main Form", ["Type"] = 2, ["IsDefault"] = true },
                    new JObject { ["Name"] = "Quick Create", ["Type"] = 7, ["IsDefault"] = false },
                },
                ["SavedQueries"] = new JArray
                {
                    new JObject { ["Name"] = "Active Accounts", ["QueryType"] = 0, ["IsDefault"] = true, ["ColumnSetXml"] = "<columnset />" },
                },
            };
        }

        private static QueryDefinition EntityAttributesQuery(int? maxRecords = null) => new QueryDefinition
        {
            Id = "entity-attributes",
            Type = "webapi",
            Path = "EntityDefinitions?$select=LogicalName,DisplayName,PrimaryIdAttribute,PrimaryNameAttribute,OwnershipType,IsCustomEntity&$expand=Attributes($select=LogicalName,DisplayName,AttributeType,IsCustomAttribute,RequiredLevel)",
            ResultKey = "entityAttributes",
            MaxRecords = maxRecords,
        };

        private static QueryDefinition GlobalOptionSetsQuery() => new QueryDefinition
        {
            Id = "optionsets-global",
            Type = "webapi",
            Path = "GlobalOptionSetDefinitions?$select=Name,DisplayName,OptionSetType,Options",
            ResultKey = "globalOptionSets",
        };

        private static QueryDefinition FormsAndViewsQuery(int? maxRecords = null) => new QueryDefinition
        {
            Id = "forms-and-views",
            Type = "webapi",
            Path = "EntityDefinitions?$select=LogicalName,DisplayName&$expand=SystemForms($select=Name,Type,IsDefault),SavedQueries($select=Name,QueryType,IsDefault,ColumnSetXml)",
            ResultKey = "formsAndViews",
            MaxRecords = maxRecords,
        };

        // -------------------------------------------------------------------------
        // 1. entity-attributes — single page
        // -------------------------------------------------------------------------

        [Test]
        public void EntityAttributes_SinglePage_ReturnsAllEntityRecords()
        {
            var handler = new Mock<HttpMessageHandler>();
            var json = EntityDefinitionsPage(new[]
            {
                MakeEntityDef("account", "Account", false,
                    MakeAttribute("accountid", "Account", "Uniqueidentifier", false, "None"),
                    MakeAttribute("name", "Account Name", "String", false, "ApplicationRequired")),
                MakeEntityDef("cust_salesorder", "Sales Order", true,
                    MakeAttribute("cust_salesorderid", "Sales Order", "Uniqueidentifier", true, "None"),
                    MakeAttribute("cust_name", "Name", "String", true, "ApplicationRequired"),
                    MakeAttribute("cust_totalamount", "Total Amount", "Money", true, "None")),
                MakeEntityDef("contact", "Contact", false,
                    MakeAttribute("contactid", "Contact", "Uniqueidentifier", false, "None")),
            });

            handler.SetupAnyRequest().ReturnsResponse(json, "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(EntityAttributesQuery(), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0]!["LogicalName"]!.Value<string>(), Is.EqualTo("account"));
            Assert.That(result[1]!["LogicalName"]!.Value<string>(), Is.EqualTo("cust_salesorder"));
            Assert.That(result[2]!["IsCustomEntity"]!.Value<bool>(), Is.False);
        }

        [Test]
        public void EntityAttributes_SinglePage_ExpandedAttributesPreservedInPayload()
        {
            var handler = new Mock<HttpMessageHandler>();
            var entity = MakeEntityDef("cust_accesslevel", "Access Level", true,
                MakeAttribute("cust_accesslevelid", "Access Level", "Uniqueidentifier", true, "None"),
                MakeAttribute("cust_name", "Name", "String", true, "ApplicationRequired"),
                MakeAttribute("cust_status", "Status", "Picklist", true, "None"));
            var json = EntityDefinitionsPage(new[] { entity });

            handler.SetupAnyRequest().ReturnsResponse(json, "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(EntityAttributesQuery(), CancellationToken.None);

            var attrs = result[0]!["Attributes"] as JArray;
            Assert.That(attrs, Is.Not.Null);
            Assert.That(attrs!.Count, Is.EqualTo(3));
            Assert.That(attrs[1]!["LogicalName"]!.Value<string>(), Is.EqualTo("cust_name"));
            Assert.That(attrs[1]!["AttributeType"]!.Value<string>(), Is.EqualTo("String"));
            Assert.That(attrs[1]!["IsCustomAttribute"]!.Value<bool>(), Is.True);
            Assert.That(attrs[1]!["RequiredLevel"]!["Value"]!.Value<string>(), Is.EqualTo("ApplicationRequired"));
        }

        // -------------------------------------------------------------------------
        // 2. entity-attributes — pagination via @odata.nextLink
        // -------------------------------------------------------------------------

        [Test]
        public void EntityAttributes_TwoPages_AccumulatesAllViaNextLink()
        {
            var page1Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName,PrimaryIdAttribute,PrimaryNameAttribute,OwnershipType,IsCustomEntity&$expand=Attributes($select=LogicalName,DisplayName,AttributeType,IsCustomAttribute,RequiredLevel)";
            var page2Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName,PrimaryIdAttribute,PrimaryNameAttribute,OwnershipType,IsCustomEntity&$expand=Attributes($select=LogicalName,DisplayName,AttributeType,IsCustomAttribute,RequiredLevel)&$skiptoken=%27%3C%27";

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeEntityDef("account", "Account", false), MakeEntityDef("contact", "Contact", false) },
                    nextLink: page2Url), "application/json");

            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeEntityDef("cust_salesorder", "Sales Order", true), MakeEntityDef("cust_workorder", "Work Order", true) }), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(EntityAttributesQuery(), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result[0]!["LogicalName"]!.Value<string>(), Is.EqualTo("account"));
            Assert.That(result[2]!["LogicalName"]!.Value<string>(), Is.EqualTo("cust_salesorder"));
            Assert.That(result[3]!["IsCustomEntity"]!.Value<bool>(), Is.True);
            handler.VerifyRequest(HttpMethod.Get, page1Url, Times.Once());
            handler.VerifyRequest(HttpMethod.Get, page2Url, Times.Once());
        }

        [Test]
        public void EntityAttributes_ThreePages_AccumulatesAllPages()
        {
            var page1Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName&$skiptoken=page1";
            var page2Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName&$skiptoken=page2";
            var page3Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName&$skiptoken=page3";

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeEntityDef("account", "Account", false) }, page2Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeEntityDef("contact", "Contact", false) }, page3Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page3Url)
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeEntityDef("cust_salesorder", "Sales Order", true) }), "application/json");

            var query = new QueryDefinition { Id = "q", Type = "webapi", Path = "EntityDefinitions?$select=LogicalName,DisplayName&$skiptoken=page1", ResultKey = "r" };
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(query, CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(3));
            handler.VerifyAnyRequest(Times.Exactly(3));
        }

        // -------------------------------------------------------------------------
        // 3. entity-attributes — MaxRecords cap
        // -------------------------------------------------------------------------

        [Test]
        public void EntityAttributes_MaxRecords_StopsAfterCap()
        {
            var page1Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName,PrimaryIdAttribute,PrimaryNameAttribute,OwnershipType,IsCustomEntity&$expand=Attributes($select=LogicalName,DisplayName,AttributeType,IsCustomAttribute,RequiredLevel)";
            var page2Url = page1Url + "&$skiptoken=page2";

            var handler = new Mock<HttpMessageHandler>();
            var entities1 = new[] {
                MakeEntityDef("account", "Account", false),
                MakeEntityDef("contact", "Contact", false),
                MakeEntityDef("cust_accesslevel", "Access Level", true),
            };
            var entities2 = new[] {
                MakeEntityDef("cust_salesorder", "Sales Order", true),
                MakeEntityDef("cust_workorder", "Work Order", true),
            };

            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(entities1, page2Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(EntityDefinitionsPage(entities2), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(EntityAttributesQuery(maxRecords: 3), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(3));
            handler.VerifyRequest(HttpMethod.Get, page1Url, Times.Once());
            handler.VerifyRequest(HttpMethod.Get, page2Url, Times.Never());
        }

        // -------------------------------------------------------------------------
        // 4. optionsets-global query
        // -------------------------------------------------------------------------

        [Test]
        public void GlobalOptionSets_SinglePage_ParsesValueArray()
        {
            var handler = new Mock<HttpMessageHandler>();
            var json = EntityDefinitionsPage(new[]
            {
                MakeGlobalOptionSet("cust_accesslevel_status", "Access Level Status",
                    (100000000, "Active"), (100000001, "Inactive"), (100000002, "Archived")),
                MakeGlobalOptionSet("cust_salesorder_type", "Sales Order Type",
                    (100000000, "Standard"), (100000001, "Blanket")),
                MakeGlobalOptionSet("cust_workorder_priority", "Work Order Priority",
                    (1, "Low"), (2, "Normal"), (3, "High"), (4, "Critical")),
            });

            handler.SetupAnyRequest().ReturnsResponse(json, "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0]!["Name"]!.Value<string>(), Is.EqualTo("cust_accesslevel_status"));
            Assert.That(result[0]!["OptionSetType"]!.Value<string>(), Is.EqualTo("Picklist"));
            var opts = result[0]!["Options"] as JArray;
            Assert.That(opts!.Count, Is.EqualTo(3));
            Assert.That(opts![0]["Value"]!.Value<int>(), Is.EqualTo(100000000));
        }

        [Test]
        public void GlobalOptionSets_TwoPages_AccumulatesAll()
        {
            var page1Url = BaseApiPath + "GlobalOptionSetDefinitions?$select=Name,DisplayName,OptionSetType,Options";
            var page2Url = page1Url + "&$skiptoken=nextPage";

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeGlobalOptionSet("cust_status_a", "Status A", (1, "Option1")) },
                    page2Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeGlobalOptionSet("cust_status_b", "Status B", (1, "OptionX")), MakeGlobalOptionSet("cust_status_c", "Status C", (1, "OptionY")) }), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(3));
        }

        // -------------------------------------------------------------------------
        // 5. forms-and-views query
        // -------------------------------------------------------------------------

        [Test]
        public void FormsAndViews_SinglePage_ExpandedFormsAndQueriesPreserved()
        {
            var handler = new Mock<HttpMessageHandler>();
            var json = EntityDefinitionsPage(new[]
            {
                MakeEntityDefWithForms("account", "Account"),
                MakeEntityDefWithForms("contact", "Contact"),
            });

            handler.SetupAnyRequest().ReturnsResponse(json, "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(FormsAndViewsQuery(), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(2));

            var forms = result[0]!["SystemForms"] as JArray;
            Assert.That(forms, Is.Not.Null);
            Assert.That(forms!.Count, Is.EqualTo(2));
            Assert.That(forms[0]!["Name"]!.Value<string>(), Is.EqualTo("Main Form"));
            Assert.That(forms[0]!["IsDefault"]!.Value<bool>(), Is.True);

            var views = result[0]!["SavedQueries"] as JArray;
            Assert.That(views, Is.Not.Null);
            Assert.That(views!.Count, Is.EqualTo(1));
            Assert.That(views[0]!["Name"]!.Value<string>(), Is.EqualTo("Active Accounts"));
        }

        [Test]
        public void FormsAndViews_MaxRecords_StopsAfterCap()
        {
            var page1Url = BaseApiPath + "EntityDefinitions?$select=LogicalName,DisplayName&$expand=SystemForms($select=Name,Type,IsDefault),SavedQueries($select=Name,QueryType,IsDefault,ColumnSetXml)";
            var page2Url = page1Url + "&$skiptoken=next";

            var handler = new Mock<HttpMessageHandler>();
            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeEntityDefWithForms("account", "Account"), MakeEntityDefWithForms("contact", "Contact") },
                    page2Url), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(FormsAndViewsQuery(maxRecords: 2), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(2));
            handler.VerifyRequest(HttpMethod.Get, page2Url, Times.Never());
        }

        // -------------------------------------------------------------------------
        // 6. HTTP headers verification
        // -------------------------------------------------------------------------

        [Test]
        public void WebApi_SetsAuthorizationBearerHeader()
        {
            var handler = new Mock<HttpMessageHandler>();
            HttpRequestMessage? captured = null;

            handler.SetupRequest(req => { captured = req; return true; })
                .ReturnsResponse(EntityDefinitionsPage(Array.Empty<JObject>()), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(captured.Headers.Authorization?.Parameter, Is.EqualTo(FakeToken));
        }

        [Test]
        public void WebApi_SetsODataVersionHeaders()
        {
            var handler = new Mock<HttpMessageHandler>();
            HttpRequestMessage? captured = null;

            handler.SetupRequest(req => { captured = req; return true; })
                .ReturnsResponse(EntityDefinitionsPage(Array.Empty<JObject>()), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(captured!.Headers.TryGetValues("OData-Version", out var vals), Is.True);
            Assert.That(string.Join(",", vals!), Is.EqualTo("4.0"));
        }

        [Test]
        public void WebApi_SetsAcceptHeaderWithODataMetadata()
        {
            var handler = new Mock<HttpMessageHandler>();
            HttpRequestMessage? captured = null;

            handler.SetupRequest(req => { captured = req; return true; })
                .ReturnsResponse(EntityDefinitionsPage(Array.Empty<JObject>()), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(captured!.Headers.Accept.ToString(), Does.Contain("application/json"));
            Assert.That(captured.Headers.Accept.ToString(), Does.Contain("odata.metadata=minimal"));
        }

        [Test]
        public void WebApi_TokenDelegateCalledOnce_EvenWithPagination()
        {
            var page1Url = BaseApiPath + "GlobalOptionSetDefinitions?$select=Name,DisplayName,OptionSetType,Options";
            var page2Url = page1Url + "&$skiptoken=p2";

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeGlobalOptionSet("a", "A", (1, "X")) }, page2Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeGlobalOptionSet("b", "B", (2, "Y")) }), "application/json");

            var tokenCallCount = 0;
            var runner = new WebApiQueryRunner(EnvUrl, () => { tokenCallCount++; return FakeToken; }, handler.CreateClient(), _ => { });
            runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(tokenCallCount, Is.EqualTo(1));
        }

        // -------------------------------------------------------------------------
        // 7. Error responses
        // -------------------------------------------------------------------------

        [Test]
        public void WebApi_Unauthorized_ThrowsHttpRequestExceptionWith401()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest()
                .ReturnsResponse(HttpStatusCode.Unauthorized,
                    "{\"error\":{\"code\":\"0x80048306\",\"message\":\"Authentication failed\"}}", "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });

            var ex = Assert.Throws<HttpRequestException>(() =>
                runner.Run(EntityAttributesQuery(), CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("401"));
            Assert.That(ex.Message, Does.Contain("Authentication failed"));
        }

        [Test]
        public void WebApi_Forbidden_ThrowsHttpRequestExceptionWith403()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest()
                .ReturnsResponse(HttpStatusCode.Forbidden,
                    "{\"error\":{\"code\":\"0x80040220\",\"message\":\"SecLib::CheckPrivilege failed\"}}", "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });

            var ex = Assert.Throws<HttpRequestException>(() =>
                runner.Run(EntityAttributesQuery(), CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("403"));
        }

        [Test]
        public void WebApi_ServiceUnavailable_ThrowsHttpRequestExceptionWithBody()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest()
                .ReturnsResponse(HttpStatusCode.ServiceUnavailable,
                    "{\"error\":{\"code\":\"0x8004502F\",\"message\":\"Service Unavailable\"}}", "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });

            var ex = Assert.Throws<HttpRequestException>(() =>
                runner.Run(GlobalOptionSetsQuery(), CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("503"));
            Assert.That(ex.Message, Does.Contain("Service Unavailable"));
        }

        [Test]
        public void WebApi_ErrorOnSecondPage_ThrowsAfterFirstPage()
        {
            var page1Url = BaseApiPath + "GlobalOptionSetDefinitions?$select=Name,DisplayName,OptionSetType,Options";
            var page2Url = page1Url + "&$skiptoken=p2";

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeGlobalOptionSet("a", "A", (1, "X")) }, page2Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(HttpStatusCode.InternalServerError, "{\"error\":\"crash\"}", "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });

            Assert.Throws<HttpRequestException>(() =>
                runner.Run(GlobalOptionSetsQuery(), CancellationToken.None));

            handler.VerifyRequest(HttpMethod.Get, page1Url, Times.Once());
            handler.VerifyRequest(HttpMethod.Get, page2Url, Times.Once());
        }

        // -------------------------------------------------------------------------
        // 8. Cancellation
        // -------------------------------------------------------------------------

        [Test]
        public void WebApi_CancellationBeforeRequest_ThrowsOperationCanceledException()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest().ReturnsResponse(EntityDefinitionsPage(Array.Empty<JObject>()), "application/json");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });

            Assert.Throws<OperationCanceledException>(() =>
                runner.Run(EntityAttributesQuery(), cts.Token));
        }

        // -------------------------------------------------------------------------
        // 9. Logging
        // -------------------------------------------------------------------------

        [Test]
        public void WebApi_LogsStartAndComplete()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest()
                .ReturnsResponse(EntityDefinitionsPage(new[] { MakeGlobalOptionSet("a", "A", (1, "X")) }), "application/json");

            var logMessages = new List<string>();
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), m => logMessages.Add(m));
            runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(logMessages, Has.Some.Contains("optionsets-global"));
            Assert.That(logMessages, Has.Some.Contains("1"));
        }

        [Test]
        public void WebApi_LogsPageCountsDuringPagination()
        {
            var page1Url = BaseApiPath + "GlobalOptionSetDefinitions?$select=Name,DisplayName,OptionSetType,Options";
            var page2Url = page1Url + "&$skiptoken=p2";

            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.SetupRequest(HttpMethod.Get, page1Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeGlobalOptionSet("a", "A", (1, "X")), MakeGlobalOptionSet("b", "B", (2, "Y")) },
                    page2Url), "application/json");
            handler.SetupRequest(HttpMethod.Get, page2Url)
                .ReturnsResponse(EntityDefinitionsPage(
                    new[] { MakeGlobalOptionSet("c", "C", (3, "Z")) }), "application/json");

            var logMessages = new List<string>();
            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), m => logMessages.Add(m));
            runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            // Should log intermediate page counts and final total
            Assert.That(logMessages, Has.Some.Contains("2"));
            Assert.That(logMessages, Has.Some.Contains("3"));
        }

        // -------------------------------------------------------------------------
        // 10. Empty results
        // -------------------------------------------------------------------------

        [Test]
        public void EntityAttributes_EmptyValueArray_ReturnsEmptyJArray()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest()
                .ReturnsResponse(EntityDefinitionsPage(Array.Empty<JObject>()), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(EntityAttributesQuery(), CancellationToken.None);

            Assert.That(result, Is.Empty);
            handler.VerifyAnyRequest(Times.Once());
        }

        [Test]
        public void GlobalOptionSets_EmptyEnvironment_ReturnsEmptyJArray()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.SetupAnyRequest()
                .ReturnsResponse(EntityDefinitionsPage(Array.Empty<JObject>()), "application/json");

            var runner = new WebApiQueryRunner(EnvUrl, () => FakeToken, handler.CreateClient(), _ => { });
            var result = runner.Run(GlobalOptionSetsQuery(), CancellationToken.None);

            Assert.That(result, Is.Empty);
        }
    }
}
