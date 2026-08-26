using SHCDESE.API;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Linq;
#if !SHARED_PRESET_TESTS
using Steamworks;
#endif

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

    internal readonly struct PlayerIdentityResolution
    {
        internal PlayerIdentityResolution(int playerId, bool isResolved, string error, string diagnostic)
        {
            PlayerId = playerId;
            IsResolved = isResolved;
            Error = error ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal int PlayerId { get; }
        internal bool IsResolved { get; }
        internal string Error { get; }
        internal string Diagnostic { get; }
    }

    internal static class PlayerIdentityHelper
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;

        internal static PlayerIdentityResolution ResolveLocalPlayerId(
            bool realMultiplayer,
            bool hasInGameHumanRoster,
            int nativePlayerId,
            int gameMemberPlayerId,
            int lobbyPlayerId,
            int networkLobbyPlayerId)
        {
            bool nativeValid = IsValidPlayerId(nativePlayerId);
            bool gameMemberValid = IsValidPlayerId(gameMemberPlayerId);
            bool lobbyValid = IsValidPlayerId(lobbyPlayerId);
            bool networkValid = IsValidPlayerId(networkLobbyPlayerId);

            if (realMultiplayer && hasInGameHumanRoster)
            {
                if (nativeValid && gameMemberValid && nativePlayerId != gameMemberPlayerId)
                {
                    return Failure(
                        $"Authoritative local player ID mismatch: native={nativePlayerId}, gameMember={gameMemberPlayerId}, " +
                        $"lobby={lobbyPlayerId}, networkLobby={networkLobbyPlayerId}.");
                }

                int authoritative = nativeValid ? nativePlayerId : gameMemberPlayerId;
                if (!IsValidPlayerId(authoritative))
                {
                    return Failure(
                        $"No authoritative local player ID is available in the active multiplayer roster: " +
                        $"native={nativePlayerId}, gameMember={gameMemberPlayerId}, lobby={lobbyPlayerId}, " +
                        $"networkLobby={networkLobbyPlayerId}.");
                }
                if (lobbyValid && lobbyPlayerId != authoritative)
                {
                    return Failure(
                        $"Final lobby mapping disagrees with the authoritative local player ID: " +
                        $"authoritative={authoritative}, lobby={lobbyPlayerId}, networkLobby={networkLobbyPlayerId}.");
                }

                return Success(
                    authoritative,
                    networkValid && networkLobbyPlayerId != authoritative
                        ? $"Lobby-order player ID differs from the final in-game slot: " +
                          $"networkLobby={networkLobbyPlayerId}, final={authoritative}."
                        : string.Empty);
            }

            if (realMultiplayer)
            {
                if (lobbyValid)
                {
                    return Success(
                        lobbyPlayerId,
                        networkValid && networkLobbyPlayerId != lobbyPlayerId
                            ? $"Script Extender lobby-order player ID differs from Vanilla's final lobby mapping: " +
                              $"networkLobby={networkLobbyPlayerId}, finalLobby={lobbyPlayerId}."
                            : string.Empty);
                }
                if (networkValid)
                    return Success(networkLobbyPlayerId, "Only the provisional lobby-order player ID is available.");
                return Failure(
                    $"No local multiplayer player ID is available yet: lobby={lobbyPlayerId}, " +
                    $"networkLobby={networkLobbyPlayerId}.");
            }

            if (nativeValid)
                return Success(nativePlayerId, string.Empty);
            if (gameMemberValid)
                return Success(gameMemberPlayerId, string.Empty);
            if (lobbyValid)
                return Success(lobbyPlayerId, string.Empty);
            if (networkValid)
                return Success(networkLobbyPlayerId, string.Empty);
            return Failure("No valid local player ID is available.");
        }

        internal static PlayerIdentityResolution ResolvePlayerIdForSteamId(
            ulong steamId,
            IReadOnlyDictionary<int, ulong> playersById)
        {
            if (steamId == 0)
                return Failure("The requested Steam identity is invalid.");

            var normalized = new Dictionary<int, ulong>();
            foreach (KeyValuePair<int, ulong> player in
                playersById ?? new Dictionary<int, ulong>())
            {
                if (!TryAddPlayer(normalized, player.Key, player.Value, out string error))
                    return Failure(error);
            }

            int[] matches = normalized
                .Where(player => player.Value == steamId)
                .Select(player => player.Key)
                .ToArray();
            if (matches.Length != 1)
            {
                return Failure(
                    matches.Length == 0
                        ? $"Steam identity {steamId} is not part of the resolved human roster."
                        : $"Steam identity {steamId} is assigned to multiple player slots.");
            }
            return Success(matches[0], string.Empty);
        }

#if !SHARED_PRESET_TESTS
        internal static PlayerIdentityResolution CaptureLocalPlayerId(
            bool preferInGameRoster) =>
            CaptureLocalPlayerId(
                GameModeHelper.IsRealMultiplayer(),
                preferInGameRoster);

        internal static PlayerIdentityResolution CaptureLocalPlayerId(
            bool realMultiplayer,
            bool preferInGameRoster)
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            ulong localSteamId = 0;
            try
            {
                localSteamId = SteamUser.GetSteamID().m_SteamID;
            }
            catch
            {
                // Steam can be unavailable during early singleplayer initialization.
            }

            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
                .Where(member => member != null && !member.kicked && !member.skirmishAI)
                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
            int gameMemberPlayerId = humanGameMembers
                .Where(member => localSteamId != 0 && member.steamID == localSteamId)
                .Select(member => member.playerID)
                .FirstOrDefault();

            int nativePlayerId = 0;
            try
            {
                nativePlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            }
            catch
            {
                // Native player resources are not guaranteed to exist in the lobby.
            }

            int lobbyPlayerId = 0;
            try
            {
                if (localSteamId != 0 && platform?.activeLobby != null)
                    lobbyPlayerId = platform.activeLobby.getThisPlayerFromSteamID(localSteamId);
            }
            catch
            {
                // The final lobby mapping can still be under construction.
            }

            int networkLobbyPlayerId = 0;
            try
            {
                networkLobbyPlayerId = GameNetworkAPI.GetLocalPlayerId();
            }
            catch
            {
                // This source is only a final fallback and may be unavailable early.
            }

            return ResolveLocalPlayerId(
                realMultiplayer,
                preferInGameRoster && humanGameMembers.Length > 0,
                nativePlayerId,
                gameMemberPlayerId,
                lobbyPlayerId,
                networkLobbyPlayerId);
        }

        internal static PlayerIdentityResolution CapturePlayerIdForSteamId(
            ulong steamId,
            bool preferInGameRoster)
        {
            if (!TryCaptureHumanRoster(
                    preferInGameRoster,
                    out Dictionary<int, ulong> playersById,
                    out string error,
                    out string diagnostic))
                return Failure(error);

            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
                steamId,
                playersById);
            if (!resolution.IsResolved)
                return resolution;

            if (preferInGameRoster)
            {
                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
                int vanillaPlayerId = 0;
                int networkLobbyPlayerId = 0;
                try
                {
                    if (lobby != null)
                        vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
                }
                catch
                {
                    // The lobby can disappear while the in-game roster remains authoritative.
                }
                try
                {
                    networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
                        new CSteamID(steamId));
                }
                catch
                {
                    // Lobby order is diagnostic-only after the game roster exists.
                }

                if (IsValidPlayerId(vanillaPlayerId) &&
                    vanillaPlayerId != resolution.PlayerId)
                {
                    return Failure(
                        $"Final lobby mapping disagrees with the authoritative in-game player slot for " +
                        $"Steam identity {steamId}: gameMember={resolution.PlayerId}, lobby={vanillaPlayerId}, " +
                        $"networkLobby={networkLobbyPlayerId}.");
                }
                if (IsValidPlayerId(networkLobbyPlayerId) &&
                    networkLobbyPlayerId != resolution.PlayerId)
                {
                    diagnostic =
                        $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
                        $"{steamId}: networkLobby={networkLobbyPlayerId}, final={resolution.PlayerId}.";
                }
            }
            return Success(resolution.PlayerId, diagnostic);
        }

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            out Dictionary<int, ulong> playersById,
            out string error) =>
            TryCaptureHumanRoster(
                preferInGameRoster,
                out playersById,
                out error,
                out _);

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            out Dictionary<int, ulong> playersById,
            out string error,
            out string diagnostic)
        {
            playersById = new Dictionary<int, ulong>();
            diagnostic = string.Empty;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
                .Where(member => member != null && !member.kicked && !member.skirmishAI)
                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
            if (preferInGameRoster)
            {
                if (humanGameMembers.Length == 0)
                {
                    error = "The active in-game human roster is unavailable.";
                    return false;
                }
                foreach (Platform_Multiplayer.MPGameMember member in humanGameMembers)
                {
                    if (!TryAddPlayer(playersById, member.playerID, member.steamID, out error))
                        return false;
                }
                error = string.Empty;
                return true;
            }

            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (lobby?.members == null)
            {
                error = "The active human lobby roster is unavailable.";
                return false;
            }

            var diagnostics = new List<string>();
            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || member.dummyToBeKicked ||
                    (member.SkirmishMember && !member.SkirmishHumanMember))
                    continue;
                ulong steamId = member.id.m_SteamID;
                int vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
                int networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
                int playerId = IsValidPlayerId(vanillaPlayerId)
                    ? vanillaPlayerId
                    : networkLobbyPlayerId;
                if (IsValidPlayerId(vanillaPlayerId) &&
                    IsValidPlayerId(networkLobbyPlayerId) &&
                    vanillaPlayerId != networkLobbyPlayerId)
                {
                    diagnostics.Add(
                        $"steamId={steamId}: networkLobby={networkLobbyPlayerId}, finalLobby={vanillaPlayerId}");
                }
                else if (!IsValidPlayerId(vanillaPlayerId) &&
                         IsValidPlayerId(networkLobbyPlayerId))
                {
                    diagnostics.Add(
                        $"steamId={steamId}: only provisional networkLobby={networkLobbyPlayerId} is available");
                }
                if (!TryAddPlayer(playersById, playerId, steamId, out error))
                    return false;
            }

            if (playersById.Count == 0)
            {
                error = "The active lobby contains no resolved human players.";
                return false;
            }
            diagnostic = diagnostics.Count == 0
                ? string.Empty
                : "Lobby player-ID source differences: " + string.Join("; ", diagnostics) + ".";
            error = string.Empty;
            return true;
        }
#endif

        private static bool TryAddPlayer(
            IDictionary<int, ulong> playersById,
            int playerId,
            ulong steamId,
            out string error)
        {
            if (!IsValidPlayerId(playerId) || steamId == 0)
            {
                error = $"A human player has an invalid final identity: playerId={playerId}, steamId={steamId}.";
                return false;
            }
            if (playersById.TryGetValue(playerId, out ulong existingSteamId) && existingSteamId != steamId)
            {
                error = $"Final player slot {playerId} is assigned to multiple Steam identities.";
                return false;
            }
            if (playersById.Any(player => player.Key != playerId && player.Value == steamId))
            {
                error = $"Steam identity {steamId} is assigned to multiple final player slots.";
                return false;
            }
            playersById[playerId] = steamId;
            error = string.Empty;
            return true;
        }

        private static PlayerIdentityResolution Success(int playerId, string diagnostic) =>
            new PlayerIdentityResolution(playerId, true, string.Empty, diagnostic);

        private static PlayerIdentityResolution Failure(string error) =>
            new PlayerIdentityResolution(0, false, error, string.Empty);

        private static bool IsValidPlayerId(int playerId) =>
            playerId >= FirstPlayerId && playerId <= LastPlayerId;
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
