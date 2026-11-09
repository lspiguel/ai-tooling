// <copyright file="DiskCallLog.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Hosting;

    /// <summary>
    /// <b>Write call log to disk</b> (SPEC 6a.2): one setting for the whole host that, while on, writes every call and its
    /// outcome, in full, to one JSON file per connection and day. Only the person turns it on or off, in the Active
    /// Approvals window; no tool reports it or changes it, and its warnings reach only the plugin window and the log
    /// panel. Writes run one at a time on a background task, so a write never fails or delays a call.
    /// </summary>
    internal sealed class DiskCallLog
    {
        private const int MaxSequence = 999;

        private readonly object gate = new object();
        private readonly HostSettingsStore settings;
        private readonly Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private volatile string? activeFolder;
        private Task tail = Task.CompletedTask;
        private bool failing;

        /// <summary>Initializes a new instance of the <see cref="DiskCallLog"/> class, and checks a saved folder.</summary>
        /// <param name="settings">The plugin settings, which keep the checkbox and the folder.</param>
        public DiskCallLog(HostSettingsStore settings)
        {
            this.settings = settings;
            var (on, folder) = settings.Read(s => (s.CallLogToDisk, s.CallLogFolder));
            if (!on)
            {
                return;
            }

            // A saved folder that is missing or inside a Git working tree keeps the log off for this session only:
            // the saved values stay, so the next start tries again.
            var refusal = folder == null ? "no log folder is saved." : Refusal(folder);
            if (refusal == null)
            {
                this.activeFolder = Path.GetFullPath(folder!);
            }
            else
            {
                this.StartupWarning = $"Write call log to disk is off for this session: {refusal} Tick it in Active Approvals to choose a folder.";
                this.LastWarning = this.StartupWarning;
            }
        }

        /// <summary>Raised when the log is turned on or off, or a warning is shown.</summary>
        public event EventHandler? Changed;

        /// <summary>Raised with a warning for the log panel, on the thread that found it.</summary>
        public event EventHandler<string>? Warned;

        /// <summary>Gets a value indicating whether the log is on for this session.</summary>
        public bool IsOn => this.activeFolder != null;

        /// <summary>Gets the folder files are written to while the log is on; otherwise <c>null</c>.</summary>
        public string? ActiveFolder => this.activeFolder;

        /// <summary>Gets the saved folder, shown next to the checkbox and where the folder prompt starts.</summary>
        public string? SavedFolder => this.settings.Read(s => s.CallLogFolder);

        /// <summary>Gets the warning raised at startup when a saved log could not be turned on, or <c>null</c>.</summary>
        public string? StartupWarning { get; private set; }

        /// <summary>Gets the latest warning, for the Active Approvals window, or <c>null</c>.</summary>
        public string? LastWarning { get; private set; }

        /// <summary>
        /// Tells why a folder cannot hold call logs: it does not exist, or it is, or is inside, a Git working tree (R7).
        /// The files hold full arguments and results, so a folder in a repository is refused, not confirmed.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns>The reason, or <c>null</c> when the folder can be used.</returns>
        public static string? Refusal(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return "choose a folder.";
            }

            try
            {
                var full = Path.GetFullPath(folder);
                if (!Directory.Exists(full))
                {
                    return $"the folder {full} does not exist.";
                }

                if (LogFileWriter.IsInsideGitWorkingTree(full))
                {
                    return $"the folder {full} is inside a Git working tree (it or a parent folder contains .git). Call log files hold full arguments and results, which can include client data, so they are never written inside a repository.";
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                return $"{folder} is not a usable folder: {ex.Message}";
            }

            return null;
        }

        /// <summary>Turns the log on, writing to a folder the person chose. A refused folder leaves it off.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns>Why the folder was refused, or <c>null</c> when the log is now on.</returns>
        public string? TurnOn(string folder)
        {
            var refusal = Refusal(folder);
            if (refusal != null)
            {
                return refusal;
            }

            var full = Path.GetFullPath(folder);
            this.settings.Update(s =>
            {
                s.CallLogToDisk = true;
                s.CallLogFolder = full;
            });
            lock (this.gate)
            {
                this.failing = false;
            }

            this.activeFolder = full;
            this.StartupWarning = null;
            this.LastWarning = null;
            this.Changed?.Invoke(this, EventArgs.Empty);
            return null;
        }

        /// <summary>Turns the log off at once. The folder is kept, so the prompt starts there next time. A call already running is still written when it ends.</summary>
        public void TurnOff()
        {
            this.settings.Update(s => s.CallLogToDisk = false);
            this.activeFolder = null;
            this.StartupWarning = null;
            this.LastWarning = null;
            this.Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Queues a call for writing. It is written to the current folder when the log is on, or to the folder that was
        /// active when the call was received, so a call running when the log is turned off is still written.
        /// </summary>
        /// <param name="record">The call.</param>
        /// <param name="folderAtReceipt">The active folder when the call was received, or <c>null</c>.</param>
        public void Write(DiskLogRecord record, string? folderAtReceipt)
        {
            var folder = this.activeFolder ?? folderAtReceipt;
            if (folder == null)
            {
                return;
            }

            lock (this.gate)
            {
                this.tail = this.tail.ContinueWith(_ => this.WriteSafely(record, folder), TaskScheduler.Default);
            }
        }

        /// <summary>Reports a failure found before a record could be queued; treated like a failed write.</summary>
        /// <param name="ex">The failure.</param>
        public void ReportFailure(Exception ex) => this.Fail(ex);

        /// <summary>Waits until every queued write has finished — for tests.</summary>
        /// <returns>A task that completes when the queue is empty.</returns>
        internal Task Flush()
        {
            lock (this.gate)
            {
                return this.tail;
            }
        }

        private static void CreateNew(string path, byte[] content)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true);
            }
        }

        /// <summary>Overwrites the trailer with the entry and the trailer, in one write. Returns <c>false</c>, writing nothing, when the trailer is not there.</summary>
        private static bool AppendTo(string path, byte[] content)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                if (!CallLogFile.EndsWithTrailer(stream))
                {
                    return false;
                }

                stream.Seek(-CallLogFile.Trailer.Length, SeekOrigin.End);
                stream.Write(content, 0, content.Length);
                stream.Flush(true);
                return true;
            }
        }

        private void WriteSafely(DiskLogRecord record, string folder)
        {
            try
            {
                this.WriteCore(record, folder);
                lock (this.gate)
                {
                    this.failing = false;
                }
            }
            catch (Exception ex)
            {
                this.Fail(ex);
            }
        }

        private void WriteCore(DiskLogRecord record, string folder)
        {
            // The folder is checked again each time a file is opened (R7).
            var refusal = Refusal(folder);
            if (refusal != null)
            {
                throw new IOException("nothing was written because " + refusal);
            }

            var date = record.ReceivedLocal.Date;
            var key = string.Join("|", folder, record.ConnectionId ?? CallLogFile.HostName, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            if (this.files.TryGetValue(key, out var known))
            {
                if (AppendTo(known, CallLogFile.Append(record.Entry)))
                {
                    return;
                }

                // Changed since the last write: left as it is, and the next file of the day starts.
                this.Reject(known);
                this.files.Remove(key);
            }

            var idPrefix = record.SharedName && record.ConnectionId != null ? CallLogFile.IdPrefix(record.ConnectionId) : null;
            for (var sequence = 1; sequence <= MaxSequence; sequence++)
            {
                var path = Path.Combine(folder, CallLogFile.FileName(record.Name, idPrefix, date, sequence));
                if (this.rejected.Contains(path))
                {
                    continue;
                }

                if (!File.Exists(path))
                {
                    CreateNew(path, CallLogFile.NewFile(record.Connection, date, record.Entry));
                    this.files[key] = path;
                    return;
                }

                switch (CallLogFile.Check(path, record.ConnectionId))
                {
                    case FileCheck.InShape:
                        if (AppendTo(path, CallLogFile.Append(record.Entry)))
                        {
                            this.files[key] = path;
                            return;
                        }

                        this.Reject(path);
                        break;
                    case FileCheck.OtherConnection:
                        // Another connection of the same name wrote this file, so this one takes the [id8] name.
                        if (idPrefix == null && record.ConnectionId != null)
                        {
                            idPrefix = CallLogFile.IdPrefix(record.ConnectionId);
                            sequence = 0;
                        }

                        break;
                    default:
                        this.Reject(path);
                        break;
                }
            }

            throw new IOException($"no file name was free after {MaxSequence} attempts.");
        }

        private void Reject(string path)
        {
            this.rejected.Add(path);
            this.Warn($"⚠ Call log file {Path.GetFileName(path)} is not in the expected shape and was left untouched; the log continues in the next file of the day.");
        }

        private void Fail(Exception ex)
        {
            lock (this.gate)
            {
                if (this.failing)
                {
                    return;
                }

                this.failing = true;
            }

            this.Warn($"⚠ A call could not be written to the call log on disk: {ex.Message} The call itself was not affected; the log stays on and the next call tries again. Further failures are not reported until a write succeeds.");
        }

        private void Warn(string warning)
        {
            this.LastWarning = warning;
            this.Warned?.Invoke(this, warning);
            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
