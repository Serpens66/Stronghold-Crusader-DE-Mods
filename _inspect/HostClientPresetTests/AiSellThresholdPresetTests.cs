using APIShared.ModSettings;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;

internal static class AiSellThresholdPresetTests
{
    // Mirrors the production bool setter contract; the native tests separately assert
    // the production property classification/default and reconciliation wiring.
    private sealed class Probe : PresetLobbyModSettingsViewModel
    {
        private bool enabled = true;
        [SyncHostOnly]
        public bool EnableAiSellThresholdFix
        {
            get => enabled;
            set
            {
                if (!CanMutateSetting(nameof(EnableAiSellThresholdFix)) || enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(EnableAiSellThresholdFix));
            }
        }
    }

    internal static void Run()
    {
        Check(new Probe().EnableAiSellThresholdFix, "new setting defaults to enabled");
        string guid = "AiSellThresholdPresetProbe";
        string targetGuid = "Tests." + guid;
        string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "TestPlugin.dll");
        string file = new PersonalSettingsPaths(plugin, targetGuid).SettingsFile;
        string personalPresetDirectory = new PersonalSettingsPaths(plugin, targetGuid).Presets;
        if (File.Exists(file)) File.Delete(file);
        if (Directory.Exists(personalPresetDirectory)) Directory.Delete(personalPresetDirectory, true);
        GameNetworkAPI.ThrowOnRoleQuery = false;
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        WritePresetBool(file, nameof(Probe.EnableAiSellThresholdFix), false);
        var vm = new Probe();
        vm.PreparePresets(null, plugin, guid);
        vm.ActivatePresets();
        Check(!vm.EnableAiSellThresholdFix, "stored false was not loaded");
        Check(!ReadWorkingBool(file, nameof(vm.EnableAiSellThresholdFix)),
            "migrated host working state is not enabled");
        GameNetworkAPI.LocalHost = false;
        vm.System_RefreshSettingsAccess();
        vm.EnableAiSellThresholdFix = true;
        Check(!vm.EnableAiSellThresholdFix, "client cannot edit host value");
        byte[] stored = File.ReadAllBytes(file);
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableAiSellThresholdFix = true);
        Check(vm.EnableAiSellThresholdFix, "host sync accepted on client");
        Check(stored.SequenceEqual(File.ReadAllBytes(file)), "remote value not persisted locally");
        Check(!ReadWorkingBool(file, nameof(vm.EnableAiSellThresholdFix)),
            "remote sync changed the locally stored host working state");
        GameNetworkAPI.LocalHost = true;
        string legacyGuid = guid + "Legacy";
        string legacyFile = new PersonalSettingsPaths(plugin, "Tests." + legacyGuid).SettingsFile;
        WritePresetBool(legacyFile, "RemovedOldSetting", false);
        var legacy = new Probe();
        legacy.PreparePresets(null, plugin, legacyGuid);
        legacy.ActivatePresets();
        Check(legacy.EnableAiSellThresholdFix, "missing setting in old preset must remain enabled");
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = false;
        vm.System_RefreshSettingsAccess();
        var disabled = vm.System_CreateCurrentWorkingSnapshot();
        disabled[nameof(vm.EnableAiSellThresholdFix)] = MessagePackSerializer.Serialize(false);
        vm.System_ApplyWorkingSnapshot(disabled);
        var saved = vm.System_CreateCurrentWorkingSnapshot();
        Check(!vm.EnableAiSellThresholdFix, "saved false snapshot");
        string presetOne = vm.System_SavePersonalPreset("sell-one", "Sell one", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(vm.EnableAiSellThresholdFix), Mode = PublishedPresetValueMode.Fixed } }, false);
        vm.EnableAiSellThresholdFix = true;
        string presetTwo = vm.System_SavePersonalPreset("sell-two", "Sell two", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(vm.EnableAiSellThresholdFix), Mode = PublishedPresetValueMode.Fixed } }, false);
        var one = vm.System_TestPublishedPresets.Single(item => item.Id == "sell-one");
        var two = vm.System_TestPublishedPresets.Single(item => item.Id == "sell-two");
        vm.System_TestLoadPreset(one.StableId); Check(!vm.EnableAiSellThresholdFix, "first personal preset retains false");
        vm.System_TestLoadPreset(two.StableId); Check(vm.EnableAiSellThresholdFix, "second personal preset retains true");
        vm.System_LoadModDefaults();
        Check(vm.EnableAiSellThresholdFix, "reset default true");
        vm.System_EnterMissionPreset(saved, "Trail", false);
        Check(!vm.EnableAiSellThresholdFix && !vm.CanEditHostSettings, "read-only Trail false");
        vm.System_ExitMissionPreset();
        Check(vm.EnableAiSellThresholdFix, "Trail exit restores local true");
        vm.System_EnterMissionPreset(saved, "Save", false);
        Check(!vm.EnableAiSellThresholdFix, "save snapshot restore false");
        vm.System_ExitMissionPreset();
        Check(vm.EnableAiSellThresholdFix, "save exit restores local working state");
        string root = FindRoot();
        string source = File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs"));
        Check(source.Contains("private bool enableAiSellThresholdFix = true;") &&
            source.Contains("[SyncHostOnly]\r\n        public bool EnableAiSellThresholdFix") &&
            source.Contains("SetSetting(ref enableAiSellThresholdFix, value, nameof(EnableAiSellThresholdFix))") &&
            source.Contains("EnableAiSellThresholdFix = true;"), "production property default, host classification, setter and reset");
        string runtime = File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs"));
        Check(runtime.Contains("settings.EnableMod && settings.EnableAiSellThresholdFix") &&
            runtime.Contains("TryApplyFeature(\"AI sell threshold safety\", ApplyAiSellThresholdFix)"), "main switch and central settings application on all peers");
        Console.WriteLine("PASS: AI sell setting defaults, legacy/stored values, host/client authority, persistence, reset, Trail/save snapshots and production wiring.");
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "BugfixesAndQoL/src"))) return dir.FullName;
        throw new InvalidOperationException("Workspace root not found.");
    }

    private static void WritePresetBool(string file, string propertyName, bool value)
    {
        byte[] valueBytes = MessagePackSerializer.Serialize(value);
        var currentSettings = new Dictionary<string, byte[]> { [propertyName] = valueBytes };
        var payload = new Dictionary<string, byte[]>
        {
            [propertyName] = valueBytes,
            ["__SerpPresetSchemaVersion"] = MessagePackSerializer.Serialize(3),
            ["__SerpCurrentSettings"] = MessagePackSerializer.Serialize(currentSettings),
            ["__SerpPresetDirty"] = MessagePackSerializer.Serialize(false),
            ["__SerpLegacyPresetImportCompleted"] = MessagePackSerializer.Serialize(true)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllBytes(file, MessagePackSerializer.Serialize(payload));
    }

    private static bool ReadWorkingBool(string file, string propertyName)
    {
        Dictionary<string, byte[]> payload =
            MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(file));
        Check(payload.TryGetValue("__SerpCurrentSettings", out byte[] presetBytes), "stored working-state payload missing");
        Dictionary<string, byte[]> preset =
            MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(presetBytes);
        Check(preset.TryGetValue(propertyName, out byte[] valueBytes), "stored AI sell value missing");
        return MessagePackSerializer.Deserialize<bool>(valueBytes);
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("AI sell preset: " + name);
    }
}
