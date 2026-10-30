// <copyright file="OutputSchema.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Builds <c>outputSchema</c> fragments from the TypeScript notation of SPEC §6.2: <c>T | null</c> → a type array
    /// with <c>"null"</c>; <c>Guid</c> → <c>format: uuid</c>; <c>DateTime</c> → <c>format: date-time</c>. Output schemas
    /// allow additional properties, so fields can be added later.
    /// </summary>
    internal static class OutputSchema
    {
        /// <summary>An object whose listed fields are all required.</summary>
        /// <param name="fields">The fields, in output order.</param>
        /// <returns>The schema.</returns>
        public static JObject Object(params (string Name, JObject Schema)[] fields) => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(fields.Select(f => new JProperty(f.Name, f.Schema))),
            ["required"] = new JArray(fields.Select(f => f.Name)),
        };

        /// <summary>A string.</summary>
        /// <returns>The schema.</returns>
        public static JObject String() => new JObject { ["type"] = "string" };

        /// <summary>A string or <c>null</c>.</summary>
        /// <returns>The schema.</returns>
        public static JObject NullableString() => new JObject { ["type"] = new JArray("string", "null") };

        /// <summary>A GUID.</summary>
        /// <returns>The schema.</returns>
        public static JObject Guid() => new JObject { ["type"] = "string", ["format"] = "uuid" };

        /// <summary>A date-time.</summary>
        /// <returns>The schema.</returns>
        public static JObject DateTime() => new JObject { ["type"] = "string", ["format"] = "date-time" };

        /// <summary>An integer.</summary>
        /// <returns>The schema.</returns>
        public static JObject Integer() => new JObject { ["type"] = "integer" };

        /// <summary>A Boolean.</summary>
        /// <returns>The schema.</returns>
        public static JObject Boolean() => new JObject { ["type"] = "boolean" };

        /// <summary>An array.</summary>
        /// <param name="items">The item schema.</param>
        /// <returns>The schema.</returns>
        public static JObject Array(JObject items) => new JObject { ["type"] = "array", ["items"] = items };

        /// <summary>One of the given strings, or <c>null</c>.</summary>
        /// <param name="values">The strings.</param>
        /// <returns>The schema.</returns>
        public static JObject NullableEnum(params string[] values)
        {
            var allowed = new JArray(values.Cast<object>().ToArray()) { JValue.CreateNull() };
            return new JObject { ["type"] = new JArray("string", "null"), ["enum"] = allowed };
        }

        /// <summary>The paging cursor of every paged result.</summary>
        /// <returns>The schema.</returns>
        public static JObject Cursor() => NullableString();
    }
}
