// <copyright file="ValueFormats.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Common
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json.Linq;

    /// <summary>The value formats every tool result uses (SPEC §6.2).</summary>
    internal static class ValueFormats
    {
        /// <summary>Formats a GUID lowercase and hyphenated, without braces.</summary>
        /// <param name="id">The GUID.</param>
        /// <returns>The formatted GUID.</returns>
        public static string FormatGuid(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

        /// <summary>Formats a date-time as ISO 8601 in UTC with <c>Z</c>, to the second.</summary>
        /// <param name="value">The date-time; an unspecified kind is taken as UTC.</param>
        /// <returns>The formatted date-time.</returns>
        public static string FormatDateTime(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Makes a JSON value from a string that may be <c>null</c>. Newtonsoft turns a null string into a String-typed
        /// token with no value; results need a real JSON null so they match their output schema.
        /// </summary>
        /// <param name="value">The string, or <c>null</c>.</param>
        /// <returns>The token.</returns>
        public static JToken NullableString(string? value) => value == null ? JValue.CreateNull() : new JValue(value);

        /// <summary>Formats a local time as the call log shows it.</summary>
        /// <param name="local">The local time.</param>
        /// <returns>The time as <c>yyyy-MM-dd HH:mm:ss</c>.</returns>
        public static string FormatLocalTimestamp(DateTime local) => local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
