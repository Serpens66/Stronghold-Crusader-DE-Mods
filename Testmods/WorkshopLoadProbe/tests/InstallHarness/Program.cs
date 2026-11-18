using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WorkshopLoadProbe.Install;

// Offline harness for install/SafeInstall.cs (same source file as the game patcher).
// selftest runs deterministic fault-injection scenarios; the other commands are used by
// tests/Run-InstallTests.ps1 for real process-kill loops and timing with real packages, and by
// tools/Stage-Install.ps1 for the game tests.
internal static class Program
{
    private sealed class Crash : Exception { internal Crash(string step) : base("simulated crash at " + step) { } }

    private static int failures;

    private static int Main(string[] args)
    {
        try
        {
            switch (args.Length > 0 ? args[0] : "")
            {
                case "selftest": SelfTest(args[1]); break;
                case "stage-item": StageItem(args[1], args[2], args[3], args[4], args.Length > 5 ? int.Parse(args[5]) : 0); break;
                case "apply":
                    int delay = args.Length > 5 ? int.Parse(args[5]) : 0;
                    if (delay > 0) Swap.Fault = step => Thread.Sleep(delay);
                    Swap.Apply(args[1], args[2], args[3], args[4], Log);
                    break;
                case "apply-crash": Swap.Fault = step => { if (step == args[5]) Environment.Exit(3); }; Swap.Apply(args[1], args[2], args[3], args[4], Log); break;
                case "recover": if (File.Exists(args[1])) Swap.Recover(args[1], Log); else Log("NO_JOURNAL"); break;
                case "tree": Console.WriteLine(TreeId(args[1])); break;
                case "prepare":
                    var f = Prepare(args[1], args[2]);
                    Console.WriteLine(string.Join("|", f.Target, f.StagedNew, f.Backup, f.Journal, f.OldId, f.NewId));
                    break;
                default: Console.Error.WriteLine("usage: selftest|stage-item|apply|apply-crash|recover|tree|prepare"); return 2;
            }
            return failures == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine("ERROR " + ex.GetType().Name + ": " + ex.Message); return 1; }
    }

    private static void Log(string line) => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + line);

    // Expected entries from an APIShared manifest (current top-level Files) below one package root.
    private static void StageItem(string item, string root, string parent, string version, int delayPerFile)
    {
        if (delayPerFile > 0) Staging.AfterCopy = _ => Thread.Sleep(delayPerFile);
        // Standalone test program: the framework serializer is allowed here (never in Unity code).
        var manifest = (Dictionary<string, object>)new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }
            .DeserializeObject(File.ReadAllText(Path.Combine(item, "apishared-workshop.json")));
        var expected = new List<FileEntry>();
        foreach (Dictionary<string, object> record in (object[])manifest["Files"])
        {
            string path = (string)record["Path"];
            if (path.StartsWith(root + "/", StringComparison.Ordinal))
                expected.Add(new FileEntry(path.Substring(root.Length + 1), Convert.ToInt64(record["Size"]), ((string)record["Sha256"]).ToLowerInvariant()));
        }
        if (expected.Count == 0) throw new InvalidDataException("no files below " + root);
        Staging.Stage(Path.Combine(item, "Payload", root.Replace('/', '\\')), expected, parent, version, Log);
    }

    private static List<FileEntry> Expected(string source) =>
        Tree.Files(source).Select(p => { string f = Path.Combine(source, p.Replace('/', '\\')); return new FileEntry(p, new FileInfo(f).Length, Tree.Sha256(f)); }).ToList();

    // Content identity of a tree (paths + hashes), installer metadata excluded.
    private static string TreeId(string root)
    {
        if (!Directory.Exists(root)) return "missing";
        var builder = new StringBuilder();
        foreach (string p in Tree.Files(root)) builder.Append(p).Append('=').Append(Tree.Sha256(Path.Combine(root, p.Replace('/', '\\')))).Append('\n');
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
    }

    // ---------------------------------------------------------------- fixtures

    private const string DefaultKeep = "settings.json\nconfig/   # whole folder\n";
    private static Dictionary<string, string> OldFiles() => new Dictionary<string, string>
    {
        ["a.dll"] = "old-a", ["b.txt"] = "same", ["sub/only-old.txt"] = "old-only", ["settings.json"] = "default-1",
        ["apishared-keep.txt"] = DefaultKeep, ["mods/child/child.dll"] = "c1", ["mods/child/apishared-keep.txt"] = "child.cfg\n",
    };
    private static Dictionary<string, string> NewFiles(string keep) => new Dictionary<string, string>
    {
        ["a.dll"] = "new-a", ["b.txt"] = "same", ["sub/only-new.txt"] = "new-only", ["settings.json"] = "default-2",
        ["apishared-keep.txt"] = keep ?? DefaultKeep, ["mods/child/child.dll"] = "c2", ["mods/child/apishared-keep.txt"] = "child.cfg\n",
    };
    // What the player changed after installing the old version. junk.log is not listed anywhere.
    private static Dictionary<string, string> PlayerFiles() => new Dictionary<string, string>
    {
        ["settings.json"] = "player settings", ["config/user.cfg"] = "player config", ["mods/child/child.cfg"] = "child player", ["junk.log"] = "junk",
    };

    private sealed class Fixture
    {
        internal string Root, Target, Staging, Backup, Journal, StagedNew, OldId, NewId;
    }

    private static void Write(string root, Dictionary<string, string> files)
    {
        foreach (var file in files)
        {
            string path = Path.Combine(root, file.Key.Replace('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, file.Value);
        }
    }

    private static string IdOf(string root, Dictionary<string, string> files)
    {
        if (Directory.Exists(root)) Tree.Delete(root);
        Write(root, files);
        return TreeId(root);
    }

    // Installed old version (through the real swap) + player changes + staged new version.
    // NewId describes the expected result with the default keep list.
    private static Fixture Prepare(string root, string name, bool installOld = true, string newKeep = null)
    {
        var f = new Fixture { Root = Path.Combine(root, name) };
        if (Directory.Exists(f.Root)) Tree.Delete(f.Root);
        f.Target = Path.Combine(f.Root, "plugins", "Pkg_Serp");
        f.Staging = Path.Combine(f.Root, "cache", "s");
        f.Backup = Path.Combine(f.Root, "cache", "b", "Pkg_Serp");
        f.Journal = Path.Combine(f.Root, "cache", "j", "Pkg_Serp.journal");
        // No cache parents are pre-created: Apply must create them (missed once, found in game test G2).
        Directory.CreateDirectory(Path.Combine(f.Root, "plugins"));
        Write(Path.Combine(f.Root, "src-old"), OldFiles());
        Write(Path.Combine(f.Root, "src-new"), NewFiles(newKeep));
        var player = PlayerFiles();
        if (installOld)
        {
            string old = Staging.Stage(Path.Combine(f.Root, "src-old"), Expected(Path.Combine(f.Root, "src-old")), f.Staging, "v1", _ => { });
            Swap.Apply(f.Target, old, f.Backup, f.Journal, _ => { });
            Write(f.Target, player);
        }
        f.StagedNew = Staging.Stage(Path.Combine(f.Root, "src-new"), Expected(Path.Combine(f.Root, "src-new")), f.Staging, "v2", _ => { });
        var oldExpected = OldFiles(); foreach (var p in player) oldExpected[p.Key] = p.Value;
        var newExpected = NewFiles(newKeep);
        if (installOld) foreach (var p in player.Where(p => p.Key != "junk.log")) newExpected[p.Key] = p.Value;
        f.OldId = installOld ? IdOf(Path.Combine(f.Root, "expect-old"), oldExpected) : "missing";
        f.NewId = IdOf(Path.Combine(f.Root, "expect-new"), newExpected);
        return f;
    }

    private static void Check(string scenario, bool condition, string detail)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + scenario + " " + detail);
        if (!condition) failures++;
    }

    private static string Read(Fixture f, string relative)
    {
        string path = Path.Combine(f.Target, relative.Replace('/', '\\'));
        return File.Exists(path) ? File.ReadAllText(path) : "<missing>";
    }

    // Whatever BepInEx could see: the installed folder is missing, or completely old/new.
    private static bool Whole(Fixture f)
    {
        string id = TreeId(f.Target);
        return id == "missing" || id == f.OldId || id == f.NewId;
    }

    private static bool Clean(Fixture f) =>
        !Directory.Exists(f.Backup) && !File.Exists(f.Journal) && !Directory.Exists(f.StagedNew + ".undo");

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (Exception) { return true; }
    }

    // ---------------------------------------------------------------- self test

    private static void SelfTest(string root)
    {
        Directory.CreateDirectory(root);
        Action<string> quiet = _ => { };

        // S1 staging publishes a verified copy once and reuses it.
        var f = Prepare(root, "s1");
        Check("S1-stage", Tree.IsComplete(f.StagedNew) && !Directory.Exists(f.StagedNew + ".tmp"), f.StagedNew);
        string again = Staging.Stage(Path.Combine(f.Root, "src-new"), Expected(Path.Combine(f.Root, "src-new")), f.Staging, "v2", quiet);
        Check("S1-reuse", again == f.StagedNew, again);

        // S2 source does not match the manifest: nothing is published.
        f = Prepare(root, "s2");
        var expected = Expected(Path.Combine(f.Root, "src-new"));
        File.WriteAllText(Path.Combine(f.Root, "src-new", "a.dll"), "tampered");
        bool rejected = Throws(() => Staging.Stage(Path.Combine(f.Root, "src-new"), expected, f.Staging, "v3", quiet));
        Check("S2-corrupt-source", rejected && !Directory.Exists(Path.Combine(f.Staging, "v3")) && !Directory.Exists(Path.Combine(f.Staging, "v3.tmp")), "rejected=" + rejected);

        // S3 the source changes while it is copied (Steam writing): the copy is rejected.
        f = Prepare(root, "s3");
        expected = Expected(Path.Combine(f.Root, "src-new"));
        Staging.AfterCopy = p => { if (p == expected[0].Path) File.WriteAllText(Path.Combine(f.Root, "src-new", expected[1].Path.Replace('/', '\\')), "changed during copy"); };
        rejected = Throws(() => Staging.Stage(Path.Combine(f.Root, "src-new"), expected, f.Staging, "v3", quiet));
        Staging.AfterCopy = null;
        Check("S3-source-changes-during-copy", rejected && !Directory.Exists(Path.Combine(f.Staging, "v3")), "rejected=" + rejected);

        // S4 crash during copy leaves only a .tmp folder, which the next stage discards.
        f = Prepare(root, "s4");
        expected = Expected(Path.Combine(f.Root, "src-new"));
        Staging.AfterCopy = p => throw new Crash("copy");
        Throws(() => Staging.Stage(Path.Combine(f.Root, "src-new"), expected, f.Staging, "v3", quiet));
        Staging.AfterCopy = null;
        string v3 = Staging.Stage(Path.Combine(f.Root, "src-new"), expected, f.Staging, "v3", quiet);
        Check("S4-crash-during-copy", Tree.IsComplete(v3) && !Directory.Exists(v3 + ".tmp"), "retryComplete=" + Tree.IsComplete(v3));

        // S5 a malformed keep list is rejected when staging, before anything is swapped.
        foreach (string bad in new[] { "../outside.cfg\n", "C:/abs.cfg\n", "conf*.json\n", "a.json\na.json\n" })
        {
            f = Prepare(root, "s5");
            File.WriteAllText(Path.Combine(f.Root, "src-new", "apishared-keep.txt"), bad);
            rejected = Throws(() => Staging.Stage(Path.Combine(f.Root, "src-new"), Expected(Path.Combine(f.Root, "src-new")), f.Staging, "v3", quiet));
            Check("S5-invalid-keep-list", rejected && !Directory.Exists(Path.Combine(f.Staging, "v3")), bad.Trim().Replace("\n", "|"));
        }

        // S6 fresh installation: exactly the package.
        f = Prepare(root, "s6", installOld: false);
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S6-fresh", TreeId(f.Target) == f.NewId && Clean(f), TreeId(f.Target));

        // S7 update: new package + listed player files (also from a nested list); everything else is gone.
        f = Prepare(root, "s7");
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S7-update-keep", TreeId(f.Target) == f.NewId && Clean(f) && Read(f, "settings.json") == "player settings" &&
            Read(f, "mods/child/child.cfg") == "child player" && Read(f, "junk.log") == "<missing>" && Read(f, "sub/only-old.txt") == "<missing>",
            "settings=" + Read(f, "settings.json") + " junk=" + Read(f, "junk.log"));

        // S8 crash after every step, then recovery as the next start would run it.
        foreach (string step in new[] { "journal", "carried", "old-moved", "new-moved", "backup-deleted" })
        {
            f = Prepare(root, "s8-" + step);
            Swap.Fault = s => { if (s == step) throw new Crash(s); };
            Throws(() => Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet));
            Swap.Fault = null;
            bool wholeAfterCrash = Whole(f);
            string outcome = File.Exists(f.Journal) ? Swap.Recover(f.Journal, quiet) : "no-journal";
            if (outcome.StartsWith("not-started", StringComparison.Ordinal)) Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
            Check("S8-crash-" + step, wholeAfterCrash && TreeId(f.Target) == f.NewId && Clean(f), "recover=" + outcome + " wholeAfterCrash=" + wholeAfterCrash);
        }

        // S9 crash inside recovery itself, then recovery again.
        f = Prepare(root, "s9");
        Swap.Fault = s => { if (s == "old-moved") throw new Crash(s); };
        Throws(() => Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet));
        Swap.Fault = s => { if (s == "recover-new-moved") throw new Crash(s); };
        Throws(() => Swap.Recover(f.Journal, quiet));
        Swap.Fault = null;
        bool whole9 = Whole(f);
        string outcome9 = Swap.Recover(f.Journal, quiet);
        Check("S9-crash-during-recover", whole9 && TreeId(f.Target) == f.NewId && Clean(f), "second=" + outcome9);

        // S10 a file in the installed folder is held open: nothing changes, old stays loadable.
        f = Prepare(root, "s10");
        bool failed10;
        using (new FileStream(Path.Combine(f.Target, "a.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
            failed10 = Throws(() => Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet));
        Check("S10-locked-installed-file", failed10 && TreeId(f.Target) == f.OldId && Tree.IsComplete(f.StagedNew) && !File.Exists(f.Journal),
            "failed=" + failed10 + " target=" + TreeId(f.Target));
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S10-retry-after-lock", TreeId(f.Target) == f.NewId && Clean(f), TreeId(f.Target));

        // S11 cleanup blocked by an open backup file: the swap counts as done, later recovery finishes.
        f = Prepare(root, "s11");
        FileStream held = null;
        Swap.Fault = s => { if (s == "new-moved") held = new FileStream(Path.Combine(f.Backup, "a.dll"), FileMode.Open, FileAccess.Read, FileShare.None); };
        bool threw = Throws(() => Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet));
        Swap.Fault = null;
        held?.Dispose();
        bool journalKept = File.Exists(f.Journal);
        string outcome11 = journalKept ? Swap.Recover(f.Journal, quiet) : "none";
        Check("S11-cleanup-deferred", !threw && journalKept && TreeId(f.Target) == f.NewId && Clean(f), "recover=" + outcome11);

        // S12 stale backup without journal is removed before a new swap.
        f = Prepare(root, "s12");
        Write(f.Backup, new Dictionary<string, string> { ["junk.txt"] = "x" });
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S12-stale-backup", TreeId(f.Target) == f.NewId && Clean(f), TreeId(f.Target));

        // S13 incomplete stage (no marker) is never swapped in.
        f = Prepare(root, "s13");
        File.Delete(Path.Combine(f.StagedNew, Tree.CompleteName));
        bool refused = Throws(() => Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet));
        Check("S13-incomplete-stage-refused", refused && TreeId(f.Target) == f.OldId && !File.Exists(f.Journal), "refused=" + refused);

        // S14 a failed attempt already carried player files; the player then changes one and
        // deletes another before the retry: newest data wins, the deleted one falls back to the
        // shipped default (the undo record restores the pristine package first).
        f = Prepare(root, "s14");
        Swap.Fault = s => { if (s == "carried") throw new Crash(s); };
        Throws(() => Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet));
        Swap.Fault = null;
        if (File.Exists(f.Journal)) Swap.Recover(f.Journal, quiet);
        File.WriteAllText(Path.Combine(f.Target, "config", "user.cfg"), "player config changed later");
        File.Delete(Path.Combine(f.Target, "settings.json"));
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S14-retry-uses-current-player-state", Read(f, "config/user.cfg") == "player config changed later" && Read(f, "settings.json") == "default-2" &&
            Read(f, "a.dll") == "new-a" && Clean(f), "config=" + Read(f, "config/user.cfg") + " settings=" + Read(f, "settings.json"));

        // S15 reset revision: raising the number replaces the player's file once with the shipped
        // default; the next update with the same number keeps the player's file again.
        f = Prepare(root, "s15", newKeep: "settings.json 1\nconfig/\n");
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        bool resetOnce = Read(f, "settings.json") == "default-2" && Read(f, "config/user.cfg") == "player config";
        File.WriteAllText(Path.Combine(f.Target, "settings.json"), "player settings after reset");
        string v3stage = Staging.Stage(Path.Combine(f.Root, "src-new"), Expected(Path.Combine(f.Root, "src-new")), f.Staging, "v3", quiet);
        Swap.Apply(f.Target, v3stage, f.Backup, f.Journal, quiet);
        Check("S15-reset-once", resetOnce && Read(f, "settings.json") == "player settings after reset" && Clean(f),
            "first=" + resetOnce + " second=" + Read(f, "settings.json"));

        // S16 reset of a folder the package does not ship: the player's folder is removed once.
        f = Prepare(root, "s16", newKeep: "settings.json\nconfig/ 1\n");
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S16-reset-unshipped-folder", !Directory.Exists(Path.Combine(f.Target, "config")) && Read(f, "settings.json") == "player settings" && Clean(f),
            "configExists=" + Directory.Exists(Path.Combine(f.Target, "config")));

        // S17 an entry removed from the keep list is no longer carried: the file disappears.
        f = Prepare(root, "s17", newKeep: "settings.json\n");
        Swap.Apply(f.Target, f.StagedNew, f.Backup, f.Journal, quiet);
        Check("S17-entry-dropped", !Directory.Exists(Path.Combine(f.Target, "config")) && Read(f, "settings.json") == "player settings" && Clean(f),
            "configExists=" + Directory.Exists(Path.Combine(f.Target, "config")));

        Console.WriteLine(failures == 0 ? "SELFTEST PASS" : "SELFTEST FAIL " + failures);
    }
}
