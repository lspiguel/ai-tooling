// <copyright file="TextChunker.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Paging
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Splits an XML payload into chunks whose JSON-encoded length stays within a limit (SPEC §6.2: XML payloads are
    /// split into chunks of at most the cap). The cap is measured on the text content block, where quotes, backslashes
    /// and control characters are escaped, so the split counts the escaped length. Surrogate pairs are never split.
    /// </summary>
    internal static class TextChunker
    {
        /// <summary>Splits the text.</summary>
        /// <param name="text">The text.</param>
        /// <param name="maxEncodedLength">The longest a chunk may be once JSON-encoded, without its quotes.</param>
        /// <returns>The chunks, in order; one empty chunk for empty text.</returns>
        public static IReadOnlyList<string> Split(string text, int maxEncodedLength)
        {
            var chunks = new List<string>();
            var current = new StringBuilder();
            var length = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var pair = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                var width = pair ? 2 : EncodedLength(text[i]);
                if (length + width > maxEncodedLength && current.Length > 0)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                    length = 0;
                }

                current.Append(text[i]);
                if (pair)
                {
                    current.Append(text[++i]);
                }

                length += width;
            }

            if (current.Length > 0 || chunks.Count == 0)
            {
                chunks.Add(current.ToString());
            }

            return chunks;
        }

        /// <summary>How long one character is once Newtonsoft encodes it in a JSON string.</summary>
        /// <param name="c">The character.</param>
        /// <returns>Its encoded length.</returns>
        internal static int EncodedLength(char c)
        {
            switch (c)
            {
                case '"':
                case '\\':
                case '\n':
                case '\r':
                case '\t':
                case '\b':
                case '\f':
                    return 2;
                default:
                    return c < ' ' || c == '\u0085' || c == '\u2028' || c == '\u2029' ? 6 : 1;
            }
        }
    }
}
