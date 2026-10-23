using VanillaFootprintCandidate = APIShared.GatehouseFootprintCandidate;
// Feature: Prevent unreachable enemies from closing protected inner gatehouses.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class ReachableEnemyGatehouseRuntime : IDisposable
    {
        private const int MaximumFailureLogs = 20;
        private const int MaximumSaneFootprintSize = 512;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<ReachabilityKey, bool> reachabilityCache =
            new Dictionary<ReachabilityKey, bool>();
        // Entry/exit-only results per tick (key signature 0); drawbridges are not involved.
        private readonly Dictionary<ReachabilityKey, bool> directReachabilityCache =
            new Dictionary<ReachabilityKey, bool>();
        private IDisposable gatehouseQuerySubscription;
        private bool reachabilityAvailable;
        private int lastCacheTick = int.MinValue;
        private int failureLogs;

        public ReachableEnemyGatehouseRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        partial void DiagInitialize();
        public void Initialize()
        {
            DiagInitialize();
            if (gatehouseQuerySubscription != null)
                return;

            // The installed manifest-selected Script Extender supplies a one-based Unit game ID.
            // The shared adapter validates it without conversion.
            gatehouseQuerySubscription = BuildingR3EventHooks.OnGatehouseQuery.Observable
                .Subscribe(OnGatehouseQuery);
        }

        public void SetNativeCompatibility(bool referenceHashMatches)
        {
            reachabilityAvailable = referenceHashMatches;
            ClearCache();
            if (!referenceHashMatches)
            {
                LogWarning(
                    "gatehouse PCL reachability filtering is unavailable because the installed DLL " +
                    "differs from the audited build; Vanilla candidate handling remains active.");
            }
        }

        public void Dispose()
        {
            gatehouseQuerySubscription?.Dispose();
            gatehouseQuerySubscription = null;
            ClearCache();
        }

        private void OnGatehouseQuery(GatehouseQueryEventArgs args)
        {
            if (!settings.EnableMod || !settings.RequireReachableEnemyForAutomaticGateClosing ||
                !reachabilityAvailable || args == null)
            {
                return;
            }

#if SERP_DEVDIAG
            GatehouseDiagnosticRuntime.QueryScope queryStarted = GatehouseDiagnosticRuntime.QueryStart();
#endif
            try
            {
                if (!TryGetPassGatehouse(
                        args.BuildingId,
                        out GameBuilding* building,
                        out PathConnectionRecord* gatehouse))
                {
                    return;
                }

                int candidateUnitId = args.UnitId;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                        candidateUnitId,
                        units.Length,
                        out int unitId) ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null)
                {
                    return;
                }

                bool vanillaCandidateCanClose =
                    APIShared.UnitAccess.IsReallyAlive(unit) &&
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_LION &&
                    unit->r_ControllableForPlayerId != 0;
                args.ShouldClose = Shared.GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(
                    args.ShouldClose,
                    vanillaCandidateCanClose);
                if (args.ShouldClose != true)
                    return;
                // Vanilla ignores the answer for units outside its closing range; the
                // reachability evaluation could not change the gate there.
                if (!IsWithinVanillaCloseRange(unit, building))
                {
#if SERP_DEVDIAG
                    GatehouseDiagnosticRuntime.Current.RangeSkips++;
#endif
                    return;
                }

                bool evaluationAvailable = TryIsUnitReachableToGate(
                    unitId,
                    unit,
                    args.BuildingId,
                    building,
                    gatehouse,
                    out bool reachable);
                if (evaluationAvailable && !reachable)
                    args.ShouldClose = false;
            }
            catch (Exception ex)
            {
                LogFailure(
                    $"gatehouse reachability query failed: buildingId={args.BuildingId}, " +
                    $"eventUnitId={args.UnitId}, error={ex}");
            }
#if SERP_DEVDIAG
            finally { GatehouseDiagnosticRuntime.Query(args.BuildingId, args.UnitId, queryStarted); }
#endif
        }

        private bool TryIsUnitReachableToGate(
            int unitId,
            GameUnit* unit,
            int gatehouseBuildingId,
            GameBuilding* gatehouseBuilding,
            PathConnectionRecord* gatehouse,
            out bool reachable)
        {
            reachable = true;
            if (unitId <= 0 || gatehouseBuildingId <= 0 || gatehouseBuilding == null ||
                gatehouse == null || unit == null ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_CurrentHealth == 0 ||
                unit->r_ControllableForPlayerId <= 0)
            {
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            GamePathingManagerAPI pathingApi = GamePathingManagerAPI.Instance;
            int sourceTileId = (int)unit->r_CurrentPositionTileId;
            int entryTileId = (int)gatehouse->r_EntryTileId;
            int exitTileId = (int)gatehouse->r_ExitTileId;
            if (!tileApi.IsValidTileId(sourceTileId) ||
                !HaveValidGateEndpoints(gatehouseBuildingId, gatehouse, entryTileId, exitTileId, tileApi))
            {
                return false;
            }

            Span<ushort> pathConnections = pathingApi.GetPathComponentGrid();
            if ((uint)sourceTileId >= (uint)pathConnections.Length ||
                (uint)entryTileId >= (uint)pathConnections.Length ||
                (uint)exitTileId >= (uint)pathConnections.Length)
            {
                return false;
            }

            int tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            if (tick != lastCacheTick)
            {
                reachabilityCache.Clear();
                directReachabilityCache.Clear();
                lastCacheTick = tick;
            }

            int playerId = unit->r_ControllableForPlayerId;
            int sourcePcl = pathConnections[sourceTileId];
            int entryPcl = pathConnections[entryTileId];
            int exitPcl = pathConnections[exitTileId];
            int mode = unit->r_PathConnectionMode;
            if (sourcePcl <= 0 || entryPcl <= 0 || exitPcl <= 0)
                return false;

#if SERP_DEVDIAG
            var diagnostics = GatehouseDiagnosticRuntime.Current;
            GatehouseDiagnosticRuntime.Context(tick, playerId, sourcePcl, entryPcl, exitPcl, mode);
#endif
            PathConnectionQueryMode queryMode = (PathConnectionQueryMode)mode;
            // A reachable entry or exit already decides "reachable" (the caller then keeps
            // Vanilla's close decision, exactly as when the drawbridge evaluation is unavailable).
            // Only an unreachable gatehouse needs the synchronized drawbridge approaches, whose
            // footprint scan dominated this query's cost.
            var directKey = new ReachabilityKey(playerId, sourcePcl, entryPcl, exitPcl, mode, 0);
            if (!directReachabilityCache.TryGetValue(directKey, out bool directlyReachable))
            {
#if SERP_DEVDIAG
                diagnostics.DirectMisses++;
#endif
                directlyReachable = FindRoute(pathingApi, playerId, sourcePcl, entryPcl, queryMode) != 0 ||
                    FindRoute(pathingApi, playerId, sourcePcl, exitPcl, queryMode) != 0;
                directReachabilityCache[directKey] = directlyReachable;
            }
#if SERP_DEVDIAG
            else diagnostics.DirectHits++;
#endif
            if (directlyReachable)
            {
                reachable = true;
                return true;
            }

            if (!TryGetPassDrawbridges(
                    gatehouseBuildingId,
                    gatehouseBuilding,
                    tileApi,
                    out IReadOnlyList<SynchronizedDrawbridgeSnapshot> drawbridges,
                    out int synchronizedGroupSignature))
            {
                return false;
            }

            var key = new ReachabilityKey(
                playerId,
                sourcePcl,
                entryPcl,
                exitPcl,
                mode,
                synchronizedGroupSignature);
            if (reachabilityCache.TryGetValue(key, out reachable))
            {
#if SERP_DEVDIAG
                diagnostics.FullHits++;
#endif
                return true;
            }
#if SERP_DEVDIAG
            diagnostics.FullMisses++;
#endif

            // Entry and exit are known to be unreachable here (checked above).
            reachable = false;
            if (drawbridges.Count > 0)
            {
#if SERP_DEVDIAG
                long exteriorStarted = GatehouseDiagnosticRuntime.Start();
                try
                {
#endif
                reachable = CanReachDrawbridge(drawbridges, pathingApi, playerId, sourcePcl, queryMode);
#if SERP_DEVDIAG
                }
                finally { diagnostics.ExteriorTicks += System.Diagnostics.Stopwatch.GetTimestamp() - exteriorStarted; }
#endif
            }

            reachabilityCache[key] = reachable;
            return true;
        }

        // Vanilla raises this query for every unit of a player list in one gate pass (0xB73D0
        // loop; native audit FBCB9319: Native-Baseline knowledge/GATEHOUSE_QUERY_PASS.md):
        // nothing between two queries of that pass writes buildings, path connection records,
        // structure or component grids, and inside one EngineInterface.run every gate has at most
        // one pass. The gatehouse lookup (linear scan of all 199 connection records) and the
        // drawbridge collection are therefore taken once per gate pass. Outside a marked run
        // (map loading, editor) every query evaluates freshly as before.
        private long passRun;
        private int passBuildingId;
        private bool passGatehouseFound, passDrawbridgesCollected, passDrawbridgesAvailable;
        private GameBuilding* passBuilding;
        private PathConnectionRecord* passGatehouse;
        private IReadOnlyList<SynchronizedDrawbridgeSnapshot> passDrawbridges =
            Array.Empty<SynchronizedDrawbridgeSnapshot>();
        private int passDrawbridgeSignature;
        private int passEndpoints; // 0 = not evaluated in this pass, 1 = valid, 2 = invalid

        private bool TryGetPassGatehouse(
            int buildingId,
            out GameBuilding* building,
            out PathConnectionRecord* gatehouse)
        {
            long run = SimulationRunScope.Current;
            if (run != 0 && run == passRun && buildingId == passBuildingId)
            {
                if (!passGatehouseFound)
                {
#if SERP_DEVDIAG
                    GatehouseDiagnosticRuntime.Current.PassHits++;
                    if (TryGetLiveGatehouse(buildingId, out _, out _))
                        GatehouseDiagnosticRuntime.Current.PassMismatches++;
#endif
                    building = null;
                    gatehouse = null;
                    return false;
                }
                // Cheap re-check of the remembered identities; a foreign subscriber that changed
                // them would get the full lookup again.
                building = passBuilding;
                gatehouse = passGatehouse;
                if (building->r_AliveState == AliveState.IsAlive && gatehouse->r_IsActive != 0 &&
                    gatehouse->r_BuildingId == buildingId &&
                    gatehouse->r_SubjectGlobalId == building->r_GlobalId)
                {
#if SERP_DEVDIAG
                    // In-game evidence for the pass contract: the fresh lookup must agree.
                    GatehouseDiagnosticRuntime.Current.PassHits++;
                    if (!TryGetLiveGatehouse(buildingId, out GameBuilding* freshBuilding, out PathConnectionRecord* freshGatehouse) ||
                        freshBuilding != building || freshGatehouse != gatehouse)
                        GatehouseDiagnosticRuntime.Current.PassMismatches++;
#endif
                    return true;
                }
            }

            bool found = TryGetLiveGatehouse(buildingId, out building, out gatehouse);
            // Only the thread inside a marked run owns the pass state; other callers never touch it.
            if (run == 0)
                return found;
            passRun = run;
            passBuildingId = buildingId;
            passGatehouseFound = found;
            passBuilding = found ? building : null;
            passGatehouse = found ? gatehouse : null;
            passDrawbridgesCollected = false;
            passDrawbridges = Array.Empty<SynchronizedDrawbridgeSnapshot>();
            passEndpoints = 0;
            return found;
        }

        // Native gate loop 0xB7B70..0xB7BD7 (FBCB9319): after this query Vanilla closes only for a unit
        // whose Chebyshev distance max(|8*X - unitX|, |8*Y - unitY|) between the gate origin tile and the
        // unit's fine world position is below 200 (below 140 when its siege flag is set). The fields
        // are read as the native signed words; any distance of at least 200 never closes the gate.
        // The rule is shared with ExtraFeatures, which skips the same queries.
        private static bool IsWithinVanillaCloseRange(GameUnit* unit, GameBuilding* gatehouse) =>
            Shared.GatehouseQueryUnitIdPolicy.IsWithinVanillaCloseRange(
                (short)gatehouse->r_TilePositionXBegin,
                (short)gatehouse->r_TilePositionYBegin,
                (short)unit->r_CurrentWorldPositionX,
                (short)unit->r_CurrentWorldPositionY);

        // Entry/exit validity depends only on the gate record and the map bounds.
        private bool HaveValidGateEndpoints(int gatehouseBuildingId, PathConnectionRecord* gatehouse,
            int entryTileId, int exitTileId, GameTileManagerAPI tileApi)
        {
            long run = SimulationRunScope.Current;
            bool pass = run != 0 && run == passRun && gatehouseBuildingId == passBuildingId &&
                gatehouse == passGatehouse;
            if (pass && passEndpoints != 0)
                return passEndpoints == 1;
            bool valid = tileApi.IsValidTileId(entryTileId) && tileApi.IsValidTileId(exitTileId) &&
                TryValidateGateEndpoint(gatehouse->r_EntryTilePositionX, gatehouse->r_EntryTilePositionY,
                    entryTileId, tileApi) &&
                TryValidateGateEndpoint(gatehouse->r_ExitTilePositionX, gatehouse->r_ExitTilePositionY,
                    exitTileId, tileApi);
            if (pass)
                passEndpoints = valid ? 1 : 2;
            return valid;
        }

        private bool TryGetPassDrawbridges(
            int gatehouseBuildingId,
            GameBuilding* gatehouseBuilding,
            GameTileManagerAPI tileApi,
            out IReadOnlyList<SynchronizedDrawbridgeSnapshot> drawbridges,
            out int signature)
        {
            long run = SimulationRunScope.Current;
            // TryGetPassGatehouse opened the pass state for exactly this gatehouse in this query.
            bool pass = run != 0 && run == passRun && gatehouseBuildingId == passBuildingId &&
                gatehouseBuilding == passBuilding;
            if (pass && passDrawbridgesCollected)
            {
#if SERP_DEVDIAG
                if (TryCollectSynchronizedDrawbridges(gatehouseBuilding, tileApi, out var freshBridges, out int freshSignature) !=
                        passDrawbridgesAvailable || freshSignature != passDrawbridgeSignature || freshBridges.Count != passDrawbridges.Count)
                    GatehouseDiagnosticRuntime.Current.PassMismatches++;
#endif
                drawbridges = passDrawbridges;
                signature = passDrawbridgeSignature;
                return passDrawbridgesAvailable;
            }

            bool available = TryCollectSynchronizedDrawbridges(
                gatehouseBuilding, tileApi, out drawbridges, out signature);
            if (pass)
            {
                passDrawbridgesCollected = true;
                passDrawbridgesAvailable = available;
                passDrawbridges = drawbridges;
                passDrawbridgeSignature = signature;
            }
            return available;
        }

        private static bool TryCollectSynchronizedDrawbridges(
            GameBuilding* gatehouseBuilding,
            GameTileManagerAPI tileApi,
            out IReadOnlyList<SynchronizedDrawbridgeSnapshot> drawbridges,
            out int signature)
        {
#if SERP_DEVDIAG
            var diagnostics = GatehouseDiagnosticRuntime.Current;
            long collectStarted = GatehouseDiagnosticRuntime.Start();
            try
            {
#endif
            drawbridges = Array.Empty<SynchronizedDrawbridgeSnapshot>();
            signature = 17;
            if (gatehouseBuilding == null || gatehouseBuilding->r_OccupyTileGridSize == 0 ||
                gatehouseBuilding->r_OccupyTileGridSize > MaximumSaneFootprintSize)
                return false;

            IReadOnlyList<VanillaFootprintCandidate> candidates =
                GatehouseFootprintGeometryCache.Get(
                    gatehouseBuilding->r_TilePositionXBegin,
                    gatehouseBuilding->r_TilePositionYBegin,
                    (int)gatehouseBuilding->r_OccupyTileGridSize);
#if SERP_DEVDIAG
            diagnostics.FootprintScans++;
            diagnostics.Candidates += candidates.Count;
#endif
            // Same first-distinct selection as APIShared, with two value slots instead
            // of per-query closures and a List. Live reads and their order stay unchanged.
            // Vanilla's gate loop raises this query for nearly every unit on the map (~4000 per
            // gate and pass), so the scan reads the live structure layer once instead of four
            // Extender calls per tile, and a building ID already found not to be a live drawbridge
            // is not looked up again within this scan (both are pure reads of unchanged state).
            Span<ushort> structures = tileApi.GetStructureLayer();
            int first = 0, second = 0, rejected = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
#if SERP_DEVDIAG
                diagnostics.LayerProbes++;
#endif
                int id = 0;
                if (tileApi.IsTileInsideMapBounds(c.X, c.Y))
                {
                    int tile = tileApi.GetTileId(c.X, c.Y);
                    // The layer spans exactly the Extender's valid packed tile IDs.
                    if ((uint)tile < (uint)structures.Length) id = structures[tile];
                }
                if (id <= 0 || id == first || id == second || id == rejected) continue;
                if (!TryGetLiveDrawbridge(id, out _)) { rejected = id; continue; }
                if (first == 0) first = id; else { second = id; break; }
            }
            if (first == 0)
            {
                signature = SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(drawbridges);
                return true;
            }
            return CollectSelectedDrawbridges(candidates, tileApi, first, second, out drawbridges, out signature);
#if SERP_DEVDIAG
            }
            finally { diagnostics.CollectTicks += System.Diagnostics.Stopwatch.GetTimestamp() - collectStarted; }
#endif
        }

        private static int CollectionBuildingId(GameTileManagerAPI tileApi, int x, int y
#if SERP_DEVDIAG
            , GatehouseDiagnosticTotals diagnostics
#endif
            )
        {
#if SERP_DEVDIAG
            diagnostics.CollectionTileProbes++;
#endif
            if (!tileApi.IsTileInsideMapBounds(x, y)) return 0;
            int tile = tileApi.GetTileId(x, y);
            return tileApi.IsValidTileId(tile) ? tileApi.GetTileBuildingId(tile) : 0;
        }

        // Closure state is allocated only after a bridge has actually been found.
        private static bool CollectSelectedDrawbridges(IReadOnlyList<VanillaFootprintCandidate> candidates,
            GameTileManagerAPI tileApi, int first, int second,
            out IReadOnlyList<SynchronizedDrawbridgeSnapshot> drawbridges, out int signature)
        {
            signature = 17;
#if SERP_DEVDIAG
            var diagnostics = GatehouseDiagnosticRuntime.Current;
#endif
            int GetBuildingIdAt(int x, int y) => CollectionBuildingId(tileApi, x, y
#if SERP_DEVDIAG
                , diagnostics
#endif
                );
            var collected = new List<SynchronizedDrawbridgeSnapshot>(
                SynchronizedGatehouseReachabilityPolicy.MaximumSynchronizedDrawbridges);
            drawbridges = collected;

            for (int selectedIndex = 0; selectedIndex < (second == 0 ? 1 : 2); selectedIndex++)
            {
                int buildingId = selectedIndex == 0 ? first : second;
                if (!TryGetLiveDrawbridge(buildingId, out GameBuilding* drawbridge) ||
                    drawbridge == null || drawbridge->r_GlobalId == 0)
                    return false;

                var snapshot = new SynchronizedDrawbridgeSnapshot(
                    buildingId,
                    drawbridge->r_GlobalId);
                var seenApproaches = new HashSet<long>();
                bool foundContact = false;
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    VanillaFootprintCandidate candidate = candidates[candidateIndex];
                    if (GetBuildingIdAt(candidate.X, candidate.Y) != buildingId)
                        continue;

                    foundContact = true;
                    bool traced;
                    DrawbridgeApproachSnapshot approach;
#if SERP_DEVDIAG
                    long traceStarted = GatehouseDiagnosticRuntime.Start();
                    try
                    {
#endif
                    traced = SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                        candidate,
                        buildingId,
                        tileApi.IsTileInsideMapBounds,
                        GetBuildingIdAt,
                        tileApi.GetTileId,
                        tileId =>
                        {
                            Span<ushort> currentComponents =
                                GamePathingManagerAPI.Instance.GetPathComponentGrid();
                            return (uint)tileId < (uint)currentComponents.Length
                                ? currentComponents[tileId]
                                : 0;
                        },
                        out approach);
#if SERP_DEVDIAG
                    }
                    finally
                    {
                        diagnostics.TraceCalls++;
                        diagnostics.TraceTicks += System.Diagnostics.Stopwatch.GetTimestamp() - traceStarted;
                    }
#endif
                    if (!traced)
                        continue;

                    long approachKey = ((long)(uint)approach.ExteriorTileId << 32) |
                        (uint)approach.ExteriorPcl;
                    if (seenApproaches.Add(approachKey))
                        snapshot.Approaches.Add(approach);
                }

                if (!foundContact || snapshot.Approaches.Count == 0)
                    return false;

                collected.Add(snapshot);
#if SERP_DEVDIAG
                diagnostics.Bridges++;
                diagnostics.Approaches += snapshot.Approaches.Count;
#endif
            }

            signature = SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                drawbridges);
            return true;
        }

        private static bool CanReachDrawbridge(IReadOnlyList<SynchronizedDrawbridgeSnapshot> bridges,
            GamePathingManagerAPI pathing, int player, int source, PathConnectionQueryMode mode)
        {
            if (bridges.Count > SynchronizedGatehouseReachabilityPolicy.MaximumSynchronizedDrawbridges) return false;
            for (int b = 0; b < bridges.Count; b++)
                for (int a = 0; a < bridges[b].Approaches.Count; a++)
                    if (FindRoute(pathing, player, source, bridges[b].Approaches[a].ExteriorPcl, mode) != 0) return true;
            return false;
        }

        private static int FindRoute(
            GamePathingManagerAPI pathingApi,
            int playerId,
            int sourcePcl,
            int destinationPcl,
            PathConnectionQueryMode queryMode)
        {
#if SERP_DEVDIAG
            var diagnostics = GatehouseDiagnosticRuntime.Current;
            long routeStarted = GatehouseDiagnosticRuntime.Start();
            try
            {
#endif
            return sourcePcl == destinationPcl
                ? sourcePcl
                : pathingApi.FindNextComponentTowardDestination(
                    playerId,
                    sourcePcl,
                    destinationPcl,
                    queryMode);
#if SERP_DEVDIAG
            }
            finally
            {
                if (sourcePcl != destinationPcl) { diagnostics.RouteCalls++; diagnostics.RouteTicks += System.Diagnostics.Stopwatch.GetTimestamp() - routeStarted; }
            }
#endif
        }

        private static bool TryGetLiveDrawbridge(
            int buildingId,
            out GameBuilding* drawbridge)
        {
            drawbridge = null;
            return buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out drawbridge) &&
                drawbridge != null && drawbridge->r_AliveState == AliveState.IsAlive &&
                drawbridge->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE;
        }

        private static bool TryValidateGateEndpoint(
            int x,
            int y,
            int expectedTileId,
            GameTileManagerAPI tileApi) =>
            tileApi.IsTileInsideMapBounds(x, y) &&
            tileApi.GetTileId(x, y) == expectedTileId;

        private static bool TryGetLiveGatehouse(
            int buildingId,
            out GameBuilding* building,
            out PathConnectionRecord* gatehouse)
        {
            building = null;
            gatehouse = null;
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            return buildingId > 0 &&
                api.TryGetBuildingById(buildingId, out building) && building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(
                    buildingId,
                    out gatehouse) &&
                gatehouse != null && gatehouse->r_BuildingId == buildingId &&
                gatehouse->r_SubjectGlobalId == building->r_GlobalId;
        }

        private void ClearCache()
        {
            reachabilityCache.Clear();
            directReachabilityCache.Clear();
            lastCacheTick = int.MinValue;
            passRun = 0;
        }

        private void LogFailure(string message)
        {
            if (failureLogs >= MaximumFailureLogs)
                return;
            failureLogs++;
            LogWarning($"{message}. Vanilla remains authoritative ({failureLogs}/{MaximumFailureLogs}).");
        }

        private void LogWarning(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) =>
            Shared.DebugLogHelper.LogWarningLimited(log, callerFile + ":" + callerLine, message);
        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private readonly struct ReachabilityKey : IEquatable<ReachabilityKey>
        {
            public ReachabilityKey(
                int playerId,
                int sourcePcl,
                int entryPcl,
                int exitPcl,
                int mode,
                int synchronizedGroupSignature)
            {
                PlayerId = playerId;
                SourcePcl = sourcePcl;
                EntryPcl = entryPcl;
                ExitPcl = exitPcl;
                Mode = mode;
                SynchronizedGroupSignature = synchronizedGroupSignature;
            }

            private int PlayerId { get; }
            private int SourcePcl { get; }
            private int EntryPcl { get; }
            private int ExitPcl { get; }
            private int Mode { get; }
            private int SynchronizedGroupSignature { get; }

            public bool Equals(ReachabilityKey other) =>
                PlayerId == other.PlayerId && SourcePcl == other.SourcePcl &&
                EntryPcl == other.EntryPcl && ExitPcl == other.ExitPcl &&
                Mode == other.Mode &&
                SynchronizedGroupSignature == other.SynchronizedGroupSignature;
            public override bool Equals(object obj) => obj is ReachabilityKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = hash * 397 ^ SourcePcl;
                    hash = hash * 397 ^ EntryPcl;
                    hash = hash * 397 ^ ExitPcl;
                    hash = hash * 397 ^ Mode;
                    return hash * 397 ^ SynchronizedGroupSignature;
                }
            }
        }
    }
}
