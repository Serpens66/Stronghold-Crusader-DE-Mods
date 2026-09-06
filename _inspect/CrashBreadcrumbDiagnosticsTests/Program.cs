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
        Run("only three sessions are retained", RetentionKeepsThreeSessions);
        Run("persistence failure never escapes", PersistenceFailureIsContained);
        Run("summary output is rate-controlled", SummaryIsRateControlled);
        Run("unexpected signatures are logged once", UnexpectedSignaturesAreLoggedOnce);

        Console.WriteLine(failures == 0 ? "PASS" : $"FAIL: {failures} test(s)");
        return failures == 0 ? 0 : 1;
    }

    private static void DisabledRecorderIsNoOp()
    {
        string root = NewRoot();
        using (var recorder = NewRecorder(false, root, "Disabled"))
        {
            recorder.Record("ignored");
            Assert(recorder.SequenceForTests == 0, "disabled recorder advanced its sequence");
            Assert(!Directory.Exists(recorder.DirectoryForTests), "disabled recorder created a directory");
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

    private static void RetentionKeepsThreeSessions()
    {
        string root = NewRoot();
        string directory = Path.Combine(root, "SerpsModsDiagnostics");
        Directory.CreateDirectory(directory);
        for (int session = 1; session <= 4; session++)
        {
            string path = Path.Combine(directory, $"Retention-pid{session}-0.txt");
            File.WriteAllText(path, session.ToString());
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10 + session));
        }

        using (var recorder = NewRecorder(true, root, "Retention"))
        {
            recorder.WriteSnapshotForTests();
            int sessions = Directory.GetFiles(directory, "Retention-pid*-*.txt")
                .Select(path => Path.GetFileName(path).Substring(0, Path.GetFileName(path).LastIndexOf('-')))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            Assert(sessions == 3, $"expected 3 retained sessions, found {sessions}");
        }
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
