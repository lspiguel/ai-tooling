// <copyright file="MetadataHelpers.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Linq;
    using System.ServiceModel;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>What the metadata tools share: labels in the base language, trimmed values and not-found faults.</summary>
    internal static class MetadataHelpers
    {
        /// <summary>The platform's "object does not exist" fault code.</summary>
        public const int ObjectDoesNotExist = unchecked((int)0x80040217);

        /// <summary>Reads the organization's base language (SPEC §5 Serialization: labels in the base language).</summary>
        /// <param name="execution">The call.</param>
        /// <returns>The LCID.</returns>
        public static int BaseLanguage(ToolExecution execution) =>
            execution.Service.Retrieve("organization", execution.Binding.OrganizationId, new ColumnSet("languagecode")).GetAttributeValue<int>("languagecode");

        /// <summary>A label in the given language, falling back to the caller's; <c>null</c> when empty (SPEC §6.2).</summary>
        /// <param name="label">The label.</param>
        /// <param name="lcid">The base language.</param>
        /// <returns>The text, as a JSON value.</returns>
        public static JToken Label(Label? label, int lcid)
        {
            var text = label?.LocalizedLabels?.FirstOrDefault(l => l.LanguageCode == lcid)?.Label ?? label?.UserLocalizedLabel?.Label;
            return ValueFormats.NullableString(string.IsNullOrEmpty(text) ? null : text);
        }

        /// <summary>A string, or JSON null when it is <c>null</c> or empty.</summary>
        /// <param name="value">The string.</param>
        /// <returns>The JSON value.</returns>
        public static JToken Text(string? value) => ValueFormats.NullableString(string.IsNullOrEmpty(value) ? null : value);

        /// <summary>A metadata ID, or JSON null.</summary>
        /// <param name="id">The ID.</param>
        /// <returns>The JSON value.</returns>
        public static JToken Id(Guid? id) => ValueFormats.NullableString(id.HasValue ? ValueFormats.FormatGuid(id.Value) : null);

        /// <summary>An enum name in camel case, as the output schemas spell them — <c>UserOwned</c> → <c>userOwned</c>.</summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The name.</returns>
        public static string Camel(Enum value)
        {
            var name = value.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>Whether a fault says the named metadata does not exist.</summary>
        /// <param name="ex">The exception.</param>
        /// <returns><c>true</c> when it is a not-found fault.</returns>
        public static bool IsNotFound(Exception ex) =>
            ex is FaultException<OrganizationServiceFault> fault
            && (fault.Detail.ErrorCode == ObjectDoesNotExist
                || (fault.Detail.Message ?? string.Empty).IndexOf("Could not find", StringComparison.OrdinalIgnoreCase) >= 0
                || (fault.Detail.Message ?? string.Empty).IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Retrieves one table's metadata, mapping a missing table to <c>not_found</c>. Uses <c>RetrieveEntityRequest</c> (F16).</summary>
        /// <param name="service">The service.</param>
        /// <param name="logicalName">The table.</param>
        /// <param name="filters">What to include.</param>
        /// <returns>The metadata.</returns>
        public static EntityMetadata RetrieveEntity(IOrganizationService service, string logicalName, EntityFilters filters)
        {
            try
            {
                var response = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest { LogicalName = logicalName, EntityFilters = filters });
                return response.EntityMetadata;
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                throw NotFound("table", logicalName);
            }
        }

        /// <summary>Creates a <c>not_found</c> error naming what is missing.</summary>
        /// <param name="kind">What kind of thing — table, column, solution.</param>
        /// <param name="name">Its name.</param>
        /// <returns>The exception.</returns>
        public static ToolException NotFound(string kind, string name) =>
            new ToolException(ToolErrorCodes.NotFound, $"No {kind} named '{name}' exists.", new JObject { ["kind"] = kind, ["name"] = name });

        /// <summary>Whether a name matches a case-insensitive search text.</summary>
        /// <param name="value">The name, possibly <c>null</c>.</param>
        /// <param name="search">The search text.</param>
        /// <returns><c>true</c> when it contains the text.</returns>
        public static bool Contains(string? value, string search) =>
            value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Reads an optional string argument.</summary>
        /// <param name="arguments">The arguments.</param>
        /// <param name="name">The argument.</param>
        /// <returns>The value, or <c>null</c>.</returns>
        public static string? OptionalString(JObject arguments, string name) =>
            arguments[name]?.Type == JTokenType.String ? (string)arguments[name]! : null;

        /// <summary>Reads an optional Boolean argument.</summary>
        /// <param name="arguments">The arguments.</param>
        /// <param name="name">The argument.</param>
        /// <param name="defaultValue">The default.</param>
        /// <returns>The value.</returns>
        public static bool OptionalBool(JObject arguments, string name, bool defaultValue) =>
            arguments[name]?.Type == JTokenType.Boolean ? (bool)arguments[name]! : defaultValue;

        /// <summary>Reads an optional string array argument.</summary>
        /// <param name="arguments">The arguments.</param>
        /// <param name="name">The argument.</param>
        /// <returns>The values, or <c>null</c> when absent.</returns>
        public static string[]? OptionalStrings(JObject arguments, string name) =>
            arguments[name] is JArray array ? array.Values<string>().Select(v => v!).ToArray() : null;
    }
}
