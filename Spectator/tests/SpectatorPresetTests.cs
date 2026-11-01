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
            Check(!vm.EnableStatistics && vm.EnableEliminatedPlayersBecomeSpectators, "statistics are local; promotion remains host controlled");
            vm.EnableSpectatorPerspective=false;
            Check(!vm.Activation.Perspective && !Prepare(path).EnableSpectatorPerspective,"local perspective preference survives restart");
            vm.EnableStatistics=true;
            vm.EnableClientFeatures = false;
            vm.EnableSingleplayerTestMode = true;
            Check(vm.EnableSingleplayerTestMode && !vm.Activation.SingleplayerTest && !vm.Activation.Statistics && !vm.Activation.Overview && vm.Activation.Defeat && !vm.Activation.TestTools,
                "local master gates local views and tools without revoking host promotion");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.EnableEliminatedPlayersBecomeSpectators = false);
            Check(!vm.Activation.Statistics && !vm.Activation.Defeat, "statistics remain available without automatic spectator promotion");
            var snapshot = vm.System_CreateCurrentWorkingSnapshot();
            Check(!snapshot.ContainsKey("EnableSurrenderAndStatistics"), "Spectator never inherits another mod's surrender preference");
            vm.EnableClientFeatures=true; vm.EnableStatistics=true; vm.EnableSpectatorPerspective=true;
            Check(vm.Activation.Overview && vm.Overview.EnableMod && vm.Activation.Statistics,"local UI can be enabled independently of promotion");
            GameNetworkAPI.LocalHost=true;vm.System_RefreshSettingsAccess();vm.EnableMod=false;
            Check(vm.Activation.Overview && vm.Activation.Statistics && vm.Activation.Perspective && !vm.Activation.Defeat,"host master never gates local UI");
            vm.EnableLordOverview=false;
            Check(!vm.Overview.EnableMod && vm.Activation.Statistics,"overview switch does not hide the separate detailed statistics");
            Check(OverviewPresetTests.Run(new[]{Path.Combine(root,"overview")})==0,"integrated overview preset contracts");
            LegacySpectatorSettingsTests.Run();
            Console.WriteLine("PASS: production Spectator settings persistence, reset, host/client authority and independent activation.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
