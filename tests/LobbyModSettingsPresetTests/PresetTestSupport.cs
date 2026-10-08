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
    [TestClass]
    [DoNotParallelize]
    public partial class PresetTests
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

        private static string CreateLegacyPresetJson(string id, string name, bool enabled, int number) =>
            ModSettingsPresetJson.Serialize(
                TargetGuid,
                id,
                name,
                "Migrated from the previous local Preset 1/2 storage.",
                string.Empty,
                string.Empty,
                new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
                {
                    [nameof(FakeSettings.EnableMod)] = new PublishedPresetSetting
                    {
                        Mode = PublishedPresetValueMode.Fixed,
                        Value = enabled,
                    },
                    [nameof(FakeSettings.Number)] = new PublishedPresetSetting
                    {
                        Mode = PublishedPresetValueMode.Fixed,
                        Value = (long)number,
                    },
                });

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
            PropertyInfo property = typeof(SHCDESE.API.GameXAMLManagerAPI).GetProperty(
                nameof(SHCDESE.API.GameXAMLManagerAPI.CurrentLobbyModSettingsChangeOrigin),
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter == null)
                throw new InvalidOperationException("Script Extender change-origin property was not found.");

            setter.Invoke(SHCDESE.API.GameXAMLManagerAPI.Instance, new object[]
            {
                value
                    ? SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin.IncomingNetwork
                    : SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin.Local
            });
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
        private string testRoot;
        [TestInitialize] public void CreateStorage()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "APIShared-Presets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }
        [TestCleanup] public void RemoveStorage()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }

    }
}
