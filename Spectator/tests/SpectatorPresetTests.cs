using System;
using System.IO;
using System.Linq;
using APIShared.ModSettings;
using SHCDESE.API;
using Spectator;

internal static class SerpLocalization {
    internal const string ResetToDefault = "Common.ResetToDefault", ResetToDefaultHelp = "Common.ResetToDefaultHelp";
    internal static string Get(string key) => key;
}
internal static class SpectatorPresetTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static SpectatorViewModel Prepare(string path)
    {
        var vm = new SpectatorViewModel(); vm.PreparePresets(null, path, "SpectatorContract"); vm.ActivatePresets(); return vm;
    }
    public static int Main()
    {
        try
        {
            string root = TestWorkDirectory.New("SpectatorPresets");
            PersonalSettingsPaths.TestDocumentsDirectory = _ => Path.Combine(root, "documents");
            GameNetworkAPI.Networked = GameNetworkAPI.MultiplayerGame = GameNetworkAPI.LocalHost = true;
            string path = Path.Combine(root, "Spectator.dll");
            var vm = Prepare(path);
            Check(!vm.EnableSingleplayerTestMode && !vm.Activation.SingleplayerTest, "active-player test mode is opt-in");
            vm.EnableSingleplayerTestMode = true;
            vm.EnableTestTools = false;
            Check(vm.Activation.SingleplayerTest && !vm.Activation.TestTools && Prepare(path).EnableSingleplayerTestMode,
                "independent local test modes persist across restart");
            vm.ResetToDefaultCommand.Execute(null);
            Check(!vm.EnableSingleplayerTestMode && vm.EnableTestTools, "visible shared reset turns active-player tools off");
            var changes = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            vm.EnableMod = false;
            Check(vm.HostSettingsOpacity == 0.65f && vm.CanEditHostSettings && changes.Contains(nameof(vm.HostSettingsOpacity)), "host switch dims via shared controller without revoking editing");
            vm.EnableMod = true;
            Check(vm.HostSettingsOpacity == 1f, "host switch restores full opacity");
            vm.EnableTestTools = false;
            vm.System_SavePersonalPreset("tools-off", "Tools off", "", new[] { new PresetSaveSelection {
                PropertyName = nameof(vm.EnableTestTools), Mode = PublishedPresetValueMode.Fixed } }, false);
            vm.EnableTestTools = true;
            vm.System_TestLoadPreset(vm.System_TestPublishedPresets.Single(p => p.Id == "tools-off").StableId);
            Check(!vm.EnableTestTools && !Prepare(path).EnableTestTools, "local tools persist through preset and restart");
            vm.System_LoadModDefaults(); Check(vm.EnableTestTools, "tools default restored");
            GameNetworkAPI.LocalHost = false; vm.System_RefreshSettingsAccess();
            vm.EnableStatistics = false; vm.EnableEliminatedPlayersBecomeSpectators = false;
            Check(vm.EnableStatistics && vm.EnableEliminatedPlayersBecomeSpectators, "client cannot change host spectator permissions");
            vm.EnableClientFeatures = false;
            vm.EnableSingleplayerTestMode = true;
            Check(vm.EnableSingleplayerTestMode && !vm.Activation.SingleplayerTest && vm.Activation.Statistics && vm.Activation.Defeat && !vm.Activation.TestTools,
                "client may configure its test preference but local activation never revokes host information or defeat rights");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableEliminatedPlayersBecomeSpectators = false);
            Check(vm.Activation.Statistics && !vm.Activation.Defeat, "statistics remain available without automatic spectator promotion");
            var snapshot = vm.System_CreateCurrentWorkingSnapshot();
            Check(!snapshot.ContainsKey("EnableSurrenderAndStatistics"), "Spectator never inherits another mod's surrender preference");
            LegacySpectatorSettingsTests.Run();
            Console.WriteLine("PASS: production Spectator settings persistence, reset, host/client authority and independent activation.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
