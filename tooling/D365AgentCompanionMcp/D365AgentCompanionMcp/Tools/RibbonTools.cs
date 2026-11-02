// <copyright file="RibbonTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The classic ribbon Read tools, <c>get_entity_ribbon</c> and <c>get_application_ribbon</c> (SPEC §6.5). The
    /// platform returns the ribbon zipped; it is decompressed here and paged in chunks (SPEC §6.2). Classic ribbon only (R4).
    /// </summary>
    internal static class RibbonTools
    {
        /// <summary>The <c>get_entity_ribbon</c> tool name.</summary>
        public const string GetEntityRibbon = "get_entity_ribbon";

        /// <summary>The <c>get_application_ribbon</c> tool name.</summary>
        public const string GetApplicationRibbon = "get_application_ribbon";

        /// <summary>
        /// Room left in each page for the result's other fields — <c>logicalName</c>, <c>location</c>,
        /// <c>sizeBytes</c>, <c>nextCursor</c> and the JSON around them.
        /// </summary>
        internal const int EnvelopeAllowance = 1000;

        private const string LogicalName = "logicalName";
        private const string Location = "location";
        private const string RibbonXml = "ribbonXml";

        private static readonly string[] Locations = { "form", "homepageGrid", "subGrid", "all" };

        /// <summary>Defines <c>get_entity_ribbon</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetEntityRibbonTool() => ToolDefinition.Chunked(
            GetEntityRibbon,
            ToolTier.Read,
            "The full classic ribbon of a table, decompressed: RibbonDefinitions, CommandDefinitions and RuleDefinitions — what an export's ribbon diff applies to. Paged in chunks: concatenate ribbonXml from every page in order; the document is complete when nextCursor is null. pageSize is ignored.",
            InputSchema.ForConnection()
                .String(LogicalName, "The table's logical name.", required: true)
                .Enum(Location, "Which ribbons; default all.", Locations, "all"),
            RibbonXml,
            OpenEntityRibbon,
            input => input.WithReason(required: false),
            Fields());

        /// <summary>Defines <c>get_application_ribbon</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetApplicationRibbonTool() => ToolDefinition.Chunked(
            GetApplicationRibbon,
            ToolTier.Read,
            "The full classic application ribbon, decompressed. Paged in chunks: concatenate ribbonXml from every page in order; the document is complete when nextCursor is null. pageSize is ignored.",
            InputSchema.ForConnection(),
            RibbonXml,
            OpenApplicationRibbon,
            input => input.WithReason(required: false),
            Fields());

        /// <summary>Unzips a ribbon the platform returned: the single XML entry, as text.</summary>
        /// <param name="compressed">The zipped ribbon.</param>
        /// <returns>The XML.</returns>
        internal static string Decompress(byte[] compressed)
        {
            using (var archive = new ZipArchive(new MemoryStream(compressed), ZipArchiveMode.Read))
            {
                var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) ?? archive.Entries.First();
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>Pages a ribbon document in chunks that keep each page within the response cap.</summary>
        /// <param name="xml">The document.</param>
        /// <param name="responseCap">The host's response cap, in characters of text content.</param>
        /// <param name="fields">The fields on every page.</param>
        /// <returns>The source.</returns>
        internal static SnapshotPageSource Chunks(string xml, int responseCap, JObject fields)
        {
            fields["sizeBytes"] = Encoding.UTF8.GetByteCount(xml);
            var chunks = TextChunker.Split(xml, Math.Max(1, responseCap - EnvelopeAllowance));
            return new SnapshotPageSource(chunks.Select(c => new JObject { [ToolDefinition.ChunkText] = c }), everyPage: fields);
        }

        private static PageField[] Fields() => new[]
        {
            PageField.Every(LogicalName, OutputSchema.NullableString()),
            PageField.Every(Location, OutputSchema.NullableEnum(Locations)),
            PageField.Every("sizeBytes", OutputSchema.Integer()),
        };

        private static IPageSource OpenEntityRibbon(ToolExecution execution)
        {
            var logicalName = (string)execution.Arguments[LogicalName]!;
            var location = MetadataHelpers.OptionalString(execution.Arguments, Location) ?? "all";

            // Resolve the table first, so a wrong name is not_found rather than a ribbon fault.
            MetadataHelpers.RetrieveEntity(execution.Service, logicalName, EntityFilters.Entity);
            var request = new RetrieveEntityRibbonRequest { EntityName = logicalName, RibbonLocationFilter = Filter(location) };
            var response = ServiceTimeout.With(execution.Service, ServiceTimeout.Ribbon, () => (RetrieveEntityRibbonResponse)execution.Service.Execute(request));

            return Chunks(
                Decompress(response.CompressedEntityXml),
                execution.Host.Settings.Read(s => s.ResponseCapCharacters),
                new JObject { [LogicalName] = logicalName, [Location] = location });
        }

        private static IPageSource OpenApplicationRibbon(ToolExecution execution)
        {
            var response = ServiceTimeout.With(
                execution.Service,
                ServiceTimeout.Ribbon,
                () => (RetrieveApplicationRibbonResponse)execution.Service.Execute(new RetrieveApplicationRibbonRequest()));
            return Chunks(
                Decompress(response.CompressedApplicationRibbonXml),
                execution.Host.Settings.Read(s => s.ResponseCapCharacters),
                new JObject { [LogicalName] = JValue.CreateNull(), [Location] = JValue.CreateNull() });
        }

        private static RibbonLocationFilters Filter(string location)
        {
            switch (location)
            {
                case "form":
                    return RibbonLocationFilters.Form;
                case "homepageGrid":
                    return RibbonLocationFilters.HomepageGrid;
                case "subGrid":
                    return RibbonLocationFilters.SubGrid;
                default:
                    return RibbonLocationFilters.All;
            }
        }
    }
}
