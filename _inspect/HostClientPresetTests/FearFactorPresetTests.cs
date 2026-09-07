using System;
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
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        var vm = new Probe();
        vm.PreparePresets(null, plugin, guid);
        vm.ActivatePresets();
        Check(!vm.EnableFearFactorNeutralization, "default off");
        vm.EnableFearFactorNeutralization = true;
        vm.SelectedPreset = 1;
        Check(!vm.EnableFearFactorNeutralization, "second preset defaults off");
        vm.SelectedPreset = 0;
        Check(vm.EnableFearFactorNeutralization, "host preset restored");
        GameNetworkAPI.LocalHost = false;
        vm.System_RefreshSettingsAccess();
        vm.EnableFearFactorNeutralization = false;
        Check(vm.EnableFearFactorNeutralization, "client cannot edit host value");
        byte[] stored = File.ReadAllBytes(file);
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableFearFactorNeutralization = false);
        Check(!vm.EnableFearFactorNeutralization, "host sync accepted on client");
        Check(stored.SequenceEqual(File.ReadAllBytes(file)), "remote value not persisted locally");
        GameNetworkAPI.LocalHost = true;
        var restarted = new Probe();
        restarted.PreparePresets(null, plugin, guid);
        restarted.ActivatePresets();
        Check(restarted.EnableFearFactorNeutralization, "local preset survives remote sync and restart");
        Console.WriteLine("PASS: fear-factor host preset, client lock, sync and persistence");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Fear-factor preset: " + name);
    }
}
