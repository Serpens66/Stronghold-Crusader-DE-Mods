using System;
using System.Runtime.CompilerServices;
using APIShared;

namespace BugfixesAndQoL
{
    internal static class AssassinGateRoutePolicy
    {
        internal static bool TryCapture(IEnemyGatePathPolicy policy, int player,
            out IEnemyGateRoutePolicySnapshot snapshot)
        {
            snapshot = null;
            if (policy == null || !policy.HasPublishedMask) return true;
            if (player < 1 || player > 8 || !(policy is IEnemyGateRoutePolicyProvider provider)) return false;
            return provider.TryCaptureRoutePolicy(player, out snapshot) &&
                snapshot != null && snapshot.PlayerId == player && snapshot.IsCurrent;
        }

        internal static bool Allows(IEnemyGateRoutePolicySnapshot snapshot, int tile, int direction) =>
            snapshot == null || snapshot.IsDirectionAllowed(tile, direction);

        internal static bool IsCurrent(IEnemyGateRoutePolicySnapshot snapshot) =>
            snapshot == null || snapshot.IsCurrent;

        internal static int IdentityHash(IEnemyGateRoutePolicySnapshot snapshot) =>
            snapshot == null ? 0 : RuntimeHelpers.GetHashCode(snapshot);

        internal static int ReadControlPlayer(byte low, byte high) => low | (high << 8);
    }
}
