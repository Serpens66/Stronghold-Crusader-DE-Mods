using System;
using System.Globalization;
using System.Reflection;
using CrusaderDE;

namespace ExtendedData
{
    internal sealed class CoopClientLobbyPresentation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo ImportSettings = typeof(FRONT_Multiplayer).GetMethod(
            "ImportSettings", PrivateInstance, null, new[] { typeof(string), typeof(bool) }, null)
            ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, "ImportSettings");
        private static readonly FieldInfo SetupData = typeof(FRONT_Multiplayer).GetField("MPsetupData", PrivateInstance)
            ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPsetupData");
        private static readonly FieldInfo SelectedHeader = typeof(FRONT_Multiplayer).GetField("selectedMPHeader", PrivateInstance)
            ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "selectedMPHeader");
        private static readonly MethodInfo UpdateRadar = typeof(FRONT_Multiplayer).GetMethod(
            "UpdateRadarShieldPositions", PrivateInstance, null, Type.EmptyTypes, null)
            ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, "UpdateRadarShieldPositions");

        private readonly Platform_Multiplayer.MPLobby room;
        private readonly FileHeader missionMap;
        private readonly string missionText;
        private string displayedSettings;

        internal CoopClientLobbyPresentation(Platform_Multiplayer.MPLobby room, FileHeader missionMap, string missionText)
        {
            this.room = room;
            this.missionMap = missionMap;
            this.missionText = missionText;
        }

        internal bool IsCurrent(FRONT_Multiplayer lobby, MainViewModel view, FileHeader selectedMissionMap) =>
            ReferenceEquals(lobby.currentLobby, room) && ReferenceEquals(missionMap, selectedMissionMap) && !room.isHost &&
            view?.Show_MultiplayerSetup == true && view.Show_MPGameCreation;

        internal void Refresh(FRONT_Multiplayer lobby, MainViewModel view)
        {
            if (Shared.CoopTrailSetupPresentation.UpdateClientTeamShields(lobby))
                UpdateRadar.Invoke(lobby, null);

            if (!view.Show_MPSettings)
                displayedSettings = null;
            else if (!string.IsNullOrEmpty(room.settings) &&
                !string.Equals(displayedSettings, room.settings, StringComparison.Ordinal) &&
                SetupData.GetValue(lobby) is EngineInterface.MultiplayerSetupData setup)
            {
                // RefreshLobbyList preserves the client's three Coop building restrictions.
                // Import the resulting setup, not the unfiltered published settings string.
                ImportSettings.Invoke(lobby, new object[] { setup.ToString(), false });
                displayedSettings = room.settings;
            }

            if (!string.Equals(view.StandaloneMissionText, missionText, StringComparison.Ordinal) &&
                !view.Show_MPRetrieveMapPanel &&
                string.Equals(room.mapFileName, missionMap.fileName, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(room.crc, NumberStyles.Integer, CultureInfo.InvariantCulture, out int crc) &&
                crc == unchecked((int)missionMap.crc) &&
                SelectedHeader.GetValue(lobby) is FileHeader header && header.crc == missionMap.crc &&
                string.Equals(header.fileName, missionMap.fileName, StringComparison.OrdinalIgnoreCase))
                view.StandaloneMissionText = missionText;
        }
    }
}
