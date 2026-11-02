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

            // A continuation page needs only connection and cursor: other arguments are ignored (SPEC §6.2 Paging).
            var continuation = tool.IsPaged && arguments[InputSchema.Cursor]?.Type == JTokenType.String;
            foreach (var name in tool.InputSchema["required"]!.Values<string>())
            {
                if (continuation && name != InputSchema.Connection)
                {
                    continue;
                }

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

                    if (schema["pattern"] != null && !System.Text.RegularExpressions.Regex.IsMatch(text, (string)schema["pattern"]!))
                    {
                        throw ToolException.InvalidArgument(name, $"{name} does not have the required form. {(string?)schema["description"]}");
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

                    if (schema["maximum"] != null && (long)value > (long)schema["maximum"]!)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be at most {(long)schema["maximum"]!}.");
                    }

                    break;
                case "boolean":
                    if (value.Type != JTokenType.Boolean)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be true or false.");
                    }

                    break;
                case "object":
                    if (value.Type != JTokenType.Object)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be an object.");
                    }

                    break;
                case "array":
                    if (!(value is JArray array))
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must be an array.");
                    }

                    if (schema["minItems"] != null && array.Count < (int)schema["minItems"]!)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must have at least {(int)schema["minItems"]!} entries.");
                    }

                    if (schema["maxItems"] != null && array.Count > (int)schema["maxItems"]!)
                    {
                        throw ToolException.InvalidArgument(name, $"{name} must have at most {(int)schema["maxItems"]!} entries.");
                    }

                    if (schema["items"] is JObject itemSchema)
                    {
                        for (var i = 0; i < array.Count; i++)
                        {
                            if (array[i].Type == JTokenType.Null)
                            {
                                throw ToolException.InvalidArgument(name, $"{name}[{i}] must not be null.");
                            }

                            Check($"{name}[{i}]", array[i], itemSchema);
                        }
                    }

                    break;
            }
        }
    }
}
