// <copyright file="CallLogFormatter.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>Formats the call line and outcome line of every call (SPEC §8.6, R13).</summary>
    internal static class CallLogFormatter
    {
        /// <summary>The longest parameter or return value shown whole.</summary>
        public const int ValueLimit = 10;

        /// <summary>Marks a value that was cut.</summary>
        public const char Ellipsis = '…';

        /// <summary>The approval column for a call that never ran.</summary>
        public const string NotRun = "—";

        private const string Separator = "  ";
        private const string ReasonArgument = "reason";

        /// <summary>Formats a call line.</summary>
        /// <param name="receivedLocal">When the host received the call, in local time.</param>
        /// <param name="tool">The tool name as called.</param>
        /// <param name="arguments">The arguments the agent sent, or <c>null</c> when they were not an object.</param>
        /// <param name="inputOrder">The tool's input-schema property order.</param>
        /// <returns>The call line.</returns>
        public static string CallLine(DateTime receivedLocal, string tool, JObject? arguments, IReadOnlyList<string> inputOrder)
        {
            var line = new StringBuilder()
                .Append(ValueFormats.FormatLocalTimestamp(receivedLocal))
                .Append(Separator)
                .Append(Sanitize(tool))
                .Append(Separator)
                .Append(Reason(arguments?[ReasonArgument]));

            foreach (var argument in OrderArguments(arguments, inputOrder))
            {
                line.Append(Separator).Append(Sanitize(argument.Name)).Append('=').Append(Value(argument.Value));
            }

            return line.ToString();
        }

        /// <summary>Formats the outcome line of a successful call.</summary>
        /// <param name="approval">How the call was authorized.</param>
        /// <param name="duration">From receipt to reply.</param>
        /// <param name="structuredContent">The result; each top-level field is shown in output-schema order.</param>
        /// <returns>The outcome line.</returns>
        public static string OutcomeOk(string approval, TimeSpan duration, JObject structuredContent)
        {
            var line = new StringBuilder("  → ok").Append(Separator).Append(approval).Append(Separator).Append(Duration(duration));
            foreach (var field in structuredContent.Properties())
            {
                line.Append(Separator).Append(Sanitize(field.Name)).Append('=').Append(Value(field.Value));
            }

            return line.ToString();
        }

        /// <summary>Formats the outcome line of a failed call. The message is shown in full.</summary>
        /// <param name="code">The tool error code.</param>
        /// <param name="approval">How the call was authorized, or <see cref="NotRun"/>.</param>
        /// <param name="duration">From receipt to reply.</param>
        /// <param name="message">The tool error message.</param>
        /// <returns>The outcome line.</returns>
        public static string OutcomeError(string code, string approval, TimeSpan duration, string message) =>
            $"  → error {Sanitize(code)}{Separator}{approval}{Separator}{Duration(duration)}{Separator}{Sanitize(message)}";

        /// <summary>Formats the line naming a component a call added unexpectedly; the name is shown in full (R1).</summary>
        /// <param name="type">The component type name.</param>
        /// <param name="name">The component name.</param>
        /// <returns>The warning line.</returns>
        public static string Unexpected(string type, string name) => $"  ⚠ unexpected {Sanitize(type)} {Sanitize(name)}";

        /// <summary>Formats one parameter or return value: strings as they are, everything else as compact JSON, then made line-safe and truncated.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The formatted value.</returns>
        public static string Value(JToken value)
        {
            // A JValue made from a null string has type String and a null value; it is shown as the literal null.
            var raw = value is JValue { Value: string text } ? text : value.ToString(Formatting.None);
            return Truncate(Sanitize(raw));
        }

        /// <summary>
        /// Replaces every control character and every Unicode bidirectional-formatting character with a space,
        /// so agent text cannot start a fake log line or reorder what the person sees (R8).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The line-safe text.</returns>
        public static string Sanitize(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                builder.Append(IsUnsafe(c) ? ' ' : c);
            }

            return builder.ToString();
        }

        /// <summary>Cuts a value longer than <see cref="ValueLimit"/> characters to its first ten, followed by an ellipsis.</summary>
        /// <param name="text">The value.</param>
        /// <returns>The value, whole or cut.</returns>
        public static string Truncate(string text)
        {
            if (text.Length <= ValueLimit)
            {
                return text;
            }

            var cut = char.IsHighSurrogate(text[ValueLimit - 1]) ? ValueLimit - 1 : ValueLimit;
            return text.Substring(0, cut) + Ellipsis;
        }

        /// <summary>Formats a duration as seconds with one decimal.</summary>
        /// <param name="duration">The duration.</param>
        /// <returns>For example <c>0.8 s</c>.</returns>
        public static string Duration(TimeSpan duration) =>
            string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", Math.Max(0, duration.TotalSeconds));

        private static string Reason(JToken? reason)
        {
            if (reason == null)
            {
                return "reason —";
            }

            var text = reason is JValue { Value: string value } ? value : reason.ToString(Formatting.None);
            return $"reason \"{Sanitize(text)}\"";
        }

        private static IEnumerable<JProperty> OrderArguments(JObject? arguments, IReadOnlyList<string> inputOrder)
        {
            if (arguments == null)
            {
                return Enumerable.Empty<JProperty>();
            }

            var sent = arguments.Properties().Where(p => p.Name != ReasonArgument).ToList();
            var known = inputOrder.Select(name => sent.FirstOrDefault(p => p.Name == name)).Where(p => p != null).Cast<JProperty>();
            return known.Concat(sent.Where(p => !inputOrder.Contains(p.Name)));
        }

        // Control characters, the line and paragraph separators, and the bidirectional formatting characters:
        // ALM, LRM, RLM, LRE..RLO and LRI..PDI.
        private static bool IsUnsafe(char c) =>
            char.IsControl(c)
            || c == '\u2028' || c == '\u2029'
            || c == '\u061C' || c == '\u200E' || c == '\u200F'
            || (c >= '\u202A' && c <= '\u202E')
            || (c >= '\u2066' && c <= '\u2069');
    }
}
