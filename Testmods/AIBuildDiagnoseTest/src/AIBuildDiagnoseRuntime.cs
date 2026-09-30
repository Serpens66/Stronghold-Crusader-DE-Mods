using APIShared;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Vegetation;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe class AIBuildDiagnoseRuntime : IAivBuildStepObserver
    {
        private const string ProbeSaveName = "test_canari_nowoodcutters_probe.sav";
        private const int ProbePlayer = 6;
        private const int ProbeX = 345;
        private const int ProbeY = 485;
        private const int GridHistoryLimit = 64;
        private const int ExistingAppleFarmLimit = 64;
        private const int AppleFarmLimit = 64;
        private const int AppleFarmSnapshotLimit = 192;
        private const int FarmGridRawLimit = 4;
        // Native 0x72490 sets this placement-reservation bit on farm parcel tiles.
        private const uint NativePlacementReservationFlag = 0x4;
        private const int OrchardObservationTicks = 700;
        // Native 0x72A50, RVA 0x2D3DC0: eight apple-tree offsets from the farm origin.
        private static readonly int[] OrchardDx = { 5, 9, 1, 5, 9, 1, 5, 9 };
        private static readonly int[] OrchardDy = { 1, 1, 5, 5, 5, 9, 9, 9 };
        private readonly ManualLogSource log;
        private readonly ulong nativeModuleBase;
        private readonly bool fixesLoaded;
        private readonly bool placementProbeEnabled;
        private readonly bool nearbyWoodTestEnabled;
        private const int NearbyCopyMaxApplications = 12;
        private bool nearbyCopySession, nearbyCalibrated, nearbyTestDisabled;
        private int nearbyCopyApplications;
        private int nearbyAlternativeTraces;
        private int nearbyCalibrationAttempts;
        private readonly bool[] nearbyDetailedByPlayer = new bool[9];
        private readonly IDisposable buildingSubscription;
        private readonly IDisposable vegetationSubscription;
        private readonly IDisposable buildStructureSubscription;
        private readonly IDisposable wallSubscription;
        private readonly IDisposable loadingSubscription;
        private readonly List<string> earlyGridHistory = new List<string>();
        private readonly List<string> earlyFarmHistory = new List<string>();
        private readonly List<AppleFarmWatch> appleFarms = new List<AppleFarmWatch>();
        private int appleFarmDropped;
        private int appleFarmSnapshots;
        private int appleFarmSnapshotDropped;
        private int orchardTransitions;
        private int orchardTransitionsDropped;
        private string pendingAppleFarmPre;
        private int pendingAppleFarmX, pendingAppleFarmY, pendingAppleFarmPlayer;
        private bool firstOrchardMismatchSeen;
        private int coarseAuditWritesEnabled = -1;
        private int coarseAuditAvailable = -1;
        private int coarseAuditReference = -1;
        private long coarseAuditSession;
        private long coarseAuditSnapshotSession;
        private bool coarseAuditComplete;
        private readonly Dictionary<int, string> coarseAuditProposals = new Dictionary<int, string>();
        private readonly HashSet<int> coarseOriginLogged = new HashSet<int>();
        private int earlyGridDropped;
        private int earlyFarmDropped;
        private int gridModeZeroCalls;
        private int gridModeOneCalls;
        private long gridSequence;
        private ulong gridState;
        private string lastGridSignature;
        private string loadingSaveName;
        private bool firstGridMismatchSeen;
        private string firstGridMismatchLine;
        private readonly Dictionary<long, Attempt> attempts = new Dictionary<long, Attempt>();
        private readonly Dictionary<string, int> outcomes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> routeCauses = new Dictionary<string, int>();
        private readonly Dictionary<string, int> nearbyCauses = new Dictionary<string, int>();
        private readonly HashSet<string> detailedRoutes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> detailedNearby = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> seen = new Dictionary<string, int>();
        private readonly int[] schedulerCalls = new int[9];
        private readonly int[] woodBuildCalls = new int[9];
        private readonly int[] woodSearchCalls = new int[9];
        private readonly int[] farmSelectedFrames = new int[9];
        private readonly int[] farmSearchCalls = new int[9];
        private readonly int[] farmTraversals = new int[9];
        private readonly int[] farmBuildCalls = new int[9];
        private readonly int[] appleSpawns = new int[9];
        private readonly int[] lastFarmResult = new int[9];
        private readonly int[] beforeFarmPhase = new int[9];
        private readonly int[] beforeFarmType = new int[9];
        private readonly int[] activeFarmSearchType = new int[9];
        private readonly bool[] beforeFarmValid = new bool[9];
        private readonly int[] farmPhaseReached = new int[9];
        private readonly int[] applePhaseReached = new int[9];
        private readonly bool[] woodCellDetailDone = new bool[9];
        private readonly bool[] farmCellDetailDone = new bool[9];
        private readonly int[] woodOriginX = new int[9], woodOriginY = new int[9];
        private readonly int[] farmOriginX = new int[9], farmOriginY = new int[9];
        private readonly int[] woodCandidateScans = new int[9], farmCandidateScans = new int[9];
        private readonly Dictionary<string, int> candidateReasons = new Dictionary<string, int>();
        private readonly int[] hutSpawns = new int[9];
        private readonly int[] initialHuts = new int[9];
        private readonly string[] lastObservedStage = new string[9];
        private readonly List<WallObservation> wallHistory = new List<WallObservation>();
        private readonly Dictionary<string, WallObservation> pendingWalls =
            new Dictionary<string, WallObservation>();
        private readonly HashSet<string> observedWallTargets = new HashSet<string>();
        private int wallHistoryDropped;
        private readonly ActiveAivStep[] activeAivSteps = new ActiveAivStep[9];
        private readonly int[] materializedWalls = new int[9];
        private const int DiagnosticBudgetBytes = 64 * 1024 * 1024;
        private readonly Queue<string> deferredLog = new Queue<string>();
        private int deferredBytes, writtenBytes, droppedLines;
        private bool budgetOverflowLogged;
        private uint[] wallMap;
        private int nextWallMapTick;
        private bool firstWallChangeLogged;
        private bool firstWoodShadowDone;
        private int farmParcelSnapshots, farmParcelSnapshotsDropped;
        private int firstWoodShadowPlayer;
        private int firstWoodShadowActualX, firstWoodShadowActualY;
        private readonly HashSet<string> cellWatchSignatures = new HashSet<string>();
        private int wallEventsLogged;
        private int wallEventsSuppressed;
        private long timelineSequence;
        private bool active;
        private bool firstTick;
        private long sessionId;
        private int lastTick;
        private long observedTickCount;
        private int nextSummaryTick;
        private bool probeSession;
        private bool probePending;
        private bool probeDone;
        private bool probeRunning;
        private long probeAttemptId;
        private long probeSpawnId;
        private int lastPathGeneration;
        private bool pathGenerationKnown;
        private int pathGenerationChanges;
        private bool farmGridPairCaptured;
        private readonly Queue<FarmGridRawSnapshot> farmGridRaw = new Queue<FarmGridRawSnapshot>();
        private int farmGridRawDropped;
        private readonly HashSet<string> detailedParcelStages = new HashSet<string>();
        private readonly HashSet<string> detailedOrchardKinds = new HashSet<string>();
        private readonly Dictionary<string, int> orchardKindCounts = new Dictionary<string, int>();
        private bool firstParcelMismatchDetailed;

        internal AIBuildDiagnoseRuntime(ManualLogSource logger, bool hasFixes,
            bool enablePlacementProbe, bool enableNearbyWoodTest, ulong moduleBase)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            nativeModuleBase = string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
                StringComparison.OrdinalIgnoreCase) ? moduleBase : 0;
            fixesLoaded = hasFixes;
            placementProbeEnabled = enablePlacementProbe;
            nearbyWoodTestEnabled = enableNearbyWoodTest;
            buildingSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn);
            vegetationSubscription = VegetationR3EventHooks.OnVegetationCreate.Observable.Subscribe(OnVegetationCreate);
            buildStructureSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuildStructure);
            wallSubscription = AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(OnAIBuildWall);
            loadingSubscription = Shared.MissionEvents.Loading.Subscribe(OnMapLoading);
            if (ApiShared.Current.TryGetAivBuildStep(AIBuildDiagnosePlugin.Guid,
                out IAivBuildStepCapability steps, out NativeCapabilityDiagnostic diagnostic))
            {
                if (!steps.TryRegisterObserver("wood-build-context", this, out diagnostic))
                    Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            else Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
        }

        private void OnMapLoading(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            if (deferredLog.Count != 0)
            {
                bool wallPreserved = false, shadowPreserved = false;
                foreach (string line in deferredLog)
                {
                    if (!wallPreserved && (line.StartsWith("AI_BUILD_WALL_CHANGE:", StringComparison.Ordinal) ||
                        line.StartsWith("AI_BUILD_WALL_MAP_CHANGE:", StringComparison.Ordinal)))
                    { Log(line); wallPreserved = true; }
                    else if (!shadowPreserved && line.StartsWith("AI_BUILD_WOOD_SHADOW_CHECK:", StringComparison.Ordinal))
                    { Log(line); shadowPreserved = true; }
                    if (wallPreserved && shadowPreserved) break;
                }
                Log($"AI_BUILD_DIAGNOSTIC_MAP_SWITCH_PENDING: session={sessionId}, " +
                    $"unflushedLines={deferredLog.Count}; MissionEvents.Ended normally flushes before this point.");
            }
            active = false;
            nearbyCopySession = nearbyCalibrated = false;
            nearbyCopyApplications = 0;
            nearbyTestDisabled = false;
            nearbyCalibrationAttempts = 0;
            nearbyAlternativeTraces = 0;
            Array.Clear(nearbyDetailedByPlayer, 0, nearbyDetailedByPlayer.Length);
            gridState = 0;
            gridSequence = 0;
            gridModeZeroCalls = gridModeOneCalls = 0;
            lastGridSignature = null;
            firstGridMismatchSeen = false;
            firstGridMismatchLine = null;
            lastTick = -1;
            observedTickCount = 0;
            earlyGridHistory.Clear();
            earlyFarmHistory.Clear();
            appleFarms.Clear();
            appleFarmDropped = appleFarmSnapshots = appleFarmSnapshotDropped = 0;
            orchardTransitions = orchardTransitionsDropped = 0;
            farmParcelSnapshots = farmParcelSnapshotsDropped = 0;
            detailedParcelStages.Clear();
            detailedOrchardKinds.Clear();
            orchardKindCounts.Clear();
            firstParcelMismatchDetailed = false;
            farmGridRaw.Clear();
            farmGridPairCaptured = false;
            farmGridRawDropped = 0;
            pathGenerationKnown = false;
            pathGenerationChanges = 0;
            pendingAppleFarmPre = null;
            firstOrchardMismatchSeen = false;
            coarseAuditWritesEnabled = coarseAuditAvailable = coarseAuditReference = -1;
            coarseAuditSession = coarseAuditSnapshotSession = 0;
            coarseAuditComplete = false;
            coarseAuditProposals.Clear();
            coarseOriginLogged.Clear();
            earlyGridDropped = earlyFarmDropped = 0;
            loadingSaveName = notification.Context.FilePath;
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            sessionId = session.SessionId;
            if (coarseAuditSnapshotSession != sessionId)
            {
                coarseAuditProposals.Clear();
                coarseAuditComplete = false;
                coarseAuditReference = -1;
            }
            coarseOriginLogged.Clear();
            active = !session.IsEditor && !session.IsReplay;
            probeSession = active && session.IsLoadedSave &&
                placementProbeEnabled &&
                string.Equals(Path.GetFileName(session.SaveFileName ?? ""), ProbeSaveName,
                    StringComparison.OrdinalIgnoreCase);
            nearbyCopySession = active && session.IsLoadedSave && nearbyWoodTestEnabled &&
                string.Equals(Path.GetFileName(session.SaveFileName ?? ""), ProbeSaveName,
                    StringComparison.OrdinalIgnoreCase);
            nearbyCalibrated = nearbyTestDisabled = false;
            nearbyCopyApplications = 0;
            nearbyCalibrationAttempts = 0;
            nearbyAlternativeTraces = 0;
            Array.Clear(nearbyDetailedByPlayer, 0, nearbyDetailedByPlayer.Length);
            probePending = probeDone = probeRunning = false;
            probeAttemptId = probeSpawnId = 0;
            firstTick = false;
            lastTick = -1;
            observedTickCount = 0;
            farmGridRaw.Clear();
            farmGridPairCaptured = false;
            farmGridRawDropped = 0;
            pathGenerationChanges = 0;
            lastPathGeneration = ReadPathGeneration();
            pathGenerationKnown = lastPathGeneration >= 0;
            nextSummaryTick = 0;
            seen.Clear();
            attempts.Clear();
            outcomes.Clear();
            routeCauses.Clear();
            nearbyCauses.Clear();
            detailedRoutes.Clear();
            detailedNearby.Clear();
            wallHistory.Clear();
            appleFarms.Clear();
            appleFarmDropped = appleFarmSnapshots = appleFarmSnapshotDropped = 0;
            orchardTransitions = orchardTransitionsDropped = 0;
            farmParcelSnapshots = farmParcelSnapshotsDropped = 0;
            detailedParcelStages.Clear();
            detailedOrchardKinds.Clear();
            orchardKindCounts.Clear();
            firstParcelMismatchDetailed = false;
            pendingAppleFarmPre = null;
            firstOrchardMismatchSeen = false;
            pendingWalls.Clear();
            observedWallTargets.Clear();
            wallHistoryDropped = 0;
            wallEventsLogged = wallEventsSuppressed = 0;
            deferredLog.Clear();
            deferredBytes = writtenBytes = droppedLines = 0;
            budgetOverflowLogged = false;
            wallMap = null;
            nextWallMapTick = 0;
            firstWallChangeLogged = false;
            firstWoodShadowDone = false;
            firstWoodShadowPlayer = 0;
            cellWatchSignatures.Clear();
            timelineSequence = 0;
            Array.Clear(activeAivSteps, 0, activeAivSteps.Length);
            Array.Clear(materializedWalls, 0, materializedWalls.Length);
            Array.Clear(schedulerCalls, 0, schedulerCalls.Length);
            Array.Clear(woodBuildCalls, 0, woodBuildCalls.Length);
            Array.Clear(woodSearchCalls, 0, woodSearchCalls.Length);
            Array.Clear(farmSelectedFrames, 0, farmSelectedFrames.Length);
            Array.Clear(farmSearchCalls, 0, farmSearchCalls.Length);
            Array.Clear(farmTraversals, 0, farmTraversals.Length);
            Array.Clear(farmBuildCalls, 0, farmBuildCalls.Length);
            Array.Clear(appleSpawns, 0, appleSpawns.Length);
            Array.Clear(lastFarmResult, 0, lastFarmResult.Length);
            Array.Clear(beforeFarmPhase, 0, beforeFarmPhase.Length);
            Array.Clear(beforeFarmType, 0, beforeFarmType.Length);
            Array.Clear(activeFarmSearchType, 0, activeFarmSearchType.Length);
            Array.Clear(beforeFarmValid, 0, beforeFarmValid.Length);
            Array.Clear(farmPhaseReached, 0, farmPhaseReached.Length);
            Array.Clear(applePhaseReached, 0, applePhaseReached.Length);
            Array.Clear(woodCellDetailDone, 0, woodCellDetailDone.Length);
            Array.Clear(farmCellDetailDone, 0, farmCellDetailDone.Length);
            Array.Clear(woodOriginX, 0, woodOriginX.Length);
            Array.Clear(woodOriginY, 0, woodOriginY.Length);
            Array.Clear(farmOriginX, 0, farmOriginX.Length);
            Array.Clear(farmOriginY, 0, farmOriginY.Length);
            Array.Clear(woodCandidateScans, 0, woodCandidateScans.Length);
            Array.Clear(farmCandidateScans, 0, farmCandidateScans.Length);
            candidateReasons.Clear();
            Array.Clear(hutSpawns, 0, hutSpawns.Length);
            Array.Clear(initialHuts, 0, initialHuts.Length);
            Array.Clear(lastObservedStage, 0, lastObservedStage.Length);
            Log($"AI_BUILD_SESSION: session={sessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, " +
                $"file={session.SaveFileName}, fixesLoaded={fixesLoaded}, active={active}, " +
                $"coarseAuditTestLoaded={Chainloader.PluginInfos.ContainsKey("AICoarsePathComponentFixTest_Serp")}, " +
                $"coarseAuditWritesEnabled={(coarseAuditSession == sessionId ? coarseAuditWritesEnabled : -1)}, " +
                $"coarseAuditAvailable={(coarseAuditSession == sessionId ? coarseAuditAvailable : -1)}, " +
                $"mode={session.Mode.ToDiagnosticString()}.");
            Log($"AI_BUILD_GRID_HISTORY: session={sessionId}, loadingFile={loadingSaveName ?? "unknown"}, " +
                $"sessionFile={session.SaveFileName}, retained={earlyGridHistory.Count}, " +
                $"dropped={earlyGridDropped}, mode0Calls={gridModeZeroCalls}, mode1Calls={gridModeOneCalls}, " +
                $"pathGeneration={lastPathGeneration}, generationReady={pathGenerationKnown}.");
            foreach (string entry in earlyGridHistory) Log("AI_BUILD_GRID_EARLY: session=" + sessionId + ", " + entry);
            if (firstGridMismatchLine != null && earlyGridDropped != 0)
                Log("AI_BUILD_GRID_FIRST_MISMATCH_PRESERVED: session=" + sessionId + ", " + firstGridMismatchLine);
            foreach (string entry in earlyFarmHistory) Log("AI_BUILD_APPLEFARM_EARLY: session=" + sessionId + ", " + entry);
            if (earlyFarmDropped != 0)
                Log($"AI_BUILD_APPLEFARM_EARLY_OVERFLOW: session={sessionId}, dropped={earlyFarmDropped}.");
            earlyGridHistory.Clear();
            earlyFarmHistory.Clear();
            Log($"AI_BUILD_PROBE_ARMED: session={sessionId}, armed={probeSession}, " +
                $"requiredSave={ProbeSaveName}, player={ProbePlayer}, target=({ProbeX},{ProbeY}).");
            Log($"AI_BUILD_NEARBY_TEST_ARMED: session={sessionId}, armed={nearbyCopySession}, " +
                $"configured={nearbyWoodTestEnabled}, copy={ProbeSaveName}, calibrationRequired=true.");
            if (active)
            {
                CaptureInitialAppleFarms();
                CaptureInitialHuts();
                LogPlayers("start");
                CaptureWallMap("session-start");
                SnapshotWoodCells("session-start");
            }
        }

        internal void OnSessionEnded()
        {
            FlushFarmGridRaw();
            FlushDeferredLog();
            if (active) LogSummary("end");
            if (active)
                Log($"AI_BUILD_APPLEFARM_OBSERVER_SUMMARY: session={sessionId}, tracked={appleFarms.Count}, " +
                    $"dropped={appleFarmDropped}, snapshots={appleFarmSnapshots}, " +
                $"snapshotDropped={appleFarmSnapshotDropped}.");
            if (active)
                Log($"AI_BUILD_ORCHARD_TRANSITION_SUMMARY: session={sessionId}, logged={orchardTransitions}, " +
                    $"compact={orchardTransitionsDropped}, kinds={string.Join("|", orchardKindCounts)}.");
            if (active)
                Log($"AI_BUILD_SOURCE_WATCH_SUMMARY: session={sessionId}, pathGenerationChanges={pathGenerationChanges}, " +
                    $"farmGridPairCaptured={farmGridPairCaptured}, rawSnapshotsDropped={farmGridRawDropped}, " +
                    $"parcelFull={farmParcelSnapshots}, parcelCompact={farmParcelSnapshotsDropped}.");
            active = false;
            probeSession = probePending = probeRunning = false;
            nearbyCopySession = false;
            gridState = 0;
            pendingAppleFarmPre = null;
            Log($"AI_BUILD_DIAGNOSTIC_BUDGET: session={sessionId}, writtenBytes={writtenBytes}, " +
                $"queuedBytes={deferredBytes}, droppedLines={droppedLines}, limitBytes={DiagnosticBudgetBytes}.");
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            lastTick = tick;
            observedTickCount++;
            FlushDeferredLog();
            FlushFarmGridRaw();
            ObservePathGeneration(tick);
            if (tick >= nextWallMapTick)
            {
                CaptureWallMap("periodic");
                nextWallMapTick = tick + 50;
            }
            foreach (AppleFarmWatch farm in appleFarms)
            {
                if (observedTickCount <= farm.OrchardWatchUntilTick)
                    ObserveOrchardTransitions(farm);
                if (!farm.IsNew || farm.FiveTickDone || observedTickCount < farm.DueTickCount)
                    continue;
                farm.FiveTickDone = true;
                try
                {
                    CaptureAppleFarm(farm, "five-ticks-after-spawn");
                    CaptureOrchardStage(farm.X, farm.Y, farm.BuildingId,
                        "five-ticks-after-spawn");
                }
                catch (Exception ex) { Log("AI_BUILD_APPLEFARM_TICK_CAPTURE_FAILED: " + ex); }
            }
            if (gridState != 0)
                ObserveGrid("tick", AiBuildDiagnostic.CaptureEconomyGridEvidence(gridState, -1));
            if (probePending)
            {
                try { RunPlacementProbe(); }
                catch (Exception ex)
                {
                    probePending = false;
                    probeDone = true;
                    probeRunning = false;
                    Log("AI_BUILD_PROBE_FAILED_CLOSED: " + ex);
                }
            }
            if (!firstTick)
            {
                firstTick = true;
                Log($"AI_BUILD_POST_STARTUP_TICK: session={sessionId}, tick={tick}.");
                LogPlayers("first-tick");
                SnapshotWoodCells("first-tick");
            }
            if (tick >= nextSummaryTick)
            {
                LogSummary("periodic");
                nextSummaryTick = tick + 500;
            }
        }

        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            if (record.Stage == "coarse-generation")
            {
                long sequence = ++timelineSequence;
                Log($"AI_BUILD_TIMELINE: session={sessionId}, sequence={sequence}, tick={record.C}, " +
                    $"event=pcl-generation, previous={record.A}, current={record.B}, sourceSession={record.D}.");
                if (active) SnapshotWoodCells("pcl-generation-" + record.B);
                return;
            }
            if (record.Stage == "coarse-audit-begin")
            {
                coarseAuditProposals.Clear();
                coarseAuditSnapshotSession = record.A;
                coarseAuditComplete = false;
                return;
            }
            if (record.Stage == "coarse-audit-cell")
            {
                if (record.A >= 0 && record.A < 25600)
                    coarseAuditProposals[(int)record.A] =
                        $"storedAtAudit={record.B}:hypotheticalAtAudit={record.C}";
                return;
            }
            if (record.Stage == "coarse-audit-summary")
            {
                coarseAuditReference = (int)record.A;
                coarseAuditSnapshotSession = record.D;
                coarseAuditComplete = true;
                return;
            }
            if (record.Stage == "coarse-audit-session")
            {
                coarseAuditWritesEnabled = (int)record.A;
                coarseAuditAvailable = (int)record.B;
                coarseAuditSession = record.C;
                return;
            }
            if (record.Stage == "wood-candidate-scan-request" ||
                record.Stage == "farm-candidate-scan-request")
            {
                bool wood = record.Stage == "wood-candidate-scan-request";
                int playerId = record.PlayerId;
                if (active && playerId >= 1 && playerId <= 8 &&
                    (wood ? woodCandidateScans[playerId]++ < 3 : farmCandidateScans[playerId]++ < 2))
                    ScanCandidateCells(unchecked((ulong)record.A), playerId, wood);
                return;
            }
            if (record.EconomyGridEvidence != null)
            {
                ObserveGrid(record.Stage, record.EconomyGridEvidence);
                if (active && !farmGridPairCaptured && appleFarms.Count != 0 &&
                    (record.Stage == "economy-grid-before" || record.Stage == "economy-grid-after"))
                    CaptureFarmGridRaw(record.Stage, record.EconomyGridEvidence);
                if (active && (record.Stage == "economy-grid-before" ||
                    record.Stage == "economy-grid-after"))
                    SnapshotWoodCells(record.Stage);
                if (active && record.Stage == "economy-grid-after")
                    ObserveAppleFarmsAfterGridUpdate(record.EconomyGridEvidence.Mode);
                return;
            }
            if (!active || record.PlayerId < 1 || record.PlayerId > 8) return;
            if (record.Stage == "wood-candidate-origin")
            {
                woodOriginX[record.PlayerId] = (int)record.A;
                woodOriginY[record.PlayerId] = (int)record.B;
                LogCoarseOrigin(record.PlayerId, "wood", (int)record.A, (int)record.B,
                    "failed-search-overlay-snapshot");
            }
            if (record.Stage == "farm-candidate-origin")
            {
                farmOriginX[record.PlayerId] = (int)record.A;
                farmOriginY[record.PlayerId] = (int)record.B;
                LogCoarseOrigin(record.PlayerId, "farm", (int)record.A, (int)record.B,
                    "failed-search-overlay-snapshot");
            }
            if (record.Stage == "wood-candidate-cell" || record.Stage == "farm-candidate-cell")
            {
                ObserveCandidateCell(record);
                return;
            }
            if (record.Stage == "wood-candidate-scan") woodCellDetailDone[record.PlayerId] = true;
            if (record.Stage == "farm-candidate-scan") farmCellDetailDone[record.PlayerId] = true;
            if (record.Stage == "farm-scheduler-before")
            {
                beforeFarmValid[record.PlayerId] = true;
                beforeFarmPhase[record.PlayerId] = (int)record.D;
                beforeFarmType[record.PlayerId] = (int)record.C;
                if (record.C == (long)eStructs.STRUCT_APPLEFARM)
                    farmSelectedFrames[record.PlayerId]++;
            }
            if (record.Stage == "farm-scheduler-after" &&
                beforeFarmValid[record.PlayerId] &&
                beforeFarmPhase[record.PlayerId] == 0 && record.D == 1)
            {
                farmPhaseReached[record.PlayerId]++;
                if (beforeFarmType[record.PlayerId] == (int)eStructs.STRUCT_APPLEFARM)
                {
                    if (applePhaseReached[record.PlayerId] == 0)
                        LogResources(record.PlayerId, "first-apple-farm-phase");
                    applePhaseReached[record.PlayerId]++;
                }
            }
            if (record.Stage == "farm-scheduler-after") beforeFarmValid[record.PlayerId] = false;
            if (record.Stage == "farm-search-before")
            {
                activeFarmSearchType[record.PlayerId] = (int)record.A;
                if (record.A == (long)eStructs.STRUCT_APPLEFARM)
                    farmSearchCalls[record.PlayerId]++;
            }
            if (record.Stage == "farm-search-after" &&
                activeFarmSearchType[record.PlayerId] == (int)eStructs.STRUCT_APPLEFARM)
                lastFarmResult[record.PlayerId] = (int)record.A;
            if (record.Stage == "wood-search-after")
            {
                LogFirstSearchOrigin(record.PlayerId, "wood", "post-search-shared-origin");
                SnapshotWoodCells("wood-search-after-player-" + record.PlayerId);
                if (!firstWoodShadowDone &&
                    ((loadingSaveName ?? "").IndexOf("test_canari_nowoodcutters",
                        StringComparison.OrdinalIgnoreCase) < 0 || record.PlayerId == 6))
                    CaptureWoodSearchShadow(record.PlayerId, (int)record.B, (int)record.C);
            }
            if (record.Stage == "farm-search-generation" &&
                activeFarmSearchType[record.PlayerId] == (int)eStructs.STRUCT_APPLEFARM &&
                record.A != record.B)
            {
                farmTraversals[record.PlayerId]++;
                LogFirstSearchOrigin(record.PlayerId, "farm", "traversal-confirmed-post-search");
            }
            lastObservedStage[record.PlayerId] = record.Stage;
            switch (record.Stage)
            {
                case "scheduler-before": schedulerCalls[record.PlayerId]++; break;
                case "wood-build-before":
                    SnapshotWoodCells("wood-build-before-player-" + record.PlayerId);
                    if (woodBuildCalls[record.PlayerId]++ == 0)
                        LogResources(record.PlayerId, "first-wood-build");
                    break;
                case "wood-search-before": woodSearchCalls[record.PlayerId]++; break;
            }
            Attempt attempt = null;
            if (record.AttemptId != 0)
            {
                if (record.Stage == "wood-build-before")
                    attempts[record.AttemptId] = new Attempt(record.PlayerId);
                attempts.TryGetValue(record.AttemptId, out attempt);
                if (attempt != null)
                {
                    if (record.Stage == "route-result") { attempt.RouteSeen = true; attempt.RouteResult = (int)record.A; }
                    if (record.Stage == "route-evidence") attempt.RouteEvidence = record.RouteEvidence;
                    if (record.Stage == "wood-nearby-path-before")
                    {
                        attempt.NearbyBefore = record.NearbyPathEvidence;
                        PrepareNearbyWoodShadow(record.AttemptId, attempt);
                    }
                    if (record.Stage == "wood-nearby-path-after")
                    {
                        attempt.NearbyAfter = record.NearbyPathEvidence;
                        CompleteNearbyWoodShadow(record.AttemptId, attempt);
                    }
                    if (record.Stage == "wood-search-after") attempt.SearchX = (int)record.B;
                    if (record.Stage == "wood-nearby-after") attempt.NearX = (int)record.A;
                    if (record.Stage != "wood-build-after") attempt.LastStep = record.Stage;
                }
            }
            string key = record.PlayerId + ":" + record.Stage + ":" + record.A + ":" + record.B + ":" + record.C + ":" + record.D;
            if (!seen.TryGetValue(key, out int count)) count = 0;
            seen[key] = count + 1;
            // First occurrence of each exact state is retained; periodic repeats show persistence.
            if (count < 2 || count == 9 || count == 99 || count % 500 == 499)
                Log($"AI_BUILD_TRACE: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                    $"attempt={record.AttemptId}, stage={record.Stage}, {Describe(record)}, repeat={count + 1}.");
            if (record.Stage == "wood-build-after" && attempt != null)
            {
                SnapshotWoodCells("wood-build-after-player-" + record.PlayerId);
                string outcome = Classify(attempt);
                string routeCause = attempt.RouteSeen && attempt.RouteResult == 0
                    ? AnalyzeRoute(attempt.RouteEvidence) :
                    (attempt.RouteSeen ? "not-rejected" : "unobserved");
                string nearbyCause = AnalyzeNearby(attempt);
                if (attempt.NearbyAfter != null)
                    LogNearbyEvidence(record.PlayerId, record.AttemptId, attempt, nearbyCause);
                if (probeSession && !probeDone && !probePending &&
                    record.PlayerId == ProbePlayer && attempt.RouteSeen &&
                    attempt.RouteResult == 0 && attempt.NearbyAfter != null &&
                    attempt.NearbyAfter.ResultX * 5 == ProbeX &&
                    attempt.NearbyAfter.ResultY * 5 == ProbeY)
                {
                    probeAttemptId = record.AttemptId;
                    probePending = true;
                    Log($"AI_BUILD_PROBE_QUEUED: session={sessionId}, tick={lastTick}, " +
                        $"attempt={probeAttemptId}, nextTick=true.");
                }
                if (attempt.RouteSeen && attempt.RouteResult == 0)
                {
                    string causeKey = record.PlayerId + ":" + routeCause;
                    routeCauses.TryGetValue(causeKey, out int causeCount);
                    routeCauses[causeKey] = causeCount + 1;
                    LogRouteEvidence(record.PlayerId, record.AttemptId, attempt.RouteEvidence, routeCause);
                }
                string outcomeKey = record.PlayerId + ":" + outcome;
                outcomes.TryGetValue(outcomeKey, out int outcomeCount);
                outcomes[outcomeKey] = ++outcomeCount;
                if (outcomeCount <= 2 || outcomeCount == 10 || outcomeCount % 100 == 0)
                    Log($"AI_BUILD_ATTEMPT: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                        $"attempt={record.AttemptId}, observedLast={attempt.LastStep}, inference={outcome}, " +
                        $"routeCause={routeCause}, " +
                        $"nearbyCause={nearbyCause}, " +
                        $"route={(attempt.RouteSeen ? attempt.RouteResult.ToString() : "unobserved")}, " +
                        $"buildPre={attempt.BuildPre}, buildPost={attempt.BuildPost}, " +
                        $"spawnPre={attempt.SpawnPre}, spawnPost={attempt.SpawnPost}, spawnId={attempt.SpawnId}.");
                attempts.Remove(record.AttemptId);
            }
        }

        private void LogFirstSearchOrigin(int playerId, string kind, string phase)
        {
            int key = playerId * 2 + (kind == "wood" ? 1 : 0);
            if (coarseOriginLogged.Contains(key)) return;
            try
            {
                AivSystem* state = GameAIVManagerAPI.Instance.GetAIVSystemPointer();
                if (state == null) throw new InvalidOperationException("AIV state unavailable");
                long address = checked((long)(ulong)state);
                int x = Marshal.ReadInt32(new IntPtr(checked(address + 0x18783C)));
                int y = Marshal.ReadInt32(new IntPtr(checked(address + 0x1A083C)));
                if ((uint)x >= 160 || (uint)y >= 160)
                    throw new InvalidOperationException("search origin outside coarse grid");
                if (kind == "wood") { woodOriginX[playerId] = x; woodOriginY[playerId] = y; }
                else { farmOriginX[playerId] = x; farmOriginY[playerId] = y; }
                LogCoarseOrigin(playerId, kind, x, y, phase);
            }
            catch (Exception ex)
            {
                coarseOriginLogged.Add(key);
                Log($"AI_BUILD_COARSE_ORIGIN_UNAVAILABLE: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, kind={kind}, phase={phase}, reason={ex.GetType().Name}.");
            }
        }

        private void LogCoarseOrigin(int playerId, string kind, int x, int y, string phase)
        {
            int key = playerId * 2 + (kind == "wood" ? 1 : 0);
            if (!coarseOriginLogged.Add(key)) return;
            var details = new StringBuilder();
            int[] dx = { 0, -1, 0, 0, 1 };
            int[] dy = { 0, 0, -1, 1, 0 };
            for (int i = 0; i < dx.Length; i++)
            {
                int cx = x + dx[i], cy = y + dy[i];
                if (i != 0) details.Append('|');
                if ((uint)cx >= 160 || (uint)cy >= 160)
                {
                    details.Append($"({cx},{cy}):outside-grid");
                    continue;
                }
                int index = cx * 160 + cy;
                coarseAuditProposals.TryGetValue(index, out string proposal);
                AiEconomyGridEvidence current = AiBuildDiagnostic.CaptureEconomyGridEvidence(cx, cy);
                var positive = new SortedDictionary<int, int>();
                int zero = 0, valid = 0, matchingGlobal = 0;
                foreach (AiPathTileSample tile in current.Tiles)
                {
                    if (tile.Status != "ok") continue;
                    valid++;
                    if (tile.NativeComponent == 0) zero++;
                    else if (tile.NativeComponent > 0)
                    {
                        positive.TryGetValue(tile.NativeComponent, out int count);
                        positive[tile.NativeComponent] = count + 1;
                    }
                    if (tile.NativeComponent == coarseAuditReference) matchingGlobal++;
                }
                var components = new StringBuilder();
                foreach (var pair in positive)
                {
                    if (components.Length != 0) components.Append(',');
                    components.Append(pair.Key).Append('x').Append(pair.Value);
                }
                bool hasAudit = coarseAuditComplete && coarseAuditSnapshotSession == sessionId &&
                    coarseAuditReference > 0;
                string hypothetical = hasAudit && valid == 25
                    ? (25 - matchingGlobal).ToString() : "unobserved";
                string auditProposal = hasAudit
                    ? (proposal ?? "no-difference-at-last-audit") : "unobserved";
                details.Append($"({cx},{cy}):status={current.Status}:storedVanilla={current.StoredForeignCount}:" +
                    $"storedReservation={ReadCoarseReservation(current)}:bit4Tiles={CountReservationTiles(current)}:" +
                    $"hypotheticalGlobalLive={hypothetical}:zero={zero}:positive={components}:" +
                    $"validTiles={valid}:auditProposal={auditProposal}");
            }
            Log($"AI_BUILD_COARSE_SEARCH_ORIGIN: session={sessionId}, tick={lastTick}, player={playerId}, " +
                $"kind={kind}, phase={phase}, origin=({x},{y}), " +
                $"auditTestLoaded={Chainloader.PluginInfos.ContainsKey("AICoarsePathComponentFixTest_Serp")}, " +
                $"auditWritesEnabled={(coarseAuditSession == sessionId ? coarseAuditWritesEnabled : -1)}, " +
                $"hypotheticalGlobalReference={(coarseAuditComplete ? coarseAuditReference : -1)}, " +
                $"cells={details}.");
        }

        private void ObserveCandidateCell(AiBuildDiagnosticRecord record)
        {
            bool wood = record.Stage == "wood-candidate-cell";
            ulong packed = unchecked((ulong)record.C);
            int foreign = unchecked((sbyte)(byte)packed);
            int b1 = (byte)(packed >> 8);
            int b2 = (sbyte)(packed >> 16);
            int b3 = (byte)(packed >> 24);
            string reason;
            string raw;
            bool origin = record.A == (wood ? woodOriginX[record.PlayerId] : farmOriginX[record.PlayerId]) &&
                record.B == (wood ? woodOriginY[record.PlayerId] : farmOriginY[record.PlayerId]);
            if (wood)
            {
                int adjustment = (byte)(packed >> 32);
                reason = origin ? "origin-seed-not-tested-as-neighbor" :
                    foreign >= 16 ? "not-traversed-foreign" :
                    b3 != 0 ? "not-traversed-retry" :
                    foreign >= 6 ? "traversed-no-candidate-foreign" :
                    b2 <= 0 ? "traversed-no-candidate-tree" : "candidate";
                raw = $"foreign={foreign}, depth={b1}, treeWeight={b2}, retry={b3}, scoreAdjustment={adjustment}";
            }
            else
            {
                int flags91 = (sbyte)(packed >> 32);
                int flags90 = (sbyte)(packed >> 40);
                int playerMask = (byte)(packed >> 48);
                int depth = (byte)(packed >> 56);
                reason = origin ? "origin-seed-not-tested-as-neighbor" :
                    foreign >= 17 ? "not-traversed-foreign" :
                    foreign != 0 ? "traversed-no-place-foreign" :
                    b1 != 0 ? "traversed-no-place-reservation" :
                    b2 != 0 ? "traversed-no-place-tree" :
                    b3 != 0 ? "traversed-no-place-retry" :
                    flags91 <= 24 ? "traversed-no-place-flags91" :
                    flags90 <= 13 ? "traversed-no-place-flags90" :
                    "local-gates-passed-mask-or-footprint-unresolved";
                raw = $"foreign={foreign}, reservation={b1}, treeWeight={b2}, retry={b3}, " +
                    $"flags91={flags91}, flags90={flags90}, playerMask={playerMask}, depth={depth}";
            }
            string key = (wood ? "wood" : "farm") + ":" + record.PlayerId + ":" + reason;
            candidateReasons.TryGetValue(key, out int count);
            candidateReasons[key] = count + 1;
            if (wood ? woodCellDetailDone[record.PlayerId] : farmCellDetailDone[record.PlayerId]) return;
            Log($"AI_BUILD_CANDIDATE_CELL: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                $"attempt={record.AttemptId}, kind={(wood ? "wood" : "farm")}, cell=({record.A},{record.B}), " +
                $"{raw}, liveDifferent={record.D}, cellGate={reason}, " +
                $"storedVsLiveMismatch={(record.D >= 0 && record.D != foreign)}.");
        }

        private void ObserveGrid(string stage, AiEconomyGridEvidence evidence)
        {
            if (evidence == null) return;
            if (stage == "economy-grid-before")
            {
                if (evidence.Mode == 0) gridModeZeroCalls++;
                else if (evidence.Mode == 1) gridModeOneCalls++;
            }
            if (evidence.State != 0 && evidence.Status == "ok") gridState = evidence.State;
            string signature = GridSignature(evidence);
            bool changed = !string.Equals(signature, lastGridSignature, StringComparison.Ordinal);
            bool mismatch = evidence.Status == "ok" &&
                evidence.StoredForeignCount != evidence.CurrentDifferentCount;
            bool firstMismatch = mismatch && !firstGridMismatchSeen;
            if (firstMismatch) firstGridMismatchSeen = true;
            bool force = evidence.Mode == 1 ||
                (evidence.Mode == 0 && gridModeZeroCalls == 1) || firstMismatch;
            if (!changed && !force) return;
            lastGridSignature = signature;
            gridSequence++;
            string line = $"seq={gridSequence}, tick={lastTick}, stage={stage}, mode={evidence.Mode}, " +
                $"state=0x{evidence.State:X}, status={evidence.Status}, " +
                $"reference={evidence.ReferenceComponent}, storedForeign={evidence.StoredForeignCount}, " +
                $"storedReservation={ReadCoarseReservation(evidence)}, bit4Tiles={CountReservationTiles(evidence)}, " +
                $"liveDifferent={evidence.CurrentDifferentCount}, liveZero={evidence.CurrentZeroCount}, " +
                $"treeFlagTiles={evidence.TreeFlagCount}, appleFarmFlagTiles={evidence.AppleFarmFlagCount}, " +
                $"storedTreeWeight={evidence.TreeWeight}, mismatch={mismatch}, firstMismatch={firstMismatch}, " +
                $"changed={changed}, tileValues={GridTiles(evidence)}; " +
                "treeWeightIsRawVanillaValue-not-a-tree-flag-count.";
            if (firstMismatch) firstGridMismatchLine = line;
            if (active) Log("AI_BUILD_GRID_TRANSITION: session=" + sessionId + ", " + line);
            else
            {
                if (earlyGridHistory.Count == GridHistoryLimit)
                {
                    earlyGridHistory.RemoveAt(0);
                    earlyGridDropped++;
                }
                earlyGridHistory.Add(line);
            }
        }

        private static string GridSignature(AiEconomyGridEvidence evidence)
        {
            var value = new StringBuilder(330);
            value.Append(evidence.Status).Append(':').Append(evidence.ReferenceComponent)
                .Append(':').Append(evidence.StoredForeignCount).Append(':')
                .Append(ReadCoarseReservation(evidence)).Append(':')
                .Append(evidence.TreeWeight);
            foreach (AiPathTileSample tile in evidence.Tiles)
                value.Append('|').Append(tile.X).Append(',').Append(tile.Y).Append(',')
                    .Append(tile.NativeComponent).Append(',').Append(tile.ApiComponent)
                    .Append(',').Append(tile.PropertyFlags).Append(',').Append(tile.Organism);
            return value.ToString();
        }

        // Native AIV cell +15 (foreign-byte-relative +11, 0x5B83F) is also
        // written by AI build attempts;
        // it is a raw search gate, not an inferred count of bit-4 tiles.
        private static int ReadCoarseReservation(AiEconomyGridEvidence evidence)
        {
            if (evidence == null || evidence.State == 0 ||
                (uint)evidence.CoarseX >= 160 || (uint)evidence.CoarseY >= 160)
                return -1;
            try
            {
                long address = checked((long)evidence.State + 0x5B83F +
                    ((long)evidence.CoarseX * 160 + evidence.CoarseY) * 0x30);
                return Marshal.ReadByte(new IntPtr(address));
            }
            catch { return -1; }
        }

        private static int CountReservationTiles(AiEconomyGridEvidence evidence)
        {
            if (evidence == null || evidence.Tiles.Count != 25) return -1;
            int count = 0;
            foreach (AiPathTileSample tile in evidence.Tiles)
            {
                if (tile.Status != "ok") return -1;
                if ((tile.PropertyFlags & NativePlacementReservationFlag) != 0) count++;
            }
            return count;
        }

        private static string GridTiles(AiEconomyGridEvidence evidence)
        {
            var value = new StringBuilder(300);
            foreach (AiPathTileSample tile in evidence.Tiles)
            {
                if (value.Length != 0) value.Append('|');
                value.Append(tile.X).Append(',').Append(tile.Y).Append(':')
                    .Append(tile.NativeComponent).Append('/').Append(tile.ApiComponent)
                    .Append(':').Append(tile.PropertyFlags.ToString("X8"))
                    .Append(':').Append(tile.Organism)
                    .Append(':').Append(tile.BuildingId)
                    .Append(':').Append(tile.Status);
            }
            return value.ToString();
        }

        private void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (active && args.Mappers == eMappers.MAPPER_APPLEFARM &&
                args.PlayerId >= 1 && args.PlayerId <= 8)
            {
                if (args.Phase == EventHookPhase.Pre) farmBuildCalls[args.PlayerId]++;
                string farmPlacement = AiBuildDiagnostic.TryReadPlacementStatus(out int farmPreparation,
                    out int farmRejected, out int farmMode)
                    ? $"placementPreparation={farmPreparation}, placementRejected={farmRejected}, placementMode={farmMode}"
                    : "placementStatus=unavailable";
                string farmReason = FormatNativePlacementReason();
                Log($"AI_BUILD_FARM_STRUCTURE: session={sessionId}, tick={lastTick}, " +
                    $"player={args.PlayerId}, phase={args.Phase}, tile=({args.TileX},{args.TileY}), " +
                    $"mapper={args.Mappers}, scale={args.BuildingScaleUnknown}, free={args.IsFree}, " +
                    $"{ReadResources(args.PlayerId)}, {farmPlacement}, {farmReason}; postEventReturnValueNotAuthoritative=true.");
                CaptureFarmParcel(args.TileX, args.TileY, 0, "build-structure-" + args.Phase);
            }
            if (probeRunning && args.PlayerId == ProbePlayer &&
                args.Mappers == eMappers.MAPPER_WOODSMAN)
            {
                Log($"AI_BUILD_PROBE_STRUCTURE: session={sessionId}, attempt={probeAttemptId}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), " +
                    $"scale={args.BuildingScaleUnknown}, free={args.IsFree}.");
                return;
            }
            if (!active || args.Mappers != eMappers.MAPPER_WOODSMAN ||
                !AiBuildDiagnostic.TryGetCurrentWoodAttempt(out long id, out int owner) ||
                owner != args.PlayerId || !attempts.TryGetValue(id, out Attempt attempt)) return;
            if (args.Phase == EventHookPhase.Pre) attempt.BuildPre = true;
            else if (args.Phase == EventHookPhase.Post) attempt.BuildPost = true;
            attempt.LastStep = "build-structure-" + args.Phase;
            string placement = AiBuildDiagnostic.TryReadPlacementStatus(out int preparation,
                out int rejected, out int mode)
                ? $"placementPreparation={preparation}, placementRejected={rejected}, placementMode={mode}"
                : "placementStatus=unavailable";
            string nativeReason = FormatNativePlacementReason();
            try
            {
                int cost = GameBuildingManagerAPI.Instance.GetWoodCost(eStructs.STRUCT_WOODCUTTERS_HUT);
                Log($"AI_BUILD_STRUCTURE: session={sessionId}, tick={lastTick}, player={owner}, attempt={id}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), mapper={args.Mappers}, " +
                    $"scale={args.BuildingScaleUnknown}, free={args.IsFree}, woodCost={cost}, " +
                    $"{ReadResources(owner)}, {placement}, {nativeReason}.");
            }
            catch (Exception ex) { Log("AI_BUILD_STRUCTURE_OBSERVATION_FAILED: " + ex); }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Building == eStructs.STRUCT_APPLEFARM)
            {
                if (active && args.Phase == EventHookPhase.Pre)
                {
                    pendingAppleFarmX = args.TileX;
                    pendingAppleFarmY = args.TileY;
                    pendingAppleFarmPlayer = args.PlayerId;
                    pendingAppleFarmPre = $"({args.TileX},{args.TileY})/player-{args.PlayerId}";
                    CaptureOrchardStage(args.TileX, args.TileY, 0, "building-spawn-pre");
                }
                string entry = $"tick={lastTick}, phase={args.Phase}, player={args.PlayerId}, " +
                    $"tile=({args.TileX},{args.TileY}), " +
                    $"buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}; " +
                    "eventDoesNotProveTreeCreation.";
                if (active) Log("AI_BUILD_APPLEFARM: session=" + sessionId + ", " + entry);
                else
                {
                    if (earlyFarmHistory.Count == GridHistoryLimit)
                    {
                        earlyFarmHistory.RemoveAt(0);
                        earlyFarmDropped++;
                    }
                    earlyFarmHistory.Add(entry);
                }
                if (active && args.Phase == EventHookPhase.Post && args.ReturnValue > 0 &&
                    args.ReturnValue <= int.MaxValue)
                {
                    if (args.PlayerId >= 1 && args.PlayerId <= 8) appleSpawns[args.PlayerId]++;
                    TrackAppleFarm((int)args.ReturnValue, args.PlayerId, args.TileX, args.TileY,
                        true, "spawn-post");
                }
                if (active && args.Phase == EventHookPhase.Post)
                {
                    CaptureOrchardStage(args.TileX, args.TileY,
                        args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue
                            ? (int)args.ReturnValue : 0,
                        "building-spawn-post");
                    pendingAppleFarmPre = null;
                }
            }
            if (!active ||
                args.Building != eStructs.STRUCT_WOODCUTTERS_HUT ||
                args.PlayerId < 1 || args.PlayerId > 8) return;
            if (probeRunning && args.PlayerId == ProbePlayer)
            {
                if (args.Phase == EventHookPhase.Post) probeSpawnId = args.ReturnValue;
                Log($"AI_BUILD_PROBE_SPAWN: session={sessionId}, attempt={probeAttemptId}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), " +
                    $"buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}.");
                return;
            }
            long id = 0;
            if (AiBuildDiagnostic.TryGetCurrentWoodAttempt(out long current, out int owner) &&
                owner == args.PlayerId && attempts.TryGetValue(current, out Attempt attempt))
            {
                id = current;
                if (args.Phase == EventHookPhase.Pre) attempt.SpawnPre = true;
                else if (args.Phase == EventHookPhase.Post)
                {
                    attempt.SpawnPost = true;
                    attempt.SpawnId = args.ReturnValue;
                }
                attempt.LastStep = "building-spawn-" + args.Phase;
            }
            if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0) hutSpawns[args.PlayerId]++;
            Log($"AI_BUILD_WOODCUTTER_SPAWN: session={sessionId}, tick={lastTick}, player={args.PlayerId}, " +
                $"attempt={id}, phase={args.Phase}, buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}, " +
                $"tile=({args.TileX},{args.TileY}).");
        }

        private void OnVegetationCreate(VegetationCreateEventArgs args)
        {
            if (!active || args.VegetationType != VegetationType.AppleTree) return;
            int farmId = 0, owner = 0, offset = -1;
            if (pendingAppleFarmPre != null &&
                TryGetOrchardOffset(pendingAppleFarmX, pendingAppleFarmY,
                    args.TileX, args.TileY, out offset))
                owner = pendingAppleFarmPlayer;
            else
            {
                foreach (AppleFarmWatch farm in appleFarms)
                    if (TryGetOrchardOffset(farm.X, farm.Y, args.TileX, args.TileY,
                        out offset))
                    {
                        farmId = farm.BuildingId;
                        owner = farm.PlayerId;
                        break;
                    }
            }
            if (offset < 0) return;
            try
            {
                AiPathTileSample tile = ReadOrchardTile(args.TileX, args.TileY);
                AiEconomyGridEvidence cell = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                    args.TileX / 5, args.TileY / 5);
                bool mismatch = cell.Status == "ok" &&
                    cell.StoredForeignCount != cell.CurrentDifferentCount;
                LogOrchardTransition("vegetation-create-" + args.Phase,
                    () => $"source=vegetation-create-{args.Phase}, farm={farmId}, " +
                    $"owner={owner}, offset={offset}, vegetationId=" +
                    (args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending") +
                    $", growthStage={args.GrowthStage}, tile={DescribeOrchardTile(tile)}, " +
                    $"cell=({cell.CoarseX},{cell.CoarseY}), storedForeign={cell.StoredForeignCount}, " +
                    $"liveDifferent={cell.CurrentDifferentCount}, mismatch={mismatch}", mismatch);
            }
            catch (Exception ex) { Log("AI_BUILD_ORCHARD_VEGETATION_FAILED: " + ex); }
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
                            (before.PropertyFlags & 0x100u) == 0 &&
                            (wall.PropertyFlags & 0x100u) != 0;
                        wall.Change = DescribeWallChange(before, wall);
                        wall.Before = before;
                        pendingWalls.Remove(key);
                    }
                    else wall.Change = "unpaired-post";
                }
                if (wallHistory.Count == 1024) { wallHistory.RemoveAt(0); wallHistoryDropped++; }
                wallHistory.Add(wall);
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
                foreach (string target in observedWallTargets)
                {
                    string[] parts = target.Split(':');
                    if (Math.Abs(wall.X - int.Parse(parts[0])) <= 12 &&
                        Math.Abs(wall.Y - int.Parse(parts[1])) <= 12)
                    {
                        LogWall(wall, "live-near-wood-target", 0);
                        break;
                    }
                }
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

        private void FlushWallHistory(int playerId, int x, int y, long attemptId)
        {
            if (!observedWallTargets.Add(x + ":" + y)) return;
            int related = 0;
            foreach (WallObservation wall in wallHistory)
                if (Math.Abs(wall.X - x) <= 12 && Math.Abs(wall.Y - y) <= 12)
                {
                    related++;
                    LogWall(wall, "history-near-wood-target", attemptId);
                }
            Log($"AI_BUILD_WALL_HISTORY: session={sessionId}, player={playerId}, attempt={attemptId}, " +
                $"target=({x},{y}), related={related}, retained={wallHistory.Count}, " +
                $"dropped={wallHistoryDropped}; dropped history cannot prove absence of earlier walls.");
        }

        private string FormatWall(WallObservation wall, string relation, long attemptId) =>
            $"AI_BUILD_WALL_EVENT: session={sessionId}, tick={wall.Tick}, " +
                $"relation={relation}, attempt={attemptId}, player={wall.PlayerId}, " +
                $"phase={wall.Phase}, mapper={wall.Mapper}, tile=({wall.X},{wall.Y}), " +
                $"tileId={wall.TileId}, rawFlags=0x{wall.PropertyFlags:X8}, " +
                $"swamp={((wall.PropertyFlags & 0x20000000u) != 0)}, " +
                $"wallPresent={((wall.PropertyFlags & 0x100u) != 0)}, " +
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
            bool oldWall = (before.PropertyFlags & 0x100u) != 0;
            bool newWall = (after.PropertyFlags & 0x100u) != 0;
            if (!oldWall && newWall) return "new-wall";
            if (oldWall && !newWall) return "removed-wall";
            if (before.PropertyFlags != after.PropertyFlags || before.WallOwner != after.WallOwner ||
                before.TileType != after.TileType || before.Height != after.Height ||
                before.BuildingId != after.BuildingId || before.PathComponent != after.PathComponent)
                return "modified-existing-tile";
            return "unchanged";
        }

        private sealed class WallObservation
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

        private static string Classify(Attempt attempt)
        {
            if (attempt.SpawnPost && attempt.SpawnId > 0) return "spawn-observed";
            if (attempt.SpawnPre) return "spawn-entered-without-successful-post";
            if (attempt.BuildPre) return "building-creation-entered-before-spawn-aborted";
            if (attempt.RouteSeen && attempt.RouteResult == 0) return "route-rejected";
            if (attempt.RouteSeen) return "route-accepted-building-event-unobserved";
            if (attempt.NearX >= 0) return "near-position-found-route-unobserved";
            if (attempt.SearchX >= 0) return "search-found-near-position-unobserved";
            return "search-or-earlier-exit";
        }

        private static string AnalyzeRoute(AiRouteEvidence evidence)
        {
            if (evidence == null) return "snapshot-unobserved";
            if (evidence.Status != "ok") return "snapshot-unavailable:" + evidence.Status;
            if (evidence.Bypass != 0) return "route-bypass-result-divergence";
            if (evidence.SourceComponent <= 0) return "source-component-result-divergence";
            if (evidence.TargetComponent == 0) return "target-component-zero";
            if (evidence.TargetComponent < 0) return "target-component-invalid";
            if (evidence.SourceComponent == evidence.TargetComponent)
                return "same-component-result-divergence";
            if (!CanReach(evidence, (connection, snapshot) => true)) return "no-macro-connection";
            if (!CanReach(evidence, (connection, snapshot) => connection.Active == 1))
                return "inactive-connection-filter";
            if (!CanReach(evidence, (connection, snapshot) =>
                connection.Active == 1 && connection.Open != 0))
                return "closed-connection-filter";
            if (!CanReach(evidence, (connection, snapshot) =>
                connection.Active == 1 && connection.Open != 0 && connection.ConnectionClass != 1))
                return "ladder-class-filter";
            if (CanReach(evidence, IsVanillaEligible)) return "eligible-graph-result-divergence";
            foreach (AiRouteConnection connection in evidence.Connections)
                if (connection.Active == 1 && connection.Open != 0 &&
                    connection.ConnectionClass != 1 &&
                    connection.OwnerToken == int.MinValue &&
                    (connection.GateFlag == 0 || connection.GateFlag == int.MinValue))
                    return "connection-filter-or-unavailable-access-data";
            return "player-access-filter";
        }

        private static bool IsVanillaEligible(AiRouteConnection connection,
            AiRouteEvidence evidence) => connection.Active == 1 && connection.Open != 0 &&
            connection.ConnectionClass != 1 &&
            (connection.OwnerToken == evidence.PlayerToken ||
             connection.GateFlag != 0 && connection.GateFlag != int.MinValue);

        private static AiPathTileSample FindAnchor(AiNearbyPathEvidence evidence, int x, int y)
        {
            if (evidence == null) return null;
            foreach (AiPathTileSample sample in evidence.Anchors)
                if (sample.X == x && sample.Y == y) return sample;
            foreach (AiPathTileSample sample in evidence.Footprint)
                if (sample.X == x && sample.Y == y) return sample;
            if (x < 0 || y < 0 || x % 5 != 0 || y % 5 != 0) return null;
            return evidence.GetCapturedAnchor(x / 5, y / 5);
        }

        private static bool IsComparableAnchor(AiPathTileSample sample) =>
            sample != null && (sample.Status == "ok" || sample.Status == "components-only");

        private static bool HasViewDifference(AiNearbyPathEvidence evidence)
        {
            if (evidence == null) return false;
            foreach (AiPathTileSample sample in evidence.Anchors)
                if (sample.Status == "ok" && sample.NativeComponent != sample.ApiComponent)
                    return true;
            foreach (AiPathTileSample sample in evidence.Footprint)
                if (sample.Status == "ok" && sample.NativeComponent != sample.ApiComponent)
                    return true;
            return false;
        }

        private static string AnalyzeNearby(Attempt attempt)
        {
            AiNearbyPathEvidence before = attempt.NearbyBefore;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            if (before == null || after == null) return "nearby-snapshot-unobserved";
            if (before.Status != "ok" || after.Status != "ok")
                return "nearby-snapshot-unavailable:" + before.Status + "/" + after.Status;
            if (HasViewDifference(before) || HasViewDifference(after) ||
                attempt.RouteEvidence != null && attempt.RouteEvidence.Status == "ok" &&
                (attempt.RouteEvidence.SourceComponent != attempt.RouteEvidence.SourceNativeComponent ||
                 attempt.RouteEvidence.TargetComponent != attempt.RouteEvidence.TargetNativeComponent))
                return "native-api-component-view-divergence";
            if (after.ResultX < 0 || after.ResultY < 0) return "no-nearby-result";
            int x = after.ResultX * 5;
            int y = after.ResultY * 5;
            AiPathTileSample preTarget = FindAnchor(before, x, y);
            AiPathTileSample postTarget = FindAnchor(after, x, y);
            if (!IsComparableAnchor(preTarget) || !IsComparableAnchor(postTarget))
                return "candidate-outside-measured-anchor-window";
            if (preTarget.NativeComponent != preTarget.ApiComponent ||
                postTarget.NativeComponent != postTarget.ApiComponent)
                return "native-api-component-view-divergence";
            if (preTarget.NativeComponent != postTarget.NativeComponent)
                return "component-changed-during-nearby-search";
            if (attempt.RouteEvidence != null && attempt.RouteEvidence.Status == "ok" &&
                postTarget.NativeComponent != attempt.RouteEvidence.TargetNativeComponent)
                return "component-changed-after-nearby-search";
            return postTarget.NativeComponent == 0
                ? "vanilla-coarse-search-selected-zero-component-anchor"
                : "vanilla-coarse-search-selected-positive-component-anchor";
        }

        private void LogNearbyEvidence(int playerId, long attemptId, Attempt attempt, string inference)
        {
            AiNearbyPathEvidence before = attempt.NearbyBefore;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            int x = after.ResultX * 5;
            int y = after.ResultY * 5;
            AiPathTileSample preOrigin = FindAnchor(before, after.InputX * 5, after.InputY * 5);
            AiPathTileSample postOrigin = FindAnchor(after, after.InputX * 5, after.InputY * 5);
            AiPathTileSample preTarget = FindAnchor(before, x, y);
            AiPathTileSample postTarget = FindAnchor(after, x, y);
            AiCoarseCellSample preCell = FindCoarseCell(before, after.ResultX, after.ResultY);
            AiCoarseCellSample postCell = after.ResultCell;
            string signature = playerId + ":" + x + ":" + y + ":" + inference;
            bool full = detailedNearby.Add(signature);
            nearbyCauses.TryGetValue(signature, out int count);
            nearbyCauses[signature] = ++count;
            if (full || count == 2 || count == 10 || count % 100 == 0)
                Log($"AI_BUILD_NEARBY_EVIDENCE: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, attempt={attemptId}, input=({after.InputX},{after.InputY}), " +
                    $"result=({after.ResultX},{after.ResultY}), " +
                    $"originBefore={FormatSample(preOrigin)}, originAfter={FormatSample(postOrigin)}, " +
                    $"targetBefore={FormatSample(preTarget)}, targetAfter={FormatSample(postTarget)}, " +
                    $"coarseOriginBefore={FormatCoarse(before.InputCell)}, " +
                    $"coarseOriginAfter={FormatCoarse(after.InputCell)}, " +
                    $"coarseTargetBefore={FormatCoarse(preCell)}, " +
                    $"coarseTargetAfter={FormatCoarse(postCell)}, " +
                    $"routeTargetNative={attempt.RouteEvidence?.TargetNativeComponent.ToString() ?? "unobserved"}, " +
                    $"observedLast={attempt.LastStep}, " +
                    $"inference={inference}, repeat={count}, fullSamples={full}.");
            if (!full) return;
            if (after.ResultX >= 0 && after.ResultY >= 0)
                FlushWallHistory(playerId, x, y, attemptId);
            LogCoarseCells("before", playerId, attemptId, before?.NearbyCells);
            LogCoarseCells("after", playerId, attemptId, after.NearbyCells);
            LogSamples("before-anchor", playerId, attemptId, before?.Anchors);
            LogSamples("after-anchor", playerId, attemptId, after.Anchors);
            LogSamples("after-footprint", playerId, attemptId, after.Footprint);
            LogCoarseExplanation(playerId, attemptId, before, after);
            LogSamples("selected-coarse-5x5", playerId, attemptId, after.CoarseTiles);
            LogFootprintArea("footprint-and-ring", playerId, attemptId,
                x, y, after.FootprintRing);
            LogPlacementIndicators(playerId, attemptId, x, y,
                after.Footprint, after.FootprintRing);
        }

        private void LogCoarseExplanation(int playerId, long attemptId,
            AiNearbyPathEvidence before, AiNearbyPathEvidence after)
        {
            AiCoarseCellSample selected = FindCoarseCell(before, after.ResultX, after.ResultY);
            int[] raw = ParseCoarseBytes(selected);
            if (raw == null)
            {
                Log($"AI_BUILD_COARSE_EXPLAIN: session={sessionId}, attempt={attemptId}, status=unavailable.");
                return;
            }
            int different = 0, zero = 0, trees = 0, swamps = 0, buildings = 0;
            foreach (AiPathTileSample tile in after.CoarseTiles)
            {
                if (tile.Status != "ok") continue;
                if (tile.NativeComponent != after.ReferenceComponent) different++;
                if (tile.NativeComponent == 0) zero++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0) trees++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsSwamp) != 0) swamps++;
                if (tile.BuildingId != 0) buildings++;
            }
            Log($"AI_BUILD_COARSE_EXPLAIN: session={sessionId}, tick={lastTick}, " +
                $"player={playerId}, attempt={attemptId}, cell=({after.ResultX},{after.ResultY}), " +
                $"referenceComponent={after.ReferenceComponent}, storedForeignCount={raw[0]}, " +
                $"currentDifferentComponentTiles={different}, currentZeroComponentTiles={zero}, " +
                $"treeTiles={trees}, swampTiles={swamps}, buildingTiles={buildings}, " +
                $"treeWeight={raw[3]}, stone={raw[4]}, iron={raw[5]}, pitch={raw[6]}, " +
                $"swamp={raw[7]}, minHeight={raw[8]}, maxHeight={raw[9]}, " +
                $"heightRangeRejected={raw[10]}, structureOrReservation={raw[11]}, " +
                $"outsideUsableMap={raw[12]}, impassableEdge={raw[15]}, " +
                $"candidateFirstFailure={CoarseFailure(raw)}, " +
                "componentDifferenceMeaning=inference-current-grid-versus-stored-counter, " +
                "treeWeightMeaning=raw-Vanilla-value-not-tree-flag-count.");
            foreach (AiCoarseCellSample cell in before.NearbyCells)
            {
                int[] values = ParseCoarseBytes(cell);
                if (values == null) continue;
                Log($"AI_BUILD_LOCAL_CANDIDATE: session={sessionId}, attempt={attemptId}, " +
                    $"cell=({cell.X},{cell.Y}), firstFailedPredicate={CoarseFailure(values)}, " +
                    "actualBfsVisit=unobserved.");
            }
        }

        private static int[] ParseCoarseBytes(AiCoarseCellSample cell)
        {
            if (cell == null || cell.Bytes == null) return null;
            string[] parts = cell.Bytes.Split('-');
            if (parts.Length < 16) return null;
            var result = new int[16];
            for (int i = 0; i < 16; i++)
                if (!int.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out result[i])) return null;
            return result;
        }

        // Order follows the audited 0x58950 acceptance branch, not a generic buildability test.
        private static string CoarseFailure(int[] cell)
        {
            if ((sbyte)cell[0] >= 15) return "traversal-foreign-component-limit";
            if (cell[10] != 0) return "height-range";
            if (cell[11] != 0) return "structure-or-reservation";
            if (cell[12] != 0) return "outside-usable-map";
            if (cell[15] != 0) return "impassable-edge";
            if (cell[3] != 0) return "tree-weight";
            if ((sbyte)cell[4] >= 1) return "stone-count";
            if (cell[0] != 0) return "foreign-component-count";
            return "none-coarse-eligible";
        }

        private void LogSamples(string phase, int playerId, long attemptId,
            IReadOnlyList<AiPathTileSample> samples)
        {
            if (samples == null) return;
            foreach (AiPathTileSample sample in samples)
                Log($"AI_BUILD_PATH_TILE: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, {FormatSample(sample)}.");
        }

        private void LogCoarseCells(string phase, int playerId, long attemptId,
            IReadOnlyList<AiCoarseCellSample> cells)
        {
            if (cells == null) return;
            foreach (AiCoarseCellSample cell in cells)
                Log($"AI_BUILD_COARSE_CELL: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, {FormatCoarse(cell)}.");
        }

        private static string FormatSample(AiPathTileSample sample) => sample == null
            ? "unobserved" : sample.Status == "components-only"
            ? $"tile=({sample.X},{sample.Y}) native={sample.NativeComponent} api={sample.ApiComponent} " +
              "tileLayers=unobserved status=components-only"
            : $"tile=({sample.X},{sample.Y}) id={sample.TileId} " +
              $"native={sample.NativeComponent} api={sample.ApiComponent} " +
              $"rawFlags=0x{sample.PropertyFlags:X8} swamp={((sample.PropertyFlags & 0x20000000u) != 0)} " +
              $"wall={((sample.PropertyFlags & 0x100u) != 0)} " +
              $"tree={((sample.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0)} " +
              $"appleFarm={((sample.PropertyFlags & (uint)TilePropertyFlag.IsAppleFarm) != 0)} " +
              $"type={sample.TileType} organism={sample.Organism} occupancy={sample.Occupancy} " +
              $"height={sample.Height} buildingId={sample.BuildingId} " +
              $"wallOwner={sample.WallOwner} status={sample.Status}";

        private static AiCoarseCellSample FindCoarseCell(AiNearbyPathEvidence evidence, int x, int y)
        {
            if (evidence == null) return null;
            foreach (AiCoarseCellSample cell in evidence.NearbyCells)
                if (cell.X == x && cell.Y == y) return cell;
            return evidence.GetCapturedCoarseCell(x, y);
        }

        private static string FormatCoarse(AiCoarseCellSample cell) => cell == null
            ? "unobserved" : $"({cell.X},{cell.Y}) bytes={cell.Bytes} status={cell.Status}";

        private static bool CanReach(AiRouteEvidence evidence,
            Func<AiRouteConnection, AiRouteEvidence, bool> eligible)
        {
            var reached = new HashSet<int> { evidence.SourceComponent };
            bool changed;
            do
            {
                changed = false;
                foreach (AiRouteConnection connection in evidence.Connections)
                {
                    if (!eligible(connection, evidence)) continue;
                    if (!reached.Contains(connection.A) && !reached.Contains(connection.B) &&
                        !reached.Contains(connection.C)) continue;
                    if (connection.A > 0) changed |= reached.Add(connection.A);
                    if (connection.B > 0) changed |= reached.Add(connection.B);
                    if (connection.C > 0) changed |= reached.Add(connection.C);
                    if (reached.Contains(evidence.TargetComponent)) return true;
                }
            } while (changed);
            return reached.Contains(evidence.TargetComponent);
        }

        private void LogRouteEvidence(int playerId, long attemptId, AiRouteEvidence evidence, string cause)
        {
            if (evidence == null)
            {
                Log($"AI_BUILD_ROUTE_EVIDENCE: session={sessionId}, player={playerId}, attempt={attemptId}, status=missing.");
                return;
            }
            string signature = playerId + ":" + evidence.SourceComponent + ":" +
                evidence.TargetComponent + ":" + cause;
            bool first = detailedRoutes.Add(signature);
            Log($"AI_BUILD_ROUTE_EVIDENCE: session={sessionId}, tick={lastTick}, player={playerId}, " +
                $"attempt={attemptId}, sourceTile={evidence.SourceTile}, targetTile={evidence.TargetTile}, " +
                $"target=({evidence.TileX},{evidence.TileY}), sourceComponent={evidence.SourceComponent}, " +
                $"targetComponent={evidence.TargetComponent}, " +
                $"sourceNative={evidence.SourceNativeComponent}, targetNative={evidence.TargetNativeComponent}, " +
                $"mode={evidence.QueryMode}, " +
                $"bypass={evidence.Bypass}, playerAccessToken={evidence.PlayerToken}, " +
                $"records={evidence.Connections.Count}, status={evidence.Status}, inference={cause}, " +
                $"fullRecords={first}.");
            if (!first) return;
            foreach (AiRouteConnection connection in evidence.Connections)
            {
                if (connection.A <= 0 && connection.B <= 0 && connection.C <= 0 &&
                    connection.Active == 0) continue;
                Log($"AI_BUILD_ROUTE_RECORD: session={sessionId}, player={playerId}, attempt={attemptId}, " +
                    $"id={connection.Id}, active={connection.Active}, open={connection.Open}, " +
                    $"class={connection.ConnectionClass}, owner={connection.Owner}, building={connection.BuildingId}, " +
                    $"components=({connection.A},{connection.B},{connection.C}), " +
                    $"ownerAccessToken={connection.OwnerToken}, gateFlag={connection.GateFlag}, " +
                    $"eligibleInference={IsVanillaEligible(connection, evidence)}.");
            }
        }

        private sealed class Attempt
        {
            internal Attempt(int playerId) { PlayerId = playerId; }
            internal int PlayerId;
            internal int SearchX = -1;
            internal int NearX = -1;
            internal bool RouteSeen;
            internal int RouteResult;
            internal AiRouteEvidence RouteEvidence;
            internal AiNearbyPathEvidence NearbyBefore;
            internal AiNearbyPathEvidence NearbyAfter;
            internal NearbyWoodShadow NormalNearby;
            internal NearbyWoodShadow AlternativeNearby;
            internal bool NearbyOverlayApplied;
            internal bool BuildPre;
            internal bool BuildPost;
            internal bool SpawnPre;
            internal bool SpawnPost;
            internal long SpawnId;
            internal string LastStep = "wood-build-before";
        }

        private static readonly int[] NearbyDx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] NearbyDy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private sealed class NearbyFootprint
        {
            internal bool Complete;
            internal int AnchorNative = -1, AnchorApi = -1;
            internal int Bit4, Zero, Swamp, Wall, WallOwners, Trees, Buildings, Organisms, Occupied,
                Unavailable, ViewMismatches;
            internal string Tiles;
            internal string FirstObservedConstraint;
            internal bool ExcludeForCopyProbe => Complete && (AnchorNative == 0 || Bit4 != 0);
            internal bool NoMeasuredBlockers => Complete && AnchorNative > 0 && Zero == 0 && Bit4 == 0 &&
                Swamp == 0 && Wall == 0 && WallOwners == 0 && Trees == 0 && Buildings == 0 &&
                Organisms == 0 && Occupied == 0;
            internal string Describe() =>
                $"anchor={AnchorNative}/{AnchorApi}:bit4={Bit4}:pcl0={Zero}:" +
                $"swamp={Swamp}:wall={Wall}:wallOwner={WallOwners}:tree={Trees}:building={Buildings}:" +
                $"organism={Organisms}:occupied={Occupied}:unavailable={Unavailable}:" +
                $"viewMismatches={ViewMismatches}:" +
                $"firstObservedConstraint={FirstObservedConstraint}:nativePlacementUnobserved=true:" +
                $"tiles=[{Tiles}]";
        }

        private static NearbyFootprint CaptureNearbyFootprint(int coarseX, int coarseY)
        {
            var result = new NearbyFootprint();
            IReadOnlyList<AiPathTileSample> tiles = AiBuildDiagnostic.CaptureTiles(
                coarseX * 5, coarseY * 5, 3, 3);
            if (tiles.Count != 9)
            {
                result.Unavailable = 9;
                result.Tiles = "tile-count=" + tiles.Count;
                result.FirstObservedConstraint = "tile-count-unavailable";
                return result;
            }
            var raw = new StringBuilder();
            foreach (AiPathTileSample tile in tiles)
            {
                if (raw.Length != 0) raw.Append('|');
                raw.Append(tile.X).Append('/').Append(tile.Y).Append(':').Append(tile.Status);
                if (tile.Status != "ok") { result.Unavailable++; continue; }
                raw.Append(':').Append(tile.NativeComponent).Append('/')
                    .Append(tile.ApiComponent).Append(':').Append(tile.PropertyFlags.ToString("X8"))
                    .Append(':').Append(tile.BuildingId).Append(':').Append(tile.Organism)
                    .Append(':').Append(tile.Occupancy).Append(':').Append(tile.Height)
                    .Append(':').Append(tile.TileType).Append(':').Append(tile.WallOwner)
                    .Append(':').Append(tile.TileId);
                if ((tile.PropertyFlags & NativePlacementReservationFlag) != 0) result.Bit4++;
                if (tile.NativeComponent == 0) result.Zero++;
                if (tile.NativeComponent != tile.ApiComponent) result.ViewMismatches++;
                if ((tile.PropertyFlags & 0x20000000u) != 0) result.Swamp++;
                if ((tile.PropertyFlags & 0x100u) != 0) result.Wall++;
                if (tile.WallOwner > 0) result.WallOwners++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0) result.Trees++;
                if (tile.BuildingId > 0) result.Buildings++;
                if (tile.Organism > 0) result.Organisms++;
                if (tile.Occupancy > 0) result.Occupied++;
            }
            AiPathTileSample anchor = tiles[0];
            result.AnchorNative = anchor.NativeComponent;
            result.AnchorApi = anchor.ApiComponent;
            result.Tiles = raw.ToString();
            result.Complete = result.Unavailable == 0 && result.ViewMismatches == 0;
            result.FirstObservedConstraint = result.Unavailable != 0 ? "tile-unavailable" :
                result.ViewMismatches != 0 ? "native-api-pcl-view-mismatch" :
                anchor.NativeComponent == 0 ? "anchor-component-zero" :
                result.Bit4 != 0 ? "parcel-bit4-native-placement-blocker" :
                result.Wall != 0 ? "wall-flag" : result.Swamp != 0 ? "swamp-flag" :
                result.Buildings != 0 ? "building-layer" :
                result.Trees != 0 || result.Organisms != 0 || result.Occupied != 0 ?
                "other-measured-occupancy" : "none-of-measured-flags";
            return result;
        }

        private sealed class NearbyWoodShadow
        {
            internal int X = -1, Y = -1, Visited;
            internal int TraceOmitted, FootprintOmitted;
            internal bool Valid = true;
            internal string Error;
            internal NearbyFootprint ExpectedFootprint;
            internal readonly List<NearbyMaskedCandidate> MaskedCandidates =
                new List<NearbyMaskedCandidate>();
            internal readonly List<string> CandidateTrace = new List<string>();
            internal readonly List<string> Trace = new List<string>();
        }

        private sealed class NearbyMaskedCandidate
        {
            internal int Index;
            internal string Reason, FootprintTiles;
        }

        private NearbyWoodShadow ReplayNearbyWood(AiNearbyPathEvidence evidence, bool excludeInvalidFootprint,
            bool trace)
        {
            var result = new NearbyWoodShadow();
            if (evidence == null || evidence.Status != "ok" ||
                (uint)evidence.InputX >= 160 || (uint)evidence.InputY >= 160)
            { result.Valid = false; result.Error = "snapshot-or-input-unavailable"; return result; }
            int[] seed = ParseCoarseBytes(evidence.GetCapturedCoarseCell(evidence.InputX, evidence.InputY));
            if (seed == null || seed[12] != 0)
            { result.Valid = false; result.Error = "seed-outside-usable-map"; return result; }
            var seenCells = new bool[25600];
            var queue = new int[25600];
            int head = 0, tail = 1;
            int seedIndex = evidence.InputX * 160 + evidence.InputY;
            queue[0] = seedIndex;
            seenCells[seedIndex] = true;
            while (head < tail)
            {
                int current = queue[head++];
                int cx = current / 160, cy = current % 160;
                int[] parent = ParseCoarseBytes(evidence.GetCapturedCoarseCell(cx, cy));
                if (parent == null)
                { result.Valid = false; result.Error = "missing-parent-cell"; break; }
                for (int direction = 0; direction < 8; direction++)
                {
                    int x = cx + NearbyDx[direction], y = cy + NearbyDy[direction];
                    // Vanilla's outside-map branch is safe only for a blocked parent; never
                    // simulate a native out-of-bounds read or permit an overlay in that case.
                    if ((uint)x >= 160 || (uint)y >= 160)
                    {
                        if (parent[12] == 0)
                        { result.Valid = false; result.Error = "native-edge-branch"; }
                        if (!result.Valid) return result;
                        continue;
                    }
                    int index = x * 160 + y;
                    if (seenCells[index]) continue;
                    seenCells[index] = true;
                    int[] cell = ParseCoarseBytes(evidence.GetCapturedCoarseCell(x, y));
                    if (cell == null)
                    { result.Valid = false; result.Error = "missing-candidate-cell"; return result; }
                    result.Visited++;
                    string rejection = CoarseFailure(cell);
                    NearbyFootprint footprint = null;
                    if (trace)
                    {
                        // Bound detailed tile reads inside the existing nearby-search callback.
                        // Every coarse-eligible candidate is retained even beyond this limit.
                        bool includeFootprint = result.Visited <= 128 || rejection == "none-coarse-eligible";
                        if (!includeFootprint) result.FootprintOmitted++;
                        string placement = includeFootprint ?
                            (footprint = CaptureNearbyFootprint(x, y)).Describe() : "not-sampled-limit";
                        if (result.Trace.Count < 2048 || rejection == "none-coarse-eligible")
                            result.Trace.Add($"cell=({x},{y}), parent=({cx},{cy}), direction={direction}, " +
                                $"depth={unchecked((sbyte)parent[1]) + 1}, foreign={(sbyte)cell[0]}, " +
                                $"bytes={evidence.GetCapturedCoarseCell(x, y).Bytes}, firstRule={rejection}, " +
                                $"footprint={placement}");
                        else result.TraceOmitted++;
                    }
                    if ((sbyte)cell[0] >= 15) continue;
                    if (rejection == "none-coarse-eligible")
                    {
                        AiPathTileSample anchor = evidence.GetCapturedAnchor(x, y);
                        if (anchor == null || anchor.NativeComponent != anchor.ApiComponent)
                        { result.Valid = false; result.Error = $"anchor-view-mismatch-({x},{y})"; return result; }
                        if (excludeInvalidFootprint)
                        {
                            if (result.CandidateTrace.Count >= 256)
                            { result.Valid = false; result.Error = "candidate-audit-limit-256"; return result; }
                            footprint = footprint ?? CaptureNearbyFootprint(x, y);
                            if (!footprint.Complete || anchor.NativeComponent != footprint.AnchorNative)
                            { result.Valid = false; result.Error = $"footprint-view-unavailable-({x},{y})"; return result; }
                            string reason = anchor.NativeComponent == 0 && footprint.Bit4 != 0 ?
                                "anchor-component-zero+parcel-bit4-in-3x3" :
                                anchor.NativeComponent == 0 ? "anchor-component-zero" :
                                footprint.Bit4 != 0 ? "parcel-bit4-in-3x3" : "no-proven-exclusion";
                            result.CandidateTrace.Add($"cell=({x},{y}), coarse={evidence.GetCapturedCoarseCell(x, y).Bytes}, " +
                                $"decision={reason}, footprint={footprint.Describe()}");
                            if (footprint.ExcludeForCopyProbe)
                                result.MaskedCandidates.Add(new NearbyMaskedCandidate
                                { Index = index, Reason = reason, FootprintTiles = footprint.Tiles });
                            else
                            {
                                result.X = x; result.Y = y;
                                result.ExpectedFootprint = footprint;
                                return result;
                            }
                        }
                        else { result.X = x; result.Y = y; return result; }
                    }
                    if (tail >= queue.Length)
                    { result.Valid = false; result.Error = "queue-overflow"; return result; }
                    queue[tail++] = index;
                }
            }
            if (excludeInvalidFootprint && result.X < 0 && result.Valid)
            { result.Valid = false; result.Error = "no-unmasked-candidate"; }
            return result;
        }

        private void PrepareNearbyWoodShadow(long attemptId, Attempt attempt)
        {
            int playerId = attempt.PlayerId;
            if ((uint)(playerId - 1) >= 8 ||
                (nearbyDetailedByPlayer[playerId] && !(nearbyCopySession && playerId == ProbePlayer)))
                return;
            bool detailed = !nearbyDetailedByPlayer[playerId];
            if (detailed) nearbyDetailedByPlayer[playerId] = true;
            attempt.NormalNearby = ReplayNearbyWood(attempt.NearbyBefore, false, detailed);
            if (nearbyCopySession && playerId == ProbePlayer &&
                nearbyCopyApplications < NearbyCopyMaxApplications)
                attempt.AlternativeNearby = ReplayNearbyWood(attempt.NearbyBefore, true, false);
            if (attempt.AlternativeNearby != null && nearbyAlternativeTraces < 2)
            {
                nearbyAlternativeTraces++;
                foreach (string line in attempt.AlternativeNearby.CandidateTrace)
                    QueueDiagnostic($"AI_BUILD_NEARBY_COPY_CANDIDATE: session={sessionId}, " +
                        $"attempt={attemptId}, player={playerId}, {line}", true);
                QueueDiagnostic($"AI_BUILD_NEARBY_COPY_PREDICTION: session={sessionId}, " +
                    $"attempt={attemptId}, player={playerId}, normal=({attempt.NormalNearby.X}," +
                    $"{attempt.NormalNearby.Y}), predicted=({attempt.AlternativeNearby.X}," +
                    $"{attempt.AlternativeNearby.Y}), masked={attempt.AlternativeNearby.MaskedCandidates.Count}, " +
                    $"visited={attempt.AlternativeNearby.Visited}, valid={attempt.AlternativeNearby.Valid}, " +
                    $"probeEligible={attempt.AlternativeNearby.ExpectedFootprint?.NoMeasuredBlockers ?? false}, " +
                    $"error={attempt.AlternativeNearby.Error}, predictedFootprint=" +
                    $"{attempt.AlternativeNearby.ExpectedFootprint?.Describe() ?? "unavailable"}.", true);
            }
            if (detailed)
            {
                foreach (string line in attempt.NormalNearby.Trace)
                    QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_VISIT: session={sessionId}, attempt={attemptId}, " + line, true);
                QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_FIRST: session={sessionId}, attempt={attemptId}, " +
                    $"player={playerId}, " +
                    $"input=({attempt.NearbyBefore?.InputX},{attempt.NearbyBefore?.InputY}), " +
                    $"normal=({attempt.NormalNearby.X},{attempt.NormalNearby.Y}), " +
                    $"alternative=({attempt.AlternativeNearby?.X},{attempt.AlternativeNearby?.Y}), " +
                    $"excludedAnchorOrBit4={attempt.AlternativeNearby?.MaskedCandidates.Count ?? 0}, " +
                    $"normalValid={attempt.NormalNearby.Valid}:{attempt.NormalNearby.Error}, " +
                    $"alternativeValid={attempt.AlternativeNearby?.Valid.ToString() ?? "unobserved"}:" +
                    $"{attempt.AlternativeNearby?.Error}, visited={attempt.NormalNearby.Visited}, " +
                    $"traceOmitted={attempt.NormalNearby.TraceOmitted}, " +
                    $"footprintsOmitted={attempt.NormalNearby.FootprintOmitted}.", true);
            }
        }

        private void CompleteNearbyWoodShadow(long attemptId, Attempt attempt)
        {
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            NearbyWoodShadow expected = attempt.NearbyOverlayApplied
                ? attempt.AlternativeNearby : attempt.NormalNearby;
            if (expected == null) return;
            bool match = expected != null && expected.Valid && after != null &&
                expected.X == after.ResultX && expected.Y == after.ResultY;
            QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_COMPARE: session={sessionId}, attempt={attemptId}, " +
                $"player={attempt.PlayerId}, overlay={attempt.NearbyOverlayApplied}, " +
                $"expected=({expected?.X},{expected?.Y}), vanilla=({after?.ResultX},{after?.ResultY}), " +
                $"match={match}, route-and-spawn=reported-by-AI_BUILD_ATTEMPT.", true);
            if (attempt.PlayerId != ProbePlayer || !nearbyCopySession || nearbyTestDisabled) return;
            if (!attempt.NearbyOverlayApplied && !nearbyCalibrated)
            {
                nearbyCalibrationAttempts++;
                if (match && attempt.NormalNearby.X == 69 && attempt.NormalNearby.Y == 97)
                { nearbyCalibrated = true; QueueDiagnostic("AI_BUILD_NEARBY_TEST_CALIBRATED: normal shadow matches first Vanilla result.", true); }
                else { nearbyTestDisabled = true; QueueDiagnostic("AI_BUILD_NEARBY_TEST_DISABLED: first Vanilla result or expected Canari target mismatched.", true); }
            }
            else if (attempt.NearbyOverlayApplied && !match)
            { nearbyTestDisabled = true; QueueDiagnostic("AI_BUILD_NEARBY_TEST_DISABLED: overlay result differed from shadow.", true); }
        }

        internal Action BeginNearbyWoodOverlay(ulong state, int playerId, int x, int y)
        {
            if (!nearbyCopySession || !nearbyCalibrated || nearbyTestDisabled ||
                nearbyCopyApplications >= NearbyCopyMaxApplications ||
                playerId != ProbePlayer || !AiBuildDiagnostic.TryGetCurrentWoodAttempt(
                    out long attemptId, out int owner) || owner != playerId ||
                !attempts.TryGetValue(attemptId, out Attempt attempt) ||
                attempt.NearbyBefore == null || attempt.NearbyBefore.InputX != x ||
                attempt.NearbyBefore.InputY != y || attempt.NormalNearby == null ||
                !attempt.NormalNearby.Valid || attempt.NormalNearby.X != 69 ||
                attempt.NormalNearby.Y != 97 || attempt.AlternativeNearby == null ||
                !attempt.AlternativeNearby.Valid ||
                attempt.AlternativeNearby.X < 0 || attempt.AlternativeNearby.Y < 0 ||
                attempt.AlternativeNearby.MaskedCandidates.Count == 0 ||
                attempt.AlternativeNearby.ExpectedFootprint == null ||
                !attempt.AlternativeNearby.ExpectedFootprint.NoMeasuredBlockers) return null;
            var changed = new List<long>();
            try
            {
                NearbyFootprint expectedLive = CaptureNearbyFootprint(
                    attempt.AlternativeNearby.X, attempt.AlternativeNearby.Y);
                if (!expectedLive.NoMeasuredBlockers ||
                    !string.Equals(expectedLive.Tiles,
                        attempt.AlternativeNearby.ExpectedFootprint.Tiles, StringComparison.Ordinal))
                    throw new InvalidOperationException("Predicted clear footprint changed before overlay.");
                foreach (NearbyMaskedCandidate candidate in attempt.AlternativeNearby.MaskedCandidates)
                {
                    int index = candidate.Index;
                    int cx = index / 160, cy = index % 160;
                    int[] prior = ParseCoarseBytes(attempt.NearbyBefore.GetCapturedCoarseCell(cx, cy));
                    long cell = checked((long)state + 0x5B834 + (long)index * 0x30);
                    var live = new byte[16];
                    Marshal.Copy(new IntPtr(cell), live, 0, live.Length);
                    bool same = prior != null && prior[0] == 0;
                    if (same)
                        for (int i = 0; i < live.Length; i++)
                            if (live[i] != prior[i]) { same = false; break; }
                    if (!same)
                        throw new InvalidOperationException($"Coarse memory changed before overlay at ({cx},{cy}).");
                    NearbyFootprint liveFootprint = CaptureNearbyFootprint(cx, cy);
                    if (!liveFootprint.ExcludeForCopyProbe ||
                        !string.Equals(liveFootprint.Tiles, candidate.FootprintTiles,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException($"Masked footprint changed before overlay at ({cx},{cy}).");
                    changed.Add(cell);
                    Marshal.WriteByte(new IntPtr(cell), 1);
                }
                attempt.NearbyOverlayApplied = true;
                nearbyCopyApplications++;
                QueueDiagnostic($"AI_BUILD_NEARBY_TEST_APPLIED: session={sessionId}, attempt={attemptId}, " +
                    $"player={playerId}, input=({x},{y}), expected=({attempt.AlternativeNearby.X}," +
                    $"{attempt.AlternativeNearby.Y}), masked={changed.Count}, " +
                    $"application={nearbyCopyApplications}/{NearbyCopyMaxApplications}, " +
                    $"reasons={string.Join("|", attempt.AlternativeNearby.MaskedCandidates.ConvertAll(c =>
                        $"({c.Index / 160},{c.Index % 160}):{c.Reason}"))}, " +
                    "onlyForeignCounter=0-to-1; restoreScheduledAfterSingleVanillaCall=true.", true);
                if (nearbyCopyApplications == NearbyCopyMaxApplications)
                    QueueDiagnostic($"AI_BUILD_NEARBY_TEST_LIMIT_REACHED: session={sessionId}, " +
                        $"applications={nearbyCopyApplications}; further calls are observation-only.", true);
                return () => RestoreNearbyWoodOverlay(changed, attemptId);
            }
            catch (Exception ex)
            {
                RestoreNearbyWoodOverlay(changed, attemptId);
                nearbyTestDisabled = true;
                QueueDiagnostic($"AI_BUILD_NEARBY_TEST_ABORTED: attempt={attemptId}, reason={ex}.", true);
                return null;
            }
        }

        private void RestoreNearbyWoodOverlay(List<long> changed, long attemptId)
        {
            bool drift = false, failed = false;
            foreach (long address in changed)
            {
                try { if (Marshal.ReadByte(new IntPtr(address)) != 1) drift = true; }
                catch { drift = true; }
                // Restore every touched cell even if another read or write fails.
                try { Marshal.WriteByte(new IntPtr(address), 0); }
                catch { failed = true; }
            }
            if (drift || failed) nearbyTestDisabled = true;
            QueueDiagnostic($"AI_BUILD_NEARBY_TEST_RESTORED: session={sessionId}, attempt={attemptId}, " +
                $"count={changed.Count}, unexpectedCounterChange={drift}, restoreFailed={failed}.", true);
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

        private void LogSummary(string phase)
        {
            try
            {
                Log($"AI_BUILD_GRID_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                    $"mode0Calls={gridModeZeroCalls}, mode1Calls={gridModeOneCalls}, " +
                    $"firstMismatchSeen={firstGridMismatchSeen}, earlyDropped={earlyGridDropped}.");
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!players.IsAIPlayer(playerId)) continue;
                    string outcomeText = "";
                    foreach (KeyValuePair<string, int> outcome in outcomes)
                        if (outcome.Key.StartsWith(playerId + ":", StringComparison.Ordinal))
                            outcomeText += outcome.Key.Substring(2) + "=" + outcome.Value + ",";
                    string routeCauseText = "";
                    foreach (KeyValuePair<string, int> cause in routeCauses)
                        if (cause.Key.StartsWith(playerId + ":", StringComparison.Ordinal))
                            routeCauseText += cause.Key.Substring(2) + "=" + cause.Value + ",";
                    string candidateText = "";
                    foreach (KeyValuePair<string, int> reason in candidateReasons)
                        if (reason.Key.StartsWith("wood:" + playerId + ":", StringComparison.Ordinal) ||
                            reason.Key.StartsWith("farm:" + playerId + ":", StringComparison.Ordinal))
                            candidateText += reason.Key + "=" + reason.Value + ",";
                    string farmInference = appleSpawns[playerId] > 0 ? "apple-spawn-observed" :
                        farmBuildCalls[playerId] > 0 ? "build-reached-no-spawn" :
                        farmSearchCalls[playerId] > 0 && farmTraversals[playerId] == 0 ?
                            "farm-search-entry-pretraversal-gate" :
                        farmSearchCalls[playerId] > 0 ? "farm-traversal-without-build" :
                        farmPhaseReached[playerId] == 0 ? "farm-phase-not-reached" :
                        applePhaseReached[playerId] == 0 ? "apple-not-selected-on-farm-phase" :
                        "apple-selected-but-pre-search-gate";
                    Log($"AI_BUILD_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                        $"player={playerId}, lord={players.GetAILord(playerId)}, scheduler={schedulerCalls[playerId]}, " +
                        $"woodBuild={woodBuildCalls[playerId]}, woodSearch={woodSearchCalls[playerId]}, " +
                        $"initialHuts={initialHuts[playerId]}, newHuts={hutSpawns[playerId]}, " +
                        $"wallEventsRetained={wallHistory.Count}, wallEventsDropped={wallHistoryDropped}, " +
                        $"materializedWalls={materializedWalls[playerId]}, wallPostLogged={wallEventsLogged}, " +
                        $"wallPostSuppressed={wallEventsSuppressed}, timelineLast={timelineSequence}, " +
                        $"routeHookReady={AiBuildDiagnostic.RouteReady}, " +
                        $"lastObserved={lastObservedStage[playerId] ?? "none"}, " +
                        $"inference={InferStage(playerId)}, attemptOutcomes={outcomeText}, " +
                        $"routeCauses={routeCauseText}, woodAndFarmCellReasons={candidateText}, " +
                        $"coarseAuditWritesEnabled={(coarseAuditSession == sessionId ? coarseAuditWritesEnabled : -1)}, " +
                        $"coarseAuditAvailable={(coarseAuditSession == sessionId ? coarseAuditAvailable : -1)}, " +
                        $"appleSelectedSnapshots={farmSelectedFrames[playerId]}, " +
                        $"farmPhaseReached={farmPhaseReached[playerId]}, " +
                        $"applePhaseReached={applePhaseReached[playerId]}, farmSearch={farmSearchCalls[playerId]}, " +
                        $"farmTraversals={farmTraversals[playerId]}, " +
                        $"farmBuild={farmBuildCalls[playerId]}, appleSpawns={appleSpawns[playerId]}, " +
                        $"lastFarmResult={lastFarmResult[playerId]}, farmInference={farmInference}.");
                }
                LogPlayers(phase);
            }
            catch (Exception ex) { Log("AI_BUILD_SUMMARY_FAILED: " + ex); }
        }

        private void LogResources(int playerId, string phase)
        {
            try
            {
                Log($"AI_BUILD_RESOURCES: session={sessionId}, phase={phase}, tick={lastTick}, " +
                    $"player={playerId}, {ReadResources(playerId)}.");
            }
            catch (Exception ex) { Log("AI_BUILD_RESOURCE_READ_FAILED: " + ex); }
        }

        private static string ReadResources(int playerId)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId,
                out GamePlayerResources* resources) || resources == null)
                return "resources=unavailable";
            return $"gold={resources->r_TotalGoodsGold}, woodLogs={resources->r_TotalGoodsWoodLogs}, " +
                $"woodPlanks={resources->r_TotalGoodsWoodPlanks}";
        }

        private void LogFootprintArea(string phase, int playerId, long attemptId,
            int anchorX, int anchorY, IReadOnlyList<AiPathTileSample> samples)
        {
            if (samples == null) return;
            foreach (AiPathTileSample sample in samples)
            {
                bool footprint = sample.X >= anchorX && sample.X < anchorX + 3 &&
                    sample.Y >= anchorY && sample.Y < anchorY + 3;
                string role = sample.X == anchorX && sample.Y == anchorY ? "anchor-footprint" :
                    footprint ? "footprint" : "perimeter-possible-access";
                Log($"AI_BUILD_PLACE_TILE: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, role={role}, " +
                    $"apparentBuildingFree={(sample.Status == "ok" && sample.BuildingId == 0)}, " +
                    $"{FormatSample(sample)}.");
            }
        }

        private void LogPlacementIndicators(int playerId, long attemptId,
            int x, int y, IReadOnlyList<AiPathTileSample> footprint,
            IReadOnlyList<AiPathTileSample> area)
        {
            int footprintTrees = 0, footprintSwamps = 0, footprintBuildings = 0;
            int footprintReservations = 0;
            int accessiblePerimeter = 0, blockedPerimeter = 0;
            foreach (AiPathTileSample tile in footprint)
            {
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0) footprintTrees++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsSwamp) != 0) footprintSwamps++;
                if (tile.BuildingId != 0 ||
                    (tile.PropertyFlags & (uint)TilePropertyFlag.IsBuilding) != 0)
                    footprintBuildings++;
                if ((tile.PropertyFlags & NativePlacementReservationFlag) != 0)
                    footprintReservations++;
            }
            foreach (AiPathTileSample tile in area)
            {
                if (tile.X >= x && tile.X < x + 3 && tile.Y >= y && tile.Y < y + 3)
                    continue;
                if (tile.Status == "ok" && tile.NativeComponent > 0) accessiblePerimeter++;
                else blockedPerimeter++;
            }
            Log($"AI_BUILD_PLACEMENT_INDICATORS: session={sessionId}, attempt={attemptId}, " +
                $"player={playerId}, anchor=({x},{y}), footprintTiles={footprint.Count}, " +
                $"footprintTrees={footprintTrees}, footprintSwamps={footprintSwamps}, " +
                $"footprintBuildings={footprintBuildings}, footprintReservationBit4={footprintReservations}, " +
                $"perimeterPositiveComponents={accessiblePerimeter}, " +
                $"perimeterOther={blockedPerimeter}, actualAccessTile=unobserved-before-spawn, " +
                "vanillaPlacement=unobserved-before-probe; countsAreObservations.");
        }

        private static string FormatNativePlacementReason()
        {
            // 0x77E60 does not clear +0x204E704 on entry. This is a raw, possibly stale
            // diagnostic value; compare Pre and Post instead of treating it as a verdict.
            if (!AiBuildDiagnostic.HasObserver) return "placementReasonRaw=unavailable";
            try
            {
                IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
                if (tileManager == IntPtr.Zero) return "placementReasonRaw=no-tile-manager";
                int reason = Marshal.ReadInt32(IntPtr.Add(tileManager, 0x204E704));
                return $"placementReasonRaw={reason}, placementReasonMayBeStale=true";
            }
            catch (Exception ex) { return "placementReasonRaw=unavailable:" + ex.GetType().Name; }
        }

        private int ReadPathGeneration()
        {
            if (nativeModuleBase == 0) return -1;
            try
            {
                return Marshal.ReadInt32(new IntPtr(checked((long)nativeModuleBase + 0x60AD660 + 0x74)));
            }
            catch (Exception ex)
            {
                Log("AI_BUILD_PATH_GENERATION_READ_FAILED: " + ex.GetType().Name);
                return -1;
            }
        }

        private void ObservePathGeneration(int tick)
        {
            int current = ReadPathGeneration();
            if (current < 0) return;
            if (!pathGenerationKnown)
            {
                lastPathGeneration = current;
                pathGenerationKnown = true;
                return;
            }
            if (current == lastPathGeneration) return;
            int previous = lastPathGeneration;
            lastPathGeneration = current;
            pathGenerationChanges++;
            QueueDiagnostic($"AI_BUILD_PATH_GENERATION: session={sessionId}, tick={tick}, " +
                $"previous={previous}, current={current}, tickObserverMayCoalesce=true.", true);
            foreach (AppleFarmWatch farm in appleFarms)
            {
                if (farm.GenerationDone) continue;
                farm.GenerationDone = true;
                CaptureFarmParcel(farm.X, farm.Y, farm.BuildingId,
                    "first-path-generation-after-tracking");
            }
        }

        // Copy only a few native coarse bytes during the existing 0x50720 callback.
        // Rendering and tile sampling happen later, on the persistent tick callback.
        private void CaptureFarmGridRaw(string phase, AiEconomyGridEvidence evidence)
        {
            if (evidence == null || evidence.State == 0 || farmGridRaw.Count >= FarmGridRawLimit)
            {
                farmGridRawDropped++;
                return;
            }
            var snapshot = new FarmGridRawSnapshot
            {
                Phase = phase, Mode = evidence.Mode, Tick = lastTick, Session = sessionId
            };
            try
            {
                foreach (AppleFarmWatch farm in appleFarms)
                {
                    int minX = Math.Max(0, farm.X / 5), maxX = Math.Min(159, (farm.X + 9) / 5);
                    int minY = Math.Max(0, farm.Y / 5), maxY = Math.Min(159, (farm.Y + 9) / 5);
                    for (int x = minX; x <= maxX; x++)
                        for (int y = minY; y <= maxY; y++)
                        {
                            int index = x * 160 + y;
                            long address = checked((long)evidence.State + 0x5B834 + index * 0x30L);
                            snapshot.Cells.Add(new FarmGridRawCell
                            {
                                FarmId = farm.BuildingId, X = x, Y = y,
                                Foreign = Marshal.ReadByte(new IntPtr(address)),
                                Reservation = Marshal.ReadByte(new IntPtr(address + 11))
                            });
                        }
                }
                farmGridRaw.Enqueue(snapshot);
                if (phase == "economy-grid-after") farmGridPairCaptured = true;
            }
            catch (Exception)
            {
                farmGridRawDropped++;
            }
        }

        private void FlushFarmGridRaw()
        {
            while (farmGridRaw.Count != 0)
            {
                FarmGridRawSnapshot snapshot = farmGridRaw.Dequeue();
                if (snapshot.Session != sessionId) continue;
                var cells = new StringBuilder();
                foreach (FarmGridRawCell cell in snapshot.Cells)
                {
                    if (cells.Length != 0) cells.Append('|');
                    cells.Append($"farm={cell.FarmId}:({cell.X},{cell.Y}):foreign={cell.Foreign}:reservation={cell.Reservation}");
                }
                QueueDiagnostic($"AI_BUILD_FARM_GRID_RAW: session={sessionId}, callbackTick={snapshot.Tick}, " +
                    $"stage={snapshot.Phase}, mode={snapshot.Mode}, cells={cells}; " +
                    "coarseBytesCopiedInNativeCallback=true; noTileSnapshotAtCallback=true.", true);
            }
        }

        private sealed class FarmGridRawSnapshot
        {
            internal string Phase;
            internal int Mode, Tick;
            internal long Session;
            internal readonly List<FarmGridRawCell> Cells = new List<FarmGridRawCell>();
        }

        private sealed class FarmGridRawCell
        {
            internal int FarmId, X, Y;
            internal byte Foreign, Reservation;
        }

        private void CaptureFarmParcel(int originX, int originY, int buildingId, string stage)
        {
            if (!active || !AiBuildDiagnostic.HasObserver) return;
            try
            {
                // 0x72490 uses the farm's 10-by-10 mapper. Four bounded 5-by-5 reads
                // preserve APIShared's per-capture limit while covering the entire parcel.
                var cells = new Dictionary<int, int[]>();
                int total = 0, reserved = 0, zero = 0, unavailable = 0;
                for (int blockX = 0; blockX < 2; blockX++)
                    for (int blockY = 0; blockY < 2; blockY++)
                        foreach (AiPathTileSample tile in AiBuildDiagnostic.CaptureTiles(
                            originX + blockX * 5, originY + blockY * 5, 5, 5))
                        {
                            total++;
                            if (tile.Status != "ok") { unavailable++; continue; }
                            int cellIndex = (tile.X / 5) * 160 + tile.Y / 5;
                            if (!cells.TryGetValue(cellIndex, out int[] counts))
                                cells[cellIndex] = counts = new int[3];
                            counts[0]++;
                            if ((tile.PropertyFlags & NativePlacementReservationFlag) != 0)
                            { reserved++; counts[1]++; }
                            if (tile.NativeComponent == 0) { zero++; counts[2]++; }
                        }
                var details = new StringBuilder();
                int mismatched = 0;
                var ordered = new List<int>(cells.Keys);
                ordered.Sort();
                foreach (int index in ordered)
                {
                    int coarseX = index / 160, coarseY = index % 160;
                    AiEconomyGridEvidence coarse = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                        coarseX, coarseY);
                    if (coarse.Status == "ok" &&
                        coarse.StoredForeignCount != coarse.CurrentDifferentCount) mismatched++;
                    if (details.Length != 0) details.Append('|');
                    int[] counts = cells[index];
                    details.Append($"({coarseX},{coarseY}):sampled={counts[0]}:" +
                        $"bit4={counts[1]}:pcl0={counts[2]}:" +
                        $"storedForeign={coarse.StoredForeignCount}:" +
                        $"storedReservation={ReadCoarseReservation(coarse)}:" +
                        $"liveDifferent={coarse.CurrentDifferentCount}:" +
                        $"coarseStatus={coarse.Status}");
                }
                bool firstMismatch = mismatched != 0 && !firstParcelMismatchDetailed;
                if (firstMismatch) firstParcelMismatchDetailed = true;
                bool detailed = detailedParcelStages.Add(stage) || firstMismatch;
                if (detailed) farmParcelSnapshots++;
                else farmParcelSnapshotsDropped++;
                QueueDiagnostic($"AI_BUILD_FARM_PARCEL: session={sessionId}, tick={lastTick}, " +
                    $"stage={stage}, buildingId={buildingId}, origin=({originX},{originY}), " +
                    $"window=10x10, sampled={total}, reservedBit4={reserved}, pclZero={zero}, " +
                    $"unavailable={unavailable}, mismatchedCells={mismatched}, detail={detailed}, " +
                    $"cells={(detailed ? details.ToString() : "summarized")}; rawMeasurementsOnly=true.",
                    detailed);
            }
            catch (Exception ex)
            {
                QueueDiagnostic("AI_BUILD_FARM_PARCEL_FAILED: stage=" + stage +
                    ", error=" + ex.GetType().Name, true);
            }
        }

        private void RunPlacementProbe()
        {
            probePending = false;
            probeDone = true; // Never retry after a native call, including an exception.
            if (!active || !probeSession ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(ProbePlayer) ||
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(ProbeX + 2, ProbeY + 2))
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=session-player-or-footprint-changed.");
                return;
            }
            if (GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride ||
                !AiBuildDiagnostic.TryReadPlacementStatus(out int prePreparation,
                    out int preRejected, out int preMode) || prePreparation != 0)
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=placement-override-or-preparation-state.");
                return;
            }
            IReadOnlyList<AiPathTileSample> before =
                AiBuildDiagnostic.CaptureTiles(ProbeX - 1, ProbeY - 1, 5, 5);
            string resourcesBefore = ReadResources(ProbePlayer);
            if (before.Count != 25)
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=tile-snapshot-unavailable.");
                return;
            }
            Log($"AI_BUILD_PROBE_BEGIN: session={sessionId}, tick={lastTick}, attempt={probeAttemptId}, " +
                $"player={ProbePlayer}, anchor=({ProbeX},{ProbeY}), mapper=0x33, scale=3, " +
                $"variant=15, free=false, bypassPlacementRules=false, " +
                $"preparation={prePreparation}, rejected={preRejected}, mode={preMode}, {resourcesBefore}.");
            LogFootprintArea("probe-before-footprint-and-ring", ProbePlayer,
                probeAttemptId, ProbeX, ProbeY, before);
            long callResult = 0;
            string failure = "none";
            try
            {
                probeRunning = true;
                // Exact 0x51540 -> 0x6D580 construction parameters, in the disposable save only.
                callResult = GameBuildingManagerAPI.Instance.CreatePrefab(ProbePlayer,
                    ProbeX, ProbeY, eMappers.MAPPER_WOODSMAN, 3, 15, false, false);
            }
            catch (Exception ex) { failure = ex.ToString(); }
            finally { probeRunning = false; }
            IReadOnlyList<AiPathTileSample> after =
                AiBuildDiagnostic.CaptureTiles(ProbeX - 1, ProbeY - 1, 5, 5);
            LogFootprintArea("probe-after-footprint-and-ring", ProbePlayer,
                probeAttemptId, ProbeX, ProbeY, after);
            string access = "unobserved-no-spawn";
            if (probeSpawnId > 0 && probeSpawnId <= int.MaxValue)
            {
                var point = GameBuildingManagerAPI.Instance.GetAccessPosition((int)probeSpawnId);
                access = $"({point.X},{point.Y})";
                if (GameTileManagerAPI.Instance.IsTileInsideMapBounds(point.X, point.Y))
                {
                    IReadOnlyList<AiPathTileSample> accessTile =
                        AiBuildDiagnostic.CaptureTiles(point.X, point.Y, 1, 1);
                    LogSamples("probe-actual-access", ProbePlayer, probeAttemptId, accessTile);
                }
            }
            bool changed = false;
            if (after.Count == before.Count)
                for (int i = 0; i < before.Count; i++)
                    if (before[i].PropertyFlags != after[i].PropertyFlags ||
                        before[i].BuildingId != after[i].BuildingId ||
                        before[i].Organism != after[i].Organism ||
                        before[i].NativeComponent != after[i].NativeComponent)
                    { changed = true; break; }
            string placement = AiBuildDiagnostic.TryReadPlacementStatus(out int preparation,
                out int rejected, out int mode)
                ? $"preparation={preparation}, rejected={rejected}, mode={mode}"
                : "placementStatus=unavailable";
            Log($"AI_BUILD_PROBE_RESULT: session={sessionId}, attempt={probeAttemptId}, " +
                $"callResult={callResult}, spawnId={probeSpawnId}, access={access}, " +
                $"tileChanged={changed}, {placement}, resourcesBefore=[{resourcesBefore}], " +
                $"resourcesAfter=[{ReadResources(ProbePlayer)}], exception={failure}, " +
                $"observedPlacement={(probeSpawnId > 0 ? "spawned" : "no-spawn")}.");
        }

        private void CaptureInitialAppleFarms()
        {
            int found = 0;
            try
            {
                Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding building = ref buildings[spanIndex];
                    if (building.r_BuildingType != eStructs.STRUCT_APPLEFARM ||
                        (building.r_AliveState != AliveState.IsAlive &&
                         building.r_AliveState != AliveState.NeedsInit)) continue;
                    found++;
                    if (appleFarms.Count >= ExistingAppleFarmLimit)
                    {
                        appleFarmDropped++;
                        continue;
                    }
                    TrackAppleFarm(spanIndex + 1, building.r_PlayerIdOwner,
                        building.r_TilePositionXBegin, building.r_TilePositionYBegin,
                        false, "session-start");
                }
                Log($"AI_BUILD_APPLEFARM_INITIAL_SCAN: session={sessionId}, found={found}, " +
                    $"tracked={appleFarms.Count}, dropped={appleFarmDropped}.");
            }
            catch (Exception ex) { Log("AI_BUILD_APPLEFARM_INITIAL_SCAN_FAILED: " + ex); }
        }

        private void TrackAppleFarm(int buildingId, int playerId, int eventX, int eventY,
            bool isNew, string stage)
        {
            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId,
                    out GameBuilding* building) || building == null ||
                    building->r_BuildingType != eStructs.STRUCT_APPLEFARM)
                {
                    Log($"AI_BUILD_APPLEFARM_UNRESOLVED: session={sessionId}, stage={stage}, " +
                        $"buildingId={buildingId}, eventTile=({eventX},{eventY}).");
                    return;
                }
                foreach (AppleFarmWatch existing in appleFarms)
                    if (existing.BuildingId == buildingId && existing.GlobalId == building->r_GlobalId)
                        return;
                if (appleFarms.Count >= AppleFarmLimit)
                {
                    appleFarmDropped++;
                    Log($"AI_BUILD_APPLEFARM_OVERFLOW: session={sessionId}, dropped={appleFarmDropped}, " +
                        $"buildingId={buildingId}.");
                    return;
                }
                var farm = new AppleFarmWatch
                {
                    BuildingId = buildingId,
                    GlobalId = building->r_GlobalId,
                    PlayerId = building->r_PlayerIdOwner,
                    X = building->r_TilePositionXBegin,
                    Y = building->r_TilePositionYBegin,
                    Size = checked((int)building->r_OccupyTileGridSize),
                    IsNew = isNew,
                    DueTickCount = observedTickCount + 5,
                    OrchardWatchUntilTick = observedTickCount + OrchardObservationTicks
                };
                appleFarms.Add(farm);
                Log($"AI_BUILD_APPLEFARM_TRACKED: session={sessionId}, tick={lastTick}, " +
                    $"stage={stage}, buildingId={buildingId}, globalId={farm.GlobalId}, " +
                    $"player={farm.PlayerId}, eventPlayer={playerId}, eventTile=({eventX},{eventY}), " +
                    $"buildingTile=({farm.X},{farm.Y}), occupiedGridSize={farm.Size}, " +
                    $"alive={building->r_AliveState}, dueObservedTick={(isNew ? farm.DueTickCount.ToString() : "none")}.");
                CaptureAppleFarm(farm, stage);
                CaptureOrchardStage(farm.X, farm.Y, farm.BuildingId, stage);
                CaptureFarmParcel(farm.X, farm.Y, farm.BuildingId, stage);
                farm.LastOrchardTiles = ReadOrchardTiles(farm.X, farm.Y);
            }
            catch (Exception ex)
            {
                Log($"AI_BUILD_APPLEFARM_TRACK_FAILED: session={sessionId}, stage={stage}, " +
                    $"buildingId={buildingId}, error={ex}.");
            }
        }

        private void ObserveAppleFarmsAfterGridUpdate(int mode)
        {
            foreach (AppleFarmWatch farm in appleFarms)
            {
                if (farm.GridUpdateDone) continue;
                farm.GridUpdateDone = true;
                try
                {
                    CaptureAppleFarm(farm, "after-grid-update-mode-" + mode);
                    CaptureOrchardStage(farm.X, farm.Y, farm.BuildingId,
                        "after-grid-update-mode-" + mode);
                }
                catch (Exception ex) { Log("AI_BUILD_APPLEFARM_GRID_CAPTURE_FAILED: " + ex); }
            }
        }

        private static bool TryGetOrchardOffset(int farmX, int farmY, int tileX,
            int tileY, out int offset)
        {
            for (int i = 0; i < OrchardDx.Length; i++)
                if (farmX + OrchardDx[i] == tileX && farmY + OrchardDy[i] == tileY)
                {
                    offset = i;
                    return true;
                }
            offset = -1;
            return false;
        }

        private static AiPathTileSample ReadOrchardTile(int x, int y)
        {
            IReadOnlyList<AiPathTileSample> tiles = AiBuildDiagnostic.CaptureTiles(x, y, 1, 1);
            return tiles.Count == 1 ? tiles[0] : null;
        }

        private static AiPathTileSample[] ReadOrchardTiles(int x, int y)
        {
            var result = new AiPathTileSample[OrchardDx.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = ReadOrchardTile(x + OrchardDx[i], y + OrchardDy[i]);
            return result;
        }

        private static bool OrchardTileChanged(AiPathTileSample before,
            AiPathTileSample after)
        {
            if (before == null || after == null) return before != after;
            return before.Status != after.Status ||
                before.NativeComponent != after.NativeComponent ||
                before.ApiComponent != after.ApiComponent ||
                before.PropertyFlags != after.PropertyFlags ||
                before.Organism != after.Organism ||
                before.BuildingId != after.BuildingId ||
                before.TileType != after.TileType;
        }

        private static string DescribeOrchardTile(AiPathTileSample tile)
        {
            if (tile == null) return "unavailable";
            return $"({tile.X},{tile.Y}):{tile.Status}:pcl={tile.NativeComponent}/{tile.ApiComponent}:" +
                $"flags={tile.PropertyFlags:X8}:type={tile.TileType}:organism={tile.Organism}:" +
                $"building={tile.BuildingId}";
        }

        private void LogOrchardTransition(string kind, Func<string> describe, bool mismatch)
        {
            orchardTransitions++;
            orchardKindCounts.TryGetValue(kind, out int count);
            orchardKindCounts[kind] = count + 1;
            bool firstMismatch = mismatch && !firstOrchardMismatchSeen;
            if (firstMismatch) firstOrchardMismatchSeen = true;
            bool detailed = detailedOrchardKinds.Add(kind) || firstMismatch;
            if (!detailed)
            {
                orchardTransitionsDropped++;
                return;
            }
            Log($"AI_BUILD_ORCHARD_TRANSITION: session={sessionId}, tick={lastTick}, " +
                $"observedTick={observedTickCount}, kind={kind}, firstMismatch={firstMismatch}, {describe()}.");
        }

        private void CaptureOrchardStage(int x, int y, int buildingId, string stage)
        {
            if (!AiBuildDiagnostic.HasObserver) return;
            try
            {
                AiPathTileSample[] points = ReadOrchardTiles(x, y);
                int zero = 0, apple = 0, mismatch = 0, unavailable = 0;
                var details = new StringBuilder();
                var cells = new HashSet<int>();
                foreach (AiPathTileSample tile in points)
                {
                    if (details.Length != 0) details.Append('|');
                    details.Append(DescribeOrchardTile(tile));
                    if (tile == null || tile.Status != "ok") { unavailable++; continue; }
                    if (tile.NativeComponent == 0) zero++;
                    if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsAppleFarm) != 0) apple++;
                    cells.Add((tile.X / 5) * 160 + tile.Y / 5);
                }
                var cellDetails = new StringBuilder();
                foreach (int index in cells)
                {
                    AiEconomyGridEvidence cell = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                        index / 160, index % 160);
                    if (cellDetails.Length != 0) cellDetails.Append('|');
                    bool differs = cell.Status == "ok" &&
                        cell.StoredForeignCount != cell.CurrentDifferentCount;
                    if (differs) mismatch++;
                    cellDetails.Append($"({cell.CoarseX},{cell.CoarseY}):{cell.Status}:" +
                        $"ref={cell.ReferenceComponent}:stored={cell.StoredForeignCount}:" +
                        $"live={cell.CurrentDifferentCount}:apple={cell.AppleFarmFlagCount}");
                }
                Log($"AI_BUILD_ORCHARD_STAGE: session={sessionId}, tick={lastTick}, " +
                    $"observedTick={observedTickCount}, stage={stage}, buildingId={buildingId}, " +
                    $"origin=({x},{y}), points={points.Length}, appleFlags={apple}, " +
                    $"pclZero={zero}, unavailable={unavailable}, mismatchedCells={mismatch}, " +
                    $"tiles={details}, cells={cellDetails}.");
            }
            catch (Exception ex) { Log("AI_BUILD_ORCHARD_STAGE_FAILED: " + ex); }
        }

        private void ObserveOrchardTransitions(AppleFarmWatch farm)
        {
            if (!AiBuildDiagnostic.HasObserver) return;
            try
            {
                AiPathTileSample[] now = ReadOrchardTiles(farm.X, farm.Y);
                if (farm.LastOrchardTiles == null)
                {
                    farm.LastOrchardTiles = now;
                    return;
                }
                for (int i = 0; i < now.Length; i++)
                {
                    AiPathTileSample before = farm.LastOrchardTiles[i];
                    AiPathTileSample after = now[i];
                    if (!OrchardTileChanged(before, after) || after == null) continue;
                    AiEconomyGridEvidence cell = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                        after.X / 5, after.Y / 5);
                    bool mismatch = cell.Status == "ok" &&
                        cell.StoredForeignCount != cell.CurrentDifferentCount;
                    string kind = before != null && before.NativeComponent > 0 &&
                        after.NativeComponent == 0 ? "pcl-positive-to-zero" :
                        before != null && before.NativeComponent == 0 &&
                        after.NativeComponent > 0 ? "pcl-zero-to-positive" :
                        "tile-or-vegetation-change";
                    LogOrchardTransition(kind, () => $"source=tick, farm={farm.BuildingId}, " +
                        $"owner={farm.PlayerId}, offset={i}, before={DescribeOrchardTile(before)}, " +
                        $"after={DescribeOrchardTile(after)}, cell=({cell.CoarseX},{cell.CoarseY}), " +
                        $"reference={cell.ReferenceComponent}, storedForeign={cell.StoredForeignCount}, " +
                        $"storedReservation={ReadCoarseReservation(cell)}, bit4Tiles={CountReservationTiles(cell)}, " +
                        $"liveDifferent={cell.CurrentDifferentCount}, mismatch={mismatch}, " +
                        $"cellTiles={(mismatch ? GridTiles(cell) : "omitted")}", mismatch);
                }
                farm.LastOrchardTiles = now;
            }
            catch (Exception ex) { Log("AI_BUILD_ORCHARD_TICK_FAILED: " + ex); }
        }

        private void CaptureAppleFarm(AppleFarmWatch farm, string stage)
        {
            if (appleFarmSnapshots >= AppleFarmSnapshotLimit)
            {
                appleFarmSnapshotDropped++;
                if (appleFarmSnapshotDropped == 1)
                    Log($"AI_BUILD_APPLEFARM_SNAPSHOT_OVERFLOW: session={sessionId}, " +
                        $"limit={AppleFarmSnapshotLimit}.");
                return;
            }
            appleFarmSnapshots++;
            if (!AiBuildDiagnostic.HasObserver)
            {
                Log($"AI_BUILD_APPLEFARM_SNAPSHOT: session={sessionId}, stage={stage}, " +
                    $"buildingId={farm.BuildingId}, status=native-observer-unavailable.");
                return;
            }
            string buildingStatus = "unavailable";
            try
            {
                if (GameBuildingManagerAPI.Instance.TryGetBuildingById(farm.BuildingId,
                    out GameBuilding* current) && current != null)
                {
                    if (current->r_BuildingType == eStructs.STRUCT_APPLEFARM &&
                        (current->r_GlobalId == farm.GlobalId || farm.GlobalId == 0))
                    {
                        buildingStatus = current->r_AliveState.ToString();
                        if (farm.GlobalId == 0) farm.GlobalId = current->r_GlobalId;
                        farm.PlayerId = current->r_PlayerIdOwner;
                        int currentSize = checked((int)current->r_OccupyTileGridSize);
                        if (currentSize >= 1 && currentSize <= 15)
                        {
                            farm.X = current->r_TilePositionXBegin;
                            farm.Y = current->r_TilePositionYBegin;
                            farm.Size = currentSize;
                        }
                    }
                    else buildingStatus = "identity-changed";
                }
            }
            catch (Exception ex) { buildingStatus = "read-failed:" + ex.GetType().Name; }
            if (farm.Size < 1 || farm.Size > 15 || farm.X < 0 || farm.Y < 0 ||
                farm.X + farm.Size > 800 || farm.Y + farm.Size > 800)
            {
                Log($"AI_BUILD_APPLEFARM_SNAPSHOT: session={sessionId}, stage={stage}, " +
                    $"buildingId={farm.BuildingId}, buildingStatus={buildingStatus}, status=invalid-footprint, " +
                    $"origin=({farm.X},{farm.Y}), size={farm.Size}.");
                return;
            }
            int startX = Math.Max(0, farm.X / 5 - 1);
            int endX = Math.Min(159, (farm.X + farm.Size - 1) / 5 + 1);
            int startY = Math.Max(0, farm.Y / 5 - 1);
            int endY = Math.Min(159, (farm.Y + farm.Size - 1) / 5 + 1);
            int valid = 0, mismatched = 0, unavailable = 0;
            int footprintTiles = 0, footprintBuildingTiles = 0;
            int footprintAppleFlags = 0, footprintTreeFlags = 0, footprintZero = 0;
            var cellLines = new List<string>();
            for (int coarseX = startX; coarseX <= endX; coarseX++)
                for (int coarseY = startY; coarseY <= endY; coarseY++)
                {
                    AiEconomyGridEvidence evidence = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                        coarseX, coarseY);
                    bool ready = evidence.Status == "ok";
                    bool mismatch = ready && evidence.StoredForeignCount != evidence.CurrentDifferentCount;
                    if (ready) valid++; else unavailable++;
                    if (mismatch) mismatched++;
                    foreach (AiPathTileSample tile in evidence.Tiles)
                    {
                        if (tile.X < farm.X || tile.X >= farm.X + farm.Size ||
                            tile.Y < farm.Y || tile.Y >= farm.Y + farm.Size || tile.Status != "ok")
                            continue;
                        footprintTiles++;
                        if (tile.BuildingId == farm.BuildingId) footprintBuildingTiles++;
                        if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsAppleFarm) != 0)
                            footprintAppleFlags++;
                        if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0)
                            footprintTreeFlags++;
                        if (tile.NativeComponent == 0) footprintZero++;
                    }
                    cellLines.Add($"AI_BUILD_APPLEFARM_CELL: session={sessionId}, tick={lastTick}, " +
                        $"stage={stage}, buildingId={farm.BuildingId}, cell=({coarseX},{coarseY}), " +
                        $"status={evidence.Status}, reference={evidence.ReferenceComponent}, " +
                        $"storedForeign={evidence.StoredForeignCount}, storedReservation={ReadCoarseReservation(evidence)}, " +
                        $"bit4Tiles={CountReservationTiles(evidence)}, liveDifferent={evidence.CurrentDifferentCount}, " +
                        $"liveZero={evidence.CurrentZeroCount}, appleFlags={evidence.AppleFarmFlagCount}, " +
                        $"treeFlags={evidence.TreeFlagCount}, storedTreeWeight={evidence.TreeWeight}, " +
                        $"mismatch={(ready ? mismatch.ToString() : "unobserved")}, " +
                        $"tiles={GridTiles(evidence)}.");
                }
            Log($"AI_BUILD_APPLEFARM_SNAPSHOT: session={sessionId}, tick={lastTick}, " +
                $"stage={stage}, buildingId={farm.BuildingId}, globalId={farm.GlobalId}, " +
                $"player={farm.PlayerId}, buildingStatus={buildingStatus}, " +
                $"occupiedGridBounds=({farm.X},{farm.Y})+{farm.Size}x{farm.Size}, " +
                $"coarseRange=({startX},{startY})-({endX},{endY}), " +
                $"cells={cellLines.Count}, valid={valid}, mismatch={mismatched}, unavailable={unavailable}, " +
                $"boundedTilesRead={footprintTiles}, buildingTiles={footprintBuildingTiles}, " +
                $"boundedAppleFlags={footprintAppleFlags}, " +
                $"boundedTreeFlags={footprintTreeFlags}, boundedPclZero={footprintZero}; " +
                "mismatchComparesRawStoredForeignWithCurrentPclCount; treeWeightIsRawVanillaValue.");
            foreach (string line in cellLines) Log(line);
        }

        private sealed class AppleFarmWatch
        {
            internal int BuildingId, PlayerId, X, Y, Size;
            internal uint GlobalId;
            internal long DueTickCount;
            internal long OrchardWatchUntilTick;
            internal bool IsNew, FiveTickDone, GridUpdateDone, GenerationDone;
            internal AiPathTileSample[] LastOrchardTiles;
        }

        private void CaptureInitialHuts()
        {
            try
            {
                Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding building = ref buildings[spanIndex];
                    int playerId = building.r_PlayerIdOwner;
                    if (playerId >= 1 && playerId <= 8 &&
                        building.r_AliveState == AliveState.IsAlive &&
                        building.r_BuildingType == eStructs.STRUCT_WOODCUTTERS_HUT)
                        initialHuts[playerId]++;
                }
            }
            catch (Exception ex) { Log("AI_BUILD_INITIAL_HUT_SCAN_FAILED: " + ex); }
        }

        private string InferStage(int playerId)
        {
            if (hutSpawns[playerId] > 0) return "woodcutter-spawn-observed";
            if (schedulerCalls[playerId] == 0) return "scheduler-not-observed";
            if (woodBuildCalls[playerId] == 0) return "wood-build-call-not-observed";
            if (woodSearchCalls[playerId] == 0) return "wood-build-returned-before-search";
            return "search-or-later-stage-see-trace";
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

        // The existing main-mod hook requests this snapshot while its player overlay is still active.
        // The scan and interpretation belong to the diagnostic mod, not BugfixesAndQoL.
        private static void ScanCandidateCells(ulong state, int playerId, bool wood)
        {
            string prefix = wood ? "wood" : "farm";
            try
            {
                uint generation = unchecked((uint)Marshal.ReadInt32(
                    new IntPtr(checked((long)state + 0x5B50C))));
                int originX = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x18783C)));
                int originY = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1A083C)));
                AiBuildDiagnostic.Publish(prefix + "-candidate-origin", playerId,
                    originX, originY, generation);
                Span<AivCoarseCell> grid = GameAIVManagerAPI.Instance.GetCoarseGrid();
                int visited = 0, candidates = 0, bestScore = int.MinValue;
                for (int cellIndex = 0; cellIndex < grid.Length; cellIndex++)
                {
                    ref AivCoarseCell cell = ref grid[cellIndex];
                    if (cell.CoarseSearchGeneration != generation) continue;
                    visited++;
                    int coarseX = cellIndex / 160;
                    int coarseY = cellIndex % 160;
                    long packed = wood ?
                        cell.ForeignPathComponentTileCount |
                        ((long)cell.CoarseSearchDepth << 8) |
                        ((long)cell.TreeObstructionWeight << 16) |
                        ((long)cell.WoodcutterRetryDelay << 24) |
                        ((long)cell.Unknown06 << 32) :
                        cell.ForeignPathComponentTileCount |
                        ((long)cell.StructureOrReservationCount << 8) |
                        ((long)cell.TreeObstructionWeight << 16) |
                        ((long)cell.WoodcutterRetryDelay << 24) |
                        ((long)cell.UnknownFlags91Count << 32) |
                        ((long)cell.UnknownFlags90Count << 40) |
                        ((long)cell.CombinedUnknownFlags << 48) |
                        ((long)cell.CoarseSearchDepth << 56);
                    if (visited <= 256)
                    {
                        AiEconomyGridEvidence live = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                            state, -1, coarseX, coarseY);
                        AiBuildDiagnostic.Publish(prefix + "-candidate-cell", playerId,
                            coarseX, coarseY, packed, live.CurrentDifferentCount);
                    }
                    if (!wood || coarseX == originX && coarseY == originY) continue;
                    if (unchecked((sbyte)cell.ForeignPathComponentTileCount) >= 6 ||
                        unchecked((sbyte)cell.TreeObstructionWeight) <= 0 ||
                        cell.WoodcutterRetryDelay != 0) continue;
                    int score = unchecked((sbyte)cell.TreeObstructionWeight) * 5 -
                        unchecked((sbyte)cell.CoarseSearchDepth) * 3;
                    if (cell.Unknown06 != 0) score = score < 1 ? score * 2 : score / 2;
                    candidates++;
                    if (score > bestScore) bestScore = score;
                }
                if (wood) AiBuildDiagnostic.Publish("wood-candidate-scan", playerId,
                    visited, candidates, bestScore, generation);
                else AiBuildDiagnostic.Publish("farm-candidate-scan", playerId,
                    visited, generation);
                if (visited > 256) AiBuildDiagnostic.Publish(prefix + "-candidate-overflow", playerId,
                    visited - 256);
            }
            catch (Exception ex)
            {
                AiBuildDiagnostic.Publish(prefix + "-candidate-scan-error", playerId, ex.HResult);
            }
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
            int bytes = Encoding.UTF8.GetByteCount(value) + 2;
            if (!preserveFirst && writtenBytes + deferredBytes + bytes > DiagnosticBudgetBytes)
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
                int bytes = Encoding.UTF8.GetByteCount(line) + 2;
                deferredBytes = Math.Max(0, deferredBytes - bytes);
                Log(line);
            }
        }

        private void CaptureWallMap(string phase)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                Span<byte> owners = tiles.GetWallOwnerLayer();
                uint[] next = new uint[owners.Length];
                int changed = 0, created = 0, removed = 0, altered = 0;
                for (int tileId = 1; tileId < next.Length; tileId++)
                {
                    uint wall = ((uint)tiles.GetTilePropertyFlag(tileId) & 0x100u) != 0 ? 0x100u : 0;
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
                        QueueDiagnostic($"AI_BUILD_WALL_MAP_CHANGE: session={sessionId}, tick={lastTick}, " +
                            $"phase={phase}, sequence={++timelineSequence}, tileId={tileId}, " +
                            $"tile=({x},{y}), old=0x{old:X3}, new=0x{next[tileId]:X3}, " +
                            $"kind={(wall != 0 && (old & 0x100u) == 0 ? "new" : wall == 0 &&
                                (old & 0x100u) != 0 ? "removed" : "owner-changed")}, " +
                            $"detailFlags=0x{(uint)tiles.GetTilePropertyFlag(tileId):X8}, " +
                            $"building={tiles.GetTileBuildingId(tileId)}, height={tiles.GetTileHeight(tileId)}.",
                            !firstWallChangeLogged);
                        firstWallChangeLogged = true;
                    }
                }
                wallMap = next;
                QueueDiagnostic($"AI_BUILD_WALL_MAP_SCAN: session={sessionId}, tick={lastTick}, phase={phase}, " +
                    $"tiles={next.Length}, changed={changed}, created={created}, removed={removed}, " +
                    $"altered={altered}, detailOmitted={Math.Max(0, changed - 512)}.");
            }
            catch (Exception ex) { QueueDiagnostic("AI_BUILD_WALL_MAP_SCAN_FAILED: " + ex); }
        }

        private void SnapshotWoodCells(string phase)
        {
            try
            {
                var cells = new HashSet<int> { 69 * 160 + 97 };
                for (int player = 1; player <= 8; player++)
                {
                    int x = woodOriginX[player], y = woodOriginY[player];
                    if (x <= 0 || x >= 159 || y <= 0 || y >= 159) continue;
                    cells.Add(x * 160 + y);
                    cells.Add((x - 1) * 160 + y);
                    cells.Add((x + 1) * 160 + y);
                    cells.Add(x * 160 + y - 1);
                    cells.Add(x * 160 + y + 1);
                }
                foreach (int index in cells)
                {
                    int x = index / 160, y = index % 160;
                    AiEconomyGridEvidence sample = gridState != 0
                        ? AiBuildDiagnostic.CaptureEconomyGridEvidence(gridState, -1, x, y)
                        : AiBuildDiagnostic.CaptureEconomyGridEvidence(x, y);
                    string signature = $"{index}:{sample.Status}:{sample.StoredForeignCount}:" +
                        $"{sample.CurrentDifferentCount}:{sample.CurrentZeroCount}:{sample.TreeWeight}";
                    bool transition = cellWatchSignatures.Add(signature);
                    if (phase.StartsWith("wood-build-before", StringComparison.Ordinal) ||
                        phase == "session-start" || phase == "first-tick" ||
                        phase.StartsWith("pcl-generation", StringComparison.Ordinal) || transition)
                        QueueDiagnostic($"AI_BUILD_CELL_SNAPSHOT: session={sessionId}, tick={lastTick}, " +
                            $"sequence={++timelineSequence}, phase={phase}, cell=({x},{y}), " +
                            $"status={sample.Status}, reference={sample.ReferenceComponent}, " +
                            $"storedForeign={sample.StoredForeignCount}, liveDifferent={sample.CurrentDifferentCount}, " +
                            $"zeroTiles={sample.CurrentZeroCount}, treeWeight={sample.TreeWeight}, " +
                            $"treeFlags={sample.TreeFlagCount}, appleFlags={sample.AppleFarmFlagCount}, " +
                            $"mismatch={sample.Status == "ok" && sample.CurrentDifferentCount != sample.StoredForeignCount}.",
                            !firstGridMismatchSeen && sample.Status == "ok" &&
                            sample.CurrentDifferentCount != sample.StoredForeignCount);
                    if (sample.Status == "ok" && sample.CurrentDifferentCount != sample.StoredForeignCount)
                        firstGridMismatchSeen = true;
                }
            }
            catch (Exception ex) { QueueDiagnostic("AI_BUILD_CELL_SNAPSHOT_FAILED: " + ex); }
        }

        private static readonly int[] WoodSearchDx = { 0, 1, 0, -1 };
        private static readonly int[] WoodSearchDy = { -1, 0, 1, 0 };

        private void CaptureWoodSearchShadow(int playerId, int actualX, int actualY)
        {
            if (actualX < 0 || actualY < 0) return;
            try
            {
                AivSystem* state = GameAIVManagerAPI.Instance.GetAIVSystemPointer();
                if (state == null) return;
                ulong address = (ulong)state;
                int originX = Marshal.ReadInt32(new IntPtr(checked((long)address + 0x18783C)));
                int originY = Marshal.ReadInt32(new IntPtr(checked((long)address + 0x1A083C)));
                Span<AivCoarseCell> grid = GameAIVManagerAPI.Instance.GetCoarseGrid();
                if (grid.Length < 25600 || (uint)originX >= 160 || (uint)originY >= 160) return;
                firstWoodShadowDone = true;
                firstWoodShadowPlayer = playerId;
                firstWoodShadowActualX = actualX;
                firstWoodShadowActualY = actualY;
                (int normalX, int normalY, int visited, int candidates) =
                    ReplayWoodSearch(grid, originX, originY, false, true);
                bool reproduced = normalX == actualX && normalY == actualY;
                QueueDiagnostic($"AI_BUILD_WOOD_SHADOW_CHECK: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, origin=({originX},{originY}), " +
                    $"vanilla=({actualX},{actualY}), shadow=({normalX},{normalY}), " +
                    $"reproduced={reproduced}, visited={visited}, candidates={candidates}, " +
                    "source=native-0x58020-read-only-replay.", true);
                if (reproduced)
                {
                    (int nextX, int nextY, int alternateVisited, int alternateCandidates) =
                        ReplayWoodSearch(grid, originX, originY, true, false);
                    QueueDiagnostic($"AI_BUILD_WOOD_RESOURCE_SHADOW_ALTERNATE: session={sessionId}, tick={lastTick}, " +
                        $"player={playerId}, excludedAnchorComponentZero=true, " +
                        $"candidate=({nextX},{nextY}), visited={alternateVisited}, " +
                        $"candidates={alternateCandidates}, inferenceOnly=true.", true);
                    if (nextX >= 0 && nextY >= 0)
                    {
                        AiEconomyGridEvidence alternate = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                            address, -1, nextX, nextY);
                        QueueDiagnostic($"AI_BUILD_WOOD_RESOURCE_SHADOW_ALTERNATE_CELL: session={sessionId}, " +
                            $"cell=({nextX},{nextY}), storedForeign={alternate.StoredForeignCount}, " +
                            $"liveDifferent={alternate.CurrentDifferentCount}, " +
                            $"zeroTiles={alternate.CurrentZeroCount}, treeWeight={alternate.TreeWeight}, " +
                            $"status={alternate.Status}.", true);
                    }
                }
                else QueueDiagnostic("AI_BUILD_WOOD_SHADOW_UNVERIFIED: alternate suppressed because the normal replay did not reproduce Vanilla.", true);
            }
            catch (Exception ex) { QueueDiagnostic("AI_BUILD_WOOD_SHADOW_FAILED: " + ex, true); }
        }

        private (int X, int Y, int Visited, int Candidates) ReplayWoodSearch(
            Span<AivCoarseCell> grid, int originX, int originY, bool excludeZeroAnchor, bool logCells)
        {
            // 0x58020: FIFO, four cardinal neighbors at DAT_1802D2E50[0,2,4,6],
            // strict score improvement, candidate-count/depth stop, depth cap 60.
            bool[] seenCells = new bool[25600];
            int[] queue = new int[25600];
            int[] depths = new int[25600];
            int head = 0, tail = 1, visited = 0, candidates = 0;
            int bestScore = -100, bestX = -1, bestY = -1;
            int originIndex = originX * 160 + originY;
            queue[0] = originIndex;
            depths[0] = 1;
            seenCells[originIndex] = true;
            while (head < tail)
            {
                int currentIndex = queue[head];
                int currentDepth = depths[head++];
                if (currentDepth > 60) break;
                int currentX = currentIndex / 160, currentY = currentIndex % 160;
                for (int direction = 0; direction < 4; direction++)
                {
                    int x = currentX + WoodSearchDx[direction];
                    int y = currentY + WoodSearchDy[direction];
                    if ((uint)x >= 160 || (uint)y >= 160) continue;
                    int index = x * 160 + y;
                    if (seenCells[index]) continue;
                    seenCells[index] = true;
                    visited++;
                    AivCoarseCell cell = grid[index];
                    int foreign = unchecked((sbyte)cell.ForeignPathComponentTileCount);
                    int tree = unchecked((sbyte)cell.TreeObstructionWeight);
                    bool traversed = foreign < 16 && cell.WoodcutterRetryDelay == 0;
                    bool candidate = traversed && foreign < 6 && tree > 0;
                    int anchorComponent = ReadCoarseAnchorComponent(x, y);
                    bool excluded = candidate && excludeZeroAnchor && anchorComponent == 0;
                    string gate = !traversed ? foreign >= 16 ? "foreign>=16" : "retry" :
                        !candidate ? foreign >= 6 ? "foreign>=6" : "tree<=0" :
                        excluded ? "hypothetical-anchor-component-zero" : "candidate";
                    if (logCells)
                        QueueDiagnostic($"AI_BUILD_WOOD_SHADOW_VISIT: session={sessionId}, tick={lastTick}, " +
                            $"ordinal={visited}, cell=({x},{y}), parent=({currentX},{currentY}), " +
                            $"depth={currentDepth}, foreign={foreign}, treeWeight={tree}, " +
                            $"retry={cell.WoodcutterRetryDelay}, scoreAdjustment={cell.Unknown06}, " +
                            $"anchorComponent={anchorComponent}, firstGate={gate}.");
                    if (!traversed) continue;
                    if (candidate && !excluded)
                    {
                        candidates++;
                        int score = tree * 5 - currentDepth * 3;
                        if (cell.Unknown06 != 0) score = score < 1 ? score * 2 : score / 2;
                        if (score > bestScore)
                        { bestScore = score; bestX = x; bestY = y; }
                        if (candidates > 10 || candidates > 5 && currentDepth > 20 ||
                            candidates > 0 && currentDepth > 30)
                            break;
                    }
                    if (tail < queue.Length)
                    {
                        queue[tail] = index;
                        depths[tail++] = currentDepth + 1;
                    }
                }
            }
            return (bestX, bestY, visited, candidates);
        }

        private static int ReadCoarseAnchorComponent(int coarseX, int coarseY)
        {
            int x = coarseX * 5, y = coarseY * 5;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y)) return -1;
            int tileId = tiles.GetTileId(x, y);
            Span<ushort> pcl = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            return tileId > 0 && (uint)tileId < (uint)pcl.Length ? pcl[tileId] : -1;
        }

        private void Log(string value)
        {
            int bytes = Encoding.UTF8.GetByteCount(value) + 2;
            bool preserved = value.StartsWith("AI_BUILD_WALL_CHANGE:", StringComparison.Ordinal) ||
                value.StartsWith("AI_BUILD_WALL_MAP_CHANGE:", StringComparison.Ordinal) ||
                value.StartsWith("AI_BUILD_DIAGNOSTIC_OVERFLOW:", StringComparison.Ordinal) ||
                value.StartsWith("AI_BUILD_DIAGNOSTIC_BUDGET:", StringComparison.Ordinal) ||
                value.StartsWith("AI_BUILD_WOOD_SHADOW_CHECK:", StringComparison.Ordinal) ||
                value.StartsWith("AI_BUILD_WOOD_RESOURCE_SHADOW_ALTERNATE:", StringComparison.Ordinal);
            if (!preserved && writtenBytes + bytes > DiagnosticBudgetBytes)
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
