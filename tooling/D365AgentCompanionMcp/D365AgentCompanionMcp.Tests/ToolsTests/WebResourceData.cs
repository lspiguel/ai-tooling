using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

using Moq;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>
    /// A synthetic environment for <c>update_web_resources</c> (R7): one unmanaged WIP under publisher prefix <c>p1</c>,
    /// web resources whose <c>versionnumber</c> rises on every update and publish as spike S10 observed, and a platform
    /// that adds a created resource to the solution it was created in.
    /// </summary>
    internal sealed class WebResourceData
    {
        public const string Wip = "WipA";
        public const string ExistingName = "p1_/scripts/account.form.js";
        public const string ManagedName = "msdyn_/scripts/first-party.js";
        public const string OutsideName = "p1_/scripts/not-in-wip.js";
        public static readonly Guid WipId = new Guid("51000000-0000-0000-0000-000000000001");
        public static readonly Guid PublisherId = new Guid("c0000000-0000-0000-0000-000000000001");
        public static readonly Guid ExistingId = new Guid("61000000-0000-0000-0000-000000000001");
        public static readonly Guid ManagedId = new Guid("61000000-0000-0000-0000-000000000002");
        public static readonly Guid OutsideId = new Guid("61000000-0000-0000-0000-000000000003");
        public static readonly Guid ModifierId = new Guid("d0000000-0000-0000-0000-000000000001");

        private long _version = 1000;

        public WebResourceData(Mock<IOrganizationService> mock)
        {
            AddRow(ExistingId, ExistingName, "// old", managed: false);
            AddRow(ManagedId, ManagedName, "// first party", managed: true);
            AddRow(OutsideId, OutsideName, "// elsewhere", managed: false);
            InWip.Add(ExistingId);
            InWip.Add(ManagedId);

            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns((QueryBase query) => RetrieveMultiple((QueryExpression)query));
            mock.Setup(s => s.Retrieve("publisher", PublisherId, It.IsAny<ColumnSet>())).Returns(new Entity("publisher", PublisherId) { ["customizationprefix"] = "p1" });
            mock.Setup(s => s.Retrieve("webresource", It.IsAny<Guid>(), It.IsAny<ColumnSet>())).Returns((string name, Guid id, ColumnSet columns) => Clone(Rows[id]));
            mock.Setup(s => s.Update(It.IsAny<Entity>())).Callback((Entity entity) =>
            {
                Updates.Add(entity);
                Rows[entity.Id]["content"] = entity["content"];
                Rows[entity.Id]["versionnumber"] = ++_version;
            });
            mock.Setup(s => s.Execute(It.IsAny<RetrieveUnpublishedRequest>())).Returns((OrganizationRequest request) =>
            {
                var id = ((RetrieveUnpublishedRequest)request).Target.Id;
                var response = new RetrieveUnpublishedResponse();
                response.Results["Entity"] = new Entity("webresource", id) { ["content"] = UnpublishedContent.TryGetValue(id, out var content) ? content : Rows[id]["content"] };
                return response;
            });
            mock.Setup(s => s.Execute(It.IsAny<CreateRequest>())).Returns((OrganizationRequest request) =>
            {
                var create = (CreateRequest)request;
                Creates.Add(create);
                var id = Guid.NewGuid();
                AddRow(id, (string)create.Target["name"], "created", managed: false);
                Rows[id]["content"] = create.Target["content"];
                Rows[id]["webresourcetype"] = create.Target["webresourcetype"];
                InWip.Add(id);
                var response = new CreateResponse();
                response.Results["id"] = id;
                return response;
            });
            mock.Setup(s => s.Execute(It.IsAny<PublishXmlRequest>())).Returns((OrganizationRequest request) =>
            {
                var publish = (PublishXmlRequest)request;
                if (PublishFails)
                {
                    throw new InvalidOperationException("The publish failed.");
                }

                Publishes.Add(publish);
                foreach (var id in System.Xml.Linq.XDocument.Parse(publish.ParameterXml).Descendants("webresource").Select(e => Guid.Parse(e.Value)))
                {
                    Rows[id]["versionnumber"] = ++_version;
                }

                return new PublishXmlResponse();
            });
        }

        public Dictionary<Guid, Entity> Rows { get; } = new Dictionary<Guid, Entity>();

        public HashSet<Guid> InWip { get; } = new HashSet<Guid>();

        public Dictionary<Guid, string> UnpublishedContent { get; } = new Dictionary<Guid, string>();

        public List<Entity> Updates { get; } = new List<Entity>();

        public List<CreateRequest> Creates { get; } = new List<CreateRequest>();

        public List<PublishXmlRequest> Publishes { get; } = new List<PublishXmlRequest>();

        public bool PublishFails { get; set; }

        /// <summary>A component the platform slips into the WIP once something has been written, for the read-back.</summary>
        public (int Type, Guid Id)? Stowaway { get; set; }

        public string VersionOf(Guid id) => ((long)Rows[id]["versionnumber"]).ToString(System.Globalization.CultureInfo.InvariantCulture);

        public void Bump(Guid id) => Rows[id]["versionnumber"] = ++_version;

        private static Entity Clone(Entity row)
        {
            var copy = new Entity(row.LogicalName, row.Id);
            copy.Attributes.AddRange(row.Attributes);
            return copy;
        }

        private void AddRow(Guid id, string name, string content, bool managed) => Rows[id] = new Entity("webresource", id)
        {
            ["name"] = name,
            ["webresourcetype"] = new OptionSetValue(3),
            ["ismanaged"] = managed,
            ["versionnumber"] = ++_version,
            ["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)),
            ["modifiedby"] = new EntityReference("systemuser", ModifierId) { Name = "Another Developer" },
        };

        private EntityCollection RetrieveMultiple(QueryExpression query)
        {
            string? Equal(string attribute) => query.Criteria.Conditions.FirstOrDefault(c => c.AttributeName == attribute && c.Operator == ConditionOperator.Equal)?.Values[0]?.ToString();

            switch (query.EntityName)
            {
                case "solution":
                    var solution = new Entity("solution", WipId) { ["uniquename"] = Wip, ["ismanaged"] = false, ["publisherid"] = new EntityReference("publisher", PublisherId) };
                    var managedSolution = new Entity("solution", Guid.NewGuid()) { ["uniquename"] = "ManagedOne", ["ismanaged"] = true, ["publisherid"] = new EntityReference("publisher", PublisherId) };
                    return new EntityCollection(new[] { solution, managedSolution }.Where(s => Equal("uniquename") is not string name || (string)s["uniquename"] == name).ToList());
                case "organization":
                    return new EntityCollection(new List<Entity> { new Entity("organization", Guid.NewGuid()) { ["maxuploadfilesize"] = 5000 } });
                case "solutioncomponent":
                    if (Equal("solutionid") != WipId.ToString())
                    {
                        return new EntityCollection();
                    }

                    var components = InWip.Select(id => (Type: 61, Id: id)).ToList();
                    if (Stowaway is { } stowaway && (Creates.Count > 0 || Updates.Count > 0))
                    {
                        components.Add(stowaway);
                    }

                    return new EntityCollection(components.Select(c => new Entity("solutioncomponent", Guid.NewGuid()) { ["componenttype"] = new OptionSetValue(c.Type), ["objectid"] = c.Id }).ToList());
                case "webresource":
                    var byName = Equal("name");
                    var ids = query.Criteria.Conditions.FirstOrDefault(c => c.Operator == ConditionOperator.In)?.Values.Cast<Guid>().ToList();
                    return new EntityCollection(Rows.Values
                        .Where(r => byName == null || (string)r["name"] == byName)
                        .Where(r => ids == null || ids.Contains(r.Id))
                        .Select(Clone)
                        .ToList());
                default:
                    return new EntityCollection();
            }
        }
    }
}
