using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("disabled recorder is a no-op", DisabledRecorderIsNoOp);
        Run("ring wraps and snapshot stays bounded", RingWraps);
        Run("nested and incomplete scopes remain identifiable", NestedAndIncompleteScopes);
        Run("parallel writers retain valid records", ParallelWriters);
        Run("snapshots alternate and clean shutdown is marked", SnapshotsAlternate);
        Run("unchanged snapshots do not rewrite files", UnchangedSnapshotsAreSkipped);
        Run("records arriving after a snapshot remain dirty", LaterRecordsRemainDirty);
        Run("concurrent records survive dirty snapshot clearing", ConcurrentRecordsSurviveDirtyClearing);
        Run("only five game starts are retained across mods", RetentionKeepsFiveGameStarts);
        Run("reused process IDs remain separate game starts", ReusedProcessIdsRemainSeparate);
        Run("legacy diagnostics are pruned with current sessions", LegacySessionsArePruned);
        Run("parallel mod writers keep their snapshot pairs", ParallelModWritersKeepSnapshotPairs);
        Run("persistence failure never escapes", PersistenceFailureIsContained);
        Run("summary output is rate-controlled", SummaryIsRateControlled);
        Run("unexpected signatures are logged once", UnexpectedSignaturesAreLoggedOnce);

        Console.WriteLine(failures == 0 ? "PASS" : $"FAIL: {failures} test(s)");
        return failures == 0 ? 0 : 1;
    }

    private static void DisabledRecorderIsNoOp()
    {
        string root = NewRoot();
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        using (var recorder = NewRecorder(false, root, "Disabled"))
        {
            recorder.Record("ignored");
            Assert(recorder.SequenceForTests == 0, "disabled recorder advanced its sequence");
            Assert(!Directory.Exists(directory), "disabled recorder created a directory");
        }

        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "Existing-pid123-0.txt");
        File.WriteAllText(existing, "preserved");
        using (var recorder = NewRecorder(false, root, "Disabled"))
        {
            recorder.WriteSnapshotForTests();
            Assert(File.ReadAllText(existing) == "preserved", "disabled recorder changed existing diagnostics");
        }
    }

    private static void RingWraps()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Wrap"))
        {
            for (int index = 0; index < 400; index++)
                recorder.Record("point", index);
            recorder.WriteSnapshotForTests();

            string text = ReadNewest(recorder.DirectoryForTests);
            Assert(text.Contains("breadcrumbSequence=400"), "unexpected final sequence");
            Assert(text.Contains("overwritten=144"), "overwritten count missing");
            Assert(CountLines(text, "seq=") == 256, "snapshot did not contain exactly the ring capacity");
        }
    }

    private static void NestedAndIncompleteScopes()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Scopes"))
        {
            CrashBreadcrumbScope outer = recorder.Enter("outer", 1);
            using (CrashBreadcrumbScope inner = recorder.Enter("inner", 2))
                inner.Complete(7);
            recorder.WriteSnapshotForTests();

            string text = ReadNewest(recorder.DirectoryForTests);
            string active = Section(text, "[active-scopes]", "[counters]");
            Assert(active.Contains("operation=outer"), "outer scope was not restored");
            Assert(!active.Contains("operation=inner"), "completed inner scope remained active");
            outer.Complete(3);
        }
    }

    private static void ParallelWriters()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Parallel"))
        {
            Task[] tasks = Enumerable.Range(0, 4)
                .Select(worker => Task.Run(() =>
                {
                    for (int index = 0; index < 250; index++)
                    {
                        using (CrashBreadcrumbScope scope = recorder.Enter("parallel", worker, index))
                            scope.Complete();
                    }
                }))
                .ToArray();
            Task.WaitAll(tasks);
            Assert(recorder.SequenceForTests == 2000, "parallel enter/exit sequence was lost");
        }
    }

    private static void SnapshotsAlternate()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Alternating"))
        {
            recorder.Record("first");
            recorder.WriteSnapshotForTests();
            recorder.Record("second");
            recorder.WriteSnapshotForTests();
            Assert(Directory.GetFiles(recorder.DirectoryForTests, "*.txt").Length == 2, "snapshot slots did not alternate");
            recorder.MarkCleanShutdown();
            Assert(
                Directory.GetFiles(recorder.DirectoryForTests, "*.txt")
                    .Any(path => File.ReadAllText(path).Contains("state=clean-shutdown")),
                "clean shutdown marker missing");
        }
    }

    private static void UnchangedSnapshotsAreSkipped()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Dirty"))
        {
            recorder.WriteSnapshotForTests();
            long firstSnapshot = recorder.SnapshotSequenceForTests;
            recorder.WriteSnapshotForTests();
            Assert(firstSnapshot == 1, "the initial empty snapshot was not persisted");
            Assert(recorder.SnapshotSequenceForTests == firstSnapshot,
                "an unchanged recorder rewrote its snapshot");
        }
    }

    private static void LaterRecordsRemainDirty()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Later"))
        {
            recorder.Record("first");
            recorder.WriteSnapshotForTests();
            long firstSnapshot = recorder.SnapshotSequenceForTests;
            recorder.Record("second");
            recorder.WriteSnapshotForTests();
            Assert(recorder.SnapshotSequenceForTests == firstSnapshot + 1,
                "a later record did not trigger another snapshot");
            Assert(ReadNewest(recorder.DirectoryForTests).Contains("breadcrumbSequence=2"),
                "the later record was missing from the persisted snapshot");
        }
    }

    private static void ConcurrentRecordsSurviveDirtyClearing()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "ConcurrentDirty"))
        {
            Task writer = Task.Run(() =>
            {
                for (int index = 0; index < 2000; index++)
                    recorder.Record("concurrent", index);
            });
            Task snapshots = Task.Run(() =>
            {
                while (!writer.IsCompleted)
                    recorder.WriteSnapshotForTests();
            });
            Task.WaitAll(writer, snapshots);

            recorder.WriteSnapshotForTests();
            long settledSnapshot = recorder.SnapshotSequenceForTests;
            string text = ReadNewest(recorder.DirectoryForTests);
            Assert(text.Contains("breadcrumbSequence=2000"),
                "a record concurrent with snapshot persistence was treated as already clean");
            recorder.WriteSnapshotForTests();
            Assert(recorder.SnapshotSequenceForTests == settledSnapshot,
                "the recorder did not become clean after persisting all concurrent records");
        }
    }

    private static void RetentionKeepsFiveGameStarts()
    {
        string root = NewRoot();
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        Directory.CreateDirectory(directory);
        for (int session = 1; session <= 6; session++)
        {
            long startedTicks = DateTime.UtcNow.AddHours(-7 + session).Ticks;
            for (int slot = 0; slot <= 1; slot++)
            {
                string mod = slot == 0 ? "RetentionA" : "RetentionB";
                string path = Path.Combine(directory, $"{mod}-pid{session}-start{startedTicks}-{slot}.txt");
                File.WriteAllText(path, session.ToString());
            }
        }

        using (var recorder = NewRecorder(true, root, "RetentionCurrent"))
        {
            recorder.WriteSnapshotForTests();
            Assert(Directory.GetFiles(directory, "RetentionA-pid1-*.txt").Length == 0,
                "oldest game start was retained");
            Assert(Directory.GetFiles(directory, "RetentionB-pid2-*.txt").Length == 0,
                "second oldest game start was retained");
            Assert(Directory.GetFiles(directory, "RetentionA-pid3-*.txt").Length == 1 &&
                Directory.GetFiles(directory, "RetentionB-pid3-*.txt").Length == 1,
                "retained game start lost one mod");
            Assert(Directory.GetFiles(directory, "RetentionCurrent-*.txt").Length == 1,
                "current game start was not retained");
        }
    }

    private static void LegacySessionsArePruned()
    {
        string root = NewRoot();
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        Directory.CreateDirectory(directory);
        for (int session = 1; session <= 6; session++)
        {
            for (int slot = 0; slot <= 1; slot++)
            {
                string path = Path.Combine(directory, $"LegacyMod{slot}-pid{session}-{slot}.txt");
                File.WriteAllText(path, session.ToString());
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-7 + session));
            }
        }
        string unrelated = Path.Combine(directory, "notes.txt");
        File.WriteAllText(unrelated, "keep");

        using (var recorder = NewRecorder(true, root, "NewMod"))
            recorder.WriteSnapshotForTests();

        Assert(Directory.GetFiles(directory, "*-pid1-*.txt").Length == 0,
            "oldest legacy game start was retained");
        Assert(Directory.GetFiles(directory, "*-pid2-*.txt").Length == 0,
            "second oldest legacy game start was retained");
        Assert(Directory.GetFiles(directory, "*-pid3-*.txt").Length == 2,
            "a retained legacy game start lost one mod");
        Assert(File.ReadAllText(unrelated) == "keep", "unrelated file was changed");
    }

    private static void ReusedProcessIdsRemainSeparate()
    {
        string root = NewRoot();
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        Directory.CreateDirectory(directory);
        for (int session = 1; session <= 6; session++)
        {
            long startedTicks = DateTime.UtcNow.AddHours(-7 + session).Ticks;
            string path = Path.Combine(directory, $"Reuse-pid123-start{startedTicks}-0.txt");
            File.WriteAllText(path, session.ToString());
        }

        using (var recorder = NewRecorder(true, root, "Current"))
            recorder.WriteSnapshotForTests();

        string[] previous = Directory.GetFiles(directory, "Reuse-*.txt");
        Assert(previous.Length == 4, "reused process IDs were grouped into one game start");
        Assert(previous.Select(File.ReadAllText).OrderBy(value => value).SequenceEqual(new[] { "3", "4", "5", "6" }),
            "retention removed a newer game start with a reused process ID");
    }

    private static void ParallelModWritersKeepSnapshotPairs()
    {
        string root = NewRoot();
        string[] mods = { "ModA", "ModB", "ModC", "ModD" };
        Task[] tasks = mods.Select(mod => Task.Run(() =>
        {
            using (var recorder = NewRecorder(true, root, mod))
            {
                recorder.Record("first");
                recorder.WriteSnapshotForTests();
                recorder.Record("second");
                recorder.WriteSnapshotForTests();
            }
        })).ToArray();
        Task.WaitAll(tasks);

        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        foreach (string mod in mods)
            Assert(Directory.GetFiles(directory, mod + "-*.txt").Length == 2,
                mod + " lost a snapshot slot during parallel writes");
    }

    private static void PersistenceFailureIsContained()
    {
        string rootFile = Path.Combine(NewRoot(), "not-a-directory");
        Directory.CreateDirectory(Path.GetDirectoryName(rootFile));
        File.WriteAllText(rootFile, "x");
        var messages = new List<string>();
        using (var recorder = new CrashBreadcrumbRecorder(
            true,
            rootFile,
            "Failure",
            "Failure",
            "1",
            messages.Add,
            TimeSpan.Zero))
        {
            recorder.Record("still-safe");
            recorder.WriteSnapshotForTests();
            Assert(recorder.SequenceForTests == 1, "in-memory diagnostics stopped after an I/O failure");
            Assert(messages.Count == 1, "persistence failure was not reported exactly once");
        }
    }

    private static void SummaryIsRateControlled()
    {
        string root = NewRoot();
        var messages = new List<string>();
        using (var recorder = new CrashBreadcrumbRecorder(
            true,
            root,
            "Summary",
            "Summary",
            "1",
            messages.Add,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(1)))
        {
            recorder.Record("event");
            Thread.Sleep(5);
            recorder.WriteSnapshotForTests();
            recorder.WriteSnapshotForTests();
            Assert(messages.Count(message => message.Contains("summary")) == 1, "summary was not rate-controlled");
        }
    }

    private static void UnexpectedSignaturesAreLoggedOnce()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(true, root, "Unexpected"))
        {
            Assert(recorder.TryRegisterUnexpected("same"), "first signature was suppressed");
            Assert(!recorder.TryRegisterUnexpected("same"), "duplicate signature was not suppressed");
            Assert(recorder.TryRegisterUnexpected("different"), "different signature was suppressed");
        }
    }

    private static CrashBreadcrumbRecorder NewRecorder(bool enabled, string root, string guid) =>
        new CrashBreadcrumbRecorder(enabled, root, guid, guid, "1", _ => { }, TimeSpan.Zero);

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(),
        "SerpsCrashBreadcrumbTests",
        Guid.NewGuid().ToString("N"));

    private static string ReadNewest(string directory) => File.ReadAllText(
        Directory.GetFiles(directory, "*.txt")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First());

    private static int CountLines(string text, string prefix) =>
        text.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
            .Count(line => line.StartsWith(prefix, StringComparison.Ordinal));

    private static string Section(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        int endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        return text.Substring(startIndex, endIndex - startIndex);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS: " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine("FAIL: " + name + " - " + exception.Message);
        }
    }
}
