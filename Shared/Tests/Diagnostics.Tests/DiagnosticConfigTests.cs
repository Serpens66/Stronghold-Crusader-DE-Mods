using Shared;
using BepInEx.Configuration;
using BepInEx.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

[TestClass]
[DoNotParallelize]
public sealed class DiagnosticConfigTests
{
    [TestMethod]
    public void DefaultConfigurationIsSilentAndCreatesNoCrashRecorder()
    {
        WithConfig(false, config => {
            using (var log = new ManualLogSource("default-fixture"))
            {
                int messages = 0;
                log.LogEvent += (_, args) => messages++;
                DebugLogHelper.InitializeDiagnostics(config, log);
                Assert.IsFalse(DebugLogHelper.DiagnosticsEnabled);
                Assert.IsFalse(DebugLogHelper.CrashBreadcrumbsEnabled);
                CrashBreadcrumbDiagnostics.Initialize(log, "fixture", "fixture", "1");
                Assert.IsFalse(CrashBreadcrumbDiagnostics.IsEnabled);
                Assert.IsNull(typeof(CrashBreadcrumbDiagnostics).GetField("recorder", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
                using (CrashBreadcrumbDiagnostics.Enter("unused")) { CrashBreadcrumbDiagnostics.Record("unused"); }
                DebugLogHelper.LogDebug(log, "must be silent");
                Assert.AreEqual(0, messages);
                RuntimeHealth.Failure(null, "null-logger", true, "Explicitly absent source must remain silent.");
                Assert.AreEqual(0, messages);
                // Info is routine release logging and stays visible without diagnostics.
                DebugLogHelper.LogInfo(log, "info remains visible");
                Assert.AreEqual(1, messages);
                DebugLogHelper.LogWarning(log, "warning remains visible");
                DebugLogHelper.LogError(log, "error remains visible");
                Assert.AreEqual(3, messages);
            }
        });
    }
    [TestMethod]
    public void CrashOptInIsIndependentOfRoutineLogsAndDebugListeners()
    {
        WithConfig(true, config => {
            using (var log = new ManualLogSource("opt-in-fixture")) {
                DebugLogHelper.InitializeDiagnostics(config, log);
                Assert.IsFalse(DebugLogHelper.DiagnosticsEnabled);
                Assert.IsTrue(DebugLogHelper.CrashBreadcrumbsEnabled);
                // Do not start a real recorder here; the recorder's persistence suite tests that boundary.
            }
        });
    }
    [TestMethod]
    public void BreadcrumbPersistenceFailureIsAnUnconditionalSingleErrorAndStopsWrites()
    {
        WithConfig(true, config => {
            string path = Path.Combine(Path.GetTempPath(), "blocked-crash-" + Guid.NewGuid());
            File.WriteAllText(path, "not a directory");
            try {
                using (var log = new ManualLogSource("breadcrumb-failure-fixture")) {
                    var messages = new System.Collections.Generic.List<LogEventArgs>();
                    log.LogEvent += (_, args) => messages.Add(args);
                    DebugLogHelper.InitializeDiagnostics(config, log);
                    Assert.IsFalse(DebugLogHelper.DiagnosticsEnabled);
                    using (var recorder = new CrashBreadcrumbRecorder(true, path, "fixture", "fixture", "1", null, null, null,
                        error => CrashBreadcrumbDiagnostics.ReportPersistenceFailure(log, "fixture", error))) {
                        recorder.Record("safe"); recorder.WriteSnapshotForTests(); recorder.WriteSnapshotForTests();
                        Assert.AreEqual(0L, recorder.SnapshotSequenceForTests);
                        Assert.IsNull(typeof(CrashBreadcrumbRecorder).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recorder));
                    }
                    Assert.AreEqual(1, messages.Count); Assert.AreEqual(LogLevel.Error, messages[0].Level);
                    Assert.IsTrue(messages[0].Data.ToString().Contains("no further writes"));
                    log.LogEvent += (_, args) => { throw new InvalidOperationException("listener"); };
                    using (var recorder = new CrashBreadcrumbRecorder(true, path, "fixture2", "fixture2", "1", null, null, null,
                        error => CrashBreadcrumbDiagnostics.ReportPersistenceFailure(log, "fixture2", error))) {
                        recorder.Record("still-safe"); recorder.WriteSnapshotForTests();
                    }
                }
            } finally { File.Delete(path); }
        });
    }
    [TestMethod]
    public void PersistenceFailureAfterAWriteDoesNotRetryAfterDirectoryRecovers()
    {
        string root = Path.Combine(Path.GetTempPath(), "late-crash-failure-" + Guid.NewGuid());
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        int failures = 0;
        try {
            using (var recorder = new CrashBreadcrumbRecorder(true, root, "fixture", "fixture", "1", null,
                TimeSpan.FromHours(1), null, error => failures++)) {
                recorder.Record("first"); recorder.WriteSnapshotForTests();
                Assert.AreEqual(1L, recorder.SnapshotSequenceForTests);
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory); File.WriteAllText(directory, "blocked");
                recorder.Record("second"); recorder.WriteSnapshotForTests();
                Assert.AreEqual(1, failures);
                long attempted = recorder.SnapshotSequenceForTests;
                File.Delete(directory); Directory.CreateDirectory(directory);
                recorder.Record("third"); recorder.WriteSnapshotForTests();
                Assert.AreEqual(attempted, recorder.SnapshotSequenceForTests);
                Assert.AreEqual(0, Directory.GetFiles(directory).Length);
                Assert.AreEqual(1, failures);
            }
        } finally {
            if (File.Exists(directory)) File.Delete(directory);
            if (Directory.Exists(directory)) { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }
    [TestMethod]
    public void WritersAlreadyWaitingAtTheSerializationLockDoNotRetryAfterFailure()
    {
        string root = Path.Combine(Path.GetTempPath(), "queued-crash-failure-" + Guid.NewGuid());
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        int failures = 0;
        try {
            using (var recorder = new CrashBreadcrumbRecorder(true, root, "fixture", "fixture", "1", null,
                TimeSpan.FromHours(1), null, error => System.Threading.Interlocked.Increment(ref failures))) {
                recorder.Record("pending");
                // This lock is the recorder's disk-write serialization boundary. Hold
                // it so all callers pass the outer guard before the first write fails.
                object gate = typeof(CrashBreadcrumbRecorder).GetField("snapshotWriteRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recorder);
                var threads = Enumerable.Range(0, 4).Select(_ => new System.Threading.Thread(() => recorder.WriteSnapshotForTests())).ToArray();
                System.Threading.Monitor.Enter(gate);
                try {
                    foreach (var thread in threads) { thread.IsBackground = true; thread.Start(); }
                    Assert.IsTrue(System.Threading.SpinWait.SpinUntil(() => threads.All(t =>
                        (t.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0), TimeSpan.FromSeconds(5)), "Writers did not reach the serialization lock.");
                    Directory.Delete(directory); File.WriteAllText(directory, "blocked");
                } finally { System.Threading.Monitor.Exit(gate); }
                foreach (var thread in threads) Assert.IsTrue(thread.Join(5000), "Writer did not complete.");
                Assert.AreEqual(1, failures);
                Assert.AreEqual(1L, recorder.SnapshotSequenceForTests, "Queued callers attempted another snapshot after persistence failed.");
            }
        } finally {
            if (File.Exists(directory)) File.Delete(directory);
            if (Directory.Exists(directory)) { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }
    [TestMethod]
    public void ReportsBeforeOrWithoutInitializationStillReachTheLog()
    {
        // Mods that never call InitializeDiagnostics still link source-level reports.
        WithConfig(false, _ => {
            var listener = new CapturingListener();
            BepInEx.Logging.Logger.Listeners.Add(listener);
            try {
                Assert.IsFalse(RuntimeHealth.IsInitialized);
                RuntimeHealth.Report("uninitialized-report", true, "failure without a plugin logger");
                Assert.AreEqual(1, listener.Errors.Count(m => m.Contains("feature=uninitialized-report;")));
            } finally { BepInEx.Logging.Logger.Listeners.Remove(listener); }
        });
    }
    private sealed class CapturingListener : ILogListener
    {
        internal readonly System.Collections.Generic.List<string> Errors = new System.Collections.Generic.List<string>();
        public void LogEvent(object sender, LogEventArgs eventArgs)
        { if (eventArgs.Level == LogLevel.Error) Errors.Add(Convert.ToString(eventArgs.Data)); }
        public void Dispose() { }
    }
    private static void WithConfig(bool enabled, Action<ConfigFile> test)
    {
        var types = new[] { typeof(DebugLogHelper), typeof(RuntimeHealth), typeof(CrashBreadcrumbDiagnostics) };
        var fields = types.SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.NonPublic)).Where(f => !f.IsInitOnly).ToArray();
        var state = fields.ToDictionary(f => f, f => f.GetValue(null));
        string path = Path.Combine(Path.GetTempPath(), "crash-config-" + Guid.NewGuid() + ".cfg");
        try {
            foreach (var field in fields) field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
            if (enabled) config.Bind("Diagnostics", "CrashBreadcrumbs", true);
            test(config);
        } finally { foreach (var item in state) item.Key.SetValue(null, item.Value); if (File.Exists(path)) File.Delete(path); }
    }
}
