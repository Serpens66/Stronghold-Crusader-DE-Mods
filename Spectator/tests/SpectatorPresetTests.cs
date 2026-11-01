using System;
using System.IO;
using System.Linq;
using APIShared.ModSettings;
using SHCDESE.API;
using Spectator;
using System.Collections.Generic;
using MessagePack;

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
            CheckMissionIndependentHostSettings(root);
            Check(OverviewPresetTests.Run(new[]{Path.Combine(root,"overview")})==0,"integrated overview preset contracts");
            LegacySpectatorSettingsTests.Run();
            Console.WriteLine("PASS: production Spectator settings persistence, reset, host/client authority and independent activation.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void CheckHost(SpectatorViewModel vm, bool enabled, bool promotion, bool enemies, string reason)
    {
        Check(vm.EnableMod == enabled && vm.EnableEliminatedPlayersBecomeSpectators == promotion &&
            vm.AllowEnemyDetails == enemies, reason);
    }

    private static Dictionary<string, byte[]> OldMissionSnapshot() => new Dictionary<string, byte[]> {
        { nameof(SpectatorViewModel.EnableMod), MessagePackSerializer.Serialize(false) },
        { nameof(SpectatorViewModel.EnableEliminatedPlayersBecomeSpectators), MessagePackSerializer.Serialize(true) },
        { nameof(SpectatorViewModel.AllowEnemyDetails), MessagePackSerializer.Serialize(false) }
    };

    private static void CheckMissionIndependentHostSettings(string root)
    {
        var documents = PersonalSettingsPaths.TestDocumentsDirectory;
        bool localHost = GameNetworkAPI.LocalHost;
        try
        {
            // Exercise the real endpoint with old full source payloads. The APIShared suite
            // separately covers the map/save/restart coordinators; these are consumer tests.
            foreach (string source in new[] { "Map", "Trail", "CoopTrail", "Savegame" })
            foreach (bool editable in new[] { false, true })
            {
                PersonalSettingsPaths.TestDocumentsDirectory = _ => Path.Combine(root, "host-" + source + editable);
                GameNetworkAPI.LocalHost = true;
                string path = Path.Combine(root, "host-" + source + editable, "Spectator.dll");
                var vm = Prepare(path);
                var hosts = ModSettingsApplication.GetHostProperties(vm);
                Check(hosts.Select(p => p.Name).OrderBy(n => n).SequenceEqual(new[] {
                    nameof(vm.AllowEnemyDetails), nameof(vm.EnableEliminatedPlayersBecomeSpectators), nameof(vm.EnableMod)
                }.OrderBy(n => n)), "all three preferences retain Host network ownership");
                Check(hosts.All(p => p.IsDefined(typeof(MissionIndependentAttribute), true)), "every Host preference opts out of mission ownership");
                Check(ModSettingsApplication.GetMissionHostProperties(vm).Length == 0, "mission authoring/save capture has no Spectator host options");
                Check(vm.System_GetPresetSettingDescriptors().Where(p => p.Scope == PresetSettingScope.Host).All(p => p.MissionIndependent), "shared preset/search metadata reports the same policy");
                CheckHost(vm, true, true, false, "original defaults unchanged");
                vm.EnableEliminatedPlayersBecomeSpectators = false; vm.AllowEnemyDetails = true;
                vm.System_SavePersonalPreset("host-options", "Host options", "", hosts.Select(p => new PresetSaveSelection {
                    PropertyName = p.Name, Mode = PublishedPresetValueMode.Fixed }).ToArray(), false);
                string preset = vm.System_TestPublishedPresets.Single(p => p.Id == "host-options").StableId;
                vm.System_EnterMissionPreset(OldMissionSnapshot(), source, editable);
                CheckHost(vm, true, false, true, "old mission payload cannot prescribe any Host preference");
                Check(vm.CanEditMissionIndependentHostSettings && vm.CanToggleHostSettings && vm.CanEditHostSettings == editable,
                    "independent Host controls and activation remain editable despite a source lock");
                vm.HostSettingsEnabled = false; vm.EnableEliminatedPlayersBecomeSpectators = true; vm.AllowEnemyDetails = false;
                CheckHost(Prepare(path), false, true, false, "Host edits persist immediately within the source context");
                if (editable)
                {
                    vm.System_TestLoadPreset(preset);
                    CheckHost(Prepare(path), true, false, true, "consciously loaded personal preset persists Host preferences in Customize");
                    // Customize has an external source provider in the game. Substitute
                    // that boundary, retaining the real shared reset/application code.
                    var sourceProvider = new DefaultsSource(vm);
                    ModSettingsWorkingSourceRegistry.Register(sourceProvider);
                    try { vm.ResetToDefaultCommand.Execute(null); }
                    finally { ModSettingsWorkingSourceRegistry.Unregister(sourceProvider); }
                    CheckHost(Prepare(path), true, true, false, "explicit Customize reset still changes personal preferences");
                    vm.HostSettingsEnabled = false;
                }
                else
                {
                    bool rejected = false;
                    try { vm.System_TestLoadPreset(preset); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && !vm.CanChangePreset && !vm.CanResetSettings, "preset loading and reset keep their existing source-lock rules");
                }
                for (int restart = 0; restart < 2; restart++)
                {
                    vm.System_ExitMissionPreset();
                    vm.System_EnterMissionPreset(OldMissionSnapshot(), source + " replacement", editable);
                    CheckHost(vm, false, true, false, "source replacement/re-entry cannot revive an old Host snapshot");
                }
                vm.System_ExitMissionPreset();
                CheckHost(Prepare(path), false, true, false, "Host preferences survive source exit and process restart");

                PersonalSettingsPaths.TestDocumentsDirectory = _ => Path.Combine(root, "client-" + source + editable);
                path = Path.Combine(root, "client-" + source + editable, "Spectator.dll");
                var client = Prepare(path);
                client.EnableEliminatedPlayersBecomeSpectators = false; client.AllowEnemyDetails = true;
                GameNetworkAPI.LocalHost = false; client.System_RefreshSettingsAccess();
                client.System_EnterMissionPreset(OldMissionSnapshot(), source, editable);
                Check(!client.CanEditMissionIndependentHostSettings && !client.CanToggleHostSettings, "marker never grants a client Host authority");
                client.HostSettingsEnabled = false; client.EnableEliminatedPlayersBecomeSpectators = true; client.AllowEnemyDetails = false;
                CheckHost(client, true, false, true, "all unauthorized client edits are rejected");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(client, () => {
                    client.EnableMod = false; client.EnableEliminatedPlayersBecomeSpectators = true; client.AllowEnemyDetails = false;
                });
                CheckHost(client, false, true, false, "authorized Host synchronization still applies under a locked source");
                CheckHost(Prepare(path), true, false, true, "received Host values never overwrite this client's personal file");
                client.System_ExitMissionPreset();
                CheckHost(client, false, true, false, "source exit does not replace received independent Host values");
            }
        }
        finally { PersonalSettingsPaths.TestDocumentsDirectory = documents; GameNetworkAPI.LocalHost = localHost; }
    }

    private sealed class DefaultsSource : IModSettingsWorkingSourceProvider
    {
        private readonly SpectatorViewModel model;
        internal DefaultsSource(SpectatorViewModel model) { this.model = model; }
        public event Action SourcesChanged { add { } remove { } }
        public IReadOnlyList<ModSettingsWorkingSource> GetSources(string guid) => Array.Empty<ModSettingsWorkingSource>();
        public void Apply(string guid, string source)
        {
            Check(source == ModSettingsWorkingSourceRegistry.ModDefaultsId, "reset requests the shared code-default source");
            model.System_ApplyMissionPresetSnapshot(model.System_CreateModDefaultSnapshot(), "Mod defaults");
        }
        public void ApplyMany(IEnumerable<string> guids, string source) { foreach (string guid in guids) Apply(guid, source); }
    }
}
