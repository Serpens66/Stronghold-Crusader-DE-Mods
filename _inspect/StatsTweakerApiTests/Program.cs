using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrusaderDETweaker.Configuration;

internal static class Program
{
    private static int passed;
    private static string root;
    private static readonly string[] names = { "units.toml", "DamageMatrices/melee.csv", "multipliers.cfg" };
    private static void Main()
    {
        root = Path.Combine(Path.GetTempPath(), "StatsTweakerApiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ContextApiTests.Run(root);
        DocumentTests.Run(root);
        DocumentTests.RunInstalledFiles(root);
        Run("replacement failure preserves existing pending configuration", () =>
        {
            var tx = Create("pending-replacement");
            string own = ConfigurationFileTransaction.Revision(tx.ReadOwn());
            tx.Stage(Values("preset"), own);
            string pending = ConfigurationFileTransaction.Revision(tx.ReadPending());
            var invalid = Values("defaults");
            invalid.Remove(names[0]);
            Throws(() => tx.Stage(invalid, own));
            Equal(pending, ConfigurationFileTransaction.Revision(tx.ReadPending()));
            tx.Stage(Values("defaults"), own);
            Equal(ConfigurationFileTransaction.Revision(Values("defaults")), ConfigurationFileTransaction.Revision(tx.ReadPending()));
            Equal(own, ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            File.WriteAllText(Path.Combine(root, "pending-replacement", names[0]), "external");
            Throws(() => tx.Stage(Values("preset-again"), own));
            Equal(ConfigurationFileTransaction.Revision(Values("defaults")), ConfigurationFileTransaction.Revision(tx.ReadPending()));
        });
        Run("staging does not change active files", () =>
        {
            var tx = Create("stage");
            var before = tx.ReadOwn();
            tx.Stage(Values("new"), ConfigurationFileTransaction.Revision(before));
            Equal(ConfigurationFileTransaction.Revision(before), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            Equal(ConfigurationFileTransaction.Revision(Values("new")), ConfigurationFileTransaction.Revision(tx.ReadPending()));
            tx.Cancel();
            if (tx.ReadPending() != null) throw new Exception("Cancellation retained pending data");
        });
        Run("external edits reject stale snapshots", () =>
        {
            var tx = Create("stale");
            var revision = ConfigurationFileTransaction.Revision(tx.ReadOwn());
            File.WriteAllText(Path.Combine(root, "stale", names[0]), "external");
            Throws(() => tx.Stage(Values("new"), revision));
            if (tx.ReadPending() != null) throw new Exception("Rejected stage was published");
        });
        Run("external edits after staging stay untouched", () =>
        {
            var tx = Create("conflict");
            tx.Stage(Values("new"), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            File.WriteAllText(Path.Combine(root, "conflict", names[0]), "external");
            var revision = ConfigurationFileTransaction.Revision(tx.ReadOwn());
            Throws(() => tx.ApplyBeforeLoading());
            Equal(revision, ConfigurationFileTransaction.Revision(tx.ReadOwn()));
        });
        foreach (string boundary in new[] { "journal-published", "written:DamageMatrices/melee.csv", "written:multipliers.cfg", "written:units.toml", "verified", "completed" })
        {
            string point = boundary;
            Run("restart recovery at " + point, () =>
            {
                var tx = Create("crash" + passed);
                tx.Stage(Values("new"), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
                tx.Checkpoint = mark => { if (mark == point) throw new IOException("Simulated process interruption"); };
                Throws(() => tx.ApplyBeforeLoading());
                tx.Checkpoint = null;
                tx.ApplyBeforeLoading();
                Equal(ConfigurationFileTransaction.Revision(Values("new")), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
                tx.ApplyBeforeLoading();
            });
        }
        Run("corrupt package does not touch files", () =>
        {
            var tx = Create("corrupt");
            tx.Stage(Values("new"), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            var bytes = File.ReadAllBytes(tx.PendingPath);
            bytes[bytes.Length / 2] ^= 1;
            File.WriteAllBytes(tx.PendingPath, bytes);
            string revision = ConfigurationFileTransaction.Revision(tx.ReadOwn());
            Throws(() => tx.ApplyBeforeLoading());
            Equal(revision, ConfigurationFileTransaction.Revision(tx.ReadOwn()));
        });
        Run("successive stages still compare against own files", () =>
        {
            var tx = Create("successive");
            string revision = ConfigurationFileTransaction.Revision(tx.ReadOwn());
            tx.Stage(Values("first"), revision);
            tx.Stage(Values("second"), revision);
            Equal(revision, ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            tx.ApplyBeforeLoading();
            Equal(ConfigurationFileTransaction.Revision(Values("second")), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
        });
        Run("write failure retains recoverable journal", () =>
        {
            var tx = Create("locked");
            tx.Stage(Values("new"), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            using (File.Open(Path.Combine(root, "locked", "units.toml"), FileMode.Open, FileAccess.Read, FileShare.Read))
                Throws(() => tx.ApplyBeforeLoading());
            if (!tx.RecoveryRequired) throw new Exception("Missing recovery journal");
            Throws(() => tx.Cancel());
            tx.ApplyBeforeLoading();
            Equal(ConfigurationFileTransaction.Revision(Values("new")), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
        });
        Run("external edit at an application boundary is preserved", () =>
        {
            var tx = Create("midconflict");
            tx.Stage(Values("new"), ConfigurationFileTransaction.Revision(tx.ReadOwn()));
            tx.Checkpoint = mark => { if (mark == "written:DamageMatrices/melee.csv") File.WriteAllText(Path.Combine(root, "midconflict", "units.toml"), "external"); };
            Throws(() => tx.ApplyBeforeLoading());
            Equal("external", File.ReadAllText(Path.Combine(root, "midconflict", "units.toml")));
            tx.Checkpoint = null;
            Throws(() => tx.ApplyBeforeLoading());
            Equal("external", File.ReadAllText(Path.Combine(root, "midconflict", "units.toml")));
        });
        Run("oversized package is rejected before publication", () =>
        {
            var tx = Create("oversized");
            var after = Values("new");
            after["units.toml"] = new byte[ConfigurationFileTransaction.MaximumPackageBytes + 1];
            Throws(() => tx.Stage(after, ConfigurationFileTransaction.Revision(tx.ReadOwn())));
            if (tx.ReadPending() != null) throw new Exception("Oversized package was published");
        });
        Console.WriteLine("PASS: " + passed + " configuration transaction scenarios. Artifacts: " + root);
    }
    private static ConfigurationFileTransaction Create(string name)
    {
        string dir = Path.Combine(root, name);
        foreach (var pair in Values("old"))
        {
            string path = Path.Combine(dir, pair.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, pair.Value);
        }
        return new ConfigurationFileTransaction(dir, names, "test-v1");
    }
    private static Dictionary<string, byte[]> Values(string value)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string name in names) result.Add(name, Encoding.UTF8.GetBytes(value + ":" + name));
        return result;
    }
    private static void Equal(string a, string b) { if (a != b) throw new Exception("Values differ"); }
    private static void Throws(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; } catch (IOException) { return; } catch (InvalidOperationException) { return; }
        throw new Exception("Expected rejection");
    }
    private static void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
}
