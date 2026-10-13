using APIShared.GateRouting.Native;
using System;

namespace BugfixesAndQoL.GateRouting
{
    internal static class GatePassageMask
    {
        internal static int ClearGatehouseOuterDirections(
            Func<int, int, int> tileId, int minX, int minY, int maxX, int maxY,
            int entryX, int entryY, int exitX, int exitY,
            bool horizontalPassage, byte[] masks, GateEdgeOwnership ownership, int gateId,
            out int firstFrom, out int firstTo, out int firstDirection,
            out int secondFrom, out int secondTo, out int secondDirection)
        {
            firstFrom = firstTo = firstDirection = -1;
            secondFrom = secondTo = secondDirection = -1;
            if (minX > maxX || minY > maxY) return 0;

            // Validate both complete boundaries before changing any mask byte.
            // Invalid coordinates must not publish a partially protected passage.
            int capacity = masks == null ? EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive : masks.Length;
            for (int coordinate = horizontalPassage ? minY : minX;
                coordinate <= (horizontalPassage ? maxY : maxX); coordinate++)
            {
                int a = horizontalPassage ? tileId(minX - 1, coordinate) : tileId(coordinate, minY - 1);
                int b = horizontalPassage ? tileId(minX, coordinate) : tileId(coordinate, minY);
                int c = horizontalPassage ? tileId(maxX, coordinate) : tileId(coordinate, maxY);
                int d = horizontalPassage ? tileId(maxX + 1, coordinate) : tileId(coordinate, maxY + 1);
                if ((uint)a >= (uint)capacity || (uint)b >= (uint)capacity ||
                    (uint)c >= (uint)capacity || (uint)d >= (uint)capacity) return 0;
            }
            int changed = 0;
            if (horizontalPassage)
            {
                if (entryY != exitY || entryY < minY || entryY > maxY ||
                    Math.Min(entryX, exitX) != minX - 1 ||
                    Math.Max(entryX, exitX) != maxX + 1) return 0;
                firstFrom = tileId(minX - 1, minY);
                firstTo = tileId(minX, minY); firstDirection = 2;
                secondFrom = tileId(maxX, minY);
                secondTo = tileId(maxX + 1, minY); secondDirection = 2;
                for (int y = minY; y <= maxY; y++)
                {
                    changed += ClearBoundary(tileId, masks, minX - 1, y, minX, y, 2, ownership, gateId);
                    changed += ClearBoundary(tileId, masks, maxX, y, maxX + 1, y, 2, ownership, gateId);
                }
            }
            else
            {
                if (entryX != exitX || entryX < minX || entryX > maxX ||
                    Math.Min(entryY, exitY) != minY - 1 ||
                    Math.Max(entryY, exitY) != maxY + 1) return 0;
                firstFrom = tileId(minX, minY - 1);
                firstTo = tileId(minX, minY); firstDirection = 4;
                secondFrom = tileId(minX, maxY);
                secondTo = tileId(minX, maxY + 1); secondDirection = 4;
                for (int x = minX; x <= maxX; x++)
                {
                    changed += ClearBoundary(tileId, masks, x, minY - 1, x, minY, 4, ownership, gateId);
                    changed += ClearBoundary(tileId, masks, x, maxY, x, maxY + 1, 4, ownership, gateId);
                }
            }
            return changed;
        }



        private static int ClearBoundary(
            Func<int, int, int> tileId, byte[] masks,
            int fromX, int fromY, int toX, int toY, int direction,
            GateEdgeOwnership ownership, int gateId)
        {
            int from = tileId(fromX, fromY);
            int to = tileId(toX, toY);
            int capacity = masks == null ? EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive : masks.Length;
            if (from < 0 || to < 0 || from >= capacity || to >= capacity)
                return 0;
            // TEMP_GATE_ROUTE_ACCEPTANCE: record the identical six boundary directions without a mask.
            if (masks == null)
            {
                ownership.Record(from, direction, gateId);
                ownership.Record(to, (direction + 4) & 7, gateId);
                ownership.Record(from, (direction + 7) & 7, gateId);
                ownership.Record(from, (direction + 1) & 7, gateId);
                ownership.Record(to, (direction + 3) & 7, gateId);
                ownership.Record(to, (direction + 5) & 7, gateId);
                return 0;
            }
            byte beforeFrom = masks[from];
            byte beforeTo = masks[to];
            masks[from] = unchecked((byte)(masks[from] & ~(1 << direction)));
            int opposite = (direction + 4) & 7;
            masks[to] = unchecked((byte)(masks[to] & ~(1 << opposite)));

            ownership.Record(from, direction, gateId);
            ownership.Record(to, opposite, gateId);
            // A diagonal crossing the same barrier would otherwise cut the corner.
            int sideDirectionA = horizontalDirection(direction - 1);
            int sideDirectionB = horizontalDirection(direction + 1);
            masks[from] = unchecked((byte)(masks[from] & ~(1 << sideDirectionA) & ~(1 << sideDirectionB)));
            masks[to] = unchecked((byte)(masks[to] &
                ~(1 << ((sideDirectionA + 4) & 7)) &
                ~(1 << ((sideDirectionB + 4) & 7))));
            ownership.Record(from, sideDirectionA, gateId);
            ownership.Record(from, sideDirectionB, gateId);
            ownership.Record(to, (sideDirectionA + 4) & 7, gateId);
            ownership.Record(to, (sideDirectionB + 4) & 7, gateId);
            return CountBits(unchecked((byte)(beforeFrom ^ masks[from]))) +
                CountBits(unchecked((byte)(beforeTo ^ masks[to])));
        }

        private static int CountBits(byte value)
        {
            int count = 0;
            while (value != 0)
            {
                value = unchecked((byte)(value & (value - 1)));
                count++;
            }
            return count;
        }

        private static int horizontalDirection(int direction) => (direction + 8) & 7;

    }
}
