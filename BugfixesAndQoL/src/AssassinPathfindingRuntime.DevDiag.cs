using System;
using APIShared;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class AssassinPathfindingRuntime
    {
        // DEVDIAG: every native bucket search is repeated by the managed search (native binding off). Route,
        // cost, expansions and edge counts must be equal (bfqol.assassin.native.match / .mismatch, first
        // mismatch logged); the native result stays the published one.
        private bool nativeCompareRunning, nativeMismatchLogged;
        partial void DiagCompareNativeRoute(int startX, int startY, int targetX, int targetY, int maximumNodes, int speedDelay,
            bool allowClimbing, bool allowReserved, IEnemyGateRoutePolicySnapshot gatePolicy, bool found, RouteSearchSummary summary)
        {
            if (nativeCompareRunning) return;
            int length = found ? summary.RouteLength : 0;
            int[] nativeRoute = new int[length], nativeCosts = new int[length];
            for (int i = 0; i < length; i++) { nativeRoute[i] = route[i]; nativeCosts[i] = costs[route[i]]; }
            bool bound = nativeSearchBound, managedFound;
            RouteSearchSummary managed;
            nativeCompareRunning = true; nativeSearchBound = false;
            try
            {
                managedFound = TryBuildWeightedRoute(startX, startY, targetX, targetY, maximumNodes, speedDelay, allowClimbing,
                    allowReserved, null, gatePolicy, out managed);
            }
            finally { nativeSearchBound = bound; nativeCompareRunning = false; }
            bool same = managedFound == found;
            if (same && found)
            {
                same = managed.RouteLength == summary.RouteLength && managed.TotalCost == summary.TotalCost &&
                    managed.ExpandedNodes == summary.ExpandedNodes && managed.GroundEdges == summary.GroundEdges && managed.ClimbEdges == summary.ClimbEdges;
                for (int i = 0; same && i < length; i++) same = route[i] == nativeRoute[i] && costs[route[i]] == nativeCosts[i];
            }
            Shared.DevDiag.Count(same ? "bfqol.assassin.native.match" : "bfqol.assassin.native.mismatch");
            if (!same && !nativeMismatchLogged)
            {
                nativeMismatchLogged = true;
                LogWarning($"native Assassin search mismatch: start={startX},{startY} target={targetX},{targetY} nodes={maximumNodes} " +
                    $"speed={speedDelay} climbing={allowClimbing} reserved={allowReserved} policy={gatePolicy != null} " +
                    $"native={found}/{summary.RouteLength}/{summary.TotalCost}/{summary.ExpandedNodes} managed={managedFound}/{managed.RouteLength}/{managed.TotalCost}/{managed.ExpandedNodes}.");
            }
            ResetTouchedNodes();
            for (int i = 0; i < length; i++) { route[i] = nativeRoute[i]; touched[touchedCount++] = nativeRoute[i]; costs[nativeRoute[i]] = nativeCosts[i]; }
        }
    }
}
