using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API.Components.Network;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LobbyModSettingsPresetTests
{
    public partial class PresetTests
    {
        [TestMethod]
        public void LegacyStorageAndNetworkRoundTrip()
        {
            string root = testRoot;

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
                PublishedModSettingsPreset migratedOne = dualMigrated.System_TestPublishedPresets.Single(item =>
                    item.Id == "legacy-preset-1" && item.SourceKind == ModSettingsPresetSourceKind.Personal);
                PublishedModSettingsPreset migratedTwo = dualMigrated.System_TestPublishedPresets.Single(item =>
                    item.Id == "legacy-preset-2" && item.SourceKind == ModSettingsPresetSourceKind.Personal);
                Assert(migratedOne.CanOverwrite && migratedTwo.CanOverwrite,
                    "Migrated slots were not exposed as overwriteable personal presets.");
                dualMigrated.System_TestLoadPreset(migratedOne.StableId);
                Assert(!dualMigrated.EnableMod && dualMigrated.Number == 11,
                    "Migrated Preset 1 could not be loaded.");
                dualMigrated.System_TestLoadPreset(migratedTwo.StableId);
                Assert(dualMigrated.EnableMod && dualMigrated.Number == 22,
                    "Migrated Preset 2 could not be loaded.");
                dualMigrated.Number = 44;
                dualMigrated.System_SavePersonalPreset(
                    "legacy-preset-2",
                    "Preset 2 (migrated)",
                    "Migrated from the previous local Preset 1/2 storage.",
                    new[]
                    {
                        new PresetSaveSelection { PropertyName = nameof(FakeSettings.EnableMod), Mode = PublishedPresetValueMode.Fixed },
                        new PresetSaveSelection { PropertyName = nameof(FakeSettings.Number), Mode = PublishedPresetValueMode.Fixed },
                    },
                    overwrite: true);
                dualMigrated.System_TestLoadPreset(migratedTwo.StableId);
                Assert(dualMigrated.EnableMod && dualMigrated.Number == 44,
                    "Migrated Preset 2 could not be overwritten and loaded again.");

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
        }

    }
}
