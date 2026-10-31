// <copyright file="SolutionTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The solution Read tools — <c>list_solutions</c>, <c>list_solution_components</c>, <c>get_component_state</c>
    /// and <c>get_required_components</c> (SPEC §6.5).
    /// </summary>
    internal static class SolutionTools
    {
        /// <summary>The <c>list_solutions</c> tool name.</summary>
        public const string ListSolutions = "list_solutions";

        /// <summary>The <c>list_solution_components</c> tool name.</summary>
        public const string ListSolutionComponents = "list_solution_components";

        /// <summary>The <c>get_component_state</c> tool name.</summary>
        public const string GetComponentState = "get_component_state";

        /// <summary>The <c>get_required_components</c> tool name.</summary>
        public const string GetRequiredComponents = "get_required_components";

        /// <summary>Solutions never reported as holding a requirement: the default solution and the active layer.</summary>
        internal static readonly IReadOnlyCollection<string> SystemSolutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Default", "Active" };

        private const string Managed = "managed";
        private const string IncludeHidden = "includeHidden";
        private const string NameContains = "nameContains";
        private const string PublisherUniqueName = "publisherUniqueName";
        private const string SolutionUniqueName = "solutionUniqueName";
        private const string ComponentTypesArgument = "componentTypes";
        private const string Components = "components";
        private const string PublisherAlias = "pub";
        private const string SolutionAlias = "sol";

        /// <summary>Defines <c>list_solutions</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition ListSolutionsTool() => ToolDefinition.Paged(
            ListSolutions,
            ToolTier.Read,
            "Solutions with version, managed and visible state and publisher, ordered by friendly name — transport and WIP solutions, and the managed solutions first-party apps installed. Hidden system solutions are left out unless includeHidden. Paged.",
            100,
            InputSchema.ForConnection()
                .Enum(Managed, "all, unmanaged or managed solutions.", new[] { "all", "unmanaged", "managed" }, "all")
                .Boolean(IncludeHidden, "Include hidden solutions (isvisible = false).", false)
                .String(NameContains, "Matches the unique or the friendly name.")
                .String(PublisherUniqueName, "Only solutions of this publisher."),
            "solutions",
            OutputSchema.Object(
                ("solutionId", OutputSchema.Guid()),
                ("uniqueName", OutputSchema.String()),
                ("friendlyName", OutputSchema.String()),
                ("version", OutputSchema.String()),
                ("isManaged", OutputSchema.Boolean()),
                ("isVisible", OutputSchema.Boolean()),
                ("publisher", OutputSchema.Object(("publisherId", OutputSchema.Guid()), ("uniqueName", OutputSchema.String()), ("customizationPrefix", OutputSchema.String()))),
                ("installedOn", NullableDateTime()),
                ("modifiedOn", OutputSchema.DateTime()),
                ("description", OutputSchema.NullableString())),
            OpenSolutions,
            input => input.WithReason(required: false));

        /// <summary>Defines <c>list_solution_components</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition ListSolutionComponentsTool() => ToolDefinition.Paged(
            ListSolutionComponents,
            ToolTier.Read,
            "Components in a solution with type, object ID, root component behaviour and root component. Run it after every add and read every page to compare against what was intended (R1): any unexpected component stops the workflow for review. Names are resolved for tables, columns (table.column), global option sets, views, forms, web resources, sitemaps and app modules; otherwise null. Paged.",
            250,
            InputSchema.ForConnection()
                .String(SolutionUniqueName, "The solution's unique name; managed or unmanaged.", required: true)
                .IntegerArray(ComponentTypesArgument, "Only these component type codes."),
            "components",
            OutputSchema.Object(
                ("solutionComponentId", OutputSchema.Guid()),
                ("componentType", ComponentTypeSchema()),
                ("objectId", OutputSchema.Guid()),
                ("name", OutputSchema.NullableString()),
                ("rootComponentBehavior", OutputSchema.NullableEnum("includeSubcomponents", "doNotIncludeSubcomponents", "includeAsShellOnly")),
                ("rootSolutionComponentId", new JObject { ["type"] = new JArray("string", "null"), ["format"] = "uuid" }),
                ("isMetadata", OutputSchema.Boolean())),
            OpenSolutionComponents,
            input => input.WithReason(required: false),
            PageField.Every("solution", OutputSchema.Object(("solutionId", OutputSchema.Guid()), ("uniqueName", OutputSchema.String()), ("isManaged", OutputSchema.Boolean()))));

        /// <summary>Defines <c>get_component_state</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetComponentStateTool() => ToolDefinition.Simple(
            GetComponentState,
            ToolTier.Read,
            "Row version, modified-on and modified-by of row-backed components — views (26), forms (60), web resources (61), sitemaps (62), app modules (80) — for the pre-import overwrite check. found: false means the component was deleted. Tables and columns have no comparable version: use a PAC re-export and diff.",
            InputSchema.ForConnection()
                .ObjectArray(Components, "1–200 components, each { type, id }.", RowBackedComponentSchema(), 1, 200)
                .WithReason(required: false),
            OutputSchema.Object(("components", OutputSchema.Array(OutputSchema.Object(
                ("type", OutputSchema.Integer()),
                ("id", OutputSchema.Guid()),
                ("found", OutputSchema.Boolean()),
                ("name", OutputSchema.NullableString()),
                ("versionNumber", OutputSchema.NullableString()),
                ("modifiedOn", NullableDateTime()),
                ("modifiedBy", new JObject { ["type"] = new JArray("object", "null") }))))),
            RunComponentState)
            .WithInspection((binding, arguments) =>
            {
                StateEntries(arguments);
                return CallInspection.None;
            });

        /// <summary>Defines <c>get_required_components</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetRequiredComponentsTool() => ToolDefinition.Paged(
            GetRequiredComponents,
            ToolTier.Read,
            "What the given components require, and which solutions (other than the default solution) already hold each requirement — the dependency check before close-out (W6, X5). The plugin does not judge: a requirement held only by a later transport group, or by no solution, is for the person. One item per dependency. Paged.",
            250,
            InputSchema.ForConnection()
                .ObjectArray(Components, "1–50 component selectors.", ComponentSelector.Schema(), 1, 50),
            "dependencies",
            DependencySchema(),
            OpenRequiredComponents,
            input => input.WithReason(required: false));

        /// <summary>Finds a solution by unique name.</summary>
        /// <param name="service">The service.</param>
        /// <param name="uniqueName">The unique name.</param>
        /// <returns>The solution row; <c>not_found</c> when there is none.</returns>
        internal static Entity FindSolution(IOrganizationService service, string uniqueName)
        {
            var query = new QueryExpression("solution") { ColumnSet = new ColumnSet("uniquename", "ismanaged"), TopCount = 1 };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
            return service.RetrieveMultiple(query).Entities.FirstOrDefault() ?? throw MetadataHelpers.NotFound("solution", uniqueName);
        }

        /// <summary>Maps <c>rootcomponentbehavior</c> to its name; <c>null</c> when the platform stores none.</summary>
        /// <param name="value">The stored value.</param>
        /// <returns>The name, as JSON.</returns>
        internal static JToken RootBehavior(OptionSetValue? value)
        {
            switch (value?.Value)
            {
                case 0:
                    return "includeSubcomponents";
                case 1:
                    return "doNotIncludeSubcomponents";
                case 2:
                    return "includeAsShellOnly";
                default:
                    return JValue.CreateNull();
            }
        }

        /// <summary>Maps a <c>dependency.dependencytype</c> value to its name.</summary>
        /// <param name="value">The stored value.</param>
        /// <returns>The name, as JSON; <c>null</c> for a value outside the three the spec lists.</returns>
        internal static JToken DependencyType(OptionSetValue? value)
        {
            switch (value?.Value)
            {
                case 1:
                    return "solutionInternal";
                case 2:
                    return "published";
                case 4:
                    return "unpublished";
                default:
                    return JValue.CreateNull();
            }
        }

        private static JObject RowBackedComponentSchema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "integer", ["enum"] = new JArray(ComponentNames.RowBacked.Keys.OrderBy(k => k)) },
                ["id"] = new JObject { ["type"] = "string", ["format"] = "uuid" },
            },
            ["required"] = new JArray("type", "id"),
            ["additionalProperties"] = false,
        };

        private static JObject DependencySchema()
        {
            var required = OutputSchema.Object(
                ("componentType", ComponentTypeSchema()),
                ("objectId", OutputSchema.Guid()),
                ("name", OutputSchema.NullableString()),
                ("isManaged", new JObject { ["type"] = new JArray("boolean", "null") }),
                ("solutions", OutputSchema.Array(OutputSchema.Object(("uniqueName", OutputSchema.String()), ("isManaged", OutputSchema.Boolean())))));
            return OutputSchema.Object(
                ("dependent", OutputSchema.Object(("type", OutputSchema.Integer()), ("id", OutputSchema.Guid()), ("name", OutputSchema.NullableString()))),
                ("required", required),
                ("dependencyType", OutputSchema.NullableEnum("solutionInternal", "published", "unpublished")));
        }

        private static JObject ComponentTypeSchema() => OutputSchema.Object(("code", OutputSchema.Integer()), ("name", OutputSchema.String()));

        private static JObject NullableDateTime() => new JObject { ["type"] = new JArray("string", "null"), ["format"] = "date-time" };

        private static JToken Date(Entity row, string column) =>
            row.GetAttributeValue<DateTime?>(column) is DateTime value ? new JValue(ValueFormats.FormatDateTime(value)) : JValue.CreateNull();

        private static T? Aliased<T>(Entity row, string column)
            where T : class =>
            row.GetAttributeValue<AliasedValue>(column)?.Value as T;

        private static IPageSource OpenSolutions(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename", "friendlyname", "version", "ismanaged", "isvisible", "installedon", "modifiedon", "description", "publisherid"),
            };
            query.AddOrder("friendlyname", OrderType.Ascending);

            var managed = MetadataHelpers.OptionalString(arguments, Managed) ?? "all";
            if (managed != "all")
            {
                query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, managed == "managed");
            }

            if (!MetadataHelpers.OptionalBool(arguments, IncludeHidden, false))
            {
                query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
            }

            if (MetadataHelpers.OptionalString(arguments, NameContains) is string search)
            {
                var pattern = $"%{PublisherTools.EscapeLike(search)}%";
                var names = new FilterExpression(LogicalOperator.Or);
                names.AddCondition("uniquename", ConditionOperator.Like, pattern);
                names.AddCondition("friendlyname", ConditionOperator.Like, pattern);
                query.Criteria.AddFilter(names);
            }

            var publisher = query.AddLink("publisher", "publisherid", "publisherid");
            publisher.EntityAlias = PublisherAlias;
            publisher.Columns = new ColumnSet("uniquename", "customizationprefix");
            if (MetadataHelpers.OptionalString(arguments, PublisherUniqueName) is string publisherName)
            {
                publisher.LinkCriteria.AddCondition("uniquename", ConditionOperator.Equal, publisherName);
            }

            return new RecordPageSource(execution.Service, query, SolutionItem);
        }

        private static JObject SolutionItem(Entity solution) => new JObject
        {
            ["solutionId"] = ValueFormats.FormatGuid(solution.Id),
            ["uniqueName"] = solution.GetAttributeValue<string>("uniquename"),
            ["friendlyName"] = solution.GetAttributeValue<string>("friendlyname"),
            ["version"] = solution.GetAttributeValue<string>("version"),
            ["isManaged"] = solution.GetAttributeValue<bool>("ismanaged"),
            ["isVisible"] = solution.GetAttributeValue<bool>("isvisible"),
            ["publisher"] = new JObject
            {
                ["publisherId"] = ValueFormats.FormatGuid(solution.GetAttributeValue<EntityReference>("publisherid")?.Id ?? Guid.Empty),
                ["uniqueName"] = Aliased<string>(solution, $"{PublisherAlias}.uniquename"),
                ["customizationPrefix"] = Aliased<string>(solution, $"{PublisherAlias}.customizationprefix"),
            },
            ["installedOn"] = Date(solution, "installedon"),
            ["modifiedOn"] = Date(solution, "modifiedon"),
            ["description"] = MetadataHelpers.Text(solution.GetAttributeValue<string>("description")),
        };

        private static IPageSource OpenSolutionComponents(ToolExecution execution)
        {
            var solution = FindSolution(execution.Service, (string)execution.Arguments[SolutionUniqueName]!);
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("componenttype", "objectid", "rootcomponentbehavior", "rootsolutioncomponentid", "ismetadata"),
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solution.Id);
            if (execution.Arguments[ComponentTypesArgument] is JArray types && types.Count > 0)
            {
                query.Criteria.AddCondition("componenttype", ConditionOperator.In, types.Values<int>().Cast<object>().ToArray());
            }

            query.AddOrder("componenttype", OrderType.Ascending);
            query.AddOrder("solutioncomponentid", OrderType.Ascending);

            var names = new ComponentNames(execution.Service);
            var header = new JObject
            {
                ["solution"] = new JObject
                {
                    ["solutionId"] = ValueFormats.FormatGuid(solution.Id),
                    ["uniqueName"] = solution.GetAttributeValue<string>("uniquename"),
                    ["isManaged"] = solution.GetAttributeValue<bool>("ismanaged"),
                },
            };

            return new RecordPageSource(
                execution.Service,
                query,
                rows =>
                {
                    var resolved = names.Resolve(rows.Select(Key));
                    return rows.Select(row => ComponentItem(row, resolved));
                },
                header);
        }

        private static (int Type, Guid Id) Key(Entity row) =>
            (row.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0, row.GetAttributeValue<Guid>("objectid"));

        private static JObject ComponentItem(Entity row, Dictionary<(int Type, Guid Id), string> names)
        {
            var key = Key(row);
            var root = row.GetAttributeValue<Guid?>("rootsolutioncomponentid");
            return new JObject
            {
                ["solutionComponentId"] = ValueFormats.FormatGuid(row.Id),
                ["componentType"] = ComponentTypes.ToJson(key.Type),
                ["objectId"] = ValueFormats.FormatGuid(key.Id),
                ["name"] = ValueFormats.NullableString(names.TryGetValue(key, out var name) ? name : null),
                ["rootComponentBehavior"] = RootBehavior(row.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")),
                ["rootSolutionComponentId"] = MetadataHelpers.Id(root == Guid.Empty ? null : root),
                ["isMetadata"] = row.GetAttributeValue<bool>("ismetadata"),
            };
        }

        private static JObject RunComponentState(ToolExecution execution)
        {
            var requested = StateEntries(execution.Arguments);
            var rows = new Dictionary<(int, Guid), Entity>();
            foreach (var group in requested.GroupBy(r => r.Type))
            {
                var table = ComponentNames.RowBacked[group.Key];
                foreach (var batch in ComponentNames.Batches(group.Select(r => r.Id).Distinct().ToList()))
                {
                    var query = new QueryExpression(table.Table) { ColumnSet = new ColumnSet(table.NameColumn, "versionnumber", "modifiedon", "modifiedby") };
                    query.Criteria.AddCondition(table.IdColumn, ConditionOperator.In, batch.Cast<object>().ToArray());
                    foreach (var row in execution.Service.RetrieveMultiple(query).Entities)
                    {
                        rows[(group.Key, row.Id)] = row;
                    }
                }
            }

            return new JObject { ["components"] = new JArray(requested.Select(r => StateItem(r.Type, r.Id, rows))) };
        }

        /// <summary>Parses and checks the entries of <c>get_component_state</c>; runs before the dialog and again when the call runs.</summary>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The entries, in request order.</returns>
        private static List<(int Index, int Type, Guid Id)> StateEntries(JObject arguments)
        {
            var requested = ((JArray)arguments[Components]!)
                .Select((entry, index) =>
                {
                    var at = $"{Components}[{index}]";
                    if (!(entry is JObject component) || component["type"]?.Type != JTokenType.Integer || component["id"] == null)
                    {
                        throw ToolException.InvalidArgument(at, $"{at} must be {{ type, id }}.");
                    }

                    return (Index: index, Type: (int)component["type"]!, Id: ComponentSelector.ParseGuid(at, component["id"]!));
                })
                .ToList();

            var unsupported = requested.Where(r => !ComponentNames.RowBacked.ContainsKey(r.Type)).ToList();
            if (unsupported.Count > 0)
            {
                throw new ToolException(
                    ToolErrorCodes.InvalidArgument,
                    $"get_component_state accepts views (26), forms (60), web resources (61), sitemaps (62) and app modules (80) only. Not accepted: entries {string.Join(", ", unsupported.Select(u => u.Index.ToString(CultureInfo.InvariantCulture)))}. For tables and columns, use a PAC re-export and diff.",
                    new JObject { ["argument"] = Components, ["entries"] = new JArray(unsupported.Select(u => u.Index)) });
            }

            return requested;
        }

        private static JObject StateItem(int type, Guid id, Dictionary<(int, Guid), Entity> rows)
        {
            var item = new JObject { ["type"] = type, ["id"] = ValueFormats.FormatGuid(id) };
            if (!rows.TryGetValue((type, id), out var row))
            {
                item["found"] = false;
                item["name"] = JValue.CreateNull();
                item["versionNumber"] = JValue.CreateNull();
                item["modifiedOn"] = JValue.CreateNull();
                item["modifiedBy"] = JValue.CreateNull();
                return item;
            }

            var modifiedBy = row.GetAttributeValue<EntityReference>("modifiedby");
            var version = row.Contains("versionnumber") ? Convert.ToInt64(row["versionnumber"], CultureInfo.InvariantCulture) : (long?)null;
            item["found"] = true;
            item["name"] = MetadataHelpers.Text(row.GetAttributeValue<string>(ComponentNames.RowBacked[type].NameColumn));
            item["versionNumber"] = ValueFormats.NullableString(version?.ToString(CultureInfo.InvariantCulture));
            item["modifiedOn"] = Date(row, "modifiedon");
            item["modifiedBy"] = modifiedBy == null
                ? JValue.CreateNull()
                : new JObject
                {
                    ["id"] = ValueFormats.FormatGuid(modifiedBy.Id),
                    ["name"] = MetadataHelpers.Text(row.FormattedValues.Contains("modifiedby") ? row.FormattedValues["modifiedby"] : modifiedBy.Name),
                };
            return item;
        }

        private static IPageSource OpenRequiredComponents(ToolExecution execution)
        {
            var service = execution.Service;
            var selectors = ((JArray)execution.Arguments[Components]!).Select((entry, index) => ComponentSelector.Parse(Components, index, entry)).ToList();

            // Resolve every selector first, so a wrong name fails the whole call before any dependency is read.
            var dependents = selectors.Select(s => s.Resolve(service)).ToList();
            var names = new ComponentNames(service);
            var dependentNames = names.Resolve(dependents.Where(d => d.Name == null).Select(d => (d.Type, d.Id)));

            var dependencies = new List<(int DependentType, Guid DependentId, string? DependentName, Entity Dependency)>();
            foreach (var dependent in dependents)
            {
                var response = (RetrieveRequiredComponentsResponse)service.Execute(new RetrieveRequiredComponentsRequest { ObjectId = dependent.Id, ComponentType = dependent.Type });
                var name = dependent.Name ?? (dependentNames.TryGetValue((dependent.Type, dependent.Id), out var found) ? found : null);
                dependencies.AddRange(response.EntityCollection.Entities.Select(d => (dependent.Type, dependent.Id, name, d)));
            }

            var required = dependencies.Select(d => RequiredKey(d.Dependency)).Distinct().ToList();
            var requiredNames = names.Resolve(required);
            var holders = Holders(service, required);

            return SnapshotPageSource.Of(dependencies.Select(d =>
            {
                var key = RequiredKey(d.Dependency);
                return new JObject
                {
                    ["dependent"] = new JObject
                    {
                        ["type"] = d.DependentType,
                        ["id"] = ValueFormats.FormatGuid(d.DependentId),
                        ["name"] = ValueFormats.NullableString(d.DependentName),
                    },
                    ["required"] = new JObject
                    {
                        ["componentType"] = ComponentTypes.ToJson(key.Type),
                        ["objectId"] = ValueFormats.FormatGuid(key.Id),
                        ["name"] = ValueFormats.NullableString(requiredNames.TryGetValue(key, out var name) ? name : null),
                        ["isManaged"] = JValue.CreateNull(),
                        ["solutions"] = new JArray(holders.TryGetValue(key, out var solutions) ? solutions : Enumerable.Empty<JObject>()),
                    },
                    ["dependencyType"] = DependencyType(d.Dependency.GetAttributeValue<OptionSetValue>("dependencytype")),
                };
            }));
        }

        private static (int Type, Guid Id) RequiredKey(Entity dependency) =>
            (dependency.GetAttributeValue<OptionSetValue>("requiredcomponenttype")?.Value ?? 0, dependency.GetAttributeValue<Guid>("requiredcomponentobjectid"));

        private static Dictionary<(int, Guid), List<JObject>> Holders(IOrganizationService service, IReadOnlyList<(int Type, Guid Id)> components)
        {
            var holders = new Dictionary<(int, Guid), List<JObject>>();
            var wanted = new HashSet<(int, Guid)>(components);
            foreach (var batch in ComponentNames.Batches(components.Select(c => c.Id).Distinct().ToList()))
            {
                var query = new QueryExpression("solutioncomponent") { ColumnSet = new ColumnSet("componenttype", "objectid") };
                query.Criteria.AddCondition("objectid", ConditionOperator.In, batch.Cast<object>().ToArray());
                var solution = query.AddLink("solution", "solutionid", "solutionid");
                solution.EntityAlias = SolutionAlias;
                solution.Columns = new ColumnSet("uniquename", "ismanaged");
                solution.LinkCriteria.AddCondition("uniquename", ConditionOperator.NotIn, SystemSolutions.Cast<object>().ToArray());

                foreach (var row in service.RetrieveMultiple(query).Entities)
                {
                    var key = Key(row);
                    if (!wanted.Contains(key))
                    {
                        continue;
                    }

                    if (!holders.TryGetValue(key, out var list))
                    {
                        holders[key] = list = new List<JObject>();
                    }

                    var uniqueName = Aliased<string>(row, $"{SolutionAlias}.uniquename");
                    if (uniqueName != null && list.All(s => (string?)s["uniqueName"] != uniqueName))
                    {
                        list.Add(new JObject
                        {
                            ["uniqueName"] = uniqueName,
                            ["isManaged"] = row.GetAttributeValue<AliasedValue>($"{SolutionAlias}.ismanaged")?.Value as bool? ?? false,
                        });
                    }
                }
            }

            return holders;
        }
    }
}
