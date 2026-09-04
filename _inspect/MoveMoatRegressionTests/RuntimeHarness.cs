using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MoveMoatTest
{
    internal enum AliveState { IsAlive }
    internal enum TribeAICommand { Move }
    internal enum AttackPipelineStage { Mode, Builder }
    internal unsafe struct GameUnit
    {
        public int r_ControllableForPlayerId, r_CurrentTilePositionX, r_CurrentTilePositionY;
        public int r_NextTilePositionX2, r_NextTilePositionY2, r_PathPlanStateBitFlags, r_MovingRelevant;
        public int r_AI_LastIssuedTribeCommand;
        public AliveState r_AliveState;
        public bool Digger;
    }
    internal unsafe class GameUnitManagerAPI
    {
        public static GameUnitManagerAPI Instance = new GameUnitManagerAPI();
        public GameUnit* Units;
        public bool TryGetUnitById(int id, out GameUnit* unit)
        { unit = id > 0 && id < 32 ? Units + id : null; return unit != null; }
    }
    internal class GamePlayerManagerAPI
    {
        public static GamePlayerManagerAPI Instance = new GamePlayerManagerAPI();
        public bool IsPlayerIdValid(int id) => id > 0 && id <= 8;
    }
    internal unsafe class GameTileManagerAPI
    {
        public static GameTileManagerAPI Instance = new GameTileManagerAPI();
        public int* Rows;
        public int GetTileId(int x, int y) => x >= 0 && x < 800 && y >= 0 && y < 800 ? Rows[y * 3] + x : -1;
        public IntPtr GetTileManager() => (IntPtr)1;
    }
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private static int tick = 10;
        private static int CaptureCurrentGameTick() => tick;
        private bool disposed, targetedRouteProbeBusy, weightedShadowBusy;
        private int mapEpoch = 1;
        private PlanScope activePlan, pendingPlan;
        private MoveCommandScope activeMoveCommand;
        private object activeAttackCommand;
        private MoatWorkSelectionScope activeMoatWorkSelection;
        private IntPtr nativePathManager;
        private byte* nativeUnitManager;
        private byte* nativeHeightLayer, movementTargetAvailability;
        private int* moatPathMode;
        private uint* tileFlags;
        private short* pathRegionGrid;
        private WeightedMoatRoutePlanner weightedMoatRoutePlanner;
        private int[] visitedWithoutMoat, visitedWithMoat, visitedWithEnemyMoat;
        private int[] distanceWithoutMoat, distanceWithMoat, distanceWithEnemyMoat, queue;
        private int[] observedRouteRegions, reachedGroundRegions, reachedFriendlyMoatRegions, reachedEnemyMoatRegions;
        private int gridGeneration, cacheMapEpoch = -1, cachePlayerId, cacheStartX, cacheStartY;
        private bool cacheIncludesEnemyRoutes;
        private int cachedReachabilityExpandedNodes, cachedTraversedRegionCount, cachedReachabilityMapHits;
        private RouteProbeSummary cachedRouteSummary;
        private Performance activeBuildingConsumerPerformance, activeBuildingApproachPerformance;
        private class Performance { public int ReachabilityCacheHits, ReachabilityMapsBuilt; }
        private class MoveCommandScope
        {
            public int TargetX, TargetY, ModeCalls, TargetedRouteCacheHits, TargetedRouteSearches, TargetedRouteExpandedNodes;
            public int TargetedRouteSearchPasses, BuilderCalls, FloodFillBypasses, FallbackBuilderCalls, FallbackRollbacks;
            public bool BuilderReached;
            public double TargetedRouteSearchMilliseconds, TargetedRouteMaximumSearchMilliseconds;
            public Dictionary<string, TargetedRouteDecision> TargetedRouteDecisions = new Dictionary<string, TargetedRouteDecision>();
        }
        private Func<IntPtr, int, int> originalUnitStandingOnCompletedMoat = (p, id) => 0;
        private Func<IntPtr, int, int, int> originalPathBuilder;
        private bool throwAudit;
        private bool ShouldLogUnitPipeline => false;
        private static void RecordVanillaBuilderResult(MoveCommandScope c, int r) { }
        private static void RecordBuilderResult(MoveCommandScope c, int r) { }
        private static void MarkCommandMoatRelevant(MoveCommandScope c, RouteProbeSummary s) { }
        private static void RecordFallbackContractRejection(PlanScope p) { }
        private static void TryLogDiagnosticFailure(string s, Exception e) { }
        private static void LogBuilderDecision(string s) { }
        private static void StartOrRefreshMoatMoveTracker(PlanScope p, RouteProbeSummary s, int r) { }
        private bool TryAuditFallbackPath(IntPtr m, byte* p, int r, PlanScope plan, GameUnit* u, out string audit)
        { audit = "fixture-reject"; if (throwAudit) throw new Exception("injected audit failure"); return false; }
        private bool TryReplaceUnsafeFallbackPath(IntPtr m, byte* p, byte[] b, int l, PlanScope plan,
            GameUnit* u, out int r, out string details)
        { r = 0; details = "fixture-no-replacement"; return false; }
        private bool occupied;
        private bool IsOccupiedByOtherLivingUnit(int tile, int id) => occupied;
        private static bool HasDownstreamMovementBlockingFlags(uint flags) => (flags & 0x10000130) != 0;
        private bool TryReadMoatRecordTile(IntPtr m, int moat, out int tile, out int x, out int y)
        { tile = 2017; x = 17; y = 11; return moat == 1; }
        private static bool CanDigMoat(GameUnit* u) => u->Digger;
        private static bool IsValidTileId(int tile) => tile >= 0 && tile < NativeTileCount;
        private bool IsCompletedMoatTile(int tile) => (tileFlags[tile] & CompletedMoatTileFlag) != 0;
        private HashSet<int> enemyTiles = new HashSet<int>();
        private bool injectOwnerFailure;
        private CompletedMoatRelationship ResolveCompletedMoatRelationship(int player, int tile)
        {
            if (injectOwnerFailure) throw new Exception("injected owner lookup failure");
            return enemyTiles.Contains(tile) ? CompletedMoatRelationship.Enemy : CompletedMoatRelationship.Friendly;
        }
        private bool IsFriendlyCompletedMoatForWeightedShadow(int player, int tile) =>
            IsCompletedMoatTile(tile) && ResolveCompletedMoatRelationship(player, tile) == CompletedMoatRelationship.Friendly;
        private bool TryQualifyAttackMovementPlan(int id, GameUnit* u, int vanilla,
            out PlanScope p, out RouteProbeSummary s, out string reason)
        { p = null; s = default; reason = "fixture-no-attack"; return false; }
        private static bool IsAttackCommand(TribeAICommand c) => false;
        private static void LogPipelineDiagnostic(string s) { }
        private static void LogMovementContext(string s) { }
        private static void LogModeContext(PlanScope p, GameUnit* u, int v)
        { Check(p.UnitId > 0 && GameUnitManagerAPI.Instance.Units + p.UnitId == u, "mode plan matches actual unit"); }
        private static void LogFailure(string s, Exception e) => throw new Exception(s, e);
        private static void LogUnscopedAttackMode(int id, GameUnit* u, int v) { }
        private static void LogAttackScopeDecision(string s, int id, GameUnit* u, int v, string reason, RouteProbeSummary sum) { }
        private static void MarkTrackedAttackPipeline(int id, AttackPipelineStage stage, int x, int y, bool b) { }
        private static void LogDiggerDecision(string s, int id, GameUnit* u, int x, int y, bool b, bool friendlyMoatRequired) { }
        private static int assertions;
        private static void Check(bool result, string message)
        { assertions++; if (!result) throw new Exception("FAIL: " + message); }

        public static void RunTests()
        {
            var f = new MoveMoatPathTest();
            f.Tests();
            Console.WriteLine("PASS: " + assertions + " assertions (unit plans, native buffers, real tile graph, work selection cache).");
        }
        private void Tests()
        {
            var allocations = new List<IntPtr>();
            IntPtr Alloc(int bytes)
            { var p = (IntPtr)NativeMemory.AllocZeroed((nuint)bytes); allocations.Add(p); return p; }
            try
            {
                int* rows = (int*)Alloc(800 * 3 * sizeof(int));
                for (int y = 0; y < 800; y++) rows[y * 3] = NativeTileCount;
                rows[10 * 3] = 1000;
                GameTileManagerAPI.Instance.Rows = rows;
                tileFlags = (uint*)Alloc(NativeTileCount * sizeof(uint));
                pathRegionGrid = (short*)Alloc(NativeTileCount * sizeof(short));
                ushort* buildings = (ushort*)Alloc(NativeTileCount * 2);
                byte* heights = (byte*)Alloc(NativeTileCount);
                nativeHeightLayer = heights;
                movementTargetAvailability = (byte*)Alloc(MapCellCount);
                movementTargetAvailability[10*800+17] = 1;
                byte* masks = (byte*)Alloc(NativeTileCount);
                byte* directions = (byte*)Alloc(8);
                byte* types = (byte*)Alloc(0x32C * 10001);
                for (int i = 0; i < 8; i++) directions[i] = (byte)(1 << i);
                for (int tile = 0; tile < NativeTileCount; tile++) tileFlags[tile] = 0x100;
                // One-tile-wide corridor: ground 10..12, friendly moat 13, ground 14..18.
                for (int x = 10; x <= 18; x++)
                { tileFlags[1000 + x] = 0x8000; masks[1000 + x] = 0x44; pathRegionGrid[1000 + x] = (short)(x < 13 ? 1 : 2); }
                masks[1010] = 0x04; masks[1018] = 0x40;
                tileFlags[1013] = CompletedMoatTileFlag;
                weightedMoatRoutePlanner = new WeightedMoatRoutePlanner(rows, tileFlags, buildings,
                    heights, masks, directions, types, ResolveCompletedMoatRelationship, tile => false);
                GameUnit* units = (GameUnit*)Alloc(32 * sizeof(GameUnit));
                GameUnitManagerAPI.Instance.Units = units;
                for (int id = 1; id <= 27; id++)
                    units[id] = new GameUnit { Digger = true, r_ControllableForPlayerId = 1,
                        r_CurrentTilePositionX = 10, r_CurrentTilePositionY = 10,
                        r_NextTilePositionX2 = 10, r_NextTilePositionY2 = 10, r_MovingRelevant = 8 };
                nativeUnitManager = (byte*)Alloc(NativeUnitPathBufferOffset + 32 * NativeUnitPathBufferStride);
                nativePathManager = Alloc(PathManagerOutputLengthOffset + 16);
                moatPathMode = (int*)Alloc(sizeof(int));
                byte* manager = (byte*)nativePathManager;
                *(int*)(manager + 8) = 10; *(int*)(manager + 12) = 10;
                *(int*)(manager + 16) = 17; *(int*)(manager + 20) = 10;
                activeMoveCommand = new MoveCommandScope { TargetX = 17, TargetY = 10 };
                for (int id = 1; id <= 27; id++)
                {
                    Check(EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, id) == 1, "group member qualified");
                    Check(pendingPlan.UnitId == id, "group plan identity");
                    byte* path = nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
                    *(byte**)(manager + PathManagerOutputBufferOffset) = path;
                    Check(GetBuilderPlan(nativePathManager) == pendingPlan, "builder binds each actual buffer");
                    Check(TryCaptureUnitFallbackPathBuffer(nativePathManager, pendingPlan, units + id,
                        out byte* captured, out int length, out byte[] backup), "capture matching buffer");
                    path[0] = 123;
                    RestoreFallbackPathBuffer(nativePathManager, captured, backup, length);
                    Check(path[0] == 0, "rollback bytes");
                }
                Check(activeMoveCommand.TargetedRouteSearches == 1, "one qualification search for ordinary group");
                Check(activeMoveCommand.TargetedRouteCacheHits == 26, "remaining members reuse decision");
                Check(!TryCaptureUnitFallbackPathBuffer(nativePathManager, new PlanScope(1,17,10), units+1,
                    out _, out _, out _), "foreign buffer rejected");
                *(int*)(manager+8) = 11;
                Check(!TryCaptureUnitFallbackPathBuffer(nativePathManager, pendingPlan, units+27,
                    out _, out _, out _), "wrong actual start rejected");
                *(int*)(manager+8) = 10;
                Check(!TryCaptureUnitFallbackPathBuffer(nativePathManager, new PlanScope(27,18,10), units+27,
                    out _, out _, out _), "wrong target rejected");
                units[2].Digger = false;
                Check(EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,2) == 0, "mixed group capability");
                units[2].Digger = true;
                activePlan = new PlanScope(1,17,10) { FriendlyRouteQualified = true };
                EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,2);
                Check(activePlan.UnitId == 1 && pendingPlan.UnitId == 2, "outer context preserved");
                *(byte**)(manager+PathManagerOutputBufferOffset) = nativeUnitManager + NativeUnitPathBufferOffset + 2*1000;
                Check(GetBuilderPlan(nativePathManager).UnitId == 2, "nested buffer selects pending unit");
                activePlan = new PlanScope(2,18,10);
                EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,2);
                Check(activePlan.UnitId == 2 && activePlan.TargetX == 18 && pendingPlan.TargetX == 18,
                    "central planner formation target retained");
                activePlan = new PlanScope(1,17,10) { FriendlyRouteQualified = true };
                pendingPlan = new PlanScope(2,18,10) { FriendlyRouteQualified = true, MoatWorkMovement = true };
                Check(EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,2) == 1 &&
                    pendingPlan.MoatWorkMovement && pendingPlan.TargetX == 18 && activePlan.UnitId == 1,
                    "nested work handoff survives unrelated outer plan");
                pendingPlan = new PlanScope(2,18,10) { FriendlyRouteQualified = true };
                activePlan = null;
                activeMoveCommand = new MoveCommandScope { TargetX = 11, TargetY = 10 };
                Check(EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,2) == 0, "new ground target rejects old qualification");
                int searched = activeMoveCommand.TargetedRouteSearches;
                Check(EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,3) == 0, "negative decision reused");
                Check(activeMoveCommand.TargetedRouteSearches == searched, "negative cache avoids new search");
                activeMoveCommand = null; pendingPlan = null;

                MoatWorkSelectionScope NewScope() => new MoatWorkSelectionScope(mapEpoch, (IntPtr)1, 1, 1, 2, 10, 10, 1010, 1);
                var work = NewScope();
                for (int repeat = 0; repeat < 20; repeat++)
                for (int x = 14; x <= 18; x++)
                    Check(TryGetMoatWorkRoute(work,x,10,out _), "friendly work candidate reachable");
                Check(work.SearchBuilds == 1 && work.EndpointCacheHits == 95, "one graph for many fill candidates");
                Check(!TryGetMoatWorkRoute(work,9,10,out _), "unreachable endpoint");
                int builds = work.SearchBuilds;
                Check(!TryGetMoatWorkRoute(work,9,10,out _), "negative endpoint cache");
                Check(work.SearchBuilds == builds, "negative endpoint does not rebuild");
                Check(!cacheIncludesEnemyRoutes, "work map excludes enemy diagnostic graph");
                // Terrain changes between selections, with unchanged player/start/tick.
                tileFlags[1013] = 0x8000;
                Check(!TryGetMoatWorkRoute(NewScope(),17,10,out var ground) && ground.ReachedWithoutMoat,
                    "new selection sees filled moat as ground");
                tileFlags[1013] = CompletedMoatTileFlag; enemyTiles.Add(1013);
                Check(!TryGetMoatWorkRoute(NewScope(),17,10,out var enemy) && !enemy.ReachedWithMoat,
                    "enemy moat cannot be work traversal");
                EnsureReachabilityMap(1,10,10,includeEnemyRoutes:true);
                Check(GetCachedRouteSummaryForTarget(17,10).EnemyOnlyReachable, "explicit enemy cursor diagnostic");
                enemyTiles.Clear();
                var handoff = NewScope();
                Check(TryGetMoatWorkRoute(handoff,17,10,out _), "fresh handoff");
                var approach = new MoatWorkApproach(1,2017,17,10,1017,0,default);
                var pending = new PendingFillMoatApproach(mapEpoch,(IntPtr)1,1,1,1,10,10,approach,true)
                    { SearchScope = handoff };
                Check(ValidatePendingFillApproach(pending), "selected approach valid");
                occupied = true;
                Check(!ValidatePendingFillApproach(pending), "occupancy rechecked despite positive cache");
                occupied = false;
                Check(ValidatePendingFillApproach(pending), "released occupancy permits handoff");
                tick++;
                Check(!TryGetMoatWorkRoute(handoff,17,10,out _), "expired handoff rejected");
                Check(!ValidatePendingFillApproach(pending), "expired fill handoff rejected");
                Check(TryGetMoatWorkRoute(NewScope(),17,10,out _), "new tick selection recomputes");
                units[1].r_CurrentTilePositionX = 13;
                var moatStart = new MoatWorkSelectionScope(mapEpoch,(IntPtr)1,1,1,2,13,10,1013,2);
                Check(TryGetMoatWorkRoute(moatStart,17,10,out _), "start on friendly moat");
                units[1].r_CurrentTilePositionX = 10;

                // Execute the actual builder transaction with controlled native failures.
                activeMoveCommand = new MoveCommandScope { TargetX = 17, TargetY = 10 };
                pendingPlan = new PlanScope(1,17,10) { FriendlyRouteQualified = true, ModeObserved = true };
                byte* retryPath = nativeUnitManager + NativeUnitPathBufferOffset + 1000;
                *(byte**)(manager+PathManagerOutputBufferOffset) = retryPath;
                for (int scenario = 0; scenario < 3; scenario++)
                {
                    int calls = 0;
                    bool throwRetry = scenario == 2;
                    throwAudit = scenario == 1;
                    retryPath[0] = 3;
                    *(int*)(manager+PathManagerOutputLengthOffset) = 0;
                    *(int*)(manager+PathManagerRouteVariantOffset) = 1;
                    *moatPathMode = 1;
                    originalPathBuilder = (m,c,p) => {
                        if (++calls == 1) return 0;
                        retryPath[0] = 77;
                        *(int*)(manager+PathManagerOutputLengthOffset) = 7;
                        *(int*)(manager+PathManagerRouteVariantOffset) = 999;
                        *moatPathMode = 0;
                        if (throwRetry) throw new Exception("injected retry failure");
                        return 7;
                    };
                    Check(BuildPathWithCompletedMoatRouteVariantCore(nativePathManager,1,1) == 0, "failed retry returns vanilla result");
                    Check(calls == 2, "vanilla and one explicit retry only");
                    Check(retryPath[0] == 3 && *(int*)(manager+PathManagerOutputLengthOffset) == 0,
                        "transaction restores buffer and length");
                    Check(*(int*)(manager+PathManagerRouteVariantOffset) == 1 && *moatPathMode == 1,
                        "transaction restores route variant and moat mode");
                }
                int positiveCalls = 0;
                originalPathBuilder = (m,c,p) => { positiveCalls++; return 5; };
                Check(BuildPathWithCompletedMoatRouteVariantCore(nativePathManager,1,1) == 5 && positiveCalls == 1,
                    "positive vanilla builder runs exactly once");
                injectOwnerFailure = true;
                bool searchThrew = false;
                try { TryGetMoatWorkRoute(NewScope(),17,10,out _); }
                catch { searchThrew = true; }
                Check(searchThrew && cacheMapEpoch == -1, "incomplete graph is never published");
                injectOwnerFailure = false;
                Check(TryGetMoatWorkRoute(NewScope(),17,10,out _), "search recovers after lookup failure");
            }
            finally { foreach (var p in allocations) NativeMemory.Free((void*)p); }
        }
    }
}
