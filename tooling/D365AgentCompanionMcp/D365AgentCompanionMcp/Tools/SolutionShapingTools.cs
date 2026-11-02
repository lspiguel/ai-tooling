// <copyright file="SolutionShapingTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Logging;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The Write tools — <c>create_solution</c>, <c>add_solution_components</c> and <c>remove_solution_component</c>
    /// (SPEC §6.6). Each resolves what it will touch before its dialog, needs a reason (R12) and a connection confirmed
    /// as development, and is approved once per call. Adding never includes required components or subcomponents (R1).
    /// </summary>
    internal static class SolutionShapingTools
    {
        /// <summary>The <c>create_solution</c> tool name.</summary>
        public const string CreateSolution = "create_solution";

        /// <summary>The <c>add_solution_components</c> tool name.</summary>
        public const string AddSolutionComponents = "add_solution_components";

        /// <summary>The <c>remove_solution_component</c> tool name.</summary>
        public const string RemoveSolutionComponent = "remove_solution_component";

        /// <summary>The component types <c>add_solution_components</c> and <c>remove_solution_component</c> accept in v1 (SPEC §6.6).</summary>
        public static readonly IReadOnlyCollection<int> AcceptedTypes = new HashSet<int>
        {
            ComponentTypes.Entity,
            ComponentTypes.Attribute,
            ComponentTypes.OptionSet,
            ComponentTypes.EntityRelationship,
            ComponentTypes.SavedQuery,
            ComponentTypes.SystemForm,
            ComponentTypes.WebResource,
            ComponentTypes.SiteMap,
            ComponentTypes.AppModule,
        };

        private const string UniqueName = "uniqueName";
        private const string FriendlyName = "friendlyName";
        private const string PublisherUniqueName = "publisherUniqueName";
        private const string Version = "version";
        private const string Description = "description";
        private const string SolutionUniqueName = "solutionUniqueName";
        private const string Components = "components";
        private const string Component = "component";
        private const string TableBehavior = "tableBehavior";
        private const string FixedLine = "Fixed       : required components OFF · subcomponents OFF";

        /// <summary>Defines <c>create_solution</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition CreateSolutionTool() => ToolDefinition.Simple(
            CreateSolution,
            ToolTier.Write,
            "Creates an empty unmanaged solution under an existing publisher. Check list_solutions first that the name is free. Requires reason and a connection confirmed as development; approved once.",
            InputSchema.ForConnection()
                .Pattern(UniqueName, "Letters, digits and underscores, starting with a letter; at most 65 characters.", "^[A-Za-z][A-Za-z0-9_]{0,64}$", required: true)
                .String(FriendlyName, "The display name.", required: true, minLength: 1)
                .String(PublisherUniqueName, "An existing publisher; the dialog shows its prefix.", required: true, minLength: 1)
                .Pattern(Version, "Four numeric parts; default 1.0.0.0.", "^[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+$", required: false)
                .String(Description, "Optional description.")
                .WithReason(required: true),
            OutputSchema.Object(
                ("solutionId", OutputSchema.Guid()),
                ("uniqueName", OutputSchema.String()),
                ("friendlyName", OutputSchema.String()),
                ("version", OutputSchema.String()),
                ("publisher", PublisherSchema())),
            RunCreateSolution)
            .WithPreparation(PrepareCreateSolution);

        /// <summary>Defines <c>add_solution_components</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition AddSolutionComponentsTool() => ToolDefinition.Simple(
            AddSolutionComponents,
            ToolTier.Write,
            "Adds existing components to an unmanaged solution, in order, with required components and subcomponents always off (R1). Tables are added metadata-only (default) or as a shell; any table in a solution exports its whole unmanaged ribbon diff. A column, form or view added alone brings its table as a parent shell, reported in results, not as unexpected. Reads the solution back: any entry in unexpected stops the workflow until a person reviews it. The first failure stops the run; later entries are notAttempted. Accepted types: table 1, column 2, global option set 9, relationship 10, view 26, form 60, web resource 61, sitemap 62, app module 80. Requires reason; approved once.",
            InputSchema.ForConnection()
                .String(SolutionUniqueName, "An unmanaged solution.", required: true, minLength: 1)
                .ObjectArray(Components, "1–50 component selectors, added in this order.", ComponentSelector.Schema(), 1, 50)
                .Enum(TableBehavior, "Tables only: metadata (the table's own properties) or shell (an empty table). Default metadata.", new[] { "metadata", "shell" }, "metadata")
                .WithReason(required: true),
            AddOutputSchema(),
            RunAddComponents)
            .WithInspection((binding, arguments) =>
            {
                ParseSelectors(arguments, Components);
                return CallInspection.None;
            })
            .WithPreparation(PrepareAddComponents)
            .WithWarnings(UnexpectedWarnings);

        /// <summary>Defines <c>remove_solution_component</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition RemoveSolutionComponentTool() => ToolDefinition.Simple(
            RemoveSolutionComponent,
            ToolTier.Write,
            "Removes one component from an unmanaged solution; the component itself stays in the environment. Fails with not_found when the component is not in the solution. Requires reason; approved once.",
            InputSchema.ForConnection()
                .String(SolutionUniqueName, "An unmanaged solution.", required: true, minLength: 1)
                .Object(Component, "One component selector.", ComponentSelector.Schema())
                .WithReason(required: true),
            OutputSchema.Object(
                ("solution", SolutionRefSchema()),
                ("component", OutputSchema.Object(("componentType", ComponentTypeSchema()), ("objectId", OutputSchema.Guid()), ("name", OutputSchema.NullableString())))),
            RunRemoveComponent)
            .WithInspection((binding, arguments) =>
            {
                ParseSelector(arguments[Component]!, Component, 0);
                return CallInspection.None;
            })
            .WithPreparation(PrepareRemoveComponent);

        /// <summary>Reads every component of a solution, every page.</summary>
        /// <param name="service">The service.</param>
        /// <param name="solutionId">The solution.</param>
        /// <returns>The components.</returns>
        internal static List<(int Type, Guid Id, int? Behavior)> ReadComponents(IOrganizationService service, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent") { ColumnSet = new ColumnSet("componenttype", "objectid", "rootcomponentbehavior") };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.AddOrder("solutioncomponentid", OrderType.Ascending);
            query.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };

            var components = new List<(int, Guid, int?)>();
            while (true)
            {
                var page = service.RetrieveMultiple(query);
                components.AddRange(page.Entities.Select(row => (
                    row.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0,
                    row.GetAttributeValue<Guid>("objectid"),
                    row.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value)));
                if (!page.MoreRecords)
                {
                    return components;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        /// <summary>Finds an unmanaged solution for a Write tool.</summary>
        /// <param name="service">The service.</param>
        /// <param name="uniqueName">The unique name.</param>
        /// <returns>The solution; <c>not_found</c> or <c>managed_solution</c> otherwise.</returns>
        internal static Entity UnmanagedSolution(IOrganizationService service, string uniqueName)
        {
            var solution = SolutionTools.FindSolution(service, uniqueName);
            if (solution.GetAttributeValue<bool>("ismanaged"))
            {
                throw new ToolException(ToolErrorCodes.ManagedSolution, $"'{uniqueName}' is a managed solution. Add and remove components only in unmanaged solutions.", new JObject { ["solution"] = uniqueName });
            }

            return solution;
        }

        private static JObject PublisherSchema() => OutputSchema.Object(("publisherId", OutputSchema.Guid()), ("uniqueName", OutputSchema.String()), ("customizationPrefix", OutputSchema.String()));

        private static JObject SolutionRefSchema() => OutputSchema.Object(("solutionId", OutputSchema.Guid()), ("uniqueName", OutputSchema.String()));

        private static JObject ComponentTypeSchema() => OutputSchema.Object(("code", OutputSchema.Integer()), ("name", OutputSchema.String()));

        private static JObject AddOutputSchema()
        {
            var result = OutputSchema.Object(
                ("index", OutputSchema.Integer()),
                ("type", OutputSchema.Integer()),
                ("id", OutputSchema.Guid()),
                ("name", OutputSchema.NullableString()),
                ("status", new JObject { ["type"] = "string", ["enum"] = new JArray("added", "alreadyPresent", "failed", "notAttempted") }));
            ((JObject)result["properties"]!)["error"] = new JObject { ["type"] = "object" };

            var unexpected = OutputSchema.Object(
                ("componentType", ComponentTypeSchema()),
                ("objectId", OutputSchema.Guid()),
                ("name", OutputSchema.NullableString()),
                ("rootComponentBehavior", OutputSchema.NullableEnum("includeSubcomponents", "doNotIncludeSubcomponents", "includeAsShellOnly")));

            return OutputSchema.Object(
                ("solution", SolutionRefSchema()),
                ("results", OutputSchema.Array(result)),
                ("unexpected", OutputSchema.Array(unexpected)),
                ("addedCount", OutputSchema.Integer()),
                ("unexpectedCount", OutputSchema.Integer()));
        }

        private static string Text(JObject arguments, string name) => ((string)arguments[name]!).Trim();

        private static JObject SolutionRef(Entity solution) => new JObject
        {
            ["solutionId"] = ValueFormats.FormatGuid(solution.Id),
            ["uniqueName"] = solution.GetAttributeValue<string>("uniquename"),
        };

        private static PreparedCall PrepareCreateSolution(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var uniqueName = Text(arguments, UniqueName);
            var publisherName = Text(arguments, PublisherUniqueName);

            var publisherQuery = new QueryExpression("publisher") { ColumnSet = new ColumnSet("uniquename", "friendlyname", "customizationprefix", "customizationoptionvalueprefix"), TopCount = 1 };
            publisherQuery.Criteria.AddCondition("uniquename", ConditionOperator.Equal, publisherName);
            var publisher = execution.Service.RetrieveMultiple(publisherQuery).Entities.FirstOrDefault() ?? throw MetadataHelpers.NotFound("publisher", publisherName);

            var existing = new QueryExpression("solution") { ColumnSet = new ColumnSet("uniquename"), TopCount = 1 };
            existing.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
            if (execution.Service.RetrieveMultiple(existing).Entities.Count > 0)
            {
                throw new ToolException(ToolErrorCodes.AlreadyExists, $"A solution named '{uniqueName}' already exists. Choose another name, or reuse that solution.", new JObject { ["kind"] = "solution", ["name"] = uniqueName });
            }

            var version = MetadataHelpers.OptionalString(arguments, Version) ?? "1.0.0.0";
            var summary = new StringBuilder()
                .AppendLine($"Solution    : {uniqueName}  \"{Text(arguments, FriendlyName)}\"  (unmanaged, empty)")
                .AppendLine($"Version     : {version}")
                .AppendLine($"Publisher   : {publisher.GetAttributeValue<string>("uniquename")}  \"{publisher.GetAttributeValue<string>("friendlyname")}\"")
                .Append($"              prefix {publisher.GetAttributeValue<string>("customizationprefix")}   option prefix {publisher.GetAttributeValue<int>("customizationoptionvalueprefix").ToString(CultureInfo.InvariantCulture)}")
                .ToString();
            return new PreparedCall(summary, publisher);
        }

        private static JObject RunCreateSolution(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var publisher = (Entity)execution.Prepared!.State!;
            var version = MetadataHelpers.OptionalString(arguments, Version) ?? "1.0.0.0";
            var solution = new Entity("solution")
            {
                ["uniquename"] = Text(arguments, UniqueName),
                ["friendlyname"] = Text(arguments, FriendlyName),
                ["publisherid"] = publisher.ToEntityReference(),
                ["version"] = version,
            };
            if (MetadataHelpers.OptionalString(arguments, Description) is string description)
            {
                solution["description"] = description;
            }

            var id = execution.Service.Create(solution);
            return new JObject
            {
                ["solutionId"] = ValueFormats.FormatGuid(id),
                ["uniqueName"] = solution.GetAttributeValue<string>("uniquename"),
                ["friendlyName"] = solution.GetAttributeValue<string>("friendlyname"),
                ["version"] = version,
                ["publisher"] = new JObject
                {
                    ["publisherId"] = ValueFormats.FormatGuid(publisher.Id),
                    ["uniqueName"] = publisher.GetAttributeValue<string>("uniquename"),
                    ["customizationPrefix"] = publisher.GetAttributeValue<string>("customizationprefix"),
                },
            };
        }

        private static ComponentSelector ParseSelector(JToken entry, string argument, int index)
        {
            var selector = ComponentSelector.Parse(argument, index, entry);
            if (!AcceptedTypes.Contains(selector.Type))
            {
                var at = argument == Component ? Component : $"{argument}[{index}]";
                throw ToolException.InvalidArgument(at, $"{at}: type {selector.Type} is not accepted. Accepted: table 1, column 2, global option set 9, relationship 10, view 26, form 60, web resource 61, sitemap 62, app module 80.");
            }

            return selector;
        }

        private static List<ComponentSelector> ParseSelectors(JObject arguments, string argument) =>
            ((JArray)arguments[argument]!).Select((entry, index) => ParseSelector(entry, argument, index)).ToList();

        private static PreparedCall PrepareAddComponents(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var solution = UnmanagedSolution(execution.Service, Text(arguments, SolutionUniqueName));
            var shell = MetadataHelpers.OptionalString(arguments, TableBehavior) == "shell";
            var targets = Describe(execution.Service, ParseSelectors(arguments, Components));

            var summary = new StringBuilder()
                .AppendLine($"Solution    : {solution.GetAttributeValue<string>("uniquename")}  (unmanaged)")
                .AppendLine("Components  :");
            foreach (var target in targets)
            {
                summary.AppendLine($"  {target.Index + 1,2}. {Line(target, shell)}");
            }

            summary.Append(FixedLine);
            return new PreparedCall(summary.ToString(), new AddPlan(solution, targets, shell));
        }

        private static string Line(Target target, bool shell)
        {
            switch (target.Type)
            {
                case ComponentTypes.Entity:
                    return $"Table         {target.Name}   ({(shell ? "shell only" : "metadata only")}, no subcomponents)";
                case ComponentTypes.Attribute:
                    return $"Column        {target.Name}";
                case ComponentTypes.SystemForm:
                    return $"Form          {target.Name}   on {target.Table}   {{{ValueFormats.FormatGuid(target.Id)}}}";
                case ComponentTypes.SavedQuery:
                    return $"View          {target.Name}   on {target.Table}   {{{ValueFormats.FormatGuid(target.Id)}}}";
                default:
                    return $"{ComponentTypes.NameOf(target.Type),-13} {target.Name}   {{{ValueFormats.FormatGuid(target.Id)}}}";
            }
        }

        /// <summary>
        /// Resolves every selector to an ID and a name, and forms, views and columns to their table. Any component that
        /// cannot be found fails the whole call with <c>not_found</c>; nothing is added (SPEC §6.6 step 1).
        /// </summary>
        private static List<Target> Describe(IOrganizationService service, IReadOnlyList<ComponentSelector> selectors)
        {
            var targets = selectors.Select(s =>
            {
                var resolved = s.Resolve(service);
                return new Target(s.Index, resolved.Type, resolved.Id, resolved.Name, s.Type == ComponentTypes.Attribute ? s.Table : null);
            }).ToList();

            // Forms and views carry their table; read both in one query per type.
            foreach (var (type, table, id, name, tableColumn) in new[]
            {
                (ComponentTypes.SystemForm, "systemform", "formid", "name", "objecttypecode"),
                (ComponentTypes.SavedQuery, "savedquery", "savedqueryid", "name", "returnedtypecode"),
            })
            {
                var ofType = targets.Where(t => t.Type == type).ToList();
                foreach (var batch in ComponentNames.Batches(ofType.Select(t => t.Id).Distinct().ToList()))
                {
                    var query = new QueryExpression(table) { ColumnSet = new ColumnSet(name, tableColumn) };
                    query.Criteria.AddCondition(id, ConditionOperator.In, batch.Cast<object>().ToArray());
                    foreach (var row in service.RetrieveMultiple(query).Entities)
                    {
                        foreach (var target in ofType.Where(t => t.Id == row.Id))
                        {
                            target.Name = row.GetAttributeValue<string>(name);
                            target.Table = row.GetAttributeValue<string>(tableColumn);
                        }
                    }
                }
            }

            foreach (var target in targets.Where(t => t.Type == ComponentTypes.EntityRelationship && t.Name == null))
            {
                try
                {
                    target.Name = ((RetrieveRelationshipResponse)service.Execute(new RetrieveRelationshipRequest { MetadataId = target.Id })).RelationshipMetadata.SchemaName;
                }
                catch (Exception ex) when (MetadataHelpers.IsNotFound(ex))
                {
                    // Reported as not found below.
                }
            }

            var unnamed = targets.Where(t => t.Name == null && t.Type != ComponentTypes.SystemForm && t.Type != ComponentTypes.SavedQuery && t.Type != ComponentTypes.EntityRelationship).ToList();
            var names = new ComponentNames(service).Resolve(unnamed.Select(t => (t.Type, t.Id)));
            foreach (var target in unnamed)
            {
                target.Name = names.TryGetValue((target.Type, target.Id), out var found) ? found : null;
            }

            foreach (var target in targets.Where(t => t.Type == ComponentTypes.Attribute && t.Table == null && t.Name != null))
            {
                target.Table = target.Name!.Split('.')[0];
            }

            var missing = targets.FirstOrDefault(t => t.Name == null);
            if (missing != null)
            {
                throw new ToolException(
                    ToolErrorCodes.NotFound,
                    $"{Components}[{missing.Index}]: no {ComponentTypes.NameOf(missing.Type)} with id {ValueFormats.FormatGuid(missing.Id)} exists. Nothing was added.",
                    new JObject { ["kind"] = ComponentTypes.NameOf(missing.Type), ["name"] = ValueFormats.FormatGuid(missing.Id), ["index"] = missing.Index });
            }

            return targets;
        }

        private static JObject RunAddComponents(ToolExecution execution)
        {
            var plan = (AddPlan)execution.Prepared!.State!;
            var service = execution.Service;
            var solutionName = plan.Solution.GetAttributeValue<string>("uniquename");

            // Snapshot, add in order with the fixed flags, read back (SPEC §6.6 steps 3–5).
            var before = ReadComponents(service, plan.Solution.Id);
            var present = new HashSet<(int, Guid)>(before.Select(c => (c.Type, c.Id)));
            var results = new JArray();
            var added = new HashSet<int>();
            var stopped = false;
            foreach (var target in plan.Targets)
            {
                var item = new JObject
                {
                    ["index"] = target.Index,
                    ["type"] = target.Type,
                    ["id"] = ValueFormats.FormatGuid(target.Id),
                    ["name"] = ValueFormats.NullableString(target.Name),
                };
                results.Add(item);
                if (stopped)
                {
                    item["status"] = "notAttempted";
                    continue;
                }

                if (present.Contains((target.Type, target.Id)))
                {
                    // Not added again: a second add could change how the platform holds a component already there.
                    item["status"] = "alreadyPresent";
                    continue;
                }

                try
                {
                    service.Execute(AddRequest(solutionName, target, plan.Shell));
                    item["status"] = "added";
                    added.Add(target.Index);
                    present.Add((target.Type, target.Id));
                }
                catch (Exception ex)
                {
                    item["status"] = "failed";
                    item["error"] = ToolException.FromDataverse(ex).ToErrorObject();
                    stopped = true;
                }
            }

            var requested = new HashSet<(int, Guid)>(plan.Targets.Select(t => (t.Type, t.Id)));
            var known = new HashSet<(int, Guid)>(before.Select(c => (c.Type, c.Id)));
            var arrived = ReadComponents(service, plan.Solution.Id).Where(c => !known.Contains((c.Type, c.Id)) && !requested.Contains((c.Type, c.Id))).ToList();
            var names = new ComponentNames(service).Resolve(arrived.Select(c => (c.Type, c.Id)));

            // A table the platform added as a shell to hold an added column, form or view is expected (spike S2).
            var children = plan.Targets
                .Where(t => (t.Type == ComponentTypes.Attribute || t.Type == ComponentTypes.SystemForm || t.Type == ComponentTypes.SavedQuery) && t.Table != null)
                .Where(t => added.Contains(t.Index))
                .GroupBy(t => t.Table!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.OrdinalIgnoreCase);

            var unexpected = new JArray();
            foreach (var component in arrived)
            {
                var name = names.TryGetValue((component.Type, component.Id), out var found) ? found : null;
                if (component.Type == ComponentTypes.Entity && component.Behavior == 2 && name != null && children.TryGetValue(name, out var child))
                {
                    results.Add(new JObject
                    {
                        ["index"] = child,
                        ["type"] = component.Type,
                        ["id"] = ValueFormats.FormatGuid(component.Id),
                        ["name"] = $"{name} (parent shell of entry {child}, added by the platform)",
                        ["status"] = "added",
                    });
                    continue;
                }

                unexpected.Add(new JObject
                {
                    ["componentType"] = ComponentTypes.ToJson(component.Type),
                    ["objectId"] = ValueFormats.FormatGuid(component.Id),
                    ["name"] = ValueFormats.NullableString(name),
                    ["rootComponentBehavior"] = SolutionTools.RootBehavior(component.Behavior.HasValue ? new OptionSetValue(component.Behavior.Value) : null),
                });
            }

            return new JObject
            {
                ["solution"] = SolutionRef(plan.Solution),
                ["results"] = results,
                ["unexpected"] = unexpected,
                ["addedCount"] = added.Count,
                ["unexpectedCount"] = unexpected.Count,
            };
        }

        /// <summary>
        /// The add request with the fixed flags of SPEC §6.6, which the caller cannot change (R1): required components
        /// off; tables without subcomponents, as metadata (settings unset → behaviour 1) or shell (settings empty → 2).
        /// </summary>
        /// <param name="solutionUniqueName">The solution.</param>
        /// <param name="target">The component.</param>
        /// <param name="shell">Whether tables are added as shells.</param>
        /// <returns>The request.</returns>
        private static AddSolutionComponentRequest AddRequest(string solutionUniqueName, Target target, bool shell)
        {
            var table = target.Type == ComponentTypes.Entity;
            return new AddSolutionComponentRequest
            {
                SolutionUniqueName = solutionUniqueName,
                ComponentType = target.Type,
                ComponentId = target.Id,
                AddRequiredComponents = false,
                DoNotIncludeSubcomponents = table,
                IncludedComponentSettingsValues = table && shell ? Array.Empty<string>() : null,
            };
        }

        private static IReadOnlyList<string> UnexpectedWarnings(JObject result) =>
            ((JArray?)result["unexpected"] ?? new JArray())
                .Select(u => CallLogFormatter.Unexpected((string)u["componentType"]!["name"]!, (string?)u["name"] ?? (string)u["objectId"]!))
                .ToList();

        private static PreparedCall PrepareRemoveComponent(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var solution = UnmanagedSolution(execution.Service, Text(arguments, SolutionUniqueName));
            var target = Describe(execution.Service, new[] { ParseSelector(arguments[Component]!, Component, 0) }).Single();

            var query = new QueryExpression("solutioncomponent") { ColumnSet = new ColumnSet("solutioncomponentid"), TopCount = 1 };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solution.Id);
            query.Criteria.AddCondition("objectid", ConditionOperator.Equal, target.Id);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, target.Type);
            if (execution.Service.RetrieveMultiple(query).Entities.Count == 0)
            {
                throw new ToolException(
                    ToolErrorCodes.NotFound,
                    $"{ComponentTypes.NameOf(target.Type)} {target.Name} is not in solution '{solution.GetAttributeValue<string>("uniquename")}'.",
                    new JObject { ["kind"] = "solution component", ["name"] = target.Name });
            }

            var summary = new StringBuilder()
                .AppendLine($"Solution    : {solution.GetAttributeValue<string>("uniquename")}  (unmanaged)")
                .AppendLine($"Remove      : {Line(target, shell: false)}")
                .Append("The component stays in the environment; only its membership of this solution ends.")
                .ToString();
            return new PreparedCall(summary, new AddPlan(solution, new List<Target> { target }, shell: false));
        }

        private static JObject RunRemoveComponent(ToolExecution execution)
        {
            var plan = (AddPlan)execution.Prepared!.State!;
            var target = plan.Targets.Single();
            execution.Service.Execute(new RemoveSolutionComponentRequest
            {
                SolutionUniqueName = plan.Solution.GetAttributeValue<string>("uniquename"),
                ComponentType = target.Type,
                ComponentId = target.Id,
            });

            return new JObject
            {
                ["solution"] = SolutionRef(plan.Solution),
                ["component"] = new JObject
                {
                    ["componentType"] = ComponentTypes.ToJson(target.Type),
                    ["objectId"] = ValueFormats.FormatGuid(target.Id),
                    ["name"] = ValueFormats.NullableString(target.Name),
                },
            };
        }

        /// <summary>One component a Write call touches, resolved.</summary>
        private sealed class Target
        {
            public Target(int index, int type, Guid id, string? name, string? table)
            {
                this.Index = index;
                this.Type = type;
                this.Id = id;
                this.Name = name;
                this.Table = table;
            }

            public int Index { get; }

            public int Type { get; }

            public Guid Id { get; }

            public string? Name { get; set; }

            /// <summary>Gets or sets the table of a column, form or view — the parent the platform may add as a shell.</summary>
            public string? Table { get; set; }
        }

        /// <summary>What a Write call resolved before its dialog.</summary>
        private sealed class AddPlan
        {
            public AddPlan(Entity solution, List<Target> targets, bool shell)
            {
                this.Solution = solution;
                this.Targets = targets;
                this.Shell = shell;
            }

            public Entity Solution { get; }

            public List<Target> Targets { get; }

            public bool Shell { get; }
        }
    }
}
