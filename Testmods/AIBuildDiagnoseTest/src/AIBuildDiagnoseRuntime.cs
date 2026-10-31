using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe class AIBuildDiagnoseRuntime : IAivBuildStepObserver
    {
        private readonly ManualLogSource log;
        private EconomyDiagnostics economy;
        private GeneralSiteSearchHooks generalSearchHooks;
        private readonly ulong nativeModuleBase;
        private readonly bool fixesLoaded;
        private readonly IDisposable buildingSubscription, buildStructureSubscription, wallSubscription, loadingSubscription;
        private readonly Dictionary<string, int> seen = new Dictionary<string, int>();
        private readonly int[] schedulerCalls = new int[9];
        private readonly int[] woodBuildCalls = new int[9];
        private readonly string[] lastObservedStage = new string[9];
        private readonly int[] activeFarmSearchType = new int[9];
        private readonly bool[] farmSearchInCall = new bool[9];
        private readonly Dictionary<string, int> genericBuildEvents = new Dictionary<string, int>();
        private readonly Dictionary<string, int> genericSpawnEvents = new Dictionary<string, int>();
        private readonly Dictionary<int, string> genericSpawnById = new Dictionary<int, string>();
        private readonly List<GenericBuildFrame> genericBuildFrames = new List<GenericBuildFrame>();
        private sealed class GenericBuildFrame
        {
            internal int Player, X, Y, Tick, SpawnCount;
            internal eMappers Mapper;
            internal string BeforeSite, Search, Route, Source, SpawnTypes;
        }
        private readonly Dictionary<string, int> genericOutcomeCounts = new Dictionary<string, int>();
        private readonly string[] recentGenericSearch = new string[9];
        private readonly string[] recentGenericRoute = new string[9];
        private readonly int[] recentGenericSearchTick = new int[9];
        private readonly int[] recentGenericRouteTick = new int[9];
        private readonly uint[] resourceSearchGenerationBefore = new uint[9];
        private int unattributedSiteSelections, genericFrameOverflow;
        private int genericSpawnIdOverflow;
        private readonly Dictionary<int, string> aivWallPlanByTile = new Dictionary<int, string>();
        private bool aivWallPlanCaptured;
        private int aivWallPlanAttempts, aivWallPlanOverflow;
        private readonly Dictionary<string, WallObservation> pendingWalls = new Dictionary<string, WallObservation>();
        private readonly ActiveAivStep[] activeAivSteps = new ActiveAivStep[9];
        private readonly int[] materializedWalls = new int[9];
        private const int DiagnosticBudgetBytes = 64 * 1024 * 1024;
        private const int LegacyDiagnosticBudgetBytes = DiagnosticBudgetBytes - EconomyLogBudget.ReservedBytes;
        private const int LogLineOverheadBytes = 96;
        private readonly Queue<string> deferredLog = new Queue<string>();
        private int deferredBytes, writtenBytes, droppedLines;
        private bool budgetOverflowLogged;
        private uint[] wallMap;
        private byte[] goodsYardMap;
        private int goodsYardTiles, goodsYardCreated, goodsYardRemoved, nextWallMapTick;
        private bool firstWallChangeLogged;
        private int wallEventsLogged, wallEventsSuppressed;
        private long timelineSequence, sessionId;
        private bool active, firstTick;
        private int lastTick = -1, nextSummaryTick;

        internal void InitializeEconomy(CrusaderLibraryLoadContext context, string hash)
        {
            try
            {
                economy = new EconomyDiagnostics(log, unchecked((ulong)context.ModuleHandle.ToInt64()), hash);
                economy.Install(context, hash);
            }
            catch (Exception ex) { Log("AI_ECONOMY_INITIALIZATION_FAILED: " + ex); }
        }

        internal void DrainEconomy() => economy?.Drain();
        internal void DrainEconomyOnExit() => economy?.DrainOnProcessExit();

        internal AIBuildDiagnoseRuntime(ManualLogSource logger, bool hasFixes, ulong moduleBase)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            nativeModuleBase = string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
                StringComparison.OrdinalIgnoreCase) ? moduleBase : 0;
            fixesLoaded = hasFixes;
            buildingSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn);
            buildStructureSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuildStructure);
            wallSubscription = AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(OnAIBuildWall);
            loadingSubscription = Shared.MissionEvents.Loading.Subscribe(OnMapLoading);
            if (ApiShared.Current.TryGetAivBuildStep(AIBuildDiagnosePlugin.Guid,
                out IAivBuildStepCapability steps, out NativeCapabilityDiagnostic diagnostic))
            {
                if (!steps.TryRegisterObserver("general-build-context", this, out diagnostic))
                    Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            else Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
        }

        private void OnMapLoading(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            OnSessionEnded();
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            sessionId = session.SessionId;
            active = !session.IsEditor && !session.IsReplay;
            firstTick = false;
            lastTick = -1;
            nextSummaryTick = nextWallMapTick = 0;
            seen.Clear(); genericBuildEvents.Clear(); genericSpawnEvents.Clear();
            genericSpawnById.Clear(); genericBuildFrames.Clear(); genericOutcomeCounts.Clear();
            aivWallPlanByTile.Clear(); pendingWalls.Clear();
            aivWallPlanCaptured = false;
            aivWallPlanAttempts = aivWallPlanOverflow = 0;
            unattributedSiteSelections = genericFrameOverflow = genericSpawnIdOverflow = 0;
            Array.Clear(recentGenericSearch, 0, recentGenericSearch.Length);
            Array.Clear(recentGenericRoute, 0, recentGenericRoute.Length);
            Array.Clear(recentGenericSearchTick, 0, recentGenericSearchTick.Length);
            Array.Clear(recentGenericRouteTick, 0, recentGenericRouteTick.Length);
            Array.Clear(resourceSearchGenerationBefore, 0, resourceSearchGenerationBefore.Length);
            Array.Clear(farmSearchInCall, 0, farmSearchInCall.Length);
            Array.Clear(activeFarmSearchType, 0, activeFarmSearchType.Length);
            Array.Clear(schedulerCalls, 0, schedulerCalls.Length);
            Array.Clear(woodBuildCalls, 0, woodBuildCalls.Length);
            Array.Clear(lastObservedStage, 0, lastObservedStage.Length);
            Array.Clear(activeAivSteps, 0, activeAivSteps.Length);
            Array.Clear(materializedWalls, 0, materializedWalls.Length);
            deferredLog.Clear(); deferredBytes = writtenBytes = droppedLines = 0;
            budgetOverflowLogged = firstWallChangeLogged = false;
            wallMap = null; goodsYardMap = null;
            goodsYardTiles = goodsYardCreated = goodsYardRemoved = 0;
            wallEventsLogged = wallEventsSuppressed = 0;
            timelineSequence = 0;
            economy?.Start(sessionId, active, session.IsLoadedSave);
            Log($"AI_BUILD_SESSION: session={sessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, " +
                $"file={session.SaveFileName}, fixesLoaded={fixesLoaded}, active={active}, " +
                $"specialDetails={AiBuildExperimentBridge.HasDetails}, mode={session.Mode.ToDiagnosticString()}.");
            if (active) { LogPlayers("start"); CaptureWallMap("session-start"); }
        }

        internal void OnSessionEnded()
        {
            economy?.End();
            if (!active) return;
            active = false;
            FlushDeferredLog();
            LogSummary("end");
            droppedLines += deferredLog.Count;
            deferredLog.Clear(); deferredBytes = 0;
            genericBuildFrames.Clear(); pendingWalls.Clear();
            Log($"AI_BUILD_DIAGNOSTIC_BUDGET: session={sessionId}, writtenBytes={writtenBytes}, " +
                $"queuedBytes={deferredBytes}, droppedLines={droppedLines}, limitBytes={DiagnosticBudgetBytes}, " +
                $"legacyLimitBytes={LegacyDiagnosticBudgetBytes}, economyReservedBytes={EconomyLogBudget.ReservedBytes}.");
        }

        internal void OnTick(int tick)
        {
            economy?.Tick(tick);
            if (!active) return;
            lastTick = tick;
            if (!aivWallPlanCaptured && aivWallPlanAttempts < 3) CaptureAivWallPlans();
            FlushDeferredLog();
            if (tick >= nextWallMapTick) { CaptureWallMap("periodic"); nextWallMapTick = tick + 50; }
            if (!firstTick)
            {
                firstTick = true;
                Log($"AI_BUILD_POST_STARTUP_TICK: session={sessionId}, tick={tick}.");
                LogPlayers("first-tick");
            }
            if (tick >= nextSummaryTick) { LogSummary("periodic"); nextSummaryTick = tick + 500; }
        }

        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            if (!active || record.PlayerId < 0 || record.PlayerId > 8) return;
            if (record.Stage.StartsWith("resource-search-", StringComparison.Ordinal) ||
                record.Stage.StartsWith("site-search-", StringComparison.Ordinal) || record.Stage == "site-route-result" || record.Stage == "route-result")
            { ObserveGeneralSearch(record); return; }
            int player = record.PlayerId;
            if (player == 0 || record.EconomyGridEvidence != null || record.RouteEvidence != null ||
                record.NearbyPathEvidence != null || record.Stage.StartsWith("coarse-", StringComparison.Ordinal) ||
                record.Stage.IndexOf("candidate", StringComparison.Ordinal) >= 0) return;
            if (record.Stage == "farm-search-before")
            { farmSearchInCall[player] = true; activeFarmSearchType[player] = (int)record.A; }
            if (record.Stage == "farm-search-after") farmSearchInCall[player] = false;
            if (record.Stage == "scheduler-before") schedulerCalls[player]++;
            if (record.Stage == "wood-build-before") woodBuildCalls[player]++;
            lastObservedStage[player] = record.Stage;
            string key = player + ":" + record.Stage + ":" + record.A + ":" + record.B + ":" + record.C + ":" + record.D;
            seen.TryGetValue(key, out int count);
            if (seen.ContainsKey(key) || seen.Count < 8192) seen[key] = count + 1;
            else { droppedLines++; return; }
            if (count < 2 || count == 9 || count == 99 || count % 500 == 499)
                Log($"AI_BUILD_TRACE: session={sessionId}, tick={lastTick}, player={player}, " +
                    $"attempt={record.AttemptId}, stage={record.Stage}, {Describe(record)}, repeat={count + 1}.");
        }

        private void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (active && args.PlayerId >= 1 && args.PlayerId <= 8) ObserveGenericBuild(args);
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (active && args.Phase == EventHookPhase.Post && args.ReturnValue > 0 &&
                args.PlayerId >= 1 && args.PlayerId <= 8) ObserveGenericSpawn(args);
        }

        private void LogSummary(string phase)
        {
            Log($"AI_BUILD_GENERIC_EVENT_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                $"buildKeys={genericBuildEvents.Count}, spawnKeys={genericSpawnEvents.Count}, " +
                $"spawnIdsRetained={genericSpawnById.Count}, spawnIdsOmitted={genericSpawnIdOverflow}, " +
                $"outcomeKeys={genericOutcomeCounts.Count}, openFrames={genericBuildFrames.Count}, " +
                $"frameOverflow={genericFrameOverflow}, unattributedSiteSelections={unattributedSiteSelections}; eventEvidenceOnly=true.");
            Log($"AI_BUILD_AIV_WALL_PLAN_SUMMARY: session={sessionId}, captured={aivWallPlanCaptured}, " +
                $"tiles={aivWallPlanByTile.Count}, attempts={aivWallPlanAttempts}, omitted={aivWallPlanOverflow}.");
            Log($"AI_BUILD_WALL_LAYER_SUMMARY: session={sessionId}, goodsYardTiles={goodsYardTiles}, " +
                $"goodsYardAdded={goodsYardCreated}, goodsYardRemoved={goodsYardRemoved}; goodsYardIsNotWall=true.");
            try
            {
            for (int player = 1; player <= 8; player++)
                if (GamePlayerManagerAPI.Instance.IsAIPlayer(player))
                    Log($"AI_BUILD_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, player={player}, " +
                        $"scheduler={schedulerCalls[player]}, woodBuild={woodBuildCalls[player]}, " +
                        $"materializedWalls={materializedWalls[player]}, wallPostLogged={wallEventsLogged}, " +
                        $"wallPostSuppressed={wallEventsSuppressed}, routeHookReady={AiBuildDiagnostic.RouteReady}, " +
                        $"lastObserved={lastObservedStage[player] ?? "none"}; " +
                        "missingObservationsDoNotProveNoAttempt=true.");
            }
            catch (Exception ex) { Log("AI_BUILD_SUMMARY_FAILED: " + ex); }
            LogPlayers(phase);
        }

        internal void TryInstallGeneralSiteSearchHooks(CrusaderLibraryLoadContext context)
        {
            if (nativeModuleBase == 0)
            {
                Log("AI_BUILD_GENERAL_SEARCH_HOOKS_UNAVAILABLE: native hash differs.");
                return;
            }
            try
            {
                generalSearchHooks = new GeneralSiteSearchHooks(context, OnGeneralSiteSearch);
                Log("AI_BUILD_GENERAL_SEARCH_HOOKS_READY: aiv=0x583A0, fine=0x58BE0; " +
                    "backend=NativeX64/Indirect, displaced=6, readOnly=true.");
            }
            catch (Exception ex)
            {
                Log("AI_BUILD_GENERAL_SEARCH_HOOKS_UNAVAILABLE: " + ex);
            }
        }

        private void OnGeneralSiteSearch(string kind, int playerId, int inputX, int inputY,
            int resultX, int resultY)
        {
            if (!active) return;
            if (playerId == 0)
            {
                for (int candidate = 1; candidate <= 8; candidate++)
                    if (activeAivSteps[candidate] != null)
                    {
                        if (playerId != 0) { playerId = 0; break; }
                        playerId = candidate;
                    }
            }
            if (playerId < 1 || playerId > 8)
            {
                unattributedSiteSelections++;
                return;
            }
            string search = $"kind:{kind}/origin:({inputX},{inputY})/coarse:({resultX},{resultY})";
            recentGenericSearch[playerId] = search;
            recentGenericSearchTick[playerId] = lastTick;
            RecordGenericOutcome(playerId, kind,
                resultX < 0 || resultY < 0 ? "no-selection" : "selected",
                $"search={search}, " +
                (resultX >= 0 && resultY >= 0
                    ? $"siteRaw={ReadGenericSite(resultX * 5, resultY * 5)}"
                    : "siteRaw=unavailable") +
                "; aiv-neighborhood-is-not-final-building-site=true; " +
                "fine-path-search-already-checks-PCL-and-fine-grid=true");
        }

        private void ObserveGeneralSearch(AiBuildDiagnosticRecord record)
        {
            if (!active) return;
            int player = record.PlayerId;
            if (player == 0 && record.Stage.StartsWith("site-search-", StringComparison.Ordinal))
            {
                // The AIV caller of 0x58950 has no player argument. Attribute it only
                // when precisely one AIV build step is active on this thread.
                for (int candidate = 1; candidate <= 8; candidate++)
                    if (activeAivSteps[candidate] != null)
                    {
                        if (player != 0) { player = 0; break; }
                        player = candidate;
                    }
                if (player == 0 && record.Stage == "site-search-after")
                    unattributedSiteSelections++;
            }
            if (player < 1 || player > 8) return;
            if (record.Stage == "resource-search-before")
            {
                resourceSearchGenerationBefore[player] = unchecked((uint)record.B);
                return;
            }
            if (record.Stage == "site-search-before") return;
            if (record.Stage == "site-route-result" || record.Stage == "route-result")
            {
                string route = $"mode:{record.B}/target:({record.C},{record.D})/result:{record.A}";
                recentGenericRoute[player] = route;
                recentGenericRouteTick[player] = lastTick;
                if (record.A == 0)
                    RecordGenericOutcome(player, "route-" + record.B, "rejected-before-build",
                        $"route={route}, search={RecentGenericSearch(player)}, " +
                        $"target={ReadGenericSite((int)record.C, (int)record.D)}");
                return;
            }
            bool resource = record.Stage == "resource-search-after";
            if (!resource && record.Stage != "site-search-after") return;
            int x = resource ? (int)record.B : (int)record.A;
            int y = resource ? (int)record.C : (int)record.B;
            long beforeGeneration = resource ? resourceSearchGenerationBefore[player] : record.C;
            long afterGeneration = record.D;
            string kind = resource ? "resource-mode-" + record.A : "shared-site";
            string search = $"kind:{kind}/coarse:({x},{y})/generation:{beforeGeneration}->{afterGeneration}";
            recentGenericSearch[player] = search;
            recentGenericSearchTick[player] = lastTick;
            if (x < 0 || y < 0)
                RecordGenericOutcome(player, kind,
                    beforeGeneration == afterGeneration ? "no-selection-before-traversal" : "traversed-without-selection",
                    $"search={search}; no-placement-attempt-proven=true");
            else
                RecordGenericOutcome(player, kind, "selected",
                    $"search={search}, coarseRaw={ReadGenericSite(x * 5, y * 5)}; " +
                    "resource-result-is-not-final-building-site=true");
        }

        private string RecentGenericSearch(int player) =>
            recentGenericSearchTick[player] == lastTick
                ? recentGenericSearch[player] ?? "unobserved" : "unattributed-or-older-tick";

        private string RecentGenericRoute(int player) =>
            recentGenericRouteTick[player] == lastTick
                ? recentGenericRoute[player] ?? "unobserved" : "unattributed-or-older-tick";

        private void RecordGenericOutcome(int player, string kind, string result, string evidence)
        {
            string key = player + ":" + kind + ":" + result;
            genericOutcomeCounts.TryGetValue(key, out int count);
            genericOutcomeCounts[key] = count + 1;
            if (count == 0)
                QueueDiagnostic($"AI_BUILD_GENERAL_OUTCOME: session={sessionId}, tick={lastTick}, " +
                    $"player={player}, kind={kind}, observedLast={result}, " +
                    $"evidence={evidence}, repeat=1.", true);
            else if (count == 9 || count == 99 || count % 500 == 499)
                QueueDiagnostic($"AI_BUILD_GENERAL_REPEAT: session={sessionId}, tick={lastTick}, " +
                    $"player={player}, kind={kind}, observedLast={result}, repeat={count + 1}.");
        }

        private void TrackGenericBuild(BuildStructureEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                if (genericBuildFrames.Count >= 128) { genericFrameOverflow++; return; }
                ActiveAivStep step = activeAivSteps[args.PlayerId];
                genericBuildFrames.Add(new GenericBuildFrame
                {
                    Player = args.PlayerId, Mapper = args.Mappers, X = args.TileX, Y = args.TileY,
                    Tick = lastTick, BeforeSite = ReadGenericSite(args.TileX, args.TileY),
                    Search = RecentGenericSearch(args.PlayerId), Route = RecentGenericRoute(args.PlayerId),
                    Source = farmSearchInCall[args.PlayerId]
                        ? "farm-economy/type:" + activeFarmSearchType[args.PlayerId]
                        : step != null ? $"aiv/frame:{step.FrameIndex}/mapper:{step.Mapper}"
                        : "unattributed"
                });
                return;
            }
            if (args.Phase != EventHookPhase.Post) return;
            for (int index = genericBuildFrames.Count - 1; index >= 0; index--)
            {
                GenericBuildFrame frame = genericBuildFrames[index];
                if (frame.Player != args.PlayerId || frame.Mapper != args.Mappers ||
                    frame.X != args.TileX || frame.Y != args.TileY) continue;
                genericBuildFrames.RemoveAt(index);
                string outcome = frame.SpawnCount > 0 ? "spawn-observed" : "build-returned-without-spawn";
                RecordGenericOutcome(args.PlayerId, args.Mappers.ToString(), outcome,
                    $"source={frame.Source}, startTick={frame.Tick}, tile=({frame.X},{frame.Y}), " +
                    $"search={frame.Search}, route={frame.Route}, before={frame.BeforeSite}, " +
                    $"after={ReadGenericSite(frame.X, frame.Y)}, spawns={frame.SpawnCount}, " +
                    $"spawnTypes={frame.SpawnTypes ?? "none"}; " +
                    "PostReturnValueUnavailable=true; noSpawnDoesNotIdentifyPlacementBranch=true");
                return;
            }
            RecordGenericOutcome(args.PlayerId, args.Mappers.ToString(), "post-without-pre",
                $"tile=({args.TileX},{args.TileY}); event-pair-unavailable=true");
        }

        private void ObserveGenericBuild(BuildStructureEventArgs args)
        {
            TrackGenericBuild(args);
            string key = $"{args.PlayerId}:{(int)args.Mappers}:{args.Phase}";
            genericBuildEvents.TryGetValue(key, out int prior);
            genericBuildEvents[key] = prior + 1;
            if (prior >= 2) return;
            // All mappers are recorded raw; their individual footprints are not inferred here.
            QueueDiagnostic($"AI_BUILD_GENERIC_STRUCTURE: session={sessionId}, tick={lastTick}, " +
                $"player={args.PlayerId}, mapper={args.Mappers}, phase={args.Phase}, " +
                $"ordinal={prior + 1}, tile=({args.TileX},{args.TileY}), " +
                $"scale={args.BuildingScaleUnknown}, free={args.IsFree}, " +
                $"{ReadGenericSite(args.TileX, args.TileY)}; footprint=not-audited-for-mapper; " +
                "postEventReturnValueNotAuthoritative=true.", prior == 0);
        }

        private void ObserveGenericSpawn(BuildingSpawnEventArgs args)
        {
            // Resolve the active frame at the spawn, so a nested child's Post restores
            // the parent's context before its later spawn. No stale Pre-only cache.
            string construction = null;
            for (int i = genericBuildFrames.Count - 1; i >= 0; i--)
            {
                GenericBuildFrame frame = genericBuildFrames[i];
                if (frame.Player != args.PlayerId) continue;
                construction = $"activeMapper={frame.Mapper}, buildTile=({frame.X},{frame.Y}), source={frame.Source}, search={frame.Search}, route={frame.Route}, siteBefore=[{frame.BeforeSite}]";
                break;
            }
            economy?.RecordConstructionContext(args.PlayerId, construction);
            for (int index = genericBuildFrames.Count - 1; index >= 0; index--)
                if (genericBuildFrames[index].Player == args.PlayerId &&
                    genericBuildFrames[index].X == args.TileX &&
                    genericBuildFrames[index].Y == args.TileY)
                {
                    GenericBuildFrame frame = genericBuildFrames[index];
                    frame.SpawnCount++;
                    frame.SpawnTypes = frame.SpawnTypes == null ? args.Building.ToString() :
                        frame.SpawnTypes + "|" + args.Building;
                    break;
                }
            string key = $"{args.PlayerId}:{(int)args.Building}";
            genericSpawnEvents.TryGetValue(key, out int prior);
            genericSpawnEvents[key] = prior + 1;
            if (args.ReturnValue <= int.MaxValue)
            {
                if (genericSpawnById.Count < 4096)
                    genericSpawnById[(int)args.ReturnValue] =
                        $"type:{args.Building}/owner:{args.PlayerId}/tick:{lastTick}/" +
                        $"origin:({args.TileX},{args.TileY})";
                else genericSpawnIdOverflow++;
            }
            else genericSpawnIdOverflow++;
            if (prior >= 2) return;
            QueueDiagnostic($"AI_BUILD_GENERIC_SPAWN: session={sessionId}, tick={lastTick}, " +
                $"player={args.PlayerId}, type={args.Building}, ordinal={prior + 1}, " +
                $"buildingId={args.ReturnValue}, tile=({args.TileX},{args.TileY}), " +
                $"{ReadGenericSite(args.TileX, args.TileY)}.", prior == 0);
        }

        private static string ReadGenericSite(int x, int y)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                if (!tiles.IsTileInsideMapBounds(x, y)) return "anchorStatus=outside-map";
                int id = tiles.GetTileId(x, y);
                Span<ushort> pcl = GamePathingManagerAPI.Instance.GetPathComponentGrid();
                if (id <= 0 || (uint)id >= (uint)pcl.Length)
                    return $"anchorStatus=invalid-tile-id, anchorTileId={id}, pclLength={pcl.Length}";
                string coarse = "unavailable";
                int cx = x / 5, cy = y / 5;
                Span<AivCoarseCell> grid = GameAIVManagerAPI.Instance.GetCoarseGrid();
                int index = cx * 160 + cy;
                if ((uint)cx < 160 && (uint)cy < 160 && (uint)index < (uint)grid.Length)
                {
                    ref AivCoarseCell cell = ref grid[index];
                    coarse = $"foreign:{unchecked((sbyte)cell.ForeignPathComponentTileCount)}/" +
                        $"reservation:{cell.StructureOrReservationCount}/" +
                        $"treeWeight:{cell.TreeObstructionWeight}/retry:{cell.WoodcutterRetryDelay}";
                }
                return $"anchorStatus=ok, anchorTileId={id}, anchorPcl={pcl[id]}, " +
                    $"anchorFlags=0x{(uint)tiles.GetTilePropertyFlag(id):X8}, " +
                    $"anchorBuilding={tiles.GetTileBuildingId(id)}, coarse=({cx},{cy}), " +
                    $"coarseRaw={coarse}";
            }
            catch (Exception ex) { return "anchorStatus=read-failed:" + ex.GetType().Name; }
        }

        private void OnAIBuildWall(AIBuildWallEventArgs args)
        {
            if (!active || args.PlayerId < 1 || args.PlayerId > 8) return;
            try
            {
                WallObservation wall = CaptureWall(args);
                wall.Tick = lastTick;
                wall.Sequence = ++timelineSequence;
                ActiveAivStep step = activeAivSteps[args.PlayerId];
                if (step != null)
                {
                    wall.AivFrame = step.FrameIndex;
                    wall.AivMapper = step.Mapper;
                    wall.AivStateBefore = step.StateBefore;
                    wall.PlannedTileCount = step.PlannedTiles.Length;
                    wall.PlannedTile = wall.TileId > 0 &&
                        Array.IndexOf(step.PlannedTiles, wall.TileId) >= 0;
                }
                string key = args.PlayerId + ":" + args.TileX + ":" + args.TileY;
                if (args.Phase == EventHookPhase.Pre) pendingWalls[key] = wall;
                else if (args.Phase == EventHookPhase.Post)
                {
                    if (pendingWalls.TryGetValue(key, out WallObservation before))
                    {
                        wall.Materialized = before.Status == "ok" && wall.Status == "ok" &&
                            !IsActualWall(before.PropertyFlags) &&
                            IsActualWall(wall.PropertyFlags);
                        wall.Change = DescribeWallChange(before, wall);
                        wall.Before = before;
                        pendingWalls.Remove(key);
                    }
                    else wall.Change = "unpaired-post";
                }
                if (args.Phase == EventHookPhase.Post)
                {
                    if (wall.Materialized) materializedWalls[args.PlayerId]++;
                    bool genuineWallChange = wall.Before != null &&
                        wall.Before.Status == "ok" && wall.Status == "ok" &&
                        wall.Change != "unchanged";
                    if (genuineWallChange)
                        QueueDiagnostic($"AI_BUILD_WALL_CHANGE: session={sessionId}, tick={lastTick}, " +
                            $"sequence={wall.Sequence}, player={wall.PlayerId}, tileId={wall.TileId}, " +
                            $"tile=({wall.X},{wall.Y}), change={wall.Change}, " +
                            $"before={DescribeWallRaw(wall.Before)}, after={DescribeWallRaw(wall)}, " +
                            $"aivFrame={wall.AivFrame}, aivMapper={wall.AivMapper}, " +
                            $"aivStateBefore={wall.AivStateBefore}, plannedTile={wall.PlannedTile}.",
                            !firstWallChangeLogged);
                    if (genuineWallChange)
                        firstWallChangeLogged = true;
                    if (wallEventsLogged < 4096)
                    {
                        QueueDiagnostic(FormatWall(wall, "all-ai-wall-post", 0));
                        wallEventsLogged++;
                    }
                    else wallEventsSuppressed++;
                }
                AiBuildExperimentBridge.DeliverWall(wall, log);
            }
            catch (Exception ex) { Log("AI_BUILD_WALL_OBSERVATION_FAILED: " + ex); }
        }

        private static WallObservation CaptureWall(AIBuildWallEventArgs args)
        {
            var wall = new WallObservation
            {
                PlayerId = args.PlayerId, X = args.TileX, Y = args.TileY,
                Phase = args.Phase.ToString(), Mapper = args.Mappers.ToString(),
                Status = "outside-map"
            };
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(wall.X, wall.Y)) return wall;
            int tileId = tiles.GetTileId(wall.X, wall.Y);
            Span<byte> owners = tiles.GetWallOwnerLayer();
            if (tileId <= 0 || (uint)tileId >= (uint)owners.Length)
            { wall.Status = tileId == 0 ? "invalid-tile-id-zero" : "wall-layer-out-of-range"; return wall; }
            wall.TileId = tileId;
            wall.PropertyFlags = (uint)tiles.GetTilePropertyFlag(tileId);
            wall.TileType = (int)tiles.GetTileType(tileId);
            wall.Height = tiles.GetTileHeight(tileId);
            wall.BuildingId = tiles.GetTileBuildingId(tileId);
            wall.WallOwner = owners[tileId];
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            wall.PathComponent = (uint)tileId < (uint)components.Length ? components[tileId] : -1;
            wall.Status = "ok";
            return wall;
        }

        private string FormatWall(WallObservation wall, string relation, long attemptId) =>
            $"AI_BUILD_WALL_EVENT: session={sessionId}, tick={wall.Tick}, " +
                $"relation={relation}, attempt={attemptId}, player={wall.PlayerId}, " +
                $"phase={wall.Phase}, mapper={wall.Mapper}, tile=({wall.X},{wall.Y}), " +
                $"tileId={wall.TileId}, rawFlags=0x{wall.PropertyFlags:X8}, " +
                $"swamp={((wall.PropertyFlags & 0x20000000u) != 0)}, " +
                $"wallPresent={IsActualWall(wall.PropertyFlags)}, " +
                $"materializedFromPre={wall.Materialized}, buildingId={wall.BuildingId}, " +
                $"tileType={wall.TileType}, height={wall.Height}, " +
                $"wallOwner={wall.WallOwner}, pathComponent={wall.PathComponent}, " +
                $"change={wall.Change ?? "unobserved"}, status={wall.Status}, sequence={wall.Sequence}, " +
                $"aivFrame={wall.AivFrame}, aivMapper={wall.AivMapper}, " +
                $"aivStateBefore={wall.AivStateBefore}, plannedTile={wall.PlannedTile}, " +
                $"plannedTileCount={wall.PlannedTileCount}.";

        private void LogWall(WallObservation wall, string relation, long attemptId) =>
            Log(FormatWall(wall, relation, attemptId));

        private static string DescribeWallRaw(WallObservation wall) => wall == null ? "missing" :
            $"status:{wall.Status}/flags:0x{wall.PropertyFlags:X8}/owner:{wall.WallOwner}/" +
            $"type:{wall.TileType}/height:{wall.Height}/building:{wall.BuildingId}/pcl:{wall.PathComponent}";

        private static string DescribeWallChange(WallObservation before, WallObservation after)
        {
            if (before.Status != "ok" || after.Status != "ok") return "invalid-tile-or-map";
            bool oldWall = IsActualWall(before.PropertyFlags);
            bool newWall = IsActualWall(after.PropertyFlags);
            if (!oldWall && newWall) return "new-wall";
            if (oldWall && !newWall) return "removed-wall";
            if (before.PropertyFlags != after.PropertyFlags || before.WallOwner != after.WallOwner ||
                before.TileType != after.TileType || before.Height != after.Height ||
                before.BuildingId != after.BuildingId || before.PathComponent != after.PathComponent)
                return "modified-existing-tile";
            return "unchanged";
        }

        internal sealed class WallObservation
        {
            internal int PlayerId, X, Y, TileId, BuildingId, WallOwner, Tick, TileType, Height, PathComponent;
            internal int AivFrame = -1, AivMapper = -1, AivStateBefore = -1, PlannedTileCount;
            internal long Sequence;
            internal bool PlannedTile;
            internal uint PropertyFlags;
            internal string Phase, Mapper, Status;
            internal bool Materialized;
            internal string Change;
            internal WallObservation Before;
        }

        private void LogPlayers(string phase)
        {
            try
            {
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!players.IsAIPlayer(playerId)) continue;
                    string aiv = "aiv=unavailable";
                    if (GameAIVManagerAPI.Instance.TryGetVillageByPlayerId(playerId,
                        out AivVillageState* village) && village != null)
                        aiv = $"aivVariant={village->SelectedVariantIndex}, " +
                            $"aivRotation={village->Rotation}, " +
                            $"keep=({village->KeepX},{village->KeepY}), " +
                            $"layoutOrigin=({village->LayoutOriginX},{village->LayoutOriginY}), " +
                            $"aivBuildStep={village->UnlockedBuildStep}/{village->MaximumBuildStep}";
                    Log($"AI_BUILD_PLAYER: session={sessionId}, phase={phase}, player={playerId}, " +
                        $"lord={players.GetAILord(playerId)}, {aiv}, {ReadResources(playerId)}.");
                }
            }
            catch (Exception ex) { Log("AI_BUILD_PLAYER_READ_FAILED: " + ex); }
        }

        private static string ReadResources(int playerId)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId,
                out GamePlayerResources* resources) || resources == null)
                return "resources=unavailable";
            return $"gold={resources->r_TotalGoodsGold}, woodLogs={resources->r_TotalGoodsWoodLogs}, " +
                $"woodPlanks={resources->r_TotalGoodsWoodPlanks}";
        }

        private static string Describe(AiBuildDiagnosticRecord record)
        {
            switch (record.Stage)
            {
                case "scheduler-before":
                case "scheduler-after":
                    return $"villageSlot={record.A}, schedulerDelay={record.B}, economyPhase={record.C}";
                case "wood-build-before":
                    return $"fixSession={record.A}, fixEnabled={record.B}, loadedSave={record.C}, fixMapRelevant={record.D}";
                case "wood-search-before":
                case "wood-search-after":
                    return $"cooldown={record.A}, resultX={record.B}, resultY={record.C}";
                case "wood-nearby-before":
                    return $"coarseX={record.A}, coarseY={record.B}";
                case "wood-build-after":
                case "wood-nearby-after":
                    return $"resultX={record.A}, resultY={record.B}";
                case "wood-candidate-scan":
                    return $"visitedCells={record.A}, formalCandidates={record.B}, bestScore={record.C}, generation={record.D}";
                case "wood-candidate-scan-error":
                    return $"scanHResult={record.A}";
                case "route-components":
                    return $"sourceComponent={record.A}, targetComponent={record.B}, sourceTile={record.C}, targetTile={record.D}";
                case "route-result":
                    return $"result={record.A}, mapperIndex={record.B}, target=({record.C},{record.D})";
                case "farm-scheduler-before":
                case "farm-scheduler-after":
                    return $"profile={record.A}, choiceIndex={record.B}, selectedStructure={record.C}, " +
                        $"economyPhase={record.D}";
                case "farm-limits-before":
                case "farm-limits-after":
                    return $"farmCount={record.A}, farmLimit={record.B}, searchCooldown={record.C}, " +
                        $"profileGoal={(int)(record.D >> 32)}, profileMinimum={(int)record.D}";
                case "farm-search-before":
                    return $"desiredStructure={record.A}, cooldown={record.B}, " +
                        $"preplacedEconomyRelevant={record.C}, preplacedFixSession={record.D}";
                case "farm-search-after":
                    return $"result={record.A}, cooldown={record.B}, sharedSearchSlot=({record.C},{record.D})";
                case "farm-search-generation":
                    return $"generationBefore={record.A}, generationAfter={record.B}, " +
                        $"coarseTraversalStarted={record.A != record.B}";
                case "farm-candidate-scan":
                    return $"lastPassVisitedCells={record.A}, lastPassGeneration={record.B}";
                case "wood-candidate-origin":
                case "farm-candidate-origin":
                    return $"searchOrigin=({record.A},{record.B}), lastPassGeneration={record.C}";
                case "farm-candidate-overflow":
                case "wood-candidate-overflow":
                    return $"suppressedCellDetails={record.A}";
                case "route-evidence":
                    return $"status={record.RouteEvidence?.Status ?? "missing"}, sourceComponent={record.A}, " +
                        $"targetComponent={record.B}, sourceNative={record.RouteEvidence?.SourceNativeComponent}, " +
                        $"targetNative={record.RouteEvidence?.TargetNativeComponent}, " +
                        $"sourceTile={record.C}, targetTile={record.D}";
                case "wood-nearby-path-before":
                case "wood-nearby-path-after":
                    return $"status={record.NearbyPathEvidence?.Status ?? "missing"}, " +
                        $"input=({record.A},{record.B}), result=({record.C},{record.D})";
                default:
                    return $"a={record.A}, b={record.B}, c={record.C}, d={record.D}";
            }
        }

        public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
        {
            if (!active || context.PlayerId < 1 || context.PlayerId > 8) return null;
            ActiveAivStep current = CaptureAivStep(context.PlayerId, context.FrameIndex);
            ActiveAivStep previous = activeAivSteps[context.PlayerId];
            if (current != null)
            {
                activeAivSteps[context.PlayerId] = current;
                long sequence = ++timelineSequence;
                Log($"AI_BUILD_AIV_TIMELINE: session={sessionId}, sequence={sequence}, tick={lastTick}, " +
                    $"phase=before, player={context.PlayerId}, frame={context.FrameIndex}, " +
                    $"mapper={current.Mapper}, state={current.StateBefore}, " +
                    $"plannedTileCount={current.PlannedTiles.Length}, plannedFirst={current.FirstTiles}.");
            }
            return new BuildStepObservation(this, context.PlayerId, context.FrameIndex, current, previous);
        }

        private ActiveAivStep CaptureAivStep(int playerId, int frameIndex)
        {
            try
            {
                GameAIVManagerAPI api = GameAIVManagerAPI.Instance;
                if (!api.TryGetVillageSlotByPlayerId(playerId, out int slot) ||
                    !api.TryGetBuildStep(slot, frameIndex, out AivBuildStep* step)) return null;
                if (!GameAIVManagerAPI.UsesOrderedMapTileBuffer(step->BuildingType)) return null;
                Span<int> tiles = api.GetBuildStepMapTiles(slot, frameIndex);
                int[] copied = new int[tiles.Length];
                tiles.CopyTo(copied);
                int shown = Math.Min(copied.Length, 16);
                string[] first = new string[shown];
                for (int i = 0; i < shown; i++) first[i] = copied[i].ToString();
                return new ActiveAivStep(frameIndex, slot, (int)step->BuildingType,
                    (int)step->State, copied, string.Join("/", first));
            }
            catch (Exception ex)
            {
                Log("AI_BUILD_AIV_CAPTURE_FAILED: player=" + playerId + ", frame=" + frameIndex + ", error=" + ex);
                return null;
            }
        }

        private sealed class ActiveAivStep
        {
            internal readonly int FrameIndex, VillageSlot, Mapper, StateBefore;
            internal readonly int[] PlannedTiles;
            internal readonly string FirstTiles;
            internal ActiveAivStep(int frameIndex, int villageSlot, int mapper, int stateBefore,
                int[] plannedTiles, string firstTiles)
            {
                FrameIndex = frameIndex; VillageSlot = villageSlot; Mapper = mapper;
                StateBefore = stateBefore; PlannedTiles = plannedTiles; FirstTiles = firstTiles;
            }
        }

        private sealed class BuildStepObservation : IAivBuildStepInvocation
        {
            private readonly AIBuildDiagnoseRuntime owner;
            private readonly int playerId;
            private readonly int frameIndex;
            private readonly ActiveAivStep current;
            private readonly ActiveAivStep previous;
            internal BuildStepObservation(AIBuildDiagnoseRuntime owner, int playerId, int frameIndex,
                ActiveAivStep current, ActiveAivStep previous)
            {
                this.owner = owner; this.playerId = playerId; this.frameIndex = frameIndex;
                this.current = current; this.previous = previous;
            }
            public void Complete(AivBuildStepCompletion completion)
            {
                if (current != null)
                {
                    int stateAfter = -1;
                    try
                    {
                        if (GameAIVManagerAPI.Instance.TryGetBuildStep(current.VillageSlot,
                            frameIndex, out AivBuildStep* step)) stateAfter = (int)step->State;
                    }
                    catch (Exception ex) { owner.Log("AI_BUILD_AIV_AFTER_FAILED: " + ex); }
                    long sequence = ++owner.timelineSequence;
                    owner.Log($"AI_BUILD_AIV_TIMELINE: session={owner.sessionId}, sequence={sequence}, " +
                        $"tick={owner.lastTick}, phase=after, player={playerId}, frame={frameIndex}, " +
                        $"mapper={current.Mapper}, stateBefore={current.StateBefore}, stateAfter={stateAfter}, " +
                        $"completed={completion.VanillaCompleted}, result={completion.VanillaResult}.");
                    owner.activeAivSteps[playerId] = previous;
                }
                if (!owner.active) return;
                string key = "aiv:" + playerId + ":" + frameIndex + ":" + completion.VanillaResult;
                if (!owner.seen.TryGetValue(key, out int count)) count = 0;
                owner.seen[key] = count + 1;
                if (count == 0)
                    owner.Log($"AI_BUILD_AIV_STEP: session={owner.sessionId}, tick={owner.lastTick}, " +
                        $"player={playerId}, frame={frameIndex}, completed={completion.VanillaCompleted}, result={completion.VanillaResult}.");
            }
        }

        private void QueueDiagnostic(string value, bool preserveFirst = false)
        {
            int bytes = Encoding.UTF8.GetByteCount(value) + LogLineOverheadBytes;
            if (writtenBytes + deferredBytes + bytes > LegacyDiagnosticBudgetBytes)
            {
                droppedLines++;
                if (!budgetOverflowLogged)
                {
                    budgetOverflowLogged = true;
                    deferredLog.Enqueue($"AI_BUILD_DIAGNOSTIC_OVERFLOW: session={sessionId}, " +
                        $"limitBytes={DiagnosticBudgetBytes}, firstDroppedTick={lastTick}.");
                }
                return;
            }
            deferredLog.Enqueue(value);
            deferredBytes += bytes;
        }

        private void FlushDeferredLog()
        {
            int flushed = 0;
            while (deferredLog.Count != 0 && flushed++ < 2048)
            {
                string line = deferredLog.Dequeue();
                int bytes = Encoding.UTF8.GetByteCount(line) + LogLineOverheadBytes;
                deferredBytes = Math.Max(0, deferredBytes - bytes);
                Log(line);
            }
        }

        private void CaptureAivWallPlans()
        {
            aivWallPlanAttempts++;
            try
            {
                GameAIVManagerAPI api = GameAIVManagerAPI.Instance;
                int wallSteps = 0, invalidTiles = 0;
                for (int player = 1; player <= 8; player++)
                {
                    if (!api.TryGetVillageSlotByPlayerId(player, out int slot) ||
                        !api.TryGetVillageByPlayerId(player, out AivVillageState* village)) continue;
                    Span<AivBuildStep> steps = api.GetBuildSteps(slot);
                    int count = village->MaximumBuildStep < 0 ? 0 :
                        Math.Min(steps.Length, village->MaximumBuildStep + 1);
                    for (int frame = 0; frame < count; frame++)
                    {
                        if (steps[frame].BuildingType != eMappers.MAPPER_WALL) continue;
                        wallSteps++;
                        Span<int> planned = api.GetBuildStepMapTiles(slot, frame);
                        for (int i = 0; i < planned.Length; i++)
                        {
                            int tileId = planned[i];
                            if (tileId <= 0 || tileId >= 320800) { invalidTiles++; continue; }
                            string match = $"player:{player}/frame:{frame}/slot:{slot}";
                            if (aivWallPlanByTile.TryGetValue(tileId, out string earlier))
                            {
                                if (earlier.Length < 160) aivWallPlanByTile[tileId] = earlier + "|" + match;
                                continue;
                            }
                            if (aivWallPlanByTile.Count < 100000) aivWallPlanByTile[tileId] = match;
                            else aivWallPlanOverflow++;
                        }
                    }
                }
                aivWallPlanCaptured = wallSteps > 0 || aivWallPlanAttempts >= 3;
                if (aivWallPlanCaptured)
                    QueueDiagnostic($"AI_BUILD_AIV_WALL_PLAN_CAPTURE: session={sessionId}, tick={lastTick}, " +
                        $"steps={wallSteps}, mappedTiles={aivWallPlanByTile.Count}, " +
                        $"invalidTiles={invalidTiles}, omitted={aivWallPlanOverflow}, " +
                        $"attempts={aivWallPlanAttempts}; membershipDoesNotProveConstruction=true.", true);
            }
            catch (Exception ex)
            {
                if (aivWallPlanAttempts >= 3) aivWallPlanCaptured = true;
                QueueDiagnostic($"AI_BUILD_AIV_WALL_PLAN_FAILED: session={sessionId}, " +
                    $"attempt={aivWallPlanAttempts}, error={ex.GetType().Name}: {ex.Message}.", true);
            }
        }

        private void CaptureWallMap(string phase)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                Span<byte> owners = tiles.GetWallOwnerLayer();
                uint[] next = new uint[owners.Length];
                byte[] nextGoodsYards = new byte[owners.Length];
                int changed = 0, created = 0, removed = 0, altered = 0;
                int goodsYards = 0, goodsYardAdded = 0, goodsYardLost = 0;
                for (int tileId = 1; tileId < next.Length; tileId++)
                {
                    uint flags = (uint)tiles.GetTilePropertyFlag(tileId);
                    bool goodsYard = (flags & (uint)TilePropertyFlag.GoodsyardRelated) != 0;
                    if (goodsYard)
                    {
                        nextGoodsYards[tileId] = 1;
                        goodsYards++;
                    }
                    if (goodsYardMap != null && tileId < goodsYardMap.Length)
                    {
                        if (goodsYardMap[tileId] == 0 && goodsYard) goodsYardAdded++;
                        else if (goodsYardMap[tileId] != 0 && !goodsYard) goodsYardLost++;
                    }
                    // Goodsyard and connection tiles also carry IsWall; they are not castle walls.
                    uint wall = IsActualWall(flags) ? (uint)TilePropertyFlag.IsWall : 0;
                    next[tileId] = wall == 0 ? 0 : wall | owners[tileId];
                    if (wallMap == null || tileId >= wallMap.Length || wallMap[tileId] == next[tileId]) continue;
                    uint old = wallMap[tileId];
                    changed++;
                    if ((old & 0x100u) == 0 && wall != 0) created++;
                    else if ((old & 0x100u) != 0 && wall == 0) removed++;
                    else altered++;
                    if (changed <= 512 || !firstWallChangeLogged)
                    {
                        int y = tiles.MapColumnLookupTable[tileId];
                        int x = tileId - tiles.MapRowLookupTable[3 * y];
                        int buildingId = tiles.GetTileBuildingId(tileId);
                        string spawned = genericSpawnById.TryGetValue(buildingId, out string source)
                            ? source : "unmatched";
                        string planned = aivWallPlanByTile.TryGetValue(tileId, out string plan)
                            ? plan : "unmatched";
                        QueueDiagnostic($"AI_BUILD_WALL_MAP_CHANGE: session={sessionId}, tick={lastTick}, " +
                            $"phase={phase}, sequence={++timelineSequence}, tileId={tileId}, " +
                            $"tile=({x},{y}), old=0x{old:X3}, new=0x{next[tileId]:X3}, " +
                            $"kind={(wall != 0 && (old & 0x100u) == 0 ? "new" : wall == 0 &&
                                (old & 0x100u) != 0 ? "removed" : "owner-changed")}, " +
                            $"detailFlags=0x{flags:X8}, " +
                            $"building={buildingId}, matchedSpawn={spawned}, aivWallPlan={planned}, " +
                            $"height={tiles.GetTileHeight(tileId)}.",
                            !firstWallChangeLogged);
                        firstWallChangeLogged = true;
                    }
                }
                wallMap = next;
                goodsYardMap = nextGoodsYards;
                goodsYardTiles = goodsYards;
                goodsYardCreated += goodsYardAdded;
                goodsYardRemoved += goodsYardLost;
                QueueDiagnostic($"AI_BUILD_WALL_MAP_SCAN: session={sessionId}, tick={lastTick}, phase={phase}, " +
                    $"tiles={next.Length}, changed={changed}, created={created}, removed={removed}, " +
                    $"altered={altered}, detailOmitted={Math.Max(0, changed - 512)}, " +
                    $"goodsYardTiles={goodsYardTiles}, goodsYardAdded={goodsYardAdded}, " +
                    $"goodsYardRemoved={goodsYardLost}; goodsYardIsNotWall=true.");
            }
            catch (Exception ex) { QueueDiagnostic("AI_BUILD_WALL_MAP_SCAN_FAILED: " + ex); }
        }

        private static bool IsActualWall(uint flags) =>
            (flags & (uint)TilePropertyFlag.IsWall) != 0 &&
            (flags & (uint)TilePropertyFlag.GoodsyardRelated) == 0;

        private void Log(string value)
        {
            int bytes = Encoding.UTF8.GetByteCount(value) + LogLineOverheadBytes;
            if (writtenBytes + bytes > LegacyDiagnosticBudgetBytes &&
                !value.StartsWith("AI_BUILD_DIAGNOSTIC_BUDGET:", StringComparison.Ordinal))
            {
                droppedLines++;
                if (!budgetOverflowLogged)
                {
                    budgetOverflowLogged = true;
                    Shared.DebugLogHelper.LogInfo(log,
                        $"AI_BUILD_DIAGNOSTIC_OVERFLOW: session={sessionId}, " +
                        $"limitBytes={DiagnosticBudgetBytes}, firstDroppedTick={lastTick}.");
                }
                return;
            }
            writtenBytes += bytes;
            Shared.DebugLogHelper.LogInfo(log, value);
        }

    }
}
