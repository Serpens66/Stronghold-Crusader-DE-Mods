// Feature: Weighted replacement for Vanilla's Assassin-only path-cost expansion.
using BepInEx.Logging;
using APIShared;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class AssassinPathfindingRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate byte SpecialTilePredicateDelegate(IntPtr context, int tileId);

        private const int MapWidth = 800;
        private const int CoordinateCount = MapWidth * MapWidth;
        private const int TileCount = 320800;
        private const int MaximumCommittedPathLength = 2000;
        private const int AssassinBuilderRva = 0xD9C40;
        private const int SpecialTilePredicateRva = 0x107160;
        private const int SpecialTilePredicateContextRva = 0x32DE440;
        private const int ValidCoordinateGridRva = 0x3A11EA4;
        private const int RowLookupRva = 0x402FF2C;
        private const int TileFlagsRva = 0x48F71B0;
        private const int BuildingLayerRva = 0x4B6AA50;
        private const int HeightLayerRva = 0x4DDD350;
        private const int OccupancyLayerRva = 0x51890D0;
        private const int NativeDistanceLayerRva = 0x5225B10;
        private const int NativeVisitStampLayerRva = 0x52C2550;
        private const int DirectionMaskRva = 0x312620;
        private const uint AssassinFallbackBlockingMask = 0x4A5014B1u;
        private const uint NativeSpecialTileFlag = 1u << 12;
        private const uint IsWallFlag = 1u << 8;
        private const uint IsStairsFlag = 1u << 11;
        private const uint IsLowWallFlag = 1u << 16;
        private const byte GroundEdgeKind = 1;
        private const byte ClimbEdgeKind = 2;
        private const byte MoveCommandKind = 1;
        private const byte TargetCommandKind = 2;
        // Development traces require both the dedicated build and the local diagnostic opt-in.
#if SERP_DEVDIAG
        private static bool PerformanceDiagnosticsEnabled => Shared.DebugLogHelper.DiagnosticsEnabled;
#else
        private const bool PerformanceDiagnosticsEnabled = false;
#endif
        private const int MaximumDetailedRequestsPerCommand = 8;
#if SERP_DEVDIAG
        private static bool DetailedDiagnosticsEnabled => Shared.DebugLogHelper.DiagnosticsEnabled;
#else
        private const bool DetailedDiagnosticsEnabled = false;
#endif
        private const string AssassinBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 EA 48 8B D9 49 63 F9";

        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly AssassinClimbRuntime climbRuntime;
        private readonly int[] costs = new int[CoordinateCount];
        private readonly int[] parents = new int[CoordinateCount];
        private readonly int[] insertionOrder = new int[CoordinateCount];
        private readonly int[] estimatedTotalCosts = new int[CoordinateCount];
        private readonly int[] heap = new int[CoordinateCount];
        private readonly HeapKey[] heapKeys = new HeapKey[CoordinateCount];
        private readonly int[] heapPositions = new int[CoordinateCount];
        private readonly int[] touched = new int[CoordinateCount];
        private readonly byte[] incomingEdgeKinds = new byte[CoordinateCount];
        private readonly int[] route = new int[MaximumCommittedPathLength + 1];
        private readonly byte[] seenTiles = new byte[TileCount];
        private IntPtr libraryHandle;
        private SpecialTilePredicateDelegate specialTilePredicate;
        private bool sharedBuilderRegistered;
        private AssassinPathReconstructionPatch reconstructionPatch;
        private byte* validCoordinates;
        private int* rowLookup;
        private uint* tileFlags;
        private ushort* buildingLayer;
        private byte* heightLayer;
        private byte* occupancyLayer;
        private short* nativeDistances;
        private short* nativeVisitStamps;
        private byte* directionMasks;
        private int heapCount;
        private int touchedCount;
        private int nextInsertionOrder;
        private int heapOperations;
        private bool fallbackLogged;
        private bool coordinateMapValidated;
        private bool coordinateValidationFailureLogged;
        private int mapEpoch;
        private IDisposable moveCommandSubscription;
        private IDisposable targetCommandSubscription;
        private AssassinCommandScope activeCommand;
        private int commandSequence;
        private bool commandScopeMismatchLogged;

        // TEMP_GATE_ROUTE_ACCEPTANCE: rooted reader only; never drives reconstruction behavior.
#if SERP_DEVDIAG
        private static AssassinPathfindingRuntime temporaryDiagnosticRuntime;
        internal static string TemporaryReconstructionRelaxation => temporaryDiagnosticRuntime?.reconstructionPatch == null
            ? "unavailable" : temporaryDiagnosticRuntime.reconstructionPatch.IsApplied ? "active" : "inactive";
#endif

        public AssassinPathfindingRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings, AssassinClimbRuntime climbRuntime)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.climbRuntime = climbRuntime ?? throw new ArgumentNullException(nameof(climbRuntime));
#if SERP_DEVDIAG
            temporaryDiagnosticRuntime = this; // TEMP_GATE_ROUTE_ACCEPTANCE
#endif
            for (int node = 0; node < CoordinateCount; node++)
            {
                costs[node] = int.MaxValue;
                parents[node] = -1;
                heapPositions[node] = -1;
            }
        }

        public bool IsInstalled => sharedBuilderRegistered && AssassinPathAPI.IsInstalled;

        public void InitializeNative(
            IntPtr newLibraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool fixedLayoutHashValidated)
        {
            if (IsInstalled)
                return;
            if (region == null)
                throw new ArgumentNullException(nameof(region));
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (newLibraryHandle == IntPtr.Zero || memory.Length <= NativeVisitStampLayerRva + TileCount * sizeof(short))
                throw new InvalidOperationException("native module memory does not cover the required Assassin pathfinding layers");

            libraryHandle = newLibraryHandle;
            validCoordinates = (byte*)IntPtr.Add(newLibraryHandle, ValidCoordinateGridRva).ToPointer();
            rowLookup = (int*)IntPtr.Add(newLibraryHandle, RowLookupRva).ToPointer();
            tileFlags = (uint*)IntPtr.Add(newLibraryHandle, TileFlagsRva).ToPointer();
            buildingLayer = (ushort*)IntPtr.Add(newLibraryHandle, BuildingLayerRva).ToPointer();
            heightLayer = (byte*)IntPtr.Add(newLibraryHandle, HeightLayerRva).ToPointer();
            occupancyLayer = (byte*)IntPtr.Add(newLibraryHandle, OccupancyLayerRva).ToPointer();
            nativeDistances = (short*)IntPtr.Add(newLibraryHandle, NativeDistanceLayerRva).ToPointer();
            nativeVisitStamps = (short*)IntPtr.Add(newLibraryHandle, NativeVisitStampLayerRva).ToPointer();
            directionMasks = (byte*)IntPtr.Add(newLibraryHandle, DirectionMaskRva).ToPointer();
            specialTilePredicate = Marshal.GetDelegateForFunctionPointer<SpecialTilePredicateDelegate>(
                IntPtr.Add(newLibraryHandle, SpecialTilePredicateRva));

            AssassinPathReconstructionPatch pendingReconstructionPatch = null;
            try
            {
                pendingReconstructionPatch = new AssassinPathReconstructionPatch(
                    log,
                    newLibraryHandle,
                    region,
                    memory,
                    referenceHashMatches: true);
                reconstructionPatch = pendingReconstructionPatch;
                moveCommandSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(ObserveMoveCommand);
                targetCommandSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                    .Subscribe(ObserveTargetCommand);
                AssassinPathAPI.RegisterWeightedBuilder(BugfixesAndQoLPlugin.PluginGuid, BuildWeightedPath);
                sharedBuilderRegistered = true;
                ApplySetting();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogDebug($"weighted Assassin pathfinding installed at RVA 0x{AssassinBuilderRva:X}; climb costs={AssassinClimbCostPolicy.MinimumClimbTicks}/{AssassinClimbCostPolicy.LowWallClimbTicks}/{AssassinClimbCostPolicy.NormalWallClimbTicks} ticks.");
            }
            catch
            {
                if (sharedBuilderRegistered) throw; // Published runtime must remain rooted.
                moveCommandSubscription?.Dispose();
                moveCommandSubscription = null;
                targetCommandSubscription?.Dispose();
                targetCommandSubscription = null;
                if (pendingReconstructionPatch?.IsApplied == true)
                    pendingReconstructionPatch.SetEnabled(false);
                reconstructionPatch = null;
                throw;
            }
        }

        public void ApplySetting()
        {
            AssassinPathReconstructionPatch patch = reconstructionPatch;
            if (patch == null)
                return;

            // The native reconstruction rule is global, not per player: it stays active while either player
            // kind uses weighted routes. With only the AI setting on, human players' Vanilla routes also get
            // the corrected one-way connection step (no failed reconstruction and full-map repair).
            patch.SetEnabled(AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(
                settings.EnableMod,
                settings.EnableImprovedAssassinPathfinding || settings.EnableImprovedAssassinPathfindingAI,
                IsInstalled));
        }

        // EnableImprovedAssassinPathfinding covers human players (and keeps the climb button and Stop
        // cancellation, which only humans use); EnableImprovedAssassinPathfindingAI covers AI lords.
        private bool IsWeightedEnabledFor(bool aiPlayer) =>
            settings.EnableMod &&
            (aiPlayer ? settings.EnableImprovedAssassinPathfindingAI : settings.EnableImprovedAssassinPathfinding);

        // Player kind from the game's player table: the same on every peer, so the choice of the
        // weighted or the Vanilla route stays deterministic in multiplayer.
        private static bool IsAIPlayer(int playerId) => GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);

        public void BeginMap()
        {
            mapEpoch++;
            ClearTransientState();
            ResetMapValidation();
        }

        public void EndMap()
        {
            mapEpoch++;
            ClearTransientState();
            ResetMapValidation();
        }

        public void Dispose()
        {
            if (sharedBuilderRegistered) return; // Published handler and subscriptions live until process exit.
            moveCommandSubscription?.Dispose();
            moveCommandSubscription = null;
            targetCommandSubscription?.Dispose();
            targetCommandSubscription = null;
            ClearTransientState();
        }

        [ThreadStatic] private static AssassinObservation activeObservation;
        private sealed class AssassinObservation
        {
            internal IEnemyGateAssassinObserver Observer;
            internal object Token;
            internal IEnemyBridgePathObserver BridgeObserver;
            internal object BridgeToken;
            internal int Player = -1, NativeResult, EffectiveResult, RouteLength;
            internal long FilteredGround, FilteredClimb;
            internal bool CacheHit;
            internal bool? EffectiveWeighted, EffectiveClimbing, ReservedEndpoints;
            internal string Outcome = "native-exception", Error;
            // TEMP_GATE_ROUTE_ACCEPTANCE: current parents only, discarded with the synchronous search.
#if SERP_DEVDIAG
            internal ITemporaryAssassinGateObserver DetailedObserver;
            internal Dictionary<int, GateTransitionEvidence> ParentDecisions;
            internal GateTransitionEvidence LastDecision;
#endif
        }
#if SERP_DEVDIAG
        private sealed class GateTransitionEvidence
        {
            internal int From, To, Direction, Gate;
            internal uint Global;
            internal AssassinTransitionKind Movement;
            internal bool Allowed;
            internal string Detail;
        }
#endif

        private int BuildWeightedPath(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            IEnemyGateAssassinObserver observer = null;
            try
            {
                IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
                if (policy != null && policy.HasPublishedMask) observer = policy as IEnemyGateAssassinObserver;
            }
            catch (Exception ex) { LogWarning("Assassin diagnostic registration failed: " + ex.GetType().Name); }
            IEnemyBridgePathObserver bridgeObserver = EnemyBridgeDiagnosticBridge.Current;
            AssassinObservation previous = activeObservation;
            AssassinObservation observation = null;
            // TEMP_GATE_ROUTE_ACCEPTANCE: link only to an already active publication call.
            object temporaryNativeSearch = null;
#if SERP_DEVDIAG
            temporaryNativeSearch = UnitCommandPathRuntime.BeginTemporaryAssassinSearch(context,
                startX, startY, targetX, targetY, continuation);
            bool temporaryCompleted = false;
            int temporaryResult = 0;
#endif
            try
            {
                if (observer != null || bridgeObserver != null || temporaryNativeSearch != null)
                {
                    observation = new AssassinObservation { Observer = observer, BridgeObserver = bridgeObserver };
                    try
                    {
                        observation.Token = observer?.BeginAssassinSearch(startX, startY, targetX, targetY,
                            maximumNodes, continuation, DescribeNativeAssassinState(context));
                    }
                    catch (Exception ex) { LogWarning("Assassin diagnostic begin failed: " + ex.GetType().Name); }
                }
                if (observation != null && bridgeObserver != null)
                    try { observation.BridgeToken = bridgeObserver.BeginAssassinSearch(startX, startY, targetX, targetY,
                        maximumNodes, continuation, DescribeNativeAssassinState(context)); }
                    catch (Exception ex) { LogWarning("Bridge Assassin begin failed: " + ex.GetType().Name); }
#if SERP_DEVDIAG
                if (observation != null && observer is ITemporaryAssassinGateObserver detailed)
                {
                    observation.DetailedObserver = detailed;
                    observation.ParentDecisions = new Dictionary<int, GateTransitionEvidence>();
                    try { detailed.ObserveAssassinStage(observation.Token, observation.Player, "search-entry", "entered",
                        UnitCommandPathRuntime.CaptureTemporaryAssassinSource() + ",mode=" + continuation +
                        ",start=" + startX + "/" + startY + ",target=" + targetX + "/" + targetY +
                        ",nativeState=" + DescribeNativeAssassinState(context) +
                        ",directGateClimb=" + AssassinPathAPI.DirectGatehouseClimbingEnabled + ",weightedConfigured=" + IsWeightedEnabledFor(aiPlayer: false) + "/ai=" + IsWeightedEnabledFor(aiPlayer: true)); }
                    catch (Exception ex) { TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-entry", ex); }
                }
#endif

                activeObservation = observation;
                int result = BuildWeightedPathCore(context, startX, startY, targetX, targetY, maximumNodes, continuation);
#if SERP_DEVDIAG
                temporaryCompleted = true; temporaryResult = result;
#endif
                if (observation != null) observation.EffectiveResult = result;
                return result;
            }
            finally
            {
#if SERP_DEVDIAG
                UnitCommandPathRuntime.EndTemporaryAssassinSearch(temporaryNativeSearch, temporaryCompleted,
                    observation?.NativeResult ?? 0, temporaryResult, observation?.Player ?? -1,
                    observation?.Outcome ?? "unobserved", observation?.CacheHit ?? false, observation?.RouteLength ?? 0);

                if (observation?.DetailedObserver != null)
                    try { observation.DetailedObserver.ObserveAssassinStage(observation.Token, observation.Player, "search-exit",
                        observation.Outcome, "native=" + observation.NativeResult + ",effective=" + observation.EffectiveResult +
                        ",effectiveWeighted=" + observation.EffectiveWeighted + ",climbingAllowed=" + observation.EffectiveClimbing +
                    ",reservedEndpoints=" + observation.ReservedEndpoints + ",completed=" + temporaryCompleted + ",cache=" + observation.CacheHit + ",nodes=" + observation.RouteLength +
                        ",requestReason=" + AssassinPathAPI.TemporaryWeightedRequestReason); }
                    catch (Exception ex) { TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-exit", ex); }
#endif

                activeObservation = previous;
                if (observation != null && bridgeObserver != null)
                    try { bridgeObserver.ObserveAssassinPolicyFiltering(observation.BridgeToken, observation.Player,
                        observation.FilteredGround, observation.FilteredClimb); }
                    catch (Exception ex) { LogWarning("Bridge Assassin filtering failed: " + ex.GetType().Name); }
                if (observation != null && bridgeObserver != null)
                    try { bridgeObserver.EndAssassinSearch(observation.BridgeToken, observation.Player,
                        observation.NativeResult, observation.EffectiveResult, observation.Outcome + observation.Error,
                        observation.CacheHit, observation.RouteLength); }
                    catch (Exception ex) { LogWarning("Bridge Assassin end failed: " + ex.GetType().Name); }
                if (observation != null && observer != null)
                    try { observer.ObserveAssassinPolicyFiltering(observation.Token, observation.Player,
                        observation.FilteredGround, observation.FilteredClimb); }
                    catch (Exception ex) { LogWarning("Assassin filtering diagnostic failed: " + ex.GetType().Name); }
                if (observation != null && observer != null)
                    try { observer.EndAssassinSearch(observation.Token, observation.Player,
                        observation.NativeResult, observation.EffectiveResult, observation.Outcome + observation.Error,
                        observation.CacheHit, observation.RouteLength); }
                    catch (Exception ex) { LogWarning("Assassin diagnostic end failed: " + ex.GetType().Name); }
            }
        }

        private void SetOutcome(string outcome)
        {
            if (activeObservation != null) activeObservation.Outcome = outcome;
        }

        private string DescribeNativeAssassinState(IntPtr context)
        {
            // Every audited D9C40 caller passes this singleton; unknown pointers are not read.
            if (context != IntPtr.Add(libraryHandle, 0x60AD660)) return "unknown-context";
            byte* pointer = (byte*)context.ToPointer();
            var positive = new List<string>(10);
            var negative = new List<string>(10);
            for (int index = 0; index < 10; index++)
            {
                int* accepted = (int*)(pointer + 0x416D8C + index * 8);
                int* rejected = (int*)(pointer + 0x416DDC + index * 8);
                positive.Add(accepted[0] + "/" + accepted[1]);
                negative.Add(rejected[0] + "/" + rejected[1]);
            }
            return "flags=" + *(int*)(pointer + 0x84) + "/" + *(int*)(pointer + 0x88) +
                ",pairCacheInitialized=" + *(int*)(pointer + 0x90) +
                ",positivePairs=[" + string.Join(";", positive) + "],negativePairs=[" +
                string.Join(";", negative) + "],pairCacheHit=not-observed-at-builder";
        }

        private void ObservePreparedAssassinRoute(int player, int routeLength)
        {
            AssassinObservation observation = activeObservation;
            if (observation == null) return;
            observation.RouteLength = routeLength;
            try
            {
                for (int index = routeLength - 1; index > 0; index--)
                {
                    int current = route[index], next = route[index - 1];
                    int dx = next % MapWidth - current % MapWidth;
                    int dy = next / MapWidth - current / MapWidth;
                    int direction = -1;
                    for (int candidate = 0; candidate < 8; candidate++)
                        if (DirectionX[candidate] == dx && DirectionY[candidate] == dy)
                        { direction = candidate; break; }
                    int fromTile = GetTileId(current % MapWidth, current / MapWidth);
                    int toTile = GetTileId(next % MapWidth, next / MapWidth);
                    if (direction < 0 || !IsNativeTile(fromTile) || !IsNativeTile(toTile))
                        throw new InvalidOperationException("Invalid prepared diagnostic edge");
                    bool climb = !HasOrdinaryConnection(fromTile, toTile, direction);
                    try
                    {
#if SERP_DEVDIAG
                        if (observation.DetailedObserver != null && observation.ParentDecisions.TryGetValue(toTile, out GateTransitionEvidence evidence) &&
                            evidence.From == fromTile && evidence.To == toTile)
                            observation.DetailedObserver.ObserveAssassinDecision(observation.Token, player, fromTile, toTile, direction,
                                true, evidence.Movement, evidence.Allowed, evidence.Gate, evidence.Global, evidence.Detail);
                        else if (observation.DetailedObserver != null)
                        {
                            // TEMP_GATE_ROUTE_ACCEPTANCE: same ordinary connection and height layers used by route costs.
                            AssassinTransitionKind movement = !climb ? AssassinTransitionKind.Ground :
                                heightLayer[toTile] > heightLayer[fromTile] ? AssassinTransitionKind.ClimbUp :
                                heightLayer[toTile] < heightLayer[fromTile] ? AssassinTransitionKind.ClimbDown : AssassinTransitionKind.Unknown;
                            observation.DetailedObserver.ObserveAssassinDecision(observation.Token, player, fromTile, toTile, direction,
                                true, movement, true, 0, 0, "movementSource=prepared-weighted-route,gateValidatorEvidence=not-retained,weightedClimb=" + climb);
                        }
                        else observation.Observer?.ObserveAssassinEdge(observation.Token, player, fromTile, toTile, direction, climb);
#else
                        observation.Observer?.ObserveAssassinEdge(observation.Token, player, fromTile, toTile, direction, climb);
#endif

                    }
                    catch (Exception ex) { LogWarning("Assassin diagnostic edge failed: " + ex.GetType().Name); }
                    try { observation.BridgeObserver?.ObserveAssassinEdge(observation.BridgeToken, player, fromTile, toTile, direction, climb); }
                    catch (Exception ex) { LogWarning("Bridge Assassin edge failed: " + ex.GetType().Name); }
                }
            }
            catch (Exception ex)
            {
                observation.Error = "/diagnostic-edge-error:" + ex.GetType().Name;
                LogWarning("Assassin diagnostic route failed: " + ex.GetType().Name);
            }
        }

        private int BuildWeightedPathCore(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            if (!IsInstalled)
                return 0;

            AssassinCommandScope command = activeCommand;
            long requestStarted = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
            long nativeStarted = requestStarted;
            // DEVDIAG request mix (all callers, also outside command scopes): counts and stage times.
            long diagRequest = 0, diagStage = 0;
            Shared.DevDiag.Start(ref diagRequest);
            Shared.DevDiag.Count(command == null ? "bfqol.assassin.req.noCommand" : "bfqol.assassin.req.command");
            Shared.DevDiag.Count(maximumNodes >= TileCount ? "bfqol.assassin.req.fullBudget" : "bfqol.assassin.req.limitedBudget");
            Shared.DevDiag.Start(ref diagStage);
            // Vanilla initializes internal queue state even when our compact route field replaces it.
            int vanillaResult = AssassinPathAPI.RunVanillaBuilder(context, startX, startY, targetX, targetY, maximumNodes, continuation);
            Shared.DevDiag.Stop(ref diagStage, vanillaResult != 0 ? "bfqol.assassin.nativeBuilder.found" : "bfqol.assassin.nativeBuilder.none");
            if (activeObservation != null)
            { activeObservation.NativeResult = vanillaResult; activeObservation.Outcome = "native-only"; }
            long nativeTicks = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - nativeStarted;
            if (PerformanceDiagnosticsEnabled) command?.RecordNativeBuilder(nativeTicks);

            bool enabled = command?.Enabled ??
                (IsWeightedEnabledFor(aiPlayer: false) || IsWeightedEnabledFor(aiPlayer: true));
            if (activeObservation != null) activeObservation.EffectiveWeighted = enabled;
            if (!enabled || continuation != 0)
            {
                SetOutcome(!enabled ? "weighted-disabled" : "continuation");
                return vanillaResult;
            }
            if (targetX < 0 || targetY < 0)
            {
                SetOutcome("native-flood-field");
                return vanillaResult;
            }

            try
            {
                backwardProofUseful = vanillaResult == 0;
                long resolutionStarted = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
                if (!TryResolveAssassinRequest(command, context, startX, startY, targetX, targetY, out int playerId, out int speedDelay))
                {
                    if (PerformanceDiagnosticsEnabled) command?.RecordResolution((PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - resolutionStarted);
                    SetOutcome("unresolved-player");
                    return vanillaResult;
                }
                if (PerformanceDiagnosticsEnabled) command?.RecordResolution((PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - resolutionStarted);
                if (activeObservation != null) activeObservation.Player = playerId;
#if SERP_DEVDIAG
                CountRequestSharing(command, playerId, startX, startY, targetX, targetY);
#endif
                // Separate host settings for human players and AI lords; same snapshot as the command.
                bool aiPlayer = IsAIPlayer(playerId);
                Shared.DevDiag.Count(aiPlayer ? "bfqol.assassin.req.aiPlayer" : "bfqol.assassin.req.humanPlayer");
                if (!(command?.IsEnabledFor(aiPlayer) ?? IsWeightedEnabledFor(aiPlayer)))
                {
                    if (activeObservation != null) activeObservation.EffectiveWeighted = false;
                    SetOutcome("weighted-disabled-player-kind");
                    return vanillaResult;
                }
                if (!EnsureCoordinateTileMappingValidated())
                {
                    SetOutcome("coordinate-map-unready");
                    return vanillaResult;
                }

                if (!AssassinGateRoutePolicy.TryCapture(EnemyGatePathPolicyBridge.Current, playerId,
                    out IEnemyGateRoutePolicySnapshot gatePolicy))
                {
                    SetOutcome("gate-context-fallback");
                    return vanillaResult;
                }
                bool allowClimbing = command?.GetClimbingAllowed(playerId, climbRuntime) ??
                    climbRuntime.IsClimbingAllowed(playerId);
                // Never publish a relaxed route unless Vanilla can reconstruct the same
                // validated reserved climb endpoints. This keeps patch failures fail-closed.
                bool allowWalkableReservedClimbEndpoints = reconstructionPatch?.IsApplied == true;
                if (activeObservation != null) { activeObservation.EffectiveClimbing = allowClimbing;
                    activeObservation.ReservedEndpoints = allowWalkableReservedClimbEndpoints; }
                var cacheKey = new RouteCacheKey(
                    startX, startY, targetX, targetY, maximumNodes, speedDelay,
                    playerId, allowClimbing, allowWalkableReservedClimbEndpoints, gatePolicy, AssassinPathAPI.DirectGatehouseClimbingEnabled);
                RouteSearchSummary routeSummary = default;
                CachedRoute preparedRoute = null;
                long cacheStarted = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
                bool routeReady = command != null &&
                    TryLoadCachedRoute(command, cacheKey, out routeSummary, out preparedRoute);
                if (PerformanceDiagnosticsEnabled) command?.RecordCacheLookup((PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - cacheStarted);
                if (routeReady) Shared.DevDiag.Count("bfqol.assassin.cacheHit");
                if (!routeReady && UsesRouteExistenceOnly(context, startX, startY, targetX, targetY, maximumNodes))
                {
                    // Without an exact single-unit frame the weighted route is never published:
                    // this request returns Vanilla's result when a valid weighted route exists and
                    // 0 otherwise. Answer exactly that question without the cost-optimal search.
                    Shared.DevDiag.Start(ref diagStage);
                    RouteExistence existence = FindAnyWeightedRoute(startX, startY, targetX, targetY, speedDelay,
                        allowClimbing, allowWalkableReservedClimbEndpoints, gatePolicy);
                    Shared.DevDiag.Stop(ref diagStage, existence == RouteExistence.Exists ? "bfqol.assassin.exist.exists" :
                        existence == RouteExistence.None ? "bfqol.assassin.exist.none" : "bfqol.assassin.exist.undecided");
                    Shared.DevDiag.Count(vanillaResult != 0 ? "bfqol.assassin.exist.nativeFound" : "bfqol.assassin.exist.nativeNone");
                    if (existence != RouteExistence.Undecided)
                    {
                        if (!AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                        {
                            SetOutcome("gate-snapshot-fallback");
                            return vanillaResult;
                        }
                        return existence == RouteExistence.Exists ? vanillaResult : 0;
                    }
                }
                if (!routeReady)
                {
#if SERP_DEVDIAG
                    activeObservation?.ParentDecisions?.Clear();
#endif

                    Shared.DevDiag.Start(ref diagStage);
                    routeReady = TryBuildWeightedRoute(
                        startX,
                        startY,
                        targetX,
                        targetY,
                        maximumNodes,
                        speedDelay,
                        allowClimbing,
                        allowWalkableReservedClimbEndpoints,
                        command,
                        gatePolicy,
                        out routeSummary);
                    Shared.DevDiag.Stop(ref diagStage, routeReady
                        ? (vanillaResult != 0 ? "bfqol.assassin.astar.found.nativeFound" : "bfqol.assassin.astar.found.nativeNone")
                        : (vanillaResult != 0 ? "bfqol.assassin.astar.none.nativeFound" : "bfqol.assassin.astar.none.nativeNone"));
                    if (routeReady && command != null)
                        preparedRoute = CachePreparedRoute(command, cacheKey, routeSummary);
                }

                if (activeObservation != null)
                { activeObservation.CacheHit = routeSummary.CacheHit;
                  activeObservation.Outcome = routeReady ? "weighted-prepared" : "weighted-no-route"; }
                if (!AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                {
                    SetOutcome("gate-snapshot-fallback");
                    return vanillaResult;
                }
                if (!routeReady)
                    return 0;
                ObservePreparedAssassinRoute(playerId, routeSummary.RouteLength);

                long publicationStarted = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
                // E1640 selects its own neighbors from step distances, losing our weights.
                // An owned F4930 frame instead receives the exact route after Vanilla returns.
                if (TryStagePreparedRoute(context, cacheKey, routeSummary, preparedRoute, command))
                {
                    SetOutcome("weighted-staged-exact");
                    return vanillaResult;
                }
                if (AssassinPathAPI.HasSingleUnitRoutePublication)
                {
                    SetOutcome("weighted-exact-fallback");
                    return vanillaResult;
                }
                // Validate before the first native stamp/distance write. A stale policy
                // must preserve the original native field, not publish a partial replacement.
                if (cacheKey.DirectGatehouseClimbing != AssassinPathAPI.DirectGatehouseClimbingEnabled ||
                    !ValidatePreparedGateRoute(gatePolicy, routeSummary.RouteLength, allowClimbing, allowWalkableReservedClimbEndpoints) ||
                    !AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                {
                    SetOutcome("gate-publication-fallback");
                    return vanillaResult;
                }
                bool published = CommitPreparedRoute(context, routeSummary.RouteLength);
                if (PerformanceDiagnosticsEnabled) command?.RecordPublication(
                    (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - publicationStarted,
                    published,
                    routeSummary);
                if (activeObservation != null) activeObservation.Outcome = published
                    ? "weighted-published" : "weighted-publication-failed";
                if (!published)
                {
                    LogError(
                        $"Assassin route publication contract failed: commandSeq={command?.Sequence ?? 0} " +
                        $"player={playerId} start={startX},{startY} target={targetX},{targetY} " +
                        $"routeLength={routeSummary.RouteLength}.");
                    return 0;
                }
                return 1;
            }
            catch (Exception ex)
            {
                SetOutcome("exception-fallback:" + ex.GetType().Name);
                if (!fallbackLogged)
                {
                    fallbackLogged = true;
                    LogError($"weighted Assassin pathfinding failed and this request fell back to Vanilla: {ex}");
                }
                return vanillaResult;
            }
            finally
            {
                backwardProofUseful = true;
                if (PerformanceDiagnosticsEnabled) command?.RecordTotal((PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - requestStarted);
                Shared.DevDiag.Stop(ref diagRequest, "bfqol.assassin.request");
            }
        }

        private bool TryBuildWeightedRoute(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int speedDelay,
            bool allowClimbing,
            bool allowWalkableReservedClimbEndpoints,
            AssassinCommandScope command,
            IEnemyGateRoutePolicySnapshot gatePolicy,
            out RouteSearchSummary routeSummary,
            bool allowBuckets = true)
        {
            long searchStarted = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
            routeSummary = default;
            if (!IsValidCoordinate(startX, startY) || !IsValidCoordinate(targetX, targetY))
                return false;

            ResetTouchedNodes();
            int cardinalTicks = AssassinClimbCostPolicy.GetCardinalMovementTicks(speedDelay);
            int diagonalTicks = AssassinClimbCostPolicy.GetDiagonalMovementTicks(speedDelay);
            int startTile = GetTileId(startX, startY);
            int targetTile = GetTileId(targetX, targetY);
            if (!IsNativeTile(startTile) || !IsNativeTile(targetTile))
                return false;
            if (ProvesTargetUnreachable(startX, startY, targetX, targetY, allowClimbing, allowWalkableReservedClimbEndpoints, gatePolicy))
            {
                if (PerformanceDiagnosticsEnabled) command?.RecordFailedSearch(
                    (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - searchStarted, 0, 0);
                return false;
            }

            int startNode = GetCoordinateIndex(startX, startY);
            int targetNode = GetCoordinateIndex(targetX, targetY);
            heapOperations = 0;
            SuffixCacheKey suffixKey = new SuffixCacheKey(
                targetX, targetY, speedDelay, allowClimbing,
                allowWalkableReservedClimbEndpoints, gatePolicy?.PlayerId ?? 0, gatePolicy, AssassinPathAPI.DirectGatehouseClimbingEnabled);
            Dictionary<int, int> suffixCosts = command?.GetSuffixCosts(suffixKey);
            Touch(startNode, 0, -1, 0,
                EstimateRemainingTicks(
                    startX, startY, targetX, targetY,
                    cardinalTicks, diagonalTicks, suffixCosts, startNode));
            // Bucket queue for the plain octile estimate (consistent, integer ticks, bounded edges);
            // suffix estimates and out-of-ring keys keep or fall back to the heap (see BucketPush).
            bool buckets = allowBuckets && suffixCosts == null && diagonalTicks <= MaximumBucketStepTicks;
            if (buckets) BucketStart(startNode); else Push(startNode);
            int expanded = 0;
            int nodeLimit = Math.Max(1, Math.Min(maximumNodes, TileCount));

            while ((buckets ? bucketCount > 0 : heapCount > 0) && expanded < nodeLimit)
            {
                int currentNode = buckets ? BucketPop() : Pop();
                if (currentNode < 0) break;
                expanded++;
                if (currentNode == targetNode)
                {
                    if (buckets) BucketClear();
                    Shared.DevDiag.Count("bfqol.assassin.astar.expanded.found", expanded);
                    long reconstructionStarted = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
                    if (!PrepareRoute(startNode, targetNode, costs[targetNode], expanded,
                        heapOperations, searchStarted, reconstructionStarted, out routeSummary))
                    {
                        if (PerformanceDiagnosticsEnabled) command?.RecordFailedSearch(
                            (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - searchStarted, expanded, heapOperations);
                        return false;
                    }
                    if (PerformanceDiagnosticsEnabled) command?.RecordSearch(routeSummary);
                    return true;
                }

                int currentX = currentNode % MapWidth;
                int currentY = currentNode / MapWidth;
                int currentTile = GetTileId(currentX, currentY);
                if (!IsNativeTile(currentTile))
                    continue;

                uint currentFlags = tileFlags[currentTile];
                for (int direction = 0; direction < DirectionX.Length; direction++)
                {
                    int nextX = currentX + DirectionX[direction];
                    int nextY = currentY + DirectionY[direction];
                    if (!IsValidCoordinate(nextX, nextY))
                        continue;

                    int nextTile = GetTileId(nextX, nextY);
                    if (!IsNativeTile(nextTile))
                        continue;

                    int nextNode = GetCoordinateIndex(nextX, nextY);
                    bool cardinal = (direction & 1) == 0;
                    int movementTicks = cardinal ? cardinalTicks : diagonalTicks;
                    // Climb ticks are never negative, so the movement cost alone bounds this edge from
                    // below. A neighbor already reached at that cost or cheaper is rejected by the cost
                    // test below on every path; skip its pure edge predicates (gate policy, climb
                    // endpoints/surface, native vegetation predicate). Observers keep the full order.
                    if (activeObservation == null && (long)costs[currentNode] + movementTicks >= costs[nextNode])
                        continue;
                    bool ordinaryEdge = HasOrdinaryConnection(currentTile, nextTile, direction);
                    bool climbEdge = false;
                    if (!ordinaryEdge)
                    {
                        if (!cardinal)
                            continue;
                        bool fallbackAccepted = IsVanillaAssassinFallback(
                            currentTile,
                            nextTile,
                            currentFlags,
                            allowWalkableReservedClimbEndpoints);
                        if (!fallbackAccepted)
                            continue;
                        climbEdge = true;
                        if (!allowClimbing)
                            continue;
                    }

                    if (!AllowsAssassinTransition(gatePolicy, currentTile, nextTile, direction, allowClimbing, allowWalkableReservedClimbEndpoints))
                    {
                        if (activeObservation != null)
                        {
                            if (climbEdge) activeObservation.FilteredClimb++;
                            else activeObservation.FilteredGround++;
                        }
                        continue;
                    }
                    int climbTicks = climbEdge ? GetClimbTicks(currentTile, nextTile) : 0;
                    int edgeCost = movementTicks > int.MaxValue - climbTicks ? int.MaxValue : movementTicks + climbTicks;
                    int newCost = costs[currentNode] > int.MaxValue - edgeCost ? int.MaxValue : costs[currentNode] + edgeCost;
                    if (newCost >= costs[nextNode])
                        continue;

                    if (costs[nextNode] == int.MaxValue)
                    {
                        int heuristic = EstimateRemainingTicks(
                            nextX, nextY, targetX, targetY,
                            cardinalTicks, diagonalTicks, suffixCosts, nextNode);
                        Touch(nextNode, newCost, currentNode,
                            climbEdge ? ClimbEdgeKind : GroundEdgeKind,
                            AssassinAStarPolicy.SaturatingAdd(newCost, heuristic));
                    }
                    else
                    {
                        costs[nextNode] = newCost;
                        parents[nextNode] = currentNode;
                        incomingEdgeKinds[nextNode] =
                            climbEdge ? ClimbEdgeKind : GroundEdgeKind;
                        int heuristic = EstimateRemainingTicks(
                            nextX, nextY, targetX, targetY,
                            cardinalTicks, diagonalTicks, suffixCosts, nextNode);
                        estimatedTotalCosts[nextNode] =
                            AssassinAStarPolicy.SaturatingAdd(newCost, heuristic);
                    }
#if SERP_DEVDIAG
            RememberTemporaryParent(nextTile);
#endif

                    if (buckets) BucketPush(nextNode); else PushOrDecrease(nextNode);
                }
                if (bucketOverflow) break;
            }
            if (buckets && bucketOverflow)
            {
                // A key outside the ring (never seen with the octile estimate): the same search on the heap.
                BucketClear();
                Shared.DevDiag.Count("bfqol.assassin.astar.bucketFallback");
                return TryBuildWeightedRoute(startX, startY, targetX, targetY, maximumNodes, speedDelay, allowClimbing,
                    allowWalkableReservedClimbEndpoints, command, gatePolicy, out routeSummary, allowBuckets: false);
            }
            if (buckets) BucketClear();

            if (PerformanceDiagnosticsEnabled) command?.RecordFailedSearch(
                (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - searchStarted, expanded, heapOperations);
            Shared.DevDiag.Count(expanded >= nodeLimit ? "bfqol.assassin.astar.expanded.nodeLimit" : "bfqol.assassin.astar.expanded.exhausted", expanded);
            return false;
        }

#if SERP_DEVDIAG
        // DEVDIAG: how often the requests of one command scope (a group order; requests outside a scope
        // share one bucket until the next scope) repeat a start, a target or a target's 8 x 8 cell of the
        // same player. Decides whether one shared field per order would replace per-request searches.
        private int diagShareScope = -1;
        private readonly HashSet<long> diagShareStarts = new HashSet<long>(), diagShareTargets = new HashSet<long>(), diagShareCells = new HashSet<long>();
        private void CountRequestSharing(AssassinCommandScope command, int playerId, int startX, int startY, int targetX, int targetY)
        {
            int scope = command?.Sequence ?? 0;
            if (scope != diagShareScope) { diagShareScope = scope; diagShareStarts.Clear(); diagShareTargets.Clear(); diagShareCells.Clear(); }
            long player = (long)playerId << 32;
            Shared.DevDiag.Count(diagShareStarts.Add(player | (uint)GetCoordinateIndex(startX, startY)) ? "bfqol.assassin.share.newStart" : "bfqol.assassin.share.repeatStart");
            Shared.DevDiag.Count(diagShareTargets.Add(player | (uint)GetCoordinateIndex(targetX, targetY)) ? "bfqol.assassin.share.newTarget" : "bfqol.assassin.share.repeatTarget");
            Shared.DevDiag.Count(diagShareCells.Add(player | (uint)((targetY >> 3) * (MapWidth >> 3) + (targetX >> 3))) ? "bfqol.assassin.share.newTargetCell" : "bfqol.assassin.share.repeatTargetCell");
        }
#endif

        private enum RouteExistence { Undecided, Exists, None }

        // Existence-only requests (group/field callers): no exact frame can consume a weighted
        // route, so SearchAndPublishWeightedPath would return Vanilla's result or 0. Diagnostic
        // observers keep the full search (they record its edges). A budget below the tile count
        // could stop A* and a different search order at different points, so it stays exact A*.
        private bool UsesRouteExistenceOnly(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes) =>
            activeObservation == null && maximumNodes >= TileCount && AssassinPathAPI.HasSingleUnitRoutePublication &&
            !AssassinPathAPI.TryGetCurrentWeightedRequest(BugfixesAndQoLPlugin.PluginGuid, context,
                startX, startY, targetX, targetY, out _, out _);

        // Greedy best-first search over exactly the edges of TryBuildWeightedRoute (same ground,
        // climb, reserved-endpoint and gate rules). Without a node budget it is complete: None means
        // the reachable region is exhausted, as A* would. A* additionally rejects optimal routes over
        // MaximumCommittedPathLength nodes; every edge costs at least cardinalTicks, so a found route
        // with cost < cardinalTicks * MaximumCommittedPathLength proves an optimal route of < that
        // many nodes. Otherwise the caller runs the exact A* (Undecided).
        private RouteExistence FindAnyWeightedRoute(int startX, int startY, int targetX, int targetY, int speedDelay,
            bool allowClimbing, bool allowWalkableReservedClimbEndpoints, IEnemyGateRoutePolicySnapshot gatePolicy)
        {
            if (!IsValidCoordinate(startX, startY) || !IsValidCoordinate(targetX, targetY))
                return RouteExistence.None;
            ResetTouchedNodes();
            int cardinalTicks = AssassinClimbCostPolicy.GetCardinalMovementTicks(speedDelay);
            int diagonalTicks = AssassinClimbCostPolicy.GetDiagonalMovementTicks(speedDelay);
            if (!IsNativeTile(GetTileId(startX, startY)) || !IsNativeTile(GetTileId(targetX, targetY)))
                return RouteExistence.None;
            if (ProvesTargetUnreachable(startX, startY, targetX, targetY, allowClimbing, allowWalkableReservedClimbEndpoints, gatePolicy))
                return RouteExistence.None;
            int startNode = GetCoordinateIndex(startX, startY);
            int targetNode = GetCoordinateIndex(targetX, targetY);
            // costs[] holds the cost of the found parent chain; the heap orders by the estimate only.
            Touch(startNode, 0, -1, 0, AssassinAStarPolicy.EstimateOctileTicks(startX, startY, targetX, targetY, cardinalTicks, diagonalTicks));
            Push(startNode);
            while (heapCount > 0)
            {
                int currentNode = Pop();
                if (currentNode == targetNode)
                {
                    Shared.DevDiag.Count("bfqol.assassin.exist.touched.found", touchedCount);
                    long bound = (long)cardinalTicks * MaximumCommittedPathLength;
                    return costs[targetNode] < bound ? RouteExistence.Exists : RouteExistence.Undecided;
                }
                int currentX = currentNode % MapWidth;
                int currentY = currentNode / MapWidth;
                int currentTile = GetTileId(currentX, currentY);
                if (!IsNativeTile(currentTile))
                    continue;
                uint currentFlags = tileFlags[currentTile];
                for (int direction = 0; direction < DirectionX.Length; direction++)
                {
                    int nextX = currentX + DirectionX[direction];
                    int nextY = currentY + DirectionY[direction];
                    if (!IsValidCoordinate(nextX, nextY))
                        continue;
                    int nextNode = GetCoordinateIndex(nextX, nextY);
                    if (costs[nextNode] != int.MaxValue)
                        continue;
                    int nextTile = GetTileId(nextX, nextY);
                    if (!IsNativeTile(nextTile))
                        continue;
                    bool cardinal = (direction & 1) == 0;
                    bool climbEdge = false;
                    if (!HasOrdinaryConnection(currentTile, nextTile, direction))
                    {
                        if (!cardinal || !IsVanillaAssassinFallback(currentTile, nextTile, currentFlags, allowWalkableReservedClimbEndpoints))
                            continue;
                        climbEdge = true;
                        if (!allowClimbing)
                            continue;
                    }
                    if (!AllowsAssassinTransition(gatePolicy, currentTile, nextTile, direction, allowClimbing, allowWalkableReservedClimbEndpoints))
                        continue;
                    int edgeCost = AssassinAStarPolicy.SaturatingAdd(cardinal ? cardinalTicks : diagonalTicks,
                        climbEdge ? GetClimbTicks(currentTile, nextTile) : 0);
                    Touch(nextNode, AssassinAStarPolicy.SaturatingAdd(costs[currentNode], edgeCost), currentNode,
                        climbEdge ? ClimbEdgeKind : GroundEdgeKind,
                        AssassinAStarPolicy.EstimateOctileTicks(nextX, nextY, targetX, targetY, cardinalTicks, diagonalTicks));
                    Push(nextNode);
                }
            }
            Shared.DevDiag.Count("bfqol.assassin.exist.touched.none", touchedCount);
            return RouteExistence.None;
        }

        // Exact unreachability proof before the start-side search. Breadth-first search from the target
        // over reversed edges; every predecessor -> node edge is checked with exactly the predicates of
        // the forward searches (ordinary connection, cardinal climb fallback, climbing switch, gate
        // policy), so the visited set is the set of tiles that can reach the target. Exhausting it
        // without the start proves that no forward search can reach the target: A* would explore the
        // whole start component (often the whole map) and still return no route. Targets enclosed by
        // buildings or walls have tiny backward sets (1-11 tiles on the 8-lord stress save, against
        // ~243,000 forward expansions). The budget bounds the cost of a reachable target, where the
        // backward set is large; then the result is undecided and the normal search runs unchanged.
        private const int BackwardProofNodeBudget = 512;
        private readonly int[] backwardStamps = new int[CoordinateCount];
        private readonly int[] backwardQueue = new int[BackwardProofNodeBudget];
        private int backwardEpoch;
        // Cleared for one request when Vanilla's builder already reached the target: the target is then
        // almost certainly reachable and the proof would only cost its budget. Skipping never changes a
        // result (the normal search runs); every unreachable request on the stress save had native 0.
        private bool backwardProofUseful = true;

        private bool ProvesTargetUnreachable(int startX, int startY, int targetX, int targetY, bool allowClimbing,
            bool allowWalkableReservedClimbEndpoints, IEnemyGateRoutePolicySnapshot gatePolicy)
        {
            // Diagnostic observers record the forward search's edge decisions; keep their full order.
            if (activeObservation != null || !backwardProofUseful)
                return false;
            int startNode = GetCoordinateIndex(startX, startY);
            int targetNode = GetCoordinateIndex(targetX, targetY);
            if (startNode == targetNode)
                return false;
            if (backwardEpoch == int.MaxValue)
            {
                Array.Clear(backwardStamps, 0, backwardStamps.Length);
                backwardEpoch = 0;
            }
            int epoch = ++backwardEpoch;
            int head = 0, tail = 0;
            backwardQueue[tail++] = targetNode;
            backwardStamps[targetNode] = epoch;
            while (head < tail)
            {
                int node = backwardQueue[head++];
                int x = node % MapWidth, y = node / MapWidth;
                int tile = GetTileId(x, y);
                for (int direction = 0; direction < DirectionX.Length; direction++)
                {
                    int fromX = x - DirectionX[direction], fromY = y - DirectionY[direction];
                    if (!IsValidCoordinate(fromX, fromY))
                        continue;
                    int from = GetCoordinateIndex(fromX, fromY);
                    if (backwardStamps[from] == epoch)
                        continue;
                    int fromTile = GetTileId(fromX, fromY);
                    if (!IsNativeTile(fromTile))
                        continue;
                    if (!HasOrdinaryConnection(fromTile, tile, direction) &&
                        ((direction & 1) != 0 || !IsVanillaAssassinFallback(fromTile, tile, tileFlags[fromTile], allowWalkableReservedClimbEndpoints) || !allowClimbing))
                        continue;
                    if (!AllowsAssassinTransition(gatePolicy, fromTile, tile, direction, allowClimbing, allowWalkableReservedClimbEndpoints))
                        continue;
                    if (from == startNode || tail == BackwardProofNodeBudget)
                        return false;
                    backwardStamps[from] = epoch;
                    backwardQueue[tail++] = from;
                }
            }
            Shared.DevDiag.Count("bfqol.assassin.backwardProof.tiles", tail);
            return true;
        }

        private bool IsVanillaAssassinFallback(
            int current,
            int target,
            uint currentFlags,
            bool allowWalkableReservedClimbEndpoints)
        {
            uint targetFlags = tileFlags[target];
            bool targetAccepted = (targetFlags & AssassinFallbackBlockingMask) == 0;
            if (!targetAccepted && (targetFlags & NativeSpecialTileFlag) != 0)
            {
                targetAccepted = specialTilePredicate(
                    IntPtr.Add(libraryHandle, SpecialTilePredicateContextRva),
                    target) != 0;
            }

            bool startAccepted = AssassinClimbTransitionPolicy.CanUseStartTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[current],
                occupancyLayer[current]) || AssassinPathAPI.IsDirectGatehouseClimbEndpoint(current);
            bool targetBuildingAccepted = AssassinClimbTransitionPolicy.CanUseTargetTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[target],
                occupancyLayer[target]) || AssassinPathAPI.IsDirectGatehouseClimbEndpoint(target);
            bool hasWall = ((currentFlags | targetFlags) & IsWallFlag) != 0;
            return targetAccepted && startAccepted && targetBuildingAccepted && hasWall;
        }

        private int EstimateRemainingTicks(
            int x,
            int y,
            int targetX,
            int targetY,
            int cardinalTicks,
            int diagonalTicks,
            Dictionary<int, int> suffixCosts,
            int node)
        {
            int estimate = AssassinAStarPolicy.EstimateOctileTicks(
                x, y, targetX, targetY, cardinalTicks, diagonalTicks);
            if (suffixCosts != null && suffixCosts.TryGetValue(node, out int suffixCost))
                estimate = Math.Max(estimate, suffixCost);
            return estimate;
        }

        private bool TryLoadCachedRoute(
            AssassinCommandScope command,
            RouteCacheKey key,
            out RouteSearchSummary summary,
            out CachedRoute prepared)
        {
            summary = default;
            prepared = null;
            if (!command.RouteCache.TryGetValue(key, out CachedRoute cached) ||
                cached.Nodes == null || cached.Nodes.Length <= 0 ||
                cached.Nodes.Length > route.Length)
            {
                return false;
            }

            Array.Copy(cached.Nodes, route, cached.Nodes.Length);
            if (!ValidateCachedRoute(key, cached))
            {
                command.RouteCache.Remove(key);
                return false;
            }

            summary = cached.Summary.AsCacheHit();
            prepared = cached;
            if (PerformanceDiagnosticsEnabled) command.RecordCacheHit(summary);
            return true;
        }

        private CachedRoute CachePreparedRoute(
            AssassinCommandScope command,
            RouteCacheKey key,
            RouteSearchSummary summary)
        {
            CachedRoute prepared = null;
            if (command.RouteCache.Count < AssassinCommandScope.MaximumCachedRoutes)
            {
                var nodes = new int[summary.RouteLength];
                Array.Copy(route, nodes, nodes.Length);
                prepared = new CachedRoute(nodes, summary, this, key, mapEpoch);
                command.RouteCache[key] = prepared;
            }

            var suffixKey = new SuffixCacheKey(
                key.TargetX, key.TargetY, key.SpeedDelay,
                key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints,
                key.GatePolicy?.PlayerId ?? 0, key.GatePolicy, key.DirectGatehouseClimbing);
            command.CacheSuffixes(suffixKey, route, summary.RouteLength, costs, summary.TotalCost);
            return prepared;
        }

        private bool ValidateCachedRoute(RouteCacheKey key, CachedRoute cached)
        {
#if SERP_DEVDIAG
            UnitCommandPathRuntime.BeginTemporaryFinalValidation();
#endif

            int length = cached.Nodes.Length;
            int expectedStart = GetCoordinateIndex(key.StartX, key.StartY);
            int expectedTarget = GetCoordinateIndex(key.TargetX, key.TargetY);
            if (cached.Nodes[length - 1] != expectedStart || cached.Nodes[0] != expectedTarget)
#if SERP_DEVDIAG
                return TemporaryRouteValidation(false, key, "endpoint-mismatch", cached.Summary);
#else
                return false;
#endif

            int totalCost = 0;
            int groundEdges = 0;
            int climbEdges = 0;
            int cardinalTicks = AssassinClimbCostPolicy.GetCardinalMovementTicks(key.SpeedDelay);
            int diagonalTicks = AssassinClimbCostPolicy.GetDiagonalMovementTicks(key.SpeedDelay);
            for (int reverseIndex = length - 1; reverseIndex > 0; reverseIndex--)
            {
                int currentNode = cached.Nodes[reverseIndex];
                int nextNode = cached.Nodes[reverseIndex - 1];
                int currentX = currentNode % MapWidth;
                int currentY = currentNode / MapWidth;
                int nextX = nextNode % MapWidth;
                int nextY = nextNode / MapWidth;
                if (!IsValidCoordinate(currentX, currentY) ||
                    !IsValidCoordinate(nextX, nextY))
#if SERP_DEVDIAG
                    return TemporaryRouteValidation(false, key, "invalid-coordinate", cached.Summary, length - 1 - reverseIndex, currentNode, nextNode);
#else
                    return false;
#endif

                int dx = nextX - currentX;
                int dy = nextY - currentY;
                int direction = GetDirectionIndex(dx, dy);
                if (direction < 0)
#if SERP_DEVDIAG
                    return TemporaryRouteValidation(false, key, "invalid-direction", cached.Summary, length - 1 - reverseIndex, currentNode, nextNode);
#else
                    return false;
#endif

                int currentTile = GetTileId(currentX, currentY);
                int nextTile = GetTileId(nextX, nextY);
                if (!IsNativeTile(currentTile) || !IsNativeTile(nextTile))
#if SERP_DEVDIAG
                    return TemporaryRouteValidation(false, key, "invalid-native-tile", cached.Summary, length - 1 - reverseIndex, currentNode, nextNode, direction, currentTile, nextTile);
#else
                    return false;
#endif

                bool directClimbingMatches = key.DirectGatehouseClimbing == AssassinPathAPI.DirectGatehouseClimbingEnabled;
                if (!directClimbingMatches ||
                    !AllowsAssassinTransition(key.GatePolicy, currentTile, nextTile, direction, key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints, publicationValidation: true))
#if SERP_DEVDIAG
                    return TemporaryRouteValidation(false, key, directClimbingMatches ? "gate-transition-rejected" : "direct-climb-switch", cached.Summary, length - 1 - reverseIndex, currentNode, nextNode, direction, currentTile, nextTile);
#else
                    return false;
#endif
#if SERP_DEVDIAG
                RememberTemporaryParent(nextTile);
#endif

                bool cardinal = (direction & 1) == 0;
                bool ordinaryEdge = HasOrdinaryConnection(currentTile, nextTile, direction);
                int edgeCost;
                if (ordinaryEdge)
                {
                    groundEdges++;
                    edgeCost = cardinal ? cardinalTicks : diagonalTicks;
                }
                else
                {
                    if (!cardinal || !key.AllowClimbing ||
                        !IsVanillaAssassinFallback(
                            currentTile,
                            nextTile,
                            tileFlags[currentTile],
                            key.AllowWalkableReservedClimbEndpoints))
                    {
#if SERP_DEVDIAG
                        return TemporaryRouteValidation(false, key, (direction & 1) != 0 ? "noncardinal-climb" : !key.AllowClimbing ? "climbing-disabled" : "native-climb-fallback", cached.Summary, length - 1 - reverseIndex, currentNode, nextNode, direction, currentTile, nextTile);
#else
                        return false;
#endif

                    }
                    climbEdges++;
                    edgeCost = AssassinAStarPolicy.SaturatingAdd(
                        cardinalTicks, GetClimbTicks(currentTile, nextTile));
                }
                totalCost = AssassinAStarPolicy.SaturatingAdd(totalCost, edgeCost);
            }
#if SERP_DEVDIAG
            return TemporaryRouteValidation(totalCost == cached.Summary.TotalCost &&
                groundEdges == cached.Summary.GroundEdges &&
                climbEdges == cached.Summary.ClimbEdges, key, "route-summary", cached.Summary,
                totalCost: totalCost, groundEdges: groundEdges, climbEdges: climbEdges);
#else
                return totalCost == cached.Summary.TotalCost &&
                groundEdges == cached.Summary.GroundEdges &&
                climbEdges == cached.Summary.ClimbEdges;
#endif

        }

        // TEMP_GATE_ROUTE_ACCEPTANCE: returns the existing result; never repeats a native predicate.
#if SERP_DEVDIAG
        private bool TemporaryRouteValidation(bool result, RouteCacheKey key, string reason, RouteSearchSummary summary,
            int edgeIndex = -1, int fromNode = -1, int toNode = -1, int direction = -1, int fromTile = -1, int toTile = -1,
            int totalCost = -1, int groundEdges = -1, int climbEdges = -1)
        {
            if (!(TemporaryGateRouteAcceptanceBridge.CurrentRequested is ITemporaryAssassinGateObserver)) return result;
            try
            {
                string category = result ? "passed" : reason == "route-summary"
                    ? totalCost != summary.TotalCost ? "total-cost-mismatch"
                    : groundEdges != summary.GroundEdges ? "ground-count-mismatch" : "climb-count-mismatch"
                    : reason;
                UnitCommandPathRuntime.ReportTemporaryAssassinStage("route-validation", category,
                    UnitCommandPathRuntime.CaptureTemporaryAssassinSource() +
                    ",validationPhase=" + UnitCommandPathRuntime.CaptureTemporaryRouteValidationPhase() +
                    ",valid=" + result + ",criterion=" + reason + ",start=" + key.StartX + "/" + key.StartY +
                    ",target=" + key.TargetX + "/" + key.TargetY + ",policyReference=" + AssassinGateRoutePolicy.IdentityHash(key.GatePolicy) + ",edgeIndex=" + edgeIndex +
                    ",fromNode=" + fromNode + ",toNode=" + toNode + ",direction=" + direction +
                    ",fromTile=" + fromTile + ",toTile=" + toTile +
                    ",costActual=" + totalCost + ",costExpected=" + summary.TotalCost +
                    ",groundActual=" + groundEdges + ",groundExpected=" + summary.GroundEdges +
                    ",climbActual=" + climbEdges + ",climbExpected=" + summary.ClimbEdges +
                    ",playerId=" + key.PlayerId + ",speedDelay=" + key.SpeedDelay,
                    key.PlayerId);
            }
            catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("route-validation-report", error); }
            return result;
        }
#endif

        private static int GetDirectionIndex(int dx, int dy) => AssassinRouteEncoding.GetDirection(dx, dy);

        private int GetClimbTicks(int current, int target)
        {
            int heightDifference = heightLayer[target] - heightLayer[current];
            uint targetFlags = tileFlags[target];
            return AssassinClimbCostPolicy.GetAdditionalTicks(
                isClimbEdge: true,
                heightDifference: heightDifference,
                targetIsLowWall: (targetFlags & IsLowWallFlag) != 0,
                targetIsNormalWall: (targetFlags & IsWallFlag) != 0,
                targetIsStairs: (targetFlags & IsStairsFlag) != 0);
        }

        private bool HasOrdinaryConnection(int from, int to, int direction) =>
            AssassinGateTransitionPolicy.HasOrdinaryConnection(occupancyLayer[from], occupancyLayer[to],
                directionMasks[direction], directionMasks[direction ^ 4]);

        private bool TryStagePreparedRoute(IntPtr context, RouteCacheKey key, RouteSearchSummary summary,
            CachedRoute prepared, AssassinCommandScope command)
        {
            long started = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
            // Reachability/field callers cannot consume an exact packed single-unit route.
            if (summary.RouteLength < 2 || !AssassinPathAPI.TryGetCurrentWeightedRequest(BugfixesAndQoLPlugin.PluginGuid, context,
                key.StartX, key.StartY, key.TargetX, key.TargetY, out _, out _)) return false;
            // Keep captured delegates in a separate method: C# allocates their closure
            // at method entry, even when an earlier guard returns without staging.
            return StagePreparedRouteCore(context, key, summary, prepared, command, started);
        }

        private bool StagePreparedRouteCore(IntPtr context, RouteCacheKey key, RouteSearchSummary summary,
            CachedRoute prepared, AssassinCommandScope command, long started)
        {
            int length = summary.RouteLength;
            if (prepared == null)
            {
                var nodes = new int[length];
                Array.Copy(route, nodes, length);
                prepared = new CachedRoute(nodes, summary, this, key, mapEpoch);
            }
            byte[] bytes = prepared.GetPackedDirections();
            if (bytes == null) return false;
#if SERP_DEVDIAG
            if (!PerformanceDiagnosticsEnabled || command == null)
#endif
                return AssassinPathAPI.TryStageWeightedRoute(BugfixesAndQoLPlugin.PluginGuid, context,
                    key.StartX, key.StartY, key.TargetX, key.TargetY, key.PlayerId, bytes, length - 1,
                    prepared.GetValidator());
#if SERP_DEVDIAG
            return StagePreparedRouteWithDiagnostics(context, key, prepared, command, started, bytes, length);
#endif
        }

        // Isolate the measurement closure so disabled diagnostics allocate no callback.

#if SERP_DEVDIAG
private bool StagePreparedRouteWithDiagnostics(IntPtr context, RouteCacheKey key,
            CachedRoute prepared, AssassinCommandScope command, long started, byte[] bytes, int length)
        {
            long stagingTicks = 0;
            bool staged = AssassinPathAPI.TryStageWeightedRoute(BugfixesAndQoLPlugin.PluginGuid, context,
                key.StartX, key.StartY, key.TargetX, key.TargetY, key.PlayerId, bytes, length - 1,
                prepared.GetValidator(),
                (ticks, success) =>
                {
                    if (PerformanceDiagnosticsEnabled) command.RecordPublication(stagingTicks + ticks, success, prepared.Summary);
                    if (PerformanceDiagnosticsEnabled) command.RecordTotal(ticks);
                });
            stagingTicks = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - started;
            return staged;
        }
#endif


        private bool PrepareRoute(
            int startNode,
            int targetNode,
            int totalCost,
            int expanded,
            int searchHeapOperations,
            long searchStarted,
            long reconstructionStarted,
            out RouteSearchSummary summary)
        {
            summary = default;
            int routeLength = 0;
            int node = targetNode;
            int groundEdges = 0;
            int climbEdges = 0;
            while (node >= 0 && routeLength < route.Length)
            {
                route[routeLength++] = node;
                switch (incomingEdgeKinds[node])
                {
                    case GroundEdgeKind:
                        groundEdges++;
                        break;
                    case ClimbEdgeKind:
                        climbEdges++;
                        break;
                }
                if (node == startNode)
                    break;
                node = parents[node];
            }

            if (routeLength == 0 || routeLength > MaximumCommittedPathLength || route[routeLength - 1] != startNode)
                return false;

            summary = new RouteSearchSummary(
                routeLength,
                groundEdges,
                climbEdges,
                totalCost,
                expanded,
                searchHeapOperations,
                reconstructionStarted - searchStarted,
                (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - reconstructionStarted,
                cacheHit: false);
            return true;
        }

        // TEMP_GATE_ROUTE_ACCEPTANCE: capture the existing decision without repeating native predicates.
#if SERP_DEVDIAG
        private void RememberTemporaryParent(int to)
        {
            AssassinObservation observation = activeObservation;
            if (observation?.ParentDecisions == null) return;
            if (observation.LastDecision == null) observation.ParentDecisions.Remove(to);
            else observation.ParentDecisions[to] = observation.LastDecision;
        }
#endif

        private bool AllowsAssassinTransition(IEnemyGateRoutePolicySnapshot policy, int from, int to,
            int direction, bool allowClimbing, bool allowReserved, bool publicationValidation = false)
        {
            bool result = EvaluateAssassinTransition(policy, from, to, direction, allowClimbing, allowReserved
#if SERP_DEVDIAG
                , out GateTransitionEvidence evidence, publicationValidation && UnitCommandPathRuntime.WantsTemporaryFinalValidation
#endif
                );
#if SERP_DEVDIAG
            if (publicationValidation && evidence != null)
                UnitCommandPathRuntime.RememberTemporaryFinalEdge(policy?.PlayerId ?? 0, from, to, direction,
                    evidence.Movement, result, evidence.Gate, evidence.Global, evidence.Detail);
#endif

            AssassinObservation observation = activeObservation;
#if SERP_DEVDIAG
                if (observation?.DetailedObserver != null)
            {
                observation.LastDecision = evidence;
                if (evidence != null)
                {
                    evidence.Allowed = result;
                    try { observation.DetailedObserver.ObserveAssassinDecision(observation.Token, policy?.PlayerId ?? 0,
                        from, to, direction, false, evidence.Movement, result, evidence.Gate, evidence.Global, evidence.Detail); }
                    catch (Exception ex) { TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-functional-transition", ex); }
                }
            }
#endif

            return result;
        }
        private bool EvaluateAssassinTransition(IEnemyGateRoutePolicySnapshot policy, int from, int to,
            int direction, bool allowClimbing, bool allowReserved
#if SERP_DEVDIAG
            , out GateTransitionEvidence evidence, bool captureFinalEvidence = false
#endif
            )
        {
#if SERP_DEVDIAG
            evidence = null;
#endif
            if (AssassinGateRoutePolicy.Allows(policy, from, direction)) return true;
#if SERP_DEVDIAG
            if (activeObservation?.DetailedObserver != null || captureFinalEvidence)
                evidence = new GateTransitionEvidence { From = from, To = to, Direction = direction,
                    Movement = AssassinTransitionKind.Unknown, Detail = "policy=masked,climbing=" + allowClimbing + ",reservedEndpoints=" + allowReserved };
#endif

            if (policy == null || !policy.IsCurrent || (uint)direction > 7 || !IsNativeTile(from) || !IsNativeTile(to)) return false;
            bool endpointAndSurface = (direction & 1) == 0 && allowClimbing &&
                IsVanillaAssassinFallback(from, to, tileFlags[from], allowReserved);
            AssassinTransitionKind movement = AssassinGateTransitionPolicy.Classify(direction,
                occupancyLayer[from], occupancyLayer[to], directionMasks[direction], directionMasks[direction ^ 4],
                tileFlags[from], tileFlags[to], endpointAndSurface, allowClimbing, endpointAndSurface);
#if SERP_DEVDIAG
            if (evidence != null) { evidence.Movement = movement; evidence.Detail += ",fallbackEndpointAndSurface=" + endpointAndSurface +
                ",sourceConnection=" + occupancyLayer[from] + ",targetConnection=" + occupancyLayer[to] +
                ",forwardMask=" + directionMasks[direction] + ",reverseMask=" + directionMasks[direction ^ 4] +
                ",sourceSurface=" + tileFlags[from] + ",targetSurface=" + tileFlags[to]; }
#endif

            if (movement != AssassinTransitionKind.ClimbUp && movement != AssassinTransitionKind.ClimbDown) return false;
            // A building endpoint exception alone is insufficient. The blocked cut must
            // belong to this exact, still live gate publication, including capture values.
            if (!(policy is IEnemyGateClimbRoutePolicySnapshot identities) ||
                !identities.TryGetBlockedGateIdentity(from, direction, out int gate, out uint global, out int owner, out int capturer) ||
                (buildingLayer[from] != gate && buildingLayer[to] != gate) ||
                !GameBuildingManagerAPI.Instance.IsValidId(gate) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(gate, out GameBuilding* live) || live == null ||
                live->r_GlobalId != global || live->r_PlayerIdOwner != owner || live->r_CapturedByPlayerId != capturer ||
                live->r_AliveState == AliveState.None || live->r_AliveState == AliveState.MarkedForDeletion ||
                (live->r_BuildingType != eStructs.STRUCT_GATE_MAIN && live->r_BuildingType != eStructs.STRUCT_GATE_INNER)) return false;
#if SERP_DEVDIAG
            if (evidence != null) { evidence.Gate = gate; evidence.Global = global;
                evidence.Detail += ",gate=" + gate + "/" + global + ",owner=" + owner + ",capturer=" + capturer + ",identity=verified"; }
#endif

            return AssassinGateTransitionPolicy.Allows(false, movement, policy.IsCurrent);
        }

        private bool ValidatePreparedGateRoute(IEnemyGateRoutePolicySnapshot policy, int routeLength,
            bool allowClimbing, bool allowReserved)
        {
            // Validate every candidate the native distance reconstruction could choose,
            // including shortcuts. Reject the replacement before writing any native data;
            // the previously computed native result/field remains intact on failure.
            return AssassinGateTransitionPolicy.ValidateReconstructionField(route, routeLength, MapWidth, (from, to) =>
            {
                int direction = GetDirectionIndex(to % MapWidth - from % MapWidth, to / MapWidth - from / MapWidth);
                return direction >= 0 && AllowsAssassinTransition(policy,
                    GetTileId(from % MapWidth, from / MapWidth), GetTileId(to % MapWidth, to / MapWidth),
                    direction, allowClimbing, allowReserved) &&
                    CanReconstructTransition(GetTileId(from % MapWidth, from / MapWidth),
                        GetTileId(to % MapWidth, to / MapWidth), direction, allowClimbing, allowReserved);
            });
        }

        private bool CanReconstructTransition(int from, int to, int direction, bool allowClimbing, bool allowReserved)
        {
            // E1640 traverses target -> predecessor, unlike the physical forward step.
            return (occupancyLayer[to] & directionMasks[direction ^ 4]) != 0 ||
                ((direction & 1) == 0 && allowClimbing &&
                    IsVanillaAssassinFallback(to, from, tileFlags[to], allowReserved));
        }

        private bool CommitPreparedRoute(IntPtr context, int routeLength)
        {
            if (routeLength <= 0 || routeLength > MaximumCommittedPathLength)
                return false;

            int generation = *(int*)((byte*)context.ToPointer() + 4) + 1;
            if (generation > 32000)
            {
                new Span<short>(nativeVisitStamps, TileCount).Clear();
                generation = 1;
            }
            *(int*)((byte*)context.ToPointer() + 4) = generation;

            for (int reverseIndex = routeLength - 1, distance = 1;
                 reverseIndex >= 0;
                 reverseIndex--, distance++)
            {
                int routeNode = route[reverseIndex];
                int routeX = routeNode % MapWidth;
                int routeY = routeNode / MapWidth;
                int routeTile = GetTileId(routeX, routeY);
                if (!IsNativeTile(routeTile))
                    return false;
                nativeVisitStamps[routeTile] = (short)generation;
                nativeDistances[routeTile] = (short)distance;
            }

            return true;
        }

        private bool TryResolveAssassinRequest(
            AssassinCommandScope command,
            IntPtr context,
            int startX,
            int startY,
            int targetX,
            int targetY,
            out int playerId,
            out int speedDelay)
        {
            playerId = -1;
            speedDelay = -1;
            if ((uint)startX >= MapWidth || (uint)startY >= MapWidth)
                return false;

            IEnemyGatePathPolicy gateProvider = EnemyGatePathPolicyBridge.Current;
            if (AssassinPathAPI.TryGetCurrentWeightedRequest(BugfixesAndQoLPlugin.PluginGuid, context,
                startX, startY, targetX, targetY, out int controlPlayer, out int currentSpeed))
            {
                playerId = gateProvider != null && gateProvider.HasPublishedMask
                    ? controlPlayer : controlPlayer & 0xff;
                if (playerId < 1 || playerId > 8) return false;
                speedDelay = currentSpeed;
                if (PerformanceDiagnosticsEnabled) command?.RecordDirectResolution();
                return true;
            }

            Dictionary<int, AssassinRequestInfo> index = GetRequestIndex(command);
            if (!index.TryGetValue(GetCoordinateIndex(startX, startY), out AssassinRequestInfo info) ||
                info.Ambiguous || info.PlayerId <= 0)
            {
                return false;
            }

            playerId = info.PlayerId;
            if (gateProvider != null && gateProvider.HasPublishedMask)
            {
                if (info.GateAmbiguous || info.GatePlayerId < 1 || info.GatePlayerId > 8) return false;
                playerId = info.GatePlayerId;
            }
            speedDelay = info.SpeedDelay;
            if (speedDelay < 0)
                speedDelay = GameUnitManagerAPI.Instance.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            return true;
        }

        private Dictionary<int, AssassinRequestInfo> GetRequestIndex(AssassinCommandScope command)
        {
            if (command != null)
            {
                if (command.RequestIndex == null)
                {
                    long started = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
                    command.RequestIndex = BuildRequestIndex();
                    if (PerformanceDiagnosticsEnabled) command.RecordRequestIndex((PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - started);
                }
                return command.RequestIndex;
            }

            // Without a native unit-position revision, tick-wide reuse would be unsafe:
            // units may move sequentially inside the same simulation tick.
            return BuildRequestIndex();
        }

        private static Dictionary<int, AssassinRequestInfo> BuildRequestIndex()
        {
            var index = new Dictionary<int, AssassinRequestInfo>();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit candidate = ref units[spanIndex];
                if (!UnitAccess.IsReallyAlive(in candidate) ||
                    candidate.r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                {
                    continue;
                }

                int x = candidate.r_CurrentTilePositionX;
                int y = candidate.r_CurrentTilePositionY;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth)
                    continue;
                int coordinate = GetCoordinateIndex(x, y);
                int candidatePlayer = candidate.r_ControllableForPlayerId;
                int gatePlayer = AssassinGateRoutePolicy.ReadControlPlayer(
                    candidate.r_ControllableForPlayerId);
                int candidateDelay = candidate.r_CurrentSpeed;
                if (!index.TryGetValue(coordinate, out AssassinRequestInfo existing))
                {
                    index.Add(coordinate, new AssassinRequestInfo(
                        candidatePlayer, candidateDelay, ambiguous: false, gatePlayer, gateAmbiguous: false));
                    continue;
                }

                bool ambiguous = existing.Ambiguous || existing.PlayerId != candidatePlayer;
                int slowestDelay = Math.Max(existing.SpeedDelay, candidateDelay);
                index[coordinate] = new AssassinRequestInfo(
                    existing.PlayerId, slowestDelay, ambiguous, existing.GatePlayerId,
                    existing.GateAmbiguous || existing.GatePlayerId != gatePlayer);
            }
            return index;
        }

        private bool IsValidCoordinate(int x, int y)
        {
            return (uint)x < MapWidth && (uint)y < MapWidth && validCoordinates[y * MapWidth + x] != 0;
        }

        private int GetTileId(int x, int y) => rowLookup[y * 3] + x;

        private static int GetCoordinateIndex(int x, int y) => y * MapWidth + x;

        private static bool IsNativeTile(int tile) => (uint)tile < TileCount;

        private void ValidateCoordinateTileMapping()
        {
            Array.Clear(seenTiles, 0, seenTiles.Length);
            int validCount = 0;
            for (int y = 0; y < MapWidth; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    if (!IsValidCoordinate(x, y))
                        continue;

                    int tile = GetTileId(x, y);
                    if (!IsNativeTile(tile))
                        throw new InvalidOperationException($"valid coordinate {x},{y} maps outside the native tile layers: {tile}");
                    if (seenTiles[tile] != 0)
                        throw new InvalidOperationException($"valid coordinate {x},{y} maps to duplicate native tile {tile}");
                    seenTiles[tile] = 1;
                    validCount++;
                }
            }

            if (validCount <= 0 || validCount > TileCount)
                throw new InvalidOperationException($"native coordinate map exposed an invalid valid-tile count: {validCount}");
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogDebug($"Assassin coordinate map validated for the current map: validCoordinates={validCount}.");
        }

        private bool EnsureCoordinateTileMappingValidated()
        {
            if (coordinateMapValidated)
                return true;

            try
            {
                // These globals are empty while the DLL loads. A real Assassin request is the
                // first lifecycle point that guarantees that Vanilla has prepared the map.
                ValidateCoordinateTileMapping();
                coordinateMapValidated = true;
                coordinateValidationFailureLogged = false;
                return true;
            }
            catch (Exception ex)
            {
                if (!coordinateValidationFailureLogged)
                {
                    coordinateValidationFailureLogged = true;
                    LogWarning($"Assassin coordinate map is not ready or invalid; this map uses Vanilla pathfinding until validation succeeds: {ex.Message}");
                }
                return false;
            }
        }

        private void ObserveMoveCommand(TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                activeCommand = new AssassinCommandScope(
                    activeCommand,
                    ++commandSequence,
                    mapEpoch,
                    GameTimeManagerAPI.Instance.GetElapsedMapTicks(),
                    args.TribeId,
                    MoveCommandKind,
                    args.TileX,
                    args.TileY,
                    $"move/{args.MoveType}/patrol={args.IsPatrolPath}/new={args.IsNewOrder}",
                    IsWeightedEnabledFor(aiPlayer: false),
                    IsWeightedEnabledFor(aiPlayer: true));
                return;
            }

            CloseCommandScope(MoveCommandKind, args.TribeId);
        }

        private void ObserveTargetCommand(TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                activeCommand = new AssassinCommandScope(
                    activeCommand,
                    ++commandSequence,
                    mapEpoch,
                    GameTimeManagerAPI.Instance.GetElapsedMapTicks(),
                    args.TribeId,
                    TargetCommandKind,
                    args.TargetValue1,
                    args.TargetValue2,
                    $"target/{args.AICommand}",
                    IsWeightedEnabledFor(aiPlayer: false),
                    IsWeightedEnabledFor(aiPlayer: true));
                return;
            }

            CloseCommandScope(TargetCommandKind, args.TribeId);
        }

        private void CloseCommandScope(byte eventKind, int tribeId)
        {
            AssassinCommandScope command = activeCommand;
            if (command == null || command.EventKind != eventKind || command.TribeId != tribeId)
            {
                // Event scopes are synchronous and must close in LIFO order. If that contract
                // ever changes, discard every snapshot/cache instead of applying stale data.
                if (!commandScopeMismatchLogged)
                {
                    commandScopeMismatchLogged = true;
                    LogWarning("Assassin command event scopes were not balanced; command-bound caches were discarded.");
                }
                activeCommand = null;
                return;
            }

            CompleteCommand(command);
            activeCommand = command.Previous;
        }

        private void CompleteCommand(AssassinCommandScope command)
        {
            if (!PerformanceDiagnosticsEnabled || command == null)
                return;

            command.ElapsedTicks = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0) - command.StartedTimestamp;
            double elapsedMilliseconds = ToMilliseconds(command.ElapsedTicks);
            if (command.BuilderCalls <= 0)
            {
                return;
            }

            long accountedTicks = command.NativeBuilderTicks + command.ResolutionTicks +
                command.CacheLookupTicks + command.SearchTicks +
                command.ReconstructionTicks + command.PublicationTicks;
            double residualMilliseconds = ToMilliseconds(
                Math.Max(0L, command.ElapsedTicks - accountedTicks));
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo(
                    $"stage=assassin-path-command-summary commandSeq={command.Sequence} " +
                    $"kind={command.Kind} tribe={command.TribeId} commandTarget={command.TargetValue1},{command.TargetValue2} " +
                    $"tick={command.Tick} mapEpoch={command.MapEpoch} " +
                    $"elapsedMs={elapsedMilliseconds:F3} enabled={command.Enabled} " +
                    $"builderCalls={command.BuilderCalls} nativeBuilderMs={ToMilliseconds(command.NativeBuilderTicks):F3} " +
                    $"requestIndexBuilds={command.RequestIndexBuilds} directResolutions={command.DirectResolutions} requestIndexMs={ToMilliseconds(command.RequestIndexTicks):F3} " +
                    $"profileResolveMs={ToMilliseconds(Math.Max(0L, command.ResolutionTicks - command.RequestIndexTicks)):F3} " +
                    $"searches={command.Searches} cacheHits={command.CacheHits} failedSearches={command.FailedSearches} " +
                    $"cacheLookupMs={ToMilliseconds(command.CacheLookupTicks):F3} " +
                    $"expanded={command.ExpandedNodes} heapOps={command.HeapOperations} " +
                    $"searchMs={ToMilliseconds(command.SearchTicks):F3} reconstructionMs={ToMilliseconds(command.ReconstructionTicks):F3} " +
                    $"publicationCalls={command.PublicationCalls} publicationFailures={command.PublicationFailures} " +
                    $"publicationMs={ToMilliseconds(command.PublicationTicks):F3} " +
                    $"groundEdges={command.GroundEdges} climbEdges={command.ClimbEdges} " +
                    $"maxRouteLength={command.MaximumRouteLength} " +
                    $"assassinRequestMs={ToMilliseconds(command.TotalRequestTicks):F3} residualMs={residualMilliseconds:F3}.");

            if (DetailedDiagnosticsEnabled)
            {
                foreach (string detail in command.Details)
                    LogDebug(detail);
                if (command.SuppressedDetails > 0)
                    LogDebug($"stage=assassin-path-details-suppressed commandSeq={command.Sequence} count={command.SuppressedDetails}.");
            }
        }

        private void ClearTransientState()
        {
            activeCommand = null;
        }

        private static double ToMilliseconds(long ticks) =>
            ticks * 1000.0 / Stopwatch.Frequency;

        private void ResetMapValidation()
        {
            coordinateMapValidated = false;
            coordinateValidationFailureLogged = false;
            fallbackLogged = false;
            commandScopeMismatchLogged = false;
        }

        private void Touch(
            int node,
            int cost,
            int parent,
            byte incomingEdgeKind,
            int estimatedTotalCost)
        {
            touched[touchedCount++] = node;
            costs[node] = cost;
            estimatedTotalCosts[node] = estimatedTotalCost;
            parents[node] = parent;
            incomingEdgeKinds[node] = incomingEdgeKind;
            insertionOrder[node] = nextInsertionOrder++;
        }

        private void ResetTouchedNodes()
        {
            for (int index = 0; index < touchedCount; index++)
            {
                int node = touched[index];
                costs[node] = int.MaxValue;
                parents[node] = -1;
                incomingEdgeKinds[node] = 0;
                heapPositions[node] = -1;
            }
            touchedCount = 0;
            heapCount = 0;
            nextInsertionOrder = 0;
        }

        // Each heap slot carries a copy of its node's priority key. Comparisons then read the
        // contiguous slot instead of three node-indexed arrays spread over 640,000 entries.
        // A node's key changes only before Push or PushOrDecrease, which refresh the copy, so
        // every comparison sees the same values as reading the node arrays would.
        private HeapKey KeyOf(int tile) =>
            new HeapKey(estimatedTotalCosts[tile], costs[tile], insertionOrder[tile]);

        // Dial bucket queue of the exact A* (TryBuildWeightedRoute). With the consistent octile estimate
        // and integer ticks, a pushed key lies in [current, current + 2 x largest edge]: the largest edge is
        // a diagonal step plus a climb over the full byte height range ((255 * 400 + 45) / 90 = 1,134 ticks),
        // so 4,096 buckets cover diagonal steps up to MaximumBucketStepTicks. Keys pop in nondecreasing
        // order like the heap's, so costs stay optimal; ties pop last-in first (the found route may be a
        // different one of equal cost). The heap's log factor was most of the search time (pf-iter-32).
        private const int BucketRing = 4096, MaximumClimbEdgeTicks = 1134;
        private const int MaximumBucketStepTicks = BucketRing / 2 - 1 - MaximumClimbEdgeTicks;
        private readonly List<int>[] buckets = new List<int>[BucketRing];
        private int bucketCount, bucketCurrent;
        private bool bucketOverflow;

        private void BucketStart(int start)
        {
            bucketCount = 0; bucketOverflow = false; bucketCurrent = estimatedTotalCosts[start];
            BucketPush(start);
        }

        private void BucketPush(int tile)
        {
            if (PerformanceDiagnosticsEnabled) heapOperations++;
            int key = estimatedTotalCosts[tile];
            if (key < bucketCurrent || key - bucketCurrent >= BucketRing) { bucketOverflow = true; return; }
            List<int> list = buckets[key & (BucketRing - 1)] ?? (buckets[key & (BucketRing - 1)] = new List<int>());
            list.Add(tile); bucketCount++;
        }

        // Next node at the smallest key; stale entries (a later decrease) and expanded nodes are skipped.
        private int BucketPop()
        {
            while (bucketCount > 0)
            {
                List<int> list = buckets[bucketCurrent & (BucketRing - 1)];
                while (list != null && list.Count > 0)
                {
                    int tile = list[list.Count - 1]; list.RemoveAt(list.Count - 1); bucketCount--;
                    if (heapPositions[tile] == -2 || estimatedTotalCosts[tile] != bucketCurrent) continue;
                    if (PerformanceDiagnosticsEnabled) heapOperations++;
                    heapPositions[tile] = -2;
                    return tile;
                }
                bucketCurrent++;
            }
            return -1;
        }

        private void BucketClear()
        {
            if (bucketCount != 0)
                foreach (List<int> list in buckets) list?.Clear();
            bucketCount = 0;
        }

        private void Push(int tile)
        {
            if (PerformanceDiagnosticsEnabled) heapOperations++;
            int position = heapCount++;
            heap[position] = tile;
            heapKeys[position] = KeyOf(tile);
            heapPositions[tile] = position;
            SiftUp(position);
        }

        private void PushOrDecrease(int tile)
        {
            int position = heapPositions[tile];
            if (position < 0)
                Push(tile);
            else
            {
                heapKeys[position] = KeyOf(tile);
                SiftUp(position);
            }
        }

        private int Pop()
        {
            if (PerformanceDiagnosticsEnabled) heapOperations++;
            int result = heap[0];
            int tail = heap[--heapCount];
            heapPositions[result] = -1;
            if (heapCount > 0)
            {
                heap[0] = tail;
                heapKeys[0] = heapKeys[heapCount];
                heapPositions[tail] = 0;
                SiftDown(0);
            }
            return result;
        }

        // Four-ary heap: half the depth of a binary heap, so fewer key moves per Push/Pop. Every key is
        // distinct (insertion orders are unique within a search), so the priority order is total and
        // the pop sequence is the same for any correct heap shape: routes, expansions and tie-breaking
        // stay identical to the former binary heap.
        private void SiftUp(int position)
        {
            int tile = heap[position];
            HeapKey key = heapKeys[position];
            while (position > 0)
            {
                int parent = (position - 1) >> 2;
                if (!ComesBefore(in key, in heapKeys[parent]))
                    break;
                heap[position] = heap[parent];
                heapKeys[position] = heapKeys[parent];
                heapPositions[heap[position]] = position;
                position = parent;
            }
            heap[position] = tile;
            heapKeys[position] = key;
            heapPositions[tile] = position;
        }

        private void SiftDown(int position)
        {
            int tile = heap[position];
            HeapKey key = heapKeys[position];
            while (true)
            {
                int first = (position << 2) + 1;
                if (first >= heapCount)
                    break;
                int best = first;
                int last = Math.Min(first + 4, heapCount);
                for (int child = first + 1; child < last; child++)
                    if (ComesBefore(in heapKeys[child], in heapKeys[best]))
                        best = child;
                if (!ComesBefore(in heapKeys[best], in key))
                    break;
                heap[position] = heap[best];
                heapKeys[position] = heapKeys[best];
                heapPositions[heap[position]] = position;
                position = best;
            }
            heap[position] = tile;
            heapKeys[position] = key;
            heapPositions[tile] = position;
        }

        // Same order as AssassinAStarPolicy.ComesBefore (estimated total, then cost, then insertion
        // order) with the first two levels in one unsigned comparison; see HeapKey.
        private static bool ComesBefore(in HeapKey left, in HeapKey right) =>
            left.Primary < right.Primary ||
            (left.Primary == right.Primary && left.InsertionOrder < right.InsertionOrder);

        private readonly struct HeapKey
        {
            // Estimated total in the high and cost in the low 32 bits. Both are non-negative ints
            // (costs start at 0 and only grow by saturating additions of non-negative edge and
            // heuristic values), so the unsigned 64-bit order equals the lexicographic int order.
            internal readonly ulong Primary;
            internal readonly int InsertionOrder;

            internal HeapKey(int estimatedTotal, int cost, int insertionOrder)
            {
                Primary = ((ulong)(uint)estimatedTotal << 32) | (uint)cost;
                InsertionOrder = insertionOrder;
            }
        }

        private readonly struct RouteSearchSummary
        {
            public RouteSearchSummary(
                int routeLength,
                int groundEdges,
                int climbEdges,
                int totalCost,
                int expandedNodes,
                int searchHeapOperations,
                long searchTicks,
                long reconstructionTicks,
                bool cacheHit)
            {
                RouteLength = routeLength;
                GroundEdges = groundEdges;
                ClimbEdges = climbEdges;
                TotalCost = totalCost;
                ExpandedNodes = expandedNodes;
                HeapOperations = searchHeapOperations;
                SearchTicks = searchTicks;
                ReconstructionTicks = reconstructionTicks;
                CacheHit = cacheHit;
            }

            public int RouteLength { get; }
            public int GroundEdges { get; }
            public int ClimbEdges { get; }
            public int TotalCost { get; }
            public int ExpandedNodes { get; }
            public int HeapOperations { get; }
            public long SearchTicks { get; }
            public long ReconstructionTicks { get; }
            public bool CacheHit { get; }

            public RouteSearchSummary AsCacheHit() => new RouteSearchSummary(
                RouteLength,
                GroundEdges,
                ClimbEdges,
                TotalCost,
                0,
                0,
                0,
                0,
                cacheHit: true);
        }

        private readonly struct AssassinRequestInfo
        {
            public AssassinRequestInfo(int playerId, int speedDelay, bool ambiguous, int gatePlayerId, bool gateAmbiguous)
            {
                PlayerId = playerId;
                SpeedDelay = speedDelay;
                Ambiguous = ambiguous;
                GatePlayerId = gatePlayerId;
                GateAmbiguous = gateAmbiguous;
            }

            public int PlayerId { get; }
            public int SpeedDelay { get; }
            public bool Ambiguous { get; }
            public int GatePlayerId { get; }
            public bool GateAmbiguous { get; }
        }

        private sealed class CachedRoute
        {
            private byte[] packedDirections;
            private Func<bool> validator;
            private readonly AssassinPathfindingRuntime runtime;
            private readonly RouteCacheKey key;
            private readonly int epoch;
            public CachedRoute(int[] nodes, RouteSearchSummary summary,
                AssassinPathfindingRuntime runtime, RouteCacheKey key, int epoch)
            {
                Nodes = nodes;
                Summary = summary;
                this.runtime = runtime;
                this.key = key;
                this.epoch = epoch;
            }

            public int[] Nodes { get; }
            public RouteSearchSummary Summary { get; }
            public byte[] GetPackedDirections() => packedDirections ??
                (packedDirections = AssassinRouteEncoding.EncodeTargetFirst(Nodes, MapWidth));
            // These are immutable route bindings, not a cached validation result.
            // Every Stage/Complete invocation still checks current map, settings,
            // climbing, gate policy, physical edges and costs in full.
            public Func<bool> GetValidator() => validator ?? (validator = Validate);
#if SERP_DEVDIAG
            private bool Validate() =>
                (epoch == runtime.mapEpoch || runtime.TemporaryRouteValidation(false, key, "map-epoch", Summary)) &&
                (runtime.settings.EnableMod || runtime.TemporaryRouteValidation(false, key, "mod-disabled", Summary)) &&
                (runtime.IsWeightedEnabledFor(IsAIPlayer(key.PlayerId)) || runtime.TemporaryRouteValidation(false, key, "improvement-disabled", Summary)) &&
                (key.AllowClimbing == runtime.climbRuntime.IsClimbingAllowed(key.PlayerId) ||
                    runtime.TemporaryRouteValidation(false, key, "player-climb-switch", Summary)) &&
                (AssassinGateRoutePolicy.IsCurrent(key.GatePolicy) ||
                    runtime.TemporaryRouteValidation(false, key, "gate-policy-stale", Summary)) && runtime.ValidateCachedRoute(key, this);
#else
            private bool Validate() =>
                (epoch == runtime.mapEpoch) &&
                (runtime.settings.EnableMod) &&
                (runtime.IsWeightedEnabledFor(IsAIPlayer(key.PlayerId))) &&
                (key.AllowClimbing == runtime.climbRuntime.IsClimbingAllowed(key.PlayerId)) &&
                (AssassinGateRoutePolicy.IsCurrent(key.GatePolicy)) && runtime.ValidateCachedRoute(key, this);
#endif

        }

        private sealed class AssassinCommandScope
        {
            internal const int MaximumCachedRoutes = 64;
            private const int MaximumSuffixTargets = 32;
            private const int MaximumSuffixNodes = 20000;
            private readonly Dictionary<int, bool> climbingByPlayer =
                new Dictionary<int, bool>();
            private readonly Dictionary<SuffixCacheKey, Dictionary<int, int>> suffixCosts =
                new Dictionary<SuffixCacheKey, Dictionary<int, int>>();
            private int suffixNodeCount;

            public AssassinCommandScope(
                AssassinCommandScope previous,
                int sequence,
                int mapEpoch,
                int tick,
                int tribeId,
                byte eventKind,
                int targetValue1,
                int targetValue2,
                string kind,
                bool enabledForHumans,
                bool? enabledForAI = null)
            {
                Previous = previous;
                Sequence = sequence;
                MapEpoch = mapEpoch;
                Tick = tick;
                TribeId = tribeId;
                EventKind = eventKind;
                TargetValue1 = targetValue1;
                TargetValue2 = targetValue2;
                Kind = kind;
                EnabledForHumans = enabledForHumans;
                EnabledForAI = enabledForAI ?? enabledForHumans;
                StartedTimestamp = (PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0);
            }

            public AssassinCommandScope Previous { get; }
            public int Sequence { get; }
            public int MapEpoch { get; }
            public int Tick { get; }
            public int TribeId { get; }
            public byte EventKind { get; }
            public int TargetValue1 { get; }
            public int TargetValue2 { get; }
            public string Kind { get; }
            // Settings snapshot taken when the command starts; requests inside it use the same values.
            public bool EnabledForHumans { get; }
            public bool EnabledForAI { get; }
            public bool Enabled => EnabledForHumans || EnabledForAI;
            public bool IsEnabledFor(bool aiPlayer) => aiPlayer ? EnabledForAI : EnabledForHumans;
            public long StartedTimestamp { get; }
            public long ElapsedTicks { get; set; }
            public Dictionary<int, AssassinRequestInfo> RequestIndex { get; set; }
            public Dictionary<RouteCacheKey, CachedRoute> RouteCache { get; } =
                new Dictionary<RouteCacheKey, CachedRoute>();
            public List<string> Details { get; } = PerformanceDiagnosticsEnabled ? new List<string>() : null;
            public int SuppressedDetails { get; private set; }
            public int BuilderCalls { get; private set; }
            public int RequestIndexBuilds { get; private set; }
            public int DirectResolutions { get; private set; }
            public int Searches { get; private set; }
            public int CacheHits { get; private set; }
            public int FailedSearches { get; private set; }
            public long ExpandedNodes { get; private set; }
            public long HeapOperations { get; private set; }
            public int PublicationCalls { get; private set; }
            public int PublicationFailures { get; private set; }
            public int GroundEdges { get; private set; }
            public int ClimbEdges { get; private set; }
            public int MaximumRouteLength { get; private set; }
            public long NativeBuilderTicks { get; private set; }
            public long RequestIndexTicks { get; private set; }
            public long ResolutionTicks { get; private set; }
            public long CacheLookupTicks { get; private set; }
            public long SearchTicks { get; private set; }
            public long ReconstructionTicks { get; private set; }
            public long PublicationTicks { get; private set; }
            public long TotalRequestTicks { get; private set; }

            public bool GetClimbingAllowed(int playerId, AssassinClimbRuntime runtime)
            {
                if (!climbingByPlayer.TryGetValue(playerId, out bool allowed))
                {
                    allowed = runtime.IsClimbingAllowed(playerId);
                    climbingByPlayer.Add(playerId, allowed);
                }
                return allowed;
            }

            public Dictionary<int, int> GetSuffixCosts(SuffixCacheKey key) =>
                suffixCosts.TryGetValue(key, out Dictionary<int, int> field) ? field : null;

            public void CacheSuffixes(
                SuffixCacheKey key,
                int[] nodes,
                int length,
                int[] sourceCosts,
                int totalCost)
            {
                if (totalCost == int.MaxValue)
                    return;

                if (!suffixCosts.TryGetValue(key, out Dictionary<int, int> field))
                {
                    if (suffixCosts.Count >= MaximumSuffixTargets)
                        return;
                    field = new Dictionary<int, int>();
                    suffixCosts.Add(key, field);
                }

                for (int index = 0; index < length && suffixNodeCount < MaximumSuffixNodes; index++)
                {
                    int node = nodes[index];
                    int sourceCost = sourceCosts[node];
                    if (sourceCost == int.MaxValue || sourceCost > totalCost)
                        continue;
                    int suffixCost = Math.Max(0, totalCost - sourceCost);
                    if (!field.ContainsKey(node))
                    {
                        field.Add(node, suffixCost);
                        suffixNodeCount++;
                    }
                }
            }

            public void RecordNativeBuilder(long ticks)
            {
                if (!PerformanceDiagnosticsEnabled) return;
                BuilderCalls++;
                NativeBuilderTicks += ticks;
            }

            public void RecordRequestIndex(long ticks)
            {
                if (!PerformanceDiagnosticsEnabled) return;
                RequestIndexBuilds++;
                RequestIndexTicks += ticks;
            }

            public void RecordResolution(long ticks) { if (PerformanceDiagnosticsEnabled) ResolutionTicks += ticks; }
            public void RecordDirectResolution() { if (PerformanceDiagnosticsEnabled) DirectResolutions++; }
            public void RecordCacheLookup(long ticks) { if (PerformanceDiagnosticsEnabled) CacheLookupTicks += ticks; }

            public void RecordSearch(RouteSearchSummary summary)
            {
                if (!PerformanceDiagnosticsEnabled) return;
                Searches++;
                ExpandedNodes += summary.ExpandedNodes;
                HeapOperations += summary.HeapOperations;
                SearchTicks += summary.SearchTicks;
                ReconstructionTicks += summary.ReconstructionTicks;
                AddDetail(summary, "search");
            }

            public void RecordFailedSearch(long ticks, int expanded, int heapOperations)
            {
                if (!PerformanceDiagnosticsEnabled) return;
                Searches++;
                FailedSearches++;
                SearchTicks += ticks;
                ExpandedNodes += expanded;
                HeapOperations += heapOperations;
            }

            public void RecordCacheHit(RouteSearchSummary summary)
            {
                if (!PerformanceDiagnosticsEnabled) return;
                CacheHits++;
                AddDetail(summary, "cache");
            }

            public void RecordPublication(long ticks, bool success, RouteSearchSummary summary)
            {
                if (!PerformanceDiagnosticsEnabled) return;
                PublicationCalls++;
                PublicationTicks += ticks;
                if (!success)
                    PublicationFailures++;
                if (success)
                {
                    GroundEdges += summary.GroundEdges;
                    ClimbEdges += summary.ClimbEdges;
                    MaximumRouteLength = Math.Max(MaximumRouteLength, summary.RouteLength);
                }
            }

            public void RecordTotal(long ticks) { if (PerformanceDiagnosticsEnabled) TotalRequestTicks += ticks; }

            private void AddDetail(RouteSearchSummary summary, string source)
            {
                if (!DetailedDiagnosticsEnabled)
                    return;
                if (Details.Count >= MaximumDetailedRequestsPerCommand)
                {
                    SuppressedDetails++;
                    return;
                }
                Details.Add(
                    $"stage=assassin-path-detail commandSeq={Sequence} source={source} " +
                    $"routeLength={summary.RouteLength} cost={summary.TotalCost} " +
                    $"ground={summary.GroundEdges} climb={summary.ClimbEdges} " +
                    $"expanded={summary.ExpandedNodes} heapOps={summary.HeapOperations}.");
            }
        }

        private void LogDebug(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) => Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message);
        private void LogInfo(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) => Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message);
        private void LogWarning(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) => Shared.DebugLogHelper.LogWarningLimited(log, callerFile + ":" + callerLine, message);
        private void LogError(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) => Shared.DebugLogHelper.LogErrorLimited(log, callerFile + ":" + callerLine, message);
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
