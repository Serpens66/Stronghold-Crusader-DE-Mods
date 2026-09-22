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
        private const string TargetGuid = "Tests.PresetTest_Serp";
        private const string SchemaKey = "__SerpPresetSchemaVersion";
        private const string ActiveKey = "__SerpActivePreset";
        private const string Preset1Key = "__SerpPreset1";
        private const string Preset2Key = "__SerpPreset2";
        private const string PublishedPresetKey = "__SerpPublishedPreset";
        private const string CurrentSettingsKey = "__SerpCurrentSettings";
        private const string BasedOnPresetKey = "__SerpBasedOnPreset";
        private const string PresetDirtyKey = "__SerpPresetDirty";
        private const string LegacyPresetImportCompletedKey = "__SerpLegacyPresetImportCompleted";

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
                TestPresetAtomicPublisher();
                ValidateInstalledLegacyFiles();
                TestViewModelWithoutPersistentSettings(root);
                TestLegacyPublicationFailureRetainsValidStorage(root);
                TestPersonalPresetDeletion(root);

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
                Assert(MessagePackSerializer.Deserialize<int>(payload[SchemaKey]) == 3,
                    "Legacy file was not migrated to working-state schema 3.");
                Assert(payload.ContainsKey(CurrentSettingsKey) &&
                        MessagePackSerializer.Deserialize<bool>(payload[LegacyPresetImportCompletedKey]) &&
                        !payload.ContainsKey(ActiveKey) &&
                        !payload.ContainsKey(Preset1Key) &&
                        !payload.ContainsKey(Preset2Key),
                    "Migrated MessagePack still contains retired slot storage.");
                string personalDirectory = Path.Combine(
                    root, "LobbyModSettings", "Presets", "Override", TargetGuid);
                string legacyOnePath = Path.Combine(personalDirectory, "preset_legacy-preset-1.json");
                Assert(File.Exists(legacyOnePath), "Legacy values were not published as a personal preset file.");
                PublishedModSettingsPreset legacyOne = ModSettingsPresetJson.Parse(
                    File.ReadAllText(legacyOnePath), "personal:" + TargetGuid, "Personal", TargetGuid, legacyOnePath);
                Assert(legacyOne.Settings.Count == 2 && legacyOne.Settings.Values.All(item =>
                        item.Mode == PublishedPresetValueMode.Fixed),
                    "Migrated legacy preset did not materialize every value as fixed.");

                migrated.Number = 7;
                payload = Read(settingsPath);
                Assert(MessagePackSerializer.Deserialize<bool>(payload[PresetDirtyKey]),
                    "Editing a loaded migrated preset did not mark its working state as modified.");
                FakeSettings restarted = Start(assemblyPath, settingsPath, () => false);
                Assert(!restarted.EnableMod && restarted.Number == 7,
                    "Editable working values did not survive restart.");

                string dualRoot = Path.Combine(root, "DualSlot");
                string dualAssembly = Path.Combine(dualRoot, "PresetTest.dll");
                string dualSettings = Path.Combine(dualRoot, "LobbyModSettings", ModName + ".msgpack");
                Directory.CreateDirectory(Path.GetDirectoryName(dualSettings));
                string legacyPublishedDirectory = Path.Combine(dualRoot, "Override", TargetGuid);
                Directory.CreateDirectory(legacyPublishedDirectory);
                File.WriteAllText(
                    Path.Combine(legacyPublishedDirectory, "preset_old-published.json"),
                    ModSettingsPresetJson.Serialize(
                        TargetGuid,
                        "old-published",
                        "Old published preset",
                        "",
                        "",
                        "",
                        new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
                        {
                            [nameof(FakeSettings.EnableMod)] = new PublishedPresetSetting
                            {
                                Mode = PublishedPresetValueMode.Player,
                            },
                            [nameof(FakeSettings.Number)] = new PublishedPresetSetting
                            {
                                Mode = PublishedPresetValueMode.Fixed,
                                Value = 88L,
                            },
                        }));
                WriteLegacySlots(dualSettings, active: 1,
                    preset1Enabled: false, preset1Number: 11,
                    preset2Enabled: true, preset2Number: 22,
                    publishedStableId: TargetGuid + "\n" + TargetGuid + "\nold-published");
                FakeSettings dualMigrated = Start(dualAssembly, dualSettings, () => false);
                Assert(dualMigrated.EnableMod && dualMigrated.Number == 88,
                    "The active legacy published preset was not materialized over its underlying player slot.");
                Dictionary<string, byte[]> dualPayload = Read(dualSettings);
                Assert(MessagePackSerializer.Deserialize<string>(dualPayload[BasedOnPresetKey]).EndsWith(
                        "\n" + TargetGuid + "\nold-published", StringComparison.Ordinal),
                    "The migrated published preset did not retain its new stable source identity.");
                string dualPersonal = Path.Combine(dualRoot, "LobbyModSettings", "Presets", "Override", TargetGuid);
                Assert(File.Exists(Path.Combine(dualPersonal, "preset_legacy-preset-1.json")) &&
                        File.Exists(Path.Combine(dualPersonal, "preset_legacy-preset-2.json")),
                    "Both populated legacy slots were not preserved as personal JSON presets.");

                string stagedRoot = Path.Combine(root, "StagedExport");
                string stagedAssembly = Path.Combine(stagedRoot, "PresetTest.dll");
                string stagedDirectory = Path.Combine(
                    stagedRoot, "LobbyModSettings", "PresetExports", "Override", TargetGuid);
                Directory.CreateDirectory(stagedDirectory);
                string stagedSource = Path.Combine(stagedDirectory, "preset_old-export.json");
                File.WriteAllText(stagedSource, ModSettingsPresetJson.Serialize(
                    TargetGuid,
                    "old-export",
                    "Old export",
                    "",
                    "",
                    "",
                    new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
                    {
                        [nameof(FakeSettings.Number)] = new PublishedPresetSetting
                        {
                            Mode = PublishedPresetValueMode.Fixed,
                            Value = 73L,
                        },
                    }));
                Start(
                    stagedAssembly,
                    Path.Combine(stagedRoot, "LobbyModSettings", ModName + ".msgpack"),
                    () => false);
                string stagedDestination = Path.Combine(
                    stagedRoot, "LobbyModSettings", "Presets", "Override", TargetGuid, "preset_old-export.json");
                Assert(File.Exists(stagedSource) && File.Exists(stagedDestination),
                    "Legacy PresetExports file was not copied into personal presets without deleting the source.");
                File.Delete(stagedDestination);
                Start(
                    stagedAssembly,
                    Path.Combine(stagedRoot, "LobbyModSettings", ModName + ".msgpack"),
                    () => false);
                Assert(!File.Exists(stagedDestination),
                    "Legacy PresetExports was imported again after its migration status was persisted.");

                RemoveCurrentProperty(settingsPath, nameof(FakeSettings.Number));
                bool incomingNetworkUpdate = false;
                FakeSettings missingProperty = Start(
                    assemblyPath,
                    settingsPath,
                    () => incomingNetworkUpdate);
                Assert(missingProperty.Number == 5, "Missing preset property did not use its code default.");

                incomingNetworkUpdate = true;
                SetNetworkSyncInProgress(true);
                try
                {
                    missingProperty.Number = 99;
                }
                finally
                {
                    SetNetworkSyncInProgress(false);
                    incomingNetworkUpdate = false;
                }
                FakeSettings afterNetwork = Start(assemblyPath, settingsPath, () => false);
                Assert(afterNetwork.Number == 5, "Incoming network value polluted the editable working state.");

                CorruptMetadata(settingsPath);
                FakeSettings recovered = Start(assemblyPath, settingsPath, () => false);
                Assert(!recovered.EnableMod && recovered.Number == 5,
                    $"Corrupt metadata did not recover the last safe top-level working values " +
                    $"(EnableMod={recovered.EnableMod}, Number={recovered.Number}).");
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
                if (schema == 3)
                {
                    Dictionary<string, byte[]> current =
                        MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload[CurrentSettingsKey]);
                    string basedOn = payload.TryGetValue(BasedOnPresetKey, out byte[] basedOnBytes)
                        ? MessagePackSerializer.Deserialize<string>(basedOnBytes)
                        : string.Empty;
                    bool dirty = payload.TryGetValue(PresetDirtyKey, out byte[] dirtyBytes) &&
                        MessagePackSerializer.Deserialize<bool>(dirtyBytes);
                    string[] retired = { ActiveKey, Preset1Key, Preset2Key, PublishedPresetKey };
                    string[] unexpected = retired.Where(payload.ContainsKey).ToArray();
                    Console.WriteLine($"Path: {path}");
                    Console.WriteLine($"Schema: {schema}");
                    Console.WriteLine($"Working properties: {current.Count}");
                    Console.WriteLine($"Based on: {basedOn}");
                    Console.WriteLine($"Modified: {dirty}");
                    Console.WriteLine($"Legacy import completed: {MessagePackSerializer.Deserialize<bool>(payload[LegacyPresetImportCompletedKey])}");
                    Console.WriteLine($"Retired keys: {unexpected.Length}");
                    return unexpected.Length == 0 ? 0 : 1;
                }
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

        private static void TestViewModelWithoutPersistentSettings(string root)
        {
            string emptyRoot = Path.Combine(root, "EmptyViewModel");
            string assemblyPath = Path.Combine(emptyRoot, "Empty.dll");
            string settingsDirectory = Path.Combine(emptyRoot, "LobbyModSettings");
            string settingsPath = Path.Combine(settingsDirectory, "EmptySettings.msgpack");
            Directory.CreateDirectory(settingsDirectory);
            byte[] original = MessagePackSerializer.Serialize(
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["LegacyValue"] = MessagePackSerializer.Serialize(17),
                });
            File.WriteAllBytes(settingsPath, original);

            var settings = new EmptySettings();
            settings.PreparePresets(null, assemblyPath, "EmptySettings");
            settings.ActivatePresets();

            Assert(File.ReadAllBytes(settingsPath).SequenceEqual(original),
                "A ViewModel without persistent settings rewrote its legacy MessagePack file.");
            Assert(!Directory.Exists(Path.Combine(settingsDirectory, "Presets")),
                "A ViewModel without persistent settings created a preset catalog directory.");
            Assert(Directory.GetFiles(settingsDirectory, "*.corrupt-*").Length == 0,
                "A ViewModel without persistent settings produced a false corrupt backup.");
        }

        private static void TestLegacyPublicationFailureRetainsValidStorage(string root)
        {
            string failureRoot = Path.Combine(root, "PublicationFailure");
            string assemblyPath = Path.Combine(failureRoot, "PresetTest.dll");
            string settingsPath = Path.Combine(failureRoot, "LobbyModSettings", ModName + ".msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            WriteLegacy(settingsPath, false, 64);
            byte[] original = File.ReadAllBytes(settingsPath);

            var settings = new FakeSettings();
            settings.PreparePresets(null, assemblyPath, ModName);
            string presetsPath = Path.Combine(failureRoot, "LobbyModSettings", "Presets");
            Directory.Delete(presetsPath, recursive: true);
            File.WriteAllText(presetsPath, "blocks the preset directory");
            ApplyTopLevelSettings(settings, Read(settingsPath));
            AttachExtenderSave(settings, settingsPath, () => false);
            settings.ActivatePresets();

            Assert(File.ReadAllBytes(settingsPath).SequenceEqual(original),
                "A failed legacy JSON publication overwrote valid MessagePack data.");
            Assert(Directory.GetFiles(Path.GetDirectoryName(settingsPath),
                    ModName + ".msgpack.corrupt-*").Length == 0,
                "A failed legacy JSON publication mislabeled valid MessagePack data as corrupt.");
            Assert(!File.Exists(Path.Combine(presetsPath, "Override", TargetGuid, "preset_legacy-preset-1.json")),
                "A failed legacy JSON publication unexpectedly produced a personal preset.");
        }

        private static void TestPersonalPresetDeletion(string root)
        {
            string deleteRoot = Path.Combine(root, "DeletePersonal");
            string assemblyPath = Path.Combine(deleteRoot, "PresetTest.dll");
            string settingsPath = Path.Combine(deleteRoot, "LobbyModSettings", ModName + ".msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            WriteLegacy(settingsPath, false, 91);
            FakeSettings settings = Start(assemblyPath, settingsPath, () => false);
            PublishedModSettingsPreset personal = settings.System_TestPublishedPresets.Single(item =>
                item.SourceKind == ModSettingsPresetSourceKind.Personal &&
                item.Id == "legacy-preset-1");
            string stableId = personal.StableId;
            string personalPath = personal.SourcePath;
            bool enabled = settings.EnableMod;
            int number = settings.Number;

            settings.System_EnterMissionPreset(
                new Dictionary<string, byte[]>
                {
                    [nameof(FakeSettings.Number)] = MessagePackSerializer.Serialize(12),
                },
                "Delete mission",
                editable: true);
            settings.System_TestDeletePreset(stableId);
            Assert(!File.Exists(personalPath), "Deleting a personal preset did not remove its JSON file.");
            Assert(settings.System_TestPublishedPresets.All(item => item.StableId != stableId),
                "Deleting a personal preset did not refresh the catalog immediately.");
            settings.System_ExitMissionPreset();
            Assert(settings.EnableMod == enabled && settings.Number == number,
                "Deleting the loaded personal preset changed its materialized working values.");
            Dictionary<string, byte[]> payload = Read(settingsPath);
            Assert(!payload.ContainsKey(BasedOnPresetKey),
                "Deleting the loaded personal preset retained its stable basis identity.");

            string bundledDirectory = Path.Combine(deleteRoot, "Override", TargetGuid);
            Directory.CreateDirectory(bundledDirectory);
            string bundledPath = Path.Combine(bundledDirectory, "preset_bundled.json");
            File.WriteAllText(bundledPath, ModSettingsPresetJson.Serialize(
                TargetGuid,
                "bundled",
                "Preset 1 (migrated)",
                string.Empty,
                string.Empty,
                string.Empty,
                new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
                {
                    [nameof(FakeSettings.Number)] = new PublishedPresetSetting
                    {
                        Mode = PublishedPresetValueMode.Fixed,
                        Value = 33L,
                    },
                }));
            settings.System_TestRefreshPresetCatalog();
            PublishedModSettingsPreset bundled = settings.System_TestPublishedPresets.Single(item =>
                item.SourceKind == ModSettingsPresetSourceKind.Bundled && item.Id == "bundled");
            bool rejected = false;
            try { settings.System_TestDeletePreset(bundled.StableId); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected && File.Exists(bundledPath),
                "A bundled preset was deletable through the personal-preset API.");

            string protectedPath = settings.System_SavePersonalPreset(
                "protected",
                "Protected",
                string.Empty,
                new[]
                {
                    new PresetSaveSelection
                    {
                        PropertyName = nameof(FakeSettings.Number),
                        Mode = PublishedPresetValueMode.Fixed,
                    },
                },
                overwrite: false);
            PublishedModSettingsPreset protectedPreset = settings.System_TestPublishedPresets.Single(item =>
                item.SourceKind == ModSettingsPresetSourceKind.Personal && item.Id == "protected");
            string outsidePath = Path.Combine(deleteRoot, "outside.json");
            File.WriteAllText(outsidePath, "outside");
            protectedPreset.SourcePath = outsidePath;
            rejected = false;
            try { settings.System_TestDeletePreset(protectedPreset.StableId); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected && File.Exists(outsidePath) && File.Exists(protectedPath),
                "A manipulated personal preset path escaped its target directory during deletion.");
            protectedPreset.SourcePath = protectedPath;

            bool lockedFailure = false;
            using (var locked = new FileStream(protectedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { settings.System_TestDeletePreset(protectedPreset.StableId); }
                catch (IOException) { lockedFailure = true; }
            }
            Assert(lockedFailure && File.Exists(protectedPath) &&
                    settings.System_TestPublishedPresets.Any(item => item.StableId == protectedPreset.StableId),
                "A failed personal-preset deletion changed the file or catalog state.");
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
                "CastlePlanner_Serp",
                "CheatMod_Serp",
                "ExtendedData_Serp",
                "ExtremePowers_Serp",
                "RandomEvents_Serp",
                "StartConditions_Serp",
                "UnitCosts_Serp",
                "UnitLimit_Serp",
            };

            int validated = 0;
            foreach (string folder in selectedModFolders)
            {
                string directPath = Path.Combine(
                    pluginRoot, folder, "LobbyModSettings", folder + ".msgpack");
                string bundledPath = Path.Combine(
                    pluginRoot, "SerpsMods_Serp_steam", "Mods", folder,
                    "LobbyModSettings", folder + ".msgpack");
                string path = File.Exists(directPath) ? directPath : bundledPath;
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

            string installedBundle = Path.Combine(pluginRoot, "SerpsMods_Serp_steam", "Mods");
            if (Directory.Exists(installedBundle))
                Assert(validated >= 9, "Fewer than the nine installed legacy MessagePack examples were validated.");

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

        private static void SetNetworkSyncInProgress(bool value)
        {
            FieldInfo field = typeof(SHCDESE.API.GameXAMLManagerAPI).GetField(
                "_isProcessingNetworkSync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("Script Extender network-sync scope field was not found.");

            field.SetValue(SHCDESE.API.GameXAMLManagerAPI.Instance, value);
        }

        private static void TestPresetAtomicPublisher()
        {
            string realDirectory = Path.Combine(
                Path.GetTempPath(),
                "SerpPresetAtomicPublisher-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(realDirectory);
            try
            {
                string temporaryPath = Path.Combine(realDirectory, "preset.tmp");
                string destinationPath = Path.Combine(realDirectory, "preset.json");
                File.WriteAllText(temporaryPath, "new");
                File.WriteAllText(destinationPath, "old");
                PresetAtomicPublishResult realResult =
                    PresetAtomicFilePublisher.Publish(temporaryPath, destinationPath);
                Assert(realResult.Succeeded &&
                        File.ReadAllText(destinationPath) == "new" &&
                        Directory.GetFiles(realDirectory, "*.replace-backup-*").Length == 0,
                    "Atomic preset publishing did not replace a real file without leaving a backup.");
            }
            finally
            {
                Directory.Delete(realDirectory, true);
            }

            var retries = new FakeAtomicFileOperations(
                new[] { true, true, true },
                new Exception[]
                {
                    new IOException("first transient failure"),
                    new IOException("second transient failure")
                });
            PresetAtomicPublishResult retryResult = PresetAtomicFilePublisher.Publish(
                "temporary",
                "destination",
                retries);
            Assert(retryResult.Succeeded && retryResult.Attempts == 3 &&
                    retries.ReplaceCalls == 3 && retries.Delays.SequenceEqual(new[] { 15, 35 }),
                "Atomic preset publishing did not retry transient replace failures as specified.");

            var exhausted = new FakeAtomicFileOperations(
                new[] { true, true, true, true },
                new Exception[]
                {
                    new IOException("failure 1"),
                    new IOException("failure 2"),
                    new IOException("failure 3"),
                    new IOException("failure 4")
                });
            PresetAtomicPublishResult exhaustedResult = PresetAtomicFilePublisher.Publish(
                "temporary",
                "destination",
                exhausted);
            Assert(!exhaustedResult.Succeeded && exhaustedResult.Attempts == 4 &&
                    exhausted.Delays.SequenceEqual(new[] { 15, 35, 75 }) &&
                    exhaustedResult.Error != null,
                "Atomic preset publishing did not stop after its bounded retry budget.");

            var destinationDisappeared = new FakeAtomicFileOperations(
                new[] { true, false },
                new Exception[] { new IOException("destination disappeared") });
            PresetAtomicPublishResult moveResult = PresetAtomicFilePublisher.Publish(
                "temporary",
                "destination",
                destinationDisappeared);
            Assert(moveResult.Succeeded && moveResult.Attempts == 2 &&
                    destinationDisappeared.ReplaceCalls == 1 &&
                    destinationDisappeared.MoveCalls == 1,
                "Atomic preset publishing did not re-evaluate destination existence between attempts.");

            var nonIoFailure = new FakeAtomicFileOperations(
                new[] { true },
                new Exception[] { new UnauthorizedAccessException("permanent") });
            bool nonIoRethrown = false;
            try
            {
                PresetAtomicFilePublisher.Publish("temporary", "destination", nonIoFailure);
            }
            catch (UnauthorizedAccessException)
            {
                nonIoRethrown = true;
            }
            Assert(nonIoRethrown && nonIoFailure.ReplaceCalls == 1 && nonIoFailure.Delays.Count == 0,
                "Atomic preset publishing retried a non-IO failure.");
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

        private static void WriteLegacySlots(
            string path,
            int active,
            bool preset1Enabled,
            int preset1Number,
            bool preset2Enabled,
            int preset2Number,
            string publishedStableId = "")
        {
            Dictionary<string, byte[]> preset1 = CreateSnapshot(preset1Enabled, preset1Number);
            Dictionary<string, byte[]> preset2 = CreateSnapshot(preset2Enabled, preset2Number);
            Dictionary<string, byte[]> activeSnapshot = active == 1 ? preset2 : preset1;
            Dictionary<string, byte[]> payload = activeSnapshot.ToDictionary(
                item => item.Key,
                item => (byte[])item.Value.Clone(),
                StringComparer.Ordinal);
            payload[SchemaKey] = MessagePackSerializer.Serialize(2);
            payload[ActiveKey] = MessagePackSerializer.Serialize(active);
            payload[Preset1Key] = MessagePackSerializer.Serialize(preset1);
            payload[Preset2Key] = MessagePackSerializer.Serialize(preset2);
            if (!string.IsNullOrEmpty(publishedStableId))
                payload[PublishedPresetKey] = MessagePackSerializer.Serialize(publishedStableId);
            File.WriteAllBytes(path, MessagePackSerializer.Serialize(payload));
        }

        private static Dictionary<string, byte[]> CreateSnapshot(bool enabled, int number) =>
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [nameof(FakeSettings.EnableMod)] = MessagePackSerializer.Serialize(enabled),
                [nameof(FakeSettings.Number)] = MessagePackSerializer.Serialize(number),
            };

        private static void WriteTopLevelSettings(string path, FakeSettings settings)
        {
            WriteLegacy(path, settings.EnableMod, settings.Number);
        }

        private static Dictionary<string, byte[]> Read(string path)
        {
            return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                File.ReadAllBytes(path));
        }

        private static void RemoveCurrentProperty(string path, string propertyName)
        {
            Dictionary<string, byte[]> payload = Read(path);
            Dictionary<string, byte[]> current =
                MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload[CurrentSettingsKey]);
            current.Remove(propertyName);
            payload[CurrentSettingsKey] = MessagePackSerializer.Serialize(current);
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

        private sealed class FakeAtomicFileOperations : IPresetAtomicFileOperations
        {
            private readonly Queue<bool> destinationExists;
            private readonly Queue<Exception> failures;

            public FakeAtomicFileOperations(
                IEnumerable<bool> destinationExists,
                IEnumerable<Exception> failures)
            {
                this.destinationExists = new Queue<bool>(destinationExists);
                this.failures = new Queue<Exception>(failures);
            }

            public int ReplaceCalls { get; private set; }
            public int MoveCalls { get; private set; }
            public List<int> Delays { get; } = new List<int>();

            public bool Exists(string path)
            {
                return destinationExists.Count > 0 && destinationExists.Dequeue();
            }

            public void Replace(string sourcePath, string destinationPath)
            {
                ReplaceCalls++;
                ThrowNextFailure();
            }

            public void Move(string sourcePath, string destinationPath)
            {
                MoveCalls++;
                ThrowNextFailure();
            }

            public void Delay(int milliseconds)
            {
                Delays.Add(milliseconds);
            }

            private void ThrowNextFailure()
            {
                if (failures.Count > 0)
                    throw failures.Dequeue();
            }
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

        private sealed class EmptySettings : PresetLobbyModSettingsViewModel
        {
        }
    }
}
