using ExtendedData;
using ExtendedData.Core;
using Shared;
using CrusaderDE;

internal static class TrailSetupTests
{
    internal static void Sources()
    {
        var state = new TrailSettingsSourceState();
        var authored = ModSettingsJson.ParseObject("{\"schemaVersion\":3,\"mods\":{\"mod\":{\"playerSettings\":[\"personal\"],\"overrides\":{\"fixed\":17,\"sequence\":[1,2]}}}}");
        string original = ModSettingsJson.Serialize(authored);
        int result = state.Activate(authored, "coop:package:fingerprint:1:1", () =>
        {
            Check(!state.IsAvailable, "source published before activation completed");
            authored.Mods["mod"].Overrides["fixed"] = 99;
            return 42;
        });
        Check(result == 42 && state.IsAvailable && state.ContextId == "coop:package:fingerprint:1:1", "successful source missing");
        Check(ModSettingsJson.Serialize(state.Capture()) == original, "live draft changed reset rules");
        ((System.Collections.IList)authored.Mods["mod"].Overrides["sequence"])[0] = 88L;
        Check(ModSettingsJson.Serialize(state.Capture()) == original, "authored sequence aliases the reset source");
        var reset = state.Capture();
        ((System.Collections.IList)reset.Mods["mod"].Overrides["sequence"])[1] = 89L;
        reset.Mods["mod"].PlayerSettings[0] = "edited";
        reset.Mods["mod"].Overrides.Clear();
        Check(ModSettingsJson.Serialize(state.Capture()) == original, "reset result aliases the authored source");
        state.Set(ModSettingsDefinition.CreateModDefaults(), "coop:package:fingerprint:1:2");
        Check(state.IsAvailable && state.Capture().Mods.Count == 0 && state.ContextId.EndsWith(":1:2"), "empty valid mission lost its source or identity");
        try { state.Activate<bool>(authored, "failed", () => throw new InvalidOperationException("activation failed")); }
        catch (InvalidOperationException) { }
        Check(!state.IsAvailable && state.ContextId == "", "failed switch retained an old source");
        state.Set(authored, "restored");
        var invalid = new ModSettingsDefinition { SchemaVersion = -1 };
        bool applied = false;
        try { state.Activate(invalid, "invalid", () => applied = true); }
        catch (InvalidDataException) { }
        Check(!applied && !state.IsAvailable, "invalid document was applied or retained the previous source");
        state.Set(authored, "restored");
        state.Clear();
        Check(!state.IsAvailable && state.Capture() == null && state.ContextId == "", "exit retained a mission source");
        state.Set(null, "trail-without-sidecar");
        Check(!state.IsAvailable, "a missing sidecar became a Trail reset source");
    }

    internal static void Presentation()
    {
        // The real production adapter runs against game-boundary doubles. This checks
        // the transition refresh and its outputs, not Vanilla's team algorithm or Noesis rendering.
        foreach (bool host in new[] { true, false })
        foreach (bool panelActive in new[] { true, false })
        foreach (int[] shields in new[] { new[] { 1, 1, 2, 2 }, new[] { 1, 1, -1, 2, 2 }, new[] { -1 } })
        {
            var setup = new EngineInterface.MultiplayerSetupData { advanced_skirmish_options = 1, HasAdvancedOption = true, StartingGold = 777, StartingSpeed = 47 };
            var header = new FileHeader { Title = "Current map", Briefing = "Map briefing", Size = 400 };
            var lobby = new FRONT_Multiplayer(setup, header, host, shields);
            lobby.panelActive = panelActive;
            MainViewModel.Instance = new MainViewModel { StandaloneMissionText = "Custom mission description", Show_SkirmishTeamsPanel = false };
            CoopTrailSetupPresentation.Refresh(lobby, preserveMissionText: true);
            Check(lobby.Members.Select(member => member.TeamShield).SequenceEqual(shields), "lobby shields stayed stale");
            Check(lobby.RadarShields.SequenceEqual(shields), "radar was refreshed before team shields");
            Check(MainViewModel.Instance.StartingGold == 777 && MainViewModel.Instance.Show_SkirmishAdvancedEnabled,
                "setup indicators stayed stale on host or client");
            Check(MainViewModel.Instance.StandaloneMissionTitle == header.Title && MainViewModel.Instance.MapSize == 400 &&
                MainViewModel.Instance.StandaloneMissionText == "Custom mission description", "map details or custom text lost");
            Check(!MainViewModel.Instance.Show_SkirmishTeamsPanel && lobby.TeamRefreshes == 1, "refresh opened the team panel");
            Check(lobby.MappingsUpdated == host, "client remapped host-owned players");
            Check(setup.StartingSpeed == 47 && lobby.panelActive == panelActive, "presentation changed game speed or input activation");
            CoopTrailSetupPresentation.Refresh(lobby, preserveMissionText: true);
            Check(lobby.RadarShields.SequenceEqual(shields), "repeated refresh changed team presentation");
            setup.advanced_skirmish_options = 0;
            CoopTrailSetupPresentation.Refresh(lobby);
            Check(!MainViewModel.Instance.Show_SkirmishAdvancedEnabled && MainViewModel.Instance.StandaloneMissionText == "Map briefing",
                "inactive advanced options or ordinary map briefing stayed stale");
        }
        var missingMap = new FRONT_Multiplayer(new EngineInterface.MultiplayerSetupData(), null, true, Array.Empty<int>());
        try { CoopTrailSetupPresentation.Refresh(missingMap); throw new Exception("missing map accepted"); }
        catch (InvalidOperationException) { Check(missingMap.TeamRefreshes == 0, "invalid setup partially refreshed"); }
        var failing = new FRONT_Multiplayer(new EngineInterface.MultiplayerSetupData(), new FileHeader(), true, new[] { 1, 1 });
        failing.ThrowOnSetupSettings = true;
        try { CoopTrailSetupPresentation.Refresh(failing); throw new Exception("injected refresh failure was swallowed"); }
        catch (System.Reflection.TargetInvocationException) { Check(failing.panelActive, "refresh failure left input disabled"); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

// Names and signatures model the reflection boundary inspected in the installed game.
public sealed class FileHeader { public string Title, Briefing; public int Size; }
public static class EngineInterface
{
    public sealed class MultiplayerSetupData
    {
        public int advanced_skirmish_options, StartingGold, StartingSpeed;
        public bool HasAdvancedOption;
        public bool advancedSkirmishOptionsEnabled() => HasAdvancedOption;
    }
}
namespace CrusaderDE
{
    public sealed class MainViewModel
    {
        public static MainViewModel Instance = new();
        public string StandaloneMissionText, StandaloneMissionTitle;
        public bool Show_SkirmishAdvancedEnabled, Show_SkirmishTeamsPanel;
        public int StartingGold, MapSize;
    }
    public sealed partial class FRONT_Multiplayer
    {
        private readonly EngineInterface.MultiplayerSetupData MPsetupData;
        private readonly FileHeader selectedMPHeader;
        private readonly bool isHost;
        public object currentLobby = new();
        public readonly Member[] Members;
        public int[] RadarShields;
        public int TeamRefreshes;
        public bool MappingsUpdated;
        public bool panelActive = true, ThrowOnSetupSettings;
        public sealed class Member { public int TeamShield = -1, ExpectedShield; }
        public FRONT_Multiplayer(EngineInterface.MultiplayerSetupData setup, FileHeader header, bool host, int[] shields)
        {
            MPsetupData = setup;
            selectedMPHeader = header;
            isHost = host;
            Members = shields.Select(shield => new Member { ExpectedShield = shield }).ToArray();
        }
        private bool updateSteamIDMappings() { MappingsUpdated = isHost; return isHost; }
        private void SetupSkirmishModeSettings()
        {
            if (ThrowOnSetupSettings) throw new InvalidOperationException("injected setup projection failure");
            MainViewModel.Instance.StartingGold = MPsetupData.StartingGold;
            // A boundary double of Vanilla's ValueChanged callback, including its panelActive gate.
            if (panelActive) MPsetupData.StartingSpeed = 0;
        }
        private void PopulateMapDetailsPanel(FileHeader header)
        {
            MainViewModel.Instance.StandaloneMissionText = header.Briefing;
            MainViewModel.Instance.StandaloneMissionTitle = header.Title;
            MainViewModel.Instance.MapSize = header.Size;
        }
        private void PopulateTeamsPanel()
        {
            TeamRefreshes++;
            foreach (var member in Members) member.TeamShield = member.ExpectedShield;
        }
        private void UpdateRadarShieldPositions() => RadarShields = Members.Select(member => member.TeamShield).ToArray();
    }
}
