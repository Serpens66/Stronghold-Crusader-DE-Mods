using System;
using System.Collections.Generic;

namespace APIShared
{
    /// <summary>One ordered perimeter contact and its outward direction.</summary>
    public readonly struct GatehouseFootprintCandidate
    {
        /// <summary>Creates a perimeter contact.</summary>
        public GatehouseFootprintCandidate(int x, int y, int outwardX, int outwardY)
        {
            X = x;
            Y = y;
            OutwardX = outwardX;
            OutwardY = outwardY;
        }

        /// <summary>Coordinate or outward direction of this contact.</summary>
        public int X { get; }
        /// <summary>Coordinate or outward direction of this contact.</summary>
        public int Y { get; }
        /// <summary>Coordinate or outward direction of this contact.</summary>
        public int OutwardX { get; }
        /// <summary>Coordinate or outward direction of this contact.</summary>
        public int OutwardY { get; }
    }


    /// <summary>Pure Vanilla gatehouse/drawbridge spatial coupling, independent of permissions and connection-record IDs.</summary>
    public static class GatehouseDrawbridgeCoupling
    {
        /// <summary>Native C5300 maximum number of coupled drawbridges.</summary>
        public const int MaximumCoupledDrawbridges = 2;

        /// <summary>Returns Vanilla B9330 perimeter order; callers validate the live footprint and map bounds.</summary>
        public static List<GatehouseFootprintCandidate> BuildOrderedFootprintCandidates(
            int originX,
            int originY,
            int occupyTileGridSize)
        {
            var candidates = new List<GatehouseFootprintCandidate>();
            if (occupyTileGridSize <= 0)
                return candidates;

            int midpoint = occupyTileGridSize / 2;
            for (int x = midpoint; x < occupyTileGridSize; x++)
            {
                candidates.Add(new GatehouseFootprintCandidate(
                    originX + x, originY - 1, 0, -1));
            }
            for (int y = 0; y < occupyTileGridSize; y++)
            {
                candidates.Add(new GatehouseFootprintCandidate(
                    originX + occupyTileGridSize, originY + y, 1, 0));
            }
            for (int x = occupyTileGridSize - 1; x >= 0; x--)
            {
                candidates.Add(new GatehouseFootprintCandidate(
                    originX + x, originY + occupyTileGridSize, 0, 1));
            }
            for (int y = occupyTileGridSize - 1; y >= 0; y--)
            {
                candidates.Add(new GatehouseFootprintCandidate(
                    originX - 1, originY + y, -1, 0));
            }
            for (int x = 0; x < midpoint; x++)
            {
                candidates.Add(new GatehouseFootprintCandidate(
                    originX + x, originY - 1, 0, -1));
            }

            return candidates;
        }

        /// <summary>Selects at most two distinct positive Game-IDs in native order. The caller supplies live drawbridge eligibility; no ownership or access policy is inferred.</summary>
        public static List<int> CollectFirstDistinctBuildingIds(
            IReadOnlyList<GatehouseFootprintCandidate> candidates,
            Func<int, int, int> getBuildingId,
            Func<int, bool> isEligibleDrawbridge)
        {
            var result = new List<int>(MaximumCoupledDrawbridges);
            if (candidates == null || getBuildingId == null || isEligibleDrawbridge == null)
                return result;

            for (int index = 0; index < candidates.Count; index++)
            {
                GatehouseFootprintCandidate candidate = candidates[index];
                int buildingId = getBuildingId(candidate.X, candidate.Y);
                if (buildingId <= 0 || result.Contains(buildingId) ||
                    !isEligibleDrawbridge(buildingId))
                {
                    continue;
                }

                result.Add(buildingId);
                if (result.Count == MaximumCoupledDrawbridges)
                    break;
            }

            return result;
        }

    }
}
