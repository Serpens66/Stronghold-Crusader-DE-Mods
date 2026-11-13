using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BugfixesAndQoL
{
    internal static partial class Program
    {
        // Exercise the actual store adapters, not a second implementation of their formats.
        private static void StorageMigration()
        {
            string root = TestWorkDirectory.New("bf-personal");
            string legacy = Path.Combine(root, "legacy");
            string target = Path.Combine(root, "documents");
            Directory.CreateDirectory(legacy);
            var logger = new ManualLogSource("PersonalStorageTests");

            string oldAi = Path.Combine(legacy, "AiAivSelectionMemory.json");
            string newAi = Path.Combine(target, "AiAivSelectionMemory.json");
            string encoded = AiAivSelectionCodec.Encode(BuildSource(11, 21));
            string key = AivAicPresetStore.BuildLordKey(BuildSource(11, 21));
            string json = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object> { [key] = encoded });
            File.WriteAllText(oldAi, json, new UTF8Encoding(false));
            var ai = new AiSelectionMemoryStore(logger, newAi, oldAi);
            Assert(ai.TryGetEncoded(key, out string restored) && restored == encoded, "AI choice lost during move");
            Assert(!File.Exists(oldAi) && File.ReadAllText(newAi) == json, "AI move was not byte-preserving");
            Assert(new AiSelectionMemoryStore(logger, newAi, oldAi).TryGetEncoded(key, out restored) && restored == encoded,
                "AI choice did not survive a second opening");

            string oldPresets = Path.Combine(legacy, "AivAicPresets.json");
            string newPresets = Path.Combine(target, "AivAicPresets.json");
            var sourcePresets = new AivAicPresetStore(logger, oldPresets);
            sourcePresets.Save(key, "Castle Ä", BuildSource(11, 21));
            byte[] presetBytes = File.ReadAllBytes(oldPresets);
            var presets = new AivAicPresetStore(logger, newPresets, oldPresets);
            var preset = presets.Find(key, "Castle Ä");
            Assert(preset != null && preset.Rotation == 3 && preset.Aivs.Count == 1 && preset.Aic != null,
                "named preset contents lost during move");
            Assert(!File.Exists(oldPresets) && File.ReadAllBytes(newPresets).SequenceEqual(presetBytes),
                "preset source removed without preserving its complete contents");
            presets.Save(key, "Castle B", BuildSource(12, 22));
            Assert(new AivAicPresetStore(logger, newPresets, oldPresets).GetPresets(key).Count == 2,
                "new preset write lost the migrated choice");

            string oldMap = Path.Combine(legacy, "LobbyMapSelectionMemory.json");
            string newMap = Path.Combine(target, "LobbyMapSelectionMemory.json");
            var mapSource = new LobbyMapSelectionStore(logger, oldMap);
            mapSource.RememberSort(2, false);
            mapSource.RememberMap(new LobbyMapIdentity { Origin = "user", FileName = "Castle.map", FilePath = @"C:\Maps\Castle.map" });
            var map = new LobbyMapSelectionStore(logger, newMap, oldMap).Current;
            Assert(map.SortColumn == 2 && !map.SortAscending && map.Map.FileName == "Castle.map" && !File.Exists(oldMap),
                "map selection or sort lost during move");

            string oldBlacklist = Path.Combine(legacy, "SteamInviteBlacklist.json");
            string newBlacklist = Path.Combine(target, "SteamInviteBlacklist.json");
            Assert(new SteamInviteBlacklistStore(oldBlacklist).TryAdd(76561198000000001, out string error), error);
            var blacklist = new SteamInviteBlacklistStore(newBlacklist, oldBlacklist);
            Assert(blacklist.IsUsable && blacklist.Contains(76561198000000001) && !File.Exists(oldBlacklist),
                "blocked sender lost during move");
            Assert(blacklist.TryAdd(76561198000000002, out error), error);
            Assert(new SteamInviteBlacklistStore(newBlacklist, oldBlacklist).Count == 2,
                "blacklist failed to persist after migration");

            // A new target has priority even when it is malformed; recovery must retain its bytes.
            string malformed = Path.Combine(target, "malformed.json");
            string validLegacy = Path.Combine(legacy, "valid-old.json");
            File.WriteAllText(malformed, "broken");
            File.WriteAllText(validLegacy, json);
            var invalidAi = new AiSelectionMemoryStore(logger, malformed, validLegacy);
            Assert(invalidAi.IsEmpty && File.ReadAllText(validLegacy) == json, "invalid target resurrected old settings");
            invalidAi.Remember(key, encoded, out _);
            Assert(Directory.GetFiles(target, "malformed.json.invalid-*.bak").Any(path => File.ReadAllText(path) == "broken"),
                "invalid AI target was overwritten without preserving original bytes");

            string brokenMap = Path.Combine(target, "broken-map.json");
            File.WriteAllText(brokenMap, "broken map");
            var mapRecovery = new LobbyMapSelectionStore(logger, brokenMap);
            mapRecovery.RememberSort(2, false);
            Assert(Directory.GetFiles(target, "broken-map.json.invalid-*.bak").Any(path => File.ReadAllText(path) == "broken map"),
                "invalid map target was overwritten without preserving original bytes");

            string oversizedBlacklist = Path.Combine(target, "oversized-blacklist.json");
            byte[] oversizedBytes = new byte[SteamInviteBlacklistStore.MaximumStoreBytes + 1];
            File.WriteAllBytes(oversizedBlacklist, oversizedBytes);
            var oversized = new SteamInviteBlacklistStore(oversizedBlacklist);
            Assert(!oversized.IsUsable && oversized.LoadError.Contains("large"), "oversized blacklist did not fail closed");
            Assert(!oversized.TryAdd(76561198000000003, out error), "oversized blacklist accepted an implicit overwrite");
            Assert(oversized.TryClear(out error) && oversized.IsUsable && oversized.Count == 0,
                "explicit clear could not recover oversized blacklist: " + error);
            Assert(Directory.GetFiles(target, "oversized-blacklist.json.invalid-*.bak").Any(path => File.ReadAllBytes(path).SequenceEqual(oversizedBytes)),
                "explicit oversized reset did not preserve all original bytes");

            string blocked = Path.Combine(root, "blocked");
            File.WriteAllText(blocked, "not a directory");
            string blockedOld = Path.Combine(legacy, "blocked-map.json");
            string blockedOriginal = LobbyMapSelectionCodec.Serialize(new LobbyMapSelectionSnapshot {
                SortColumn = 2, SortAscending = false });
            File.WriteAllText(blockedOld, blockedOriginal);
            var unavailable = new LobbyMapSelectionStore(logger, Path.Combine(blocked, "map.json"), blockedOld);
            Assert(unavailable.Current.SortColumn == 2, "blocked destination lost readable legacy values for the session");
            unavailable.RememberSort(3, true);
            Assert(File.ReadAllText(blockedOld) == blockedOriginal, "failed migration overwrote legacy values");

            var previousResolver = Shared.PersonalDataFile.TestDocumentsDirectory;
            try
            {
                Shared.PersonalDataFile.TestDocumentsDirectory = () => target;
                Assert(SteamInviteBlacklistStore.GetDefaultPath() == Path.Combine(target, "SHCDE", "BugfixesAndQoL", "SteamInviteBlacklist.json"),
                    "blacklist default path does not use Documents");
                Assert(LobbyMapSelectionStore.GetDefaultPath() == Path.Combine(target, "SHCDE", "BugfixesAndQoL", "LobbyMapSelectionMemory.json"),
                    "map default path does not use Documents");
            }
            finally { Shared.PersonalDataFile.TestDocumentsDirectory = previousResolver; }
        }
    }
}
