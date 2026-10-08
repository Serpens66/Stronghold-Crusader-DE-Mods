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
        public void ViewModelWithoutPersistentSettings() => TestViewModelWithoutPersistentSettings(testRoot);

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
        [TestMethod]
        public void LegacyPublicationFailureRetainsValidStorage() => TestLegacyPublicationFailureRetainsValidStorage(testRoot);

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
        [TestMethod]
        public void PersonalPresetDeletion() => TestPersonalPresetDeletion(testRoot);

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
        [TestMethod]
        public void LegacyPartialPublicationRetry() => TestLegacyPartialPublicationRetry(testRoot);

        private static void TestLegacyPartialPublicationRetry(string root)
        {
            string retryRoot = Path.Combine(root, "PartialRetry");
            string assemblyPath = Path.Combine(retryRoot, "PresetTest.dll");
            string settingsPath = Path.Combine(retryRoot, "LobbyModSettings", ModName + ".msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            WriteLegacySlots(settingsPath, active: 1,
                preset1Enabled: false, preset1Number: 31,
                preset2Enabled: true, preset2Number: 32,
                publishedStableId: string.Empty);

            string personalDirectory = Path.Combine(
                retryRoot, "LobbyModSettings", "Presets", "Override", TargetGuid);
            Directory.CreateDirectory(personalDirectory);
            string presetOnePath = Path.Combine(personalDirectory, "preset_legacy-preset-1.json");
            File.WriteAllText(presetOnePath, CreateLegacyPresetJson(
                "legacy-preset-1", "Preset 1 (migrated)", false, 31));

            FakeSettings retried = Start(assemblyPath, settingsPath, () => false);
            Assert(retried.EnableMod && retried.Number == 32,
                "A partial legacy publication retry did not restore the active Preset 2 working values.");
            Assert(File.Exists(Path.Combine(personalDirectory, "preset_legacy-preset-2.json")) &&
                    MessagePackSerializer.Deserialize<int>(Read(settingsPath)[SchemaKey]) == 3,
                "A valid partially published migration was not completed atomically on retry.");

            string conflictRoot = Path.Combine(root, "PartialConflict");
            string conflictAssembly = Path.Combine(conflictRoot, "PresetTest.dll");
            string conflictSettings = Path.Combine(conflictRoot, "LobbyModSettings", ModName + ".msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(conflictSettings));
            WriteLegacySlots(conflictSettings, active: 0,
                preset1Enabled: false, preset1Number: 41,
                preset2Enabled: true, preset2Number: 42,
                publishedStableId: string.Empty);
            byte[] original = File.ReadAllBytes(conflictSettings);
            string conflictDirectory = Path.Combine(
                conflictRoot, "LobbyModSettings", "Presets", "Override", TargetGuid);
            Directory.CreateDirectory(conflictDirectory);
            string conflictPath = Path.Combine(conflictDirectory, "preset_legacy-preset-1.json");
            File.WriteAllText(conflictPath, "{\"unrelated\":true}");

            Start(conflictAssembly, conflictSettings, () => false);
            Assert(File.ReadAllBytes(conflictSettings).SequenceEqual(original),
                "A conflicting personal migration file caused the valid schema-1 MessagePack to be rewritten.");
            Assert(File.ReadAllText(conflictPath) == "{\"unrelated\":true}" &&
                    Directory.GetFiles(Path.GetDirectoryName(conflictSettings), "*.corrupt-*").Length == 0,
                "A conflicting personal migration file was overwritten or mislabeled the valid MessagePack as corrupt.");
        }
        [TestMethod]
        public void PresetAtomicPublisher() => TestPresetAtomicPublisher();

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
                        !File.Exists(temporaryPath) &&
                        Directory.GetFiles(realDirectory, "*.replace-backup-*").Length == 0,
                    "Atomic preset publishing did not replace a real file and consume its temporary source.");

                string missingSourcePath = Path.Combine(realDirectory, "missing.tmp");
                File.WriteAllText(destinationPath, "preserved");
                bool failedWithIoException = false;
                try
                {
                    AtomicFileReplacement.Replace(missingSourcePath, destinationPath);
                }
                catch (IOException exception)
                {
                    failedWithIoException = exception.InnerException is System.ComponentModel.Win32Exception;
                }
                Assert(failedWithIoException && File.ReadAllText(destinationPath) == "preserved",
                    "Failed atomic replacement did not preserve the destination and expose an IOException.");
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

    }
}
