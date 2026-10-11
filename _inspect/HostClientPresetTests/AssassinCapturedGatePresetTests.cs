using APIShared.ModSettings;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;

internal static class AssassinCapturedGatePresetTests
{
    // Mirrors the production bool setter contract; the native tests separately assert
    // the production property classification/default and reconciliation wiring.
    private sealed class Probe : PresetLobbyModSettingsViewModel
    {
        private bool enabled = true;
        [SyncHostOnly]
        public bool EnableAssassinCapturedGateProtectionFix
        {
            get => enabled;
            set
            {
                if (!CanMutateSetting(nameof(EnableAssassinCapturedGateProtectionFix)) || enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(EnableAssassinCapturedGateProtectionFix));
            }
        }
    }

    internal static void Run()
    {
        Check(new Probe().EnableAssassinCapturedGateProtectionFix, "new setting defaults to enabled");
        string guid = "AssassinCapturedGatePresetProbe";
        string targetGuid = "Tests." + guid;
        string plugin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestPlugin.dll");
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LobbyModSettings", guid + ".msgpack");
        string personalPresetDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "LobbyModSettings",
            "Presets",
            "Override",
            targetGuid);
        if (File.Exists(file)) File.Delete(file);
        if (Directory.Exists(personalPresetDirectory)) Directory.Delete(personalPresetDirectory, true);
        GameNetworkAPI.ThrowOnRoleQuery = false;
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        WritePresetBool(file, nameof(Probe.EnableAssassinCapturedGateProtectionFix), false);
        var vm = new Probe();
        vm.PreparePresets(null, plugin, guid);
        vm.ActivatePresets();
        Check(!vm.EnableAssassinCapturedGateProtectionFix, "stored false was not loaded");
        Check(!ReadWorkingBool(file, nameof(vm.EnableAssassinCapturedGateProtectionFix)),
            "migrated host working state is not enabled");
        GameNetworkAPI.LocalHost = false;
        vm.System_RefreshSettingsAccess();
        vm.EnableAssassinCapturedGateProtectionFix = true;
        Check(!vm.EnableAssassinCapturedGateProtectionFix, "client cannot edit host value");
        byte[] stored = File.ReadAllBytes(file);
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableAssassinCapturedGateProtectionFix = true);
        Check(vm.EnableAssassinCapturedGateProtectionFix, "host sync accepted on client");
        Check(stored.SequenceEqual(File.ReadAllBytes(file)), "remote value not persisted locally");
        Check(!ReadWorkingBool(file, nameof(vm.EnableAssassinCapturedGateProtectionFix)),
            "remote sync changed the locally stored host working state");
        GameNetworkAPI.LocalHost = true;
        string legacyGuid = guid + "Legacy";
        string legacyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LobbyModSettings", legacyGuid + ".msgpack");
        WritePresetBool(legacyFile, "RemovedOldSetting", false);
        var legacy = new Probe();
        legacy.PreparePresets(null, plugin, legacyGuid);
        legacy.ActivatePresets();
        Check(legacy.EnableAssassinCapturedGateProtectionFix, "missing setting in old preset must remain enabled");
        Console.WriteLine("PASS: captured-gate default, stored false, old preset, client lock, host sync and persistence");
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
        Check(preset.TryGetValue(propertyName, out byte[] valueBytes), "stored captured-gate value missing");
        return MessagePackSerializer.Deserialize<bool>(valueBytes);
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Captured-gate preset: " + name);
    }
}
