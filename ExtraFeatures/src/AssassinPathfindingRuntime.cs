// Feature: Weighted replacement for Vanilla's Assassin-only path-cost expansion.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ExtraFeatures
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
        private static readonly long DiagnosticIntervalTicks = Math.Max(1L, Stopwatch.Frequency * 2L);
        private const string AssassinBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 EA 48 8B D9 49 63 F9";

        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly AssassinClimbRuntime climbRuntime;
        private readonly int[] costs = new int[CoordinateCount];
        private readonly int[] parents = new int[CoordinateCount];
        private readonly int[] incomingClimbTicks = new int[CoordinateCount];
        private readonly byte[] incomingClimbEdges = new byte[CoordinateCount];
        private readonly int[] insertionOrder = new int[CoordinateCount];
        private readonly int[] heap = new int[CoordinateCount];
        private readonly int[] heapPositions = new int[CoordinateCount];
        private readonly int[] touched = new int[CoordinateCount];
        private readonly int[] route = new int[MaximumCommittedPathLength + 1];
        private IntPtr libraryHandle;
        private AssassinPathBuilderDelegate original;
        private AssassinPathBuilderDelegate rootedDetour;
        private SpecialTilePredicateDelegate specialTilePredicate;
        private NativeDetour detour;
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
        private long nextDiagnosticTimestamp;
        private int lastDiagnosticStart = -1;
        private int lastDiagnosticTarget = -1;
        private bool lastDiagnosticClimbingAllowed;
        private bool fallbackLogged;

        public AssassinPathfindingRuntime(ManualLogSource log, ExtraFeaturesViewModel settings, AssassinClimbRuntime climbRuntime)
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
            ValidateCoordinateTileMapping();

            rootedDetour = BuildWeightedPath;
            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedDetour);
            NativeDetour installed = null;
            try
            {
                installed = new NativeDetour(resolved, detourAddress, new NativeDetourConfig { ManualApply = true });
                original = installed.GenerateTrampoline<AssassinPathBuilderDelegate>();
                installed.Apply();
                detour = installed;
                LogInfo($"weighted Assassin pathfinding installed at RVA 0x{AssassinBuilderRva:X}; climb costs={AssassinClimbCostPolicy.MinimumClimbTicks}/{AssassinClimbCostPolicy.LowWallClimbTicks}/{AssassinClimbCostPolicy.NormalWallClimbTicks} ticks.");
            }
            catch
            {
                installed?.Dispose();
                original = null;
                rootedDetour = null;
                throw;
            }
        }

        private int BuildWeightedPath(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            AssassinPathBuilderDelegate vanilla = original;
            if (vanilla == null)
                return 0;

            // Vanilla initializes internal queue state even when our compact route field replaces it.
            int vanillaResult = vanilla(context, startX, startY, targetX, targetY, maximumNodes, continuation);
            if (!settings.EnableMod || !settings.EnableImprovedAssassinPathfinding || continuation != 0)
                return vanillaResult;
            if (targetX < 0 || targetY < 0)
                return vanillaResult;

            try
            {
                if (!TryResolveAssassinRequest(startX, startY, out int playerId, out int speedDelay))
                    return vanillaResult;

                bool allowClimbing = climbRuntime.IsClimbingAllowed(playerId);
                bool found = TryBuildWeightedRoute(
                    context,
                    startX,
                    startY,
                    targetX,
                    targetY,
                    maximumNodes,
                    speedDelay,
                    allowClimbing,
                    out PathDiagnostic diagnostic);
                LogPathDiagnosticIfDue(playerId, startX, startY, targetX, targetY, speedDelay, allowClimbing, vanillaResult, found, diagnostic);
                return found ? 1 : 0;
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
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int speedDelay,
            bool allowClimbing,
            out PathDiagnostic diagnostic)
        {
            diagnostic = default;
            if (!IsValidCoordinate(startX, startY) || !IsValidCoordinate(targetX, targetY))
                return false;

            ResetTouchedNodes();
            int cardinalTicks = AssassinClimbCostPolicy.GetCardinalMovementTicks(speedDelay);
            int diagonalTicks = AssassinClimbCostPolicy.GetDiagonalMovementTicks(speedDelay);
            diagnostic.CardinalTicks = cardinalTicks;
            diagnostic.DiagonalTicks = diagonalTicks;
            int startTile = GetTileId(startX, startY);
            int targetTile = GetTileId(targetX, targetY);
            if (!IsNativeTile(startTile) || !IsNativeTile(targetTile))
                return false;

            int startNode = GetCoordinateIndex(startX, startY);
            int targetNode = GetCoordinateIndex(targetX, targetY);
            Touch(startNode, 0, -1, 0, false);
            Push(startNode);
            int expanded = 0;
            int nodeLimit = Math.Max(1, Math.Min(maximumNodes, TileCount));

            while (heapCount > 0 && expanded < nodeLimit)
            {
                int currentNode = Pop();
                expanded++;
                diagnostic.ExpandedNodes = expanded;
                if (currentNode == targetNode)
                    return CommitRoute(context, startNode, targetNode, ref diagnostic);

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
                    bool ordinaryEdge = (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
                    bool climbEdge = false;
                    if (!ordinaryEdge)
                    {
                        if ((direction & 1) != 0 || !IsVanillaAssassinFallback(currentTile, nextTile, currentFlags))
                            continue;
                        climbEdge = true;
                        diagnostic.ClimbCandidates++;
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
                        Touch(nextNode, newCost, currentNode, climbTicks, climbEdge);
                    else
                    {
                        costs[nextNode] = newCost;
                        parents[nextNode] = currentNode;
                        incomingClimbTicks[nextNode] = climbTicks;
                        incomingClimbEdges[nextNode] = climbEdge ? (byte)1 : (byte)0;
                    }
                    if (climbEdge)
                        diagnostic.AcceptedClimbEdges++;
                    else
                        diagnostic.AcceptedOrdinaryEdges++;
                    PushOrDecrease(nextNode);
                }
            }

            return false;
        }

        private bool IsVanillaAssassinFallback(int current, int target, uint currentFlags)
        {
            uint targetFlags = tileFlags[target];
            bool targetAccepted = (targetFlags & AssassinFallbackBlockingMask) == 0;
            if (!targetAccepted && (targetFlags & NativeSpecialTileFlag) != 0)
            {
                targetAccepted = specialTilePredicate(
                    IntPtr.Add(libraryHandle, SpecialTilePredicateContextRva),
                    target) != 0;
            }

            return targetAccepted && buildingLayer[current] == 0 && buildingLayer[target] == 0 &&
                ((currentFlags | targetFlags) & IsWallFlag) != 0;
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

        private bool CommitRoute(IntPtr context, int startNode, int targetNode, ref PathDiagnostic diagnostic)
        {
            int routeLength = 0;
            int node = targetNode;
            while (node >= 0 && routeLength < route.Length)
            {
                route[routeLength++] = node;
                if (node == startNode)
                    break;
                node = parents[node];
            }

            if (routeLength == 0 || routeLength > MaximumCommittedPathLength || route[routeLength - 1] != startNode)
                return false;

            int generation = *(int*)((byte*)context.ToPointer() + 4) + 1;
            if (generation > 32000)
            {
                new Span<short>(nativeVisitStamps, TileCount).Clear();
                generation = 1;
            }
            *(int*)((byte*)context.ToPointer() + 4) = generation;

            for (int reverseIndex = routeLength - 1, distance = 1; reverseIndex >= 0; reverseIndex--, distance++)
            {
                int routeNode = route[reverseIndex];
                int routeX = routeNode % MapWidth;
                int routeY = routeNode / MapWidth;
                int routeTile = GetTileId(routeX, routeY);
                if (!IsNativeTile(routeTile))
                    return false;
                nativeVisitStamps[routeTile] = (short)generation;
                nativeDistances[routeTile] = (short)distance;

                if (reverseIndex < routeLength - 1)
                {
                    int climbTicks = incomingClimbTicks[routeNode];
                    diagnostic.SelectedClimbTicks += climbTicks;
                    if (incomingClimbEdges[routeNode] != 0)
                        diagnostic.SelectedClimbEdges++;
                }
            }
            diagnostic.RouteLength = routeLength;
            diagnostic.TotalTicks = costs[targetNode];
            diagnostic.SelectedMovementTicks = diagnostic.TotalTicks - diagnostic.SelectedClimbTicks;
            return true;
        }

        private bool TryResolveAssassinRequest(int startX, int startY, out int playerId, out int speedDelay)
        {
            playerId = -1;
            speedDelay = -1;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int index = 0; index < units.Length; index++)
            {
                ref GameUnit candidate = ref units[index];
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

        private int GetTileId(int x, int y) => rowLookup[y * 3] + x;

        private static int GetCoordinateIndex(int x, int y) => y * MapWidth + x;

        private static bool IsNativeTile(int tile) => (uint)tile < TileCount;

        private void ValidateCoordinateTileMapping()
        {
            var seenTiles = new byte[TileCount];
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
            LogInfo($"Assassin coordinate map validated: validCoordinates={validCount}, nativeTileCapacity={TileCount}.");
        }

        private void Touch(int node, int cost, int parent, int climbTicks, bool climbEdge)
        {
            touched[touchedCount++] = node;
            costs[node] = cost;
            parents[node] = parent;
            incomingClimbTicks[node] = climbTicks;
            incomingClimbEdges[node] = climbEdge ? (byte)1 : (byte)0;
            insertionOrder[node] = nextInsertionOrder++;
        }

        private void ResetTouchedNodes()
        {
            for (int index = 0; index < touchedCount; index++)
            {
                int node = touched[index];
                costs[node] = int.MaxValue;
                parents[node] = -1;
                incomingClimbTicks[node] = 0;
                incomingClimbEdges[node] = 0;
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

        private void LogPathDiagnosticIfDue(
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int speedDelay,
            bool allowClimbing,
            int vanillaResult,
            bool found,
            PathDiagnostic diagnostic)
        {
            int localPlayerId;
            try
            {
                localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            }
            catch
            {
                return;
            }
            if (playerId != localPlayerId)
                return;

            int start = GetCoordinateIndex(startX, startY);
            int target = GetCoordinateIndex(targetX, targetY);
            long now = Stopwatch.GetTimestamp();
            bool sameRequest = start == lastDiagnosticStart && target == lastDiagnosticTarget &&
                allowClimbing == lastDiagnosticClimbingAllowed;
            if (sameRequest && now < nextDiagnosticTimestamp)
                return;

            lastDiagnosticStart = start;
            lastDiagnosticTarget = target;
            lastDiagnosticClimbingAllowed = allowClimbing;
            nextDiagnosticTimestamp = now + DiagnosticIntervalTicks;
            log.LogDebug(
                $"[{TimestampNow()}] Extra Features Assassin path diagnostic: " +
                $"playerId={playerId}, start={startX},{startY}, target={targetX},{targetY}, " +
                $"speedDelay={speedDelay}, movementTicks={diagnostic.CardinalTicks}/{diagnostic.DiagonalTicks}, " +
                $"climbingAllowed={allowClimbing}, vanillaResult={vanillaResult}, weightedResult={found}, " +
                $"expanded={diagnostic.ExpandedNodes}, acceptedOrdinary={diagnostic.AcceptedOrdinaryEdges}, " +
                $"climbCandidates={diagnostic.ClimbCandidates}, acceptedClimb={diagnostic.AcceptedClimbEdges}, " +
                $"routeLength={diagnostic.RouteLength}, selectedClimbs={diagnostic.SelectedClimbEdges}, " +
                $"movementCost={diagnostic.SelectedMovementTicks}, climbCost={diagnostic.SelectedClimbTicks}, " +
                $"totalCost={diagnostic.TotalTicks}.");
        }

        private void LogInfo(string message) => log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Extra Features {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private struct PathDiagnostic
        {
            public int CardinalTicks;
            public int DiagonalTicks;
            public int ExpandedNodes;
            public int AcceptedOrdinaryEdges;
            public int ClimbCandidates;
            public int AcceptedClimbEdges;
            public int RouteLength;
            public int SelectedClimbEdges;
            public int SelectedMovementTicks;
            public int SelectedClimbTicks;
            public int TotalTicks;
        }
    }
}
