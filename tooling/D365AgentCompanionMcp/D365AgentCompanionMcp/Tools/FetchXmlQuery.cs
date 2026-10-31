// <copyright file="FetchXmlQuery.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// A FetchXML query checked against the rules of SPEC §6.5 <c>query</c>: no <c>top</c> (F15), no
    /// <c>&lt;all-attributes /&gt;</c>, and record detection against the configuration allow-list.
    /// </summary>
    internal sealed class FetchXmlQuery
    {
        /// <summary>The tables a query may read without touching records (SPEC §6.5, configuration allow-list v1).</summary>
        public static readonly IReadOnlyCollection<string> ConfigurationTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "publisher",
            "solution",
            "solutioncomponent",
            "systemform",
            "savedquery",
            "sitemap",
            "appmodule",
            "appmodulecomponent",
            "webresource",
            "ribboncustomization",
            "organization",
            "workflow",
            "customcontrol",
            "environmentvariabledefinition",
            "connectionreference",
            "pluginassembly",
            "sdkmessageprocessingstep",
            "customapi",
            "role",
            "fieldsecurityprofile",
        };

        private const string Argument = "fetchXml";

        private FetchXmlQuery(XDocument document, string entityName, IReadOnlyList<string> tables)
        {
            this.Document = document;
            this.EntityName = entityName;
            this.Tables = tables;
        }

        /// <summary>Gets the query, with any caller-supplied paging attributes removed.</summary>
        public XDocument Document { get; }

        /// <summary>Gets the root entity.</summary>
        public string EntityName { get; }

        /// <summary>Gets every table the query reads — the root entity and each link-entity.</summary>
        public IReadOnlyList<string> Tables { get; }

        /// <summary>Gets the tables outside the configuration allow-list.</summary>
        public IReadOnlyList<string> RecordTables => this.Tables.Where(t => !ConfigurationTables.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>Gets a value indicating whether the query touches records.</summary>
        public bool TouchesRecords => this.RecordTables.Count > 0;

        /// <summary>Parses and checks a query; any problem is <c>invalid_argument</c> naming <c>fetchXml</c>.</summary>
        /// <param name="fetchXml">The FetchXML.</param>
        /// <returns>The query.</returns>
        public static FetchXmlQuery Parse(string fetchXml)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(fetchXml);
            }
            catch (XmlException ex)
            {
                throw ToolException.InvalidArgument(Argument, $"fetchXml is not well-formed XML: {ex.Message}");
            }

            var fetch = document.Root!;
            if (fetch.Name.LocalName != "fetch")
            {
                throw ToolException.InvalidArgument(Argument, "fetchXml must have <fetch> as its root element.");
            }

            if (fetch.Attribute("top") != null)
            {
                throw ToolException.InvalidArgument(Argument, "top is not accepted: Dataverse refuses top together with paging. Use pageSize, and stop reading pages when you have enough.");
            }

            if (document.Descendants().Any(e => e.Name.LocalName == "all-attributes"))
            {
                throw ToolException.InvalidArgument(Argument, "<all-attributes /> is not accepted. Name the columns you need with <attribute name=\"...\" />.");
            }

            var entities = fetch.Elements().Where(e => e.Name.LocalName == "entity").ToList();
            if (entities.Count != 1 || string.IsNullOrWhiteSpace((string?)entities[0].Attribute("name")))
            {
                throw ToolException.InvalidArgument(Argument, "fetchXml must have exactly one <entity name=\"...\"> under <fetch>.");
            }

            var links = document.Descendants().Where(e => e.Name.LocalName == "link-entity").ToList();
            if (links.Any(l => string.IsNullOrWhiteSpace((string?)l.Attribute("name"))))
            {
                throw ToolException.InvalidArgument(Argument, "Every <link-entity> must name its table.");
            }

            // The server sets paging; whatever the caller supplied is ignored.
            fetch.Attribute("page")?.Remove();
            fetch.Attribute("count")?.Remove();
            fetch.Attribute("paging-cookie")?.Remove();

            var entityName = TableName(entities[0]);
            var tables = new[] { entityName }.Concat(links.Select(TableName)).ToList();
            return new FetchXmlQuery(document, entityName, tables);
        }

        private static string TableName(XElement element) => (element.Attribute("name")?.Value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
