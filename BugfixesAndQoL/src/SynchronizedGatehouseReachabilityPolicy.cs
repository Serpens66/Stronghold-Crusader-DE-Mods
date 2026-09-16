// Feature: Evaluate a gatehouse and its synchronized drawbridges as one hypothetical portal group.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal enum DrawbridgeAssociationResult
    {
        Accepted,
        InvalidComponents,
        NoSharedComponent,
        NonAdjacentEndpoints
    }

    internal readonly struct GatePortalSnapshot
    {
        internal GatePortalSnapshot(
            int firstPcl,
            int secondPcl,
            int thirdPcl,
            int entryX,
            int entryY,
            int exitX,
            int exitY)
        {
            FirstPcl = firstPcl;
            SecondPcl = secondPcl;
            ThirdPcl = thirdPcl;
            EntryX = entryX;
            EntryY = entryY;
            ExitX = exitX;
            ExitY = exitY;
        }

        internal int FirstPcl { get; }
        internal int SecondPcl { get; }
        internal int ThirdPcl { get; }
        internal int EntryX { get; }
        internal int EntryY { get; }
        internal int ExitX { get; }
        internal int ExitY { get; }

        internal bool HasValidComponents =>
            FirstPcl > 0 && SecondPcl > 0 &&
            FirstPcl <= ushort.MaxValue && SecondPcl <= ushort.MaxValue &&
            ThirdPcl >= 0 && ThirdPcl <= ushort.MaxValue;
    }

    internal static class SynchronizedGatehouseReachabilityPolicy
    {
        internal const int MaximumSynchronizedDrawbridges = 2;

        internal static bool IsAssociatedDrawbridge(
            GatePortalSnapshot gatehouse,
            GatePortalSnapshot drawbridge) =>
            EvaluateAssociation(gatehouse, drawbridge) == DrawbridgeAssociationResult.Accepted;

        internal static DrawbridgeAssociationResult EvaluateAssociation(
            GatePortalSnapshot gatehouse,
            GatePortalSnapshot drawbridge)
        {
            if (!gatehouse.HasValidComponents || !drawbridge.HasValidComponents)
                return DrawbridgeAssociationResult.InvalidComponents;
            if (!SharesComponent(gatehouse, drawbridge))
                return DrawbridgeAssociationResult.NoSharedComponent;
            return HasAdjacentEndpoint(gatehouse, drawbridge)
                ? DrawbridgeAssociationResult.Accepted
                : DrawbridgeAssociationResult.NonAdjacentEndpoints;
        }

        internal static bool CanReachSynchronizedGroup(
            GatePortalSnapshot gatehouse,
            IReadOnlyList<GatePortalSnapshot> drawbridges,
            Func<int, bool> canReachCurrentComponent)
        {
            if (!gatehouse.HasValidComponents || canReachCurrentComponent == null)
                return false;

            IReadOnlyList<GatePortalSnapshot> bridges =
                drawbridges ?? Array.Empty<GatePortalSnapshot>();
            if (bridges.Count > MaximumSynchronizedDrawbridges)
                return false;

            var reachableGroup = new HashSet<int>();
            AddComponents(reachableGroup, gatehouse);

            bool added;
            do
            {
                added = false;
                for (int index = 0; index < bridges.Count; index++)
                {
                    GatePortalSnapshot bridge = bridges[index];
                    if (!bridge.HasValidComponents ||
                        !IsAssociatedDrawbridge(gatehouse, bridge) ||
                        !ContainsAny(reachableGroup, bridge))
                    {
                        continue;
                    }

                    int previousCount = reachableGroup.Count;
                    AddComponents(reachableGroup, bridge);
                    added |= reachableGroup.Count != previousCount;
                }
            }
            while (added);

            foreach (int component in reachableGroup)
            {
                if (canReachCurrentComponent(component))
                    return true;
            }

            return false;
        }

        private static bool SharesComponent(
            GatePortalSnapshot first,
            GatePortalSnapshot second)
        {
            return Contains(first, second.FirstPcl) ||
                Contains(first, second.SecondPcl) ||
                Contains(first, second.ThirdPcl);
        }

        private static bool HasAdjacentEndpoint(
            GatePortalSnapshot gatehouse,
            GatePortalSnapshot drawbridge)
        {
            return IsAdjacent(gatehouse.EntryX, gatehouse.EntryY, drawbridge.EntryX, drawbridge.EntryY) ||
                IsAdjacent(gatehouse.EntryX, gatehouse.EntryY, drawbridge.ExitX, drawbridge.ExitY) ||
                IsAdjacent(gatehouse.ExitX, gatehouse.ExitY, drawbridge.EntryX, drawbridge.EntryY) ||
                IsAdjacent(gatehouse.ExitX, gatehouse.ExitY, drawbridge.ExitX, drawbridge.ExitY);
        }

        private static bool IsAdjacent(int firstX, int firstY, int secondX, int secondY) =>
            Math.Abs(firstX - secondX) <= 1 && Math.Abs(firstY - secondY) <= 1;

        private static bool Contains(GatePortalSnapshot portal, int component) =>
            component > 0 &&
            (portal.FirstPcl == component || portal.SecondPcl == component ||
             portal.ThirdPcl == component);

        private static bool ContainsAny(HashSet<int> components, GatePortalSnapshot portal) =>
            components.Contains(portal.FirstPcl) ||
            components.Contains(portal.SecondPcl) ||
            (portal.ThirdPcl > 0 && components.Contains(portal.ThirdPcl));

        private static void AddComponents(HashSet<int> components, GatePortalSnapshot portal)
        {
            if (portal.FirstPcl > 0)
                components.Add(portal.FirstPcl);
            if (portal.SecondPcl > 0)
                components.Add(portal.SecondPcl);
            if (portal.ThirdPcl > 0)
                components.Add(portal.ThirdPcl);
        }
    }
}
