using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes
{
    /// <summary>
    /// Validates a value against the JSON Schema subset the tool schemas use: type (including type arrays), properties,
    /// required, items, enum, and the uuid and date-time formats. Returns the problems found.
    /// </summary>
    internal static class SchemaValidator
    {
        private static readonly Regex Uuid = new Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
        private static readonly Regex DateTimeUtc = new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");

        public static List<string> Validate(JToken value, JObject schema, string path = "$")
        {
            var problems = new List<string>();
            Check(value, schema, path, problems);
            return problems;
        }

        private static void Check(JToken value, JObject schema, string path, List<string> problems)
        {
            var types = schema["type"] is JArray array ? array.Values<string>().ToList() : new List<string?> { (string?)schema["type"] };
            if (!types.Any(t => Matches(value, t)))
            {
                problems.Add($"{path}: {value.Type} is not {string.Join("|", types)}");
                return;
            }

            if (schema["enum"] is JArray allowed && !allowed.Any(a => JToken.DeepEquals(a, value)))
            {
                problems.Add($"{path}: {value} is not in the enum");
            }

            if (value.Type == JTokenType.String && (string?)schema["format"] == "uuid" && !Uuid.IsMatch((string)value!))
            {
                problems.Add($"{path}: {value} is not a lowercase uuid");
            }

            if (value.Type == JTokenType.String && (string?)schema["format"] == "date-time" && !DateTimeUtc.IsMatch((string)value!))
            {
                problems.Add($"{path}: {value} is not a UTC date-time");
            }

            if (value is JObject obj)
            {
                foreach (var name in (schema["required"] as JArray)?.Values<string>() ?? Enumerable.Empty<string>())
                {
                    if (obj[name!] == null)
                    {
                        problems.Add($"{path}.{name}: missing");
                    }
                }

                if (schema["properties"] is JObject properties)
                {
                    foreach (var property in obj.Properties())
                    {
                        if (properties[property.Name] is JObject propertySchema)
                        {
                            Check(property.Value, propertySchema, $"{path}.{property.Name}", problems);
                        }
                    }
                }
            }

            if (value is JArray items && schema["items"] is JObject itemSchema)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    Check(items[i], itemSchema, $"{path}[{i}]", problems);
                }
            }
        }

        private static bool Matches(JToken value, string? type) => type switch
        {
            null => true,
            "object" => value.Type == JTokenType.Object,
            "array" => value.Type == JTokenType.Array,
            "string" => value.Type == JTokenType.String,
            "integer" => value.Type == JTokenType.Integer,
            "number" => value.Type == JTokenType.Integer || value.Type == JTokenType.Float,
            "boolean" => value.Type == JTokenType.Boolean,
            "null" => value.Type == JTokenType.Null,
            _ => false,
        };
    }
}
