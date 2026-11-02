using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using Moq;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>A mocked organization service with synthetic data only (R7).</summary>
    internal static class Dataverse
    {
        public static readonly Guid BusinessUnit = new Guid("b0000000-0000-0000-0000-000000000001");

        public static Mock<IOrganizationService> Mock(Guid organizationId, Guid userId, int publisherCount = 3)
        {
            var mock = new Mock<IOrganizationService>();
            mock.Setup(s => s.Execute(It.IsAny<WhoAmIRequest>())).Returns(() =>
            {
                var response = new WhoAmIResponse();
                response.Results["UserId"] = userId;
                response.Results["BusinessUnitId"] = BusinessUnit;
                response.Results["OrganizationId"] = organizationId;
                return response;
            });
            mock.Setup(s => s.Execute(It.IsAny<RetrieveVersionRequest>())).Returns(() =>
            {
                var response = new RetrieveVersionResponse();
                response.Results["Version"] = "9.2.0.0";
                return response;
            });
            mock.Setup(s => s.Retrieve("systemuser", It.IsAny<Guid>(), It.IsAny<ColumnSet>())).Returns(new Entity("systemuser")
            {
                ["firstname"] = "Test",
                ["lastname"] = "User",
                ["fullname"] = "Test User",
                ["domainname"] = "test.user@example.invalid",
            });
            mock.Setup(s => s.Retrieve("organization", organizationId, It.IsAny<ColumnSet>())).Returns(new Entity("organization") { ["languagecode"] = 1033 });

            var publishers = Enumerable.Range(1, publisherCount).Select(Publisher).ToList();
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns((QueryBase query) => Page(publishers, ((QueryExpression)query).PageInfo));
            return mock;
        }

        private static Entity Publisher(int index) => new Entity("publisher", new Guid($"c0000000-0000-0000-0000-{index:000000000000}"))
        {
            ["uniquename"] = $"publisher{index}",
            ["friendlyname"] = $"Publisher {index}",
            ["customizationprefix"] = $"p{index}",
            ["customizationoptionvalueprefix"] = 10000 + index,
            ["isreadonly"] = false,
        };

        private static EntityCollection Page(List<Entity> all, PagingInfo paging)
        {
            var skip = (paging.PageNumber - 1) * paging.Count;
            var page = new EntityCollection(all.Skip(skip).Take(paging.Count).ToList())
            {
                MoreRecords = skip + paging.Count < all.Count,
                PagingCookie = $"<cookie page=\"{paging.PageNumber}\" />",
            };
            return page;
        }
    }
}
