# UnitLimit release status

**Status:** code newer

- Release: [v1.0.92](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.92)
- Release commit: [4ace0a6](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4ace0a6d4d03fcf3447cbdc14ca444190c9476f0)
- Current main commit: [8b83786](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/8b83786ab9c78d81177ee0f9903f9a639669f7ac)

## Relevant changed files

- `Shared/CrashBreadcrumbDiagnostics.cs`
- `Shared/CrashBreadcrumbRecorder.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayFeatureModePolicy.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplayModModePolicy.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`
- `Shared/ToolTipPresentation.cs`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Override/ScriptExtenderUI/UnitLimitSettings.xaml`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/UnitLimit.dll`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/UnitLimit.pdb`
- `UnitLimit/src/ActiveSiegeTentCache.cs`
- `UnitLimit/src/MakeTroopGameActionHook.cs`
- `UnitLimit/src/UnitLimitPlugin.cs`
- `UnitLimit/src/UnitLimitRuntime.cs`
- `UnitLimit/src/UnitLimitRuntime.Helpers.cs`
- `UnitLimit/src/UnitLimitRuntime.RecruitmentAvailability.cs`
- `UnitLimit/src/UnitLimitRuntime.Settings.cs`
- `UnitLimit/src/UnitLimitRuntime.Tooltips.cs`
- `UnitLimit/src/UnitLimitRuntime.UnitLimits.cs`
- `UnitLimit/UnitLimit.csproj`

## Diff

```diff
diff --git a/Shared/CrashBreadcrumbDiagnostics.cs b/Shared/CrashBreadcrumbDiagnostics.cs
new file mode 100644
index 00000000..f8a2e523
--- /dev/null
+++ b/Shared/CrashBreadcrumbDiagnostics.cs
@@ -0,0 +1,65 @@
+using BepInEx.Logging;
+using System;
+using UnityEngine;
+
+namespace Shared
+{
+    internal static class CrashBreadcrumbDiagnostics
+    {
+        private static CrashBreadcrumbRecorder recorder;
+        private static bool initialized;
+
+        internal static bool IsEnabled => recorder?.IsEnabled == true;
+
+        internal static void Initialize(
+            ManualLogSource log,
+            string pluginGuid,
+            string pluginName,
+            string pluginVersion)
+        {
+            if (initialized)
+                return;
+
+            initialized = true;
+            bool enabled = DebugLogHelper.IsDebugEnabled();
+            recorder = new CrashBreadcrumbRecorder(
+                enabled,
+                BepInEx.Paths.BepInExRootPath,
+                pluginGuid,
+                pluginName,
+                pluginVersion,
+                message => DebugLogHelper.LogDebug(log, message));
+            if (!enabled)
+                return;
+
+            Application.quitting += MarkCleanShutdown;
+            DebugLogHelper.LogDebug(
+                log,
+                $"Crash breadcrumb diagnostics enabled: plugin={pluginGuid}, " +
+                "ringCapacity=256, snapshotIntervalSeconds=1, retainedSessions=3.");
+        }
+
+        internal static CrashBreadcrumbScope Enter(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0) =>
+            recorder?.Enter(operation, value1, value2, value3, value4) ??
+            default(CrashBreadcrumbScope);
+
+        internal static void Record(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0,
+            int outcome = 0) =>
+            recorder?.Record(operation, value1, value2, value3, value4, outcome);
+
+        internal static bool ShouldLogUnexpected(string signature) =>
+            recorder?.TryRegisterUnexpected(signature) ?? true;
+
+        private static void MarkCleanShutdown() => recorder?.MarkCleanShutdown();
+    }
+}

diff --git a/Shared/CrashBreadcrumbRecorder.cs b/Shared/CrashBreadcrumbRecorder.cs
new file mode 100644
index 00000000..4b45ad4d
--- /dev/null
+++ b/Shared/CrashBreadcrumbRecorder.cs
@@ -0,0 +1,652 @@
+using System;
+using System.Collections.Generic;
+using System.Diagnostics;
+using System.Globalization;
+using System.IO;
+using System.Linq;
+using System.Runtime.InteropServices;
+using System.Text;
+using System.Threading;
+
+namespace Shared
+{
+    internal sealed class CrashBreadcrumbRecorder : IDisposable
+    {
+        private const int RingCapacity = 256;
+        private const int RetainedSessions = 3;
+        private readonly object syncRoot = new object();
+        private readonly object snapshotWriteRoot = new object();
+        private readonly BreadcrumbRecord[] ring = new BreadcrumbRecord[RingCapacity];
+        private readonly Dictionary<int, BreadcrumbRecord> activeByThread =
+            new Dictionary<int, BreadcrumbRecord>();
+        private readonly Dictionary<string, CounterState> counters =
+            new Dictionary<string, CounterState>(StringComparer.Ordinal);
+        private readonly HashSet<string> unexpectedSignatures =
+            new HashSet<string>(StringComparer.Ordinal);
+        private readonly Action<string> statusLogger;
+        private readonly string pluginGuid;
+        private readonly string pluginName;
+        private readonly string pluginVersion;
+        private readonly string directory;
+        private readonly string filePrefix;
+        private readonly int processId;
+        private readonly DateTime startedUtc;
+        private readonly long startedTimestamp;
+        private readonly long summaryIntervalTicks;
+        private readonly Timer timer;
+        private long sequence;
+        private long snapshotSequence;
+        private long nextSummaryTimestamp;
+        private bool cleanShutdown;
+        private bool persistenceDisabled;
+        private bool persistenceFailureReported;
+        private bool disposed;
+
+        internal CrashBreadcrumbRecorder(
+            bool enabled,
+            string rootDirectory,
+            string pluginGuid,
+            string pluginName,
+            string pluginVersion,
+            Action<string> statusLogger,
+            TimeSpan? snapshotInterval = null,
+            TimeSpan? summaryInterval = null)
+        {
+            IsEnabled = enabled;
+            this.pluginGuid = pluginGuid ?? string.Empty;
+            this.pluginName = pluginName ?? string.Empty;
+            this.pluginVersion = pluginVersion ?? string.Empty;
+            this.statusLogger = statusLogger;
+            processId = Process.GetCurrentProcess().Id;
+            startedUtc = DateTime.UtcNow;
+            startedTimestamp = Stopwatch.GetTimestamp();
+            summaryIntervalTicks = Math.Max(
+                1L,
+                (long)((summaryInterval ?? TimeSpan.FromMinutes(1)).TotalSeconds * Stopwatch.Frequency));
+            nextSummaryTimestamp = startedTimestamp + summaryIntervalTicks;
+
+            if (!enabled)
+                return;
+
+            directory = Path.Combine(rootDirectory ?? string.Empty, "SerpsModsDiagnostics");
+            filePrefix = SanitizeFileName(this.pluginGuid) + "-pid" +
+                processId.ToString(CultureInfo.InvariantCulture);
+            TryPrepareDirectory();
+
+            TimeSpan interval = snapshotInterval ?? TimeSpan.FromSeconds(1);
+            if (interval > TimeSpan.Zero)
+            {
+                timer = new Timer(
+                    _ => TryWriteSnapshot(finalSnapshot: false),
+                    null,
+                    interval,
+                    interval);
+            }
+        }
+
+        internal bool IsEnabled { get; }
+
+        internal CrashBreadcrumbScope Enter(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0)
+        {
+            if (!IsEnabled || disposed)
+                return default(CrashBreadcrumbScope);
+
+            try
+            {
+                int threadId = GetCurrentThreadId();
+                BreadcrumbRecord previous;
+                bool hadPrevious;
+                long entrySequence;
+                lock (syncRoot)
+                {
+                    hadPrevious = activeByThread.TryGetValue(threadId, out previous);
+                    entrySequence = AddRecordLocked(
+                        BreadcrumbKind.Enter,
+                        operation,
+                        threadId,
+                        value1,
+                        value2,
+                        value3,
+                        value4,
+                        outcome: 0);
+                    activeByThread[threadId] = ring[(int)((entrySequence - 1) % RingCapacity)];
+                }
+
+                return new CrashBreadcrumbScope(this, entrySequence, threadId, hadPrevious, previous);
+            }
+            catch
+            {
+                return default(CrashBreadcrumbScope);
+            }
+        }
+
+        internal void Record(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0,
+            int outcome = 0)
+        {
+            if (!IsEnabled || disposed)
+                return;
+
+            try
+            {
+                lock (syncRoot)
+                {
+                    AddRecordLocked(
+                        BreadcrumbKind.Point,
+                        operation,
+                        GetCurrentThreadId(),
+                        value1,
+                        value2,
+                        value3,
+                        value4,
+                        outcome);
+                }
+            }
+            catch
+            {
+            }
+        }
+
+        internal void CompleteScope(
+            long entrySequence,
+            int threadId,
+            bool hadPrevious,
+            BreadcrumbRecord previous,
+            int outcome)
+        {
+            if (!IsEnabled || disposed || entrySequence <= 0)
+                return;
+
+            try
+            {
+                lock (syncRoot)
+                {
+                    if (!activeByThread.TryGetValue(threadId, out BreadcrumbRecord current) ||
+                        current.Sequence != entrySequence)
+                    {
+                        return;
+                    }
+
+                    AddRecordLocked(
+                        BreadcrumbKind.Exit,
+                        current.Operation,
+                        threadId,
+                        current.Value1,
+                        current.Value2,
+                        current.Value3,
+                        current.Value4,
+                        outcome);
+                    if (hadPrevious)
+                        activeByThread[threadId] = previous;
+                    else
+                        activeByThread.Remove(threadId);
+                }
+            }
+            catch
+            {
+            }
+        }
+
+        internal void MarkCleanShutdown()
+        {
+            if (!IsEnabled || disposed)
+                return;
+
+            cleanShutdown = true;
+            TryWriteSnapshot(finalSnapshot: true);
+        }
+
+        internal bool TryRegisterUnexpected(string signature)
+        {
+            if (!IsEnabled || disposed)
+                return true;
+
+            try
+            {
+                lock (syncRoot)
+                {
+                    string normalized = signature ?? string.Empty;
+                    if (!unexpectedSignatures.Add(normalized))
+                        return false;
+
+                    AddRecordLocked(
+                        BreadcrumbKind.Point,
+                        "UnexpectedState",
+                        GetCurrentThreadId(),
+                        normalized.GetHashCode(),
+                        0,
+                        0,
+                        0,
+                        outcome: -1);
+                    return true;
+                }
+            }
+            catch
+            {
+                return true;
+            }
+        }
+
+        internal void WriteSnapshotForTests() => TryWriteSnapshot(finalSnapshot: false);
+
+        internal long SequenceForTests
+        {
+            get
+            {
+                lock (syncRoot)
+                    return sequence;
+            }
+        }
+
+        internal string DirectoryForTests => directory;
+
+        public void Dispose()
+        {
+            if (disposed)
+                return;
+
+            disposed = true;
+            timer?.Dispose();
+        }
+
+        private long AddRecordLocked(
+            BreadcrumbKind kind,
+            string operation,
+            int threadId,
+            long value1,
+            long value2,
+            long value3,
+            long value4,
+            int outcome)
+        {
+            long next = ++sequence;
+            BreadcrumbRecord record = new BreadcrumbRecord(
+                next,
+                Stopwatch.GetTimestamp(),
+                threadId,
+                kind,
+                operation ?? string.Empty,
+                value1,
+                value2,
+                value3,
+                value4,
+                outcome);
+            ring[(int)((next - 1) % RingCapacity)] = record;
+
+            if (!counters.TryGetValue(record.Operation, out CounterState counter))
+            {
+                counter = new CounterState();
+                counters.Add(record.Operation, counter);
+            }
+            counter.Total++;
+            counter.Interval++;
+            if (outcome < 0)
+            {
+                counter.Failures++;
+                counter.IntervalFailures++;
+            }
+            return next;
+        }
+
+        private void TryPrepareDirectory()
+        {
+            try
+            {
+                Directory.CreateDirectory(directory);
+                TrimOldSessions();
+            }
+            catch (Exception exception)
+            {
+                DisablePersistence(exception);
+            }
+        }
+
+        private void TrimOldSessions()
+        {
+            string safeGuid = SanitizeFileName(pluginGuid);
+            FileInfo[] files = new DirectoryInfo(directory)
+                .GetFiles(safeGuid + "-pid*-*.txt", SearchOption.TopDirectoryOnly);
+            var sessions = files
+                .GroupBy(file => GetSessionPrefix(file.Name), StringComparer.OrdinalIgnoreCase)
+                .OrderByDescending(group => group.Max(file => file.LastWriteTimeUtc))
+                .Skip(RetainedSessions - 1)
+                .ToArray();
+
+            foreach (var session in sessions)
+            {
+                foreach (FileInfo file in session)
+                    file.Delete();
+            }
+        }
+
+        private void TryWriteSnapshot(bool finalSnapshot)
+        {
+            if (!IsEnabled || persistenceDisabled || disposed && !finalSnapshot)
+                return;
+
+            lock (snapshotWriteRoot)
+            {
+                try
+                {
+                    Snapshot snapshot = CaptureSnapshot();
+                    string text = FormatSnapshot(snapshot, finalSnapshot);
+                    int slot = (int)(snapshot.SnapshotSequence & 1L);
+                    string path = Path.Combine(directory, filePrefix + "-" + slot + ".txt");
+                    File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
+                    TryLogMinuteSummary(snapshot);
+                }
+                catch (Exception exception)
+                {
+                    DisablePersistence(exception);
+                }
+            }
+        }
+
+        private Snapshot CaptureSnapshot()
+        {
+            lock (syncRoot)
+            {
+                long currentSequence = sequence;
+                long first = Math.Max(1, currentSequence - RingCapacity + 1);
+                List<BreadcrumbRecord> records = new List<BreadcrumbRecord>((int)(currentSequence - first + 1));
+                for (long itemSequence = first; itemSequence <= currentSequence; itemSequence++)
+                {
+                    BreadcrumbRecord record = ring[(int)((itemSequence - 1) % RingCapacity)];
+                    if (record.Sequence == itemSequence)
+                        records.Add(record);
+                }
+
+                List<BreadcrumbRecord> active = activeByThread.Values
+                    .OrderBy(record => record.ThreadId)
+                    .ToList();
+                Dictionary<string, CounterSnapshot> counterCopy = counters.ToDictionary(
+                    pair => pair.Key,
+                    pair => new CounterSnapshot(
+                        pair.Value.Total,
+                        pair.Value.Failures,
+                        pair.Value.Interval,
+                        pair.Value.IntervalFailures),
+                    StringComparer.Ordinal);
+                return new Snapshot(
+                    ++snapshotSequence,
+                    currentSequence,
+                    records,
+                    active,
+                    counterCopy,
+                    cleanShutdown,
+                    Stopwatch.GetTimestamp());
+            }
+        }
+
+        private string FormatSnapshot(Snapshot snapshot, bool finalSnapshot)
+        {
+            StringBuilder builder = new StringBuilder(32768);
+            AppendLine(builder, "SERPS_MOD_CRASH_BREADCRUMBS_V1");
+            AppendLine(builder, "pluginGuid=" + Escape(pluginGuid));
+            AppendLine(builder, "pluginName=" + Escape(pluginName));
+            AppendLine(builder, "pluginVersion=" + Escape(pluginVersion));
+            AppendLine(builder, "processId=" + processId.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "sessionStartedUtc=" + startedUtc.ToString("O", CultureInfo.InvariantCulture));
+            AppendLine(builder, "snapshotUtc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
+            AppendLine(builder, "snapshotSequence=" + snapshot.SnapshotSequence.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "breadcrumbSequence=" + snapshot.BreadcrumbSequence.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "state=" + ((snapshot.CleanShutdown || finalSnapshot) ? "clean-shutdown" : "running"));
+            AppendLine(builder, "ringCapacity=" + RingCapacity.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "overwritten=" + Math.Max(0, snapshot.BreadcrumbSequence - RingCapacity).ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, string.Empty);
+            AppendLine(builder, "[active-scopes]");
+            if (snapshot.Active.Count == 0)
+                AppendLine(builder, "none");
+            foreach (BreadcrumbRecord record in snapshot.Active)
+                AppendRecord(builder, record);
+
+            AppendLine(builder, string.Empty);
+            AppendLine(builder, "[counters]");
+            foreach (KeyValuePair<string, CounterSnapshot> pair in snapshot.Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
+            {
+                AppendLine(
+                    builder,
+                    Escape(pair.Key) +
+                    " total=" + pair.Value.Total.ToString(CultureInfo.InvariantCulture) +
+                    " failures=" + pair.Value.Failures.ToString(CultureInfo.InvariantCulture));
+            }
+
+            AppendLine(builder, string.Empty);
+            AppendLine(builder, "[breadcrumbs-oldest-to-newest]");
+            foreach (BreadcrumbRecord record in snapshot.Records)
+                AppendRecord(builder, record);
+            return builder.ToString();
+        }
+
+        private void TryLogMinuteSummary(Snapshot snapshot)
+        {
+            if (snapshot.CapturedTimestamp < nextSummaryTimestamp)
+                return;
+
+            nextSummaryTimestamp = snapshot.CapturedTimestamp + summaryIntervalTicks;
+            string[] parts = snapshot.Counters
+                .Where(pair => pair.Value.Interval > 0)
+                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
+                .Select(pair => Escape(pair.Key) + "=" +
+                    pair.Value.Interval.ToString(CultureInfo.InvariantCulture) +
+                    (pair.Value.IntervalFailures > 0
+                        ? "/fail:" + pair.Value.IntervalFailures.ToString(CultureInfo.InvariantCulture)
+                        : string.Empty))
+                .ToArray();
+            if (parts.Length > 0)
+                statusLogger?.Invoke("Crash diagnostics 60s summary: " + string.Join(", ", parts) + ".");
+
+            lock (syncRoot)
+            {
+                foreach (KeyValuePair<string, CounterSnapshot> pair in snapshot.Counters)
+                {
+                    if (!counters.TryGetValue(pair.Key, out CounterState counter))
+                        continue;
+
+                    counter.Interval = Math.Max(0, counter.Interval - pair.Value.Interval);
+                    counter.IntervalFailures = Math.Max(
+                        0,
+                        counter.IntervalFailures - pair.Value.IntervalFailures);
+                }
+            }
+        }
+
+        private void DisablePersistence(Exception exception)
+        {
+            persistenceDisabled = true;
+            if (persistenceFailureReported)
+                return;
+
+            persistenceFailureReported = true;
+            try
+            {
+                statusLogger?.Invoke("Crash diagnostics persistence disabled for this process: " + exception.Message);
+            }
+            catch
+            {
+            }
+        }
+
+        private void AppendRecord(StringBuilder builder, BreadcrumbRecord record)
+        {
+            double milliseconds = (record.Timestamp - startedTimestamp) * 1000.0 / Stopwatch.Frequency;
+            AppendLine(
+                builder,
+                "seq=" + record.Sequence.ToString(CultureInfo.InvariantCulture) +
+                " ms=" + milliseconds.ToString("F3", CultureInfo.InvariantCulture) +
+                " thread=" + record.ThreadId.ToString(CultureInfo.InvariantCulture) +
+                " kind=" + record.Kind.ToString().ToLowerInvariant() +
+                " operation=" + Escape(record.Operation) +
+                " v1=" + record.Value1.ToString(CultureInfo.InvariantCulture) +
+                " v2=" + record.Value2.ToString(CultureInfo.InvariantCulture) +
+                " v3=" + record.Value3.ToString(CultureInfo.InvariantCulture) +
+                " v4=" + record.Value4.ToString(CultureInfo.InvariantCulture) +
+                " outcome=" + record.Outcome.ToString(CultureInfo.InvariantCulture));
+        }
+
+        private static void AppendLine(StringBuilder builder, string value)
+        {
+            builder.Append(value);
+            builder.Append((char)13);
+            builder.Append((char)10);
+        }
+
+        private static string Escape(string value) =>
+            (value ?? string.Empty)
+                .Replace(((char)13).ToString(), " ")
+                .Replace(((char)10).ToString(), " ")
+                .Replace("=", ":");
+
+        private static string SanitizeFileName(string value)
+        {
+            string result = value ?? string.Empty;
+            foreach (char invalid in Path.GetInvalidFileNameChars())
+                result = result.Replace(invalid, '_');
+            return string.IsNullOrWhiteSpace(result) ? "unknown-mod" : result;
+        }
+
+        private static string GetSessionPrefix(string fileName)
+        {
+            int slotSeparator = fileName.LastIndexOf('-');
+            return slotSeparator > 0 ? fileName.Substring(0, slotSeparator) : fileName;
+        }
+
+        [DllImport("kernel32.dll")]
+        private static extern int GetCurrentThreadId();
+
+        internal enum BreadcrumbKind : byte
+        {
+            Enter,
+            Exit,
+            Point
+        }
+
+        internal readonly struct BreadcrumbRecord
+        {
+            internal BreadcrumbRecord(
+                long sequence,
+                long timestamp,
+                int threadId,
+                BreadcrumbKind kind,
+                string operation,
+                long value1,
+                long value2,
+                long value3,
+                long value4,
+                int outcome)
+            {
+                Sequence = sequence;
+                Timestamp = timestamp;
+                ThreadId = threadId;
+                Kind = kind;
+                Operation = operation;
+                Value1 = value1;
+                Value2 = value2;
+                Value3 = value3;
+                Value4 = value4;
+                Outcome = outcome;
+            }
+
+            internal long Sequence { get; }
+            internal long Timestamp { get; }
+            internal int ThreadId { get; }
+            internal BreadcrumbKind Kind { get; }
+            internal string Operation { get; }
+            internal long Value1 { get; }
+            internal long Value2 { get; }
+            internal long Value3 { get; }
+            internal long Value4 { get; }
+            internal int Outcome { get; }
+        }
+
+        private sealed class CounterState
+        {
+            internal long Total;
+            internal long Failures;
+            internal long Interval;
+            internal long IntervalFailures;
+        }
+
+        private readonly struct CounterSnapshot
+        {
+            internal CounterSnapshot(long total, long failures, long interval, long intervalFailures)
+            {
+                Total = total;
+                Failures = failures;
+                Interval = interval;
+                IntervalFailures = intervalFailures;
+            }
+
+            internal long Total { get; }
+            internal long Failures { get; }
+            internal long Interval { get; }
+            internal long IntervalFailures { get; }
+        }
+
+        private sealed class Snapshot
+        {
+            internal Snapshot(
+                long snapshotSequence,
+                long breadcrumbSequence,
+                List<BreadcrumbRecord> records,
+                List<BreadcrumbRecord> active,
+                Dictionary<string, CounterSnapshot> counters,
+                bool cleanShutdown,
+                long capturedTimestamp)
+            {
+                SnapshotSequence = snapshotSequence;
+                BreadcrumbSequence = breadcrumbSequence;
+                Records = records;
+                Active = active;
+                Counters = counters;
+                CleanShutdown = cleanShutdown;
+                CapturedTimestamp = capturedTimestamp;
+            }
+
+            internal long SnapshotSequence { get; }
+            internal long BreadcrumbSequence { get; }
+            internal List<BreadcrumbRecord> Records { get; }
+            internal List<BreadcrumbRecord> Active { get; }
+            internal Dictionary<string, CounterSnapshot> Counters { get; }
+            internal bool CleanShutdown { get; }
+            internal long CapturedTimestamp { get; }
+        }
+    }
+
+    internal readonly struct CrashBreadcrumbScope : IDisposable
+    {
+        private readonly CrashBreadcrumbRecorder recorder;
+        private readonly long entrySequence;
+        private readonly int threadId;
+        private readonly bool hadPrevious;
+        private readonly CrashBreadcrumbRecorder.BreadcrumbRecord previous;
+
+        internal CrashBreadcrumbScope(
+            CrashBreadcrumbRecorder recorder,
+            long entrySequence,
+            int threadId,
+            bool hadPrevious,
+            CrashBreadcrumbRecorder.BreadcrumbRecord previous)
+        {
+            this.recorder = recorder;
+            this.entrySequence = entrySequence;
+            this.threadId = threadId;
+            this.hadPrevious = hadPrevious;
+            this.previous = previous;
+        }
+
+        public void Complete(int outcome = 0) =>
+            recorder?.CompleteScope(entrySequence, threadId, hadPrevious, previous, outcome);
+
+        public void Dispose() => Complete();
+    }
+}

diff --git a/Shared/GameModeHelper.cs b/Shared/GameModeHelper.cs
index 22480a13..838befb5 100644
--- a/Shared/GameModeHelper.cs
+++ b/Shared/GameModeHelper.cs
@@ -1,17 +1,122 @@
 using SHCDESE.API;
+using SHCDESE.EventAPI.MapLoader;
 using CrusaderDE;
 using System;
 using System.Collections.Generic;
 using System.Linq;
+using System.Reflection;
 #if !SHARED_PRESET_TESTS
 using Steamworks;
 #endif
 
 namespace Shared
 {
+    internal enum GameModeKind
+    {
+        Unknown,
+        MapEditor,
+        Campaign,
+        StandaloneMission,
+        CustomGame,
+        VanillaTrail,
+        CustomTrail,
+        CoopTrail,
+        SandsOfTime,
+    }
+
+    internal enum GameModeLaunchVariant
+    {
+        Standard,
+        Customized,
+        RestoredCustomizedSave,
+    }
+
+    internal enum GameTrailType
+    {
+        FirstEdition = 0,
+        Warchest = 1,
+        Extreme = 2,
+        SandsOne = 11,
+        SandsTwo = 12,
+        SandsThree = 13,
+        SandsFour = 14,
+        SandsFive = 15,
+        SandsSix = 16,
+        SandsSeven = 17,
+        SandsEight = 18,
+    }
+
     internal static class GameModeHelper
     {
+        private const int NoGameValue = -1;
+        private const int NoCoopTrail = 0;
+        private const uint NonCampaignMapId = uint.MaxValue;
+        private const int MinimumOriginApiVersion = 1;
+        private const int SupportedOriginApiVersion = 2;
+        private const int FirstCustomTrailId = 90;
+        private const int LastCustomTrailId = 92;
+        private const int FirstCoopTrailId = 0;
+        private const int LastCoopTrailId = 3;
+        private const int FirstMissionId = 1;
+        private const int LastCoopMissionId = 10;
+
         public static GameModeSnapshot Capture(bool multiplayerSave = false)
+        {
+            return CaptureCore(
+                multiplayerSave,
+                campaignMapId: 0,
+                eventTrailType: NoGameValue,
+                editorLoad: false);
+        }
+
+        public static GameModeSnapshot Capture(MapStartEventArgs args) =>
+            CaptureCore(
+                args != null && args.bMultiplayerSave != 0,
+                args?.CampaignMapId ?? 0,
+                NoGameValue,
+                editorLoad: false);
+
+        public static GameModeSnapshot Capture(MapLoadEventArgs args) =>
+            CaptureCore(
+                args != null && args.bMultiplayerSave != 0,
+                args != null && args.CampaignMapID != NonCampaignMapId && args.CampaignMapID <= int.MaxValue
+                    ? (int)args.CampaignMapID
+                    : 0,
+                args?.TrailType ?? NoGameValue,
+                editorLoad: false);
+
+        public static GameModeSnapshot Capture(LoadSaveGameEventArgs args) =>
+            CaptureCore(
+                multiplayerSave: false,
+                campaignMapId: 0,
+                eventTrailType: NoGameValue,
+                editorLoad: args != null && args.LoadingEditorMap);
+
+        internal static bool AllowsCustomGameMods(
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant)
+        {
+            if (kind == GameModeKind.CustomGame)
+                return true;
+            if (launchVariant == GameModeLaunchVariant.Standard)
+                return false;
+
+            return kind == GameModeKind.VanillaTrail ||
+                kind == GameModeKind.CustomTrail ||
+                kind == GameModeKind.CoopTrail ||
+                kind == GameModeKind.SandsOfTime;
+        }
+
+        internal static bool AllowsRegularGameplayMods(
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant) =>
+            kind == GameModeKind.MapEditor || AllowsCustomGameMods(kind, launchVariant);
+
+        private static GameModeSnapshot CaptureCore(
+            bool multiplayerSave,
+            int campaignMapId,
+            int eventTrailType,
+            bool editorLoad)
         {
             Director director = Director.instance;
             GameData gameData = GameData.Instance;
@@ -58,9 +163,16 @@ namespace Shared
                 realLobbyMembers > 0 ||
                 realNetworkGameMembers > 0;
 
-            int gameType = gameData != null ? gameData.game_type : -1;
-            int skirmishGameType = gameData != null ? gameData.SkirmishGameType : -1;
-            bool mapEditor = IsMapEditor();
+            int gameType = gameData != null ? gameData.game_type : NoGameValue;
+            int skirmishGameType = gameData != null ? gameData.SkirmishGameType : NoGameValue;
+            int skirmishTrailType = gameData != null ? gameData.SkirmishTrailType : NoGameValue;
+            int coopTrailId = gameData != null ? gameData.coopTrailID : NoGameValue;
+            bool mapEditor =
+                editorLoad ||
+                gameData?.mapType == Enums.GameModes.MAP_EDITOR ||
+                IsMapEditor();
+            bool sandsOfTime = TryIsSandsOfTime(gameData);
+            bool customTrailRestart = TryCaptureCustomTrailRestart();
             // game_type 3 is Vanilla's skirmish family. Immediately after leaving a
             // real multiplayer game, a new local skirmish can reach OnStartMap before
             // Vanilla changes SkirmishGameType from -1. Its all-local skirmish lobby
@@ -73,14 +185,69 @@ namespace Shared
             bool singleplayerSkirmishMode =
                 !realMultiplayer &&
                 !mapEditor &&
-                gameType == 3 &&
+                gameType == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
                 (skirmishGameType >= 0 || localSkirmishTransition);
 
+            bool vanillaCustomized = TryCaptureVanillaCustomizedTrail(
+                out int customizedTrailType,
+                out int customizedTrailId);
+            ExternalCustomizedOrigin externalOrigin = CaptureExternalCustomizedOrigin();
+            GameModeKind observedKind = ResolveKind(
+                mapEditor,
+                gameType,
+                skirmishGameType,
+                skirmishTrailType,
+                coopTrailId,
+                campaignMapId,
+                eventTrailType);
+            GameModeKind kind = observedKind;
+            if (observedKind == GameModeKind.CustomGame && externalOrigin.LaunchPending)
+            {
+                GameModeKind originKind = ResolveExternalOriginKind(externalOrigin.Origin);
+                if (originKind != GameModeKind.Unknown)
+                    kind = originKind;
+            }
+            if (sandsOfTime && kind != GameModeKind.MapEditor && kind != GameModeKind.CoopTrail)
+                kind = GameModeKind.SandsOfTime;
+            else if (customTrailRestart && (kind == GameModeKind.Unknown || kind == GameModeKind.CustomGame))
+                kind = GameModeKind.CustomTrail;
+            if (vanillaCustomized && customizedTrailId >= 0 && kind == GameModeKind.CustomGame)
+            {
+                bool builtInOriginRequired = externalOrigin.SupportsBuiltInOrigins;
+                if (IsVanillaTrailType(customizedTrailType) &&
+                    (!builtInOriginRequired || externalOrigin.Origin == ExternalCustomizedOrigin.VanillaTrail))
+                    kind = GameModeKind.VanillaTrail;
+                else if (IsSandsTrailType(customizedTrailType) &&
+                    (!builtInOriginRequired || externalOrigin.Origin == ExternalCustomizedOrigin.SandsOfTime))
+                    kind = GameModeKind.SandsOfTime;
+            }
+            GameModeLaunchVariant launchVariant = ResolveLaunchVariant(
+                kind,
+                vanillaCustomized,
+                customizedTrailType,
+                customizedTrailId,
+                observedKind == GameModeKind.CustomGame,
+                externalOrigin);
+            bool conflictingOrigin = externalOrigin.IsInvalid ||
+                (externalOrigin.Origin != ExternalCustomizedOrigin.None &&
+                 (!ExternalOriginMatchesKind(externalOrigin, kind) ||
+                  !ExternalOriginMatchesEvidence(
+                      externalOrigin,
+                      kind,
+                      skirmishTrailType,
+                      coopTrailId,
+                      eventTrailType,
+                      vanillaCustomized,
+                      customizedTrailType,
+                      customizedTrailId)));
+
             return new GameModeSnapshot(
                 realMultiplayer,
                 singleplayerSkirmishMode,
-                singleplayerSkirmishMode && skirmishGameType == 0,
-                singleplayerSkirmishMode && (skirmishGameType == 1 || skirmishGameType == 2),
+                singleplayerSkirmishMode && skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
+                singleplayerSkirmishMode &&
+                    (skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL ||
+                     skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL),
                 mapEditor,
                 multiplayerSave,
                 director != null,
@@ -95,13 +262,275 @@ namespace Shared
                 realNetworkGameMembers,
                 gameType,
                 skirmishGameType,
-                gameData != null ? gameData.coopTrailID : -1);
+                skirmishTrailType,
+                coopTrailId,
+                kind,
+                launchVariant,
+                campaignMapId,
+                eventTrailType,
+                externalOrigin.Origin != ExternalCustomizedOrigin.None
+                    ? externalOrigin.TrailId
+                    : customizedTrailId,
+                externalOrigin.Origin != ExternalCustomizedOrigin.None
+                    ? externalOrigin.MissionId
+                    : customizedTrailId,
+                externalOrigin.Origin,
+                conflictingOrigin);
+        }
+
+        internal static GameModeKind ResolveKind(
+            bool mapEditor,
+            int gameType,
+            int skirmishGameType,
+            int skirmishTrailType,
+            int coopTrailId,
+            int campaignMapId = 0,
+            int eventTrailType = NoGameValue)
+        {
+            if (mapEditor)
+                return GameModeKind.MapEditor;
+            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_CAMPAIGN || campaignMapId > 0)
+                return GameModeKind.Campaign;
+            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_MAP)
+                return GameModeKind.StandaloneMission;
+            if (coopTrailId > NoCoopTrail)
+                return GameModeKind.CoopTrail;
+
+            bool hasTrailEvent = eventTrailType >= 0;
+            int effectiveTrailType = hasTrailEvent ? eventTrailType : skirmishTrailType;
+            bool vanillaTrailMode =
+                skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL;
+            if ((vanillaTrailMode || hasTrailEvent) && IsSandsTrailType(effectiveTrailType))
+                return GameModeKind.SandsOfTime;
+            if ((vanillaTrailMode || hasTrailEvent) && IsVanillaTrailType(effectiveTrailType))
+            {
+                return GameModeKind.VanillaTrail;
+            }
+            if (skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL)
+                return GameModeKind.CustomTrail;
+            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
+                skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM &&
+                coopTrailId == NoCoopTrail)
+            {
+                return GameModeKind.CustomGame;
+            }
+            return GameModeKind.Unknown;
+        }
+
+        internal static GameModeLaunchVariant ResolveLaunchVariant(
+            GameModeKind kind,
+            bool vanillaCustomized,
+            int customizedTrailType,
+            int customizedTrailId,
+            bool vanillaCustomGameContext,
+            ExternalCustomizedOrigin externalOrigin)
+        {
+            bool vanillaMatches = vanillaCustomized &&
+                vanillaCustomGameContext &&
+                customizedTrailId >= 0 &&
+                (!externalOrigin.SupportsBuiltInOrigins ||
+                 ExternalOriginMatchesKind(externalOrigin, kind)) &&
+                ((kind == GameModeKind.VanillaTrail && IsVanillaTrailType(customizedTrailType)) ||
+                 (kind == GameModeKind.SandsOfTime && IsSandsTrailType(customizedTrailType)));
+            bool externalMatches =
+                ExternalOriginMatchesKind(externalOrigin, kind) &&
+                (kind != GameModeKind.CustomGame || externalOrigin.LaunchPending);
+            if (!vanillaMatches && !externalMatches)
+                return GameModeLaunchVariant.Standard;
+            return externalMatches && externalOrigin.RestoredFromSave
+                ? GameModeLaunchVariant.RestoredCustomizedSave
+                : GameModeLaunchVariant.Customized;
+        }
+
+        private static bool IsVanillaTrailType(int value) =>
+            value >= (int)GameTrailType.FirstEdition && value <= (int)GameTrailType.Extreme;
+
+        private static bool IsSandsTrailType(int value) =>
+            value >= (int)GameTrailType.SandsOne && value <= (int)GameTrailType.SandsEight;
+
+        private static GameModeKind ResolveExternalOriginKind(int origin)
+        {
+            switch (origin)
+            {
+                case ExternalCustomizedOrigin.CustomTrail: return GameModeKind.CustomTrail;
+                case ExternalCustomizedOrigin.CoopTrail: return GameModeKind.CoopTrail;
+                case ExternalCustomizedOrigin.VanillaTrail: return GameModeKind.VanillaTrail;
+                case ExternalCustomizedOrigin.SandsOfTime: return GameModeKind.SandsOfTime;
+                default: return GameModeKind.Unknown;
+            }
+        }
+
+        private static bool ExternalOriginMatchesKind(ExternalCustomizedOrigin origin, GameModeKind kind) =>
+            (origin.Origin == ExternalCustomizedOrigin.CustomTrail && kind == GameModeKind.CustomTrail) ||
+            (origin.Origin == ExternalCustomizedOrigin.CoopTrail && kind == GameModeKind.CoopTrail) ||
+            (origin.Origin == ExternalCustomizedOrigin.VanillaTrail && kind == GameModeKind.VanillaTrail) ||
+            (origin.Origin == ExternalCustomizedOrigin.SandsOfTime && kind == GameModeKind.SandsOfTime);
+
+        internal static bool ExternalOriginMatchesEvidence(
+            ExternalCustomizedOrigin origin,
+            GameModeKind kind,
+            int skirmishTrailType,
+            int coopTrailId,
+            int eventTrailType,
+            bool vanillaCustomized,
+            int customizedTrailType,
+            int customizedTrailId)
+        {
+            if (kind == GameModeKind.CoopTrail && coopTrailId > NoCoopTrail)
+                return origin.TrailId + 1 == coopTrailId;
+            if (kind != GameModeKind.VanillaTrail && kind != GameModeKind.SandsOfTime)
+                return true;
+
+            int observedTrailType = eventTrailType >= 0 ? eventTrailType : skirmishTrailType;
+            if (observedTrailType >= 0 && origin.TrailType != observedTrailType)
+                return false;
+            if (!vanillaCustomized)
+                return true;
+            return origin.TrailType == customizedTrailType &&
+                origin.MissionId == customizedTrailId;
+        }
+
+        private static bool TryCaptureVanillaCustomizedTrail(out int trailType, out int trailId)
+        {
+#if SHARED_PRESET_TESTS
+            trailType = NoGameValue;
+            trailId = NoGameValue;
+            return false;
+#else
+            trailType = FRONT_Multiplayer.customizedTrailType;
+            trailId = FRONT_Multiplayer.customizedTrailID;
+            return FRONT_Multiplayer.customizedTrail;
+#endif
+        }
+
+        private static bool TryIsSandsOfTime(GameData gameData)
+        {
+            try
+            {
+                return gameData?.IsSandsOfTime() == true;
+            }
+            catch
+            {
+                return false;
+            }
+        }
+
+        private static bool TryCaptureCustomTrailRestart()
+        {
+#if SHARED_PRESET_TESTS
+            return false;
+#else
+            if (!MainViewModel.viewModelLoaded)
+                return false;
+            try
+            {
+                return MainViewModel.Instance?.HUDIngameMenu?.restartSkirmishMapInfo?.customTrail == true;
+            }
+            catch
+            {
+                return false;
+            }
+#endif
+        }
+
+        private static ExternalCustomizedOrigin CaptureExternalCustomizedOrigin()
+        {
+#if SHARED_PRESET_TESTS
+            return default;
+#else
+            try
+            {
+                Type api = Type.GetType(
+                    "CustomCustomTrail.CustomCustomTrailLaunchOriginApi, CustomCustomTrail",
+                    throwOnError: false);
+                if (api == null)
+                    return default;
+                if (!TryReadStaticInt(api, "ApiVersion", out int apiVersion) ||
+                    !TryReadStaticInt(api, "Origin", out int origin))
+                {
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                }
+                if (apiVersion < MinimumOriginApiVersion || apiVersion > SupportedOriginApiVersion)
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                if (origin == ExternalCustomizedOrigin.None)
+                    return ExternalCustomizedOrigin.AvailableProvider(apiVersion >= 2);
+                bool knownOrigin = origin == ExternalCustomizedOrigin.CustomTrail ||
+                    origin == ExternalCustomizedOrigin.CoopTrail ||
+                    (apiVersion >= 2 && (origin == ExternalCustomizedOrigin.VanillaTrail ||
+                                         origin == ExternalCustomizedOrigin.SandsOfTime));
+                if (!knownOrigin)
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                bool launchPending = false;
+                if (!TryReadStaticInt(api, "TrailType", out int trailType) ||
+                    !TryReadStaticInt(api, "TrailId", out int trailId) ||
+                    !TryReadStaticInt(api, "MissionId", out int missionId) ||
+                    !TryReadStaticBool(api, "RestoredFromSave", out bool restoredFromSave) ||
+                    (apiVersion >= 2 && !TryReadStaticBool(api, "LaunchPending", out launchPending)))
+                {
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                }
+                var result = new ExternalCustomizedOrigin(
+                    origin,
+                    trailType,
+                    trailId,
+                    missionId,
+                    restoredFromSave,
+                    launchPending,
+                    supportsBuiltInOrigins: apiVersion >= 2);
+                if ((result.Origin == ExternalCustomizedOrigin.CustomTrail &&
+                        (result.MissionId < FirstMissionId ||
+                         result.TrailId < FirstCustomTrailId || result.TrailId > LastCustomTrailId)) ||
+                    (result.Origin == ExternalCustomizedOrigin.CoopTrail &&
+                        (result.MissionId < FirstMissionId ||
+                         result.TrailId < FirstCoopTrailId || result.TrailId > LastCoopTrailId ||
+                         result.MissionId > LastCoopMissionId)) ||
+                    (result.Origin == ExternalCustomizedOrigin.VanillaTrail &&
+                        (!IsVanillaTrailType(result.TrailType) || result.TrailId < 0 || result.MissionId < 0)) ||
+                    (result.Origin == ExternalCustomizedOrigin.SandsOfTime &&
+                        (!IsSandsTrailType(result.TrailType) || result.TrailId < 0 || result.MissionId < 0)))
+                {
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                }
+                return result;
+            }
+            catch
+            {
+                // CustomCustomTrail is optional; invalid providers must never enable gameplay mods.
+                return ExternalCustomizedOrigin.InvalidProvider;
+            }
+#endif
+        }
+
+        private static bool TryReadStaticInt(Type type, string name, out int result)
+        {
+            result = NoGameValue;
+            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
+            if (property == null || property.GetIndexParameters().Length != 0)
+                return false;
+            object value = property.GetValue(null);
+            if (value == null)
+                return false;
+            result = Convert.ToInt32(value);
+            return true;
+        }
+
+        private static bool TryReadStaticBool(Type type, string name, out bool result)
+        {
+            result = false;
+            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
+            if (property == null || property.PropertyType != typeof(bool) ||
+                property.GetIndexParameters().Length != 0)
+            {
+                return false;
+            }
+            result = (bool)property.GetValue(null);
+            return true;
         }
 
         public static bool IsRealMultiplayer(bool multiplayerSave = false) =>
             Capture(multiplayerSave).IsRealMultiplayer;
 
-        // Subtype 0 is a normal skirmish. Subtypes 1 and 2 are Vanilla and custom Trails.
+        // Keep the legacy property contract while deriving it from Vanilla's named subtype enum.
         public static bool IsSingleplayerSkirmish(bool multiplayerSave = false) =>
             Capture(multiplayerSave).IsSingleplayerSkirmish;
 
@@ -140,6 +569,52 @@ namespace Shared
         }
     }
 
+    internal readonly struct ExternalCustomizedOrigin
+    {
+        internal const int None = 0;
+        internal const int CustomTrail = 1;
+        internal const int CoopTrail = 2;
+        internal const int VanillaTrail = 3;
+        internal const int SandsOfTime = 4;
+
+        internal static ExternalCustomizedOrigin InvalidProvider =>
+            new ExternalCustomizedOrigin(-1, -1, -1, -1, false, false, isInvalid: true);
+
+        internal static ExternalCustomizedOrigin AvailableProvider(bool supportsBuiltInOrigins) =>
+            new ExternalCustomizedOrigin(
+                None, -1, -1, -1, false, false,
+                supportsBuiltInOrigins: supportsBuiltInOrigins);
+
+        internal ExternalCustomizedOrigin(
+            int origin,
+            int trailType,
+            int trailId,
+            int missionId,
+            bool restoredFromSave,
+            bool launchPending = false,
+            bool isInvalid = false,
+            bool supportsBuiltInOrigins = false)
+        {
+            Origin = origin;
+            TrailType = trailType;
+            TrailId = trailId;
+            MissionId = missionId;
+            RestoredFromSave = restoredFromSave;
+            LaunchPending = launchPending;
+            IsInvalid = isInvalid;
+            SupportsBuiltInOrigins = supportsBuiltInOrigins;
+        }
+
+        internal int Origin { get; }
+        internal int TrailType { get; }
+        internal int TrailId { get; }
+        internal int MissionId { get; }
+        internal bool RestoredFromSave { get; }
+        internal bool LaunchPending { get; }
+        internal bool IsInvalid { get; }
+        internal bool SupportsBuiltInOrigins { get; }
+    }
+
     internal readonly struct PlayerIdentityResolution
     {
         internal PlayerIdentityResolution(int playerId, bool isResolved, string error, string diagnostic)
@@ -609,7 +1084,16 @@ namespace Shared
             int realNetworkGameMembers,
             int gameType,
             int skirmishGameType,
-            int coopTrailId)
+            int skirmishTrailType,
+            int coopTrailId,
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant,
+            int campaignMapId,
+            int eventTrailType,
+            int customizedTrailId,
+            int customizedMissionId,
+            int customizedOriginKind,
+            bool hasConflictingCustomizedOrigin)
         {
             IsRealMultiplayer = isRealMultiplayer;
             IsSingleplayerSkirmishMode = isSingleplayerSkirmishMode;
@@ -629,7 +1113,16 @@ namespace Shared
             RealNetworkGameMembers = realNetworkGameMembers;
             GameType = gameType;
             SkirmishGameType = skirmishGameType;
+            SkirmishTrailType = skirmishTrailType;
             CoopTrailId = coopTrailId;
+            Kind = kind;
+            LaunchVariant = launchVariant;
+            CampaignMapId = campaignMapId;
+            EventTrailType = eventTrailType;
+            CustomizedTrailId = customizedTrailId;
+            CustomizedMissionId = customizedMissionId;
+            CustomizedOriginKind = customizedOriginKind;
+            HasConflictingCustomizedOrigin = hasConflictingCustomizedOrigin;
         }
 
         public bool IsRealMultiplayer { get; }
@@ -650,7 +1143,66 @@ namespace Shared
         public int RealNetworkGameMembers { get; }
         public int GameType { get; }
         public int SkirmishGameType { get; }
+        public int SkirmishTrailType { get; }
         public int CoopTrailId { get; }
+        public GameModeKind Kind { get; }
+        public GameModeLaunchVariant LaunchVariant { get; }
+        public bool IsCustomized => LaunchVariant != GameModeLaunchVariant.Standard;
+        public bool IsMissionContent =>
+            Kind == GameModeKind.Campaign ||
+            Kind == GameModeKind.StandaloneMission ||
+            Kind == GameModeKind.VanillaTrail ||
+            Kind == GameModeKind.CustomTrail ||
+            Kind == GameModeKind.CoopTrail ||
+            Kind == GameModeKind.SandsOfTime;
+        public bool AllowsCustomGameMods =>
+            !HasConflictingCustomizedOrigin && GameModeHelper.AllowsCustomGameMods(Kind, LaunchVariant);
+        public bool AllowsRegularGameplayMods =>
+            !HasConflictingCustomizedOrigin &&
+            GameModeHelper.AllowsRegularGameplayMods(Kind, LaunchVariant);
+        public int CampaignMapId { get; }
+        public int EventTrailType { get; }
+        public int CustomizedTrailId { get; }
+        public int CustomizedMissionId { get; }
+        public int CustomizedOriginKind { get; }
+        public bool HasConflictingCustomizedOrigin { get; }
+
+#if SHARED_PRESET_TESTS
+        internal GameModeSnapshot WithModeEvidenceForTests(
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant,
+            int eventTrailType,
+            bool hasConflictingCustomizedOrigin = false) =>
+            new GameModeSnapshot(
+                IsRealMultiplayer,
+                IsSingleplayerSkirmishMode,
+                IsSingleplayerSkirmish,
+                IsSingleplayerTrail,
+                IsMapEditor,
+                MultiplayerSave,
+                DirectorAvailable,
+                DirectorMultiplayer,
+                DirectorSkirmish,
+                LowLevelNetworked,
+                PlatformMultiplayer,
+                LobbyMembers,
+                RealLobbyMembers,
+                SkirmishLobbyMembers,
+                GameMembers,
+                RealNetworkGameMembers,
+                GameType,
+                SkirmishGameType,
+                SkirmishTrailType,
+                CoopTrailId,
+                kind,
+                launchVariant,
+                CampaignMapId,
+                eventTrailType,
+                CustomizedTrailId,
+                CustomizedMissionId,
+                CustomizedOriginKind,
+                hasConflictingCustomizedOrigin);
+#endif
 
         public string ToDiagnosticString()
         {
@@ -663,7 +1215,13 @@ namespace Shared
                 $"lobbyMembers={LobbyMembers}, realLobbyMembers={RealLobbyMembers}, " +
                 $"skirmishLobbyMembers={SkirmishLobbyMembers}, " +
                 $"gameMembers={GameMembers}, realNetworkGameMembers={RealNetworkGameMembers}, " +
-                $"gameType={GameType}, skirmishGameType={SkirmishGameType}, coopTrailId={CoopTrailId}";
+                $"gameType={GameType}, skirmishGameType={SkirmishGameType}, skirmishTrailType={SkirmishTrailType}, " +
+                $"coopTrailId={CoopTrailId}, kind={Kind}, launchVariant={LaunchVariant}, " +
+                $"allowsCustomGameMods={AllowsCustomGameMods}, " +
+                $"allowsRegularGameplayMods={AllowsRegularGameplayMods}, campaignMapId={CampaignMapId}, " +
+                $"eventTrailType={EventTrailType}, customizedTrailId={CustomizedTrailId}, " +
+                $"customizedMissionId={CustomizedMissionId}, customizedOriginKind={CustomizedOriginKind}, " +
+                $"conflictingCustomizedOrigin={HasConflictingCustomizedOrigin}";
         }
     }
 }

diff --git a/Shared/GameplayFeatureModePolicy.cs b/Shared/GameplayFeatureModePolicy.cs
new file mode 100644
index 00000000..e29dc777
--- /dev/null
+++ b/Shared/GameplayFeatureModePolicy.cs
@@ -0,0 +1,277 @@
+using BepInEx.Logging;
+using System;
+using System.Collections.Generic;
+
+namespace Shared
+{
+    internal enum GameplayFeatureId
+    {
+        BuildingCostTooltip,
+        BuildingLimitEnforcement,
+        UnitCostEnforcement,
+        UnitLimitEnforcement,
+        LordHealthMultipliers,
+        EndlessExtremePowersRecharge,
+        RandomEventsRuntime,
+        ImprovedHunterTargetSelection,
+        ImprovedHunterPathfinding,
+        CastleSpawning,
+        FreeCastlePreview,
+        CastleBlueprints,
+    }
+
+    internal readonly struct GameplayFeatureActivationProfile
+    {
+        internal GameplayFeatureActivationProfile(
+            string modGuid,
+            GameplayFeatureId featureId,
+            GameplayModAllowedContext allowedContexts,
+            bool allowRealMultiplayer)
+        {
+            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
+            FeatureId = featureId;
+            AllowedContexts = allowedContexts;
+            AllowRealMultiplayer = allowRealMultiplayer;
+        }
+
+        internal string ModGuid { get; }
+        internal GameplayFeatureId FeatureId { get; }
+        internal GameplayModAllowedContext AllowedContexts { get; }
+        internal bool AllowRealMultiplayer { get; }
+    }
+
+    /// <summary>
+    /// Typed source of truth for features that intentionally have a narrower
+    /// mode contract than their owning gameplay mod.
+    /// </summary>
+    internal static class GameplayFeatureModePolicy
+    {
+        private const GameplayModAllowedContext NonEditorGameplayContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime;
+
+        private const GameplayModAllowedContext AllRecognizedContexts =
+            NonEditorGameplayContexts |
+            GameplayModAllowedContext.MapEditor |
+            GameplayModAllowedContext.Campaign |
+            GameplayModAllowedContext.StandaloneMission |
+            GameplayModAllowedContext.VanillaTrail |
+            GameplayModAllowedContext.CustomTrail |
+            GameplayModAllowedContext.CoopTrail |
+            GameplayModAllowedContext.SandsOfTime;
+
+        private static readonly object LogSync = new object();
+        private static readonly Dictionary<GameplayFeatureId, bool> LoggedDecisions =
+            new Dictionary<GameplayFeatureId, bool>();
+
+        internal static GameplayFeatureActivationProfile GetProfile(
+            string modGuid,
+            GameplayFeatureId featureId)
+        {
+            string expectedGuid;
+            GameplayModAllowedContext contexts;
+            bool allowRealMultiplayer = true;
+
+            switch (featureId)
+            {
+                case GameplayFeatureId.BuildingCostTooltip:
+                    expectedGuid = "BuildingCosts_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.BuildingLimitEnforcement:
+                    expectedGuid = "BuildingLimit_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.UnitCostEnforcement:
+                    expectedGuid = "UnitCosts_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.UnitLimitEnforcement:
+                    expectedGuid = "UnitLimit_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.LordHealthMultipliers:
+                    expectedGuid = "ExtraFeatures_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.EndlessExtremePowersRecharge:
+                    expectedGuid = "CheatMod_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.RandomEventsRuntime:
+                    expectedGuid = "RandomEvents_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.ImprovedHunterTargetSelection:
+                case GameplayFeatureId.ImprovedHunterPathfinding:
+                    expectedGuid = "ImprovedHunters_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    allowRealMultiplayer = false;
+                    break;
+                case GameplayFeatureId.CastleSpawning:
+                case GameplayFeatureId.FreeCastlePreview:
+                    expectedGuid = "CastlePlanner_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.CastleBlueprints:
+                    expectedGuid = "CastlePlanner_Serp";
+                    contexts = AllRecognizedContexts;
+                    break;
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(featureId), featureId, "Unknown gameplay feature ID.");
+            }
+
+            if (!string.Equals(modGuid, expectedGuid, StringComparison.Ordinal))
+            {
+                throw new ArgumentOutOfRangeException(
+                    nameof(modGuid),
+                    modGuid,
+                    $"Feature {featureId} belongs to mod GUID {expectedGuid}.");
+            }
+
+            return new GameplayFeatureActivationProfile(
+                expectedGuid,
+                featureId,
+                contexts,
+                allowRealMultiplayer);
+        }
+
+        internal static bool IsAllowed(
+            string modGuid,
+            GameplayFeatureId featureId,
+            GameModeSnapshot snapshot)
+        {
+            try
+            {
+                return IsAllowed(GetProfile(modGuid, featureId), snapshot, out _);
+            }
+            catch (ArgumentOutOfRangeException)
+            {
+                // A bad GUID/feature pair is a programming or versioning error;
+                // gameplay hooks must still leave Vanilla unchanged.
+                return false;
+            }
+        }
+
+        internal static bool IsAllowed(
+            GameplayFeatureActivationProfile profile,
+            GameModeSnapshot snapshot,
+            out string reason)
+        {
+            if (snapshot.HasConflictingCustomizedOrigin)
+            {
+                reason = "conflicting-customize-origin";
+                return false;
+            }
+
+            GameplayModAllowedContext context = GameplayModModePolicy.ResolveContext(snapshot);
+            if (context == GameplayModAllowedContext.None)
+            {
+                reason = snapshot.Kind == GameModeKind.Unknown
+                    ? "unknown-fail-closed"
+                    : "owning-mod-context-not-allowed";
+                return false;
+            }
+
+            if ((profile.AllowedContexts & context) != context)
+            {
+                reason = context == GameplayModAllowedContext.MapEditor
+                    ? "feature-not-supported-in-map-editor"
+                    : "feature-context-not-allowed";
+                return false;
+            }
+
+            if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
+            {
+                reason = "feature-not-approved-for-real-multiplayer";
+                return false;
+            }
+
+            reason = "feature-context-allowed";
+            return true;
+        }
+
+        internal static void LogDecisions(
+            ManualLogSource log,
+            string modGuid,
+            GameModeSnapshot snapshot,
+            string source)
+        {
+            foreach (GameplayFeatureActivationProfile feature in GetProfiles(modGuid))
+            {
+                bool allowed = IsAllowed(feature, snapshot, out string reason);
+                if (!RecordDecision(feature.FeatureId, allowed))
+                    continue;
+
+                DebugLogHelper.LogInfo(
+                    log,
+                    $"[{modGuid}] gameplay-feature gate: feature={feature.FeatureId}, source={source}, " +
+                    $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                    $"realMultiplayer={snapshot.IsRealMultiplayer}, modeAllowed={allowed}, " +
+                    $"action={(allowed ? "enabled" : "disabled-by-feature-mode")}, reason={reason}.");
+            }
+        }
+
+        private static bool RecordDecision(GameplayFeatureId featureId, bool allowed)
+        {
+            lock (LogSync)
+            {
+                bool changed = !LoggedDecisions.TryGetValue(featureId, out bool previous) ||
+                    previous != allowed;
+                LoggedDecisions[featureId] = allowed;
+                return changed;
+            }
+        }
+
+#if SHARED_PRESET_TESTS
+        internal static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
+            RecordDecision(featureId, allowed);
+
+        internal static void ResetLoggedDecisionsForTests()
+        {
+            lock (LogSync)
+                LoggedDecisions.Clear();
+        }
+#endif
+
+        private static IEnumerable<GameplayFeatureActivationProfile> GetProfiles(string modGuid)
+        {
+            switch (modGuid)
+            {
+                case "BuildingCosts_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingCostTooltip);
+                    break;
+                case "BuildingLimit_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingLimitEnforcement);
+                    break;
+                case "UnitCosts_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.UnitCostEnforcement);
+                    break;
+                case "UnitLimit_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.UnitLimitEnforcement);
+                    break;
+                case "ExtraFeatures_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.LordHealthMultipliers);
+                    break;
+                case "CheatMod_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.EndlessExtremePowersRecharge);
+                    break;
+                case "RandomEvents_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.RandomEventsRuntime);
+                    break;
+                case "ImprovedHunters_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection);
+                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding);
+                    break;
+                case "CastlePlanner_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.CastleSpawning);
+                    yield return GetProfile(modGuid, GameplayFeatureId.FreeCastlePreview);
+                    yield return GetProfile(modGuid, GameplayFeatureId.CastleBlueprints);
+                    break;
+            }
+        }
+    }
+}

diff --git a/Shared/GameplayModActivationGate.cs b/Shared/GameplayModActivationGate.cs
new file mode 100644
index 00000000..c7c6e926
--- /dev/null
+++ b/Shared/GameplayModActivationGate.cs
@@ -0,0 +1,212 @@
+using BepInEx.Logging;
+using System;
+#if !SHARED_PRESET_TESTS
+using R3;
+using SHCDESE.EventAPI;
+using SHCDESE.EventAPI.MapLoader;
+#endif
+
+namespace Shared
+{
+    /// <summary>
+    /// Caches the current map policy for one mod assembly. Shared sources are linked
+    /// into every mod, so one mod can never accidentally change another mod's state.
+    /// </summary>
+    internal static class GameplayModActivationGate
+    {
+        private static ManualLogSource log;
+        private static GameplayModActivationProfile profile;
+        private static Func<bool> configuredEnabledProvider;
+        private static GameModeSnapshot snapshot;
+        private static volatile bool isAllowed;
+        private static bool initialized;
+        private static bool hasAuthoritativeLoadEvidence;
+        private static GameModeSnapshot authoritativeLoadSnapshot;
+#if !SHARED_PRESET_TESTS
+        private static IDisposable mapLoadSubscription;
+        private static IDisposable loadSaveSubscription;
+        private static IDisposable mapStartSubscription;
+        private static IDisposable mapUnloadSubscription;
+#endif
+
+        internal static event Action<bool> StateChanged;
+
+        internal static bool IsAllowed => isAllowed;
+        internal static GameModeSnapshot Snapshot => snapshot;
+        internal static bool IsEnabled(bool configuredEnabled) => configuredEnabled && IsAllowed;
+
+        internal static void Initialize(
+            ManualLogSource logger,
+            string modGuid,
+            string displayName,
+            Func<bool> isConfiguredEnabled)
+        {
+            if (initialized)
+                return;
+
+            log = logger;
+            profile = GameplayModModePolicy.GetProfile(modGuid, displayName);
+            configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
+
+#if !SHARED_PRESET_TESTS
+            // Register before the mod's own handlers. Castle spawning and similar
+            // native work already begins in OnStartMap(Pre).
+            mapLoadSubscription = MapLoaderR3EventHooks.OnLoadMap.Observable
+                .Subscribe(args => UpdateLoad(GameModeHelper.Capture(args), $"OnLoadMap({args.Phase})"));
+            loadSaveSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
+                .Where(args => args.Phase == EventHookPhase.Post)
+                .Subscribe(args => UpdateLoad(GameModeHelper.Capture(args), $"OnLoadSave({args.Phase})"));
+            mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
+                .Subscribe(args => UpdateStart(GameModeHelper.Capture(args), $"OnStartMap({args.Phase})"));
+            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
+                .Subscribe(args =>
+                {
+                    if (args.Phase == EventHookPhase.Pre)
+                        Reset("OnUnloadMap(Pre)");
+                });
+#endif
+            initialized = true;
+            GameModeSnapshot current = GameModeHelper.Capture();
+            if (current.Kind == GameModeKind.MapEditor)
+                UpdateLoad(current, "initial-current-editor");
+            else
+                LogTransition("initialization");
+        }
+
+        private static void UpdateLoad(GameModeSnapshot next, string source)
+        {
+            if (hasAuthoritativeLoadEvidence)
+                next = MergeWithAuthoritativeLoad(next);
+            if (HasAuthoritativeLoadEvidence(next))
+            {
+                authoritativeLoadSnapshot = next;
+                hasAuthoritativeLoadEvidence = true;
+            }
+            Update(next, source);
+        }
+
+        private static bool HasAuthoritativeLoadEvidence(GameModeSnapshot candidate) =>
+            candidate.Kind == GameModeKind.MapEditor ||
+            candidate.CampaignMapId > 0 ||
+            candidate.EventTrailType >= 0 ||
+            (candidate.Kind == GameModeKind.CoopTrail && candidate.CoopTrailId > 0) ||
+            (candidate.Kind == GameModeKind.CustomTrail &&
+             candidate.SkirmishGameType ==
+                 (int)global::Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL) ||
+            (candidate.IsMissionContent && candidate.IsCustomized);
+
+        private static void UpdateStart(GameModeSnapshot next, string source)
+        {
+            if (hasAuthoritativeLoadEvidence)
+                next = MergeWithAuthoritativeLoad(next);
+            Update(next, source);
+        }
+
+        private static GameModeSnapshot MergeWithAuthoritativeLoad(GameModeSnapshot next)
+        {
+            if (authoritativeLoadSnapshot.Kind == GameModeKind.MapEditor)
+                return authoritativeLoadSnapshot;
+            if ((next.Kind == GameModeKind.CustomGame || next.Kind == GameModeKind.Unknown) &&
+                authoritativeLoadSnapshot.IsMissionContent)
+            {
+                return authoritativeLoadSnapshot;
+            }
+            if (next.Kind == authoritativeLoadSnapshot.Kind &&
+                authoritativeLoadSnapshot.IsCustomized && !next.IsCustomized)
+            {
+                return authoritativeLoadSnapshot;
+            }
+            return next;
+        }
+
+        private static void Update(GameModeSnapshot next, string source)
+        {
+            bool previousAllowed = isAllowed;
+            bool changed = next.Kind != snapshot.Kind ||
+                next.LaunchVariant != snapshot.LaunchVariant ||
+                next.CustomizedTrailId != snapshot.CustomizedTrailId ||
+                next.CustomizedMissionId != snapshot.CustomizedMissionId ||
+                next.IsRealMultiplayer != snapshot.IsRealMultiplayer ||
+                next.HasConflictingCustomizedOrigin != snapshot.HasConflictingCustomizedOrigin;
+            snapshot = next;
+            isAllowed = GameplayModModePolicy.IsAllowed(profile, next, out _);
+            if (changed)
+                LogTransition(source);
+            if (previousAllowed != isAllowed)
+                NotifyStateChanged(isAllowed);
+        }
+
+        private static void Reset(string source)
+        {
+            bool changed = snapshot.Kind != GameModeKind.Unknown ||
+                snapshot.LaunchVariant != GameModeLaunchVariant.Standard;
+            bool previousAllowed = isAllowed;
+            isAllowed = false;
+            snapshot = default;
+            hasAuthoritativeLoadEvidence = false;
+            authoritativeLoadSnapshot = default;
+            if (changed)
+                LogTransition(source);
+            if (previousAllowed)
+                NotifyStateChanged(false);
+        }
+
+        private static void NotifyStateChanged(bool allowed)
+        {
+            Delegate[] handlers = StateChanged?.GetInvocationList();
+            if (handlers == null)
+                return;
+
+            foreach (Delegate handler in handlers)
+            {
+                try { ((Action<bool>)handler)(allowed); }
+                catch (Exception ex)
+                {
+                    DebugLogHelper.LogError(
+                        log,
+                        $"[{profile.DisplayName}] gameplay-mod gate listener failed closed: {ex}");
+                }
+            }
+        }
+
+        private static void LogTransition(string source)
+        {
+            bool configuredEnabled = ReadConfiguredEnabled();
+            bool effectiveEnabled = configuredEnabled && IsAllowed;
+            GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
+            string action = effectiveEnabled
+                ? "enabled"
+                : !IsAllowed ? "disabled-by-mode" : "restriction-lifted-setting-disabled";
+            DebugLogHelper.LogInfo(
+                log,
+                $"[{profile.DisplayName}] gameplay-mod gate: modGuid={profile.ModGuid}, source={source}, " +
+                $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                $"customized={snapshot.IsCustomized}, customizedOrigin={snapshot.CustomizedOriginKind}, " +
+                $"modeAllowed={IsAllowed}, configuredEnabled={configuredEnabled}, " +
+                $"effectiveEnabled={effectiveEnabled}, action={action}, reason={reason}.");
+            GameplayFeatureModePolicy.LogDecisions(log, profile.ModGuid, snapshot, source);
+        }
+
+        private static bool ReadConfiguredEnabled()
+        {
+            try { return configuredEnabledProvider?.Invoke() == true; }
+            catch (Exception ex)
+            {
+                DebugLogHelper.LogError(log, $"[{profile.DisplayName}] EnableMod provider failed closed: {ex}");
+                return false;
+            }
+        }
+
+#if SHARED_PRESET_TESTS
+        internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
+        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => UpdateLoad(next, "test-load");
+        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => UpdateStart(next, "test-start");
+        internal static void ResetForTests()
+        {
+            profile = GameplayModModePolicy.GetProfile("ExtraFeatures_Serp", "Extra Features");
+            configuredEnabledProvider = () => true;
+            Reset("test-reset");
+        }
+#endif
+    }
+}

diff --git a/Shared/GameplayModModePolicy.cs b/Shared/GameplayModModePolicy.cs
new file mode 100644
index 00000000..be28505f
--- /dev/null
+++ b/Shared/GameplayModModePolicy.cs
@@ -0,0 +1,139 @@
+using System;
+
+namespace Shared
+{
+    [Flags]
+    internal enum GameplayModAllowedContext
+    {
+        None = 0,
+        CustomGame = 1 << 0,
+        CustomizedVanillaTrail = 1 << 1,
+        CustomizedCustomTrail = 1 << 2,
+        CustomizedCoopTrail = 1 << 3,
+        CustomizedSandsOfTime = 1 << 4,
+        MapEditor = 1 << 5,
+        Campaign = 1 << 6,
+        StandaloneMission = 1 << 7,
+        VanillaTrail = 1 << 8,
+        CustomTrail = 1 << 9,
+        CoopTrail = 1 << 10,
+        SandsOfTime = 1 << 11,
+    }
+
+    internal readonly struct GameplayModActivationProfile
+    {
+        internal GameplayModActivationProfile(
+            string modGuid,
+            string displayName,
+            GameplayModAllowedContext allowedContexts)
+        {
+            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
+            DisplayName = string.IsNullOrWhiteSpace(displayName) ? modGuid : displayName;
+            AllowedContexts = allowedContexts;
+        }
+
+        internal string ModGuid { get; }
+        internal string DisplayName { get; }
+        internal GameplayModAllowedContext AllowedContexts { get; }
+    }
+
+    /// <summary>Single typed source of truth for mode permissions of regular gameplay mods.</summary>
+    internal static class GameplayModModePolicy
+    {
+        private const GameplayModAllowedContext RegularContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime |
+            GameplayModAllowedContext.MapEditor;
+
+        internal static GameplayModActivationProfile GetProfile(string modGuid, string displayName)
+        {
+            switch (modGuid)
+            {
+                case "BuildingCosts_Serp":
+                case "BuildingLimit_Serp":
+                case "CastlePlanner_Serp":
+                case "CheatMod_Serp":
+                case "ExtraFeatures_Serp":
+                case "ExtremePowers_Serp":
+                case "ImprovedHunters_Serp":
+                case "RandomEvents_Serp":
+                case "StartConditions_Serp":
+                case "UnitCosts_Serp":
+                case "UnitLimit_Serp":
+                    return Create(modGuid, displayName);
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(modGuid), modGuid, "Unknown gameplay mod GUID.");
+            }
+        }
+
+        internal static bool IsAllowed(
+            GameplayModActivationProfile profile,
+            GameModeSnapshot snapshot,
+            out string reason)
+        {
+            if (snapshot.HasConflictingCustomizedOrigin)
+            {
+                reason = "conflicting-customize-origin";
+                return false;
+            }
+
+            GameplayModAllowedContext context = ResolveContext(snapshot);
+            if (context == GameplayModAllowedContext.None)
+            {
+                reason = snapshot.Kind == GameModeKind.Unknown
+                    ? "unknown-fail-closed"
+                    : snapshot.IsMissionContent ? "direct-mission-content" : "mode-not-allowed";
+                return false;
+            }
+
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/UnitLimit.diff).
