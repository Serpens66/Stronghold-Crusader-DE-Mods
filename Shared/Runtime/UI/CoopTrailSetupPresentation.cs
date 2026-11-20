using System;
using System.Reflection;
using CrusaderDE;
using SHCDESE.API;

namespace Shared
{
    // Projects the Coop setup on the Unity thread. Vanilla's CreateTeamShields skips Coop.
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
            if (lobby.currentLobby.isHost || FRONT_Multiplayer.skirmishGame)
                Invoke(PopulateTeams, lobby, "PopulateTeamsPanel");
            else
                UpdateClientTeamShields(lobby);
            Invoke(UpdateRadar, lobby, "UpdateRadarShieldPositions");
            view.Show_MPPeacetime = !FRONT_Multiplayer.skirmishGame;
        }

        // PopulateTeamsPanel would also sort and normalize the client lobby's teams.
        // Project only the shield fields consumed by the rows and radar.
        internal static bool UpdateClientTeamShields(FRONT_Multiplayer lobby)
        {
            var room = lobby.currentLobby;
            var members = room.members;
            if (members.Count == 0 || members.Count > GamePlayerManagerAPI.MAX_PLAYERS)
                return false;

            Span<int> teams = stackalloc int[members.Count];
            Span<int> teamCounts = stackalloc int[GamePlayerManagerAPI.MAX_PLAYERS + 1];
            teamCounts.Clear();
            int playerSlots = 0;
            for (int index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member == null || member.dummyToBeKicked)
                    return false;
                int slot = room.getThisPlayerFromSteamID(member.id.m_SteamID);
                int team = room.getTeam(member);
                if (slot < 1 || slot > GamePlayerManagerAPI.MAX_PLAYERS || (playerSlots & (1 << slot)) != 0 ||
                    team < 0 || team > GamePlayerManagerAPI.MAX_PLAYERS)
                    return false;
                playerSlots |= 1 << slot;
                teams[index] = team;
                teamCounts[team]++;
            }

            bool changed = false;
            for (int index = 0; index < members.Count; index++)
            {
                int team = teams[index];
                int shield = team > 0 && teamCounts[team] > 1 ? team : -1;
                if (members[index].teamShield == shield)
                    continue;
                members[index].teamShield = shield;
                changed = true;
            }

            return changed;
        }

        private static MethodInfo Find(string name, params Type[] parameters) =>
            typeof(FRONT_Multiplayer).GetMethod(name, PrivateInstance, null, parameters, null);

        private static T Read<T>(FieldInfo field, object owner, string name) where T : class =>
            (T)(field ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, name)).GetValue(owner);

        private static void Invoke(MethodInfo method, object owner, string name, params object[] arguments) =>
            (method ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name)).Invoke(owner, arguments);
    }
}
