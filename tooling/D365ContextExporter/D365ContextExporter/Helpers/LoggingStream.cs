// <copyright file="LoggingStream.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Helpers
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>Write-only stream that buffers UTF-8 bytes from IronPython stdout/stderr and fires a log delegate per complete line.</summary>
    internal sealed class LoggingStream : Stream
    {
        private readonly Action<string> log;
        private readonly StringBuilder buffer = new StringBuilder();

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingStream"/> class.
        /// </summary>
        /// <param name="log">Delegate called for each complete line (without trailing newline).</param>
        public LoggingStream(Action<string> log)
        {
            this.log = log;
        }

        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(buffer, offset, count);
            this.buffer.Append(text);
            this.FlushLines(final: false);
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            this.FlushLines(final: true);
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        private void FlushLines(bool final)
        {
            var text = this.buffer.ToString();
            var start = 0;

            while (start < text.Length)
            {
                var nl = text.IndexOf('\n', start);
                if (nl < 0)
                {
                    break;
                }

                var line = text.Substring(start, nl - start).TrimEnd('\r');
                this.log(line);
                start = nl + 1;
            }

            this.buffer.Clear();
            if (start < text.Length)
            {
                this.buffer.Append(text.Substring(start));
            }

            if (final && this.buffer.Length > 0)
            {
                this.log(this.buffer.ToString());
                this.buffer.Clear();
            }
        }
    }
}
