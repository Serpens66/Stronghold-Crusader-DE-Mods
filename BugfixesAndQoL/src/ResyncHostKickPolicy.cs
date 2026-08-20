using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct ResyncHostKickCandidate
    {
        internal ResyncHostKickCandidate(
            int playerId,
            string playerName,
            DateTime lastHeartbeat,
            bool isSelf,
            bool isHuman,
            bool isKicked)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            LastHeartbeat = lastHeartbeat;
            IsSelf = isSelf;
            IsHuman = isHuman;
            IsKicked = isKicked;
        }

        internal int PlayerId { get; }
        internal string PlayerName { get; }
        internal DateTime LastHeartbeat { get; }
        internal bool IsSelf { get; }
        internal bool IsHuman { get; }
        internal bool IsKicked { get; }
    }

    internal static class ResyncHostKickPolicy
    {
        internal static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(5);

        internal static bool TrySelect(
            IEnumerable<ResyncHostKickCandidate> candidates,
            DateTime now,
            out ResyncHostKickCandidate selected)
        {
            selected = default(ResyncHostKickCandidate);
            bool found = false;
            DateTime cutoff = now - HeartbeatTimeout;

            if (candidates == null)
                return false;

            foreach (ResyncHostKickCandidate candidate in candidates)
            {
                if (candidate.PlayerId <= 0 ||
                    candidate.IsSelf ||
                    !candidate.IsHuman ||
                    candidate.IsKicked ||
                    candidate.LastHeartbeat == DateTime.MaxValue ||
                    candidate.LastHeartbeat >= cutoff)
                {
                    continue;
                }

                if (!found ||
                    candidate.LastHeartbeat < selected.LastHeartbeat ||
                    (candidate.LastHeartbeat == selected.LastHeartbeat && candidate.PlayerId < selected.PlayerId))
                {
                    selected = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
