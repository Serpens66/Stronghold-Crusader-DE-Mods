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
    internal const string ResetToDefault = "Common.ResetToDefault", ResetToDefaultHelp = "Common.ResetToDefaultHelp";
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
    // Saves the non-default value of every option and checks preset, restart, reset and old snapshots.
    private static void RoundTrip<T>(string root, string id, string[] options, bool defaultValue = true) where T : PresetLobbyModSettingsViewModel, new()
    {
        Role(true);
        string file = Path.Combine(root, id + ".dll");
        T vm = Prepare<T>(file, id);
        foreach (string name in options) Check(Get(vm, name) == defaultValue, id + " default " + defaultValue + ": " + name);
        foreach (string name in options) Set(vm, name, !defaultValue);
        var saved = vm.System_CreateCurrentWorkingSnapshot();
        var selection = options.Select(name => new PresetSaveSelection { PropertyName = name, Mode = PublishedPresetValueMode.Fixed }).ToArray();
        vm.System_SavePersonalPreset("split-values", "Split values", "", selection, false);
        foreach (string name in options) Set(vm, name, defaultValue);
        var preset = vm.System_TestPublishedPresets.Single(item => item.Id == "split-values");
        vm.System_TestLoadPreset(preset.StableId);
        foreach (string name in options) Check(Get(vm, name) != defaultValue, id + " named preset retains non-default: " + name);
        T restart = Prepare<T>(file, id);
        foreach (string name in options) Check(Get(restart, name) != defaultValue, id + " restart retains non-default: " + name);
        restart.System_LoadModDefaults();
        foreach (string name in options) Check(Get(restart, name) == defaultValue, id + " reset to default: " + name);

        var legacy = new Dictionary<string, byte[]>(saved);
        foreach (string name in options) legacy.Remove(name);
        T oldPreset = Prepare<T>(Path.Combine(root, id + "-old.dll"), id + "-old");
        oldPreset.System_ApplyWorkingSnapshot(legacy);
        foreach (string name in options) Check(Get(oldPreset, name) == defaultValue, id + " missing old key uses default: " + name);
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
            RoundTrip<SpectatorViewModel>(Program.PresetFixtureInstallRoot, "SpectatorSplit", SpectatorOptions, false);

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
            // Since APIShared 62.3/ExtendedData 1.0.23, only mission (SyncHostOnly, not
            // MissionIndependent) values are locked; personal Local values stay outside the draft.
            // Synthetic: real Map/Trail flows never enter ExtendedData's own settings (the plugin is
            // savegame-excluded); its model only serves as a vehicle for APIShared's lock contract.
            bool retained = vm.EnableLordDataIntegration;
            vm.EnableLordDataIntegration = !retained;
            Check(vm.EnableLordDataIntegration == retained, "read-only Trail rejects host mission setter commands");
            bool local = vm.EnableEditorFileIntegration;
            vm.EnableEditorFileIntegration = !local;
            Check(vm.EnableEditorFileIntegration == !local, "read-only Trail keeps personal Local options editable");
            vm.EnableEditorFileIntegration = local;
            vm.System_ExitMissionPreset();

            Role(true);
            var spectator = Prepare<SpectatorViewModel>(Path.Combine(root, "spectator-roles.dll"), "SpectatorRoles");
            Role(false); spectator.System_RefreshSettingsAccess();
            // Promotion is host-only; statistics, views and tools are local (Spectator 0.1.0).
            spectator.EnableEliminatedPlayersBecomeSpectators = false;
            Check(spectator.EnableEliminatedPlayersBecomeSpectators, "Spectator setter cannot bypass host authority");
            spectator.EnableClientFeatures = false;
            spectator.EnableTestTools = false;
            Check(spectator.Activation.Defeat && !spectator.Activation.Statistics && !spectator.Activation.TestTools,
                "host promotion survives the local opt-out, which gates local views and tools");
            Console.WriteLine("PASS: " + checks + " real consumer preset, defaults, persistence and role checks. Fixture: " + root);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
