// <copyright file="TemplateFilters.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Scriban.Runtime;

    /// <summary>C# port of all custom filter functions from <c>filters.py</c>. Registered on the root <see cref="ScriptObject"/> before rendering.</summary>
    internal static class TemplateFilters
    {
        private static readonly IReadOnlyDictionary<int, string> ComponentTypes = new Dictionary<int, string>
        {
            [1] = "Table (Entity)",
            [3] = "Column (Attribute)",
            [5] = "Relationship",
            [20] = "Option Set (Global)",
            [50] = "Role",
            [60] = "Form",
            [90] = "Model-driven App",
            [91] = "Canvas App",
            [92] = "Cloud Flow",
        };

        private static readonly IReadOnlyDictionary<string, string> AttrTypeAbbrevMap = new Dictionary<string, string>
        {
            ["String"] = "str",
            ["Memo"] = "text",
            ["Lookup"] = "lkp",
            ["Picklist"] = "opt",
            ["MultiSelectPicklist"] = "multiopt",
            ["Boolean"] = "bool",
            ["Integer"] = "int",
            ["Decimal"] = "dec",
            ["Money"] = "money",
            ["DateTime"] = "dt",
            ["Image"] = "img",
            ["Uniqueidentifier"] = "pk",
            ["File"] = "file",
            ["Owner"] = "lkp",
            ["Customer"] = "lkp",
            ["EntityName"] = "str",
            ["BigInt"] = "int",
            ["Double"] = "dec",
            ["State"] = "opt",
            ["Status"] = "opt",
            ["Virtual"] = "-",
            ["ManagedProperty"] = "-",
            ["CalendarRules"] = "-",
            ["PartyList"] = "lkp",
        };

        private static readonly IReadOnlyDictionary<int, string> FormTypeShort = new Dictionary<int, string>
        {
            [7] = "quick",
            [8] = "main",
            [11] = "card",
            [106] = "quickCreate",
        };

        private static readonly IReadOnlyDictionary<int, string> PluginStageMap = new Dictionary<int, string>
        {
            [10] = "PreVal",
            [20] = "Pre",
            [40] = "Post",
            [45] = "Post",
        };

        private static readonly IReadOnlyDictionary<int, string> PluginModeMap = new Dictionary<int, string>
        {
            [0] = "Sync",
            [1] = "Async",
        };

        private static readonly IReadOnlyDictionary<int, string> EnvVarTypeMap = new Dictionary<int, string>
        {
            [100000000] = "String",
            [100000001] = "Number",
            [100000002] = "Boolean",
            [100000003] = "JSON",
            [100000004] = "DataSource",
            [100000005] = "Secret",
        };

        private static readonly IReadOnlyDictionary<int, string> PrivDepthMap = new Dictionary<int, string>
        {
            [0] = "None",
            [1] = "User",
            [2] = "Business Unit",
            [4] = "Parent: Child BUs",
            [8] = "Organization",
        };

        private static readonly IReadOnlyDictionary<int, string> ApiParamTypeMap = new Dictionary<int, string>
        {
            [0] = "bool",
            [1] = "dt",
            [2] = "dec",
            [3] = "Entity",
            [4] = "EntityCollection",
            [5] = "EntityReference",
            [6] = "float",
            [7] = "int",
            [8] = "money",
            [9] = "opt",
            [10] = "str",
            [11] = "str[]",
            [12] = "guid",
        };

        private static readonly HashSet<int> SkipViewTypes = new HashSet<int>
        {
            16, 32, 64, 128, 256, 512, 1024, 4096, 16384,
        };

        /// <summary>Registers all custom filters as named delegates on <paramref name="target"/>.</summary>
        public static void RegisterAll(ScriptObject target)
        {
            target.Import("component_type_name", new Func<object, string>(ComponentTypeName));
            target.Import("schemaname_to_title", new Func<string, string>(SchemaNameToTitle));
            target.Import("markdown_table", new Func<ScriptArray, ScriptArray, string>(MarkdownTable));
            target.Import("csv_list", new Func<ScriptArray, string, string>(CsvList));
            target.Import("optionset_label", new Func<ScriptArray, object, string>(OptionsetLabel));
            target.Import("iso_date", new Func<string, string>(IsoDate));
            target.Import("attr_type_abbrev", new Func<object, string>(AttrTypeAbbrev));
            target.Import("req_indicator", new Func<object, string>(ReqIndicator));
            target.Import("format_forms", new Func<ScriptArray, string>(FormatForms));
            target.Import("format_views", new Func<ScriptArray, string>(FormatViews));
            target.Import("entity_forms", new Func<ScriptArray, string, ScriptArray>(FilterFormsByEntity));
            target.Import("entity_views", new Func<ScriptArray, string, ScriptArray>(FilterViewsByEntity));
            target.Import("plugin_stage", new Func<object, string>(PluginStage));
            target.Import("plugin_mode", new Func<object, string>(PluginMode));
            target.Import("flow_trigger", new Func<string, string>(FlowTrigger));
            target.Import("classic_trigger", new Func<ScriptObject, string>(ClassicTrigger));
            target.Import("envvar_type", new Func<object, string>(EnvvarType));
            target.Import("api_param_type", new Func<object, string>(ApiParamType));
            target.Import("priv_depth", new Func<object, string>(PrivDepth));
            target.Import("pluck", new Func<ScriptArray, string, ScriptArray>(Pluck));
            target.Import("group_by_key", new Func<ScriptArray, string, ScriptArray>(GroupByKey));
            target.Import("display_label", new Func<object, string, string>(DisplayLabel));
        }

        private static string ComponentTypeName(object value)
        {
            var key = ToInt(value);
            return ComponentTypes.TryGetValue(key, out var name) ? name : $"Unknown ({key})";
        }

        private static string SchemaNameToTitle(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var spaced = Regex.Replace(value, @"([A-Z])", " $1");
            var words = Regex.Split(spaced, @"[_\s]+")
                             .Where(w => !string.IsNullOrEmpty(w))
                             .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
            return string.Join(" ", words);
        }

        private static string MarkdownTable(ScriptArray rows, ScriptArray columns)
        {
            if (rows == null || rows.Count == 0)
            {
                return "_No data._";
            }

            var cols = columns?.Select(c => c?.ToString() ?? string.Empty).ToList()
                       ?? new List<string>();
            var header = "| " + string.Join(" | ", cols) + " |";
            var separator = "| " + string.Join(" | ", cols.Select(_ => "---")) + " |";

            var lines = new List<string> { header, separator };
            foreach (var row in rows)
            {
                if (row is ScriptObject obj)
                {
                    var cells = cols.Select(col =>
                    {
                        var v = GetDictValue(obj, col);
                        return v?.ToString() ?? string.Empty;
                    });
                    lines.Add("| " + string.Join(" | ", cells) + " |");
                }
                else
                {
                    lines.Add("| " + string.Join(" | ", cols.Select(_ => string.Empty)) + " |");
                }
            }

            return string.Join("\n", lines);
        }

        private static string CsvList(ScriptArray items, string attr)
        {
            if (items == null || items.Count == 0)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(attr))
            {
                return string.Join(", ", items.Select(item =>
                {
                    if (item is ScriptObject obj)
                    {
                        var v = GetDictValue(obj, attr);
                        return v?.ToString() ?? string.Empty;
                    }

                    return item?.ToString() ?? string.Empty;
                }));
            }

            return string.Join(", ", items.Select(item => item?.ToString() ?? string.Empty));
        }

        private static string OptionsetLabel(ScriptArray options, object value)
        {
            var intValue = ToInt(value);
            foreach (var opt in options ?? new ScriptArray())
            {
                if (opt is ScriptObject obj && ToInt(GetDictValue(obj, "Value")) == intValue)
                {
                    var labelVal = GetDictValue(obj, "Label");
                    if (labelVal is ScriptObject labelObj)
                    {
                        var uslVal = GetDictValue(labelObj, "UserLocalizedLabel");
                        if (uslVal is ScriptObject uslObj)
                        {
                            var label = GetDictValue(uslObj, "Label");
                            return label?.ToString() ?? intValue.ToString(CultureInfo.InvariantCulture);
                        }
                    }

                    return intValue.ToString(CultureInfo.InvariantCulture);
                }
            }

            return intValue.ToString(CultureInfo.InvariantCulture);
        }

        private static string IsoDate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return value.Length >= 10 ? value.Substring(0, 10) : value;
        }

        private static string AttrTypeAbbrev(object value)
        {
            if (value == null)
            {
                return "-";
            }

            var key = value.ToString()!;
            if (string.IsNullOrEmpty(key))
            {
                return "-";
            }

            return AttrTypeAbbrevMap.TryGetValue(key, out var abbrev) ? abbrev : key;
        }

        private static string ReqIndicator(object value)
        {
            string? strVal;
            if (value is ScriptObject obj)
                strVal = GetDictValue(obj, "Value")?.ToString();
            else
                strVal = value?.ToString();

            return strVal switch
            {
                "Required" => "**R**",
                "Recommended" => "r",
                _ => "-",
            };
        }

        private static ScriptArray FilterFormsByEntity(ScriptArray forms, string entityLogicalName)
        {
            var result = new ScriptArray();
            foreach (var form in forms ?? new ScriptArray())
            {
                if (form is ScriptObject obj)
                {
                    var code = GetDictValue(obj, "objecttypecode")?.ToString();
                    if (string.Equals(code, entityLogicalName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(form);
                    }
                }
            }

            return result;
        }

        private static ScriptArray FilterViewsByEntity(ScriptArray views, string entityLogicalName)
        {
            var result = new ScriptArray();
            foreach (var view in views ?? new ScriptArray())
            {
                if (view is ScriptObject obj)
                {
                    var code = GetDictValue(obj, "returnedtypecode")?.ToString();
                    if (string.Equals(code, entityLogicalName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(view);
                    }
                }
            }

            return result;
        }

        private static string FormatForms(ScriptArray forms)
        {
            var groups = new Dictionary<string, List<string>>();
            var order = new List<string>();

            foreach (var form in forms ?? new ScriptArray())
            {
                if (form is not ScriptObject obj)
                {
                    continue;
                }

                var name = GetDictValue(obj, "Name")?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var ftypeRaw = GetDictValue(obj, "Type");
                var ftype = ftypeRaw != null ? ToInt(ftypeRaw) : 0;
                var shortType = FormTypeShort.TryGetValue(ftype, out var s) ? s : ftype.ToString(CultureInfo.InvariantCulture);

                if (!groups.ContainsKey(name))
                {
                    groups[name] = new List<string>();
                    order.Add(name);
                }

                if (!groups[name].Contains(shortType))
                {
                    groups[name].Add(shortType);
                }
            }

            if (order.Count == 0)
            {
                return "—";
            }

            return string.Join(", ", order.Select(name => $"{name}({string.Join(",", groups[name])})"));
        }

        private static string FormatViews(ScriptArray views)
        {
            var names = new List<string>();
            foreach (var view in views ?? new ScriptArray())
            {
                if (view is not ScriptObject obj)
                {
                    continue;
                }

                var name = GetDictValue(obj, "Name")?.ToString() ?? string.Empty;
                var qtRaw = GetDictValue(obj, "QueryType");
                int qtInt;
                try
                {
                    qtInt = qtRaw != null ? ToInt(qtRaw) : -1;
                }
                catch
                {
                    qtInt = -1;
                }

                if (!string.IsNullOrEmpty(name) && !SkipViewTypes.Contains(qtInt) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }

            return names.Count > 0 ? string.Join(", ", names) : "—";
        }

        private static string PluginStage(object value)
        {
            if (value == null)
            {
                return "—";
            }

            try
            {
                var key = ToInt(value);
                return PluginStageMap.TryGetValue(key, out var s) ? s : value.ToString()!;
            }
            catch
            {
                return value.ToString()!;
            }
        }

        private static string PluginMode(object value)
        {
            if (value == null)
            {
                return "—";
            }

            try
            {
                var key = ToInt(value);
                return PluginModeMap.TryGetValue(key, out var s) ? s : value.ToString()!;
            }
            catch
            {
                return value.ToString()!;
            }
        }

        private static string FlowTrigger(string name)
        {
            var nl = (name ?? string.Empty).ToLowerInvariant();

            if (nl.Contains("http"))
            {
                return "HTTP";
            }

            var schedKeywords = new[] { "sched", "every 1 day", "every weekend", "daily", "weekly", "monthly", "annual", "dec 31", "every 1 week" };
            if (schedKeywords.Any(k => nl.Contains(k)))
            {
                return "Sched";
            }

            if (nl.Contains("child") || nl.Contains("childprocess"))
            {
                return "Child";
            }

            var manualKeywords = new[] { "on demand", "ondemand", "manual", "manuallybatch", "on-demand" };
            if (manualKeywords.Any(k => nl.Contains(k)))
            {
                return "Manual";
            }

            return "Auto";
        }

        private static string ClassicTrigger(ScriptObject wf)
        {
            var triggers = new List<string>();
            if (IsTruthy(GetDictValue(wf, "triggeroncreate")))
            {
                triggers.Add("Create");
            }

            if (IsTruthy(GetDictValue(wf, "triggeronupdateattributelist")))
            {
                triggers.Add("Update");
            }

            if (IsTruthy(GetDictValue(wf, "triggerondelete")))
            {
                triggers.Add("Delete");
            }

            if (triggers.Count == 0)
            {
                triggers.Add("Manual");
            }

            return string.Join("/", triggers);
        }

        private static string EnvvarType(object value)
        {
            if (value == null)
            {
                return "—";
            }

            try
            {
                var key = ToInt(value);
                return EnvVarTypeMap.TryGetValue(key, out var s) ? s : value.ToString()!;
            }
            catch
            {
                return value.ToString()!;
            }
        }

        private static string PrivDepth(object value)
        {
            if (value == null)
            {
                return "—";
            }

            try
            {
                var key = ToInt(value);
                return PrivDepthMap.TryGetValue(key, out var s) ? s : value.ToString();
            }
            catch
            {
                return value.ToString()!;
            }
        }

        private static string ApiParamType(object value)
        {
            if (value == null)
            {
                return "?";
            }

            try
            {
                var key = ToInt(value);
                return ApiParamTypeMap.TryGetValue(key, out var s) ? s : value.ToString()!;
            }
            catch
            {
                return value.ToString()!;
            }
        }

        private static ScriptArray Pluck(ScriptArray items, string key)
        {
            var result = new ScriptArray();
            var seen = new HashSet<object>();

            foreach (var item in items ?? new ScriptArray())
            {
                if (item is ScriptObject obj)
                {
                    var val = GetDictValue(obj, key);
                    if (val != null && seen.Add(val))
                    {
                        result.Add(val);
                    }
                }
            }

            return result;
        }

        private static ScriptArray GroupByKey(ScriptArray items, string key)
        {
            var order = new List<object?>();
            var groupArrays = new List<ScriptArray>();

            foreach (var item in items ?? new ScriptArray())
            {
                object? val = null;
                if (item is ScriptObject obj)
                {
                    val = GetDictValue(obj, key);
                }

                var idx = order.FindIndex(k => Equals(k, val));
                if (idx < 0)
                {
                    idx = order.Count;
                    order.Add(val);
                    groupArrays.Add(new ScriptArray());
                }

                groupArrays[idx].Add(item);
            }

            var result = new ScriptArray();
            for (var i = 0; i < order.Count; i++)
            {
                var group = new ScriptObject();
                group["key"] = order[i];
                group["items"] = groupArrays[i];
                result.Add(group);
            }

            return result;
        }

        private static string DisplayLabel(object labelObj, string fallback)
        {
            var fb = fallback ?? "—";

            if (labelObj == null)
            {
                return fb;
            }

            if (labelObj is ScriptObject obj)
            {
                var uslVal = GetDictValue(obj, "UserLocalizedLabel");
                if (uslVal is ScriptObject uslObj)
                {
                    var label = GetDictValue(uslObj, "Label")?.ToString();
                    return !string.IsNullOrEmpty(label) ? label! : fb;
                }
            }

            return fb;
        }

        private static object? GetDictValue(ScriptObject? obj, string key)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj.ContainsKey(key))
            {
                return obj[key];
            }

            var lower = key.ToLowerInvariant();
            return lower != key && obj.ContainsKey(lower) ? obj[lower] : null;
        }

        private static int ToInt(object? value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static bool IsTruthy(object? value)
        {
            return value switch
            {
                null => false,
                bool b => b,
                long l => l != 0,
                int i => i != 0,
                double d => d != 0,
                string s => !string.IsNullOrEmpty(s) && s != "0" && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase),
                _ => true,
            };
        }
    }
}
