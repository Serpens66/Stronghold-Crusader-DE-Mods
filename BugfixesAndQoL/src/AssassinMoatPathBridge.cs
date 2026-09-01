using System;

namespace BugfixesAndQoL
{
    // Test-only integration seam. With no registered provider the Assassin pathfinder
    // has exactly the same behavior as before MoveMoatTest existed.
    internal static class AssassinMoatPathBridge
    {
        private static readonly object Sync = new object();
        private static string providerId;
        private static Func<int, int, bool> friendlyCompletedMoat;
        private static AssassinPathfindingRuntime runtime;

        internal static bool IsProviderActive => friendlyCompletedMoat != null;

        internal static void AttachRuntime(AssassinPathfindingRuntime value)
        {
            lock (Sync)
            {
                runtime = value;
            }
        }

        // Invoked reflectively by MoveMoatTest so neither assembly has a hard dependency
        // on the other. A second provider is rejected rather than silently replacing policy.
        internal static bool RegisterProvider(
            string requestedProviderId,
            Func<int, int, bool> classifier)
        {
            if (string.IsNullOrWhiteSpace(requestedProviderId) || classifier == null)
                return false;

            AssassinPathfindingRuntime attached;
            lock (Sync)
            {
                if (friendlyCompletedMoat != null &&
                    !string.Equals(providerId, requestedProviderId, StringComparison.Ordinal))
                {
                    return false;
                }

                providerId = requestedProviderId;
                friendlyCompletedMoat = classifier;
                attached = runtime;
            }

            attached?.OnMoatPathProviderChanged();
            return true;
        }

        internal static bool UnregisterProvider(string requestedProviderId)
        {
            AssassinPathfindingRuntime attached;
            lock (Sync)
            {
                if (friendlyCompletedMoat == null ||
                    !string.Equals(providerId, requestedProviderId, StringComparison.Ordinal))
                {
                    return false;
                }

                providerId = null;
                friendlyCompletedMoat = null;
                attached = runtime;
            }

            attached?.OnMoatPathProviderChanged();
            return true;
        }

        internal static bool IsFriendlyCompletedMoat(int playerId, int tileId)
        {
            Func<int, int, bool> classifier = friendlyCompletedMoat;
            return classifier != null && classifier(playerId, tileId);
        }

        // Bit 0: reachable, bit 1: used friendly moat, bit 2: used climb edge.
        internal static int ProbeRoute(
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY)
        {
            AssassinPathfindingRuntime attached = runtime;
            return attached == null || friendlyCompletedMoat == null
                ? 0
                : attached.ProbeMoatRoute(playerId, startX, startY, targetX, targetY);
        }
    }
}
