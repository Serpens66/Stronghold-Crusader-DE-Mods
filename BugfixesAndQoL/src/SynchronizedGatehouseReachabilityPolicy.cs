// Feature: Mirror Vanilla's footprint-edge lookup for gatehouse-coupled drawbridges.
using VanillaFootprintCandidate = APIShared.GatehouseFootprintCandidate;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
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
        internal const int MaximumSynchronizedDrawbridges = APIShared.GatehouseDrawbridgeCoupling.MaximumCoupledDrawbridges;

        internal static List<VanillaFootprintCandidate> BuildOrderedFootprintCandidates(int originX, int originY, int occupyTileGridSize) =>
            APIShared.GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates(originX, originY, occupyTileGridSize);

        internal static List<int> CollectFirstDistinctBuildingIds(IReadOnlyList<VanillaFootprintCandidate> candidates,
            Func<int, int, int> getBuildingId, Func<int, bool> isEligibleDrawbridge) =>
            APIShared.GatehouseDrawbridgeCoupling.CollectFirstDistinctBuildingIds(candidates, getBuildingId, isEligibleDrawbridge);

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
