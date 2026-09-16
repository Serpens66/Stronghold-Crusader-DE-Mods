using SHCDESE.API;
using SHCDESE.EventAPI.MapLoader;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if !SHARED_PRESET_TESTS
using Steamworks;
#endif

namespace Shared
{
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
        // Resolve the local player only from sources whose slot semantics are known.
        // The GameNetworkAPI local-player getter reads the same managed rosters and logs a
        // warning whenever they are still transitional, so it must not be used as an
        // additional fallback from persistent lobby observers.
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;

        internal static PlayerIdentityResolution ResolveLocalPlayerId(
            bool realMultiplayer,
            bool hasInGameHumanRoster,
            int nativePlayerId,
            int gameMemberPlayerId,
            int lobbyPlayerId)
        {
            bool nativeValid = IsValidPlayerId(nativePlayerId);
            bool gameMemberValid = IsValidPlayerId(gameMemberPlayerId);
            bool lobbyValid = IsValidPlayerId(lobbyPlayerId);

            if (realMultiplayer && hasInGameHumanRoster)
            {
                if (nativeValid && gameMemberValid && nativePlayerId != gameMemberPlayerId)
                {
                    return Failure(
                        $"Authoritative local player ID mismatch: native={nativePlayerId}, gameMember={gameMemberPlayerId}, " +
                        $"lobby={lobbyPlayerId}.");
                }

                int authoritative = nativeValid ? nativePlayerId : gameMemberPlayerId;
                if (!IsValidPlayerId(authoritative))
                {
                    return Failure(
                        $"No authoritative local player ID is available in the active multiplayer roster: " +
                        $"native={nativePlayerId}, gameMember={gameMemberPlayerId}, lobby={lobbyPlayerId}.");
                }
                if (lobbyValid && lobbyPlayerId != authoritative)
                {
                    return Failure(
                        $"Final lobby mapping disagrees with the authoritative local player ID: " +
                        $"authoritative={authoritative}, lobby={lobbyPlayerId}.");
                }

                return Success(authoritative, string.Empty);
            }

            if (realMultiplayer)
            {
                if (lobbyValid)
                    return Success(lobbyPlayerId, string.Empty);
                return Failure($"No local multiplayer player ID is available yet: lobby={lobbyPlayerId}.");
            }

            if (nativeValid)
                return Success(nativePlayerId, string.Empty);
            if (gameMemberValid)
                return Success(gameMemberPlayerId, string.Empty);
            if (lobbyValid)
                return Success(lobbyPlayerId, string.Empty);
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

        internal static PlayerIdentityResolution ResolveAuthenticatedPerPlayerTarget(
            ulong senderSteamId,
            int payloadPlayerId,
            IReadOnlyDictionary<int, ulong> playersById)
        {
            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
                senderSteamId,
                playersById);
            if (!resolution.IsResolved || resolution.PlayerId == payloadPlayerId)
                return resolution;
            return Success(
                resolution.PlayerId,
                $"The per-player payload claimed slot {payloadPlayerId}, but authenticated " +
                $"Steam identity {senderSteamId} belongs to final slot {resolution.PlayerId}.");
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

            return ResolveLocalPlayerId(
                realMultiplayer,
                preferInGameRoster && humanGameMembers.Length > 0,
                nativePlayerId,
                gameMemberPlayerId,
                lobbyPlayerId);
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

        internal static string CaptureProvisionalPlayerIdDiagnostic(
            ulong steamId,
            int finalPlayerId,
            bool inGame)
        {
            if (steamId == 0 || !IsValidPlayerId(finalPlayerId))
                return string.Empty;

            try
            {
                int provisionalPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
                    new CSteamID(steamId));
                if (!IsValidPlayerId(provisionalPlayerId) ||
                    provisionalPlayerId == finalPlayerId)
                {
                    return string.Empty;
                }

                return inGame
                    ? $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
                      $"{steamId}: networkLobby={provisionalPlayerId}, final={finalPlayerId}."
                    : $"Script Extender lobby-order player ID differs from Vanilla's final lobby mapping " +
                      $"for Steam identity {steamId}: networkLobby={provisionalPlayerId}, " +
                      $"finalLobby={finalPlayerId}.";
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            out Dictionary<int, ulong> playersById,
            out string error) =>
            TryCaptureHumanRoster(
                preferInGameRoster,
                requireAuthoritativeLobbyRoster: false,
                out playersById,
                out error,
                out _);

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            out Dictionary<int, ulong> playersById,
            out string error,
            out string diagnostic) =>
            TryCaptureHumanRoster(
                preferInGameRoster,
                requireAuthoritativeLobbyRoster: false,
                out playersById,
                out error,
                out diagnostic);

        internal static bool TryCaptureHumanRoster(
            bool preferInGameRoster,
            bool requireAuthoritativeLobbyRoster,
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
                if (requireAuthoritativeLobbyRoster && !IsValidPlayerId(vanillaPlayerId))
                {
                    error =
                        $"Vanilla has not assigned a final player slot to lobby member {steamId} yet.";
                    return false;
                }
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

}
