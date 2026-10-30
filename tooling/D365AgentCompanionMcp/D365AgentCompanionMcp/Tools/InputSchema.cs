// <copyright file="InputSchema.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Builds a tool's <c>inputSchema</c> (SPEC §6.2): <c>connection</c> first and required, then the tool's own
    /// arguments, then the paging inputs, then <c>reason</c>; <c>additionalProperties: false</c>.
    /// </summary>
    internal sealed class InputSchema
    {
        /// <summary>The <c>connection</c> argument.</summary>
        public const string Connection = "connection";

        /// <summary>The <c>reason</c> argument.</summary>
        public const string Reason = "reason";

        /// <summary>The paging <c>cursor</c> argument.</summary>
        public const string Cursor = "cursor";

        /// <summary>The paging <c>pageSize</c> argument.</summary>
        public const string PageSize = "pageSize";

        /// <summary>The longest reason accepted.</summary>
        public const int ReasonMaxLength = 500;

        private readonly JObject properties = new JObject();
        private readonly JArray required = new JArray();

        private InputSchema()
        {
        }

        /// <summary>Starts a schema with no arguments, for <c>list_connections</c>.</summary>
        /// <returns>The builder.</returns>
        public static InputSchema None() => new InputSchema();

        /// <summary>Starts a schema with the required <c>connection</c> argument first.</summary>
        /// <returns>The builder.</returns>
        public static InputSchema ForConnection() =>
            new InputSchema().String(Connection, "connectionId from list_connections. No default: name it on every call.", required: true);

        /// <summary>Adds a string argument.</summary>
        /// <param name="name">The argument name.</param>
        /// <param name="description">Its description.</param>
        /// <param name="required">Whether it is required.</param>
        /// <param name="minLength">The shortest value, if any.</param>
        /// <param name="maxLength">The longest value, if any.</param>
        /// <returns>The builder.</returns>
        public InputSchema String(string name, string description, bool required = false, int? minLength = null, int? maxLength = null)
        {
            var property = new JObject { ["type"] = "string", ["description"] = description };
            if (minLength.HasValue)
            {
                property["minLength"] = minLength.Value;
            }

            if (maxLength.HasValue)
            {
                property["maxLength"] = maxLength.Value;
            }

            return this.Add(name, property, required);
        }

        /// <summary>Adds the paging inputs <c>cursor</c> and <c>pageSize</c>.</summary>
        /// <param name="defaultPageSize">The tool's default page size.</param>
        /// <returns>The builder.</returns>
        public InputSchema Paging(int defaultPageSize)
        {
            this.String(Cursor, "nextCursor from the previous page, with the same connection. Omit on the first call; other arguments are then ignored.");
            var pageSize = new JObject
            {
                ["type"] = "integer",
                ["minimum"] = 1,
                ["default"] = defaultPageSize,
                ["description"] = "Items per page, first call only. At most the host's maximum page size.",
            };
            return this.Add(PageSize, pageSize, required: false);
        }

        /// <summary>Adds <c>reason</c>: optional for Read tools, required and non-blank for Write and Publisher tools (R12).</summary>
        /// <param name="required">Whether it is required.</param>
        /// <returns>The builder.</returns>
        public InputSchema WithReason(bool required)
        {
            var description = required
                ? "Why this call is needed. Required. Shown to the person approving it as unverified agent text."
                : "Why this call is needed. Optional. Shown to the person approving it as unverified agent text.";
            return required
                ? this.String(Reason, description, required: true, minLength: 1, maxLength: ReasonMaxLength)
                : this.String(Reason, description, maxLength: ReasonMaxLength);
        }

        /// <summary>Builds the schema.</summary>
        /// <returns>The JSON Schema object.</returns>
        public JObject Build() => new JObject
        {
            ["type"] = "object",
            ["properties"] = this.properties.DeepClone(),
            ["required"] = this.required.DeepClone(),
            ["additionalProperties"] = false,
        };

        private InputSchema Add(string name, JObject property, bool required)
        {
            this.properties[name] = property;
            if (required)
            {
                this.required.Add(name);
            }

            return this;
        }
    }
}
