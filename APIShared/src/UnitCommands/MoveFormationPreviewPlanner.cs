using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace APIShared.UnitCommands
{
    internal readonly struct MoveFormationDestination
    {
        internal MoveFormationDestination(int tileId, int x, int y)
        {
            TileId = tileId;
            X = x;
            Y = y;
        }

        internal int TileId { get; }
        internal int X { get; }
        internal int Y { get; }
    }

    internal readonly struct MoveFormationPlanMetrics
    {
        internal MoveFormationPlanMetrics(
            int visitedTiles,
            int exactDestinations,
            int relaxedDestinations,
            int reusedDestinations,
            int uniqueDestinations)
        {
            VisitedTiles = visitedTiles;
            ExactDestinations = exactDestinations;
            RelaxedDestinations = relaxedDestinations;
            ReusedDestinations = reusedDestinations;
            UniqueDestinations = uniqueDestinations;
        }

        internal int VisitedTiles { get; }
        internal int ExactDestinations { get; }
        internal int RelaxedDestinations { get; }
        internal int ReusedDestinations { get; }
        internal int UniqueDestinations { get; }
    }

    internal sealed class MoveFormationPreviewPlanner
    {
        internal const int MapWidth = 800;
        internal const int NativeTileCapacity = GameTileManagerView.NativePackedTileCapacity;
        internal const int MaximumFormationDistance = 4000;
        internal const int AssassinUnitType = 0x49;

        internal readonly int[] visitStamp = new int[NativeTileCapacity];
        internal readonly int[] queueTile = new int[NativeTileCapacity];
        internal readonly short[] queueX = new short[NativeTileCapacity];
        internal readonly short[] queueY = new short[NativeTileCapacity];
        internal readonly short[] queueDistance = new short[NativeTileCapacity];
        internal readonly int[] relaxedQueueIndices = new int[NativeTileCapacity];
        internal readonly Func<int, int, bool> targetAvailable;
        internal int stamp;

        internal MoveFormationPreviewPlanner(Func<int, int, bool> targetAvailable)
        {
            this.targetAvailable = targetAvailable ??
                throw new ArgumentNullException(nameof(targetAvailable));
        }

        internal MoveFormationPlanMetrics Plan(
            int anchorX,
            int anchorY,
            int spacing,
            int requiredCount,
            int[] selectedUnitTypes,
            List<MoveFormationDestination> destination,
            Func<int, int, bool> additionalCandidateFilter = null)
        {
            return Plan(
                anchorX,
                anchorY,
                spacing,
                requiredCount,
                IsAssassinOnly(selectedUnitTypes),
                destination,
                additionalCandidateFilter);
        }

        internal MoveFormationPlanMetrics Plan(
            int anchorX,
            int anchorY,
            int spacing,
            int requiredCount,
            bool assassinOnly,
            List<MoveFormationDestination> destination,
            Func<int, int, bool> additionalCandidateFilter = null)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            if (requiredCount <= 0)
                return default;

            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager ??
                throw new InvalidOperationException("The native tile-manager view is unavailable.");
            Span<byte> edgeMasks = tileManager.PathEdgeMaskGrid;
            Span<ushort> components = tileManager.PathConnectionGrid;
            Span<int> logic = tileManager.LogicGrid;
            if (edgeMasks.Length < NativeTileCapacity ||
                components.Length < NativeTileCapacity)
            {
                throw new InvalidOperationException(
                    "The native formation-preview grids have an unexpected capacity.");
            }

            int anchorTile = GameTileManagerAPI.Instance.GetTileId(anchorX, anchorY);
            if (!IsCoordinateValid(anchorX, anchorY) ||
                !targetAvailable(anchorX, anchorY) ||
                (uint)anchorTile >= (uint)components.Length ||
                components[anchorTile] == 0)
            {
                throw new InvalidOperationException("The formation-preview anchor is not walkable.");
            }

            int currentStamp = NextStamp();
            int read = 0;
            int write = 0;
            int relaxedWrite = 0;
            Enqueue(anchorTile, anchorX, anchorY, 1, currentStamp, ref write);
            ushort anchorComponent = components[anchorTile];
            int normalizedSpacing = MoveFormationSpacingPolicy.Normalize(spacing);

            while (read < write && destination.Count < requiredCount)
            {
                int queueIndex = read;
                int tileId = queueTile[read];
                int x = queueX[read];
                int y = queueY[read];
                int distance = queueDistance[read];
                read++;

                bool candidateAllowed = distance > 0 &&
                    distance < MaximumFormationDistance &&
                    targetAvailable(x, y) &&
                    (!assassinOnly ||
                     ((uint)tileId < (uint)logic.Length &&
                      (logic[tileId] & 0x10000100) == 0)) &&
                    (additionalCandidateFilter == null ||
                     additionalCandidateFilter(x, y));
                if (candidateAllowed)
                {
                    if ((Math.Abs(x - anchorX) + Math.Abs(y - anchorY)) %
                        normalizedSpacing == 0)
                    {
                        destination.Add(new MoveFormationDestination(tileId, x, y));
                        if (destination.Count >= requiredCount)
                            break;
                    }
                    else
                    {
                        relaxedQueueIndices[relaxedWrite++] = queueIndex;
                    }
                }

                if (distance >= MaximumFormationDistance - 2)
                    continue;

                byte mask = edgeMasks[tileId];
                TryEnqueue(x - 1, y, distance + 1, 0x40, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x + 1, y, distance + 1, 0x04, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x, y - 1, distance + 1, 0x01, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x - 1, y - 1, distance + 2, 0x80, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x + 1, y - 1, distance + 2, 0x02, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x, y + 1, distance + 1, 0x10, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x - 1, y + 1, distance + 2, 0x20, mask, anchorComponent,
                    components, currentStamp, ref write);
                TryEnqueue(x + 1, y + 1, distance + 2, 0x08, mask, anchorComponent,
                    components, currentStamp, ref write);
            }

            int exactCount = destination.Count;
            for (int index = 0;
                index < relaxedWrite && destination.Count < requiredCount;
                index++)
            {
                int queueIndex = relaxedQueueIndices[index];
                destination.Add(new MoveFormationDestination(
                    queueTile[queueIndex], queueX[queueIndex], queueY[queueIndex]));
            }
            int uniqueCount = destination.Count;
            if (uniqueCount == 0)
                throw new InvalidOperationException(
                    "The formation planner found no valid destination tile.");

            int reuseIndex = 0;
            while (destination.Count < requiredCount)
            {
                destination.Add(destination[reuseIndex]);
                reuseIndex++;
                if (reuseIndex >= uniqueCount)
                    reuseIndex = 0;
            }

            return new MoveFormationPlanMetrics(
                write,
                exactCount,
                uniqueCount - exactCount,
                destination.Count - uniqueCount,
                uniqueCount);
        }

        internal void TryEnqueue(
            int x,
            int y,
            int distance,
            byte requiredMask,
            byte sourceMask,
            ushort anchorComponent,
            Span<ushort> components,
            int currentStamp,
            ref int write)
        {
            if ((sourceMask & requiredMask) == 0 || !IsCoordinateValid(x, y))
                return;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)tileId >= (uint)components.Length ||
                components[tileId] != anchorComponent ||
                visitStamp[tileId] == currentStamp)
                return;
            Enqueue(tileId, x, y, distance, currentStamp, ref write);
        }

        internal void Enqueue(
            int tileId, int x, int y, int distance, int currentStamp, ref int write)
        {
            if ((uint)write >= (uint)queueTile.Length)
                return;
            visitStamp[tileId] = currentStamp;
            queueTile[write] = tileId;
            queueX[write] = checked((short)x);
            queueY[write] = checked((short)y);
            queueDistance[write] = checked((short)distance);
            write++;
        }

        internal int NextStamp()
        {
            stamp++;
            if (stamp != 0)
                return stamp;
            Array.Clear(visitStamp, 0, visitStamp.Length);
            stamp = 1;
            return stamp;
        }

        internal static bool IsCoordinateValid(int x, int y) =>
            (uint)x < MapWidth && (uint)y < MapWidth;

        internal static bool IsAssassinOnly(int[] selectedUnitTypes)
        {
            if (selectedUnitTypes == null || selectedUnitTypes.Length == 0)
                return false;
            for (int index = 0; index < selectedUnitTypes.Length; index++)
            {
                if (selectedUnitTypes[index] != AssassinUnitType)
                    return false;
            }
            return true;
        }
    }
}
