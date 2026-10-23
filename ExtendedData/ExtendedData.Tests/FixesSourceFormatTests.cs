using ExtendedData;
using System.Security.Cryptography;
using System.Text;

internal static class FixesSourceFormatTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    internal static void Run()
    {
        // Genuine historical envelopes are hashed before parsing; migration must not repair a bad digest.
        foreach (int version in new[] { 2, 3 })
        {
            string payload = "{\"version\":" + version + ",\"session\":\"old\",\"fixesInstalled\":true,\"slots\":[{" +
                "\"playerId\":1,\"lordType\":-1,\"lordName\":\"Legacy\",\"configName\":\"cfg\",\"configChecksum\":\"42\"," +
                "\"modLordData\":null,\"fixesJson\":null}]}";
            var wire = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
                { ["digest"] = Hash(payload), ["payload"] = Shared.DependencyFreeJson.Parse(payload) });
            var old = LordDataSnapshot.Parse(wire);
            Check(old.Digest == Hash(payload) && old.WireJson == wire && old.GetSlot(1).FixesSources == null,
                "Historical snapshot was rehashed or invented sources.");
            LordDataSaveRecord.Read(wire, out var savedOld, out var oldPackage);
            Check(savedOld.Digest == old.Digest && oldPackage == null, "Legacy unwrapped save lost its original digest.");
            bool rejected = false;
            try { LordDataSnapshot.Parse(wire.Replace("Legacy", "Changed")); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Invalid historical checksum was accepted.");
        }

        var plugin = new Fixes.Boostrap.Plugin();
        BepInEx.Bootstrap.Chainloader.PluginInfos["fixes"] = new BepInEx.Bootstrap.FakePluginInfo { Instance = plugin };
        var bridge = new FixesLordPreferencesBridge();
        var slot = new LordDataSlot { PlayerId = 1, LordType = -1, LordName = "Custom", ConfigName = "cfg",
            ConfigChecksum = "42", FixesSources = new[] { new FixesPreferenceSource { CustomName = "RAT" } } };
        var snapshot = LordDataSnapshot.Create("ready", true, new[] { slot });
        var packageSlot = new LordPackageSlot { PlayerId = 1, LordType = -1, LordName = "Custom", ConfigName = "cfg",
            ConfigChecksum = "42", FileDigest = "files", NeedsSnapshot = true, EmbeddedTrail = true,
            FixesSourcesDigest = FixesPreferenceSources.Digest(slot.FixesSources) };
        var manifest = LordPackageManifest.Create("ready", false, new[] { packageSlot });
        LordDataSaveRecord.Read(LordDataSaveRecord.Write(snapshot, manifest), out var saved, out var savedPackage);
        Check(saved.Digest == snapshot.Digest && saved.GetSlot(1).FixesSources.Single().Json == null &&
            savedPackage.Digest == manifest.Digest && savedPackage.Slots.Single().EmbeddedTrail &&
            savedPackage.Slots.Single().FixesSourcesDigest == packageSlot.FixesSourcesDigest,
            "Save roundtrip lost explicit absence or its separate source digest.");
        Check(manifest.MatchesSnapshot(snapshot, bridge.Normalize), "Complete absent-source snapshot was not ready.");
        packageSlot.FixesSourcesDigest = FixesPreferenceSources.Digest(Array.Empty<FixesPreferenceSource>());
        Check(!manifest.MatchesSnapshot(snapshot, bridge.Normalize), "Old snapshot ACK bypassed a changed sources digest.");
        packageSlot.FixesSourcesDigest = null;
        Check(!manifest.MatchesSnapshot(snapshot, bridge.Normalize), "Missing source proof was treated as a complete capture.");
        slot.FixesSources = null;
        Check(!manifest.MatchesSnapshot(LordDataSnapshot.Create("ready", true, new[] { slot }), bridge.Normalize),
            "Legacy snapshot was allowed to acknowledge new Fixes synchronization.");

        string oldManifestPayload = "{\"version\":1,\"session\":\"old\",\"local\":false,\"slots\":[]}";
        string oldManifest = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            { ["payload"] = oldManifestPayload, ["digest"] = Hash(oldManifestPayload) });
        var parsed = LordPackageManifest.Parse(oldManifest);
        Check(parsed.WireJson == oldManifest && parsed.Digest == Hash(oldManifestPayload), "Legacy manifest digest changed during read.");

        string root = Path.Combine(Path.GetTempPath(), "FixesSourceFormat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string mission = Path.Combine(root, "01.trail"); File.WriteAllText(mission, "mission");
            var trailSlot = new TrailLordSlot { PlayerId = 1, LordType = -1, LordName = "Custom", ConfigName = "cfg",
                ConfigChecksum = "42", AivChecksums = new[] { "aiv" }, FixesSources = new[]
                { new FixesPreferenceSource { CustomName = "RAT", Json = "{\"Value\":null}" }, new FixesPreferenceSource { LordType = 0 } } };
            TrailLordRequirements.Create(mission, new[] { trailSlot }).Write(mission);
            Check(TrailLordRequirements.Read(mission).Slots[0].FixesSources.Count == 2, "Trail export lost source constraints.");
            trailSlot.FixesSources = null; trailSlot.EmbeddedValuesOnly = true;
            TrailLordRequirements.Create(mission, new[] { trailSlot }).Write(mission);
            string path = TrailLordRequirements.SidecarPath(mission);
            string current = File.ReadAllText(path);
            foreach (int version in new[] { 1, 2, 3 })
            {
                File.WriteAllText(path, current.Replace("\"version\": 4", "\"version\": " + version));
                var restored = TrailLordRequirements.Read(mission).Slots[0];
                Check(restored.FixesSources == null && restored.EmbeddedValuesOnly == (version >= 3),
                    "Historical Trail format changed meaning.");
            }
        }
        finally { Directory.Delete(root, true); }
        plugin.CustomLordPreferences.Clear(); Fixes.Config.Preferences.Reset();
    }
}
