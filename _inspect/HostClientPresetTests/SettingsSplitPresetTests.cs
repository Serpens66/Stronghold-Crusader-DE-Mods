using APIShared.ModSettings;
using ExtendedData;
using MessagePack;
using Shared;
using SHCDESE.API;
using Spectator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Localization is a presentation boundary; persistence exercises the real setters/controller.
internal static class SerpLocalization
{
    internal static string Get(string key) => key;
}

internal static class SettingsSplitPresetTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static readonly string[] ExtendedOptions = { "EnableMissionModSettings", "EnableCoopPackages", "EnableLordDataIntegration", "EnableEditorFileIntegration", "EnableWorkshopUploads" };
    private static readonly string[] SpectatorOptions = { "EnableTestTools" };

    private static T Prepare<T>(string file, string id) where T : PresetLobbyModSettingsViewModel, new()
    {
        var model = new T();
        model.PreparePresets(null, file, id);
        model.ActivatePresets();
        return model;
    }
    private static bool Get(object model, string name) => (bool)model.GetType().GetProperty(name).GetValue(model, null);
    private static void Set(object model, string name, bool value) => model.GetType().GetProperty(name).SetValue(model, value, null);
    private static void Role(bool host)
    {
        GameNetworkAPI.LocalHost = host;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
    }
    private static void RoundTrip<T>(string root, string id, string[] options) where T : PresetLobbyModSettingsViewModel, new()
    {
        Role(true);
        string file = Path.Combine(root, id + ".dll");
        T vm = Prepare<T>(file, id);
        foreach (string name in options) Check(Get(vm, name), id + " default true: " + name);
        foreach (string name in options) Set(vm, name, false);
        var saved = vm.System_CreateCurrentWorkingSnapshot();
        var selection = options.Select(name => new PresetSaveSelection { PropertyName = name, Mode = PublishedPresetValueMode.Fixed }).ToArray();
        vm.System_SavePersonalPreset("split-values", "Split values", "", selection, false);
        foreach (string name in options) Set(vm, name, true);
        var preset = vm.System_TestPublishedPresets.Single(item => item.Id == "split-values");
        vm.System_TestLoadPreset(preset.StableId);
        foreach (string name in options) Check(!Get(vm, name), id + " named preset retains false: " + name);
        T restart = Prepare<T>(file, id);
        foreach (string name in options) Check(!Get(restart, name), id + " restart retains false: " + name);
        restart.System_LoadModDefaults();
        foreach (string name in options) Check(Get(restart, name), id + " reset true: " + name);

        var legacy = new Dictionary<string, byte[]>(saved);
        foreach (string name in options) legacy.Remove(name);
        T oldPreset = Prepare<T>(Path.Combine(root, id + "-old.dll"), id + "-old");
        oldPreset.System_ApplyWorkingSnapshot(legacy);
        foreach (string name in options) Check(Get(oldPreset, name), id + " missing old key uses default: " + name);
    }
    public static int Main()
    {
        try
        {
            string root = TestWorkDirectory.New("SettingsSplitPresets");
            Program.PresetFixtureInstallRoot = Path.Combine(root, "installation");
            Directory.CreateDirectory(Program.PresetFixtureInstallRoot);
            PersonalSettingsPaths.TestDocumentsDirectory = _ => Path.Combine(root, "documents");
            RoundTrip<ExtendedDataSettingsViewModel>(Program.PresetFixtureInstallRoot, "ExtendedSplit", ExtendedOptions);
            RoundTrip<SpectatorViewModel>(Program.PresetFixtureInstallRoot, "SpectatorSplit", SpectatorOptions);

            Role(true);
            var vm = Prepare<ExtendedDataSettingsViewModel>(Path.Combine(root, "roles.dll"), "SplitRoles");
            vm.EnableMissionModSettings = false;
            vm.EnableEditorFileIntegration = false;
            vm.HostSettingsEnabled = false;
            Check(vm.FeatureActivation.WorkshopUploads && !vm.FeatureActivation.CoopPackages,
                "host switch keeps local upload active");
            vm.HostSettingsEnabled = true;
            vm.ClientSettingsEnabled = false;
            Check(!vm.EnableMissionModSettings && !vm.EnableEditorFileIntegration && vm.FeatureActivation.CoopPackages,
                "main switches retain suboptions and do not suppress the opposite category");
            Role(false); vm.System_RefreshSettingsAccess();
            vm.EnableCoopPackages = false;
            Check(vm.EnableCoopPackages, "client cannot change new host option");
            vm.EnableEditorFileIntegration = true;
            vm.EnableWorkshopUploads = false;
            Check(vm.EnableEditorFileIntegration && !vm.EnableWorkshopUploads, "client can edit both local options");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => { vm.EnableMod = false; vm.EnableLordDataIntegration = false; });
            Check(!vm.EnableMod && !vm.EnableLordDataIntegration && vm.EnableEditorFileIntegration,
                "authoritative update never replaces local preferences");
            Role(true); vm.System_RefreshSettingsAccess();
            vm.EnableMod = true;
            var trail = vm.System_CreateCurrentWorkingSnapshot();
            foreach (string name in new[] { "EnableClientFeatures", "EnableEditorFileIntegration", "EnableWorkshopUploads", "PlayerTrailPropertyIds", "FixedTrailPropertyIds" }) trail.Remove(name);
            vm.System_EnterMissionPreset(trail, "Trail", false);
            bool retained = vm.EnableEditorFileIntegration;
            vm.EnableEditorFileIntegration = !retained;
            Check(vm.EnableEditorFileIntegration == retained, "read-only Trail rejects local setter commands");
            vm.System_ExitMissionPreset();

            Role(true);
            var spectator = Prepare<SpectatorViewModel>(Path.Combine(root, "spectator-roles.dll"), "SpectatorRoles");
            Role(false); spectator.System_RefreshSettingsAccess();
            spectator.EnableStatistics = false;
            Check(spectator.EnableStatistics, "Spectator setter cannot bypass host authority");
            spectator.EnableClientFeatures = false;
            spectator.EnableTestTools = false;
            Check(spectator.Activation.Statistics && !spectator.Activation.TestTools, "spectator host permission survives local opt-out");
            Console.WriteLine("PASS: " + checks + " real consumer preset, defaults, persistence and role checks. Fixture: " + root);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
