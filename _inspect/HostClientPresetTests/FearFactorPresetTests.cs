using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;

internal static class FearFactorPresetTests
{
    // Mirrors the production bool setter contract; the native tests separately assert
    // the production property classification/default and reconciliation wiring.
    private sealed class Probe : PresetLobbyModSettingsViewModel
    {
        private bool enabled;
        [SyncHostOnly]
        public bool EnableFearFactorNeutralization
        {
            get => enabled;
            set
            {
                if (!CanMutateSetting(nameof(EnableFearFactorNeutralization)) || enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(EnableFearFactorNeutralization));
            }
        }
    }

    internal static void Run()
    {
        string guid = "FearFactorPresetProbe";
        string plugin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestPlugin.dll");
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LobbyModSettings", guid + ".msgpack");
        if (File.Exists(file)) File.Delete(file);
        GameNetworkAPI.ThrowOnRoleQuery = false;
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        WritePresetBool(file, nameof(Probe.EnableFearFactorNeutralization), true);
        var vm = new Probe();
        vm.PreparePresets(null, plugin, guid);
        vm.ActivatePresets();
        Check(vm.EnableFearFactorNeutralization, "stored host preset was not loaded");
        Check(ReadPresetBool(file, "__SerpPreset1", nameof(vm.EnableFearFactorNeutralization)),
            "host preset fixture is not enabled");
        GameNetworkAPI.LocalHost = false;
        vm.System_RefreshSettingsAccess();
        vm.EnableFearFactorNeutralization = false;
        Check(vm.EnableFearFactorNeutralization, "client cannot edit host value");
        byte[] stored = File.ReadAllBytes(file);
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableFearFactorNeutralization = false);
        Check(!vm.EnableFearFactorNeutralization, "host sync accepted on client");
        Check(stored.SequenceEqual(File.ReadAllBytes(file)), "remote value not persisted locally");
        Check(ReadPresetBool(file, "__SerpPreset1", nameof(vm.EnableFearFactorNeutralization)),
            "remote sync changed the locally stored host preset");
        Console.WriteLine("PASS: fear-factor host preset, client lock, sync and persistence");
    }

    private static void WritePresetBool(string file, string propertyName, bool value)
    {
        byte[] valueBytes = MessagePackSerializer.Serialize(value);
        var preset = new Dictionary<string, byte[]> { [propertyName] = valueBytes };
        var payload = new Dictionary<string, byte[]>
        {
            [propertyName] = valueBytes,
            ["__SerpPresetSchemaVersion"] = MessagePackSerializer.Serialize(1),
            ["__SerpActivePreset"] = MessagePackSerializer.Serialize(0),
            ["__SerpPreset1"] = MessagePackSerializer.Serialize(preset)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllBytes(file, MessagePackSerializer.Serialize(payload));
    }

    private static bool ReadPresetBool(string file, string presetKey, string propertyName)
    {
        Dictionary<string, byte[]> payload =
            MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(file));
        Check(payload.TryGetValue(presetKey, out byte[] presetBytes), "stored preset payload missing");
        Dictionary<string, byte[]> preset =
            MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(presetBytes);
        Check(preset.TryGetValue(propertyName, out byte[] valueBytes), "stored fear-factor value missing");
        return MessagePackSerializer.Deserialize<bool>(valueBytes);
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Fear-factor preset: " + name);
    }
}
