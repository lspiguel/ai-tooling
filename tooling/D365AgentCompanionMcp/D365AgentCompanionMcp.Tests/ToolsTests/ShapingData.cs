using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using Moq;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.ToolsTests
{
    /// <summary>
    /// A synthetic environment whose solutions change as components are added and removed, built on the metadata of
    /// <see cref="DiscoveryData"/> (R7). It behaves as spike S2 observed: tables keep the behaviour the flags ask for,
    /// and a form added without its table brings the table as a shell.
    /// </summary>
    internal sealed class ShapingData
    {
        public static readonly Guid ManagedSolutionId = new Guid("51000000-0000-0000-0000-000000000002");
        public static readonly Guid WorkflowId = new Guid("29000000-0000-0000-0000-000000000001");

        public ShapingData(Mock<IOrganizationService> mock)
        {
            DiscoveryData.Add(mock);
            mock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns((QueryBase query) => RetrieveMultiple((QueryExpression)query));
            mock.Setup(s => s.Execute(It.IsAny<AddSolutionComponentRequest>())).Returns((OrganizationRequest request) => Add((AddSolutionComponentRequest)request));
            mock.Setup(s => s.Execute(It.IsAny<RemoveSolutionComponentRequest>())).Returns((OrganizationRequest request) => Remove((RemoveSolutionComponentRequest)request));
            mock.Setup(s => s.Create(It.IsAny<Entity>())).Returns((Entity entity) =>
            {
                Created.Add(entity);
                return Guid.NewGuid();
            });
        }

        /// <summary>What WipA holds: type, ID and root behaviour.</summary>
        public List<(int Type, Guid Id, int? Behavior)> Components { get; } = new List<(int, Guid, int?)>();

        public List<AddSolutionComponentRequest> Adds { get; } = new List<AddSolutionComponentRequest>();

        public List<RemoveSolutionComponentRequest> Removes { get; } = new List<RemoveSolutionComponentRequest>();

        public List<Entity> Created { get; } = new List<Entity>();

        /// <summary>Components whose add fails.</summary>
        public HashSet<Guid> Failing { get; } = new HashSet<Guid>();

        /// <summary>A component the platform slips in with the next add — what R1's read-back must catch.</summary>
        public (int Type, Guid Id, int? Behavior)? Stowaway { get; set; }

        private OrganizationResponse Add(AddSolutionComponentRequest request)
        {
            Adds.Add(request);
            if (Failing.Contains(request.ComponentId))
            {
                throw DiscoveryData.NotFound("The component could not be added.");
            }

            int? behaviour = request.ComponentType == 1 ? (request.IncludedComponentSettingsValues == null ? 1 : 2) : (int?)null;
            Components.Add((request.ComponentType, request.ComponentId, behaviour));
            if (request.ComponentType == 60 && !Components.Any(c => c.Type == 1 && c.Id == DiscoveryData.AccountId))
            {
                Components.Add((1, DiscoveryData.AccountId, 2));
            }

            if (Stowaway is { } stowaway)
            {
                Components.Add(stowaway);
                Stowaway = null;
            }

            return new AddSolutionComponentResponse();
        }

        private OrganizationResponse Remove(RemoveSolutionComponentRequest request)
        {
            Removes.Add(request);
            Components.RemoveAll(c => c.Type == request.ComponentType && c.Id == request.ComponentId);
            return new RemoveSolutionComponentResponse();
        }

        private EntityCollection RetrieveMultiple(QueryExpression query)
        {
            string? Equal(string attribute) => query.Criteria.Conditions.FirstOrDefault(c => c.AttributeName == attribute && c.Operator == ConditionOperator.Equal)?.Values[0]?.ToString();

            switch (query.EntityName)
            {
                case "solution":
                    var solutions = new[]
                    {
                        new Entity("solution", DiscoveryData.WipSolutionId) { ["uniquename"] = "WipA", ["ismanaged"] = false },
                        new Entity("solution", ManagedSolutionId) { ["uniquename"] = "ManagedOne", ["ismanaged"] = true },
                    };
                    return new EntityCollection(solutions.Where(s => Equal("uniquename") is not string name || (string)s["uniquename"] == name).ToList());
                case "publisher":
                    var publishers = new[]
                    {
                        new Entity("publisher", DiscoveryData.PublisherId) { ["uniquename"] = "publisher1", ["friendlyname"] = "Publisher 1", ["customizationprefix"] = "p1", ["customizationoptionvalueprefix"] = 10001 },
                    };
                    return new EntityCollection(publishers.Where(p => Equal("uniquename") is not string name || (string)p["uniquename"] == name).ToList());
                case "solutioncomponent":
                    if (Equal("solutionid") != DiscoveryData.WipSolutionId.ToString())
                    {
                        return new EntityCollection();
                    }

                    var rows = Components
                        .Where(c => Equal("objectid") is not string id || c.Id.ToString() == id)
                        .Where(c => Equal("componenttype") is not string type || c.Type.ToString() == type)
                        .Select(c =>
                        {
                            var row = new Entity("solutioncomponent", Guid.NewGuid()) { ["componenttype"] = new OptionSetValue(c.Type), ["objectid"] = c.Id };
                            if (c.Behavior.HasValue)
                            {
                                row["rootcomponentbehavior"] = new OptionSetValue(c.Behavior.Value);
                            }

                            return row;
                        });
                    return new EntityCollection(rows.ToList());
                case "systemform":
                    return InIds(query, new Entity("systemform", DiscoveryData.MainFormId) { ["name"] = "Account", ["objecttypecode"] = "account" });
                case "savedquery":
                    return InIds(query, new Entity("savedquery", DiscoveryData.ViewId) { ["name"] = "Active Accounts", ["returnedtypecode"] = "account" });
                default:
                    return new EntityCollection();
            }
        }

        private static EntityCollection InIds(QueryExpression query, params Entity[] rows)
        {
            var ids = query.Criteria.Conditions.Single(c => c.Operator == ConditionOperator.In).Values.Cast<Guid>().ToList();
            return new EntityCollection(rows.Where(r => ids.Contains(r.Id)).ToList());
        }
    }
}
