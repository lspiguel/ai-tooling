// <copyright file="ToolDefinition.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Newtonsoft.Json.Linq;

    /// <summary>One tool: its listing in <c>tools/list</c>, its tier and how it runs.</summary>
    internal sealed class ToolDefinition
    {
        /// <summary>The field of a chunk item that holds its text.</summary>
        public const string ChunkText = "text";

        private ToolDefinition(string name, string title, string description, ToolTier tier, JObject inputSchema, JObject outputSchema)
        {
            this.Name = name;
            this.Title = title;
            this.Description = description;
            this.Tier = tier;
            this.InputSchema = inputSchema;
            this.OutputSchema = outputSchema;
            this.InputOrder = ((JObject)inputSchema["properties"]!).Properties().Select(p => p.Name).ToList();
        }

        /// <summary>Gets the tool name.</summary>
        public string Name { get; }

        /// <summary>Gets the display title.</summary>
        public string Title { get; }

        /// <summary>Gets the description shown to the agent.</summary>
        public string Description { get; }

        /// <summary>Gets the authorization tier.</summary>
        public ToolTier Tier { get; }

        /// <summary>Gets the input schema.</summary>
        public JObject InputSchema { get; }

        /// <summary>Gets the output schema.</summary>
        public JObject OutputSchema { get; }

        /// <summary>Gets the input property names, in schema order — the call line's parameter order.</summary>
        public IReadOnlyList<string> InputOrder { get; }

        /// <summary>Gets the default page size; <c>null</c> for tools that are not paged.</summary>
        public int? DefaultPageSize { get; private set; }

        /// <summary>Gets a value indicating whether the tool is paged.</summary>
        public bool IsPaged => this.DefaultPageSize.HasValue;

        /// <summary>Gets how a host tool runs.</summary>
        public Func<JObject>? RunHost { get; private set; }

        /// <summary>Gets how a tool that is not paged runs, on a background thread.</summary>
        public Func<ToolExecution, JObject>? Run { get; private set; }

        /// <summary>Gets how a paged tool opens its item source.</summary>
        public Func<ToolExecution, IPageSource>? OpenPages { get; private set; }

        /// <summary>
        /// Gets how a paged tool builds its result from a page of items, the next cursor, the source's own fields and
        /// whether this is the first page.
        /// </summary>
        public Func<JArray, string?, IPageFields?, bool, JObject>? BuildPage { get; private set; }

        /// <summary>Gets the name of a paged tool's item array; <c>null</c> for other tools.</summary>
        public string? ItemsName { get; private set; }

        /// <summary>Gets a value indicating whether every page holds one item whatever <c>pageSize</c> says — XML chunks.</summary>
        public bool FixedPageSize { get; private set; }

        /// <summary>
        /// Gets the check a tool runs on its arguments before the approval dialog, on the first call only. It throws
        /// <see cref="ToolException"/> to refuse the call, and says what the dialog must flag.
        /// </summary>
        public Func<BoundConnection, JObject, CallInspection>? Inspect { get; private set; }

        /// <summary>Defines the host tool.</summary>
        /// <param name="name">The name.</param>
        /// <param name="description">The description.</param>
        /// <param name="input">The input schema.</param>
        /// <param name="output">The output schema.</param>
        /// <param name="run">Runs the tool.</param>
        /// <returns>The definition.</returns>
        public static ToolDefinition Host(string name, string description, InputSchema input, JObject output, Func<JObject> run) =>
            new ToolDefinition(name, name, description, ToolTier.Host, input.Build(), output) { RunHost = run };

        /// <summary>Defines a tool that is not paged.</summary>
        /// <param name="name">The name.</param>
        /// <param name="tier">The tier.</param>
        /// <param name="description">The description.</param>
        /// <param name="input">The input schema.</param>
        /// <param name="output">The output schema.</param>
        /// <param name="run">Runs the tool.</param>
        /// <returns>The definition.</returns>
        public static ToolDefinition Simple(string name, ToolTier tier, string description, InputSchema input, JObject output, Func<ToolExecution, JObject> run) =>
            new ToolDefinition(name, name, description, tier, input.Build(), output) { Run = run };

        /// <summary>Defines a paged tool.</summary>
        /// <param name="name">The name.</param>
        /// <param name="tier">The tier.</param>
        /// <param name="description">The description.</param>
        /// <param name="defaultPageSize">The default page size.</param>
        /// <param name="input">The input schema, without the paging inputs.</param>
        /// <param name="itemsName">The name of the result's item array.</param>
        /// <param name="itemSchema">The schema of one item.</param>
        /// <param name="openPages">Opens the item source.</param>
        /// <param name="tailInput">Adds the inputs that follow paging, such as <c>reason</c>.</param>
        /// <param name="fields">Result fields beside the items, which the source supplies through <see cref="IPageFields"/>.</param>
        /// <returns>The definition.</returns>
        public static ToolDefinition Paged(string name, ToolTier tier, string description, int defaultPageSize, InputSchema input, string itemsName, JObject itemSchema, Func<ToolExecution, IPageSource> openPages, Func<InputSchema, InputSchema> tailInput, params PageField[] fields)
        {
            var output = PagedOutput(fields, (itemsName, Tools.OutputSchema.Array(itemSchema), true));
            return new ToolDefinition(name, name, description, tier, tailInput(input.Paging(defaultPageSize)).Build(), output)
            {
                ItemsName = itemsName,
                DefaultPageSize = defaultPageSize,
                OpenPages = openPages,
                BuildPage = (items, cursor, source, firstPage) => PageResult(source, firstPage, cursor, result =>
                {
                    if (source == null || !source.OmitItems)
                    {
                        result[itemsName] = items;
                    }
                }),
            };
        }

        /// <summary>
        /// Defines a tool that pages one text payload in chunks (SPEC §6.2: XML payloads). Each page holds one chunk
        /// item, <c>{ "text": "..." }</c>, which the result carries as a string; <c>pageSize</c> is ignored.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="tier">The tier.</param>
        /// <param name="description">The description.</param>
        /// <param name="input">The input schema, without the paging inputs.</param>
        /// <param name="textName">The name of the result's text field.</param>
        /// <param name="openPages">Opens the chunk source.</param>
        /// <param name="tailInput">Adds the inputs that follow paging, such as <c>reason</c>.</param>
        /// <param name="fields">Result fields beside the text.</param>
        /// <returns>The definition.</returns>
        public static ToolDefinition Chunked(string name, ToolTier tier, string description, InputSchema input, string textName, Func<ToolExecution, IPageSource> openPages, Func<InputSchema, InputSchema> tailInput, params PageField[] fields)
        {
            var output = PagedOutput(fields, (textName, Tools.OutputSchema.String(), true));
            return new ToolDefinition(name, name, description, tier, tailInput(input.Paging(1)).Build(), output)
            {
                DefaultPageSize = 1,
                FixedPageSize = true,
                OpenPages = openPages,
                BuildPage = (items, cursor, source, firstPage) => PageResult(source, firstPage, cursor, result =>
                    result[textName] = string.Concat(items.Select(i => (string?)i[ChunkText]))),
            };
        }

        /// <summary>Marks the item array as present only when the caller asked for items (<see cref="IPageFields.OmitItems"/>).</summary>
        /// <returns>This definition.</returns>
        public ToolDefinition WithOptionalItems()
        {
            var required = (JArray)this.OutputSchema["required"]!;
            required.Where(r => (string?)r == this.ItemsName).ToList().ForEach(r => r.Remove());
            return this;
        }

        /// <summary>Adds the check a tool runs before the approval dialog.</summary>
        /// <param name="inspect">The check.</param>
        /// <returns>This definition.</returns>
        public ToolDefinition WithInspection(Func<BoundConnection, JObject, CallInspection> inspect)
        {
            this.Inspect = inspect;
            return this;
        }

        /// <summary>Builds the entry in <c>tools/list</c>.</summary>
        /// <returns>The entry.</returns>
        public JObject ToListEntry() => new JObject
        {
            ["name"] = this.Name,
            ["title"] = this.Title,
            ["description"] = this.Description,
            ["inputSchema"] = this.InputSchema.DeepClone(),
            ["outputSchema"] = this.OutputSchema.DeepClone(),
        };

        private static JObject PagedOutput(PageField[] fields, (string Name, JObject Schema, bool Required) payload)
        {
            var all = fields.Select(f => (Name: f.Name, Schema: f.Schema, Required: f.Required))
                .Concat(new[] { payload, (Name: "nextCursor", Schema: Tools.OutputSchema.Cursor(), Required: true) })
                .ToList();
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(all.Select(f => new JProperty(f.Name, f.Schema))),
                ["required"] = new JArray(all.Where(f => f.Required).Select(f => f.Name)),
            };
        }

        private static JObject PageResult(IPageFields? source, bool firstPage, string? cursor, Action<JObject> addPayload)
        {
            var result = new JObject();
            Merge(result, source?.EveryPage);
            if (firstPage)
            {
                Merge(result, source?.FirstPage);
            }

            addPayload(result);
            result["nextCursor"] = ValueFormats.NullableString(cursor);
            return result;
        }

        private static void Merge(JObject result, JObject? fields)
        {
            if (fields == null)
            {
                return;
            }

            foreach (var field in fields.Properties())
            {
                result[field.Name] = field.Value.DeepClone();
            }
        }
    }
}
