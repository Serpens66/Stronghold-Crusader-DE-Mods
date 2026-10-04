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
