using MessagePack;
using SHCDESE.API.Components.Network;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LobbyModSettingsPresetTests
{
    internal static class Program
    {
        private const string ModName = "PresetTest_Serp";
        private const string SchemaKey = "__SerpPresetSchemaVersion";
        private const string ActiveKey = "__SerpActivePreset";
        private const string Preset1Key = "__SerpPreset1";
        private const string Preset2Key = "__SerpPreset2";

        private static int Main(string[] args)
        {
            if (args.Length == 1)
                return AuditPresetFile(args[0]);

            string root = Path.Combine(
                Path.GetTempPath(),
                "SerpPresetTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                ValidateInstalledLegacyFiles();

                string assemblyPath = Path.Combine(root, "PresetTest.dll");
                string settingsPath = Path.Combine(
                    root,
                    "LobbyModSettings",
                    ModName + ".msgpack");
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));

                WriteLegacy(settingsPath, false, 42);
                FakeSettings migrated = Start(assemblyPath, settingsPath, () => false);
                Assert(!migrated.EnableMod && migrated.Number == 42, "Legacy values were not restored.");
                Dictionary<string, byte[]> payload = Read(settingsPath);
                Assert(payload.ContainsKey(SchemaKey), "Legacy file was not migrated.");
                Assert(!payload.ContainsKey(Preset2Key), "Preset 2 must remain unset after migration.");

                migrated.SelectedPreset = 1;
                Assert(migrated.EnableMod && migrated.Number == 5, "Unset preset 2 did not show defaults.");
                payload = Read(settingsPath);
                Assert(MessagePackSerializer.Deserialize<int>(payload[ActiveKey]) == 1, "Active preset was not saved.");
                Assert(!payload.ContainsKey(Preset2Key), "Switching alone must not save preset 2.");

                migrated.Number = 7;
                payload = Read(settingsPath);
                Assert(payload.ContainsKey(Preset2Key), "First setting change did not create preset 2.");

                migrated.SelectedPreset = 0;
                Assert(!migrated.EnableMod && migrated.Number == 42, "Preset 1 was not restored.");
                migrated.SelectedPreset = 1;
                Assert(migrated.EnableMod && migrated.Number == 7, "Preset 2 was not restored.");

                FakeSettings restarted = Start(assemblyPath, settingsPath, () => false);
                Assert(restarted.SelectedPreset == 1, "Active preset did not survive restart.");
                Assert(restarted.EnableMod && restarted.Number == 7, "Active values did not survive restart.");

                RemovePresetProperty(settingsPath, Preset2Key, nameof(FakeSettings.Number));
                bool incomingNetworkUpdate = false;
                FakeSettings missingProperty = Start(
                    assemblyPath,
                    settingsPath,
                    () => incomingNetworkUpdate);
                Assert(missingProperty.Number == 5, "Missing preset property did not use its code default.");

                incomingNetworkUpdate = true;
                missingProperty.Number = 99;
                incomingNetworkUpdate = false;
                missingProperty.SelectedPreset = 0;
                missingProperty.SelectedPreset = 1;
                Assert(missingProperty.Number == 5, "Incoming network value polluted the local preset.");

                CorruptMetadata(settingsPath);
                FakeSettings recovered = Start(assemblyPath, settingsPath, () => false);
                Assert(recovered.SelectedPreset == 0, "Corrupt metadata did not recover as preset 1.");
                Assert(Directory.GetFiles(
                    Path.GetDirectoryName(settingsPath),
                    ModName + ".msgpack.corrupt-*").Length > 0,
                    "Corrupt preset data was not backed up.");

                Console.WriteLine("All lobby-settings preset tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static int AuditPresetFile(string path)
        {
            try
            {
                Dictionary<string, byte[]> payload = Read(path);
                int schema = MessagePackSerializer.Deserialize<int>(payload[SchemaKey]);
                int active = MessagePackSerializer.Deserialize<int>(payload[ActiveKey]);
                Dictionary<string, byte[]> preset1 =
                    MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload[Preset1Key]);
                Dictionary<string, byte[]> preset2 = payload.TryGetValue(Preset2Key, out byte[] preset2Bytes)
                    ? MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(preset2Bytes)
                    : null;
                Dictionary<string, byte[]> activePreset = active == 1 ? preset2 : preset1;
                string[] propertyKeys = payload.Keys
                    .Where(key => key != SchemaKey && key != ActiveKey &&
                        key != Preset1Key && key != Preset2Key)
                    .ToArray();
                string[] mismatches = propertyKeys
                    .Where(key => activePreset == null ||
                        !activePreset.TryGetValue(key, out byte[] bytes) ||
                        !payload[key].SequenceEqual(bytes))
                    .ToArray();

                Console.WriteLine($"Path: {path}");
                Console.WriteLine($"Schema: {schema}");
                Console.WriteLine($"Active preset: {active + 1}");
                Console.WriteLine($"Preset 1 properties: {preset1.Count}");
                Console.WriteLine($"Preset 2 saved: {preset2 != null}");
                Console.WriteLine($"Preset 2 properties: {preset2?.Count ?? 0}");
                Console.WriteLine($"Top-level properties: {propertyKeys.Length}");
                Console.WriteLine($"Top-level/active mismatches: {mismatches.Length}");
                if (mismatches.Length > 0)
                    Console.WriteLine("Mismatched keys: " + string.Join(", ", mismatches));

                return mismatches.Length == 0 ? 0 : 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void ValidateInstalledLegacyFiles()
        {
            const string pluginRoot =
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins";
            if (!Directory.Exists(pluginRoot))
                return;

            string[] selectedModFolders =
            {
                "BugfixesAndQoL_Serp",
                "BuildingCosts_Serp",
                "BuildingLimit_Serp",
                "ExtraFeatures_Serp",
                "ImprovedHunters_Serp",
                "RandomEvents_Serp",
                "StartConditions_Serp",
                "UnitCosts_Serp",
                "UnitLimit_Serp",
            };

            int validated = 0;
            foreach (string folder in selectedModFolders)
            {
                string path = Path.Combine(
                    pluginRoot,
                    folder,
                    "LobbyModSettings",
                    folder + ".msgpack");
                if (!File.Exists(path))
                    continue;

                Dictionary<string, byte[]> original = Read(path);
                Dictionary<string, byte[]> augmented = original.ToDictionary(
                    entry => entry.Key,
                    entry => (byte[])entry.Value.Clone(),
                    StringComparer.Ordinal);
                if (!original.ContainsKey(SchemaKey))
                {
                    augmented[SchemaKey] = MessagePackSerializer.Serialize(1);
                    augmented[ActiveKey] = MessagePackSerializer.Serialize(0);
                    augmented[Preset1Key] = MessagePackSerializer.Serialize(original);
                }

                Dictionary<string, byte[]> roundTrip =
                    MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                        MessagePackSerializer.Serialize(augmented));
                foreach (KeyValuePair<string, byte[]> entry in original)
                {
                    Assert(
                        roundTrip.TryGetValue(entry.Key, out byte[] bytes) &&
                        bytes.SequenceEqual(entry.Value),
                        $"Extender-compatible key [{entry.Key}] changed for [{path}].");
                }

                validated++;
            }

            Console.WriteLine($"Validated {validated} installed legacy MessagePack files in memory.");
        }

        private static FakeSettings Start(
            string assemblyPath,
            string settingsPath,
            Func<bool> suppressSave)
        {
            FakeSettings settings = new FakeSettings();
            settings.PreparePresets(null, assemblyPath, ModName);

            if (File.Exists(settingsPath))
                ApplyTopLevelSettings(settings, Read(settingsPath));

            AttachExtenderSave(settings, settingsPath, suppressSave);
            settings.ActivatePresets();
            return settings;
        }

        private static void AttachExtenderSave(
            FakeSettings settings,
            string settingsPath,
            Func<bool> suppressSave)
        {
            PropertyChangedEventHandler handler = (sender, args) =>
            {
                if (!suppressSave())
                    WriteTopLevelSettings(settingsPath, settings);
            };
            settings.PropertyChanged += handler;
        }

        private static void ApplyTopLevelSettings(
            FakeSettings settings,
            Dictionary<string, byte[]> payload)
        {
            if (payload.TryGetValue(nameof(FakeSettings.EnableMod), out byte[] enabled))
                settings.EnableMod = MessagePackSerializer.Deserialize<bool>(enabled);
            if (payload.TryGetValue(nameof(FakeSettings.Number), out byte[] number))
                settings.Number = MessagePackSerializer.Deserialize<int>(number);
        }

        private static void WriteLegacy(string path, bool enabled, int number)
        {
            Dictionary<string, byte[]> payload = new Dictionary<string, byte[]>
            {
                [nameof(FakeSettings.EnableMod)] = MessagePackSerializer.Serialize(enabled),
                [nameof(FakeSettings.Number)] = MessagePackSerializer.Serialize(number),
            };
            File.WriteAllBytes(path, MessagePackSerializer.Serialize(payload));
        }

        private static void WriteTopLevelSettings(string path, FakeSettings settings)
        {
            WriteLegacy(path, settings.EnableMod, settings.Number);
        }

        private static Dictionary<string, byte[]> Read(string path)
        {
            return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                File.ReadAllBytes(path));
        }

        private static void RemovePresetProperty(
            string path,
            string presetKey,
            string propertyName)
        {
            Dictionary<string, byte[]> payload = Read(path);
            Dictionary<string, byte[]> preset =
                MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload[presetKey]);
            preset.Remove(propertyName);
            payload[presetKey] = MessagePackSerializer.Serialize(preset);
            File.WriteAllBytes(path, MessagePackSerializer.Serialize(payload));
        }

        private static void CorruptMetadata(string path)
        {
            Dictionary<string, byte[]> payload = Read(path);
            payload[SchemaKey] = new byte[] { 0xC1 };
            File.WriteAllBytes(path, MessagePackSerializer.Serialize(payload));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class FakeSettings : PresetLobbyModSettingsViewModel
        {
            private bool enableMod = true;
            private int number = 5;

            [SyncHostOnly]
            public bool EnableMod
            {
                get => enableMod;
                set
                {
                    if (enableMod == value)
                        return;
                    enableMod = value;
                    OnPropertyChanged(nameof(EnableMod));
                }
            }

            [SyncHostOnly]
            public int Number
            {
                get => number;
                set
                {
                    if (number == value)
                        return;
                    number = value;
                    OnPropertyChanged(nameof(Number));
                }
            }
        }
    }
}
