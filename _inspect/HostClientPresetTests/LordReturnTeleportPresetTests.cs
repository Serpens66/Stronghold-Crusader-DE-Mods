using APIShared.ModSettings;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;

internal static class LordReturnTeleportPresetTests
{
    // Isolated consumer of the actual shared preset engine. The compiled production setting,
    // initializer, reset and central setter are checked independently by LordTeleportGuard.Tests.
    private sealed class Probe : PresetLobbyModSettingsViewModel
    {
        private bool enabled = true;
        [SyncHostOnly]
        public bool EnableLordReturnTeleportFix
        {
            get => enabled;
            set
            {
                if (!CanMutateSetting(nameof(EnableLordReturnTeleportFix)) || enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(EnableLordReturnTeleportFix));
            }
        }
    }

    internal static void Run()
    {
        Check(new Probe().EnableLordReturnTeleportFix, "new setting defaults to enabled");
        string guid = "LordReturnTeleportPresetProbe";
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
        WritePresetBool(file, nameof(Probe.EnableLordReturnTeleportFix), false);
        var vm = new Probe();
        vm.PreparePresets(null, plugin, guid);
        vm.ActivatePresets();
        Check(!vm.EnableLordReturnTeleportFix, "stored false was not loaded");
        Check(!ReadWorkingBool(file, nameof(vm.EnableLordReturnTeleportFix)),
            "migrated host working state is not enabled");
        var restarted = new Probe();
        restarted.PreparePresets(null, plugin, guid); restarted.ActivatePresets();
        Check(!restarted.EnableLordReturnTeleportFix, "stored false survives a new settings instance");
        GameNetworkAPI.LocalHost = false;
        vm.System_RefreshSettingsAccess();
        vm.EnableLordReturnTeleportFix = true;
        Check(!vm.EnableLordReturnTeleportFix, "client cannot edit host value");
        byte[] stored = File.ReadAllBytes(file);
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableLordReturnTeleportFix = true);
        Check(vm.EnableLordReturnTeleportFix, "host sync accepted on client");
        Check(stored.SequenceEqual(File.ReadAllBytes(file)), "remote value not persisted locally");
        Check(!ReadWorkingBool(file, nameof(vm.EnableLordReturnTeleportFix)),
            "remote sync changed the locally stored host working state");
        GameNetworkAPI.LocalHost = true;
        string legacyGuid = guid + "Legacy";
        string legacyFile = new PersonalSettingsPaths(plugin, "Tests." + legacyGuid).SettingsFile;
        WritePresetBool(legacyFile, "RemovedOldSetting", false);
        var legacy = new Probe();
        legacy.PreparePresets(null, plugin, legacyGuid);
        legacy.ActivatePresets();
        Check(legacy.EnableLordReturnTeleportFix, "missing setting in old preset uses enabled default");
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = false;
        vm.System_RefreshSettingsAccess();
        var disabled = vm.System_CreateCurrentWorkingSnapshot();
        disabled[nameof(vm.EnableLordReturnTeleportFix)] = MessagePackSerializer.Serialize(false);
        vm.System_ApplyWorkingSnapshot(disabled);
        var saved = vm.System_CreateCurrentWorkingSnapshot();
        Check(!vm.EnableLordReturnTeleportFix, "saved false snapshot");
        string presetOne = vm.System_SavePersonalPreset("lord-one", "Lord one", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(vm.EnableLordReturnTeleportFix), Mode = PublishedPresetValueMode.Fixed } }, false);
        vm.EnableLordReturnTeleportFix = true;
        string presetTwo = vm.System_SavePersonalPreset("lord-two", "Lord two", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(vm.EnableLordReturnTeleportFix), Mode = PublishedPresetValueMode.Fixed } }, false);
        var one = vm.System_TestPublishedPresets.Single(item => item.Id == "lord-one");
        var two = vm.System_TestPublishedPresets.Single(item => item.Id == "lord-two");
        vm.System_TestLoadPreset(one.StableId); Check(!vm.EnableLordReturnTeleportFix, "first personal preset retains false");
        vm.System_TestLoadPreset(two.StableId); Check(vm.EnableLordReturnTeleportFix, "second personal preset retains true");
        vm.System_LoadModDefaults();
        Check(vm.EnableLordReturnTeleportFix, "reset default true");
        vm.EnableLordReturnTeleportFix = true;
        vm.System_EnterMissionPreset(saved, "Trail", false);
        Check(!vm.EnableLordReturnTeleportFix && !vm.CanEditHostSettings, "read-only Trail false");
        vm.System_ExitMissionPreset();
        Check(vm.EnableLordReturnTeleportFix, "Trail exit restores local true");
        vm.System_EnterMissionPreset(saved, "Save", false);
        Check(!vm.EnableLordReturnTeleportFix, "save snapshot restore false");
        vm.System_ExitMissionPreset();
        Check(vm.EnableLordReturnTeleportFix, "save exit restores local working state");
        Console.WriteLine("PASS: Lord teleport setting defaults, legacy/stored values, host/client authority, persistence, reset, Trail/save snapshots; compiled production contracts are checked separately.");
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
        Check(preset.TryGetValue(propertyName, out byte[] valueBytes), "stored Lord teleport value missing");
        return MessagePackSerializer.Deserialize<bool>(valueBytes);
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Lord teleport preset: " + name);
    }
}
