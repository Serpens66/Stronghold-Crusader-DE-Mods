// Feature: Weighted replacement for Vanilla's Assassin-only path-cost expansion.
using BepInEx.Logging;
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
    internal sealed unsafe class AssassinPathfindingRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AssassinPathBuilderDelegate(
            IntPtr context,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int continuation);

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
        private HookTransaction transaction;
        private readonly DetourHandle<AssassinPathBuilderDelegate> detour =
            new DetourHandle<AssassinPathBuilderDelegate>();
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

        public AssassinPathfindingRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings, AssassinClimbRuntime climbRuntime)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.climbRuntime = climbRuntime ?? throw new ArgumentNullException(nameof(climbRuntime));
            for (int node = 0; node < CoordinateCount; node++)
            {
                costs[node] = int.MaxValue;
                parents[node] = -1;
                heapPositions[node] = -1;
            }
        }

        public bool IsInstalled => detour.Success && detour.IsInstalled;

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

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinBuilderPattern,
                AssassinBuilderRva,
                referenceHashMatches: true,
                "Assassin path-cost builder",
                log);
            IntPtr resolved = IntPtr.Add(newLibraryHandle, resolution.Rva);
            if (resolved != IntPtr.Add(newLibraryHandle, AssassinBuilderRva))
                throw new InvalidOperationException("Assassin path-cost builder resolved outside its validated RVA");

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
                    memory,
                    referenceHashMatches: true);
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddDetour(
                    detour,
                    HookTarget.FromAddress(unchecked((ulong)resolved.ToInt64())),
                    BuildWeightedPath);
                CommitResult commitResult = transaction.Commit();
                if (!commitResult.IsCompleteSuccess || !detour.Success)
                    throw new InvalidOperationException("The weighted Assassin pathfinding detour was not installed.");
                reconstructionPatch = pendingReconstructionPatch;
                moveCommandSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(ObserveMoveCommand);
                targetCommandSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                    .Subscribe(ObserveTargetCommand);
                ApplySetting();
                LogDebug($"weighted Assassin pathfinding installed at RVA 0x{AssassinBuilderRva:X}; climb costs={AssassinClimbCostPolicy.MinimumClimbTicks}/{AssassinClimbCostPolicy.LowWallClimbTicks}/{AssassinClimbCostPolicy.NormalWallClimbTicks} ticks.");
            }
            catch
            {
                moveCommandSubscription?.Dispose();
                moveCommandSubscription = null;
                targetCommandSubscription?.Dispose();
                targetCommandSubscription = null;
                if (pendingReconstructionPatch?.IsApplied == true)
                    pendingReconstructionPatch.SetEnabled(false);
                transaction?.Dispose();
                transaction = null;
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
            moveCommandSubscription?.Dispose();
            moveCommandSubscription = null;
            targetCommandSubscription?.Dispose();
            targetCommandSubscription = null;
            ClearTransientState();
        }

        private int BuildWeightedPath(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            if (!detour.Success)
                return 0;

            AssassinCommandScope command = activeCommand;
            long requestStarted = Stopwatch.GetTimestamp();
            long nativeStarted = requestStarted;
            // Vanilla initializes internal queue state even when our compact route field replaces it.
            int vanillaResult = detour.Original(context, startX, startY, targetX, targetY, maximumNodes, continuation);
            long nativeTicks = Stopwatch.GetTimestamp() - nativeStarted;
            command?.RecordNativeBuilder(nativeTicks);

            bool enabled = command?.Enabled ??
                (settings.EnableMod && settings.EnableImprovedAssassinPathfinding);
            if (!enabled || continuation != 0)
                return vanillaResult;
            if (targetX < 0 || targetY < 0)
                return vanillaResult;

            try
            {
                long resolutionStarted = Stopwatch.GetTimestamp();
                if (!TryResolveAssassinRequest(command, startX, startY, out int playerId, out int speedDelay))
                {
                    command?.RecordResolution(Stopwatch.GetTimestamp() - resolutionStarted);
                    return vanillaResult;
                }
                command?.RecordResolution(Stopwatch.GetTimestamp() - resolutionStarted);
                if (!EnsureCoordinateTileMappingValidated())
                    return vanillaResult;

                bool allowClimbing = command?.GetClimbingAllowed(playerId, climbRuntime) ??
                    climbRuntime.IsClimbingAllowed(playerId);
                // Never publish a relaxed route unless Vanilla can reconstruct the same
                // validated reserved climb endpoints. This keeps patch failures fail-closed.
                bool allowWalkableReservedClimbEndpoints = reconstructionPatch?.IsApplied == true;
                var cacheKey = new RouteCacheKey(
                    startX, startY, targetX, targetY, maximumNodes, speedDelay,
                    playerId, allowClimbing, allowWalkableReservedClimbEndpoints);
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
                        out routeSummary);
                    if (routeReady && command != null)
                        CachePreparedRoute(command, cacheKey, routeSummary);
                }

                if (!routeReady)
                    return 0;

                long publicationStarted = Stopwatch.GetTimestamp();
                bool published = CommitPreparedRoute(context, routeSummary.RouteLength);
                command?.RecordPublication(
                    Stopwatch.GetTimestamp() - publicationStarted,
                    published,
                    routeSummary);
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
                allowWalkableReservedClimbEndpoints);
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
                occupancyLayer[current]);
            bool targetBuildingAccepted = AssassinClimbTransitionPolicy.CanUseTargetTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[target],
                occupancyLayer[target]);
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
                key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints);
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
                int candidateDelay = candidate.r_CurrentSpeed;
                if (!index.TryGetValue(coordinate, out AssassinRequestInfo existing))
                {
                    index.Add(coordinate, new AssassinRequestInfo(
                        candidatePlayer, candidateDelay, ambiguous: false));
                    continue;
                }

                bool ambiguous = existing.Ambiguous || existing.PlayerId != candidatePlayer;
                int slowestDelay = Math.Max(existing.SpeedDelay, candidateDelay);
                index[coordinate] = new AssassinRequestInfo(
                    existing.PlayerId, slowestDelay, ambiguous);
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
            public AssassinRequestInfo(int playerId, int speedDelay, bool ambiguous)
            {
                PlayerId = playerId;
                SpeedDelay = speedDelay;
                Ambiguous = ambiguous;
            }

            public int PlayerId { get; }
            public int SpeedDelay { get; }
            public bool Ambiguous { get; }
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

        private readonly struct RouteCacheKey : IEquatable<RouteCacheKey>
        {
            public RouteCacheKey(
                int startX,
                int startY,
                int targetX,
                int targetY,
                int maximumNodes,
                int speedDelay,
                int playerId,
                bool allowClimbing,
                bool allowWalkableReservedClimbEndpoints)
            {
                StartX = startX;
                StartY = startY;
                TargetX = targetX;
                TargetY = targetY;
                MaximumNodes = maximumNodes;
                SpeedDelay = speedDelay;
                PlayerId = playerId;
                AllowClimbing = allowClimbing;
                AllowWalkableReservedClimbEndpoints = allowWalkableReservedClimbEndpoints;
            }

            public int StartX { get; }
            public int StartY { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int MaximumNodes { get; }
            public int SpeedDelay { get; }
            public int PlayerId { get; }
            public bool AllowClimbing { get; }
            public bool AllowWalkableReservedClimbEndpoints { get; }

            public bool Equals(RouteCacheKey other) =>
                StartX == other.StartX && StartY == other.StartY &&
                TargetX == other.TargetX && TargetY == other.TargetY &&
                MaximumNodes == other.MaximumNodes && SpeedDelay == other.SpeedDelay &&
                PlayerId == other.PlayerId && AllowClimbing == other.AllowClimbing &&
                AllowWalkableReservedClimbEndpoints == other.AllowWalkableReservedClimbEndpoints;

            public override bool Equals(object obj) =>
                obj is RouteCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StartX;
                    hash = hash * 397 ^ StartY;
                    hash = hash * 397 ^ TargetX;
                    hash = hash * 397 ^ TargetY;
                    hash = hash * 397 ^ MaximumNodes;
                    hash = hash * 397 ^ SpeedDelay;
                    hash = hash * 397 ^ PlayerId;
                    hash = hash * 397 ^ (AllowClimbing ? 1 : 0);
                    return hash * 397 ^ (AllowWalkableReservedClimbEndpoints ? 1 : 0);
                }
            }
        }

        private readonly struct SuffixCacheKey : IEquatable<SuffixCacheKey>
        {
            public SuffixCacheKey(
                int targetX,
                int targetY,
                int speedDelay,
                bool allowClimbing,
                bool allowWalkableReservedClimbEndpoints)
            {
                TargetX = targetX;
                TargetY = targetY;
                SpeedDelay = speedDelay;
                AllowClimbing = allowClimbing;
                AllowWalkableReservedClimbEndpoints = allowWalkableReservedClimbEndpoints;
            }

            public int TargetX { get; }
            public int TargetY { get; }
            public int SpeedDelay { get; }
            public bool AllowClimbing { get; }
            public bool AllowWalkableReservedClimbEndpoints { get; }

            public bool Equals(SuffixCacheKey other) =>
                TargetX == other.TargetX && TargetY == other.TargetY &&
                SpeedDelay == other.SpeedDelay && AllowClimbing == other.AllowClimbing &&
                AllowWalkableReservedClimbEndpoints == other.AllowWalkableReservedClimbEndpoints;

            public override bool Equals(object obj) =>
                obj is SuffixCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = TargetX;
                    hash = hash * 397 ^ TargetY;
                    hash = hash * 397 ^ SpeedDelay;
                    hash = hash * 397 ^ (AllowClimbing ? 1 : 0);
                    return hash * 397 ^ (AllowWalkableReservedClimbEndpoints ? 1 : 0);
                }
            }
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
