// <copyright file="RecordValues.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Microsoft.Xrm.Sdk;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Serializes a record as SPEC §6.5 <c>query</c> sets out: plain JSON for text, numbers, Booleans, GUIDs and
    /// date-times; typed wrappers for option sets, references and money; aliased values unwrapped under the key the
    /// platform returns. Columns without a value are omitted. Labels, names and formatted values appear only when asked for.
    /// </summary>
    internal static class RecordValues
    {
        /// <summary>Serializes one record.</summary>
        /// <param name="record">The record.</param>
        /// <param name="includeFormattedValues">Whether to add labels, names and formatted values.</param>
        /// <returns>The JSON object.</returns>
        public static JObject Serialize(Entity record, bool includeFormattedValues)
        {
            var result = new JObject();
            foreach (var attribute in record.Attributes)
            {
                var formatted = includeFormattedValues && record.FormattedValues.Contains(attribute.Key) ? record.FormattedValues[attribute.Key] : null;
                var value = Value(attribute.Value, formatted, includeFormattedValues);
                if (value != null)
                {
                    result[attribute.Key] = value;
                }
            }

            return result;
        }

        /// <summary>Serializes one value.</summary>
        /// <param name="value">The SDK value.</param>
        /// <param name="formatted">The platform's formatted value, if any.</param>
        /// <param name="includeFormattedValues">Whether to add labels, names and formatted values.</param>
        /// <returns>The JSON value, or <c>null</c> when there is no value.</returns>
        public static JToken? Value(object? value, string? formatted, bool includeFormattedValues)
        {
            switch (value)
            {
                case null:
                    return null;
                case AliasedValue aliased:
                    return Value(aliased.Value, formatted, includeFormattedValues);
                case string text:
                    return text;
                case Guid id:
                    return ValueFormats.FormatGuid(id);
                case DateTime date:
                    return ValueFormats.FormatDateTime(date);
                case bool flag:
                    return flag;
                case int number:
                    return number;
                case long number:
                    return number;
                case decimal number:
                    return number;
                case double number:
                    return number;
                case float number:
                    return number;
                case OptionSetValue option:
                    var single = new JObject { ["_option"] = option.Value };
                    if (includeFormattedValues)
                    {
                        single["label"] = ValueFormats.NullableString(formatted);
                    }

                    return single;
                case OptionSetValueCollection options:
                    var many = new JObject { ["_options"] = new JArray(options.Select(o => o.Value)) };
                    if (includeFormattedValues)
                    {
                        var labels = formatted?.Split(new[] { "; " }, StringSplitOptions.None);
                        many["labels"] = new JArray(options.Select((o, i) => labels != null && labels.Length == options.Count ? new JValue(labels[i]) : JValue.CreateNull()));
                    }

                    return many;
                case EntityReference reference:
                    var target = new JObject
                    {
                        ["_ref"] = new JObject { ["entity"] = reference.LogicalName, ["id"] = ValueFormats.FormatGuid(reference.Id) },
                    };
                    if (includeFormattedValues)
                    {
                        target["name"] = ValueFormats.NullableString(formatted ?? reference.Name);
                    }

                    return target;
                case Money money:
                    var amount = new JObject { ["_money"] = money.Value };
                    if (includeFormattedValues)
                    {
                        amount["formatted"] = ValueFormats.NullableString(formatted);
                    }

                    return amount;
                case byte[] bytes:
                    return Convert.ToBase64String(bytes);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
