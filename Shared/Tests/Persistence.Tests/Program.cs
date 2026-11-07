using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static class Program
{
    private static int checks;
    private static string root;
    private static int Main()
    {
        try
        {
            root = TestWorkDirectory.New("personal-data");
            Basic(); Precedence(); Failures(); Cleanup(); Bounds(); InvalidBackup(); LongPaths(); Documents(); LegacyPathConstruction(); ExplicitReset();
            Console.WriteLine("PASS: " + checks + " personal-data persistence/migration checks; runtime=" +
                (Type.GetType("Mono.Runtime") == null ? Environment.Version.ToString() : "Unity Mono") + "; fixture=" + root);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { PersonalDataIO.TestBeforeOperation = null; PersonalDataFile.TestDocumentsDirectory = null; }
    }

    private static string At(string name) => PersonalDataIO.Normalize(Path.Combine(root, name));
    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);
    private static void Put(string path, string text) => PersonalDataIO.Publish(path, Bytes(text), false);
    private static string Read(string path)
    {
        byte[] bytes = PersonalDataIO.ReadOptional(path, int.MaxValue);
        return bytes == null ? null : Encoding.UTF8.GetString(bytes);
    }
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception("FAIL: " + name); }
    private static void Fails(Action action, string name)
    {
        try { action(); } catch (IOException) { Check(true, name); return; }
        throw new Exception("FAIL: expected IOException: " + name);
    }

    private static void Basic()
    {
        string target = At("basic/new/data.json"), legacy = At("basic/old/data.json");
        // The migration is opaque: JSON, msgpack, TSV and BOMs must retain their exact bytes.
        byte[] bytes = { 0xef, 0xbb, 0xbf, 0, 1, 17, 0xff };
        PersonalDataIO.Publish(legacy, bytes, false);
        var file = new PersonalDataFile(target, legacy);
        Check(PersonalDataIO.Equal(file.ReadAllBytes(), bytes), "opaque source bytes restored");
        Check(PersonalDataIO.Equal(PersonalDataIO.ReadOptional(target, 100), bytes), "published bytes identical");
        Check(Read(legacy) == null, "verified old source removed");
        Check(Read(target + ".migration.json") != null, "permanent receipt written");
        file.WriteAllText("changed\r\nvalue\n");
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "changed\r\nvalue\n", "updated data survives new object");
        Check(new PersonalDataFile(target, legacy).ReadAllLines().Length == 2, "line reader retains File.ReadAllLines behavior");
        file.Delete();
        Check(new PersonalDataFile(target, legacy).ReadAllBytes() == null, "delete retains adoption and absence");
        Put(legacy, "stale downgrade file");
        Check(new PersonalDataFile(target, legacy).ReadAllBytes() == null, "receipt prevents old data resurrection");
        Check(Read(legacy) == "stale downgrade file", "different later legacy data not deleted");
    }

    private static void Precedence()
    {
        string target = At("precedence/target"), legacy = At("precedence/legacy");
        Put(target, "current"); Put(legacy, "stale");
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "current", "existing target authoritative");
        Check(Read(legacy) == "stale", "different legacy retained");
        target = At("empty/target"); legacy = At("empty/legacy");
        Put(target, ""); Put(legacy, "older nonempty");
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "", "empty existing target not overwritten");
        target = At("missing/target"); legacy = At("missing/legacy");
        Check(new PersonalDataFile(target, legacy).ReadAllBytes() == null, "missing data remains absent");
        Put(legacy, "appeared later");
        Check(new PersonalDataFile(target, legacy).ReadAllBytes() == null, "no-source adoption blocks late legacy creation");
        target = At("corrupt/target"); legacy = At("corrupt/legacy");
        Put(target, "broken-format"); Put(legacy, "valid old data");
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "broken-format", "file adapter never substitutes old data for malformed current format");
        Check(Read(legacy) == "valid old data", "source retained for differing malformed target");
    }

    private static void Failures()
    {
        foreach (string step in new[] { "write", "publish", "verify" })
        {
            string target = At("failure-" + step + "/target"), legacy = At("failure-" + step + "/legacy");
            Put(legacy, "user settings");
            PersonalDataIO.TestBeforeOperation = (op, path) => { if (op == step && path == target) throw new IOException("injected"); };
            var file = new PersonalDataFile(target, legacy);
            Check(file.ReadAllText() == "user settings", step + " failure keeps session settings");
            Fails(() => file.WriteAllText("defaults"), step + " failure blocks defaults");
            Fails(() => file.Delete(), step + " failure blocks delete");
            PersonalDataIO.TestBeforeOperation = null;
            Check(Read(legacy) == "user settings", step + " failure retains source");
            Check(new PersonalDataFile(target, legacy).ReadAllText() == "user settings", step + " failure retries safely");
        }
        foreach (string step in new[] { "write", "publish", "verify" })
        {
            string target = At("receipt-" + step + "/target"), legacy = At("receipt-" + step + "/legacy");
            Put(legacy, "settings");
            PersonalDataIO.TestBeforeOperation = (op, path) => { if (op == step && path == target + ".migration.json") throw new IOException("injected receipt"); };
            var file = new PersonalDataFile(target, legacy);
            Check(file.ReadAllText() == "settings", "receipt " + step + " retains read-only settings");
            Fails(() => file.WriteAllText("defaults"), "receipt " + step + " prevents defaults");
            PersonalDataIO.TestBeforeOperation = null;
            Check(Read(legacy) == "settings", "receipt " + step + " retains old bytes");
            Check(new PersonalDataFile(target, legacy).ReadAllText() == "settings", "receipt " + step + " next run completes");
        }
        string inaccessible = At("unreadable/target"), old = At("unreadable/legacy");
        Put(inaccessible, "newest"); Put(old, "old");
        PersonalDataIO.TestBeforeOperation = (op, path) => { if (op == "read" && path == inaccessible) throw new IOException("denied target"); };
        var blocked = new PersonalDataFile(inaccessible, old);
        Fails(() => blocked.ReadAllText(), "inaccessible target does not enable source");
        Fails(() => blocked.WriteAllText("defaults"), "inaccessible target cannot be overwritten");
        PersonalDataIO.TestBeforeOperation = null;
        Check(Read(inaccessible) == "newest" && Read(old) == "old", "inaccessible case unchanged");
        string badReceipt = At("bad-receipt/target");
        Put(badReceipt + ".migration.json", "not-json");
        var invalid = new PersonalDataFile(badReceipt, old);
        Fails(() => invalid.ReadAllText(), "invalid receipt never enables source");
        Fails(() => invalid.WriteAllText("defaults"), "invalid receipt blocks writes");
    }

    private static void Cleanup()
    {
        string target = At("cleanup/target"), legacy = At("cleanup/legacy");
        Put(legacy, "prepared restart");
        PersonalDataIO.TestBeforeOperation = (op, path) => { if (op == "cleanup" && path == legacy) throw new IOException("locked source"); };
        var file = new PersonalDataFile(target, legacy, identity: "installation-one");
        Check(file.ReadAllText() == "prepared restart", "source-delete failure does not disable migrated settings");
        file.Delete();
        PersonalDataIO.TestBeforeOperation = null;
        Check(new PersonalDataFile(target, legacy, identity: "installation-one").ReadAllText() == null, "consumed journal never resurrects pending source");
        Check(Read(legacy) == null, "pending cleanup retried even when canonical file consumed");

        target = At("changed-source/target"); legacy = At("changed-source/legacy");
        Put(legacy, "original");
        PersonalDataIO.TestBeforeOperation = (op, path) => { if (op == "cleanup" && path == legacy) throw new IOException("locked"); };
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "original", "initial adoption with deferred cleanup");
        PersonalDataIO.TestBeforeOperation = null;
        PersonalDataIO.Publish(legacy, Bytes("changed by other process"), true);
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "original", "changed legacy does not override canonical");
        Check(Read(legacy) == "changed by other process", "cleanup never deletes changed source");

        target = At("identity/target");
        var first = new PersonalDataFile(target, null, identity: "full-installation-one"); first.WriteAllText("one");
        var other = new PersonalDataFile(target, null, identity: "full-installation-two");
        Fails(() => other.ReadAllText(), "full installation identity mismatch blocks reading foreign data");
        Fails(() => other.WriteAllText("two"), "full installation identity mismatch blocks overwrite");
        Check(first.ReadAllText() == "one", "identity mismatch keeps original data");
    }

    private static void Bounds()
    {
        string target = At("oversize/target"), legacy = At("oversize/legacy");
        Put(legacy, "too much");
        var file = new PersonalDataFile(target, legacy, maximumReadBytes: 3);
        Fails(() => file.ReadAllBytes(), "oversized source rejected before migration");
        Fails(() => file.WriteAllText("new"), "oversized source cannot be overwritten with defaults");
        Check(Read(legacy) == "too much" && Read(target) == null, "oversized source remains in place");
        target = At("growing-target/target");
        var current = new PersonalDataFile(target, null, maximumReadBytes: 3); current.WriteAllText("one");
        PersonalDataIO.Publish(target, Bytes("now oversized"), true);
        Fails(() => current.ReadAllBytes(), "oversized updated target rejects read");
        Fails(() => current.WriteAllText("new"), "failed reread blocks replacement with defaults");
    }

    private static void InvalidBackup()
    {
        string target = At("invalid/target"), legacy = At("invalid/legacy");
        Put(legacy, "malformed old content");
        var file = new PersonalDataFile(target, legacy);
        Check(file.ReadAllText() == "malformed old content", "malformed bytes migrate intact for existing parser");
        file.PreserveOnNextWrite(); file.WriteAllText("valid replacement");
        string[] backups = Directory.GetFiles(Path.GetDirectoryName(target), "*.bak");
        Check(backups.Length == 1 && Read(backups[0]) == "malformed old content", "invalid original preserved before replacement");
        Check(file.ReadAllText() == "valid replacement", "new valid data written after backup");
    }

    private static void LongPaths()
    {
        string prefix = At("long");
        while (prefix.Length < 310) prefix += Path.DirectorySeparatorChar + "long-unicode-ä-directory";
        string legacy = prefix + Path.DirectorySeparatorChar + "legacy.msgpack";
        string target = prefix + Path.DirectorySeparatorChar + "documents" + Path.DirectorySeparatorChar + "new.msgpack";
        Put(legacy, "long path data ä");
        var file = new PersonalDataFile(target, legacy);
        Check(file.ReadAllText() == "long path data ä", "long source and target migrate via Unicode handles");
        file.WriteAllText("new long value");
        Check(new PersonalDataFile(target, legacy).ReadAllText() == "new long value", "long path replace/read succeeds");
        file.Delete();
        Check(new PersonalDataFile(target, legacy).ReadAllText() == null, "long delete preserves receipt");
        Check(!PersonalDataIO.IsWindows || PersonalDataIO.Extended(@"\\server\share\folder\file") == @"\\?\UNC\server\share\folder\file", "UNC namespace conversion");
    }

    private static void Documents()
    {
        PersonalDataFile.TestDocumentsDirectory = () => At("documents ä");
        string path = PersonalDataFile.GetPath("CastlePlanner", "runtime.msgpack");
        Check(path == Path.Combine(At("documents ä"), "SHCDE", "CastlePlanner", "runtime.msgpack"), "documents resolution injectable without global process path switches");
        PersonalDataFile.TestDocumentsDirectory = null;
        Check(Path.IsPathRooted(PersonalDataFile.GetDocuments()), "production per-user resolution is absolute");
    }

    private static void LegacyPathConstruction()
    {
        string directory = At("assembly-path");
        while (directory.Length < 360) directory += Path.DirectorySeparatorChar + "plugin-directory-ä";
        string assembly = Path.Combine(directory, "Plugin.dll");
        Check(Path.GetDirectoryName(assembly) == directory, "adapter Path.GetDirectoryName accepts >260-character assembly paths");
        Check(Path.Combine(Path.GetDirectoryName(assembly), "LobbyModSettings", "data.json") ==
            directory + Path.DirectorySeparatorChar + "LobbyModSettings" + Path.DirectorySeparatorChar + "data.json",
            "adapter Path.Combine retains the complete long legacy directory");
    }

    private static void ExplicitReset()
    {
        string target = At("reset-target/target"), legacy = At("reset-target/legacy");
        Put(target, "oversized target"); Put(legacy, "older");
        var file = new PersonalDataFile(target, legacy, maximumReadBytes: 3);
        Fails(() => file.ReadAllBytes(), "oversized target blocks ordinary read before explicit reset");
        file.ResetAllBytes(Bytes("new"));
        Check(file.ReadAllText() == "new", "explicit reset recovers oversized target");
        string[] backups = Directory.GetFiles(Path.GetDirectoryName(target), "*.bak");
        Check(backups.Length == 1 && Read(backups[0]) == "oversized target", "oversized target preserved by allocation-free rename");
        Check(Read(legacy) == "older", "explicit reset never deletes different legacy data");
        file.Delete();
        Check(new PersonalDataFile(target, legacy, maximumReadBytes: 3).ReadAllText() == null, "reset receipt prevents later legacy resurrection");

        target = At("reset-legacy/target"); legacy = At("reset-legacy/legacy");
        Put(legacy, "oversized source");
        file = new PersonalDataFile(target, legacy, maximumReadBytes: 3);
        Fails(() => file.ReadAllBytes(), "oversized legacy blocks ordinary read before explicit reset");
        file.ResetAllBytes(Bytes("new"));
        Check(file.ReadAllText() == "new" && Read(legacy) == "oversized source", "explicit reset adopts personal path while retaining oversized legacy");

        foreach (string phase in new[] { "write", "publish", "verify", "receipt" })
        {
            target = At("reset-fail-" + phase + "/target"); legacy = At("reset-fail-" + phase + "/legacy");
            Put(target, "oversized original");
            file = new PersonalDataFile(target, legacy, maximumReadBytes: 3);
            Fails(() => file.ReadAllBytes(), "setup " + phase + " oversized");
            string currentTarget = target;
            PersonalDataIO.TestBeforeOperation = (operation, path) => {
                if (phase == "receipt" ? operation == "publish" && path == currentTarget + ".migration.json" : operation == phase && path == currentTarget)
                    throw new IOException("reset injected failure");
            };
            Fails(() => file.ResetAllBytes(Bytes("new")), "explicit reset " + phase + " failure returned");
            PersonalDataIO.TestBeforeOperation = null;
            Check(Read(target) == "oversized original", "explicit reset " + phase + " failure restores original target");
        }

        target = At("reset-invalid-receipt/target"); Put(target, "oversized"); Put(target + ".migration.json", "invalid receipt");
        file = new PersonalDataFile(target, null, maximumReadBytes: 3);
        Fails(() => file.ResetAllBytes(Bytes("new")), "reset never bypasses malformed receipt hidden by size failure");
        Check(Read(target) == "oversized", "invalid receipt reset leaves original unchanged");

        target = At("reset-identity/target");
        new PersonalDataFile(target, null, identity: "one", maximumReadBytes: 3).WriteAllText("one");
        PersonalDataIO.Publish(target, Bytes("oversized"), true);
        file = new PersonalDataFile(target, null, identity: "two", maximumReadBytes: 3);
        Fails(() => file.ResetAllBytes(Bytes("two")), "reset never bypasses foreign identity hidden by size failure");
        Check(Read(target) == "oversized", "identity reset leaves original unchanged");

        target = At("reset-access/target"); Put(target, "oversized"); file = new PersonalDataFile(target, null, maximumReadBytes: 3);
        Fails(() => file.ReadAllBytes(), "setup access after size failure");
        string inaccessible = target;
        PersonalDataIO.TestBeforeOperation = (operation, path) => { if (operation == "read" && path == inaccessible) throw new IOException("access blocked"); };
        Fails(() => file.ResetAllBytes(Bytes("new")), "reset never bypasses a current access error");
        PersonalDataIO.TestBeforeOperation = null;
        Check(Read(target) == "oversized", "access reset preserves original");

        target = At("reset-old-failure/target"); legacy = At("reset-old-failure/legacy"); Put(legacy, "oversized old data");
        file = new PersonalDataFile(target, legacy, maximumReadBytes: 3);
        Fails(() => file.ReadAllBytes(), "setup legacy reset failure");
        string blocked = target;
        PersonalDataIO.TestBeforeOperation = (operation, path) => { if (operation == "verify" && path == blocked) throw new IOException("failed new reset verify"); };
        Fails(() => file.ResetAllBytes(Bytes("new")), "legacy reset publication failure returned");
        PersonalDataIO.TestBeforeOperation = null;
        Check(Read(legacy) == "oversized old data" && Read(target) == null, "failed legacy reset keeps original and removes unverified replacement");
    }
}
