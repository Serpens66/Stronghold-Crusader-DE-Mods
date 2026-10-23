using System;
using System.Reflection;
using CrusaderDE;

namespace Shared
{
    // Called once on the Unity thread when a Coop Trail becomes an editable setup.
    // Vanilla's CreateTeamShields skips coopGame; PopulateTeamsPanel also initializes
    // the teamShield values consumed by lobby rows and radar without opening the panel.
    internal static class CoopTrailSetupPresentation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo SetupData = typeof(FRONT_Multiplayer).GetField("MPsetupData", PrivateInstance);
        private static readonly FieldInfo SelectedHeader = typeof(FRONT_Multiplayer).GetField("selectedMPHeader", PrivateInstance);
        private static readonly MethodInfo SetupSettings = Find("SetupSkirmishModeSettings");
        private static readonly MethodInfo UpdateMappings = Find("updateSteamIDMappings");
        private static readonly MethodInfo PopulateTeams = Find("PopulateTeamsPanel");
        private static readonly MethodInfo UpdateRadar = Find("UpdateRadarShieldPositions");
        private static readonly MethodInfo PopulateMap = Find("PopulateMapDetailsPanel", typeof(FileHeader));

        internal static void Refresh(FRONT_Multiplayer lobby, bool preserveMissionText = false)
        {
            if (lobby?.currentLobby == null)
                throw new InvalidOperationException("Coop setup presentation requires an active lobby.");
            var setup = Read<EngineInterface.MultiplayerSetupData>(SetupData, lobby, "MPsetupData");
            var header = Read<FileHeader>(SelectedHeader, lobby, "selectedMPHeader");
            if (setup == null || header == null)
                throw new InvalidOperationException("Coop setup presentation requires mission setup and map data.");
            MainViewModel view = MainViewModel.Instance;
            string missionText = view.StandaloneMissionText;
            Invoke(UpdateMappings, lobby, "updateSteamIDMappings");
            // The four Coop speed sliders raise ValueChanged. Like Vanilla doOpen,
            // suppress input callbacks while projecting settings, then restore even on failure.
            bool panelWasActive = lobby.panelActive;
            lobby.panelActive = false;
            try { Invoke(SetupSettings, lobby, "SetupSkirmishModeSettings"); }
            finally { lobby.panelActive = panelWasActive; }
            view.Show_SkirmishAdvancedEnabled = setup.advanced_skirmish_options > 0 && setup.advancedSkirmishOptionsEnabled();
            try { Invoke(PopulateMap, lobby, "PopulateMapDetailsPanel", header); }
            finally { if (preserveMissionText) view.StandaloneMissionText = missionText; }
            Invoke(PopulateTeams, lobby, "PopulateTeamsPanel");
            Invoke(UpdateRadar, lobby, "UpdateRadarShieldPositions");
        }

        private static MethodInfo Find(string name, params Type[] parameters) =>
            typeof(FRONT_Multiplayer).GetMethod(name, PrivateInstance, null, parameters, null);

        private static T Read<T>(FieldInfo field, object owner, string name) where T : class =>
            (T)(field ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, name)).GetValue(owner);

        private static void Invoke(MethodInfo method, object owner, string name, params object[] arguments) =>
            (method ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name)).Invoke(owner, arguments);
    }
}
