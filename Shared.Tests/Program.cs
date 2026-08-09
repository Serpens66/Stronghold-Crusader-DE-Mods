using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("native JSON roundtrip", NativeJsonRoundtrip),
            ("seven complete entries", SevenCompleteEntries),
            ("missing entry becomes disabled", MissingEntryBecomesDisabled),
            ("corrupt and typed documents rejected", InvalidDocumentsRejected),
            ("atomic write replaces existing file", AtomicWriteReplaces),
            ("leader election is deterministic", LeaderElection),
        };
        int failures = 0;
        foreach ((string name, Action run) in tests)
        {
            try { run(); Console.WriteLine("PASS " + name); }
            catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + exception); }
        }
        Console.WriteLine((tests.Length - failures) + "/" + tests.Length + " tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void NativeJsonRoundtrip()
    {
        TrailSettingsDocument document = TrailSettingsDocument.CreateDisabled();
        document.Mods["StartConditions_Serp"] = new TrailModEntry
        {
            Enabled = true,
            Settings = new Dictionary<string, object>
            {
                ["Bool"] = true,
                ["Int"] = 42,
                ["Double"] = 1.25,
                ["String"] = "Wood=10\r\nStone=-1",
            },
        };
        string json = TrailSettingsJson.Serialize(document);
        Assert(json.Contains("\r\n"), "serialized JSON has no CRLF");
        Assert(!json.Replace("\r\n", string.Empty).Contains("\n"), "serialized JSON contains naked LF");
        TrailModEntry entry = TrailSettingsJson.ParseObject(json).Mods["StartConditions_Serp"];
        Assert(entry.Enabled && (bool)entry.Settings["Bool"], "bool changed");
        Assert(Convert.ToInt32(entry.Settings["Int"]) == 42, "int changed");
        Assert(Math.Abs(Convert.ToDouble(entry.Settings["Double"]) - 1.25) < 0.0001, "double changed");
        Assert((string)entry.Settings["String"] == "Wood=10\r\nStone=-1", "complex string changed");
    }

    private static void SevenCompleteEntries()
    {
        TrailSettingsDocument parsed = TrailSettingsJson.ParseObject(TrailSettingsJson.Serialize(TrailSettingsDocument.CreateDisabled()));
        Assert(parsed.Mods.Count == 7, "expected seven entries");
        Assert(TrailModSettingsRegistry.TargetModIds.All(id => parsed.Mods.ContainsKey(id) && !parsed.Mods[id].Enabled), "disabled marker missing");
    }

    private static void MissingEntryBecomesDisabled()
    {
        TrailSettingsDocument parsed = TrailSettingsJson.ParseObject("{\"schemaVersion\":1,\"mods\":{}}");
        Assert(parsed.Mods.Count == 7 && parsed.Mods.Values.All(entry => !entry.Enabled), "missing entries were not disabled");
    }

    private static void InvalidDocumentsRejected()
    {
        ExpectFailure(() => TrailSettingsJson.ParseObject("broken"));
        ExpectFailure(() => TrailSettingsJson.ParseObject("{\"schemaVersion\":2,\"mods\":{}}"));
        ExpectFailure(() => TrailSettingsJson.ParseObject("{\"schemaVersion\":1,\"mods\":{\"UnitLimit_Serp\":{\"enabled\":true,\"settings\":{\"Limit\":{\"bad\":1}}}}}"));
    }

    private static void AtomicWriteReplaces()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrailModSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "Trail_Mission_1.modjson");
            File.WriteAllText(path, "old");
            TrailSettingsDocument document = TrailSettingsDocument.CreateDisabled();
            document.Mods["UnitLimit_Serp"].Enabled = true;
            TrailSettingsJson.WriteAtomic(path, document);
            Assert(TrailSettingsJson.Read(path).Mods["UnitLimit_Serp"].Enabled, "replacement was not readable");
            Assert(!Directory.GetFiles(root, "*.tmp-*").Any(), "temporary file remained");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void LeaderElection()
    {
        Assert(TrailModSettingsRegistry.ElectLeader(new[] { "UnitLimit_Serp" }) == "UnitLimit_Serp", "single leader wrong");
        Assert(TrailModSettingsRegistry.ElectLeader(new[] { "UnitLimit_Serp", "ExtraFeatures_Serp", "BuildingCosts_Serp" }) == "BuildingCosts_Serp", "multi leader wrong");
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch { return; }
        throw new InvalidOperationException("expected failure");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
