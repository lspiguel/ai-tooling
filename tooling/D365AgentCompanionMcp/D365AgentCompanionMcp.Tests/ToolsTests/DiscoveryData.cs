using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Xml.Linq;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

using Moq;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>
    /// A synthetic environment for the discovery tools (R7): two tables, a global choice, a ribbon, two solutions with
    /// components, and one dependency. Metadata setters are internal in the SDK, so they are set by reflection.
    /// </summary>
    internal static class DiscoveryData
    {
        public const int Lcid = 1033;

        public static readonly Guid AccountId = new Guid("e0000000-0000-0000-0000-000000000001");
        public static readonly Guid WidgetId = new Guid("e0000000-0000-0000-0000-000000000002");
        public static readonly Guid NameColumnId = new Guid("a0000000-0000-0000-0000-000000000001");
        public static readonly Guid SizeOptionSetId = new Guid("05000000-0000-0000-0000-000000000001");
        public static readonly Guid ParentRelationshipId = new Guid("e1000000-0000-0000-0000-000000000001");
        public static readonly Guid MainFormId = new Guid("f0000000-0000-0000-0000-000000000001");
        public static readonly Guid MissingFormId = new Guid("f0000000-0000-0000-0000-0000000000ff");
        public static readonly Guid ViewId = new Guid("50000000-0000-0000-0000-000000000001");
        public static readonly Guid WipSolutionId = new Guid("51000000-0000-0000-0000-000000000001");
        public static readonly Guid PublisherId = new Guid("c0000000-0000-0000-0000-000000000001");
        public static readonly Guid ModifierId = new Guid("d0000000-0000-0000-0000-000000000001");

        /// <summary>The account ribbon, long enough to need several pages under a small cap.</summary>
        public static readonly string RibbonXml = new XDocument(new XElement(
            "RibbonDefinitions",
            Enumerable.Range(0, 120).Select(i => new XElement("CommandDefinition", new XAttribute("Id", $"Mscrm.Command.{i}"), new XAttribute("Note", "quote \" and backslash \\"))))).ToString();

        public static Mock<IOrganizationService> Add(Mock<IOrganizationService> mock)
        {
            var account = Account();
            var widget = Table("new_widget", "new_Widget", "Widget", WidgetId, 10001, isCustom: true);
            var tables = new[] { account, widget };

            mock.Setup(s => s.Execute(It.IsAny<RetrieveAllEntitiesRequest>())).Returns(() => Response(new RetrieveAllEntitiesResponse(), "EntityMetadata", tables));
            mock.Setup(s => s.Execute(It.IsAny<RetrieveEntityRequest>())).Returns((OrganizationRequest request) =>
            {
                var name = ((RetrieveEntityRequest)request).LogicalName;
                var table = tables.FirstOrDefault(t => t.LogicalName == name) ?? throw NotFound($"Could not find entity with name '{name}'.");
                return Response(new RetrieveEntityResponse(), "EntityMetadata", table);
            });
            mock.Setup(s => s.Execute(It.IsAny<RetrieveAllOptionSetsRequest>())).Returns(() => Response(new RetrieveAllOptionSetsResponse(), "OptionSetMetadata", new OptionSetMetadataBase[] { SizeOptionSet() }));
            mock.Setup(s => s.Execute(It.IsAny<RetrieveOptionSetRequest>())).Returns((OrganizationRequest request) =>
                ((RetrieveOptionSetRequest)request).Name == "new_size"
                    ? Response(new RetrieveOptionSetResponse(), "OptionSetMetadata", SizeOptionSet())
                    : throw NotFound("Could not find optionset."));
            mock.Setup(s => s.Execute(It.IsAny<RetrieveAttributeRequest>())).Returns((OrganizationRequest request) =>
            {
                var attribute = (RetrieveAttributeRequest)request;
                var found = tables.FirstOrDefault(t => t.LogicalName == attribute.EntityLogicalName)?.Attributes.FirstOrDefault(a => a.LogicalName == attribute.LogicalName)
                    ?? throw NotFound("Could not find attribute.");
                return Response(new RetrieveAttributeResponse(), "AttributeMetadata", found);
            });
            mock.Setup(s => s.Execute(It.IsAny<RetrieveMetadataChangesRequest>())).Returns(() =>
                Response(new RetrieveMetadataChangesResponse(), "EntityMetadata", new EntityMetadataCollection { account }));
            mock.Setup(s => s.Execute(It.IsAny<RetrieveEntityRibbonRequest>())).Returns(() => Response(new RetrieveEntityRibbonResponse(), "CompressedEntityXml", Zip(RibbonXml)));
            mock.Setup(s => s.Execute(It.IsAny<RetrieveApplicationRibbonRequest>())).Returns(() => Response(new RetrieveApplicationRibbonResponse(), "CompressedApplicationRibbonXml", Zip("<RibbonDefinitions />")));
            mock.Setup(s => s.Execute(It.IsAny<RetrieveRequiredComponentsRequest>())).Returns(() =>
            {
                var dependency = new Entity("dependency")
                {
                    ["requiredcomponentobjectid"] = AccountId,
                    ["requiredcomponenttype"] = new OptionSetValue(1),
                    ["dependencytype"] = new OptionSetValue(2),
                };
                return Response(new RetrieveRequiredComponentsResponse(), "EntityCollection", new EntityCollection(new List<Entity> { dependency }));
            });
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns((QueryBase query) => RetrieveMultiple(query));
            return mock;
        }

        public static byte[] Zip(string xml)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            using (var writer = new StreamWriter(archive.CreateEntry("RibbonXml.xml").Open(), new UTF8Encoding(true)))
            {
                writer.Write(xml);
            }

            return stream.ToArray();
        }

        public static T Set<T>(T target, string property, object? value)
            where T : class
        {
            // Setters are internal or private on the declaring type, so look the property up where it is declared.
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetProperty(property, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
                if (info?.GetSetMethod(nonPublic: true) is System.Reflection.MethodInfo setter)
                {
                    setter.Invoke(target, new[] { value });
                    return target;
                }
            }

            throw new InvalidOperationException($"No setter for {property} on {target.GetType().Name}.");
        }

        public static FaultException<OrganizationServiceFault> NotFound(string message)
        {
            var fault = new OrganizationServiceFault { ErrorCode = unchecked((int)0x80060888), Message = message };
            return new FaultException<OrganizationServiceFault>(fault, message);
        }

        private static OrganizationResponse Response(OrganizationResponse response, string key, object value)
        {
            response.Results[key] = value;
            return response;
        }

        private static EntityCollection RetrieveMultiple(QueryBase query)
        {
            if (query is FetchExpression fetch)
            {
                return FetchPage(XDocument.Parse(fetch.Query));
            }

            var expression = (QueryExpression)query;
            switch (expression.EntityName)
            {
                case "solution":
                    if (expression.Criteria.Conditions.Any(c => c.AttributeName == "uniquename" && (string)c.Values[0] != "WipA"))
                    {
                        return new EntityCollection();
                    }

                    return new EntityCollection(new List<Entity> { WipSolution() });
                case "solutioncomponent":
                    return expression.LinkEntities.Any(l => l.LinkToEntityName == "solution") ? Holders() : Components();
                case "systemform":
                    return new EntityCollection(new List<Entity> { MainForm() });
                case "savedquery":
                    return new EntityCollection(new List<Entity> { new Entity("savedquery", ViewId) { ["name"] = "Active Accounts" } });
                default:
                    return new EntityCollection();
            }
        }

        public static EntityCollection FetchPage(XDocument fetch)
        {
            var page = int.Parse(fetch.Root!.Attribute("page")!.Value);
            var count = int.Parse(fetch.Root!.Attribute("count")!.Value);
            var all = Enumerable.Range(1, 5).Select(i => new Entity("systemform", new Guid($"f1000000-0000-0000-0000-{i:000000000000}"))
            {
                ["formid"] = new Guid($"f1000000-0000-0000-0000-{i:000000000000}"),
                ["name"] = $"Form {i}",
                ["type"] = new OptionSetValue(2),
                ["objecttypecode"] = "account",
                FormattedValues = { ["type"] = "Main" },
            }).ToList();
            return new EntityCollection(all.Skip((page - 1) * count).Take(count).ToList())
            {
                MoreRecords = page * count < all.Count,
                PagingCookie = $"<cookie page=\"{page}\" />",
            };
        }

        private static Entity WipSolution() => new Entity("solution", WipSolutionId)
        {
            ["uniquename"] = "WipA",
            ["friendlyname"] = "WIP A",
            ["version"] = "1.0.0.0",
            ["ismanaged"] = false,
            ["isvisible"] = true,
            ["modifiedon"] = new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc),
            ["publisherid"] = new EntityReference("publisher", PublisherId),
            ["pub.uniquename"] = new AliasedValue("publisher", "uniquename", "publisher1"),
            ["pub.customizationprefix"] = new AliasedValue("publisher", "customizationprefix", "p1"),
        };

        private static EntityCollection Components() => new EntityCollection(new List<Entity>
        {
            Component("5c000000-0000-0000-0000-000000000001", 1, AccountId, 2),
            Component("5c000000-0000-0000-0000-000000000002", 2, NameColumnId, null),
            Component("5c000000-0000-0000-0000-000000000003", 60, MainFormId, null),
            Component("5c000000-0000-0000-0000-000000000004", 29, new Guid("29000000-0000-0000-0000-000000000001"), null),
        });

        private static Entity Component(string id, int type, Guid objectId, int? behaviour)
        {
            var row = new Entity("solutioncomponent", new Guid(id))
            {
                ["componenttype"] = new OptionSetValue(type),
                ["objectid"] = objectId,
                ["ismetadata"] = type <= 10,
            };
            if (behaviour.HasValue)
            {
                row["rootcomponentbehavior"] = new OptionSetValue(behaviour.Value);
            }

            return row;
        }

        private static EntityCollection Holders() => new EntityCollection(new List<Entity>
        {
            Holder("Base", true),
            Holder("Base", true),
        });

        private static Entity Holder(string solution, bool managed) => new Entity("solutioncomponent", Guid.NewGuid())
        {
            ["componenttype"] = new OptionSetValue(1),
            ["objectid"] = AccountId,
            ["sol.uniquename"] = new AliasedValue("solution", "uniquename", solution),
            ["sol.ismanaged"] = new AliasedValue("solution", "ismanaged", managed),
        };

        private static Entity MainForm() => new Entity("systemform", MainFormId)
        {
            ["name"] = "Account",
            ["versionnumber"] = 9007199254740993L,
            ["modifiedon"] = new DateTime(2026, 10, 29, 8, 30, 0, DateTimeKind.Utc),
            ["modifiedby"] = new EntityReference("systemuser", ModifierId),
            FormattedValues = { ["modifiedby"] = "Test User" },
        };

        private static EntityMetadata Account()
        {
            var account = Table("account", "Account", "Account", AccountId, 1, isCustom: false);
            Set(account, "PrimaryIdAttribute", "accountid");
            Set(account, "PrimaryNameAttribute", "name");
            Set(account, "OwnershipType", (OwnershipTypes?)OwnershipTypes.UserOwned);

            var name = Column(new StringAttributeMetadata { MaxLength = 160 }, "name", "Name", NameColumnId, AttributeRequiredLevel.ApplicationRequired);
            var parent = Column(new LookupAttributeMetadata { Targets = new[] { "account" } }, "parentaccountid", "Parent Account", Guid.NewGuid(), AttributeRequiredLevel.None);
            var flag = Column(
                new BooleanAttributeMetadata(new BooleanOptionSetMetadata(new OptionMetadata(new Label("Yes", Lcid), 1), new OptionMetadata(new Label("No", Lcid), 0))),
                "new_flag",
                "Flag",
                Guid.NewGuid(),
                AttributeRequiredLevel.None,
                isCustom: true);
            var size = Column(new PicklistAttributeMetadata { OptionSet = SizeOptionSet() }, "new_size", "Size", Guid.NewGuid(), AttributeRequiredLevel.None, isCustom: true);
            Set(account, "Attributes", new AttributeMetadata[] { parent, name, flag, size });

            var key = new EntityKeyMetadata { MetadataId = new Guid("4e000000-0000-0000-0000-000000000001"), LogicalName = "new_accountnumber", SchemaName = "new_AccountNumber", DisplayName = new Label("Account number", Lcid), KeyAttributes = new[] { "accountnumber" } };
            Set(key, "IsManaged", (bool?)false);
            Set(account, "Keys", new[] { key });

            var selfReference = new OneToManyRelationshipMetadata
            {
                SchemaName = "account_parent_account",
                ReferencedEntity = "account",
                ReferencedAttribute = "accountid",
                ReferencingEntity = "account",
                ReferencingAttribute = "parentaccountid",
                CascadeConfiguration = new CascadeConfiguration { Assign = CascadeType.NoCascade, Delete = CascadeType.RemoveLink, Merge = CascadeType.NoCascade, Reparent = CascadeType.NoCascade, Share = CascadeType.NoCascade, Unshare = CascadeType.NoCascade },
                MetadataId = ParentRelationshipId,
            };
            var manyToMany = new ManyToManyRelationshipMetadata
            {
                SchemaName = "new_account_widget",
                Entity1LogicalName = "account",
                Entity1IntersectAttribute = "accountid",
                Entity2LogicalName = "new_widget",
                Entity2IntersectAttribute = "new_widgetid",
                IntersectEntityName = "new_account_widget",
                MetadataId = Guid.NewGuid(),
            };
            Set(account, "OneToManyRelationships", new[] { selfReference });
            Set(account, "ManyToOneRelationships", new[] { selfReference });
            Set(account, "ManyToManyRelationships", new[] { manyToMany });
            return account;
        }

        private static EntityMetadata Table(string logicalName, string schemaName, string displayName, Guid id, int objectTypeCode, bool isCustom)
        {
            var table = new EntityMetadata { LogicalName = logicalName, SchemaName = schemaName, DisplayName = new Label(displayName, Lcid), MetadataId = id };
            Set(table, "ObjectTypeCode", (int?)objectTypeCode);
            Set(table, "IsCustomEntity", (bool?)isCustom);
            Set(table, "IsManaged", (bool?)!isCustom);
            Set(table, "IsIntersect", (bool?)false);
            Set(table, "IsCustomizable", new BooleanManagedProperty(true));
            Set(table, "Attributes", Array.Empty<AttributeMetadata>());
            return table;
        }

        private static AttributeMetadata Column(AttributeMetadata column, string logicalName, string displayName, Guid id, AttributeRequiredLevel level, bool isCustom = false)
        {
            column.LogicalName = logicalName;
            column.SchemaName = logicalName;
            column.DisplayName = new Label(displayName, Lcid);
            column.MetadataId = id;
            column.RequiredLevel = new AttributeRequiredLevelManagedProperty(level);
            Set(column, "IsCustomAttribute", (bool?)isCustom);
            Set(column, "IsManaged", (bool?)false);
            Set(column, "IsValidForForm", (bool?)true);
            return column;
        }

        private static OptionSetMetadata SizeOptionSet()
        {
            var optionSet = new OptionSetMetadata(new OptionMetadataCollection(new List<OptionMetadata>
            {
                new OptionMetadata(new Label("Small", Lcid), 100000000),
                new OptionMetadata(new Label("Large", Lcid), 100000001),
            }))
            {
                Name = "new_size",
                IsGlobal = true,
                DisplayName = new Label("Size", Lcid),
                MetadataId = SizeOptionSetId,
                OptionSetType = OptionSetType.Picklist,
            };
            Set<OptionSetMetadataBase>(optionSet, "IsCustomOptionSet", (bool?)true);
            Set<OptionSetMetadataBase>(optionSet, "IsManaged", (bool?)false);
            return optionSet;
        }
    }
}
