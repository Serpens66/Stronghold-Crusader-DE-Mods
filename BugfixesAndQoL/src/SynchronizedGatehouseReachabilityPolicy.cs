// Feature: Mirror Vanilla's footprint-edge lookup for gatehouse-coupled drawbridges.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct VanillaFootprintCandidate
    {
        internal VanillaFootprintCandidate(int x, int y, int outwardX, int outwardY)
        {
            X = x;
            Y = y;
            OutwardX = outwardX;
            OutwardY = outwardY;
        }

        internal int X { get; }
        internal int Y { get; }
        internal int OutwardX { get; }
        internal int OutwardY { get; }
    }

    internal readonly struct DrawbridgeApproachSnapshot
    {
        internal DrawbridgeApproachSnapshot(
            int contactX,
            int contactY,
            int exteriorX,
            int exteriorY,
            int exteriorTileId,
            int exteriorPcl)
        {
            ContactX = contactX;
            ContactY = contactY;
            ExteriorX = exteriorX;
            ExteriorY = exteriorY;
            ExteriorTileId = exteriorTileId;
            ExteriorPcl = exteriorPcl;
        }

        internal int ContactX { get; }
        internal int ContactY { get; }
        internal int ExteriorX { get; }
        internal int ExteriorY { get; }
        internal int ExteriorTileId { get; }
        internal int ExteriorPcl { get; }
    }

    internal sealed class SynchronizedDrawbridgeSnapshot
    {
        internal SynchronizedDrawbridgeSnapshot(int buildingId, uint globalId)
        {
            BuildingId = buildingId;
            GlobalId = globalId;
        }

        internal int BuildingId { get; }
        internal uint GlobalId { get; }
        internal List<DrawbridgeApproachSnapshot> Approaches { get; } =
            new List<DrawbridgeApproachSnapshot>();
    }

    internal static class SynchronizedGatehouseReachabilityPolicy
    {
        internal const int MaximumSynchronizedDrawbridges = 2;

        internal static List<VanillaFootprintCandidate> BuildOrderedFootprintCandidates(
            int originX,
            int originY,
            int occupyTileGridSize)
        {
            var candidates = new List<VanillaFootprintCandidate>();
            if (occupyTileGridSize <= 0)
                return candidates;

            int midpoint = occupyTileGridSize / 2;
            for (int x = midpoint; x < occupyTileGridSize; x++)
            {
                candidates.Add(new VanillaFootprintCandidate(
                    originX + x, originY - 1, 0, -1));
            }
            for (int y = 0; y < occupyTileGridSize; y++)
            {
                candidates.Add(new VanillaFootprintCandidate(
                    originX + occupyTileGridSize, originY + y, 1, 0));
            }
            for (int x = occupyTileGridSize - 1; x >= 0; x--)
            {
                candidates.Add(new VanillaFootprintCandidate(
                    originX + x, originY + occupyTileGridSize, 0, 1));
            }
            for (int y = occupyTileGridSize - 1; y >= 0; y--)
            {
                candidates.Add(new VanillaFootprintCandidate(
                    originX - 1, originY + y, -1, 0));
            }
            for (int x = 0; x < midpoint; x++)
            {
                candidates.Add(new VanillaFootprintCandidate(
                    originX + x, originY - 1, 0, -1));
            }

            return candidates;
        }

        internal static List<int> CollectFirstDistinctBuildingIds(
            IReadOnlyList<VanillaFootprintCandidate> candidates,
            Func<int, int, int> getBuildingId,
            Func<int, bool> isEligibleDrawbridge)
        {
            var result = new List<int>(MaximumSynchronizedDrawbridges);
            if (candidates == null || getBuildingId == null || isEligibleDrawbridge == null)
                return result;

            for (int index = 0; index < candidates.Count; index++)
            {
                VanillaFootprintCandidate candidate = candidates[index];
                int buildingId = getBuildingId(candidate.X, candidate.Y);
                if (buildingId <= 0 || result.Contains(buildingId) ||
                    !isEligibleDrawbridge(buildingId))
                {
                    continue;
                }

                result.Add(buildingId);
                if (result.Count == MaximumSynchronizedDrawbridges)
                    break;
            }

            return result;
        }

        internal static bool TryTraceExteriorApproach(
            VanillaFootprintCandidate contact,
            int drawbridgeBuildingId,
            Func<int, int, bool> isInsideMap,
            Func<int, int, int> getBuildingId,
            Func<int, int, int> getTileId,
            Func<int, int> getPathComponent,
            out DrawbridgeApproachSnapshot approach)
        {
            approach = default;
            if (drawbridgeBuildingId <= 0 || isInsideMap == null || getBuildingId == null ||
                getTileId == null || getPathComponent == null ||
                Math.Abs(contact.OutwardX) + Math.Abs(contact.OutwardY) != 1 ||
                !isInsideMap(contact.X, contact.Y) ||
                getBuildingId(contact.X, contact.Y) != drawbridgeBuildingId)
            {
                return false;
            }

            int x = contact.X;
            int y = contact.Y;
            do
            {
                x += contact.OutwardX;
                y += contact.OutwardY;
                if (!isInsideMap(x, y))
                    return false;
            }
            while (getBuildingId(x, y) == drawbridgeBuildingId);

            int tileId = getTileId(x, y);
            int pcl = getPathComponent(tileId);
            if (tileId < 0 || pcl <= 0 || pcl > ushort.MaxValue)
                return false;

            approach = new DrawbridgeApproachSnapshot(
                contact.X,
                contact.Y,
                x,
                y,
                tileId,
                pcl);
            return true;
        }

        internal static bool CanReachAnyExteriorApproach(
            IReadOnlyList<SynchronizedDrawbridgeSnapshot> drawbridges,
            Func<int, bool> canReachComponent)
        {
            if (drawbridges == null || canReachComponent == null ||
                drawbridges.Count > MaximumSynchronizedDrawbridges)
            {
                return false;
            }

            for (int bridgeIndex = 0; bridgeIndex < drawbridges.Count; bridgeIndex++)
            {
                IReadOnlyList<DrawbridgeApproachSnapshot> approaches =
                    drawbridges[bridgeIndex].Approaches;
                for (int approachIndex = 0; approachIndex < approaches.Count; approachIndex++)
                {
                    if (canReachComponent(approaches[approachIndex].ExteriorPcl))
                        return true;
                }
            }

            return false;
        }

        internal static int ComputeDrawbridgeSignature(
            IReadOnlyList<SynchronizedDrawbridgeSnapshot> drawbridges)
        {
            unchecked
            {
                int signature = 17;
                if (drawbridges == null)
                    return signature;

                for (int bridgeIndex = 0; bridgeIndex < drawbridges.Count; bridgeIndex++)
                {
                    SynchronizedDrawbridgeSnapshot bridge = drawbridges[bridgeIndex];
                    signature = signature * 397 ^ bridge.BuildingId;
                    signature = signature * 397 ^ (int)bridge.GlobalId;
                    for (int approachIndex = 0;
                         approachIndex < bridge.Approaches.Count;
                         approachIndex++)
                    {
                        signature = signature * 397 ^
                            bridge.Approaches[approachIndex].ExteriorPcl;
                    }
                }

                return signature;
            }
        }
    }
}
