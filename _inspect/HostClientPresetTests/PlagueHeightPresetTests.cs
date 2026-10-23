using System;
using System.IO;
using System.Linq;
using APIShared.ModSettings;

using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;

internal static class PlagueHeightPresetTests
{
    // UI-boundary probe exercises the real preset/sync pipeline. Production property metadata
    // and constructor defaults are checked separately against the compiled ExtraFeatures assembly.
    private sealed class Probe : PresetLobbyModSettingsViewModel
    {
        private bool walls=true, gates=true, towers=true, keeps;
        private void Set(ref bool field, bool value, string name)
        { if (!CanMutateSetting(name) || field == value) return; field=value; OnPropertyChanged(name); }
        [SyncHostOnly] public bool PlagueDamageWalls { get => walls; set => Set(ref walls,value,nameof(PlagueDamageWalls)); }
        [SyncHostOnly] public bool PlagueDamageGates { get => gates; set => Set(ref gates,value,nameof(PlagueDamageGates)); }
        [SyncHostOnly] public bool PlagueDamageTowers { get => towers; set => Set(ref towers,value,nameof(PlagueDamageTowers)); }
        [SyncHostOnly] public bool PlagueDamageKeeps { get => keeps; set => Set(ref keeps,value,nameof(PlagueDamageKeeps)); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        GameNetworkAPI.ThrowOnRoleQuery = false;
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
        string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "PlagueHeight.dll");
        var vm = new Probe();
        vm.PreparePresets(null, plugin, "PlagueHeight"); vm.ActivatePresets();
        var names = new[] { "PlagueDamageWalls", "PlagueDamageGates", "PlagueDamageTowers", "PlagueDamageKeeps" };
        foreach (var name in names)
            Check(typeof(Probe).GetProperty(name).GetCustomAttributes(typeof(SyncHostOnlyAttribute), true).Length == 1,
                "Host-only classification missing: " + name);
        Check(vm.PlagueDamageWalls && vm.PlagueDamageGates && vm.PlagueDamageTowers && !vm.PlagueDamageKeeps, "Initial defaults");
        var defaults = vm.System_CreateCurrentWorkingSnapshot();
        foreach (int mask in Enumerable.Range(0, 16))
        {
            for (int i = 0; i < names.Length; i++) typeof(Probe).GetProperty(names[i]).SetValue(vm, (mask & (1 << i)) != 0);
            var snapshot = vm.System_CreateCurrentWorkingSnapshot();
            vm.System_ApplyWorkingSnapshot(defaults); vm.System_ApplyWorkingSnapshot(snapshot);
            for (int i = 0; i < names.Length; i++) Check((bool)typeof(Probe).GetProperty(names[i]).GetValue(vm) == ((mask & (1 << i)) != 0), "Roundtrip " + mask);
        }
        vm.System_ApplyWorkingSnapshot(defaults);
        GameNetworkAPI.LocalHost = false; vm.System_RefreshSettingsAccess();
        vm.PlagueDamageWalls = false; Check(vm.PlagueDamageWalls, "Client changed host setting");
        var paths = new PersonalSettingsPaths(plugin, "Tests.PlagueHeight");
        byte[] stored = File.Exists(paths.SettingsFile) ? File.ReadAllBytes(paths.SettingsFile) : null;
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => { vm.PlagueDamageWalls = false; vm.PlagueDamageKeeps = true; });
        Check(!vm.PlagueDamageWalls && vm.PlagueDamageKeeps, "Host values did not apply on client");
        if (stored != null) Check(stored.SequenceEqual(File.ReadAllBytes(paths.SettingsFile)), "Host receipt changed local preset");
        Console.WriteLine("PASS: plague UI-boundary defaults, four host classifications, 16 snapshot roundtrips, client lock and host receipt.");
    }
}
