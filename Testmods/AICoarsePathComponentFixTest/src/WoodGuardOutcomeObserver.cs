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
using System.Text;

namespace AICoarsePathComponentFixTest
{
    // Read-only outcome audit. The plugin's static field roots this observer and its subscriptions.
    internal sealed unsafe class WoodGuardOutcomeObserver
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
        private long session;
        private int tick, nextScan, hutSpawns, farmSpawns;
        private int wallCalls, wallEventChanges, created, removed, altered, detailCount;
        private int omittedEvents, omittedDetails, failedReads;
        private bool active, baselineReady, firstChangeLogged, initialBuildingsCaptured;

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
            tick = -1;
            nextScan = 0;
            wallMap = null;
            baselineReady = firstChangeLogged = initialBuildingsCaptured = false;
            hutSpawns = farmSpawns = wallCalls = wallEventChanges = 0;
            created = removed = altered = detailCount = omittedEvents = omittedDetails = failedReads = 0;
            pending.Clear();
            wallPre.Clear();
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_WOOD_OUTCOME_SESSION: session={session}; file={notification?.Context?.FilePath}; " +
                $"guardActive={guard()?.IsActive ?? false}; firstMapScan=pending-first-tick.");
        }

        private void OnEnded(MissionLifecycleNotification notification)
        {
            if (!active) return;
            Flush();
            LogSummary("ended");
            active = false;
            wallMap = null;
            wallPre.Clear();
        }

        internal void OnTick(int currentTick)
        {
            if (!active) return;
            tick = currentTick;
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
                if (wallEventChanges <= DetailLimit)
                    Queue($"AI_WOOD_OUTCOME_WALL_EVENT_CHANGE: session={session}; tick={tick}; " +
                        $"player={args.PlayerId}; mapper={args.Mappers}; tile=({args.TileX},{args.TileY}); " +
                        $"before={Describe(before)}; after={Describe(state)}; " +
                        $"recentGuard={guard()?.DescribeRecentDecisions() ?? "none"}.");
                else omittedEvents++;
            }
            catch (Exception ex) { failedReads++; if (failedReads == 1) Queue("AI_WOOD_OUTCOME_WALL_EVENT_FAILED: " + ex.Message); }
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
            $"wall:{(tile.Flags & 0x100u) != 0},owner:{tile.Owner},type:{tile.Type}," +
            $"height:{tile.Height},building:{tile.Building},component:{tile.Component}";

        private void ScanWalls(string phase, bool compare)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                Span<byte> owners = tiles.GetWallOwnerLayer();
                if (owners.Length == 0) return;
                uint[] next = new uint[owners.Length];
                int changes = 0;
                for (int id = 1; id < next.Length; id++)
                {
                    uint present = ((uint)tiles.GetTilePropertyFlag(id) & 0x100u) != 0 ? 0x100u : 0;
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
                baselineReady = true;
                if (phase == "initial" || changes != 0)
                    Queue($"AI_WOOD_OUTCOME_WALL_SCAN: session={session}; tick={tick}; phase={phase}; " +
                        $"tiles={next.Length}; changes={changes}; created={created}; removed={removed}; " +
                        $"altered={altered}; omittedDetails={omittedDetails}.");
            }
            catch (Exception ex) { failedReads++; if (failedReads == 1) Queue("AI_WOOD_OUTCOME_WALL_SCAN_FAILED: " + ex.Message); }
        }

        private void Queue(string line)
        {
            if (pending.Count < EventLimit) pending.Enqueue(line);
            else omittedEvents++;
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
            $"wallAltered={altered}; omittedEvents={omittedEvents}; " +
            $"omittedWallDetails={omittedDetails}; failedReads={failedReads}.");
    }
}
