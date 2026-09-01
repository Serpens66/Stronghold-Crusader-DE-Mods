// Feature: Weighted replacement for Vanilla's Assassin-only path-cost expansion.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AssassinPathfindingRuntime
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
        private const uint CompletedMoatFlag = 1u << 30;
        private const byte GroundEdgeKind = 1;
        private const byte MoatEdgeKind = 2;
        private const byte ClimbEdgeKind = 3;
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
        private readonly int[] heap = new int[CoordinateCount];
        private readonly int[] heapPositions = new int[CoordinateCount];
        private readonly int[] touched = new int[CoordinateCount];
        private readonly byte[] incomingEdgeKinds = new byte[CoordinateCount];
        private readonly int[] route = new int[MaximumCommittedPathLength + 1];
        private readonly byte[] seenTiles = new byte[TileCount];
        private IntPtr libraryHandle;
        private AssassinPathBuilderDelegate original;
        private AssassinPathBuilderDelegate rootedDetour;
        private SpecialTilePredicateDelegate specialTilePredicate;
        private NativeDetour detour;
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
        private bool fallbackLogged;
        private bool coordinateMapValidated;
        private bool coordinateValidationFailureLogged;
        private bool moatProviderFailureLogged;
        private bool routeSearchActive;
        private string lastMoatRouteLog;

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

        public bool IsInstalled => detour != null;

        public void InitializeNative(IntPtr newLibraryHandle, ReadOnlySpan<byte> memory, bool fixedLayoutHashValidated)
        {
            if (detour != null)
                return;
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

            rootedDetour = BuildWeightedPath;
            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedDetour);
            NativeDetour installed = null;
            AssassinPathReconstructionPatch pendingReconstructionPatch = null;
            try
            {
                pendingReconstructionPatch = new AssassinPathReconstructionPatch(
                    log,
                    newLibraryHandle,
                    memory,
                    referenceHashMatches: true);
                installed = new NativeDetour(resolved, detourAddress, new NativeDetourConfig { ManualApply = true });
                original = installed.GenerateTrampoline<AssassinPathBuilderDelegate>();
                installed.Apply();
                detour = installed;
                reconstructionPatch = pendingReconstructionPatch;
                AssassinMoatPathBridge.AttachRuntime(this);
                ApplySetting();
                LogDebug($"weighted Assassin pathfinding installed at RVA 0x{AssassinBuilderRva:X}; climb costs={AssassinClimbCostPolicy.MinimumClimbTicks}/{AssassinClimbCostPolicy.LowWallClimbTicks}/{AssassinClimbCostPolicy.NormalWallClimbTicks} ticks.");
            }
            catch
            {
                if (pendingReconstructionPatch?.IsApplied == true)
                    pendingReconstructionPatch.SetEnabled(false);
                installed?.Dispose();
                original = null;
                rootedDetour = null;
                detour = null;
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
                IsInstalled) || (IsInstalled && AssassinMoatPathBridge.IsProviderActive));
        }

        internal void OnMoatPathProviderChanged()
        {
            moatProviderFailureLogged = false;
            lastMoatRouteLog = null;
            ApplySetting();
            LogDebug($"MoveMoatTest Assassin route provider " +
                $"{(AssassinMoatPathBridge.IsProviderActive ? "registered" : "removed")}; " +
                "the weighted builder remains the sole RVA 0xD9C40 hook owner.");
        }

        public void BeginMap()
        {
            ResetMapValidation();
        }

        public void EndMap()
        {
            ResetMapValidation();
        }

        private int BuildWeightedPath(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            AssassinPathBuilderDelegate vanilla = original;
            if (vanilla == null)
                return 0;

            // Vanilla initializes internal queue state even when our compact route field replaces it.
            int vanillaResult = vanilla(context, startX, startY, targetX, targetY, maximumNodes, continuation);
            bool improvedPathfindingEnabled = settings.EnableMod && settings.EnableImprovedAssassinPathfinding;
            bool moatProviderActive = AssassinMoatPathBridge.IsProviderActive;
            if ((!improvedPathfindingEnabled && !moatProviderActive) || continuation != 0)
                return vanillaResult;
            if (targetX < 0 || targetY < 0)
                return vanillaResult;

            try
            {
                if (!TryResolveAssassinRequest(startX, startY, out int playerId, out int speedDelay))
                    return vanillaResult;
                if (!EnsureCoordinateTileMappingValidated())
                    return vanillaResult;

                bool allowClimbing = climbRuntime.IsClimbingAllowed(playerId);
                // Never publish a relaxed route unless Vanilla can reconstruct the same
                // validated reserved climb endpoints. This keeps patch failures fail-closed.
                bool allowWalkableReservedClimbEndpoints =
                    (improvedPathfindingEnabled || moatProviderActive) &&
                    reconstructionPatch?.IsApplied == true;
                if (routeSearchActive)
                    return vanillaResult;

                routeSearchActive = true;
                bool found;
                RouteSearchSummary routeSummary;
                try
                {
                    found = TryBuildWeightedRoute(
                        context,
                        playerId,
                        startX,
                        startY,
                        targetX,
                        targetY,
                        maximumNodes,
                        speedDelay,
                        allowClimbing,
                        allowWalkableReservedClimbEndpoints,
                        allowFriendlyMoatEdges: moatProviderActive,
                        commitRoute: false,
                        out routeSummary);
                }
                finally
                {
                    routeSearchActive = false;
                }

                bool publishMoatRoute = found && routeSummary.UsedFriendlyMoat &&
                    moatProviderActive && vanillaResult == 0;
                if (!improvedPathfindingEnabled && !publishMoatRoute)
                    return vanillaResult;
                if (!found || !CommitPreparedRoute(context, routeSummary.RouteLength))
                    return 0;

                if (routeSummary.UsedFriendlyMoat)
                    LogMoatRoute("builder", playerId, startX, startY, targetX, targetY, routeSummary);
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
        }

        private bool TryBuildWeightedRoute(
            IntPtr context,
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int speedDelay,
            bool allowClimbing,
            bool allowWalkableReservedClimbEndpoints,
            bool allowFriendlyMoatEdges,
            bool commitRoute,
            out RouteSearchSummary routeSummary)
        {
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
            Touch(startNode, 0, -1, 0);
            Push(startNode);
            int expanded = 0;
            int nodeLimit = Math.Max(1, Math.Min(maximumNodes, TileCount));

            while (heapCount > 0 && expanded < nodeLimit)
            {
                int currentNode = Pop();
                expanded++;
                if (currentNode == targetNode)
                {
                    if (!PrepareRoute(startNode, targetNode, playerId, out routeSummary))
                        return false;
                    return !commitRoute || CommitPreparedRoute(context, routeSummary.RouteLength);
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
                    // Keep the pre-bridge weighted graph byte-for-byte equivalent at the
                    // decision level when no provider is registered.
                    bool currentIsCompletedMoat = allowFriendlyMoatEdges &&
                        (currentFlags & CompletedMoatFlag) != 0;
                    bool nextIsCompletedMoat = allowFriendlyMoatEdges &&
                        (nextFlags & CompletedMoatFlag) != 0;
                    bool currentIsFriendlyMoat = currentIsCompletedMoat &&
                        IsFriendlyCompletedMoat(playerId, currentTile);
                    bool nextIsFriendlyMoat = nextIsCompletedMoat &&
                        IsFriendlyCompletedMoat(playerId, nextTile);
                    if ((currentIsCompletedMoat && !currentIsFriendlyMoat) ||
                        (nextIsCompletedMoat && !nextIsFriendlyMoat))
                    {
                        continue;
                    }

                    bool cardinal = (direction & 1) == 0;
                    bool includesFriendlyMoat = currentIsFriendlyMoat || nextIsFriendlyMoat;
                    bool hasWall = ((currentFlags | nextFlags) & IsWallFlag) != 0;
                    bool ordinaryEdge = !currentIsCompletedMoat && !nextIsCompletedMoat &&
                        (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
                    bool moatEdge = cardinal && includesFriendlyMoat && !hasWall &&
                        IsAcceptedMoatGroundTarget(
                            nextTile,
                            nextFlags,
                            nextIsFriendlyMoat,
                            allowWalkableReservedClimbEndpoints);
                    bool climbEdge = false;
                    if (!ordinaryEdge && !moatEdge)
                    {
                        if (!cardinal)
                            continue;
                        bool fallbackAccepted = IsVanillaAssassinFallback(
                            currentTile,
                            nextTile,
                            currentFlags,
                            currentIsFriendlyMoat,
                            nextIsFriendlyMoat,
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
                        Touch(nextNode, newCost, currentNode,
                            climbEdge ? ClimbEdgeKind : moatEdge ? MoatEdgeKind : GroundEdgeKind);
                    else
                    {
                        costs[nextNode] = newCost;
                        parents[nextNode] = currentNode;
                        incomingEdgeKinds[nextNode] =
                            climbEdge ? ClimbEdgeKind : moatEdge ? MoatEdgeKind : GroundEdgeKind;
                    }
                    PushOrDecrease(nextNode);
                }
            }

            return false;
        }

        private bool IsVanillaAssassinFallback(
            int current,
            int target,
            uint currentFlags,
            bool currentIsFriendlyMoat,
            bool targetIsFriendlyMoat,
            bool allowWalkableReservedClimbEndpoints)
        {
            uint targetFlags = tileFlags[target];
            bool targetAccepted = targetIsFriendlyMoat ||
                (targetFlags & AssassinFallbackBlockingMask) == 0;
            if (!targetAccepted && (targetFlags & NativeSpecialTileFlag) != 0)
            {
                targetAccepted = specialTilePredicate(
                    IntPtr.Add(libraryHandle, SpecialTilePredicateContextRva),
                    target) != 0;
            }

            bool startAccepted = currentIsFriendlyMoat || AssassinClimbTransitionPolicy.CanUseStartTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[current],
                occupancyLayer[current]);
            bool targetBuildingAccepted = targetIsFriendlyMoat || AssassinClimbTransitionPolicy.CanUseTargetTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[target],
                occupancyLayer[target]);
            bool hasWall = ((currentFlags | targetFlags) & IsWallFlag) != 0;
            return targetAccepted && startAccepted && targetBuildingAccepted && hasWall;
        }

        private bool IsAcceptedMoatGroundTarget(
            int target,
            uint targetFlags,
            bool targetIsFriendlyMoat,
            bool allowWalkableReservedClimbEndpoints)
        {
            if (targetIsFriendlyMoat)
                return buildingLayer[target] == 0;

            bool targetAccepted = (targetFlags & AssassinFallbackBlockingMask) == 0;
            if (!targetAccepted && (targetFlags & NativeSpecialTileFlag) != 0)
            {
                targetAccepted = specialTilePredicate(
                    IntPtr.Add(libraryHandle, SpecialTilePredicateContextRva), target) != 0;
            }
            return targetAccepted && AssassinClimbTransitionPolicy.CanUseTargetTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[target],
                occupancyLayer[target]);
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
            int playerId,
            out RouteSearchSummary summary)
        {
            summary = default;
            int routeLength = 0;
            int node = targetNode;
            int groundEdges = 0;
            int moatEdges = 0;
            int climbEdges = 0;
            bool usedFriendlyMoat = false;
            while (node >= 0 && routeLength < route.Length)
            {
                route[routeLength++] = node;
                int routeX = node % MapWidth;
                int routeY = node / MapWidth;
                int routeTile = GetTileId(routeX, routeY);
                if (!IsNativeTile(routeTile))
                    return false;
                if ((tileFlags[routeTile] & CompletedMoatFlag) != 0 &&
                    IsFriendlyCompletedMoat(playerId, routeTile))
                {
                    usedFriendlyMoat = true;
                }
                switch (incomingEdgeKinds[node])
                {
                    case GroundEdgeKind:
                        groundEdges++;
                        break;
                    case MoatEdgeKind:
                        moatEdges++;
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
                usedFriendlyMoat,
                groundEdges,
                moatEdges,
                climbEdges);
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

        internal int ProbeMoatRoute(
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY)
        {
            if (!AssassinMoatPathBridge.IsProviderActive || routeSearchActive ||
                !IsValidCoordinate(startX, startY) || !IsValidCoordinate(targetX, targetY))
            {
                return 0;
            }

            try
            {
                if (!TryResolveAssassinRequest(startX, startY, out int resolvedPlayerId, out int speedDelay) ||
                    resolvedPlayerId != playerId || !EnsureCoordinateTileMappingValidated())
                {
                    return 0;
                }

                routeSearchActive = true;
                bool found = TryBuildWeightedRoute(
                    IntPtr.Zero,
                    playerId,
                    startX,
                    startY,
                    targetX,
                    targetY,
                    TileCount,
                    speedDelay,
                    climbRuntime.IsClimbingAllowed(playerId),
                    reconstructionPatch?.IsApplied == true,
                    allowFriendlyMoatEdges: true,
                    commitRoute: false,
                    out RouteSearchSummary summary);
                if (!found || !summary.UsedFriendlyMoat)
                    return 0;

                LogMoatRoute("cursor-probe", playerId, startX, startY, targetX, targetY, summary);
                return 1 | 2 | (summary.ClimbEdges > 0 ? 4 : 0);
            }
            catch (Exception ex)
            {
                if (!moatProviderFailureLogged)
                {
                    moatProviderFailureLogged = true;
                    LogWarning($"MoveMoatTest Assassin route probe failed closed: {ex.Message}");
                }
                return 0;
            }
            finally
            {
                routeSearchActive = false;
            }
        }

        private bool TryResolveAssassinRequest(int startX, int startY, out int playerId, out int speedDelay)
        {
            playerId = -1;
            speedDelay = -1;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit candidate = ref units[spanIndex];
                if (candidate.r_AliveState != AliveState.IsAlive ||
                    candidate.r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                    candidate.r_CurrentTilePositionX != startX || candidate.r_CurrentTilePositionY != startY)
                {
                    continue;
                }

                int candidatePlayer = candidate.r_ControllableForPlayerId;
                if (playerId > 0 && candidatePlayer != playerId)
                    return false;
                playerId = candidatePlayer;
                int candidateDelay = candidate.r_CurrentSpeed;
                if (candidateDelay > speedDelay)
                    speedDelay = candidateDelay;
            }
            if (speedDelay < 0)
                speedDelay = GameUnitManagerAPI.Instance.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            return playerId > 0;
        }

        private bool IsValidCoordinate(int x, int y)
        {
            return (uint)x < MapWidth && (uint)y < MapWidth && validCoordinates[y * MapWidth + x] != 0;
        }

        private bool IsFriendlyCompletedMoat(int playerId, int tileId)
        {
            if (!IsNativeTile(tileId) || (tileFlags[tileId] & CompletedMoatFlag) == 0)
                return false;

            try
            {
                return AssassinMoatPathBridge.IsFriendlyCompletedMoat(playerId, tileId);
            }
            catch (Exception ex)
            {
                if (!moatProviderFailureLogged)
                {
                    moatProviderFailureLogged = true;
                    LogWarning($"MoveMoatTest moat classifier failed closed: {ex.Message}");
                }
                return false;
            }
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

        private void ResetMapValidation()
        {
            coordinateMapValidated = false;
            coordinateValidationFailureLogged = false;
            fallbackLogged = false;
            moatProviderFailureLogged = false;
            lastMoatRouteLog = null;
        }

        private void Touch(int node, int cost, int parent, byte incomingEdgeKind)
        {
            touched[touchedCount++] = node;
            costs[node] = cost;
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
            return costs[left] < costs[right] ||
                (costs[left] == costs[right] && insertionOrder[left] < insertionOrder[right]);
        }

        private void LogMoatRoute(
            string source,
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            RouteSearchSummary summary)
        {
            string signature = $"{source}:{playerId}:{startX}:{startY}:{targetX}:{targetY}:" +
                $"{summary.RouteLength}:{summary.GroundEdges}:{summary.MoatEdges}:" +
                $"{summary.ClimbEdges}";
            if (string.Equals(lastMoatRouteLog, signature, StringComparison.Ordinal))
                return;

            lastMoatRouteLog = signature;
            LogDebug($"MoveMoatTest Assassin route source={source}, player={playerId}, " +
                $"start=({startX},{startY}), target=({targetX},{targetY}), " +
                $"length={summary.RouteLength}, groundEdges={summary.GroundEdges}, " +
                $"moatEdges={summary.MoatEdges}, climbEdges={summary.ClimbEdges}.");
        }

        private readonly struct RouteSearchSummary
        {
            public RouteSearchSummary(
                int routeLength,
                bool usedFriendlyMoat,
                int groundEdges,
                int moatEdges,
                int climbEdges)
            {
                RouteLength = routeLength;
                UsedFriendlyMoat = usedFriendlyMoat;
                GroundEdges = groundEdges;
                MoatEdges = moatEdges;
                ClimbEdges = climbEdges;
            }

            public int RouteLength { get; }
            public bool UsedFriendlyMoat { get; }
            public int GroundEdges { get; }
            public int MoatEdges { get; }
            public int ClimbEdges { get; }
        }

        private void LogDebug(string message) => log.LogDebug($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogWarning(string message) => log.LogWarning($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
