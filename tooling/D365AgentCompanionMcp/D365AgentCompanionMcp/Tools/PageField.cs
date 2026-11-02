// <copyright file="PageField.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using Newtonsoft.Json.Linq;

    /// <summary>A result field of a paged tool beside its items and <c>nextCursor</c>, for the output schema.</summary>
    internal sealed class PageField
    {
        private PageField(string name, JObject schema, bool required)
        {
            this.Name = name;
            this.Schema = schema;
            this.Required = required;
        }

        /// <summary>Gets the field name.</summary>
        public string Name { get; }

        /// <summary>Gets the field's schema.</summary>
        public JObject Schema { get; }

        /// <summary>Gets a value indicating whether every page carries the field.</summary>
        public bool Required { get; }

        /// <summary>A field on every page.</summary>
        /// <param name="name">The field name.</param>
        /// <param name="schema">Its schema.</param>
        /// <returns>The field.</returns>
        public static PageField Every(string name, JObject schema) => new PageField(name, schema, required: true);

        /// <summary>A field on the first page only, or only when the caller asked for it.</summary>
        /// <param name="name">The field name.</param>
        /// <param name="schema">Its schema.</param>
        /// <returns>The field.</returns>
        public static PageField First(string name, JObject schema) => new PageField(name, schema, required: false);
    }
}
