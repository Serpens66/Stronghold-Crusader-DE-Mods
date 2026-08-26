using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MultiplayerLeaveFix
{
    internal enum LeaveMessageDisposition
    {
        NotLimited,
        AllowFirst,
        SuppressDuplicate
    }

    internal sealed class MultiplayerLeaveMessagePolicy
    {
        private static readonly TimeSpan LeaveAssociationLifetime = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan DuplicateBurstLifetime = TimeSpan.FromSeconds(5);

        private readonly Func<long> getTimestamp;
        private readonly Func<int, bool> isValidPlayerId;
        private readonly long timestampFrequency;
        private readonly Dictionary<int, LeftPlayerInfo> leavesByPlayerId = new Dictionary<int, LeftPlayerInfo>();
        private readonly Dictionary<string, LeftPlayerInfo> leavesByPlayerName = new Dictionary<string, LeftPlayerInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> seenMessageExpirations = new Dictionary<string, long>(StringComparer.Ordinal);
        private long nextSequence;

        public MultiplayerLeaveMessagePolicy()
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency, IsGamePlayerIdValid)
        {
        }

        internal MultiplayerLeaveMessagePolicy(
            Func<long> getTimestamp,
            long timestampFrequency,
            Func<int, bool> isValidPlayerId)
        {
            this.getTimestamp = getTimestamp ?? throw new ArgumentNullException(nameof(getTimestamp));
            this.isValidPlayerId = isValidPlayerId ?? throw new ArgumentNullException(nameof(isValidPlayerId));
            if (timestampFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

            this.timestampFrequency = timestampFrequency;
        }

        public void Clear()
        {
            leavesByPlayerId.Clear();
            leavesByPlayerName.Clear();
            seenMessageExpirations.Clear();
        }

        public bool RecordProcessedLeave(int playerId, string playerName, ulong steamId)
        {
            long now = getTimestamp();
            PruneExpired(now);

            int validPlayerId = isValidPlayerId(playerId) ? playerId : 0;
            string normalizedName = Normalize(playerName);
            if (validPlayerId == 0 && string.IsNullOrEmpty(normalizedName))
                return false;

            RemoveByPlayerId(validPlayerId);
            RemoveByPlayerName(normalizedName);

            LeftPlayerInfo info = new LeftPlayerInfo
            {
                Sequence = ++nextSequence,
                PlayerId = validPlayerId,
                PlayerName = normalizedName,
                SteamId = steamId,
                ExpiresAt = AddDuration(now, LeaveAssociationLifetime)
            };

            AddIndexes(info);

            // Vanilla emits the removal message synchronously while processing packet 8.
            // Mark that exact prefix as seen only after the successful trampoline returns.
            seenMessageExpirations[BuildMessageKey(info, "Removing Player :")] =
                AddDuration(now, DuplicateBurstLifetime);
            return true;
        }

        public void DiscardForActiveMember(int playerId, string playerName, ulong steamId)
        {
            long now = getTimestamp();
            PruneExpired(now);

            if (isValidPlayerId(playerId))
                RemoveByPlayerId(playerId);

            string normalizedName = Normalize(playerName);
            if (!string.IsNullOrEmpty(normalizedName))
                RemoveByPlayerName(normalizedName);
        }

        public LeaveMessageDisposition Classify(int fromPlayerId, string playerName, string prefix)
        {
            long now = getTimestamp();
            PruneExpired(now);

            LeftPlayerInfo info = null;
            if (isValidPlayerId(fromPlayerId))
                leavesByPlayerId.TryGetValue(fromPlayerId, out info);

            string normalizedName = Normalize(playerName);
            if (info == null && !string.IsNullOrEmpty(normalizedName))
                leavesByPlayerName.TryGetValue(normalizedName, out info);

            if (info == null)
                return LeaveMessageDisposition.NotLimited;

            string key = BuildMessageKey(info, prefix);
            if (seenMessageExpirations.TryGetValue(key, out long expiresAt) && now <= expiresAt)
                return LeaveMessageDisposition.SuppressDuplicate;

            seenMessageExpirations[key] = AddDuration(now, DuplicateBurstLifetime);
            return LeaveMessageDisposition.AllowFirst;
        }

        private void PruneExpired(long now)
        {
            var expiredLeaves = new List<LeftPlayerInfo>();
            foreach (LeftPlayerInfo info in leavesByPlayerId.Values)
            {
                if (now > info.ExpiresAt)
                    expiredLeaves.Add(info);
            }

            foreach (LeftPlayerInfo info in leavesByPlayerName.Values)
            {
                if (now > info.ExpiresAt && !expiredLeaves.Contains(info))
                    expiredLeaves.Add(info);
            }

            for (int i = 0; i < expiredLeaves.Count; i++)
                RemoveIndexes(expiredLeaves[i]);

            var expiredKeys = new List<string>();
            foreach (KeyValuePair<string, long> pair in seenMessageExpirations)
            {
                if (now > pair.Value)
                    expiredKeys.Add(pair.Key);
            }

            for (int i = 0; i < expiredKeys.Count; i++)
                seenMessageExpirations.Remove(expiredKeys[i]);
        }

        private void AddIndexes(LeftPlayerInfo info)
        {
            if (info.PlayerId != 0)
                leavesByPlayerId[info.PlayerId] = info;

            if (!string.IsNullOrEmpty(info.PlayerName))
                leavesByPlayerName[info.PlayerName] = info;
        }

        private void RemoveByPlayerId(int playerId)
        {
            if (playerId != 0 && leavesByPlayerId.TryGetValue(playerId, out LeftPlayerInfo info))
                RemoveIndexes(info);
        }

        private void RemoveByPlayerName(string playerName)
        {
            if (!string.IsNullOrEmpty(playerName) && leavesByPlayerName.TryGetValue(playerName, out LeftPlayerInfo info))
                RemoveIndexes(info);
        }

        private void RemoveIndexes(LeftPlayerInfo info)
        {
            if (info.PlayerId != 0 && leavesByPlayerId.TryGetValue(info.PlayerId, out LeftPlayerInfo byId) && ReferenceEquals(byId, info))
                leavesByPlayerId.Remove(info.PlayerId);

            if (!string.IsNullOrEmpty(info.PlayerName) && leavesByPlayerName.TryGetValue(info.PlayerName, out LeftPlayerInfo byName) && ReferenceEquals(byName, info))
                leavesByPlayerName.Remove(info.PlayerName);
        }

        private long AddDuration(long timestamp, TimeSpan duration)
        {
            double delta = duration.TotalSeconds * timestampFrequency;
            if (delta >= long.MaxValue - timestamp)
                return long.MaxValue;

            return timestamp + (long)delta;
        }

        private static bool IsGamePlayerIdValid(int playerId)
        {
            return playerId > 0 && playerId <= GamePlayerManagerAPI.MAX_PLAYERS;
        }

        private static string BuildMessageKey(LeftPlayerInfo info, string prefix)
        {
            return info.Sequence + ":" + info.SteamId + ":" + Normalize(prefix);
        }

        internal static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class LeftPlayerInfo
        {
            public long Sequence;
            public int PlayerId;
            public string PlayerName;
            public ulong SteamId;
            public long ExpiresAt;
        }
    }
}
