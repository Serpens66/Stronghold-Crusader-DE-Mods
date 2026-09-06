using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Shared
{
    internal sealed class CrashBreadcrumbRecorder : IDisposable
    {
        private const int RingCapacity = 256;
        private const int RetainedSessions = 3;
        private readonly object syncRoot = new object();
        private readonly object snapshotWriteRoot = new object();
        private readonly BreadcrumbRecord[] ring = new BreadcrumbRecord[RingCapacity];
        private readonly Dictionary<int, BreadcrumbRecord> activeByThread =
            new Dictionary<int, BreadcrumbRecord>();
        private readonly Dictionary<string, CounterState> counters =
            new Dictionary<string, CounterState>(StringComparer.Ordinal);
        private readonly HashSet<string> unexpectedSignatures =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string> statusLogger;
        private readonly string pluginGuid;
        private readonly string pluginName;
        private readonly string pluginVersion;
        private readonly string directory;
        private readonly string filePrefix;
        private readonly int processId;
        private readonly DateTime startedUtc;
        private readonly long startedTimestamp;
        private readonly long summaryIntervalTicks;
        private readonly Timer timer;
        private long sequence;
        private long snapshotSequence;
        private long nextSummaryTimestamp;
        private bool cleanShutdown;
        private bool persistenceDisabled;
        private bool persistenceFailureReported;
        private bool disposed;

        internal CrashBreadcrumbRecorder(
            bool enabled,
            string rootDirectory,
            string pluginGuid,
            string pluginName,
            string pluginVersion,
            Action<string> statusLogger,
            TimeSpan? snapshotInterval = null,
            TimeSpan? summaryInterval = null)
        {
            IsEnabled = enabled;
            this.pluginGuid = pluginGuid ?? string.Empty;
            this.pluginName = pluginName ?? string.Empty;
            this.pluginVersion = pluginVersion ?? string.Empty;
            this.statusLogger = statusLogger;
            processId = Process.GetCurrentProcess().Id;
            startedUtc = DateTime.UtcNow;
            startedTimestamp = Stopwatch.GetTimestamp();
            summaryIntervalTicks = Math.Max(
                1L,
                (long)((summaryInterval ?? TimeSpan.FromMinutes(1)).TotalSeconds * Stopwatch.Frequency));
            nextSummaryTimestamp = startedTimestamp + summaryIntervalTicks;

            if (!enabled)
                return;

            directory = Path.Combine(rootDirectory ?? string.Empty, "SerpsModsDiagnostics");
            filePrefix = SanitizeFileName(this.pluginGuid) + "-pid" +
                processId.ToString(CultureInfo.InvariantCulture);
            TryPrepareDirectory();

            TimeSpan interval = snapshotInterval ?? TimeSpan.FromSeconds(1);
            if (interval > TimeSpan.Zero)
            {
                timer = new Timer(
                    _ => TryWriteSnapshot(finalSnapshot: false),
                    null,
                    interval,
                    interval);
            }
        }

        internal bool IsEnabled { get; }

        internal CrashBreadcrumbScope Enter(
            string operation,
            long value1 = 0,
            long value2 = 0,
            long value3 = 0,
            long value4 = 0)
        {
            if (!IsEnabled || disposed)
                return default(CrashBreadcrumbScope);

            try
            {
                int threadId = GetCurrentThreadId();
                BreadcrumbRecord previous;
                bool hadPrevious;
                long entrySequence;
                lock (syncRoot)
                {
                    hadPrevious = activeByThread.TryGetValue(threadId, out previous);
                    entrySequence = AddRecordLocked(
                        BreadcrumbKind.Enter,
                        operation,
                        threadId,
                        value1,
                        value2,
                        value3,
                        value4,
                        outcome: 0);
                    activeByThread[threadId] = ring[(int)((entrySequence - 1) % RingCapacity)];
                }

                return new CrashBreadcrumbScope(this, entrySequence, threadId, hadPrevious, previous);
            }
            catch
            {
                return default(CrashBreadcrumbScope);
            }
        }

        internal void Record(
            string operation,
            long value1 = 0,
            long value2 = 0,
            long value3 = 0,
            long value4 = 0,
            int outcome = 0)
        {
            if (!IsEnabled || disposed)
                return;

            try
            {
                lock (syncRoot)
                {
                    AddRecordLocked(
                        BreadcrumbKind.Point,
                        operation,
                        GetCurrentThreadId(),
                        value1,
                        value2,
                        value3,
                        value4,
                        outcome);
                }
            }
            catch
            {
            }
        }

        internal void CompleteScope(
            long entrySequence,
            int threadId,
            bool hadPrevious,
            BreadcrumbRecord previous,
            int outcome)
        {
            if (!IsEnabled || disposed || entrySequence <= 0)
                return;

            try
            {
                lock (syncRoot)
                {
                    if (!activeByThread.TryGetValue(threadId, out BreadcrumbRecord current) ||
                        current.Sequence != entrySequence)
                    {
                        return;
                    }

                    AddRecordLocked(
                        BreadcrumbKind.Exit,
                        current.Operation,
                        threadId,
                        current.Value1,
                        current.Value2,
                        current.Value3,
                        current.Value4,
                        outcome);
                    if (hadPrevious)
                        activeByThread[threadId] = previous;
                    else
                        activeByThread.Remove(threadId);
                }
            }
            catch
            {
            }
        }

        internal void MarkCleanShutdown()
        {
            if (!IsEnabled || disposed)
                return;

            cleanShutdown = true;
            TryWriteSnapshot(finalSnapshot: true);
        }

        internal bool TryRegisterUnexpected(string signature)
        {
            if (!IsEnabled || disposed)
                return true;

            try
            {
                lock (syncRoot)
                {
                    string normalized = signature ?? string.Empty;
                    if (!unexpectedSignatures.Add(normalized))
                        return false;

                    AddRecordLocked(
                        BreadcrumbKind.Point,
                        "UnexpectedState",
                        GetCurrentThreadId(),
                        normalized.GetHashCode(),
                        0,
                        0,
                        0,
                        outcome: -1);
                    return true;
                }
            }
            catch
            {
                return true;
            }
        }

        internal void WriteSnapshotForTests() => TryWriteSnapshot(finalSnapshot: false);

        internal long SequenceForTests
        {
            get
            {
                lock (syncRoot)
                    return sequence;
            }
        }

        internal string DirectoryForTests => directory;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            timer?.Dispose();
        }

        private long AddRecordLocked(
            BreadcrumbKind kind,
            string operation,
            int threadId,
            long value1,
            long value2,
            long value3,
            long value4,
            int outcome)
        {
            long next = ++sequence;
            BreadcrumbRecord record = new BreadcrumbRecord(
                next,
                Stopwatch.GetTimestamp(),
                threadId,
                kind,
                operation ?? string.Empty,
                value1,
                value2,
                value3,
                value4,
                outcome);
            ring[(int)((next - 1) % RingCapacity)] = record;

            if (!counters.TryGetValue(record.Operation, out CounterState counter))
            {
                counter = new CounterState();
                counters.Add(record.Operation, counter);
            }
            counter.Total++;
            counter.Interval++;
            if (outcome < 0)
            {
                counter.Failures++;
                counter.IntervalFailures++;
            }
            return next;
        }

        private void TryPrepareDirectory()
        {
            try
            {
                Directory.CreateDirectory(directory);
                TrimOldSessions();
            }
            catch (Exception exception)
            {
                DisablePersistence(exception);
            }
        }

        private void TrimOldSessions()
        {
            string safeGuid = SanitizeFileName(pluginGuid);
            FileInfo[] files = new DirectoryInfo(directory)
                .GetFiles(safeGuid + "-pid*-*.txt", SearchOption.TopDirectoryOnly);
            var sessions = files
                .GroupBy(file => GetSessionPrefix(file.Name), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Max(file => file.LastWriteTimeUtc))
                .Skip(RetainedSessions - 1)
                .ToArray();

            foreach (var session in sessions)
            {
                foreach (FileInfo file in session)
                    file.Delete();
            }
        }

        private void TryWriteSnapshot(bool finalSnapshot)
        {
            if (!IsEnabled || persistenceDisabled || disposed && !finalSnapshot)
                return;

            lock (snapshotWriteRoot)
            {
                try
                {
                    Snapshot snapshot = CaptureSnapshot();
                    string text = FormatSnapshot(snapshot, finalSnapshot);
                    int slot = (int)(snapshot.SnapshotSequence & 1L);
                    string path = Path.Combine(directory, filePrefix + "-" + slot + ".txt");
                    File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    TryLogMinuteSummary(snapshot);
                }
                catch (Exception exception)
                {
                    DisablePersistence(exception);
                }
            }
        }

        private Snapshot CaptureSnapshot()
        {
            lock (syncRoot)
            {
                long currentSequence = sequence;
                long first = Math.Max(1, currentSequence - RingCapacity + 1);
                List<BreadcrumbRecord> records = new List<BreadcrumbRecord>((int)(currentSequence - first + 1));
                for (long itemSequence = first; itemSequence <= currentSequence; itemSequence++)
                {
                    BreadcrumbRecord record = ring[(int)((itemSequence - 1) % RingCapacity)];
                    if (record.Sequence == itemSequence)
                        records.Add(record);
                }

                List<BreadcrumbRecord> active = activeByThread.Values
                    .OrderBy(record => record.ThreadId)
                    .ToList();
                Dictionary<string, CounterSnapshot> counterCopy = counters.ToDictionary(
                    pair => pair.Key,
                    pair => new CounterSnapshot(
                        pair.Value.Total,
                        pair.Value.Failures,
                        pair.Value.Interval,
                        pair.Value.IntervalFailures),
                    StringComparer.Ordinal);
                return new Snapshot(
                    ++snapshotSequence,
                    currentSequence,
                    records,
                    active,
                    counterCopy,
                    cleanShutdown,
                    Stopwatch.GetTimestamp());
            }
        }

        private string FormatSnapshot(Snapshot snapshot, bool finalSnapshot)
        {
            StringBuilder builder = new StringBuilder(32768);
            AppendLine(builder, "SERPS_MOD_CRASH_BREADCRUMBS_V1");
            AppendLine(builder, "pluginGuid=" + Escape(pluginGuid));
            AppendLine(builder, "pluginName=" + Escape(pluginName));
            AppendLine(builder, "pluginVersion=" + Escape(pluginVersion));
            AppendLine(builder, "processId=" + processId.ToString(CultureInfo.InvariantCulture));
            AppendLine(builder, "sessionStartedUtc=" + startedUtc.ToString("O", CultureInfo.InvariantCulture));
            AppendLine(builder, "snapshotUtc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            AppendLine(builder, "snapshotSequence=" + snapshot.SnapshotSequence.ToString(CultureInfo.InvariantCulture));
            AppendLine(builder, "breadcrumbSequence=" + snapshot.BreadcrumbSequence.ToString(CultureInfo.InvariantCulture));
            AppendLine(builder, "state=" + ((snapshot.CleanShutdown || finalSnapshot) ? "clean-shutdown" : "running"));
            AppendLine(builder, "ringCapacity=" + RingCapacity.ToString(CultureInfo.InvariantCulture));
            AppendLine(builder, "overwritten=" + Math.Max(0, snapshot.BreadcrumbSequence - RingCapacity).ToString(CultureInfo.InvariantCulture));
            AppendLine(builder, string.Empty);
            AppendLine(builder, "[active-scopes]");
            if (snapshot.Active.Count == 0)
                AppendLine(builder, "none");
            foreach (BreadcrumbRecord record in snapshot.Active)
                AppendRecord(builder, record);

            AppendLine(builder, string.Empty);
            AppendLine(builder, "[counters]");
            foreach (KeyValuePair<string, CounterSnapshot> pair in snapshot.Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                AppendLine(
                    builder,
                    Escape(pair.Key) +
                    " total=" + pair.Value.Total.ToString(CultureInfo.InvariantCulture) +
                    " failures=" + pair.Value.Failures.ToString(CultureInfo.InvariantCulture));
            }

            AppendLine(builder, string.Empty);
            AppendLine(builder, "[breadcrumbs-oldest-to-newest]");
            foreach (BreadcrumbRecord record in snapshot.Records)
                AppendRecord(builder, record);
            return builder.ToString();
        }

        private void TryLogMinuteSummary(Snapshot snapshot)
        {
            if (snapshot.CapturedTimestamp < nextSummaryTimestamp)
                return;

            nextSummaryTimestamp = snapshot.CapturedTimestamp + summaryIntervalTicks;
            string[] parts = snapshot.Counters
                .Where(pair => pair.Value.Interval > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => Escape(pair.Key) + "=" +
                    pair.Value.Interval.ToString(CultureInfo.InvariantCulture) +
                    (pair.Value.IntervalFailures > 0
                        ? "/fail:" + pair.Value.IntervalFailures.ToString(CultureInfo.InvariantCulture)
                        : string.Empty))
                .ToArray();
            if (parts.Length > 0)
                statusLogger?.Invoke("Crash diagnostics 60s summary: " + string.Join(", ", parts) + ".");

            lock (syncRoot)
            {
                foreach (KeyValuePair<string, CounterSnapshot> pair in snapshot.Counters)
                {
                    if (!counters.TryGetValue(pair.Key, out CounterState counter))
                        continue;

                    counter.Interval = Math.Max(0, counter.Interval - pair.Value.Interval);
                    counter.IntervalFailures = Math.Max(
                        0,
                        counter.IntervalFailures - pair.Value.IntervalFailures);
                }
            }
        }

        private void DisablePersistence(Exception exception)
        {
            persistenceDisabled = true;
            if (persistenceFailureReported)
                return;

            persistenceFailureReported = true;
            try
            {
                statusLogger?.Invoke("Crash diagnostics persistence disabled for this process: " + exception.Message);
            }
            catch
            {
            }
        }

        private void AppendRecord(StringBuilder builder, BreadcrumbRecord record)
        {
            double milliseconds = (record.Timestamp - startedTimestamp) * 1000.0 / Stopwatch.Frequency;
            AppendLine(
                builder,
                "seq=" + record.Sequence.ToString(CultureInfo.InvariantCulture) +
                " ms=" + milliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                " thread=" + record.ThreadId.ToString(CultureInfo.InvariantCulture) +
                " kind=" + record.Kind.ToString().ToLowerInvariant() +
                " operation=" + Escape(record.Operation) +
                " v1=" + record.Value1.ToString(CultureInfo.InvariantCulture) +
                " v2=" + record.Value2.ToString(CultureInfo.InvariantCulture) +
                " v3=" + record.Value3.ToString(CultureInfo.InvariantCulture) +
                " v4=" + record.Value4.ToString(CultureInfo.InvariantCulture) +
                " outcome=" + record.Outcome.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendLine(StringBuilder builder, string value)
        {
            builder.Append(value);
            builder.Append((char)13);
            builder.Append((char)10);
        }

        private static string Escape(string value) =>
            (value ?? string.Empty)
                .Replace(((char)13).ToString(), " ")
                .Replace(((char)10).ToString(), " ")
                .Replace("=", ":");

        private static string SanitizeFileName(string value)
        {
            string result = value ?? string.Empty;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(result) ? "unknown-mod" : result;
        }

        private static string GetSessionPrefix(string fileName)
        {
            int slotSeparator = fileName.LastIndexOf('-');
            return slotSeparator > 0 ? fileName.Substring(0, slotSeparator) : fileName;
        }

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        internal enum BreadcrumbKind : byte
        {
            Enter,
            Exit,
            Point
        }

        internal readonly struct BreadcrumbRecord
        {
            internal BreadcrumbRecord(
                long sequence,
                long timestamp,
                int threadId,
                BreadcrumbKind kind,
                string operation,
                long value1,
                long value2,
                long value3,
                long value4,
                int outcome)
            {
                Sequence = sequence;
                Timestamp = timestamp;
                ThreadId = threadId;
                Kind = kind;
                Operation = operation;
                Value1 = value1;
                Value2 = value2;
                Value3 = value3;
                Value4 = value4;
                Outcome = outcome;
            }

            internal long Sequence { get; }
            internal long Timestamp { get; }
            internal int ThreadId { get; }
            internal BreadcrumbKind Kind { get; }
            internal string Operation { get; }
            internal long Value1 { get; }
            internal long Value2 { get; }
            internal long Value3 { get; }
            internal long Value4 { get; }
            internal int Outcome { get; }
        }

        private sealed class CounterState
        {
            internal long Total;
            internal long Failures;
            internal long Interval;
            internal long IntervalFailures;
        }

        private readonly struct CounterSnapshot
        {
            internal CounterSnapshot(long total, long failures, long interval, long intervalFailures)
            {
                Total = total;
                Failures = failures;
                Interval = interval;
                IntervalFailures = intervalFailures;
            }

            internal long Total { get; }
            internal long Failures { get; }
            internal long Interval { get; }
            internal long IntervalFailures { get; }
        }

        private sealed class Snapshot
        {
            internal Snapshot(
                long snapshotSequence,
                long breadcrumbSequence,
                List<BreadcrumbRecord> records,
                List<BreadcrumbRecord> active,
                Dictionary<string, CounterSnapshot> counters,
                bool cleanShutdown,
                long capturedTimestamp)
            {
                SnapshotSequence = snapshotSequence;
                BreadcrumbSequence = breadcrumbSequence;
                Records = records;
                Active = active;
                Counters = counters;
                CleanShutdown = cleanShutdown;
                CapturedTimestamp = capturedTimestamp;
            }

            internal long SnapshotSequence { get; }
            internal long BreadcrumbSequence { get; }
            internal List<BreadcrumbRecord> Records { get; }
            internal List<BreadcrumbRecord> Active { get; }
            internal Dictionary<string, CounterSnapshot> Counters { get; }
            internal bool CleanShutdown { get; }
            internal long CapturedTimestamp { get; }
        }
    }

    internal readonly struct CrashBreadcrumbScope : IDisposable
    {
        private readonly CrashBreadcrumbRecorder recorder;
        private readonly long entrySequence;
        private readonly int threadId;
        private readonly bool hadPrevious;
        private readonly CrashBreadcrumbRecorder.BreadcrumbRecord previous;

        internal CrashBreadcrumbScope(
            CrashBreadcrumbRecorder recorder,
            long entrySequence,
            int threadId,
            bool hadPrevious,
            CrashBreadcrumbRecorder.BreadcrumbRecord previous)
        {
            this.recorder = recorder;
            this.entrySequence = entrySequence;
            this.threadId = threadId;
            this.hadPrevious = hadPrevious;
            this.previous = previous;
        }

        public void Complete(int outcome = 0) =>
            recorder?.CompleteScope(entrySequence, threadId, hadPrevious, previous, outcome);

        public void Dispose() => Complete();
    }
}
