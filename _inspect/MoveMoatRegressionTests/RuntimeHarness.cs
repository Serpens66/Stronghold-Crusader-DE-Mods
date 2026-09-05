using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MoveMoatTest
{
    internal enum AliveState { IsAlive, Dead }
    internal enum eStructs { STRUCT_NULL }
    internal enum TribeAICommand { Move, AttackUnit=4, AttackBuilding=5, DigMoatTileId = 6, Unknown7 = 7 }
    internal enum EventHookPhase { Pre, Post }
    internal class UnitMoveHereEventArgs
    {
        public EventHookPhase Phase;
        public int UnitId, TileX, TileY, Unknown;
        public long ReturnValue;
        public bool SkipOriginalFunction;
        public UnitMoveHereEventArgs(EventHookPhase phase, int unitId, int tileX, int tileY, int unknown)
        { Phase = phase; UnitId = unitId; TileX = tileX; TileY = tileY; Unknown = unknown; }
    }
    internal struct UnmanagedVector2<T> { public T X, Y; }
    internal enum AttackPipelineStage { Mode, Builder }
    [StructLayout(LayoutKind.Sequential, Size = 0x490)]
    internal unsafe struct GameUnit
    {
        public uint r_GlobalId, r_CurrentPositionTileId;
        public int r_TribeId;
        public ushort r_AttackMoveToTargetTileX, r_AttackMoveToTargetTileY;
        public int r_ControllableForPlayerId, r_CurrentTilePositionX, r_CurrentTilePositionY;
        public int r_NextTilePositionX2, r_NextTilePositionY2, r_PathPlanStateBitFlags, r_MovingRelevant;
        public int r_AI_LastIssuedTribeCommand;
        public int r_AIState;
        public AliveState r_AliveState;
        public int r_UnitSelected, r_UnitSelected2;
        public bool Digger;
        public int r_UnitChimp;
    }
    internal struct GameCursorManager { public uint r_HoverOverBuildingId,r_HoverOverUnitId,r_HoverOverBuildingTileId,r_MouseTileId2,r_HoveringOverWall,r_MouseTileId,r_MouseTileX,r_MouseTileY; }
    internal unsafe struct CursorPointer { public GameCursorManager* Pointer; }
    internal unsafe class GameUnitManagerAPI
    {
        public static GameUnitManagerAPI Instance = new GameUnitManagerAPI();
        public GameUnit* Units;
        public Span<GameUnit> GetUnitsAsSpan() => new Span<GameUnit>(Units+1,1024);
        public bool TryGetUnitById(int id, out GameUnit* unit)
        { unit = id > 0 && id < 1025 ? Units + id : null; return unit != null; }
    }
    internal unsafe class GamePlayerManagerAPI
    {
        public static GamePlayerManagerAPI Instance = new GamePlayerManagerAPI();
        public GameCursorManager* Cursor;
        public CursorPointer GetCursorManager() => new CursorPointer { Pointer=Cursor };
        public bool IsPlayerIdValid(int id) => id > 0 && id <= 8;
        public bool IsPlayerAlliedTo(int a, int b) => a == b;
        public int GetSelectedChimpsCount() => EngineInterface.Selection.Length / 2;
    }
    internal unsafe class GameTileManagerAPI
    {
        public static GameTileManagerAPI Instance = new GameTileManagerAPI();
        public int* Rows;
        public ushort* Buildings;
        public ushort GetTileBuildingId(int tile) => Buildings[tile];
        public int GetTileId(int x, int y) => x >= 0 && x < 800 && y >= 0 && y < 800 ? Rows[y * 3] + x : -1;
        public IntPtr TileManager = (IntPtr)1;
        public IntPtr GetTileManager() => TileManager;
        public readonly Dictionary<int,int> Occupants = new Dictionary<int,int>();
        public bool ForceOccupied;
        public int GetTileUnitId(int tile) => ForceOccupied ? 2 : Occupants.TryGetValue(tile,out int id) ? id : 0;
        public UnmanagedVector2<ushort> GetTileVectorFromId(int tile) =>
            new UnmanagedVector2<ushort> { X = (ushort)(tile % 1000), Y = (ushort)(tile >= 2000 ? 11 : 10) };
    }
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private static int tick = 10;
        private object log;
        private static int CaptureCurrentGameTick() => tick;
        private bool disposed, targetedRouteProbeBusy, weightedShadowBusy;
        private int mapEpoch = 1;
        private PlanScope activePlan, pendingPlan;
        private UnitMoveFrame unitMoveFrame;
        private MoveCommandScope activeMoveCommand;
        private AttackCommandScope activeAttackCommand;
        private object activeAttackApproachDiagnostic;
        private int* cursorTargetX, cursorTargetY;
        private Func<IntPtr,int> originalCursorTilePairFallbackSelection, selectionCanDigMoat;
        private Func<IntPtr,int,int> getRepresentativeSelectedUnit;
        private bool TryResolveHostileLivingBuildingFromRawCursor(int p,uint b,uint h,uint m2,uint m,int x,int y,out int tx,out int ty,out int tile,out BuildingCursorTarget target)
        { tx=ty=tile=-1;target=default;return false; }
        private bool TryGetHostileLivingBuildingForCursor(int p,int tile,out BuildingCursorTarget target,out bool wall)
        { target=default;wall=false;return false; }
        private AttackCursorPairScope pendingAttackCursorPair;
        private Func<IntPtr,int,int,byte,int> originalCursorTilePairReachability;
        private void DiagnoseAttackApproachTilePair(object scope,int t,int s,byte cache,int result) {}
        private bool TryProbeBuildingApproachCursorRoute(AttackCursorPairScope s,out bool n,out bool f,out int x,out int y,out RouteProbeSummary r) { n=f=false;x=y=-1;r=default;return false; }
        private long nativeModeEntries, preBuilderFailures, preBuilderRecovered;
        private readonly Dictionary<string,long> preBuilderRejections=new Dictionary<string,long>();
        private void LogUnitWithoutBuilder(UnitMoveFrame frame, long result) {}
        private MoatWorkSelectionScope activeMoatWorkSelection;
        private IntPtr nativePathManager;
        private IntPtr nativeTribeManager;
        private DirectCursorMoveScope activeDirectCursorMove;
        private Func<IntPtr,int,int> originalFirstGroupUnitOnCompletedMoat;
        private Func<IntPtr,int,int,int> getGroupUnitId;
        private static void LogCommandDiagnostic(string message) {}
        private void InstallConnectivityObserver<T>(ReadOnlySpan<byte> memory, ulong libraryBase, int rva, string bytes, T callback, out T original) where T : Delegate { original = callback; }
        private byte* nativeUnitManager;
        private byte* nativeHeightLayer, movementTargetAvailability, nativeMovementMasks;
        private ushort* nativeBuildingLayer;
        private int* moatPathMode;
        private uint* tileFlags;
        private short* pathRegionGrid;
        private WeightedMoatRoutePlanner weightedMoatRoutePlanner;
        private int[] visitedWithoutMoat, visitedWithMoat, visitedWithEnemyMoat;
        private int[] distanceWithoutMoat, distanceWithMoat, distanceWithEnemyMoat, queue;
        private int[] observedRouteRegions, reachedGroundRegions, reachedFriendlyMoatRegions, reachedEnemyMoatRegions;
        private int gridGeneration, cacheMapEpoch = -1, cachePlayerId, cacheStartX, cacheStartY;
        private bool cacheIncludesEnemyRoutes;
        private int reachabilityQueueHead, reachabilityQueueTail, reachabilityTick;
        private object reachabilityOwner;
        private readonly Dictionary<long, bool> nativeGroundDecisions = new Dictionary<long, bool>();
        private object nativeGroundOwner;
        private int nativeGroundEpoch, nativeGroundTick, nativeGroundPlayer;
        private bool nativeGroundProbeBusy;
        private long nativeGroundQueries, nativeGroundCacheHits;
        private int cachedReachabilityExpandedNodes, cachedTraversedRegionCount, cachedReachabilityMapHits;
        private RouteProbeSummary cachedRouteSummary;
        private BuildingConsumerPerformanceScope activeBuildingConsumerPerformance;
        private Performance activeBuildingApproachPerformance;
        private class Performance { public int ReachabilityCacheHits, ReachabilityMapsBuilt; }
        private class MoveCommandScope
        {
            internal GroupRouteSession Routes = new GroupRouteSession(MoveMoatTestPlugin.Settings.EnableMod, MoveMoatTestPlugin.Settings.RouteMode == 1);
            internal int[] ActiveUnitIdsAtDispatch = Array.Empty<int>();
            public bool IsNewOrder;
            public int WeightedPublished;
            public int TargetX, TargetY, ModeCalls, TargetedRouteCacheHits, TargetedRouteSearches, TargetedRouteExpandedNodes;
            public int TargetedRouteSearchPasses, BuilderCalls, FloodFillBypasses, FallbackBuilderCalls, FallbackRollbacks;
            public bool BuilderReached;
            public int RegionCalls, TribeId, UnitsOnMoatAtDispatch;
            public bool MoatRelevant;
            public string LastGroupMoatModeDiagnostic;
            public int UnitMoveCalls, UnitMoveCompleted, UnitMovePositive, UnitMoveWithoutBuilder, UnitMoveAlreadyArrived;
            public int UnitMoveAbandoned, BuilderIntermediateTargets, FallbackContractRejections;
            public double TargetedRouteSearchMilliseconds, TargetedRouteMaximumSearchMilliseconds;
            public Dictionary<RouteDecisionKey, TargetedRouteDecision> TargetedRouteDecisions = new Dictionary<RouteDecisionKey, TargetedRouteDecision>();
        }
        private Func<IntPtr,int,int,int> originalBuildingCursorReachability = (m,b,u)=>0;
        private Func<IntPtr,int,int> getMoatIdAtTile;
        private Func<IntPtr,int,int,int,int> originalHasFillMoatApproach;
        private Func<IntPtr,int,int,int,int> originalFindMoatWorkTarget;
        private bool IsImprovedMoatFillingEnabled() => true;
        private Func<IntPtr, int, int> originalUnitStandingOnCompletedMoat = (p, id) => 0;
        private Func<IntPtr, int, int, int> originalPathBuilder;
        private Func<IntPtr, int> originalPathReconstruction;
        private Func<IntPtr, int, int, int, int, int> originalRegionPairReachability;
        private Func<IntPtr, int, int, int, int, int> originalRegionReachability = (p, player, region, x, y) => 0;
        private bool TryAllowDigWorkRegionSearch(IntPtr p, int player, int region, int x, int y, int vanilla, out int result)
        { result = vanilla; return false; }
        private bool TryAllowEarlyMoveHereGroupRegion(IntPtr p, int player, int region, int x, int y, int vanilla, out int result)
        { result = vanilla; return false; }
        private bool throwAudit;
        private class BuilderWeightedScope
        {
            public PlanScope FillPlan;
            public string WorkKind;
            public BuilderWeightedScope() {}
            public BuilderWeightedScope(int epoch,int id,int type,int player,int tribe,TribeAICommand command,string context,
                int sequence,int sx,int sy,int tx,int ty,int currentX,int currentY,uint ai,uint raw,string work,string phase,
                WeightedMovementCostProfile profile,bool reserved,bool calibratable)
            { MapEpoch=epoch;UnitId=id;UnitType=type;PlayerId=player;TribeId=tribe;Command=command;CommandContext=context;
                CommandSequence=sequence;StartX=sx;StartY=sy;TargetX=tx;TargetY=ty;SnapshotCurrentX=currentX;SnapshotCurrentY=currentY;
                AiState=ai;RawCommand=raw;WorkKind=work;WorkPhase=phase;CostProfile=profile;AllowReservedTarget=reserved; }
            public int MapEpoch,TribeId,CommandSequence,SnapshotCurrentX,SnapshotCurrentY;
            public uint AiState,RawCommand;
            public TribeAICommand Command;
            public string CommandContext,WorkPhase;
            public string CaptureSource => "unit-builder";
            public long OptimisticLowerBoundTicks;
            public int PublishedBuilderResult = -1;
            public int UnitId, UnitType, PlayerId, StartX, StartY, TargetX, TargetY, SearchPasses;
            public uint UnitGlobalId;
            public bool AllowReservedTarget, CandidateFound;
            public WeightedMovementCostProfile CostProfile;
            public WeightedMoatRouteSummary Candidate;
            public WeightedMoatEncodedRoute CandidateRoute;
            public double AccumulatedSearchMilliseconds;
        }
        private CadenceFixture nativeMovementCadenceResolver = new CadenceFixture();
        private class CadenceFixture
        {
            public bool TryGetPlausibleSpeedBonuses(int type, int current, out int[] bonuses, out ulong rva, out string reason)
            { bonuses = new[] {current,1}; rva=0;reason=null;return true; }
        }
        private bool captureWeighted;
        private bool TryCaptureWeightedMovementCostProfile(GameUnit* u,out WeightedMovementCostProfile p,out string why)
        { bool valid=WeightedMovementCostProfile.TryCreate(1,1,0,0,0,0,false,out p,out why);return captureWeighted && valid; }
        private void ResolveCommandDiagnosticContext(int id,GameUnit* u,out TribeAICommand command,out string context,out int sequence)
        { command=(TribeAICommand)u->r_AI_LastIssuedTribeCommand;context="fixture";sequence=0; }
        private bool IsIsolatedActiveGroupUnit(int id,int tribe)=>false;
        private void LogWeightedShadowDecision(BuilderWeightedScope s,string d)=>RecordFillRouteDecision(s,d);
        private void LogWeightedPublicationDecision(int id,string message) {}
        private void StartOrRefreshWeightedShadowTracker(BuilderWeightedScope s,int length,WeightedMoatRouteSummary n,bool valid,string d) {}
        private PendingFillMoatApproach pendingFillMoatApproach;
        private PendingDigMoatTarget pendingDigMoatTarget;
        private Func<IntPtr,int,int,uint,uint,int> originalResolveMoatWorkTile;
        private void LogMoatWorkSelection(MoatWorkSelectionScope scope) {}
        private void LogResolvedFillMoatApproach(PendingFillMoatApproach p) {}
        private void LogResolvedDigMoatTarget(PendingDigMoatTarget p) {}
        private bool ShouldLogUnitPipeline => false;
        private static void RecordVanillaBuilderResult(MoveCommandScope c, int r) { }
        private static void RecordBuilderResult(MoveCommandScope c, int r) { }
        private static void MarkCommandMoatRelevant(MoveCommandScope c, RouteProbeSummary s) { }
        private void RecordFallbackContractRejection(PlanScope p, string reason = "retry-contract", IntPtr m = default)
        { if (activeMoveCommand != null) activeMoveCommand.FallbackContractRejections++; }
        private static void TryLogDiagnosticFailure(string s, Exception e) { }
        private static void LogBuilderDecision(string s) { }
        private static void StartOrRefreshMoatMoveTracker(PlanScope p, RouteProbeSummary s, int r) { }
        private static bool IsPublishedWalkableBuildingApproach(int unitId, int tileId) => false;
        private bool occupied { get => GameTileManagerAPI.Instance.ForceOccupied; set => GameTileManagerAPI.Instance.ForceOccupied=value; }
        private static bool HasDownstreamMovementBlockingFlags(uint flags) => (flags & 0x10000130) != 0;
        private static bool CanDigMoat(GameUnit* u) => u->Digger;
        private static bool IsValidTileId(int tile) => tile >= 0 && tile < NativeTileCount;
        private bool IsCompletedMoatTile(int tile) => (tileFlags[tile] & CompletedMoatTileFlag) != 0;
        private HashSet<int> enemyTiles = new HashSet<int>();
        private int enemyPlayerId;
        private bool injectOwnerFailure;
        private CompletedMoatRelationship ResolveCompletedMoatRelationship(int player, int tile)
        {
            if (injectOwnerFailure) throw new Exception("injected owner lookup failure");
            if (throwAudit) throw new Exception("injected audit failure");
            return enemyTiles.Contains(tile) && (enemyPlayerId == 0 || enemyPlayerId == player)
                ? CompletedMoatRelationship.Enemy : CompletedMoatRelationship.Friendly;
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
                ushort* buildings = (ushort*)Alloc(NativeTileCount * 2); nativeBuildingLayer = buildings;
                byte* heights = (byte*)Alloc(NativeTileCount);
                nativeHeightLayer = heights;
                movementTargetAvailability = (byte*)Alloc(MapCellCount);
                nativePlaceReservations = (byte*)Alloc(NativeTileCount);
                movementTargetAvailability[10*800+17] = 1;
                byte* masks = (byte*)Alloc(NativeTileCount); nativeMovementMasks = masks;
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
                GameUnit* units = (GameUnit*)Alloc(1025 * sizeof(GameUnit));
                GameUnitManagerAPI.Instance.Units = units;
                for (int id = 1; id <= 1000; id++)
                    units[id] = new GameUnit { Digger = true, r_ControllableForPlayerId = 1,
                        r_CurrentTilePositionX = 10, r_CurrentTilePositionY = 10,
                        r_NextTilePositionX2 = 10, r_NextTilePositionY2 = 10, r_MovingRelevant = 8 };
                nativeUnitManager = (byte*)Alloc(NativeUnitPathBufferOffset + 1025 * NativeUnitPathBufferStride);
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

                TestSharedRoutePipeline(units);
                TestUnitMovePipeline(manager, units);
                captureWeighted=true;MoveMoatTestPlugin.Settings.RouteMode=1;
                TestUnitMovePipeline(manager, units);
                captureWeighted=false;MoveMoatTestPlugin.Settings.RouteMode=0;

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
                rows[11 * 3] = 2000;
                byte* handoffRecords = (byte*)Alloc(MoatRecordCountOffset + 16);
                *(int*)(handoffRecords + MoatRecordCountOffset) = 2;
                *(int*)(handoffRecords + MoatRecordArrayOffset + MoatRecordSize) = 2017;
                *(short*)(handoffRecords + MoatRecordArrayOffset + MoatRecordSize + 4) = 17;
                *(short*)(handoffRecords + MoatRecordArrayOffset + MoatRecordSize + 6) = 11;
                var pending = new PendingFillMoatApproach(mapEpoch,(IntPtr)handoffRecords,1,1,1,10,10,approach,true)
                    { SearchScope = handoff };
                Check(ValidatePendingFillApproach(pending), "selected approach valid");
                occupied = true;
                Check(!ValidatePendingFillApproach(pending), "occupancy rechecked despite positive cache");
                occupied = false;
                Check(ValidatePendingFillApproach(pending), "released occupancy permits handoff");
                tick++;
                Check(!TryGetMoatWorkRoute(handoff,17,10,out _), "expired handoff rejected");
                Check(!ValidatePendingFillApproach(pending), "expired fill handoff rejected");
                rows[11 * 3] = NativeTileCount;
                Check(TryGetMoatWorkRoute(NewScope(),17,10,out _), "new tick selection recomputes");
                units[1].r_CurrentTilePositionX = 13;
                var moatStart = new MoatWorkSelectionScope(mapEpoch,(IntPtr)1,1,1,2,13,10,1013,2);
                Check(TryGetMoatWorkRoute(moatStart,17,10,out _), "start on friendly moat");
                units[1].r_CurrentTilePositionX = 10;

                // The new fallback never calls the native flood a second time.
                activeMoveCommand = new MoveCommandScope { TargetX = 17, TargetY = 10 };
                pendingPlan = new PlanScope(1,17,10) { FriendlyRouteQualified = true, ModeObserved = true };
                byte* retryPath = nativeUnitManager + NativeUnitPathBufferOffset + 1000;
                *(byte**)(manager+PathManagerOutputBufferOffset) = retryPath;
                for (int scenario = 0; scenario < 3; scenario++)
                {
                    int calls = 0;
                    throwAudit = scenario == 1;
                    injectOwnerFailure = scenario == 2;
                    retryPath[0] = 3;
                    *(int*)(manager+PathManagerOutputLengthOffset) = 0;
                    *(int*)(manager+PathManagerRouteVariantOffset) = 1;
                    *moatPathMode = 1;
                    originalPathBuilder = (m,c,p) => {
                        calls++;
                        if (scenario == 0) enemyTiles.Add(1013);
                        return 0;
                    };
                    weightedMoatRoutePlanner.SetSearchSession(new object(), 1, mapEpoch, tick);
                    Check(BuildPathWithCompletedMoatRouteVariantCore(nativePathManager,1,1) == 0, "failed publication returns failure");
                    Check(calls == 1, "one vanilla call and no duplicate native flood");
                    Check(retryPath[0] == 3 && *(int*)(manager+PathManagerOutputLengthOffset) == 0,
                        "transaction restores buffer and length");
                    Check(*(int*)(manager+PathManagerRouteVariantOffset) == 1 && *moatPathMode == 1,
                        "transaction restores route variant and moat mode");
                    enemyTiles.Clear(); throwAudit = injectOwnerFailure = false;
                }
                int positiveCalls = 0;
                originalPathBuilder = (m,c,p) => {
                    positiveCalls++;
                    for (int i=0;i<4;i++) retryPath[i]=0x22;
                    *(int*)(manager+PathManagerOutputLengthOffset)=7;
                    return 7;
                };
                Check(BuildPathWithCompletedMoatRouteVariantCore(nativePathManager,1,1) == 7 && positiveCalls == 1,
                    "valid positive vanilla builder runs exactly once");                injectOwnerFailure = true;
                bool searchThrew = false;
                try { TryGetMoatWorkRoute(NewScope(),17,10,out _); }
                catch { searchThrew = true; }
                Check(searchThrew && cacheMapEpoch == -1, "incomplete graph is never published");
                injectOwnerFailure = false;
                Check(TryGetMoatWorkRoute(NewScope(),17,10,out _), "search recovers after lookup failure");
                CursorAdapterTests();
                FillSelectionTests();
                PlacementTests();
                FillFormationTests();
                BuildingCandidateTests();
            }
            finally { foreach (var p in allocations) NativeMemory.Free((void*)p); }
        }

        private void TestUnitMovePipeline(byte* manager, GameUnit* units)
        {
            byte* Buffer(int id) => nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
            void SetBuilder(int id, int x, int start = 10)
            {
                *(byte**)(manager + PathManagerOutputBufferOffset) = Buffer(id);
                *(int*)(manager + PathManagerOutputLengthOffset) = 0;
                *(int*)(manager + PathManagerRouteVariantOffset) = 1;
                *(int*)(manager + 8) = start; *(int*)(manager + 12) = 10;
                *(int*)(manager + 16) = x; *(int*)(manager + 20) = 10;
            }
            UnitMoveHereEventArgs Pre(int id, int x, int unknown = 0)
            {
                var args = new UnitMoveHereEventArgs(EventHookPhase.Pre, id, x, 10, unknown);
                ObserveUnitMoveOrder(args);
                return args;
            }
            void Post(int id, int x, long result = 1, int unknown = 0) =>
                ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Post, id, x, 10, unknown) { ReturnValue = result });
            void NewCommand(int x = 17)
            {
                ClearUnitMoveFrames();
                activePlan = pendingPlan = null;
                activeMoveCommand = new MoveCommandScope { TargetX = x, TargetY = 10 };
                if(MoveMoatTestPlugin.Settings.RouteMode==1)
                    activeMoveCommand.ActiveUnitIdsAtDispatch=System.Linq.Enumerable.Range(1,120).ToArray();
            }
            int nativeCalls = 0;
            originalPathBuilder = (m, c, p) =>
            {
                nativeCalls++;
                if (*moatPathMode == 0) return 0;
                int length = *(int*)(manager + 16) - *(int*)(manager + 8);
                byte* output = *(byte**)(manager + PathManagerOutputBufferOffset);
                for (int i = 0; i < (length + 1) / 2; i++) output[i] = 0x22;
                *(int*)(manager + PathManagerOutputLengthOffset) = length;
                return length;
            };
            // Actual sequence: group context -> per-unit Pre (formation target) ->
            // native mode callback -> unit buffer/target binding -> builder -> Post.
            originalPathReconstruction = m => originalPathBuilder(m, 1, 1);
            foreach (int count in new[] { 1, 5, 20, 27, 29, 120, 680, 1000 })
            foreach (bool formation in new[] { false, true })
            {
                NewCommand();
                if(MoveMoatTestPlugin.Settings.RouteMode==1)activeMoveCommand.ActiveUnitIdsAtDispatch=Enumerable.Range(1,count).ToArray();
                for (int id = 1; id <= count; id++)
                {
                    int target = formation ? 14 + id % 5 : 17;
                    Pre(id, target);
                    *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, id);
                    Check(*moatPathMode == 1, "native event qualifies actual formation target");
                    Check(AllowBuilderAfterFailedRegionSearch(nativePathManager, 1, 2, 10, 10) == 2,
                        "real unit context authorizes native region gate");
                    SetBuilder(id, target);
                    PlanScope request = unitMoveFrame.Plan;
                    Check(request.UnitId == id && request.TargetX == target && activeMoveCommand.TargetX == 17,
                        "request identity is separate from click target");
                    int before = nativeCalls;
                    int result = MoveMoatTestPlugin.Settings.RouteMode==1 && id%2==0
                        ? BuildReconstructedUnitPath(nativePathManager)
                        : BuildPathWithCompletedMoatRouteVariant(nativePathManager, 1, 1);
                    Check(result == target - 10 && nativeCalls == before + 1,
                        "every group unit gets vanilla-first and successful fallback");
                    Check(TryAuditFallbackPath(nativePathManager, Buffer(id), result, request, units + id, out _),
                        "published path bytes pass actual owner and endpoint audit");
                    Post(id, target, 1);
                    Check(unitMoveFrame == null && pendingPlan == null && activePlan == null,
                        "unit Post leaves no plan for next group member");
                }
                Check(activeMoveCommand.UnitMoveCalls == count && activeMoveCommand.UnitMoveCompleted == count &&
                    activeMoveCommand.UnitMovePositive == count && activeMoveCommand.BuilderCalls == count &&
                    activeMoveCommand.FallbackContractRejections == 0, "all eligible formation members accounted for");
                Check(activeMoveCommand.TargetedRouteSearches == (formation ? Math.Min(count, 5) : 1), "only exact endpoint decisions are shared");
            }

            NewCommand();
            Pre(1, 17);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            PlanScope outer = unitMoveFrame.Plan;
            Pre(2, 18);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 2);
            SetBuilder(2, 16);
            PlanScope intermediate = GetBuilderPlan(nativePathManager);
            Check(intermediate.UnitId == 2 && intermediate.TargetX == 16 && intermediate.ExactRouteEndpoints &&
                intermediate.FriendlyRouteQualified && unitMoveFrame.Plan.TargetX == 18,
                "native intermediate endpoint separately qualified without rewriting request");
            int intermediateResult = BuildPathWithCompletedMoatRouteVariantCore(nativePathManager, 1, 1);
            Check(intermediateResult == 6, "intermediate fallback published");
            Post(2, 18);
            Check(ReferenceEquals(unitMoveFrame.Plan, outer), "different-unit nested call restores parent plan");
            Pre(1, 18);
            EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            Check(!ReferenceEquals(unitMoveFrame.Plan, outer), "same-unit nested invocation owns new plan");
            Post(1, 18);
            Check(ReferenceEquals(unitMoveFrame.Plan, outer), "same-unit nested call restores parent");
            SetBuilder(2, 17);
            activePlan = new PlanScope(2, 17, 10) { FriendlyRouteQualified = true };
            Check(GetBuilderPlan(nativePathManager, true) == null, "foreign buffer rejected even if an older plan matches it");
            activePlan = null;
            SetBuilder(1, 17, 11);
            Check(GetBuilderPlan(nativePathManager, true) == null, "incorrect actual start rejected");
            SetBuilder(1, 800);
            Check(GetBuilderPlan(nativePathManager, true) == null, "invalid builder target rejected");
            SetBuilder(1, 17);
            *(int*)(manager + PathManagerOutputLengthOffset) = 2001;
            Check(!TryCaptureUnitFallbackPathBuffer(nativePathManager, outer, units + 1, out _, out _, out _),
                "invalid output length rejected before retry, not before native initialization");
            Check(DescribeFallbackContractFailure(nativePathManager, outer, units + 1) == "length",
                "length rejection has specific diagnostic reason");
            SetBuilder(1, 17);
            Check(!TryCaptureUnitFallbackPathBuffer(nativePathManager, outer, units + 2, out _, out _, out _),
                "foreign unit struct rejected even with matching coordinates");
            Post(1, 17);

            NewCommand();
            var changed = Pre(1, 17);
            changed.UnitId = 2; changed.TileX = 18; changed.Unknown = 1;
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 2);
            Check(unitMoveFrame.Plan.UnitId == 2 && unitMoveFrame.Plan.TargetX == 18,
                "later subscriber changes read before native mode");
            // Unknown=1 uses a different native builder; there need not be an F4930 callback.
            Post(1, 17, 1);
            Check(unitMoveFrame == null && activeMoveCommand.UnitMoveWithoutBuilder == 1,
                "Post with original args closes changed request without observed F4930");
            Pre(2, 18); // Simulate an earlier subscriber changing Pre before our observer.
            Post(1, 17);
            Check(unitMoveFrame == null, "earlier subscriber mutation still closes via synchronous LIFO");

            Pre(1, 17);
            var parentFrame = unitMoveFrame;
            Pre(2, 18); // Child input was originally (1,17), changed before our observer.
            Post(1, 17);
            Check(ReferenceEquals(unitMoveFrame, parentFrame),
                "mutated child's original Post args cannot accidentally close identical parent input");
            Post(1, 17);

            Pre(1, 17);
            EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            outer = unitMoveFrame.Plan;
            var skipped = Pre(1, 18);
            skipped.SkipOriginalFunction = true;
            Check(ReferenceEquals(GetCurrentUnitMoveFrame().Plan, outer), "skipped nested original restores parent before reuse");
            Post(1, 17);
            skipped = Pre(2, 18);
            skipped.SkipOriginalFunction = true;
            Pre(3, 17);
            Check(unitMoveFrame.Parent == null, "next Pre prunes skipped original with no Post");
            Post(3, 17, 0);
            Check(unitMoveFrame == null, "native early failure without builder closes scope");
            Pre(1, 17);
            tick++;
            Check(GetCurrentUnitMoveFrame() == null, "missing Post cannot survive tick change");
            Pre(1, 17);
            mapEpoch++;
            Check(GetCurrentUnitMoveFrame() == null, "missing Post cannot survive map change");
            Pre(1, 17);
            activeMoveCommand = new MoveCommandScope { TargetX = 18, TargetY = 10 };
            Check(GetCurrentUnitMoveFrame() == null, "missing Post cannot survive command replacement");
            Pre(1, 17);
            ClearUnitMoveFrames();
            Check(unitMoveFrame == null, "command end clears incomplete invocation");

            NewCommand();
            units[1].r_PathPlanStateBitFlags = 2;
            units[1].r_NextTilePositionX2 = 11;
            Pre(1, 17);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            SetBuilder(1, 17, 11);
            Check(unitMoveFrame.Plan.RouteStartX == 11 && BuildPathWithCompletedMoatRouteVariantCore(nativePathManager, 1, 1) == 6,
                "moving unit qualifies and publishes from native next tile");
            Post(1, 17);
            units[1].r_PathPlanStateBitFlags = 0; units[1].r_NextTilePositionX2 = 10;
            units[1].r_CurrentTilePositionX = 13;
            originalUnitStandingOnCompletedMoat = (p, id) => id == 1 ? 1 : 0;
            Pre(1, 17);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            SetBuilder(1, 17, 13);
            Check(unitMoveFrame.Plan.RouteStartX == 13 && BuildPathWithCompletedMoatRouteVariantCore(nativePathManager, 1, 1) == 4,
                "start on friendly moat preserves native positive builder");
            Post(1, 17);
            units[1].r_CurrentTilePositionX = 10;
            originalUnitStandingOnCompletedMoat = (p, id) => 0;

            NewCommand();
            Pre(1, 17);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            SetBuilder(1, 12);
            int callsBeforeGround = nativeCalls;
            Check(BuildPathWithCompletedMoatRouteVariantCore(nativePathManager, 1, 1) == 0 && nativeCalls == callsBeforeGround + 1,
                "ground intermediate uses vanilla mode with no moat retry");
            SetBuilder(1, 19);
            Check(!GetBuilderPlan(nativePathManager).FriendlyRouteQualified, "unreachable intermediate is not authorized");
            tileFlags[1018] = CompletedMoatTileFlag; enemyTiles.Add(1018);
            SetBuilder(1, 18);
            Check(!GetBuilderPlan(nativePathManager).FriendlyRouteQualified &&
                BuildPathWithCompletedMoatRouteVariantCore(nativePathManager, 1, 1) == 0,
                "enemy intermediate cannot inherit friendly request authorization");
            enemyTiles.Remove(1018); tileFlags[1018] = 0x8000;
            Post(1, 17);

            // Audit hostile path bytes even if a previous command cached a friendly decision.
            SetBuilder(1, 17);
            for (int i = 0; i < 4; i++) Buffer(1)[i] = 0x22;
            *(int*)(manager + PathManagerOutputLengthOffset) = 7;
            enemyTiles.Add(1013);
            Check(!TryAuditFallbackPath(nativePathManager, Buffer(1), 7, new PlanScope(1, 17, 10), units + 1, out _),
                "actual audit rejects enemy crossing despite valid direction bytes");
            enemyPlayerId = 1;
            SetBuilder(2, 17);
            for (int i = 0; i < 4; i++) Buffer(2)[i] = 0x22;
            *(int*)(manager + PathManagerOutputLengthOffset) = 7;
            units[2].r_ControllableForPlayerId = 2;
            Check(TryAuditFallbackPath(nativePathManager, Buffer(2), 7, new PlanScope(2, 17, 10), units + 2, out _),
                "audit recomputes cached enemy classifications for another player");
            units[2].r_ControllableForPlayerId = 1; enemyPlayerId = 0;
            SetBuilder(1, 17);
            *(int*)(manager + PathManagerOutputLengthOffset) = 7;
            enemyTiles.Clear();
            tileFlags[1016] = CompletedMoatTileFlag; enemyTiles.Add(1016);
            var fillContact = new PlanScope(1, 17, 10) { PlayerId = 1, MoatWorkMovement = true, MoatWorkTargetTileId = 1016 };
            units[1].r_AI_LastIssuedTribeCommand = (int)TribeAICommand.Unknown7;
            bool fillAllowed = TryAuditFallbackPath(nativePathManager, Buffer(1), 7, fillContact, units + 1, out string fillAudit);
            Check(fillAllowed, "native terminal Fill contact remains permitted: " + fillAudit);
            Check(!TryAuditFallbackPath(nativePathManager, Buffer(1), 7, new PlanScope(1, 17, 10), units + 1, out _),
                "terminal enemy contact never authorizes ordinary movement");
            enemyTiles.Clear(); tileFlags[1016] = 0x8000;
            units[1].r_AI_LastIssuedTribeCommand = 0;

            NewCommand();
            var workSource = new PlanScope(1, 17, 10) {
                MoatWorkMovement = true, MoatWorkTargetTileId = 2017, FriendlyRouteQualified = true,
                MoatWorkSearch = new MoatWorkSelectionScope(mapEpoch, (IntPtr)1, 1, 1, 2, 10, 10, 1010, 1)
            };
            pendingPlan = workSource;
            activePlan = new PlanScope(2, 18, 10) { FriendlyRouteQualified = true };
            Pre(1, 17);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            Check(unitMoveFrame.Plan.MoatWorkMovement && unitMoveFrame.Plan.MoatWorkTargetTileId == 2017 &&
                ReferenceEquals(unitMoveFrame.Plan.MoatWorkSearch, workSource.MoatWorkSearch),
                "matching work handoff retains work identity and shared selection graph");
            SetBuilder(1, 17);
            Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager, 1, 1) == 7 && pendingPlan == null,
                "actual wrapper consumes copied work handoff after builder");
            Post(1, 17);
            Check(activePlan.UnitId == 2 && workSource.TargetX == 17, "work call preserves unrelated outer context");
            NewCommand(); pendingPlan=workSource; activePlan=null;
            Pre(2,17);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,2);
            Check(unitMoveFrame.InheritedPlan==null && !unitMoveFrame.Plan.MoatWorkMovement,
                "foreign pending work context is not bound as a handoff");
            SetBuilder(2,17);
            BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1);
            Check(ReferenceEquals(pendingPlan,workSource),"foreign unit cannot consume another worker's pending handoff");
            Post(2,17);
            NewCommand();
            activePlan = new PlanScope(1, 17, 10) { AttackMovementQualified = true, PostCombatRepath = true };
            Pre(1, 17);
            EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            Check(unitMoveFrame.Plan.AttackMovementQualified && unitMoveFrame.Plan.PostCombatRepath,
                "matching attack and post-combat context flags survive event binding");
            Post(1, 17);
            Pre(1, 18);
            EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            Check(!unitMoveFrame.Plan.AttackMovementQualified, "different request target cannot inherit old attack context");
            Post(1, 18);

            NewCommand(); activeMoveCommand = null;
            Pre(1, 17);
            *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 1);
            SetBuilder(1, 12);
            int beforeStandalone = nativeCalls;
            Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager, 1, 1) == 0 && nativeCalls == beforeStandalone + 1,
                "standalone intermediate also restores vanilla mode without group context");
            Post(1, 17);

            // Cheap native PCL admission is a hint, never an endpoint reachability proof.
            NewCommand();
            int pclCalls = 0;
            *(int*)(manager+0xC0)=41; *(int*)(manager+0xC4)=42; *(int*)(manager+0x98)=43;
            originalRegionPairReachability = (p,player,source,target,mode) => {
                pclCalls++; *(int*)(manager+0xC0)=91; *(int*)(manager+0xC4)=92; *(int*)(manager+0x98)=0;
                return target;
            };
            for (int id=1;id<=2;id++)
            {
                Pre(id,17);
                *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,id);
                Check(*moatPathMode==0 && unitMoveFrame.Plan.NativeGroundPrecheck,"PCL positive defers to vanilla");
                Check(*(int*)(manager+0xC0)==41 && *(int*)(manager+0xC4)==42 && *(int*)(manager+0x98)==43,
                    "native precheck restores every documented scratch value");
                SetBuilder(id,17);
                Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1)==7,
                    "failed vanilla path still receives exact late moat qualification");
                Post(id,17);
            }
            Check(pclCalls==1,"group shares native PCL precheck");
            // Positive second-phase E2610 answers only describe a blocked portal route.
            NewCommand();
            originalRegionPairReachability=(p,player,source,target,mode)=> { *(int*)(manager+0x98)=1; return target; };
            Pre(1,17); *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            Check(!unitMoveFrame.Plan.NativeGroundPrecheck && *moatPathMode==1,"blocked portal hint cannot defer required moat admission");
            Post(1,17,0);
            originalRegionPairReachability=null;
            // Model the actual native failure branch, before buffer initialization.
            foreach(int id in new[]{1,2,5,20,27,29})
            {
                NewCommand(); Pre(id,17);
                *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,id);
                long runs=weightedMoatRoutePlanner.SearchRuns;
                Check(TryRecoverBeforeBuilder((IntPtr)nativeUnitManager,id,10,10,17,10)==1,"native portal failure enters own buffer initialization");
                Check(TryRecoverBeforeBuilder((IntPtr)nativeUnitManager,id,10,10,17,10)==0,"one recovery per invocation");
                SetBuilder(id,17);
                Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1)==7,"recovered native call publishes its retained owner-safe route");
                Check(weightedMoatRoutePlanner.SearchRuns<=runs+1,"recovery route is not searched again at publication");
                Post(id,17);
            }
            NewCommand(); Pre(1,17); *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            *(short*)((byte*)(units+1)+0x2A4)=44; *(short*)((byte*)(units+1)+0x290)=45;
            Check(TryRecoverBeforeBuilder((IntPtr)nativeUnitManager,1,10,10,17,10)==1,"rollback fixture recovers");
            enemyTiles.Add(1013); SetBuilder(1,17); Buffer(1)[0]=122;
            Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1)==0 && Buffer(1)[0]==122,"owner change rejects retained recovery and restores bytes");
            Post(1,17,0);
            Check(*(short*)((byte*)(units+1)+0x2A4)==44 && *(short*)((byte*)(units+1)+0x290)==45,"native failure restores recovery control fields");
            enemyTiles.Clear();
            NewCommand(); Pre(1,17); *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            Check(TryRecoverBeforeBuilder((IntPtr)nativeUnitManager,2,10,10,17,10)==0,"foreign recovery unit rejected");
            movementTargetAvailability[10*800+17]=0;
            Check(TryRecoverBeforeBuilder((IntPtr)nativeUnitManager,1,10,10,17,10)==0,"unavailable recovery target rejected");
            movementTargetAvailability[10*800+17]=1; Post(1,17,0);
            foreach(int id in new[]{799,800,32768,63999}) Check(IsValidMoatRecordId(id,64000),"full native moat capacity");
            foreach(int id in new[]{-1,0,64000,65535}) Check(!IsValidMoatRecordId(id,64000),"invalid moat slot rejected");
            Check(!IsValidMoatRecordId(950,950) && !IsValidMoatRecordId(950,64001),"moat high-water and capacity independently checked");

            NewCommand(); Pre(1,17,1);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            SetBuilder(1,17);
            int reconstructionCalls=0, oldNativeCalls=nativeCalls;
            originalPathReconstruction=m=>{
                reconstructionCalls++;
                for(int i=0;i<4;i++)Buffer(1)[i]=0x22;
                *(int*)(manager+PathManagerOutputLengthOffset)=7; return 7;
            };
            Check(BuildReconstructedUnitPath(nativePathManager)==7 && reconstructionCalls==1 && nativeCalls==oldNativeCalls,
                "E32B0 positive path uses its own audited adapter without F4930");
            Post(1,17,1,1);
            NewCommand(); Pre(1,17,1);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            SetBuilder(1,17); originalPathReconstruction=m=>0;
            Check(BuildReconstructedUnitPath(nativePathManager)==7,"E32B0 failure reuses managed qualified route");
            Post(1,17,1,1);

            NewCommand(); Pre(1,17,1);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            SetBuilder(1,17); Buffer(1)[0]=123;
            originalPathReconstruction=m=>{
                enemyTiles.Add(1013);
                for(int i=0;i<4;i++)Buffer(1)[i]=0x22;
                *(int*)(manager+PathManagerOutputLengthOffset)=7; return 7;
            };
            Check(BuildReconstructedUnitPath(nativePathManager)==0 && Buffer(1)[0]==123 &&
                *(int*)(manager+PathManagerOutputLengthOffset)==0,"unsafe E32B0 output is fully rolled back");
            enemyTiles.Clear(); Post(1,17,0,1);

            // A nonstandard reconstruction variant cannot turn an audit rejection into success.
            NewCommand(); Pre(1,17,1);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            SetBuilder(1,17); Buffer(1)[0]=124;
            *(int*)(manager+PathManagerRouteVariantOffset)=2;
            Check(BuildReconstructedUnitPath(nativePathManager)==0 && Buffer(1)[0]==124 &&
                *(int*)(manager+PathManagerRouteVariantOffset)==2,
                "unsupported reconstruction variant rejects and restores unsafe native result");
            enemyTiles.Clear(); Post(1,17,0,1);

            NewCommand(); Pre(1,17);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            SetBuilder(1,17);
            units[1].r_GlobalId++;
            Check(GetBuilderPlan(nativePathManager,true)==null,"recycled game ID cannot reuse old unit qualification");
            units[1].r_GlobalId--; Post(1,17,0);

            // Execute the actual weighted publisher against native encoded bytes.
            NewCommand(); Pre(1,17);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            SetBuilder(1,17);
            WeightedMovementCostProfile.TryCreate(1,1,0,0,0,0,false,out var profile,out _);
            byte* weightedBuffer=Buffer(1);
            for(int i=0;i<7;i++)weightedBuffer[i]=0;
            for(int i=0;i<13;i++)weightedBuffer[i>>1]|=(byte)((i<7||i>=10?2:6)<<((i&1)*4));
            *(int*)(manager+PathManagerOutputLengthOffset)=13;
            Check(weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,17,10,profile,weightedBuffer,13,false,out var baseline),
                "native incumbent fixture has valid real encoded edges");
            var shadow=new BuilderWeightedScope {UnitId=1,PlayerId=1,UnitGlobalId=units[1].r_GlobalId,
                StartX=10,StartY=10,TargetX=17,TargetY=10,CostProfile=profile};
            Check(TryPublishSafelyFasterWeightedRoute(nativePathManager,weightedBuffer,13,shadow,baseline,
                out var published,out long saving,out _,out _) && shadow.PublishedBuilderResult==7 && saving>0,
                "actual publisher enforces all profiles, writes own buffer and validates roundtrip");
            Check(profile.EstimateRouteTicks(baseline.GroundEdges,baseline.MoatEdges)-published.EstimatedTicks>=40,
                "actual runtime profile retains the forty-tick margin");
            Post(1,17);

            // A native work consumer may require one enemy contact as the penultimate node.
            NewCommand();
            tileFlags[1016]=CompletedMoatTileFlag; enemyTiles.Add(1016);
            units[1].r_AI_LastIssuedTribeCommand=(int)TribeAICommand.Unknown7;
            pendingPlan=new PlanScope(1,17,10) {MoatWorkMovement=true,MoatWorkTargetTileId=1016,
                MoatWorkSearch=new MoatWorkSelectionScope(mapEpoch,(IntPtr)1,1,1,2,10,10,1010,1)};
            Pre(1,17);
            *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
            Check(*moatPathMode==1 && unitMoveFrame.Plan.QualifiedTerminalRoute.IsValid,
                "actual work context qualifies exact terminal fill endpoint");
            SetBuilder(1,17);
            Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1)==7,
                "managed fallback preserves terminal fill contact without enemy transit");
            Post(1,17);
            enemyTiles.Clear(); tileFlags[1016]=0x8000; units[1].r_AI_LastIssuedTribeCommand=0;

            units[2].Digger = false;
            NewCommand(); Pre(2, 17);
            Check(EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, 2) == 0,
                "mixed group member without moat capability receives no mode");
            Post(2, 17, 0); units[2].Digger = true;
            NewCommand();
            activeMoveCommand = null;
            SetBuilder(1, 17);
            *moatPathMode = 0;
        }
    }
}

public static class EngineInterface { private static int[] selectedChimps = Array.Empty<int>(); public static int[] Selection { get=>selectedChimps; set=>selectedChimps=value; } }
namespace MoveMoatTest {
    internal struct GameBuilding { public uint r_GlobalId; public AliveState r_AliveState; public int r_PlayerIdOwner, r_BuildingType; public int r_TilePositionXBegin, r_TilePositionXEnd, r_TilePositionYBegin, r_TilePositionYEnd; }
    internal unsafe class GameBuildingManagerAPI {
        public static GameBuildingManagerAPI Instance = new GameBuildingManagerAPI();
        public GameBuilding* Building;
        public bool TryGetBuildingById(int id,out GameBuilding* building) { building=id==1?Building:null;return building!=null; }
    }
}

namespace Shared { internal static class DebugLogHelper { public static void LogInfo(object log,string text) {} public static void LogWarning(object log,string text) {} } }

namespace MoveMoatTest {
 internal static class MoveMoatTestPlugin {
  internal static readonly SettingsStub Settings = new SettingsStub();
 }
 internal sealed class SettingsStub { internal bool EnableMod=true; internal int RouteMode; }
}
