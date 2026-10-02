using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AICoarsePathComponentFixTest
{
    // Read-only outcome audit. The plugin's static field roots this observer and its subscriptions.
    internal sealed unsafe class WoodGuardOutcomeObserver : IAivBuildStepObserver
    {
        private const int ScanInterval = 50;
        private const int EventLimit = 128;
        private const int DetailLimit = 48;
        private readonly ManualLogSource log;
        private readonly Func<WoodSiteGuardExperiment> guard;
        private readonly IDisposable started, ended, building, walls;
        private readonly Queue<string> pending = new Queue<string>();
        private readonly Dictionary<long, WallTile> wallPre = new Dictionary<long, WallTile>();
        private uint[] wallMap;
        private byte[] goodsYardMap;
        private long session;
        private int tick, nextScan, hutSpawns, farmSpawns;
        private int wallCalls, wallEventChanges, created, removed, altered, detailCount;
        private int goodsYardTiles, goodsYardAdded, goodsYardRemoved;
        private int omittedEvents, omittedDetails, failedReads;
        private bool active, ratSession, observeAivWalls, baselineReady, firstChangeLogged, initialBuildingsCaptured;
        private bool targetPlanCaptured, firstMaterializedCaptured;
        private int targetPlanAttempts, nextTargetPlanTick;
        private readonly ActiveWallStep[] activeWallSteps = new ActiveWallStep[9];
        private const int TargetWallX = 457, TargetWallY = 307;

        private sealed class ActiveWallStep : IAivBuildStepInvocation
        {
            internal readonly WoodGuardOutcomeObserver Owner;
            internal readonly int Player, Slot, Frame, Mapper, StateBefore, Variant, Rotation;
            internal readonly ActiveWallStep Previous;
            internal bool Materialized;

            internal ActiveWallStep(WoodGuardOutcomeObserver owner, int player, int slot, int frame,
                int mapper, int state, int variant, int rotation, ActiveWallStep previous)
            {
                Owner = owner; Player = player; Slot = slot; Frame = frame;
                Mapper = mapper; StateBefore = state; Variant = variant; Rotation = rotation;
                Previous = previous;
            }

            public void Complete(AivBuildStepCompletion completion)
            {
                try
                {
                    if (Materialized)
                    {
                        int after = -1;
                        if (GameAIVManagerAPI.Instance.TryGetBuildStep(Slot, Frame,
                            out AivBuildStep* step)) after = (int)step->State;
                        Owner.QueueImportant($"AI_WOOD_AIV_WALL_STEP_AFTER: session={Owner.session}; tick={Owner.tick}; " +
                            $"player={Player}; slot={Slot}; frame={Frame}; mapper={Mapper}; " +
                            $"stateBefore={StateBefore}; stateAfter={after}; variant={Variant}; " +
                            $"rotation={Rotation}; vanillaCompleted={completion.VanillaCompleted}; " +
                            $"vanillaResult={completion.VanillaResult}.");
                    }
                }
                catch (Exception ex) { Owner.RecordAivFailure("complete", ex); }
                finally { Owner.activeWallSteps[Player] = Previous; }
            }
        }

        private struct WallTile
        {
            internal int X, Y, TileId, Player, Type, Height, Building, Component;
            internal uint Flags;
            internal byte Owner;
            internal string Status;
        }

        internal WoodGuardOutcomeObserver(ManualLogSource logger,
            Func<WoodSiteGuardExperiment> guardProvider)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            guard = guardProvider ?? throw new ArgumentNullException(nameof(guardProvider));
            started = Shared.MissionEvents.Started.Subscribe(OnStarted);
            ended = Shared.MissionEvents.Ended.Subscribe(OnEnded);
            building = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn);
            walls = AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(OnWall);
            Shared.DebugLogHelper.LogInfo(log,
                "AI_WOOD_OUTCOME_READY: publisher=GameTimeManagerAPI.OnTick; " +
                "building=OnBuildingSpawn; wall=OnAIBuildWall+50-tick-map-scan; readOnly=True.");
        }

        private void OnStarted(MissionLifecycleNotification notification)
        {
            session++;
            active = true;
            ratSession = notification?.Context?.IsSave == true &&
                string.Equals(Path.GetFileName(notification.Context.FilePath),
                    "rat_wood_guard_control_probe.sav", StringComparison.OrdinalIgnoreCase);
            observeAivWalls = guard()?.IsActive ?? false;
            tick = -1;
            nextScan = 0;
            wallMap = null;
            goodsYardMap = null;
            goodsYardTiles = goodsYardAdded = goodsYardRemoved = 0;
            baselineReady = firstChangeLogged = initialBuildingsCaptured = false;
            targetPlanCaptured = !ratSession;
            firstMaterializedCaptured = false;
            targetPlanAttempts = nextTargetPlanTick = 0;
            Array.Clear(activeWallSteps, 0, activeWallSteps.Length);
            hutSpawns = farmSpawns = wallCalls = wallEventChanges = 0;
            created = removed = altered = detailCount = omittedEvents = omittedDetails = failedReads = 0;
            pending.Clear();
            wallPre.Clear();
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_WOOD_OUTCOME_SESSION: session={session}; file={notification?.Context?.FilePath}; " +
                $"guardActive={guard()?.IsActive ?? false}; aivWallObservation={observeAivWalls}; " +
                "firstMapScan=pending-first-tick.");
        }

        private void OnEnded(MissionLifecycleNotification notification)
        {
            if (!active) return;
            Flush();
            LogSummary("ended");
            active = false;
            ratSession = false;
            observeAivWalls = false;
            wallMap = null;
            goodsYardMap = null;
            wallPre.Clear();
            Array.Clear(activeWallSteps, 0, activeWallSteps.Length);
        }

        internal void OnTick(int currentTick)
        {
            if (!active) return;
            tick = currentTick;
            if (!targetPlanCaptured && currentTick >= nextTargetPlanTick) CaptureTargetPlan();
            if (!baselineReady)
            {
                if (!initialBuildingsCaptured) CaptureInitialBuildings();
                ScanWalls("initial", false);
                if (baselineReady) nextScan = currentTick + ScanInterval;
            }
            else if (currentTick >= nextScan)
            {
                ScanWalls("periodic", true);
                nextScan = currentTick + ScanInterval;
            }
            Flush();
            if (currentTick > 0 && currentTick % 500 == 0) LogSummary("tick");
        }

        private void CaptureInitialBuildings()
        {
            try
            {
                int[] huts = new int[9], farms = new int[9];
                Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding item = ref buildings[spanIndex];
                    int owner = item.r_PlayerIdOwner;
                    if (owner < 1 || owner > 8 || item.r_AliveState != AliveState.IsAlive) continue;
                    if (item.r_BuildingType == eStructs.STRUCT_WOODCUTTERS_HUT) huts[owner]++;
                    else if (item.r_BuildingType == eStructs.STRUCT_APPLEFARM) farms[owner]++;
                }
                var line = new StringBuilder($"AI_WOOD_OUTCOME_INITIAL_BUILDINGS: session={session}; tick={tick}");
                for (int player = 1; player <= 8; player++)
                    line.Append($"; p{player}=huts:{huts[player]},farms:{farms[player]}");
                Shared.DebugLogHelper.LogInfo(log, line.ToString() + ".");
                initialBuildingsCaptured = true;
            }
            catch (Exception ex) { Queue("AI_WOOD_OUTCOME_INITIAL_BUILDINGS_FAILED: " + ex.Message); }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0) return;
            string kind;
            if (args.Building == eStructs.STRUCT_WOODCUTTERS_HUT)
            { hutSpawns++; kind = "woodcutter"; }
            else if (args.Building == eStructs.STRUCT_APPLEFARM)
            { farmSpawns++; kind = "apple-farm"; }
            else return;
            Queue($"AI_WOOD_OUTCOME_SPAWN: session={session}; tick={tick}; player={args.PlayerId}; " +
                $"kind={kind}; buildingId={args.ReturnValue}; tile=({args.TileX},{args.TileY}); " +
                $"guardActive={guard()?.IsActive ?? false}.");
        }

        private void OnWall(AIBuildWallEventArgs args)
        {
            if (!active) return;
            long key = ((long)args.PlayerId << 32) | ((long)(ushort)args.TileX << 16) | (ushort)args.TileY;
            try
            {
                WallTile state = ReadWall(args.TileX, args.TileY, args.PlayerId);
                if (args.Phase == EventHookPhase.Pre)
                {
                    if (wallPre.Count < EventLimit) wallPre[key] = state;
                    else omittedEvents++;
                    return;
                }
                if (args.Phase != EventHookPhase.Post) return;
                wallCalls++;
                if (!wallPre.TryGetValue(key, out WallTile before)) return;
                wallPre.Remove(key);
                if (before.Status != "ok" || state.Status != "ok" ||
                    before.Flags == state.Flags && before.Owner == state.Owner &&
                    before.Type == state.Type && before.Height == state.Height &&
                    before.Building == state.Building && before.Component == state.Component) return;
                wallEventChanges++;
                bool materialized = !IsActualWall(before.Flags) && IsActualWall(state.Flags);
                if (observeAivWalls && materialized && (!firstMaterializedCaptured ||
                    ratSession && args.TileX == TargetWallX && args.TileY == TargetWallY))
                {
                    firstMaterializedCaptured = true;
                    CaptureMaterializedStep(args.PlayerId, state, before);
                }
                if (wallEventChanges <= DetailLimit)
                    Queue($"AI_WOOD_OUTCOME_WALL_EVENT_CHANGE: session={session}; tick={tick}; " +
                        $"player={args.PlayerId}; mapper={args.Mappers}; tile=({args.TileX},{args.TileY}); " +
                        $"before={Describe(before)}; after={Describe(state)}; " +
                        $"recentGuard={guard()?.DescribeRecentDecisions() ?? "none"}.");
                else omittedEvents++;
            }
            catch (Exception ex) { failedReads++; if (failedReads == 1) Queue("AI_WOOD_OUTCOME_WALL_EVENT_FAILED: " + ex.Message); }
        }

        public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
        {
            if (!active || !observeAivWalls || context.PlayerId < 1 || context.PlayerId > 8) return null;
            try
            {
                GameAIVManagerAPI api = GameAIVManagerAPI.Instance;
                if (!api.TryGetVillageSlotByPlayerId(context.PlayerId, out int slot) ||
                    !api.TryGetVillageByPlayerId(context.PlayerId, out AivVillageState* village) ||
                    !api.TryGetBuildStep(slot, context.FrameIndex, out AivBuildStep* step) ||
                    !GameAIVManagerAPI.UsesOrderedMapTileBuffer(step->BuildingType)) return null;
                var current = new ActiveWallStep(this, context.PlayerId, slot, context.FrameIndex,
                    (int)step->BuildingType, (int)step->State, village->SelectedVariantIndex,
                    (int)village->Rotation, activeWallSteps[context.PlayerId]);
                activeWallSteps[context.PlayerId] = current;
                return current;
            }
            catch (Exception ex) { RecordAivFailure("begin", ex); return null; }
        }

        private void CaptureTargetPlan()
        {
            try
            {
                targetPlanAttempts++;
                nextTargetPlanTick = tick + 50;
                GameAIVManagerAPI api = GameAIVManagerAPI.Instance;
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                if (!tiles.IsTileInsideMapBounds(TargetWallX, TargetWallY) ||
                    !api.TryGetVillageSlotByPlayerId(4, out int slot) ||
                    !api.TryGetVillageByPlayerId(4, out AivVillageState* village)) return;
                int targetId = tiles.GetTileId(TargetWallX, TargetWallY);
                var matches = new StringBuilder();
                int matchCount = 0, wallSteps = 0, invalidSteps = 0;
                Span<AivBuildStep> steps = api.GetBuildSteps(slot);
                int stepCount = village->MaximumBuildStep < 0 ? 0 :
                    village->MaximumBuildStep >= steps.Length - 1 ? steps.Length :
                    village->MaximumBuildStep + 1;
                for (int frame = 0; frame < stepCount; frame++)
                {
                    if (steps[frame].BuildingType != eMappers.MAPPER_WALL) continue;
                    wallSteps++;
                    Span<int> planned = api.GetBuildStepMapTiles(slot, frame);
                    if (planned.Length == 0) { invalidSteps++; continue; }
                    if (planned.IndexOf(targetId) >= 0)
                    {
                        if (matchCount++ != 0) matches.Append('/');
                        matches.Append(frame);
                    }
                }
                if (wallSteps == 0 && targetPlanAttempts < 3) return;
                QueueImportant($"AI_WOOD_AIV_TARGET_PLAN: session={session}; tick={tick}; player=4; " +
                    $"tile=({TargetWallX},{TargetWallY}); tileId={targetId}; slot={slot}; " +
                    $"variant={village->SelectedVariantIndex}; rotation={(int)village->Rotation}; " +
                    $"stepCount={stepCount}; wallSteps={wallSteps}; invalidSteps={invalidSteps}; " +
                    $"initialWall={IsActualWall((uint)tiles.GetTilePropertyFlag(targetId))}; " +
                    $"attempts={targetPlanAttempts}; " +
                    $"matchingFrames={matches}.");
                targetPlanCaptured = true;
            }
            catch (Exception ex) { RecordAivFailure("plan", ex); targetPlanCaptured = true; }
        }

        private void CaptureMaterializedStep(int player, WallTile after, WallTile before)
        {
            ActiveWallStep step = player >= 1 && player <= 8 ? activeWallSteps[player] : null;
            int plannedIndex = -1, plannedCount = -1, stateBefore = -1, frame = -1, slot = -1;
            int variant = -1, rotation = -1;
            if (step != null)
            {
                step.Materialized = true;
                frame = step.Frame; slot = step.Slot; stateBefore = step.StateBefore;
                variant = step.Variant; rotation = step.Rotation;
                try
                {
                    Span<int> planned = GameAIVManagerAPI.Instance.GetBuildStepMapTiles(slot, frame);
                    plannedCount = planned.Length;
                    plannedIndex = planned.IndexOf(after.TileId);
                }
                catch (Exception ex) { RecordAivFailure("materialized", ex); }
            }
            string recent = guard()?.DescribeRecentDecisions() ?? "none";
            QueueImportant($"AI_WOOD_AIV_WALL_MATERIALIZED: session={session}; tick={tick}; player={player}; " +
                $"tile=({after.X},{after.Y}); tileId={after.TileId}; " +
                $"aivContext={(step != null)}; slot={slot}; frame={frame}; stateBefore={stateBefore}; " +
                $"variant={variant}; rotation={rotation}; plannedCount={plannedCount}; " +
                $"plannedIndex={plannedIndex}; before={Describe(before)}; after={Describe(after)}; " +
                $"recentGuard={recent}.");
        }

        private void RecordAivFailure(string phase, Exception ex)
        {
            failedReads++;
            if (failedReads <= 3) Queue($"AI_WOOD_AIV_OBSERVATION_FAILED: phase={phase}; error={ex}");
        }

        private static WallTile ReadWall(int x, int y, int player)
        {
            var result = new WallTile { X = x, Y = y, Player = player, Status = "outside-map" };
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y)) return result;
            int id = tiles.GetTileId(x, y);
            Span<byte> owners = tiles.GetWallOwnerLayer();
            if (id <= 0 || (uint)id >= (uint)owners.Length)
            { result.Status = "invalid-tile-id"; return result; }
            result.TileId = id;
            result.Flags = (uint)tiles.GetTilePropertyFlag(id);
            result.Owner = owners[id];
            result.Type = (int)tiles.GetTileType(id);
            result.Height = tiles.GetTileHeight(id);
            result.Building = tiles.GetTileBuildingId(id);
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            result.Component = (uint)id < (uint)components.Length ? components[id] : -1;
            result.Status = "ok";
            return result;
        }

        private static string Describe(WallTile tile) =>
            $"status:{tile.Status},id:{tile.TileId},flags:0x{tile.Flags:X8}," +
            $"wall:{IsActualWall(tile.Flags)},goodsYard:{IsGoodsYard(tile.Flags)}," +
            $"owner:{tile.Owner},type:{tile.Type}," +
            $"height:{tile.Height},building:{tile.Building},component:{tile.Component}";

        private static bool IsGoodsYard(uint flags) =>
            (flags & (uint)TilePropertyFlag.GoodsyardRelated) != 0;

        private static bool IsActualWall(uint flags) =>
            (flags & (uint)TilePropertyFlag.IsWall) != 0 && !IsGoodsYard(flags);

        private void ScanWalls(string phase, bool compare)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                Span<byte> owners = tiles.GetWallOwnerLayer();
                if (owners.Length == 0) return;
                uint[] next = new uint[owners.Length];
                byte[] nextGoodsYards = new byte[owners.Length];
                int changes = 0;
                int currentGoodsYards = 0, addedGoodsYards = 0, removedGoodsYards = 0;
                for (int id = 1; id < next.Length; id++)
                {
                    uint flags = (uint)tiles.GetTilePropertyFlag(id);
                    bool goodsYard = IsGoodsYard(flags);
                    if (goodsYard)
                    {
                        nextGoodsYards[id] = 1;
                        currentGoodsYards++;
                    }
                    if (compare && goodsYardMap != null && id < goodsYardMap.Length)
                    {
                        if (goodsYardMap[id] == 0 && goodsYard) addedGoodsYards++;
                        else if (goodsYardMap[id] != 0 && !goodsYard) removedGoodsYards++;
                    }
                    uint present = IsActualWall(flags) ? (uint)TilePropertyFlag.IsWall : 0;
                    next[id] = present == 0 ? 0 : present | owners[id];
                    if (!compare || wallMap == null || id >= wallMap.Length || next[id] == wallMap[id]) continue;
                    changes++;
                    uint prior = wallMap[id];
                    string kind;
                    if ((prior & 0x100u) == 0) { created++; kind = "created"; }
                    else if (present == 0) { removed++; kind = "removed"; }
                    else { altered++; kind = "owner-changed"; }
                    if (detailCount < DetailLimit || !firstChangeLogged)
                    {
                        int y = tiles.MapColumnLookupTable[id];
                        int x = id - tiles.MapRowLookupTable[3 * y];
                        Shared.DebugLogHelper.LogInfo(log,
                            $"AI_WOOD_OUTCOME_WALL_MAP_CHANGE: session={session}; tick={tick}; " +
                            $"tileId={id}; tile=({x},{y}); kind={kind}; " +
                            $"before=0x{prior:X3}; after=0x{next[id]:X3}; " +
                            $"flags=0x{(uint)tiles.GetTilePropertyFlag(id):X8}; " +
                            $"building={tiles.GetTileBuildingId(id)}; height={tiles.GetTileHeight(id)}; " +
                            $"recentGuard={guard()?.DescribeRecentDecisions() ?? "none"}.");
                        firstChangeLogged = true;
                        detailCount++;
                    }
                    else omittedDetails++;
                }
                wallMap = next;
                goodsYardMap = nextGoodsYards;
                goodsYardTiles = currentGoodsYards;
                goodsYardAdded += addedGoodsYards;
                goodsYardRemoved += removedGoodsYards;
                baselineReady = true;
                if (phase == "initial" || changes != 0 || addedGoodsYards != 0 || removedGoodsYards != 0)
                    Queue($"AI_WOOD_OUTCOME_WALL_SCAN: session={session}; tick={tick}; phase={phase}; " +
                        $"tiles={next.Length}; changes={changes}; created={created}; removed={removed}; " +
                        $"altered={altered}; goodsYardTiles={goodsYardTiles}; " +
                        $"goodsYardAdded={addedGoodsYards}; goodsYardRemoved={removedGoodsYards}; " +
                        $"omittedDetails={omittedDetails}; goodsYardIsNotWall=true.");
            }
            catch (Exception ex) { failedReads++; if (failedReads == 1) Queue("AI_WOOD_OUTCOME_WALL_SCAN_FAILED: " + ex.Message); }
        }

        private void Queue(string line)
        {
            if (pending.Count < EventLimit) pending.Enqueue(line);
            else omittedEvents++;
        }

        private void QueueImportant(string line)
        {
            if (pending.Count == EventLimit) { pending.Dequeue(); omittedEvents++; }
            pending.Enqueue(line);
        }

        private void Flush()
        {
            while (pending.Count != 0) Shared.DebugLogHelper.LogInfo(log, pending.Dequeue());
        }

        private void LogSummary(string phase) => Shared.DebugLogHelper.LogInfo(log,
            $"AI_WOOD_OUTCOME_STATUS: session={session}; phase={phase}; tick={tick}; " +
            $"guardActive={guard()?.IsActive ?? false}; baselineReady={baselineReady}; " +
            $"hutSpawns={hutSpawns}; farmSpawns={farmSpawns}; wallCalls={wallCalls}; " +
            $"wallEventChanges={wallEventChanges}; wallCreated={created}; wallRemoved={removed}; " +
            $"wallAltered={altered}; goodsYardTiles={goodsYardTiles}; " +
            $"goodsYardAdded={goodsYardAdded}; goodsYardRemoved={goodsYardRemoved}; " +
            $"omittedEvents={omittedEvents}; " +
            $"omittedWallDetails={omittedDetails}; failedReads={failedReads}.");
    }
}
