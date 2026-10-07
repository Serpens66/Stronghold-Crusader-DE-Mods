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
        private const double SlowCommandThresholdMilliseconds = 100.0;
        private const int MaximumDetailedRequestsPerCommand = 8;
        private static readonly bool DetailedDiagnosticsEnabled = false;
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
        private static AssassinPathfindingRuntime temporaryDiagnosticRuntime;
        internal static string TemporaryReconstructionRelaxation => temporaryDiagnosticRuntime?.reconstructionPatch == null
            ? "unavailable" : temporaryDiagnosticRuntime.reconstructionPatch.IsApplied ? "active" : "inactive";

        public AssassinPathfindingRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings, AssassinClimbRuntime climbRuntime)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.climbRuntime = climbRuntime ?? throw new ArgumentNullException(nameof(climbRuntime));
            temporaryDiagnosticRuntime = this; // TEMP_GATE_ROUTE_ACCEPTANCE
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

            patch.SetEnabled(AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(
                settings.EnableMod,
                settings.EnableImprovedAssassinPathfinding,
                IsInstalled));
        }

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
            internal string Outcome = "native-exception", Error;
        }

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
            object temporaryNativeSearch = UnitCommandPathRuntime.BeginTemporaryAssassinSearch(context,
                startX, startY, targetX, targetY, continuation);
            bool temporaryCompleted = false;
            int temporaryResult = 0;
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
                activeObservation = observation;
                int result = BuildWeightedPathCore(context, startX, startY, targetX, targetY, maximumNodes, continuation);
                temporaryCompleted = true; temporaryResult = result;
                if (observation != null) observation.EffectiveResult = result;
                return result;
            }
            finally
            {
                UnitCommandPathRuntime.EndTemporaryAssassinSearch(temporaryNativeSearch, temporaryCompleted,
                    observation?.NativeResult ?? 0, temporaryResult, observation?.Player ?? -1,
                    observation?.Outcome ?? "unobserved", observation?.CacheHit ?? false, observation?.RouteLength ?? 0);
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
                    bool climb = (directionMasks[direction] & occupancyLayer[fromTile]) == 0;
                    try { observation.Observer?.ObserveAssassinEdge(observation.Token, player,
                        fromTile, toTile, direction, climb); }
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
            long requestStarted = Stopwatch.GetTimestamp();
            long nativeStarted = requestStarted;
            // Vanilla initializes internal queue state even when our compact route field replaces it.
            int vanillaResult = AssassinPathAPI.RunVanillaBuilder(context, startX, startY, targetX, targetY, maximumNodes, continuation);
            if (activeObservation != null)
            { activeObservation.NativeResult = vanillaResult; activeObservation.Outcome = "native-only"; }
            long nativeTicks = Stopwatch.GetTimestamp() - nativeStarted;
            command?.RecordNativeBuilder(nativeTicks);

            bool enabled = command?.Enabled ??
                (settings.EnableMod && settings.EnableImprovedAssassinPathfinding);
            if (!enabled || continuation != 0)
            {
                if (activeObservation != null) activeObservation.Outcome = !enabled ? "weighted-disabled" : "continuation";
                return vanillaResult;
            }
            if (targetX < 0 || targetY < 0)
            {
                if (activeObservation != null) activeObservation.Outcome = "native-flood-field";
                return vanillaResult;
            }

            try
            {
                long resolutionStarted = Stopwatch.GetTimestamp();
                if (!TryResolveAssassinRequest(command, startX, startY, out int playerId, out int speedDelay))
                {
                    command?.RecordResolution(Stopwatch.GetTimestamp() - resolutionStarted);
                    if (activeObservation != null) activeObservation.Outcome = "unresolved-player";
                    return vanillaResult;
                }
                command?.RecordResolution(Stopwatch.GetTimestamp() - resolutionStarted);
                if (activeObservation != null) activeObservation.Player = playerId;
                if (!EnsureCoordinateTileMappingValidated())
                {
                    if (activeObservation != null) activeObservation.Outcome = "coordinate-map-unready";
                    return vanillaResult;
                }

                if (!AssassinGateRoutePolicy.TryCapture(EnemyGatePathPolicyBridge.Current, playerId,
                    out IEnemyGateRoutePolicySnapshot gatePolicy))
                {
                    if (activeObservation != null) activeObservation.Outcome = "gate-context-fallback";
                    return vanillaResult;
                }
                bool allowClimbing = command?.GetClimbingAllowed(playerId, climbRuntime) ??
                    climbRuntime.IsClimbingAllowed(playerId);
                // Never publish a relaxed route unless Vanilla can reconstruct the same
                // validated reserved climb endpoints. This keeps patch failures fail-closed.
                bool allowWalkableReservedClimbEndpoints = reconstructionPatch?.IsApplied == true;
                var cacheKey = new RouteCacheKey(
                    startX, startY, targetX, targetY, maximumNodes, speedDelay,
                    playerId, allowClimbing, allowWalkableReservedClimbEndpoints, gatePolicy, AssassinPathAPI.DirectGatehouseClimbingEnabled);
                RouteSearchSummary routeSummary = default;
                long cacheStarted = Stopwatch.GetTimestamp();
                bool routeReady = command != null &&
                    TryLoadCachedRoute(command, cacheKey, out routeSummary);
                command?.RecordCacheLookup(Stopwatch.GetTimestamp() - cacheStarted);
                if (!routeReady)
                {
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
                    if (routeReady && command != null)
                        CachePreparedRoute(command, cacheKey, routeSummary);
                }

                if (activeObservation != null)
                { activeObservation.CacheHit = routeSummary.CacheHit;
                  activeObservation.Outcome = routeReady ? "weighted-prepared" : "weighted-no-route"; }
                if (!AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                {
                    if (activeObservation != null) activeObservation.Outcome = "gate-snapshot-fallback";
                    return vanillaResult;
                }
                if (!routeReady)
                    return 0;
                ObservePreparedAssassinRoute(playerId, routeSummary.RouteLength);

                long publicationStarted = Stopwatch.GetTimestamp();
                // Validate before the first native stamp/distance write. A stale policy
                // must preserve the original native field, not publish a partial replacement.
                if (cacheKey.DirectGatehouseClimbing != AssassinPathAPI.DirectGatehouseClimbingEnabled ||
                    !ValidatePreparedGateRoute(gatePolicy, routeSummary.RouteLength, allowClimbing, allowWalkableReservedClimbEndpoints) ||
                    !AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                {
                    if (activeObservation != null) activeObservation.Outcome = "gate-publication-fallback";
                    return vanillaResult;
                }
                bool published = CommitPreparedRoute(context, routeSummary.RouteLength);
                command?.RecordPublication(
                    Stopwatch.GetTimestamp() - publicationStarted,
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
                if (activeObservation != null) activeObservation.Outcome = "exception-fallback:" + ex.GetType().Name;
                if (!fallbackLogged)
                {
                    fallbackLogged = true;
                    LogError($"weighted Assassin pathfinding failed and this request fell back to Vanilla: {ex}");
                }
                return vanillaResult;
            }
            finally
            {
                command?.RecordTotal(Stopwatch.GetTimestamp() - requestStarted);
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
            out RouteSearchSummary routeSummary)
        {
            long searchStarted = Stopwatch.GetTimestamp();
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

            int startNode = GetCoordinateIndex(startX, startY);
            int targetNode = GetCoordinateIndex(targetX, targetY);
            heapOperations = 0;
            SuffixCacheKey suffixKey = new SuffixCacheKey(
                targetX, targetY, speedDelay, allowClimbing,
                allowWalkableReservedClimbEndpoints, gatePolicy?.PlayerId ?? 0, gatePolicy, AssassinPathAPI.DirectGatehouseClimbingEnabled);
            Touch(startNode, 0, -1, 0,
                EstimateRemainingTicks(
                    startX, startY, targetX, targetY,
                    cardinalTicks, diagonalTicks, command, suffixKey, startNode));
            Push(startNode);
            int expanded = 0;
            int nodeLimit = Math.Max(1, Math.Min(maximumNodes, TileCount));

            while (heapCount > 0 && expanded < nodeLimit)
            {
                int currentNode = Pop();
                expanded++;
                if (currentNode == targetNode)
                {
                    long reconstructionStarted = Stopwatch.GetTimestamp();
                    if (!PrepareRoute(startNode, targetNode, costs[targetNode], expanded,
                        heapOperations, searchStarted, reconstructionStarted, out routeSummary))
                    {
                        command?.RecordFailedSearch(
                            Stopwatch.GetTimestamp() - searchStarted, expanded, heapOperations);
                        return false;
                    }
                    command?.RecordSearch(routeSummary);
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
                    uint nextFlags = tileFlags[nextTile];
                    bool cardinal = (direction & 1) == 0;
                    bool ordinaryEdge = (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
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
                    int movementTicks = (direction & 1) == 0
                        ? cardinalTicks
                        : diagonalTicks;
                    int climbTicks = climbEdge ? GetClimbTicks(currentTile, nextTile) : 0;
                    int edgeCost = movementTicks > int.MaxValue - climbTicks ? int.MaxValue : movementTicks + climbTicks;
                    int newCost = costs[currentNode] > int.MaxValue - edgeCost ? int.MaxValue : costs[currentNode] + edgeCost;
                    if (newCost >= costs[nextNode])
                        continue;

                    if (costs[nextNode] == int.MaxValue)
                    {
                        int heuristic = EstimateRemainingTicks(
                            nextX, nextY, targetX, targetY,
                            cardinalTicks, diagonalTicks, command, suffixKey, nextNode);
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
                            cardinalTicks, diagonalTicks, command, suffixKey, nextNode);
                        estimatedTotalCosts[nextNode] =
                            AssassinAStarPolicy.SaturatingAdd(newCost, heuristic);
                    }
                    PushOrDecrease(nextNode);
                }
            }

            command?.RecordFailedSearch(
                Stopwatch.GetTimestamp() - searchStarted, expanded, heapOperations);
            return false;
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
            AssassinCommandScope command,
            SuffixCacheKey suffixKey,
            int node)
        {
            int estimate = AssassinAStarPolicy.EstimateOctileTicks(
                x, y, targetX, targetY, cardinalTicks, diagonalTicks);
            if (command != null && command.TryGetSuffixCost(suffixKey, node, out int suffixCost))
                estimate = Math.Max(estimate, suffixCost);
            return estimate;
        }

        private bool TryLoadCachedRoute(
            AssassinCommandScope command,
            RouteCacheKey key,
            out RouteSearchSummary summary)
        {
            summary = default;
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
            command.RecordCacheHit(summary);
            return true;
        }

        private void CachePreparedRoute(
            AssassinCommandScope command,
            RouteCacheKey key,
            RouteSearchSummary summary)
        {
            if (command.RouteCache.Count < AssassinCommandScope.MaximumCachedRoutes)
            {
                var nodes = new int[summary.RouteLength];
                Array.Copy(route, nodes, nodes.Length);
                command.RouteCache[key] = new CachedRoute(nodes, summary);
            }

            var suffixKey = new SuffixCacheKey(
                key.TargetX, key.TargetY, key.SpeedDelay,
                key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints,
                key.GatePolicy?.PlayerId ?? 0, key.GatePolicy);
            command.CacheSuffixes(suffixKey, route, summary.RouteLength, costs, summary.TotalCost);
        }

        private bool ValidateCachedRoute(RouteCacheKey key, CachedRoute cached)
        {
            int length = cached.Nodes.Length;
            int expectedStart = GetCoordinateIndex(key.StartX, key.StartY);
            int expectedTarget = GetCoordinateIndex(key.TargetX, key.TargetY);
            if (cached.Nodes[length - 1] != expectedStart || cached.Nodes[0] != expectedTarget)
                return false;

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
                    return false;
                int dx = nextX - currentX;
                int dy = nextY - currentY;
                int direction = GetDirectionIndex(dx, dy);
                if (direction < 0)
                    return false;

                int currentTile = GetTileId(currentX, currentY);
                int nextTile = GetTileId(nextX, nextY);
                if (!IsNativeTile(currentTile) || !IsNativeTile(nextTile))
                    return false;

                if (key.DirectGatehouseClimbing != AssassinPathAPI.DirectGatehouseClimbingEnabled ||
                    !AllowsAssassinTransition(key.GatePolicy, currentTile, nextTile, direction, key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints))
                    return false;
                bool cardinal = (direction & 1) == 0;
                bool ordinaryEdge = (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
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
                        return false;
                    }
                    climbEdges++;
                    edgeCost = AssassinAStarPolicy.SaturatingAdd(
                        cardinalTicks, GetClimbTicks(currentTile, nextTile));
                }
                totalCost = AssassinAStarPolicy.SaturatingAdd(totalCost, edgeCost);
            }

            return totalCost == cached.Summary.TotalCost &&
                groundEdges == cached.Summary.GroundEdges &&
                climbEdges == cached.Summary.ClimbEdges;
        }

        private static int GetDirectionIndex(int dx, int dy)
        {
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                if (DirectionX[direction] == dx && DirectionY[direction] == dy)
                    return direction;
            }
            return -1;
        }

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
                Stopwatch.GetTimestamp() - reconstructionStarted,
                cacheHit: false);
            return true;
        }

        private bool AllowsAssassinTransition(IEnemyGateRoutePolicySnapshot policy, int from, int to,
            int direction, bool allowClimbing, bool allowReserved)
        {
            if (AssassinGateRoutePolicy.Allows(policy, from, direction)) return true;
            if (policy == null || !policy.IsCurrent || (uint)direction > 7 || !IsNativeTile(from) || !IsNativeTile(to)) return false;
            bool endpointAndSurface = (direction & 1) == 0 && allowClimbing &&
                IsVanillaAssassinFallback(from, to, tileFlags[from], allowReserved);
            AssassinTransitionKind movement = AssassinGateTransitionPolicy.Classify(direction,
                occupancyLayer[from], occupancyLayer[to], directionMasks[direction], directionMasks[direction ^ 4],
                tileFlags[from], tileFlags[to], endpointAndSurface, allowClimbing, endpointAndSurface);
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
            return AssassinGateTransitionPolicy.Allows(false, movement, policy.IsCurrent);
        }

        private bool ValidatePreparedGateRoute(IEnemyGateRoutePolicySnapshot policy, int routeLength,
            bool allowClimbing, bool allowReserved)
        {
            if (policy == null) return true;
            // Validate every candidate the native distance reconstruction could choose,
            // including shortcuts. Reject the replacement before writing any native data;
            // the previously computed native result/field remains intact on failure.
            return AssassinGateTransitionPolicy.ValidateReconstructionField(route, routeLength, MapWidth, (from, to) =>
            {
                int direction = GetDirectionIndex(to % MapWidth - from % MapWidth, to / MapWidth - from / MapWidth);
                return direction >= 0 && AllowsAssassinTransition(policy,
                    GetTileId(from % MapWidth, from / MapWidth), GetTileId(to % MapWidth, to / MapWidth),
                    direction, allowClimbing, allowReserved);
            });
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
            int startX,
            int startY,
            out int playerId,
            out int speedDelay)
        {
            playerId = -1;
            speedDelay = -1;
            if ((uint)startX >= MapWidth || (uint)startY >= MapWidth)
                return false;

            Dictionary<int, AssassinRequestInfo> index = GetRequestIndex(command);
            if (!index.TryGetValue(GetCoordinateIndex(startX, startY), out AssassinRequestInfo info) ||
                info.Ambiguous || info.PlayerId <= 0)
            {
                return false;
            }

            playerId = info.PlayerId;
            IEnemyGatePathPolicy gateProvider = EnemyGatePathPolicyBridge.Current;
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
                    long started = Stopwatch.GetTimestamp();
                    command.RequestIndex = BuildRequestIndex();
                    command.RecordRequestIndex(Stopwatch.GetTimestamp() - started);
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
                if (candidate.r_AliveState != AliveState.IsAlive ||
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
                    candidate.r_ControllableForPlayerId, candidate.N00000569);
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
                    settings.EnableMod && settings.EnableImprovedAssassinPathfinding);
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
                    settings.EnableMod && settings.EnableImprovedAssassinPathfinding);
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
            if (command == null)
                return;

            command.ElapsedTicks = Stopwatch.GetTimestamp() - command.StartedTimestamp;
            double elapsedMilliseconds = ToMilliseconds(command.ElapsedTicks);
            if (command.BuilderCalls <= 0 ||
                (!DetailedDiagnosticsEnabled && elapsedMilliseconds < SlowCommandThresholdMilliseconds))
            {
                return;
            }

            long accountedTicks = command.NativeBuilderTicks + command.ResolutionTicks +
                command.CacheLookupTicks + command.SearchTicks +
                command.ReconstructionTicks + command.PublicationTicks;
            double residualMilliseconds = ToMilliseconds(
                Math.Max(0L, command.ElapsedTicks - accountedTicks));
            LogInfo(
                $"stage=assassin-path-command-summary commandSeq={command.Sequence} " +
                $"kind={command.Kind} tribe={command.TribeId} commandTarget={command.TargetValue1},{command.TargetValue2} " +
                $"tick={command.Tick} mapEpoch={command.MapEpoch} " +
                $"elapsedMs={elapsedMilliseconds:F3} enabled={command.Enabled} " +
                $"builderCalls={command.BuilderCalls} nativeBuilderMs={ToMilliseconds(command.NativeBuilderTicks):F3} " +
                $"requestIndexBuilds={command.RequestIndexBuilds} requestIndexMs={ToMilliseconds(command.RequestIndexTicks):F3} " +
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

        private void Push(int tile)
        {
            heapOperations++;
            int position = heapCount++;
            heap[position] = tile;
            heapPositions[tile] = position;
            SiftUp(position);
        }

        private void PushOrDecrease(int tile)
        {
            int position = heapPositions[tile];
            if (position < 0)
                Push(tile);
            else
                SiftUp(position);
        }

        private int Pop()
        {
            heapOperations++;
            int result = heap[0];
            int tail = heap[--heapCount];
            heapPositions[result] = -1;
            if (heapCount > 0)
            {
                heap[0] = tail;
                heapPositions[tail] = 0;
                SiftDown(0);
            }
            return result;
        }

        private void SiftUp(int position)
        {
            int tile = heap[position];
            while (position > 0)
            {
                int parent = (position - 1) >> 1;
                if (!ComesBefore(tile, heap[parent]))
                    break;
                heap[position] = heap[parent];
                heapPositions[heap[position]] = position;
                position = parent;
            }
            heap[position] = tile;
            heapPositions[tile] = position;
        }

        private void SiftDown(int position)
        {
            int tile = heap[position];
            while (true)
            {
                int left = position * 2 + 1;
                if (left >= heapCount)
                    break;
                int right = left + 1;
                int best = right < heapCount && ComesBefore(heap[right], heap[left]) ? right : left;
                if (!ComesBefore(heap[best], tile))
                    break;
                heap[position] = heap[best];
                heapPositions[heap[position]] = position;
                position = best;
            }
            heap[position] = tile;
            heapPositions[tile] = position;
        }

        private bool ComesBefore(int left, int right)
        {
            return AssassinAStarPolicy.ComesBefore(
                estimatedTotalCosts[left], costs[left], insertionOrder[left],
                estimatedTotalCosts[right], costs[right], insertionOrder[right]);
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

        private readonly struct CachedRoute
        {
            public CachedRoute(int[] nodes, RouteSearchSummary summary)
            {
                Nodes = nodes;
                Summary = summary;
            }

            public int[] Nodes { get; }
            public RouteSearchSummary Summary { get; }
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
                bool enabled)
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
                Enabled = enabled;
                StartedTimestamp = Stopwatch.GetTimestamp();
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
            public bool Enabled { get; }
            public long StartedTimestamp { get; }
            public long ElapsedTicks { get; set; }
            public Dictionary<int, AssassinRequestInfo> RequestIndex { get; set; }
            public Dictionary<RouteCacheKey, CachedRoute> RouteCache { get; } =
                new Dictionary<RouteCacheKey, CachedRoute>();
            public List<string> Details { get; } = new List<string>();
            public int SuppressedDetails { get; private set; }
            public int BuilderCalls { get; private set; }
            public int RequestIndexBuilds { get; private set; }
            public int Searches { get; private set; }
            public int CacheHits { get; private set; }
            public int FailedSearches { get; private set; }
            public int ExpandedNodes { get; private set; }
            public int HeapOperations { get; private set; }
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

            public bool TryGetSuffixCost(SuffixCacheKey key, int node, out int cost)
            {
                cost = 0;
                return suffixCosts.TryGetValue(key, out Dictionary<int, int> field) &&
                    field.TryGetValue(node, out cost);
            }

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
                BuilderCalls++;
                NativeBuilderTicks += ticks;
            }

            public void RecordRequestIndex(long ticks)
            {
                RequestIndexBuilds++;
                RequestIndexTicks += ticks;
            }

            public void RecordResolution(long ticks) => ResolutionTicks += ticks;
            public void RecordCacheLookup(long ticks) => CacheLookupTicks += ticks;

            public void RecordSearch(RouteSearchSummary summary)
            {
                Searches++;
                ExpandedNodes += summary.ExpandedNodes;
                HeapOperations += summary.HeapOperations;
                SearchTicks += summary.SearchTicks;
                ReconstructionTicks += summary.ReconstructionTicks;
                AddDetail(summary, "search");
            }

            public void RecordFailedSearch(long ticks, int expanded, int heapOperations)
            {
                Searches++;
                FailedSearches++;
                SearchTicks += ticks;
                ExpandedNodes += expanded;
                HeapOperations += heapOperations;
            }

            public void RecordCacheHit(RouteSearchSummary summary)
            {
                CacheHits++;
                AddDetail(summary, "cache");
            }

            public void RecordPublication(long ticks, bool success, RouteSearchSummary summary)
            {
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

            public void RecordTotal(long ticks) => TotalRequestTicks += ticks;

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

        private void LogDebug(string message) => log.LogDebug($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogInfo(string message) => log.LogInfo($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogWarning(string message) => log.LogWarning($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
