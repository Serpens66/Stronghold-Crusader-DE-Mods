using System;
using System.Diagnostics;

namespace MoveMoatTest
{
    internal delegate bool FriendlyCompletedMoatPredicate(int playerId, int tileId);

    internal readonly struct WeightedMovementCostProfile : IEquatable<WeightedMovementCostProfile>
    {
        private const int MoatDelayPenalty = 6;

        private WeightedMovementCostProfile(
            int currentSpeed,
            int currentSpeed2,
            int speedBonus,
            int additionalSubsteps,
            int extraDelay,
            int moatPhase,
            int currentTerrainPenalty,
            int normalizedDelay,
            int cadenceProgress,
            bool startedOnCompletedMoat)
        {
            CurrentSpeed = currentSpeed;
            CurrentSpeed2 = currentSpeed2;
            SpeedBonus = speedBonus;
            AdditionalSubsteps = additionalSubsteps;
            ExtraDelay = extraDelay;
            MoatPhase = moatPhase;
            CurrentTerrainPenalty = currentTerrainPenalty;
            NormalizedDelay = normalizedDelay;
            CadenceProgress = cadenceProgress;
            StartedOnCompletedMoat = startedOnCompletedMoat;
        }

        public int CurrentSpeed { get; }
        public int CurrentSpeed2 { get; }
        public int SpeedBonus { get; }
        public int AdditionalSubsteps { get; }
        public int ExtraDelay { get; }
        public int MoatPhase { get; }
        public int CurrentTerrainPenalty { get; }
        public int NormalizedDelay { get; }
        public int CadenceProgress { get; }
        public bool StartedOnCompletedMoat { get; }

        public long GroundEdgeFixedCost => GetEdgeFixedCost(false);

        public static bool TryCreate(
            int currentSpeed,
            int currentSpeed2,
            int speedBonus,
            int additionalSubsteps,
            int extraDelay,
            int moatPhase,
            bool currentTileIsCompletedMoat,
            out WeightedMovementCostProfile profile,
            out string rejectionReason)
        {
            profile = new WeightedMovementCostProfile(
                currentSpeed,
                currentSpeed2,
                speedBonus,
                additionalSubsteps,
                extraDelay,
                moatPhase,
                0,
                currentSpeed2,
                0,
                currentTileIsCompletedMoat);
            rejectionReason = null;
            if (currentSpeed < 0 || currentSpeed2 < 0 || speedBonus < 0 ||
                additionalSubsteps < 0 || extraDelay < 0)
            {
                rejectionReason = "negative-runtime-speed-field";
                return false;
            }
            if (moatPhase < 0 || moatPhase > 24 || (moatPhase & 3) != 0)
            {
                rejectionReason = "invalid-moat-slowdown-phase";
                return false;
            }

            // 0x19B260 stores the terrain-adjusted delay in CurrentSpeed2. Its phase
            // lets us remove only an unambiguous active +3/+4/+6 terrain component.
            int terrainPenalty = 0;
            if (moatPhase != 0)
            {
                terrainPenalty = currentTileIsCompletedMoat
                    ? moatPhase < 5 ? 3 : moatPhase < 10 ? 4 : 6
                    : 3;
            }
            int normalizedDelay = currentSpeed2 - terrainPenalty;
            if (normalizedDelay < 0 || speedBonus == int.MaxValue)
            {
                rejectionReason = "inconsistent-runtime-speed-fields";
                return false;
            }

            if (speedBonus > int.MaxValue - additionalSubsteps - 1)
            {
                rejectionReason = "runtime-speed-overflow";
                return false;
            }
            int cadenceProgress = speedBonus + additionalSubsteps + 1;
            long groundInterval = (long)normalizedDelay + speedBonus + extraDelay + 1L;
            long moatInterval = groundInterval + MoatDelayPenalty;
            if (cadenceProgress <= 0 || groundInterval <= 0 || moatInterval <= 0 ||
                moatInterval > long.MaxValue / 8L)
            {
                rejectionReason = "runtime-speed-overflow";
                return false;
            }

            profile = new WeightedMovementCostProfile(
                currentSpeed,
                currentSpeed2,
                speedBonus,
                additionalSubsteps,
                extraDelay,
                moatPhase,
                terrainPenalty,
                normalizedDelay,
                cadenceProgress,
                currentTileIsCompletedMoat);
            return true;
        }

        public long GetEdgeFixedCost(bool moatEdge)
        {
            long delay = NormalizedDelay + (moatEdge ? MoatDelayPenalty : 0L);
            return 8L * (delay + SpeedBonus + ExtraDelay + 1L);
        }

        public long ConvertFixedCostToTicks(long fixedCost)
        {
            if (fixedCost < 0 || CadenceProgress <= 0)
                return long.MaxValue;
            if (fixedCost == 0)
                return 0;
            return fixedCost > long.MaxValue - (CadenceProgress - 1L)
                ? long.MaxValue
                : (fixedCost + CadenceProgress - 1L) / CadenceProgress;
        }

        public long EstimateRouteTicks(int groundEdges, int moatEdges)
        {
            if (groundEdges < 0 || moatEdges < 0)
                return long.MaxValue;
            long groundCost = GetEdgeFixedCost(false);
            long moatCost = GetEdgeFixedCost(true);
            if ((groundEdges != 0 && groundCost > long.MaxValue / groundEdges) ||
                (moatEdges != 0 && moatCost > long.MaxValue / moatEdges))
            {
                return long.MaxValue;
            }
            long fixedCost = groundCost * groundEdges;
            long moatFixedCost = moatCost * moatEdges;
            if (fixedCost > long.MaxValue - moatFixedCost)
                return long.MaxValue;
            return ConvertFixedCostToTicks(fixedCost + moatFixedCost);
        }

        public bool HasSameNormalizedCadence(WeightedMovementCostProfile other) =>
            SpeedBonus == other.SpeedBonus &&
            AdditionalSubsteps == other.AdditionalSubsteps &&
            ExtraDelay == other.ExtraDelay &&
            NormalizedDelay == other.NormalizedDelay &&
            CadenceProgress == other.CadenceProgress;

        public bool Equals(WeightedMovementCostProfile other) =>
            CurrentSpeed == other.CurrentSpeed &&
            CurrentSpeed2 == other.CurrentSpeed2 &&
            SpeedBonus == other.SpeedBonus &&
            AdditionalSubsteps == other.AdditionalSubsteps &&
            ExtraDelay == other.ExtraDelay &&
            MoatPhase == other.MoatPhase &&
            CurrentTerrainPenalty == other.CurrentTerrainPenalty &&
            NormalizedDelay == other.NormalizedDelay &&
            CadenceProgress == other.CadenceProgress &&
            StartedOnCompletedMoat == other.StartedOnCompletedMoat;

        public override bool Equals(object obj) =>
            obj is WeightedMovementCostProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CurrentSpeed;
                hash = hash * 397 ^ CurrentSpeed2;
                hash = hash * 397 ^ SpeedBonus;
                hash = hash * 397 ^ AdditionalSubsteps;
                hash = hash * 397 ^ ExtraDelay;
                hash = hash * 397 ^ MoatPhase;
                hash = hash * 397 ^ CurrentTerrainPenalty;
                hash = hash * 397 ^ NormalizedDelay;
                hash = hash * 397 ^ CadenceProgress;
                return hash * 397 ^ StartedOnCompletedMoat.GetHashCode();
            }
        }
    }

    internal sealed unsafe class WeightedMoatRoutePlanner
    {
        internal const int MaximumRouteEdges = 2000;

        private const int MapWidth = 800;
        private const int CoordinateCount = MapWidth * MapWidth;
        private const int NativeTileCount = 0x4E520;
        private const uint CompletedMoatTileFlag = 0x40000000;
        private const uint OrdinaryWalkableTileFlag = 0x00008000;
        private const uint WallOrStairMask = 0x00010900;
        private const uint MoatReconstructionBlockingMask = 0x0A5014B1;

        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly byte* validCoordinates;
        private readonly int* rowLookup;
        private readonly uint* tileFlags;
        private readonly ushort* buildingLayer;
        private readonly byte* heightLayer;
        private readonly byte* occupancyLayer;
        private readonly byte* directionMasks;
        private readonly FriendlyCompletedMoatPredicate isFriendlyCompletedMoat;

        private readonly long[] costs = new long[CoordinateCount];
        private readonly int[] parents = new int[CoordinateCount];
        private readonly int[] insertionOrder = new int[CoordinateCount];
        private readonly int[] heap = new int[CoordinateCount];
        private readonly int[] heapPositions = new int[CoordinateCount];
        private readonly int[] moatEdges = new int[CoordinateCount];
        private readonly int[] edgeCounts = new int[CoordinateCount];
        private readonly int[] touched = new int[CoordinateCount];
        private readonly int[] route = new int[MaximumRouteEdges + 1];
        private readonly byte[] moatClassification = new byte[NativeTileCount];
        private readonly int[] classifiedTiles = new int[NativeTileCount];

        private int heapCount;
        private int touchedCount;
        private int classifiedCount;
        private int nextInsertionOrder;
        private int targetX;
        private int targetY;
        private long groundEdgeFixedCost;

        public WeightedMoatRoutePlanner(
            byte* validCoordinates,
            int* rowLookup,
            uint* tileFlags,
            ushort* buildingLayer,
            byte* heightLayer,
            byte* occupancyLayer,
            byte* directionMasks,
            FriendlyCompletedMoatPredicate isFriendlyCompletedMoat)
        {
            this.validCoordinates = validCoordinates;
            this.rowLookup = rowLookup;
            this.tileFlags = tileFlags;
            this.buildingLayer = buildingLayer;
            this.heightLayer = heightLayer;
            this.occupancyLayer = occupancyLayer;
            this.directionMasks = directionMasks;
            this.isFriendlyCompletedMoat = isFriendlyCompletedMoat ??
                throw new ArgumentNullException(nameof(isFriendlyCompletedMoat));

            for (int node = 0; node < CoordinateCount; node++)
            {
                costs[node] = long.MaxValue;
                parents[node] = -1;
                heapPositions[node] = -1;
                moatEdges[node] = int.MaxValue;
                edgeCounts[node] = int.MaxValue;
            }
        }

        public bool TryBuild(
            int playerId,
            int startX,
            int startY,
            int requestedTargetX,
            int requestedTargetY,
            WeightedMovementCostProfile costProfile,
            bool allowReservedTarget,
            out WeightedMoatRouteSummary summary)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            summary = default;
            ResetSearch();
            try
            {
                if (!IsValidCoordinate(startX, startY) ||
                    !IsValidCoordinate(requestedTargetX, requestedTargetY))
                {
                    summary = WeightedMoatRouteSummary.Failed(
                        "invalid-coordinate", stopwatch.Elapsed.TotalMilliseconds);
                    return false;
                }

                int startTile = GetTileId(startX, startY);
                int requestedTargetTile = GetTileId(requestedTargetX, requestedTargetY);
                if (!IsNativeTile(startTile) || !IsNativeTile(requestedTargetTile))
                {
                    summary = WeightedMoatRouteSummary.Failed(
                        "invalid-tile", stopwatch.Elapsed.TotalMilliseconds);
                    return false;
                }
                if (IsCompletedMoat(startTile) && !IsFriendlyMoat(playerId, startTile) ||
                    IsCompletedMoat(requestedTargetTile) &&
                    !IsFriendlyMoat(playerId, requestedTargetTile))
                {
                    summary = WeightedMoatRouteSummary.Failed(
                        "enemy-or-invalid-moat-endpoint", stopwatch.Elapsed.TotalMilliseconds);
                    return false;
                }

                targetX = requestedTargetX;
                targetY = requestedTargetY;
                groundEdgeFixedCost = costProfile.GroundEdgeFixedCost;
                int startNode = GetNode(startX, startY);
                int targetNode = GetNode(targetX, targetY);
                Touch(startNode, 0, -1, 0, 0);
                Push(startNode);

                int expanded = 0;
                while (heapCount > 0 && expanded < CoordinateCount)
                {
                    int currentNode = Pop();
                    expanded++;
                    if (currentNode == targetNode)
                    {
                        if (!TrySummarizeRoute(
                            playerId, startNode, targetNode, allowReservedTarget,
                            costProfile, expanded, stopwatch, out summary))
                        {
                            return false;
                        }
                        return true;
                    }

                    int currentX = currentNode % MapWidth;
                    int currentY = currentNode / MapWidth;
                    int currentTile = GetTileId(currentX, currentY);
                    if (!IsNativeTile(currentTile))
                        continue;

                    for (int direction = 0; direction < DirectionX.Length; direction++)
                    {
                        int nextX = currentX + DirectionX[direction];
                        int nextY = currentY + DirectionY[direction];
                        if (!IsValidCoordinate(nextX, nextY))
                            continue;

                        int nextTile = GetTileId(nextX, nextY);
                        if (!IsNativeTile(nextTile))
                            continue;
                        bool targetEndpoint = nextX == targetX && nextY == targetY;
                        if (!TryGetEdge(
                            playerId, currentX, currentY, currentTile, nextX, nextY,
                            nextTile, direction, targetEndpoint, allowReservedTarget,
                            out bool moatEdge))
                        {
                            continue;
                        }

                        int nextNode = GetNode(nextX, nextY);
                        int newEdgeCount = edgeCounts[currentNode] + 1;
                        if (newEdgeCount > MaximumRouteEdges)
                            continue;
                        long edgeFixedCost = costProfile.GetEdgeFixedCost(moatEdge);
                        long newCost = costs[currentNode] > long.MaxValue - edgeFixedCost
                            ? long.MaxValue
                            : costs[currentNode] + edgeFixedCost;
                        int newMoatEdges = moatEdges[currentNode] == int.MaxValue
                            ? int.MaxValue
                            : moatEdges[currentNode] + (moatEdge ? 1 : 0);
                        if (newCost > costs[nextNode] ||
                            newCost == costs[nextNode] && newMoatEdges >= moatEdges[nextNode])
                        {
                            continue;
                        }

                        if (costs[nextNode] == long.MaxValue)
                            Touch(nextNode, newCost, currentNode, newMoatEdges, newEdgeCount);
                        else
                        {
                            costs[nextNode] = newCost;
                            parents[nextNode] = currentNode;
                            moatEdges[nextNode] = newMoatEdges;
                            edgeCounts[nextNode] = newEdgeCount;
                        }
                        PushOrDecrease(nextNode);
                    }
                }

                summary = WeightedMoatRouteSummary.Failed(
                    heapCount == 0 ? "unreachable" : "node-limit",
                    stopwatch.Elapsed.TotalMilliseconds,
                    expanded);
                return false;
            }
            catch
            {
                summary = WeightedMoatRouteSummary.Failed(
                    "exception", stopwatch.Elapsed.TotalMilliseconds);
                throw;
            }
            finally
            {
                ResetClassifications();
            }
        }

        public bool TryDescribeEncodedPath(
            int playerId,
            int startX,
            int startY,
            int expectedTargetX,
            int expectedTargetY,
            WeightedMovementCostProfile costProfile,
            byte* encodedDirections,
            int directionCount,
            bool allowReservedTarget,
            out WeightedMoatRouteSummary summary)
        {
            summary = default;
            ResetClassifications();
            try
            {
                if (encodedDirections == null || directionCount < 0 ||
                    directionCount > MaximumRouteEdges || !IsValidCoordinate(startX, startY))
                {
                    summary = WeightedMoatRouteSummary.Failed("invalid-native-path", 0);
                    return false;
                }

                int x = startX;
                int y = startY;
                int ground = 0;
                int moat = 0;
                int diagonal = 0;
                long fixedCost = 0;
                for (int index = 0; index < directionCount; index++)
                {
                    // 0xE1640 first records target -> start, then 0xE4E90 reverses the
                    // complete nibble sequence. The published buffer is start -> target.
                    int bufferIndex = index;
                    int direction =
                        (encodedDirections[bufferIndex >> 1] >> ((bufferIndex & 1) * 4)) & 0x0F;
                    if (direction >= DirectionX.Length)
                    {
                        summary = WeightedMoatRouteSummary.Failed("invalid-direction-nibble", 0);
                        return false;
                    }
                    int nextX = x + DirectionX[direction];
                    int nextY = y + DirectionY[direction];
                    if (!IsValidCoordinate(nextX, nextY))
                    {
                        summary = WeightedMoatRouteSummary.Failed("native-path-out-of-bounds", 0);
                        return false;
                    }
                    int currentTile = GetTileId(x, y);
                    int nextTile = GetTileId(nextX, nextY);
                    bool targetEndpoint = index == directionCount - 1;
                    if (!TryGetEdge(
                        playerId, x, y, currentTile, nextX, nextY, nextTile,
                        direction, targetEndpoint, allowReservedTarget, out bool moatEdge))
                    {
                        summary = WeightedMoatRouteSummary.Failed("native-edge-invalid", 0);
                        return false;
                    }

                    long edgeFixedCost = costProfile.GetEdgeFixedCost(moatEdge);
                    fixedCost = fixedCost > long.MaxValue - edgeFixedCost
                        ? long.MaxValue
                        : fixedCost + edgeFixedCost;
                    if (moatEdge)
                        moat++;
                    else
                        ground++;
                    if ((direction & 1) != 0)
                        diagonal++;
                    x = nextX;
                    y = nextY;
                }

                if (x != expectedTargetX || y != expectedTargetY)
                {
                    summary = WeightedMoatRouteSummary.Failed(
                        $"native-endpoint-mismatch-actual-{x}-{y}", 0,
                        routeLength: directionCount);
                    return false;
                }

                summary = WeightedMoatRouteSummary.Succeeded(
                    directionCount, ground, moat, diagonal,
                    costProfile.ConvertFixedCostToTicks(fixedCost), 0, 0);
                return true;
            }
            finally
            {
                ResetClassifications();
            }
        }

        private bool TrySummarizeRoute(
            int playerId,
            int startNode,
            int targetNode,
            bool allowReservedTarget,
            WeightedMovementCostProfile costProfile,
            int expanded,
            Stopwatch stopwatch,
            out WeightedMoatRouteSummary summary)
        {
            int routeNodes = 0;
            int node = targetNode;
            while (node >= 0 && routeNodes < route.Length)
            {
                route[routeNodes++] = node;
                if (node == startNode)
                    break;
                node = parents[node];
            }
            if (routeNodes == 0 || routeNodes > MaximumRouteEdges + 1 ||
                route[routeNodes - 1] != startNode)
            {
                summary = WeightedMoatRouteSummary.Failed(
                    "route-over-2000-edges", stopwatch.Elapsed.TotalMilliseconds, expanded);
                return false;
            }

            int ground = 0;
            int moat = 0;
            int diagonal = 0;
            for (int index = routeNodes - 1; index > 0; index--)
            {
                int currentNode = route[index];
                int nextNode = route[index - 1];
                int currentX = currentNode % MapWidth;
                int currentY = currentNode / MapWidth;
                int nextX = nextNode % MapWidth;
                int nextY = nextNode / MapWidth;
                int direction = FindDirection(nextX - currentX, nextY - currentY);
                bool targetEndpoint = index == 1;
                if (direction < 0 || !TryGetEdge(
                    playerId, currentX, currentY, GetTileId(currentX, currentY),
                    nextX, nextY, GetTileId(nextX, nextY), direction,
                    targetEndpoint, allowReservedTarget, out bool moatEdge))
                {
                    summary = WeightedMoatRouteSummary.Failed(
                        "e1640-edge-validation-failed", stopwatch.Elapsed.TotalMilliseconds,
                        expanded, routeNodes - 1);
                    return false;
                }
                if (moatEdge)
                    moat++;
                else
                    ground++;
                if ((direction & 1) != 0)
                    diagonal++;
            }

            summary = WeightedMoatRouteSummary.Succeeded(
                routeNodes - 1, ground, moat, diagonal,
                costProfile.ConvertFixedCostToTicks(costs[targetNode]),
                stopwatch.Elapsed.TotalMilliseconds, expanded);
            return true;
        }

        private bool TryGetEdge(
            int playerId,
            int currentX,
            int currentY,
            int currentTile,
            int nextX,
            int nextY,
            int nextTile,
            int direction,
            bool targetEndpoint,
            bool allowReservedTarget,
            out bool moatEdge)
        {
            moatEdge = false;
            if (!IsNativeTile(currentTile) || !IsNativeTile(nextTile))
                return false;

            bool currentMoat = IsCompletedMoat(currentTile);
            bool nextMoat = IsCompletedMoat(nextTile);
            if (currentMoat && !IsFriendlyMoat(playerId, currentTile) ||
                nextMoat && !IsFriendlyMoat(playerId, nextTile))
            {
                return false;
            }

            bool ordinaryEdge = (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
            if (!currentMoat && !nextMoat)
            {
                if (!ordinaryEdge)
                    return false;
                if ((tileFlags[nextTile] & WallOrStairMask) != 0)
                    return false;
                if (buildingLayer[nextTile] != 0 && !(targetEndpoint && allowReservedTarget))
                    return false;
                return true;
            }

            moatEdge = true;
            if (ordinaryEdge)
            {
                if (((tileFlags[currentTile] | tileFlags[nextTile]) & WallOrStairMask) != 0)
                    return false;
                return buildingLayer[nextTile] == 0 ||
                    targetEndpoint && allowReservedTarget;
            }
            if (Math.Abs(heightLayer[nextTile] - heightLayer[currentTile]) > 16)
                return false;
            if (buildingLayer[nextTile] != 0 &&
                !(targetEndpoint && allowReservedTarget && ordinaryEdge))
            {
                return false;
            }
            if (!IsUsableMoatAdjacentTile(currentTile, false) ||
                !IsUsableMoatAdjacentTile(nextTile, targetEndpoint && allowReservedTarget))
            {
                return false;
            }
            if ((direction & 1) != 0 &&
                !IsValidMoatDiagonal(playerId, currentX, currentY, nextX, nextY))
            {
                return false;
            }
            return true;
        }

        private bool IsValidMoatDiagonal(
            int playerId, int currentX, int currentY, int nextX, int nextY)
        {
            int firstTile = GetTileId(nextX, currentY);
            int secondTile = GetTileId(currentX, nextY);
            return IsValidCoordinate(nextX, currentY) &&
                IsValidCoordinate(currentX, nextY) &&
                IsDiagonalCornerUsable(playerId, firstTile) &&
                IsDiagonalCornerUsable(playerId, secondTile);
        }

        private bool IsDiagonalCornerUsable(int playerId, int tileId)
        {
            if (!IsNativeTile(tileId))
                return false;
            if (IsCompletedMoat(tileId))
                return IsFriendlyMoat(playerId, tileId);
            return IsUsableMoatAdjacentTile(tileId, false);
        }

        private bool IsUsableMoatAdjacentTile(int tileId, bool allowReservedEndpoint)
        {
            if (IsCompletedMoat(tileId))
                return true;
            uint flags = tileFlags[tileId];
            if ((flags & OrdinaryWalkableTileFlag) == 0 ||
                (flags & MoatReconstructionBlockingMask) != 0 ||
                (flags & WallOrStairMask) != 0)
                return false;
            return buildingLayer[tileId] == 0 || allowReservedEndpoint;
        }

        private bool IsFriendlyMoat(int playerId, int tileId)
        {
            byte state = moatClassification[tileId];
            if (state == 0)
            {
                state = isFriendlyCompletedMoat(playerId, tileId) ? (byte)1 : (byte)2;
                moatClassification[tileId] = state;
                classifiedTiles[classifiedCount++] = tileId;
            }
            return state == 1;
        }

        private bool IsCompletedMoat(int tileId) =>
            (tileFlags[tileId] & CompletedMoatTileFlag) != 0;

        private int GetTileId(int x, int y) => rowLookup[y * 3] + x;

        private static int GetNode(int x, int y) => y * MapWidth + x;

        private bool IsValidCoordinate(int x, int y) =>
            x >= 0 && x < MapWidth && y >= 0 && y < MapWidth &&
            validCoordinates[GetNode(x, y)] != 0;

        private static bool IsNativeTile(int tileId) =>
            tileId >= 0 && tileId < NativeTileCount;

        private long Heuristic(int node)
        {
            int x = node % MapWidth;
            int y = node / MapWidth;
            int dx = Math.Abs(targetX - x);
            int dy = Math.Abs(targetY - y);
            // Both cardinal and diagonal tile changes use the same native cadence.
            return (long)Math.Max(dx, dy) * groundEdgeFixedCost;
        }

        private void Touch(
            int node, long cost, int parent, int moatEdgeCount, int edgeCount)
        {
            touched[touchedCount++] = node;
            costs[node] = cost;
            parents[node] = parent;
            moatEdges[node] = moatEdgeCount;
            edgeCounts[node] = edgeCount;
            insertionOrder[node] = nextInsertionOrder++;
            heapPositions[node] = -1;
        }

        private void Push(int node)
        {
            int index = heapCount++;
            heap[index] = node;
            heapPositions[node] = index;
            SiftUp(index);
        }

        private void PushOrDecrease(int node)
        {
            int position = heapPositions[node];
            if (position < 0)
                Push(node);
            else
                SiftUp(position);
        }

        private int Pop()
        {
            int result = heap[0];
            heapPositions[result] = -1;
            heapCount--;
            if (heapCount > 0)
            {
                heap[0] = heap[heapCount];
                heapPositions[heap[0]] = 0;
                SiftDown(0);
            }
            return result;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (!ComesBefore(heap[index], heap[parent]))
                    break;
                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= heapCount)
                    return;
                int right = left + 1;
                int best = right < heapCount && ComesBefore(heap[right], heap[left])
                    ? right
                    : left;
                if (!ComesBefore(heap[best], heap[index]))
                    return;
                Swap(index, best);
                index = best;
            }
        }

        private bool ComesBefore(int left, int right)
        {
            long leftF = costs[left] > long.MaxValue - Heuristic(left)
                ? long.MaxValue
                : costs[left] + Heuristic(left);
            long rightF = costs[right] > long.MaxValue - Heuristic(right)
                ? long.MaxValue
                : costs[right] + Heuristic(right);
            if (leftF != rightF)
                return leftF < rightF;
            if (costs[left] != costs[right])
                return costs[left] < costs[right];
            if (moatEdges[left] != moatEdges[right])
                return moatEdges[left] < moatEdges[right];
            return insertionOrder[left] < insertionOrder[right];
        }

        private void Swap(int left, int right)
        {
            int node = heap[left];
            heap[left] = heap[right];
            heap[right] = node;
            heapPositions[heap[left]] = left;
            heapPositions[heap[right]] = right;
        }

        private void ResetSearch()
        {
            for (int index = 0; index < touchedCount; index++)
            {
                int node = touched[index];
                costs[node] = long.MaxValue;
                parents[node] = -1;
                moatEdges[node] = int.MaxValue;
                edgeCounts[node] = int.MaxValue;
                heapPositions[node] = -1;
            }
            touchedCount = 0;
            heapCount = 0;
            nextInsertionOrder = 0;
            ResetClassifications();
        }

        private void ResetClassifications()
        {
            for (int index = 0; index < classifiedCount; index++)
                moatClassification[classifiedTiles[index]] = 0;
            classifiedCount = 0;
        }

        private static int FindDirection(int dx, int dy)
        {
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                if (DirectionX[direction] == dx && DirectionY[direction] == dy)
                    return direction;
            }
            return -1;
        }
    }

    internal readonly struct WeightedMoatRouteSummary
    {
        private WeightedMoatRouteSummary(
            bool found,
            string reason,
            int routeLength,
            int groundEdges,
            int moatEdges,
            int diagonalEdges,
            long estimatedTicks,
            double searchMilliseconds,
            int expandedNodes)
        {
            Found = found;
            Reason = reason;
            RouteLength = routeLength;
            GroundEdges = groundEdges;
            MoatEdges = moatEdges;
            DiagonalEdges = diagonalEdges;
            EstimatedTicks = estimatedTicks;
            SearchMilliseconds = searchMilliseconds;
            ExpandedNodes = expandedNodes;
        }

        public bool Found { get; }
        public string Reason { get; }
        public int RouteLength { get; }
        public int GroundEdges { get; }
        public int MoatEdges { get; }
        public int DiagonalEdges { get; }
        public long EstimatedTicks { get; }
        public double SearchMilliseconds { get; }
        public int ExpandedNodes { get; }

        public static WeightedMoatRouteSummary Succeeded(
            int routeLength,
            int groundEdges,
            int moatEdges,
            int diagonalEdges,
            long estimatedTicks,
            double searchMilliseconds,
            int expandedNodes) =>
            new WeightedMoatRouteSummary(
                true, "none", routeLength, groundEdges, moatEdges, diagonalEdges,
                estimatedTicks, searchMilliseconds, expandedNodes);

        public static WeightedMoatRouteSummary Failed(
            string reason,
            double searchMilliseconds,
            int expandedNodes = 0,
            int routeLength = 0) =>
            new WeightedMoatRouteSummary(
                false, reason, routeLength, 0, 0, 0, 0,
                searchMilliseconds, expandedNodes);
    }
}
