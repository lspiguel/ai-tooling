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
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Newtonsoft.Json.Linq;

    /// <summary>One tool: its listing in <c>tools/list</c>, its tier and how it runs.</summary>
    internal sealed class ToolDefinition
    {
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

        /// <summary>Gets how a paged tool builds its result from a page of items and the next cursor.</summary>
        public Func<JArray, string?, JObject>? BuildPage { get; private set; }

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
        /// <returns>The definition.</returns>
        public static ToolDefinition Paged(string name, ToolTier tier, string description, int defaultPageSize, InputSchema input, string itemsName, JObject itemSchema, Func<ToolExecution, IPageSource> openPages, Func<InputSchema, InputSchema> tailInput)
        {
            var output = Tools.OutputSchema.Object((itemsName, Tools.OutputSchema.Array(itemSchema)), ("nextCursor", Tools.OutputSchema.Cursor()));
            return new ToolDefinition(name, name, description, tier, tailInput(input.Paging(defaultPageSize)).Build(), output)
            {
                DefaultPageSize = defaultPageSize,
                OpenPages = openPages,
                BuildPage = (items, cursor) => new JObject { [itemsName] = items, ["nextCursor"] = ValueFormats.NullableString(cursor) },
            };
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
    }
}
