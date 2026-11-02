// <copyright file="ComponentTypes.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Common
{
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// <c>solutioncomponent.componenttype</c> codes and their names (SPEC §6.2 <c>ComponentType</c>). The v1 codes were
    /// confirmed against the option set in spike S2; the others are the platform's documented values.
    /// </summary>
    internal static class ComponentTypes
    {
        /// <summary>Table.</summary>
        public const int Entity = 1;

        /// <summary>Column.</summary>
        public const int Attribute = 2;

        /// <summary>Global option set.</summary>
        public const int OptionSet = 9;

        /// <summary>Relationship.</summary>
        public const int EntityRelationship = 10;

        /// <summary>View.</summary>
        public const int SavedQuery = 26;

        /// <summary>Form.</summary>
        public const int SystemForm = 60;

        /// <summary>Web resource.</summary>
        public const int WebResource = 61;

        /// <summary>Sitemap.</summary>
        public const int SiteMap = 62;

        /// <summary>Model-driven app.</summary>
        public const int AppModule = 80;

        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            [1] = "Entity",
            [2] = "Attribute",
            [3] = "Relationship",
            [9] = "OptionSet",
            [10] = "EntityRelationship",
            [14] = "EntityKey",
            [20] = "Role",
            [24] = "Form",
            [26] = "SavedQuery",
            [29] = "Workflow",
            [31] = "Report",
            [36] = "EmailTemplate",
            [44] = "DuplicateRule",
            [46] = "EntityMap",
            [47] = "AttributeMap",
            [48] = "RibbonCommand",
            [49] = "RibbonContextGroup",
            [50] = "RibbonCustomization",
            [52] = "RibbonRule",
            [53] = "RibbonTabToGroupMap",
            [55] = "RibbonDiff",
            [59] = "SavedQueryVisualization",
            [60] = "SystemForm",
            [61] = "WebResource",
            [62] = "SiteMap",
            [63] = "ConnectionRole",
            [65] = "HierarchyRule",
            [66] = "CustomControl",
            [68] = "CustomControlDefaultConfig",
            [70] = "FieldSecurityProfile",
            [71] = "FieldPermission",
            [80] = "AppModule",
            [90] = "PluginType",
            [91] = "PluginAssembly",
            [92] = "SDKMessageProcessingStep",
            [93] = "SDKMessageProcessingStepImage",
            [95] = "ServiceEndpoint",
            [150] = "RoutingRule",
            [152] = "SLA",
            [154] = "ConvertRule",
            [161] = "MobileOfflineProfile",
            [165] = "SimilarityRule",
            [300] = "CanvasApp",
            [371] = "Connector",
            [372] = "Connector",
            [380] = "EnvironmentVariableDefinition",
            [381] = "EnvironmentVariableValue",
        };

        /// <summary>The name of a component type code.</summary>
        /// <param name="code">The code.</param>
        /// <returns>The name, or <c>ComponentType&lt;code&gt;</c> when it is not known.</returns>
        public static string NameOf(int code) =>
            Names.TryGetValue(code, out var name) ? name : "ComponentType" + code.ToString(CultureInfo.InvariantCulture);

        /// <summary>The <c>ComponentType</c> object of SPEC §6.2.</summary>
        /// <param name="code">The code.</param>
        /// <returns>The object.</returns>
        public static JObject ToJson(int code) => new JObject { ["code"] = code, ["name"] = NameOf(code) };
    }
}
