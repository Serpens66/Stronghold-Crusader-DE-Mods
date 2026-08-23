using SHCDESE.API;
using CrusaderDE;

namespace Shared
{
    internal static class GameModeHelper
    {
        public static GameModeSnapshot Capture(bool multiplayerSave = false)
        {
            Director director = Director.instance;
            GameData gameData = GameData.Instance;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;

            int lobbyMembers = lobby?.members?.Count ?? -1;
            int realLobbyMembers = 0;
            int skirmishLobbyMembers = 0;
            if (lobby?.members != null)
            {
                foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                {
                    if (member == null)
                        continue;
                    if (member.SkirmishMember)
                        skirmishLobbyMembers++;
                    else
                        realLobbyMembers++;
                }
            }

            int gameMembers = platform?.gameMembers?.Count ?? -1;
            int realNetworkGameMembers = 0;
            if (platform?.gameMembers != null)
            {
                foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                {
                    if (member != null && !member.skirmishAI && member.steamID > 1000)
                        realNetworkGameMembers++;
                }
            }

            bool directorMultiplayer = director != null && director.MultiplayerGame;
            bool platformMultiplayer = GameNetworkAPI.IsMultiplayerGame();
            bool realMultiplayer =
                multiplayerSave ||
                directorMultiplayer ||
                platformMultiplayer ||
                realLobbyMembers > 0 ||
                realNetworkGameMembers > 0;

            int gameType = gameData != null ? gameData.game_type : -1;
            int skirmishGameType = gameData != null ? gameData.SkirmishGameType : -1;
            bool mapEditor = IsMapEditor();
            // game_type 3 is Vanilla's skirmish family. Immediately after leaving a
            // real multiplayer game, a new local skirmish can reach OnStartMap before
            // Vanilla changes SkirmishGameType from -1. Its all-local skirmish lobby
            // is the stable transition signal; no map or mission allow-list is needed.
            bool localSkirmishTransition =
                lobbyMembers > 0 &&
                skirmishLobbyMembers == lobbyMembers &&
                realLobbyMembers == 0 &&
                realNetworkGameMembers == 0;
            bool singleplayerSkirmishMode =
                !realMultiplayer &&
                !mapEditor &&
                gameType == 3 &&
                (skirmishGameType >= 0 || localSkirmishTransition);

            return new GameModeSnapshot(
                realMultiplayer,
                singleplayerSkirmishMode,
                singleplayerSkirmishMode && skirmishGameType == 0,
                singleplayerSkirmishMode && (skirmishGameType == 1 || skirmishGameType == 2),
                mapEditor,
                multiplayerSave,
                director != null,
                directorMultiplayer,
                director != null && director.SkirmishModeGame,
                GameNetworkAPI.IsNetworkedEnvironment(),
                platformMultiplayer,
                lobbyMembers,
                realLobbyMembers,
                skirmishLobbyMembers,
                gameMembers,
                realNetworkGameMembers,
                gameType,
                skirmishGameType,
                gameData != null ? gameData.coopTrailID : -1);
        }

        public static bool IsRealMultiplayer(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsRealMultiplayer;

        // Subtype 0 is a normal skirmish. Subtypes 1 and 2 are Vanilla and custom Trails.
        public static bool IsSingleplayerSkirmish(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerSkirmish;

        public static bool IsSingleplayerTrail(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerTrail;

        // This broader check also covers future/utility subtypes initialized by Vanilla as skirmish mode.
        public static bool IsSingleplayerSkirmishMode(bool multiplayerSave = false) =>
            Capture(multiplayerSave).IsSingleplayerSkirmishMode;

        public static bool IsMapEditor()
        {
            try
            {
                if (GamePlayerManagerAPI.Instance?.IsInMapEditor() == true)
                    return true;
            }
            catch
            {
                // The Script Extender singleton can be unavailable during early plugin startup.
            }

            // MainViewModel.Instance constructs the ViewModel. Reading it before the
            // game's own loaded marker is set can therefore fail inside Vanilla code.
            if (!MainViewModel.viewModelLoaded)
                return false;

            try
            {
                return MainViewModel.Instance?.IsMapEditorMode ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    internal readonly struct GameModeSnapshot
    {
        public GameModeSnapshot(
            bool isRealMultiplayer,
            bool isSingleplayerSkirmishMode,
            bool isSingleplayerSkirmish,
            bool isSingleplayerTrail,
            bool isMapEditor,
            bool multiplayerSave,
            bool directorAvailable,
            bool directorMultiplayer,
            bool directorSkirmish,
            bool lowLevelNetworked,
            bool platformMultiplayer,
            int lobbyMembers,
            int realLobbyMembers,
            int skirmishLobbyMembers,
            int gameMembers,
            int realNetworkGameMembers,
            int gameType,
            int skirmishGameType,
            int coopTrailId)
        {
            IsRealMultiplayer = isRealMultiplayer;
            IsSingleplayerSkirmishMode = isSingleplayerSkirmishMode;
            IsSingleplayerSkirmish = isSingleplayerSkirmish;
            IsSingleplayerTrail = isSingleplayerTrail;
            IsMapEditor = isMapEditor;
            MultiplayerSave = multiplayerSave;
            DirectorAvailable = directorAvailable;
            DirectorMultiplayer = directorMultiplayer;
            DirectorSkirmish = directorSkirmish;
            LowLevelNetworked = lowLevelNetworked;
            PlatformMultiplayer = platformMultiplayer;
            LobbyMembers = lobbyMembers;
            RealLobbyMembers = realLobbyMembers;
            SkirmishLobbyMembers = skirmishLobbyMembers;
            GameMembers = gameMembers;
            RealNetworkGameMembers = realNetworkGameMembers;
            GameType = gameType;
            SkirmishGameType = skirmishGameType;
            CoopTrailId = coopTrailId;
        }

        public bool IsRealMultiplayer { get; }
        public bool IsSingleplayerSkirmishMode { get; }
        public bool IsSingleplayerSkirmish { get; }
        public bool IsSingleplayerTrail { get; }
        public bool IsMapEditor { get; }
        public bool MultiplayerSave { get; }
        public bool DirectorAvailable { get; }
        public bool DirectorMultiplayer { get; }
        public bool DirectorSkirmish { get; }
        public bool LowLevelNetworked { get; }
        public bool PlatformMultiplayer { get; }
        public int LobbyMembers { get; }
        public int RealLobbyMembers { get; }
        public int SkirmishLobbyMembers { get; }
        public int GameMembers { get; }
        public int RealNetworkGameMembers { get; }
        public int GameType { get; }
        public int SkirmishGameType { get; }
        public int CoopTrailId { get; }

        public string ToDiagnosticString()
        {
            return
                $"realMultiplayer={IsRealMultiplayer}, singleplayerSkirmishMode={IsSingleplayerSkirmishMode}, " +
                $"singleplayerSkirmish={IsSingleplayerSkirmish}, singleplayerTrail={IsSingleplayerTrail}, " +
                $"mapEditor={IsMapEditor}, multiplayerSave={MultiplayerSave}, directorAvailable={DirectorAvailable}, " +
                $"directorMultiplayer={DirectorMultiplayer}, directorSkirmish={DirectorSkirmish}, " +
                $"lowLevelNetworked={LowLevelNetworked}, platformMultiplayer={PlatformMultiplayer}, " +
                $"lobbyMembers={LobbyMembers}, realLobbyMembers={RealLobbyMembers}, " +
                $"skirmishLobbyMembers={SkirmishLobbyMembers}, " +
                $"gameMembers={GameMembers}, realNetworkGameMembers={RealNetworkGameMembers}, " +
                $"gameType={GameType}, skirmishGameType={SkirmishGameType}, coopTrailId={CoopTrailId}";
        }
    }
}
