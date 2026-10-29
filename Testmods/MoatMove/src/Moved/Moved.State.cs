extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.State.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal static readonly int[] EndpointNeighbourX = { -1, 1, 0, 0, -1, 1, -1, 1 };
        internal static readonly int[] EndpointNeighbourY = { 0, 0, -1, 1, -1, -1, 1, 1 };
        internal static readonly byte[] EndpointSourceEdgeMasks =
            { 0x04, 0x40, 0x10, 0x01, 0x08, 0x20, 0x02, 0x80 };
        internal const int MapCellCount = MapWidth * MapWidth;
        internal const int RouteCellMask = (1 << RouteStateShift) - 1;
        internal const int GroundRouteState = 0;
        internal const int FriendlyMoatRouteState = 1;
        internal const int EnemyMoatRouteState = 2;
        internal const int PathManagerRouteVariantOffset = 0x80;
        [ThreadStatic] internal static int requiredPublicationTimingDepth;

        internal int[] visitedWithoutMoat;
        internal int[] visitedWithMoat;
        internal int[] visitedWithEnemyMoat;
        internal int[] distanceWithoutMoat;
        internal int[] distanceWithMoat;
        internal int[] distanceWithEnemyMoat;
        internal int[] queue;
        internal int[] observedRouteRegions;
        internal int[] reachedGroundRegions;
        internal int[] reachedFriendlyMoatRegions;
        internal int[] reachedEnemyMoatRegions;
        internal int gridGeneration;
        internal bool cacheIncludesEnemyRoutes;
        internal int cachedReachabilityExpandedNodes;
        internal bool targetedRouteProbeBusy;

        // Scratch data only: command snapshots and native rollback buffers retain their ownership.
        internal readonly Stack<BuildingFallbackWorkBuffers> buildingFallbackWorkBuffers =
            new Stack<BuildingFallbackWorkBuffers>();
        internal readonly Stack<MoatCandidateField> buildingCandidateFields = new Stack<MoatCandidateField>();

        internal int reachabilityQueueHead, reachabilityQueueTail;
        internal object reachabilityOwner;
        internal int reachabilityTick;
        internal const int RouteStateShift = 20;
#if SERP_DEVDIAG

        // Log dedupe owned here: BugfixesAndQoL declares these only in its own DEVDIAG build,
        // while MoatMove compiles against its normal assembly. Signatures carry the map epoch.
        internal readonly Dictionary<int, string> lastUnscopedAttackModes = new Dictionary<int, string>();
        internal readonly Dictionary<int, string> lastAttackCommandCandidates = new Dictionary<int, string>();
        internal readonly HashSet<string> loggedDiggerDecisions = new HashSet<string>(StringComparer.Ordinal);
        internal int loggedDiggerDecisionsMapEpoch = int.MinValue;
#endif
    }
}
