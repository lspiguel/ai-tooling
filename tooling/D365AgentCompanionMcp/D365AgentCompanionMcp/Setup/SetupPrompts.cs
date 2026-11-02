// <copyright file="SetupPrompts.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Setup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Composes the setup prompts from their embedded text resources (SPEC §8.7), so their wording can be revised
    /// without code. A placeholder is written <c>{{NAME}}</c>; an unknown one is left as it is, which the tests catch.
    /// </summary>
    internal static class SetupPrompts
    {
        /// <summary>Stands in for the organization URL when the tab is not bound.</summary>
        public const string NoOrganization = "none yet — this tab is not connected; ask me for the environment URLs";

        /// <summary>Stands in for the export folder before the skills are exported from the page.</summary>
        public const string NoSkillsFolder = "not exported from this page yet — ask me for the folder that holds the zips";

        private const string ResourcePrefix = "Setup.Prompts.";

        private static readonly Regex Placeholder = new Regex(@"\{\{([A-Z_]+)\}\}", RegexOptions.CultureInvariant);

        /// <summary>Tells whether a prompt holds the bearer token, so its page can say so.</summary>
        /// <param name="prompt">The prompt.</param>
        /// <returns><c>true</c> for the two MCP prompts.</returns>
        public static bool HoldsToken(SetupPrompt prompt) => prompt == SetupPrompt.McpRegister || prompt == SetupPrompt.McpUpdate;

        /// <summary>Builds a prompt's embedded resource name.</summary>
        /// <param name="prompt">The prompt.</param>
        /// <returns>The resource name, such as <c>Setup.Prompts.PacCli.txt</c>.</returns>
        public static string ResourceName(SetupPrompt prompt) => $"{ResourcePrefix}{prompt}.txt";

        /// <summary>Reads a prompt's template, unfilled.</summary>
        /// <param name="prompt">The prompt.</param>
        /// <returns>The template text.</returns>
        public static string Template(SetupPrompt prompt)
        {
            var name = ResourceName(prompt);
            using (var stream = typeof(SetupPrompts).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"The setup prompt resource {name} is missing from the plugin."))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Composes a prompt, ready for the clipboard.</summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="values">The values to fill in.</param>
        /// <returns>The prompt text.</returns>
        public static string Compose(SetupPrompt prompt, PromptValues values)
        {
            var fills = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PLUGIN_VERSION"] = values.PluginVersion,
                ["PREVIOUS_VERSION"] = values.PreviousVersion ?? "an earlier version",
                ["ENDPOINT"] = values.Endpoint,
                ["CLIENT_CONFIG"] = values.ClientConfig,
                ["ORGANIZATION_URL"] = string.IsNullOrWhiteSpace(values.OrganizationUrl) ? NoOrganization : values.OrganizationUrl!,
                ["SKILLS_FOLDER"] = string.IsNullOrWhiteSpace(values.SkillsFolder) ? NoSkillsFolder : values.SkillsFolder!,
                ["SKILL_NAMES"] = string.Join(", ", values.SkillNames),
            };

            return Placeholder.Replace(Template(prompt), m => fills.TryGetValue(m.Groups[1].Value, out var fill) ? fill : m.Value);
        }
    }
}
