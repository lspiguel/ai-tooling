// <copyright file="WebResourceTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Files;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The direct-update tool <c>update_web_resources</c> (SPEC §6.8): create or update web resources from local files
    /// in a WIP, with the overwrite check inside the tool and a targeted publish. Files are passed by path, resolved
    /// against the File root, and the dialog shows each file's size and SHA-256 so the person approves the bytes that
    /// will be written.
    /// </summary>
    internal static class WebResourceTools
    {
        /// <summary>The <c>update_web_resources</c> tool name.</summary>
        public const string UpdateWebResources = "update_web_resources";

        private const string Items = "items";
        private const string SolutionUniqueName = "solutionUniqueName";
        private const string Publish = "publish";
        private const string WebResource = "webresource";

        /// <summary>The web resource type of each file extension (SPEC §6.8, step 1).</summary>
        private static readonly IReadOnlyDictionary<string, int> TypeByExtension = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [".htm"] = 1,
            [".html"] = 1,
            [".css"] = 2,
            [".js"] = 3,
            [".xml"] = 4,
            [".png"] = 5,
            [".jpg"] = 6,
            [".jpeg"] = 6,
            [".gif"] = 7,
            [".xsl"] = 9,
            [".xslt"] = 9,
            [".ico"] = 10,
            [".svg"] = 11,
            [".resx"] = 12,
        };

        /// <summary>Defines <c>update_web_resources</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition UpdateWebResourcesTool() => ToolDefinition.Simple(
            UpdateWebResources,
            ToolTier.Write,
            "Creates or updates web resources in development from local files, then publishes just those. Files are passed by path, never by content: a path relative to the File root (get_connection_info.settings.fileRoot) or an absolute path inside it. An existing resource must already be in the named unmanaged WIP and needs expectedVersion — the versionNumber from get_component_state — which the tool re-checks just before writing; a new resource is created in the WIP, under the WIP publisher's prefix. A resource whose content already matches the environment's unpublished content is reported unchanged and not written. The first failure stops the run; later entries are notAttempted. versionAfter is read after the publish and is the baseline for the next call. Registering a script on a form or a command stays an import. Requires reason and a connection confirmed as development; approved once or under a Write grant.",
            InputSchema.ForConnection()
                .String(SolutionUniqueName, "The WIP: an unmanaged solution.", required: true, minLength: 1)
                .ObjectArray(Items, "1–50 files.", ItemSchema(), 1, 50)
                .Boolean(Publish, "Publish the resources written, in one targeted request. Default true.", true)
                .WithReason(required: true),
            OutputSchema(),
            RunUpdate)
            .WithInspection((binding, arguments) =>
            {
                ParseItems(arguments);
                return CallInspection.None;
            })
            .WithPreparation(PrepareUpdate)
            .WithWarnings(SolutionShapingTools.UnexpectedWarnings);

        private static JObject ItemSchema() => new JObject
        {
            ["type"] = "object",
            ["description"] = "{path, name, displayName?, expectedVersion?}: path relative to the File root or absolute inside it; name the web resource name, such as \"[prefix]_/scripts/account.form.js\"; displayName for a new resource (default the file name); expectedVersion the versionNumber from get_component_state, required for an existing resource.",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string" },
                ["name"] = new JObject { ["type"] = "string" },
                ["displayName"] = new JObject { ["type"] = "string" },
                ["expectedVersion"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("path", "name"),
            ["additionalProperties"] = false,
        };

        private static JObject OutputSchema()
        {
            var result = Tools.OutputSchema.Object(
                ("index", Tools.OutputSchema.Integer()),
                ("name", Tools.OutputSchema.String()),
                ("id", Tools.OutputSchema.NullableString()),
                ("status", new JObject { ["type"] = "string", ["enum"] = new JArray("created", "updated", "unchanged", "failed", "notAttempted") }),
                ("versionBefore", Tools.OutputSchema.NullableString()),
                ("versionAfter", Tools.OutputSchema.NullableString()),
                ("sha256", Tools.OutputSchema.String()));
            ((JObject)result["properties"]!)["error"] = new JObject { ["type"] = "object" };

            var unexpected = Tools.OutputSchema.Object(
                ("componentType", Tools.OutputSchema.Object(("code", Tools.OutputSchema.Integer()), ("name", Tools.OutputSchema.String()))),
                ("objectId", Tools.OutputSchema.Guid()),
                ("name", Tools.OutputSchema.NullableString()),
                ("rootComponentBehavior", Tools.OutputSchema.NullableEnum("includeSubcomponents", "doNotIncludeSubcomponents", "includeAsShellOnly")));

            return Tools.OutputSchema.Object(
                ("solution", Tools.OutputSchema.Object(("solutionId", Tools.OutputSchema.Guid()), ("uniqueName", Tools.OutputSchema.String()))),
                ("results", Tools.OutputSchema.Array(result)),
                ("published", Tools.OutputSchema.Boolean()),
                ("publishError", Tools.OutputSchema.NullableString()),
                ("unexpected", Tools.OutputSchema.Array(unexpected)),
                ("unexpectedCount", Tools.OutputSchema.Integer()));
        }

        private static List<Item> ParseItems(JObject arguments)
        {
            var items = new List<Item>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (entry, index) in ((JArray)arguments[Items]!).Select((e, i) => (e, i)))
            {
                var at = $"{Items}[{index}]";
                if (!(entry is JObject item))
                {
                    throw ToolException.InvalidArgument(at, $"{at} must be an object with path and name.");
                }

                string? Optional(string name)
                {
                    var token = item[name];
                    if (token == null || token.Type == JTokenType.Null)
                    {
                        return null;
                    }

                    if (token.Type != JTokenType.String || string.IsNullOrWhiteSpace((string?)token))
                    {
                        throw ToolException.InvalidArgument($"{at}.{name}", $"{at}.{name} must be a non-empty string.");
                    }

                    return ((string?)token ?? string.Empty).Trim();
                }

                string Required(string name) => Optional(name) ?? throw ToolException.InvalidArgument($"{at}.{name}", $"{at}.{name} is required.");

                foreach (var property in item.Properties())
                {
                    if (property.Name != "path" && property.Name != "name" && property.Name != "displayName" && property.Name != "expectedVersion")
                    {
                        throw ToolException.InvalidArgument($"{at}.{property.Name}", $"{at}.{property.Name} is not a property of an item.");
                    }
                }

                var name = Required("name");
                if (!names.Add(name))
                {
                    throw ToolException.InvalidArgument($"{at}.name", $"{at}.name: '{name}' appears twice in one call.");
                }

                var version = Optional("expectedVersion");
                if (version != null && !long.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    throw ToolException.InvalidArgument($"{at}.expectedVersion", $"{at}.expectedVersion must be the versionNumber from get_component_state, digits only.");
                }

                items.Add(new Item(index, Required("path"), name, Optional("displayName"), version));
            }

            return items;
        }

        private static PreparedCall PrepareUpdate(ToolExecution execution)
        {
            var arguments = execution.Arguments;
            var service = execution.Service;
            var items = ParseItems(arguments);
            var solution = SolutionShapingTools.UnmanagedSolution(service, ((string)arguments[SolutionUniqueName]!).Trim());

            // The File root first: it is a setting, asked for before any approval dialog (SPEC 6.8).
            var root = execution.RequireFileRoot(items.Select(i => i.Path).ToList());
            var maxBytes = MaxUploadBytes(service);

            foreach (var item in items)
            {
                var at = $"{Items}[{item.Index}]";
                item.FullPath = FilePathResolver.Resolve(root.Path, item.Path, $"{at}.path");
                if (!TypeByExtension.TryGetValue(Path.GetExtension(item.FullPath), out var type))
                {
                    throw ToolException.InvalidArgument($"{at}.path", $"{at}.path: '{Path.GetExtension(item.FullPath)}' is not a web resource file type. Accepted: {string.Join(", ", TypeByExtension.Keys)}.");
                }

                item.Type = type;
                var info = new FileInfo(item.FullPath);
                if (maxBytes.HasValue && info.Length > maxBytes.Value)
                {
                    throw ToolException.InvalidArgument($"{at}.path", $"{at}.path: the file is {info.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes, over this organization's maximum upload size of {maxBytes.Value.ToString("N0", CultureInfo.InvariantCulture)} bytes.");
                }

                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(item.FullPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw ToolException.InvalidArgument($"{at}.path", $"{at}.path: the file cannot be read: {ex.Message}");
                }

                item.Size = bytes.Length;
                item.Sha256 = Hex(bytes);
                item.Base64 = Convert.ToBase64String(bytes);
            }

            var prefix = Prefix(service, solution);
            var inSolution = SolutionShapingTools.ReadComponents(service, solution.Id)
                .Where(c => c.Type == ComponentTypes.WebResource)
                .Select(c => c.Id)
                .ToHashSet();
            var conflicts = new JArray();
            foreach (var item in items)
            {
                var at = $"{Items}[{item.Index}]";
                var row = Existing(service, item.Name);
                if (row == null)
                {
                    if (!item.Name.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
                    {
                        throw ToolException.InvalidArgument($"{at}.name", $"{at}.name: a new web resource must start with the WIP publisher's prefix and an underscore ('{prefix}_'). '{item.Name}' does not.");
                    }

                    if (item.ExpectedVersion != null)
                    {
                        throw ToolException.InvalidArgument($"{at}.expectedVersion", $"{at}.expectedVersion was given, but no web resource named '{item.Name}' exists: it would be created.");
                    }

                    continue;
                }

                item.ExistingId = row.Id;
                item.VersionBefore = VersionOf(row);
                if (row.GetAttributeValue<bool>("ismanaged"))
                {
                    throw new ToolException(
                        ToolErrorCodes.ManagedComponent,
                        $"{at}: '{item.Name}' is a managed web resource. Do not customize it this way.",
                        new JObject { ["argument"] = at, ["name"] = item.Name });
                }

                if (!inSolution.Contains(row.Id))
                {
                    throw new ToolException(
                        ToolErrorCodes.NotInSolution,
                        $"{at}: '{item.Name}' exists but is not in solution '{solution.GetAttributeValue<string>("uniquename")}'. Add it with add_solution_components or PAC first.",
                        new JObject { ["argument"] = at, ["name"] = item.Name, ["solution"] = solution.GetAttributeValue<string>("uniquename") });
                }

                if (row.GetAttributeValue<OptionSetValue>("webresourcetype")?.Value is int existingType && existingType != item.Type)
                {
                    throw ToolException.InvalidArgument($"{at}.path", $"{at}.path: '{item.Name}' is a different web resource type from the file ({Path.GetExtension(item.FullPath)}).");
                }

                if (item.ExpectedVersion == null)
                {
                    throw ToolException.InvalidArgument($"{at}.expectedVersion", $"{at}.expectedVersion is required for the existing web resource '{item.Name}': pass the versionNumber from get_component_state.");
                }

                if (item.ExpectedVersion != item.VersionBefore)
                {
                    conflicts.Add(Conflict(item, row));
                }
            }

            if (conflicts.Count > 0)
            {
                throw new ToolException(
                    ToolErrorCodes.VersionConflict,
                    $"{conflicts.Count} web resource{(conflicts.Count == 1 ? " has" : "s have")} changed since the version passed. Nothing was written. Re-baseline with get_component_state, merge, or ask the person.",
                    new JObject { ["conflicts"] = conflicts });
            }

            var publish = MetadataHelpers.OptionalBool(arguments, Publish, true);
            var summary = new StringBuilder()
                .AppendLine($"Solution    : {solution.GetAttributeValue<string>("uniquename")}  (unmanaged)")
                .AppendLine($"File root   : {root.Path}  ({root.LevelLabel})")
                .AppendLine($"Publish     : {(publish ? "yes, just the resources written" : "no")}")
                .AppendLine("Files       :");
            foreach (var item in items)
            {
                summary
                    .AppendLine($"  {item.Index + 1,2}. {(item.ExistingId.HasValue ? "update" : "create")}  {item.Name}")
                    .AppendLine($"      from  {FilePathResolver.Relative(root.Path, item.FullPath!)}   {item.Size.ToString("N0", CultureInfo.InvariantCulture)} bytes")
                    .AppendLine($"      sha256 {item.Sha256}")
                    .AppendLine($"      version {item.VersionBefore ?? "— (new)"}");
            }

            return new PreparedCall(summary.ToString().TrimEnd(), new Plan(solution, items, publish, prefix));
        }

        private static JObject RunUpdate(ToolExecution execution)
        {
            var plan = (Plan)execution.Prepared!.State!;
            var service = execution.Service;
            var solutionName = plan.Solution.GetAttributeValue<string>("uniquename");
            var before = SolutionShapingTools.ReadComponents(service, plan.Solution.Id);

            var results = new JArray();
            var written = new List<Item>();
            var stopped = false;
            foreach (var item in plan.Items)
            {
                var result = new JObject
                {
                    ["index"] = item.Index,
                    ["name"] = item.Name,
                    ["id"] = ValueFormats.NullableString(item.ExistingId.HasValue ? ValueFormats.FormatGuid(item.ExistingId.Value) : null),
                    ["versionBefore"] = ValueFormats.NullableString(item.VersionBefore),
                    ["versionAfter"] = ValueFormats.NullableString(item.VersionBefore),
                    ["sha256"] = item.Sha256,
                };
                results.Add(result);
                item.Result = result;
                if (stopped)
                {
                    result["status"] = "notAttempted";
                    continue;
                }

                try
                {
                    if (item.ExistingId.HasValue)
                    {
                        // The version is read again: a change since preparation fails the item (SPEC 6.8, step 3).
                        var current = service.Retrieve(WebResource, item.ExistingId.Value, new ColumnSet("versionnumber", "modifiedby"));
                        if (VersionOf(current) != item.ExpectedVersion)
                        {
                            throw new ToolException(
                                ToolErrorCodes.VersionConflict,
                                $"'{item.Name}' changed since preparation: its version is {VersionOf(current)}, not {item.ExpectedVersion}.",
                                new JObject { ["conflicts"] = new JArray(Conflict(item, current)) });
                        }

                        var unpublished = ((RetrieveUnpublishedResponse)service.Execute(new RetrieveUnpublishedRequest
                        {
                            Target = new EntityReference(WebResource, item.ExistingId.Value),
                            ColumnSet = new ColumnSet("content"),
                        })).Entity;
                        if (string.Equals(unpublished.GetAttributeValue<string>("content"), item.Base64, StringComparison.Ordinal))
                        {
                            // Identical content is not written: every update raises versionNumber (S10).
                            result["status"] = "unchanged";
                            continue;
                        }

                        service.Update(new Entity(WebResource, item.ExistingId.Value) { ["content"] = item.Base64 });
                        result["status"] = "updated";
                    }
                    else
                    {
                        var create = new CreateRequest
                        {
                            Target = new Entity(WebResource)
                            {
                                ["name"] = item.Name,
                                ["displayname"] = item.DisplayName ?? Path.GetFileName(item.FullPath),
                                ["webresourcetype"] = new OptionSetValue(item.Type),
                                ["content"] = item.Base64,
                            },
                        };
                        create.Parameters["SolutionUniqueName"] = solutionName;
                        var id = ((CreateResponse)service.Execute(create)).id;
                        item.ExistingId = id;
                        result["id"] = ValueFormats.FormatGuid(id);
                        result["status"] = "created";
                    }

                    written.Add(item);
                }
                catch (Exception ex)
                {
                    result["status"] = "failed";
                    result["error"] = ToolException.FromDataverse(ex).ToErrorObject();
                    stopped = true;
                }
            }

            var published = false;
            string? publishError = null;
            if (plan.Publish && written.Count > 0)
            {
                try
                {
                    service.Execute(new PublishXmlRequest { ParameterXml = PublishXml(written.Select(w => w.ExistingId!.Value)) });
                    published = true;
                }
                catch (Exception ex)
                {
                    publishError = ToolException.FromDataverse(ex).Message;
                }
            }

            // Publishing raises the version again, so versionAfter is read last (S10).
            foreach (var item in plan.Items.Where(i => i.ExistingId.HasValue && i.Result != null && (string?)i.Result["status"] is "created" or "updated" or "unchanged"))
            {
                item.Result!["versionAfter"] = ValueFormats.NullableString(VersionOf(service.Retrieve(WebResource, item.ExistingId!.Value, new ColumnSet("versionnumber"))));
            }

            var unexpected = ReadBack(service, plan, before, written);
            return new JObject
            {
                ["solution"] = new JObject { ["solutionId"] = ValueFormats.FormatGuid(plan.Solution.Id), ["uniqueName"] = solutionName },
                ["results"] = results,
                ["published"] = published,
                ["publishError"] = ValueFormats.NullableString(publishError),
                ["unexpected"] = unexpected,
                ["unexpectedCount"] = unexpected.Count,
            };
        }

        private static JArray ReadBack(IOrganizationService service, Plan plan, List<(int Type, Guid Id, int? Behavior)> before, List<Item> written)
        {
            var known = new HashSet<(int, Guid)>(before.Select(c => (c.Type, c.Id)));
            foreach (var item in plan.Items.Where(i => i.ExistingId.HasValue))
            {
                known.Add((ComponentTypes.WebResource, item.ExistingId!.Value));
            }

            var arrived = SolutionShapingTools.ReadComponents(service, plan.Solution.Id).Where(c => !known.Contains((c.Type, c.Id))).ToList();
            var names = new ComponentNames(service).Resolve(arrived.Select(c => (c.Type, c.Id)));
            var perEnvironment = new Dictionary<int, string>();
            var unexpected = new JArray();
            foreach (var component in arrived)
            {
                unexpected.Add(new JObject
                {
                    ["componentType"] = SolutionShapingTools.TypeJson(service, component.Type, perEnvironment),
                    ["objectId"] = ValueFormats.FormatGuid(component.Id),
                    ["name"] = ValueFormats.NullableString(names.TryGetValue((component.Type, component.Id), out var name) ? name : null),
                    ["rootComponentBehavior"] = SolutionTools.RootBehavior(component.Behavior.HasValue ? new OptionSetValue(component.Behavior.Value) : null),
                });
            }

            return unexpected;
        }

        private static string PublishXml(IEnumerable<Guid> ids) =>
            "<importexportxml><webresources>" + string.Concat(ids.Select(id => "<webresource>{" + id.ToString("D") + "}</webresource>")) + "</webresources></importexportxml>";

        private static JObject Conflict(Item item, Entity current)
        {
            var modifiedBy = current.GetAttributeValue<EntityReference>("modifiedby");
            return new JObject
            {
                ["name"] = item.Name,
                ["expectedVersion"] = item.ExpectedVersion,
                ["currentVersion"] = VersionOf(current),
                ["modifiedBy"] = modifiedBy == null
                    ? JValue.CreateNull()
                    : new JObject { ["id"] = ValueFormats.FormatGuid(modifiedBy.Id), ["name"] = ValueFormats.NullableString(modifiedBy.Name) },
            };
        }

        private static Entity? Existing(IOrganizationService service, string name)
        {
            var query = new QueryExpression(WebResource) { ColumnSet = new ColumnSet("name", "webresourcetype", "ismanaged", "versionnumber", "modifiedby"), TopCount = 1 };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            return service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        private static string? VersionOf(Entity row) =>
            row.Contains("versionnumber") ? Convert.ToInt64(row["versionnumber"], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : null;

        private static string Prefix(IOrganizationService service, Entity solution)
        {
            var publisher = service.Retrieve("publisher", solution.GetAttributeValue<EntityReference>("publisherid").Id, new ColumnSet("customizationprefix"));
            return publisher.GetAttributeValue<string>("customizationprefix");
        }

        private static long? MaxUploadBytes(IOrganizationService service)
        {
            var query = new QueryExpression("organization") { ColumnSet = new ColumnSet("maxuploadfilesize"), TopCount = 1 };
            var value = service.RetrieveMultiple(query).Entities.FirstOrDefault()?.GetAttributeValue<int?>("maxuploadfilesize");
            return value > 0 ? value : null;
        }

        private static string Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>One file of the call, with what preparation learned about it.</summary>
        private sealed class Item
        {
            public Item(int index, string path, string name, string? displayName, string? expectedVersion)
            {
                this.Index = index;
                this.Path = path;
                this.Name = name;
                this.DisplayName = displayName;
                this.ExpectedVersion = expectedVersion;
            }

            public int Index { get; }

            public string Path { get; }

            public string Name { get; }

            public string? DisplayName { get; }

            public string? ExpectedVersion { get; }

            public string? FullPath { get; set; }

            public int Type { get; set; }

            public long Size { get; set; }

            public string Sha256 { get; set; } = string.Empty;

            public string Base64 { get; set; } = string.Empty;

            public Guid? ExistingId { get; set; }

            public string? VersionBefore { get; set; }

            public JObject? Result { get; set; }
        }

        /// <summary>What the call resolved before its dialog.</summary>
        private sealed class Plan
        {
            public Plan(Entity solution, List<Item> items, bool publish, string prefix)
            {
                this.Solution = solution;
                this.Items = items;
                this.Publish = publish;
                this.Prefix = prefix;
            }

            public Entity Solution { get; }

            public List<Item> Items { get; }

            public bool Publish { get; }

            public string Prefix { get; }
        }
    }
}
