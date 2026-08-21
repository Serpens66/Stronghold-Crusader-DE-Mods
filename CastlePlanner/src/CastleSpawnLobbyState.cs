using System;
using System.Collections.Generic;
using System.Linq;

namespace CastlePlanner
{
    internal sealed class CastleSpawnLobbyState
    {
        private readonly Dictionary<int, ulong> playersById =
            new Dictionary<int, ulong>();
        private ulong lobbyId;
        private bool hasLobby;

        public CastleSpawnLobbyChange Observe(
            ulong? currentLobbyId,
            IReadOnlyDictionary<int, ulong> currentPlayers)
        {
            if (!currentLobbyId.HasValue)
            {
                bool changed = hasLobby || playersById.Count != 0;
                hasLobby = false;
                lobbyId = 0;
                playersById.Clear();
                return new CastleSpawnLobbyChange(
                    changed,
                    changed,
                    changed ? Enumerable.Range(1, 8).ToArray() : Array.Empty<int>());
            }

            bool sessionChanged = !hasLobby || lobbyId != currentLobbyId.Value;
            var normalized = new Dictionary<int, ulong>();
            foreach (KeyValuePair<int, ulong> player in
                currentPlayers ?? new Dictionary<int, ulong>())
            {
                if (player.Key > 0 && player.Key <= 8 && player.Value != 0)
                    normalized[player.Key] = player.Value;
            }

            bool membershipChanged = sessionChanged ||
                normalized.Count != playersById.Count ||
                normalized.Any(player =>
                    !playersById.TryGetValue(player.Key, out ulong previousSteamId) ||
                    previousSteamId != player.Value);
            if (!membershipChanged)
                return CastleSpawnLobbyChange.None;

            int[] slotsToClear = sessionChanged
                ? Enumerable.Range(1, 8).ToArray()
                : Enumerable.Range(1, 8)
                    .Where(playerId =>
                        playersById.TryGetValue(playerId, out ulong previousSteamId) &&
                        (!normalized.TryGetValue(playerId, out ulong currentSteamId) ||
                            currentSteamId != previousSteamId))
                    .ToArray();

            hasLobby = true;
            lobbyId = currentLobbyId.Value;
            playersById.Clear();
            foreach (KeyValuePair<int, ulong> player in normalized)
                playersById[player.Key] = player.Value;

            return new CastleSpawnLobbyChange(true, sessionChanged, slotsToClear);
        }
    }

    internal sealed class CastleSpawnLobbyChange
    {
        public static readonly CastleSpawnLobbyChange None =
            new CastleSpawnLobbyChange(false, false, Array.Empty<int>());

        public CastleSpawnLobbyChange(
            bool membershipChanged,
            bool sessionChanged,
            int[] slotsToClear)
        {
            MembershipChanged = membershipChanged;
            SessionChanged = sessionChanged;
            SlotsToClear = slotsToClear ?? Array.Empty<int>();
        }

        public bool MembershipChanged { get; }
        public bool SessionChanged { get; }
        public int[] SlotsToClear { get; }
    }
}
