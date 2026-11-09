// <copyright file="CallLogFile.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The shape of an automatic call log file (SPEC 6a.2): its name, the header and entry JSON, the <c>]}</c> trailer
    /// an entry is appended in front of, and the check that a file on disk is still in that shape. Pure apart from
    /// <see cref="Check"/> and <see cref="EndsWithTrailer"/>, which read a file.
    /// </summary>
    internal static class CallLogFile
    {
        /// <summary>The schema version of the file.</summary>
        public const int SchemaVersion = 1;

        /// <summary>The name part used for host calls, which have no connection.</summary>
        public const string HostName = "host";

        /// <summary>How many characters of the connection ID tell two connections with one name apart.</summary>
        public const int IdPrefixLength = 8;

        /// <summary>The trailer every file ends with: the end of the entries array and of the document, then a newline.</summary>
        public static readonly byte[] Trailer = Encoding.UTF8.GetBytes("\n]}\n");

        private const string EntryIndent = "    ";

        /// <summary>Builds a file name: <c>AgentCompanion-[name]-yyyyMMdd.json</c>, then <c>-2</c>, <c>-3</c>… before the extension.</summary>
        /// <param name="name">The connection name, or <see cref="HostName"/>.</param>
        /// <param name="idPrefix">The first characters of the connection ID when two bound connections share the name; otherwise <c>null</c>.</param>
        /// <param name="localDate">The local date the host received the call.</param>
        /// <param name="sequence">1 for the first file of the day; 2 and up when an earlier file was not in shape.</param>
        /// <returns>A file name safe for Windows.</returns>
        public static string FileName(string name, string? idPrefix, DateTime localDate, int sequence = 1)
        {
            var parts = new StringBuilder("AgentCompanion-").Append(SafeName(name));
            if (!string.IsNullOrEmpty(idPrefix))
            {
                parts.Append('-').Append(SafeName(idPrefix!));
            }

            parts.Append('-').Append(localDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            if (sequence > 1)
            {
                parts.Append('-').Append(sequence.ToString(CultureInfo.InvariantCulture));
            }

            return parts.Append(".json").ToString();
        }

        /// <summary>The first <see cref="IdPrefixLength"/> characters of a connection ID.</summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <returns>The prefix.</returns>
        public static string IdPrefix(string connectionId) =>
            connectionId.Length <= IdPrefixLength ? connectionId : connectionId.Substring(0, IdPrefixLength);

        /// <summary>Formats when the host received a call, local with its offset, to the millisecond.</summary>
        /// <param name="receivedLocal">The local receipt time.</param>
        /// <returns>For example <c>2026-11-08T14:03:05.123+01:00</c>.</returns>
        public static string FormatReceivedAt(DateTimeOffset receivedLocal) =>
            receivedLocal.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

        /// <summary>Composes a new file: the header, its first entry and the trailer, as UTF-8 without a byte order mark.</summary>
        /// <param name="connection">The connection object, or <c>null</c> for the host file.</param>
        /// <param name="localDate">The local date of the file.</param>
        /// <param name="entry">The first entry.</param>
        /// <returns>The bytes to write.</returns>
        public static byte[] NewFile(JObject? connection, DateTime localDate, JObject entry)
        {
            var header = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["connection"] = connection?.DeepClone() ?? JValue.CreateNull(),
                ["date"] = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["entries"] = new JArray(),
            };

            // "entries" is the last property and empty, so the indented text ends with "[]" and the closing brace.
            var text = Serialize(header, Formatting.Indented).Replace("\r\n", "\n");
            var open = text.Substring(0, text.LastIndexOf("[]", StringComparison.Ordinal)) + "[\n";
            return Concat(Encoding.UTF8.GetBytes(open), Encoding.UTF8.GetBytes(EntryText(entry)), Trailer);
        }

        /// <summary>Composes what replaces the trailer when an entry is appended: a comma, the entry, then the trailer again.</summary>
        /// <param name="entry">The entry.</param>
        /// <returns>The bytes to write over the old trailer.</returns>
        public static byte[] Append(JObject entry) =>
            Concat(Encoding.UTF8.GetBytes(",\n"), Encoding.UTF8.GetBytes(EntryText(entry)), Trailer);

        /// <summary>
        /// Serializes JSON, then escapes every Unicode bidirectional-formatting character and the line and paragraph
        /// separators as <c>\uXXXX</c>, so a file opened in a viewer cannot be made to read differently from what it holds
        /// (R8). Those characters only occur inside JSON strings, where the escape is valid. Control characters are
        /// already escaped by the serializer.
        /// </summary>
        /// <param name="token">The JSON.</param>
        /// <param name="formatting">Indented or not.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize(JToken token, Formatting formatting)
        {
            var text = token.ToString(formatting);
            if (!text.Any(IsBidiOrSeparator))
            {
                return text;
            }

            var builder = new StringBuilder(text.Length + 16);
            foreach (var c in text)
            {
                if (IsBidiOrSeparator(c))
                {
                    builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>Tells whether a file still ends with the trailer, by reading its last bytes.</summary>
        /// <param name="stream">An open, seekable stream on the file.</param>
        /// <returns><c>true</c> when the file ends with the trailer.</returns>
        public static bool EndsWithTrailer(Stream stream)
        {
            if (stream.Length < Trailer.Length)
            {
                return false;
            }

            var tail = new byte[Trailer.Length];
            stream.Seek(-Trailer.Length, SeekOrigin.End);
            var read = 0;
            while (read < tail.Length)
            {
                var count = stream.Read(tail, read, tail.Length - read);
                if (count == 0)
                {
                    return false;
                }

                read += count;
            }

            return tail.SequenceEqual(Trailer);
        }

        /// <summary>
        /// Checks a file found on disk before the plugin first appends to it: it ends with the trailer, parses as one
        /// JSON document of this schema, has an entries array, and belongs to the same connection — or to none, for the
        /// host file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="connectionId">The connection ID, or <c>null</c> for the host file.</param>
        /// <returns>What was found.</returns>
        public static FileCheck Check(string path, string? connectionId)
        {
            JObject document;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (!EndsWithTrailer(stream))
                {
                    return FileCheck.NotInShape;
                }

                stream.Position = 0;
                try
                {
                    using (var reader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8)) { DateParseHandling = DateParseHandling.None })
                    {
                        document = JObject.Load(reader);
                        if (reader.Read())
                        {
                            // Something follows the document.
                            return FileCheck.NotInShape;
                        }
                    }
                }
                catch (JsonException)
                {
                    return FileCheck.NotInShape;
                }
            }

            if (document["schemaVersion"]?.Type != JTokenType.Integer || (int)document["schemaVersion"]! != SchemaVersion || !(document["entries"] is JArray))
            {
                return FileCheck.NotInShape;
            }

            var owner = document["connection"];
            if (connectionId == null)
            {
                return owner == null || owner.Type == JTokenType.Null ? FileCheck.InShape : FileCheck.OtherConnection;
            }

            return owner is JObject && string.Equals((string?)owner["connectionId"], connectionId, StringComparison.OrdinalIgnoreCase)
                ? FileCheck.InShape
                : FileCheck.OtherConnection;
        }

        private static string EntryText(JObject entry)
        {
            // JSON strings hold no raw line breaks, so indenting each line cannot change a value.
            var lines = Serialize(entry, Formatting.Indented).Replace("\r\n", "\n").Split('\n');
            return string.Join("\n", lines.Select(l => EntryIndent + l));
        }

        private static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var all = new byte[parts.Sum(p => p.Length)];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, all, offset, part.Length);
                offset += part.Length;
            }

            return all;
        }

        // ALM, LRM, RLM, LRE..RLO, LRI..PDI, and the line and paragraph separators.
        private static bool IsBidiOrSeparator(char c) =>
            c == '\u061C' || c == '\u200E' || c == '\u200F'
            || (c >= '\u202A' && c <= '\u202E')
            || (c >= '\u2066' && c <= '\u2069')
            || c == '\u2028' || c == '\u2029';
    }
}
