// <copyright file="EntityJsonSerializer.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace D365ContextExporter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using D365ContextExporter.Models;

    using Microsoft.Xrm.Sdk;

    /// <summary>Converts Dataverse <see cref="Entity"/> objects into plain CLR dictionaries safe for JSON serialisation.</summary>
    internal static class EntityJsonSerializer
    {
        /// <summary>Serialises a collection of entities, applying the deny list from <paramref name="output"/>.</summary>
        public static List<Dictionary<string, object?>> SerializeEntities(IEnumerable<Entity> entities, OutputSettings output)
        {
            return entities.Select(e => SerializeEntity(e, output.AttributeDenyList)).ToList();
        }

        /// <summary>Serialises a single entity, filtering attributes that match the deny list.</summary>
        public static Dictionary<string, object?> SerializeEntity(Entity entity, IList<string> denyList)
        {
            var dict = new Dictionary<string, object?> { ["_id"] = entity.Id.ToString() };

            foreach (var attr in entity.Attributes)
            {
                if (IsDenied(attr.Key, denyList))
                {
                    continue;
                }

                dict[attr.Key] = ConvertValue(attr.Value);
            }

            return dict;
        }

        private static bool IsDenied(string key, IList<string> denyList)
        {
            return denyList.Any(denied => key.IndexOf(denied, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static object? ConvertValue(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case EntityReference er:
                    return new Dictionary<string, object?> { ["id"] = er.Id.ToString(), ["logicalName"] = er.LogicalName, ["name"] = er.Name };
                case OptionSetValue osv:
                    return osv.Value;
                case OptionSetValueCollection osvc:
                    return osvc.Select(o => o.Value).ToList();
                case Money m:
                    return m.Value;
                case AliasedValue av:
                    return ConvertValue(av.Value);
                case EntityCollection ec:
                    return ec.Entities.Select(e => SerializeEntity(e, Array.Empty<string>())).ToList();
                case DateTime dt:
                    return dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                case bool b:
                    return b;
                case int i:
                    return i;
                case long l:
                    return l;
                case double d:
                    return d;
                case decimal dec:
                    return dec;
                case Guid g:
                    return g.ToString();
                case string s:
                    return s;
                default:
                    return value.ToString();
            }
        }
    }
}
