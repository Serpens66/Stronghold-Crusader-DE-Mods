// Feature: Identify the sole local survivor of an abruptly disconnected host.
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct AbruptHostMigrationCandidate
    {
        internal AbruptHostMigrationCandidate(
            int playerId,
            bool isSelf,
            bool isHost,
            bool isHuman,
            bool kicked,
            bool pendingKick)
        {
            PlayerId = playerId;
            IsSelf = isSelf;
            IsHost = isHost;
            IsHuman = isHuman;
            Kicked = kicked;
            PendingKick = pendingKick;
        }

        internal int PlayerId { get; }
        internal bool IsSelf { get; }
        internal bool IsHost { get; }
        internal bool IsHuman { get; }
        internal bool Kicked { get; }
        internal bool PendingKick { get; }
    }

    internal static class AbruptHostMigrationPolicy
    {
        internal static bool TrySelectLocalSuccessor(
            bool featureEnabled,
            int departingPlayerId,
            bool departingIsSelf,
            bool departingIsHost,
            bool departingIsHuman,
            bool departingKicked,
            bool departingPendingKick,
            IEnumerable<AbruptHostMigrationCandidate> candidates,
            out int successorPlayerId)
        {
            successorPlayerId = -1;
            if (!featureEnabled ||
                departingPlayerId <= 0 ||
                departingIsSelf ||
                !departingIsHost ||
                !departingIsHuman ||
                departingKicked ||
                departingPendingKick ||
                candidates == null)
            {
                return false;
            }

            AbruptHostMigrationCandidate successor = default;
            int eligibleCount = 0;
            foreach (AbruptHostMigrationCandidate candidate in candidates)
            {
                if (candidate.PlayerId <= 0 ||
                    candidate.PlayerId == departingPlayerId ||
                    !candidate.IsHuman ||
                    candidate.Kicked ||
                    candidate.PendingKick)
                {
                    continue;
                }

                eligibleCount++;
                successor = candidate;
                if (eligibleCount > 1)
                    return false;
            }

            if (eligibleCount != 1 || !successor.IsSelf || successor.IsHost)
                return false;

            successorPlayerId = successor.PlayerId;
            return true;
        }
    }
}
