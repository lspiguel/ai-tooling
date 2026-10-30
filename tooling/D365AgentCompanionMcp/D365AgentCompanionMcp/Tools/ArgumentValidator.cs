// <copyright file="ArgumentValidator.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>Checks a call's arguments against its input schema; any problem is <c>invalid_argument</c> (SPEC §6.2).</summary>
    internal static class ArgumentValidator
    {
        /// <summary>The message for a missing or blank reason on a Write or Publisher tool (R12).</summary>
        public const string ReasonRequired = "reason is required for Write and Publisher tools.";

        /// <summary>Checks the reason of a Write or Publisher call. Runs before every other check and before any dialog (R12).</summary>
        /// <param name="arguments">The arguments.</param>
        public static void RequireReason(JObject? arguments)
        {
            var reason = arguments?[InputSchema.Reason];
            if (reason == null || reason.Type != JTokenType.String || string.IsNullOrWhiteSpace((string?)reason))
            {
                throw ToolException.InvalidArgument(InputSchema.Reason, ReasonRequired);
            }
        }

        /// <summary>Checks the arguments against the schema subset the tools use.</summary>
        /// <param name="tool">The tool.</param>
        /// <param name="arguments">The arguments, or <c>null</c> when they were not an object.</param>
        public static void Validate(ToolDefinition tool, JObject? arguments)
        {
            if (arguments == null)
            {
                throw ToolException.InvalidArgument("arguments", "arguments must be a JSON object.");
            }

            var properties = (JObject)tool.InputSchema["properties"]!;
            foreach (var argument in arguments.Properties())
            {
                if (!(properties[argument.Name] is JObject schema))
                {
                    throw ToolException.InvalidArgument(argument.Name, $"{argument.Name} is not an argument of {tool.Name}.");
                }

                Check(argument.Name, argument.Value, schema);
            }

            foreach (var name in tool.InputSchema["required"]!.Values<string>())
            {
                if (arguments[name!] == null || arguments[name!]!.Type == JTokenType.Null)
                {
                    var message = name == InputSchema.Connection
                        ? "connection is required: pass a connectionId from list_connections. There is no default connection."
                        : $"{name} is required.";
                    throw ToolException.InvalidArgument(name!, message);
                }
            }
        }

        private static void Check(string name, JToken value, JObject schema)
        {
            if (value.Type == JTokenType.Null)
            {
                return;
            }

            switch ((string?)schema["type"])
            {
                case "string":
                    if (value.Type != JTokenType.String)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be a string.");
                    }

                    var text = (string)value!;
                    if (schema["minLength"] != null && text.Length < (int)schema["minLength"]!)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must not be empty.");
                    }

                    if (schema["maxLength"] != null && text.Length > (int)schema["maxLength"]!)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be at most {(int)schema["maxLength"]!} characters.");
                    }

                    if (schema["enum"] is JArray allowed && !allowed.Values<string>().Contains(text))
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be one of: {string.Join(", ", allowed.Values<string>())}.");
                    }

                    break;
                case "integer":
                    if (value.Type != JTokenType.Integer)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be an integer.");
                    }

                    if (schema["minimum"] != null && (long)value < (long)schema["minimum"]!)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be at least {(long)schema["minimum"]!}.");
                    }

                    break;
                case "boolean":
                    if (value.Type != JTokenType.Boolean)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be true or false.");
                    }

                    break;
            }
        }
    }
}
