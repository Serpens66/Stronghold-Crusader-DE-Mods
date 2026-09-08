// Feature: Decide whether Vanilla's AI-building accessibility result may be relaxed.
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct PclPortalConnection
    {
        internal PclPortalConnection(
            int first,
            int second,
            int third = 0,
            int ownerId = 0,
            int buildingId = 0,
            uint globalId = 0)
        {
            First = first;
            Second = second;
            Third = third;
            OwnerId = ownerId;
            BuildingId = buildingId;
            GlobalId = globalId;
        }

        internal int First { get; }
        internal int Second { get; }
        internal int Third { get; }
        internal int OwnerId { get; }
        internal int BuildingId { get; }
        internal uint GlobalId { get; }
    }

    internal enum GateBlockageEvaluationKind
    {
        ReachableWithoutFriendlyGate,
        ReachableViaFriendlyGate,
        UnreachableEvenWithFriendlyGates
    }

    internal readonly struct GateBlockageEvaluation
    {
        internal GateBlockageEvaluation(
            GateBlockageEvaluationKind kind,
            int[] usedPortalIndices)
        {
            Kind = kind;
            UsedPortalIndices = usedPortalIndices ?? Array.Empty<int>();
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal int[] UsedPortalIndices { get; }
        internal bool IsReachableUnderImprovedCheck =>
            Kind != GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates;
    }

    internal static class TemporaryGateBlockagePolicy
    {
        internal const int VanillaMode = 0;
        internal const int ImprovedReachabilityMode = 1;
        internal const int AlwaysPreventMode = 2;
        internal const int AccessibleResult = 1;
        internal const int NoEntranceResult = 0;
        internal const int DisconnectedEntranceResult = 2;

        internal static bool IsFriendlyPortalOwner(
            int playerId,
            int portalOwnerId,
            Func<int, bool> isValidPlayer,
            Func<int, int, bool> isAllied)
        {
            if (portalOwnerId == playerId)
                return true;
            return isValidPlayer != null && isAllied != null &&
                isValidPlayer(portalOwnerId) && isAllied(playerId, portalOwnerId);
        }

        internal static bool IsGateOrDrawbridge(eStructs type) =>
            type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER ||
            type == eStructs.STRUCT_GATE_WOOD ||
            type == eStructs.STRUCT_GATE_POSTERN ||
            type == eStructs.STRUCT_DRAWBRIDGE ||
            type == eStructs.STRUCT_GATEHOUSE;

        internal static int ResolveAccessibilityResult(
            int mode,
            bool isLivingAiBuilding,
            eStructs buildingType,
            int vanillaResult,
            bool classificationAvailable,
            bool reachableViaFriendlyPortals)
        {
            if (!isLivingAiBuilding || mode == VanillaMode ||
                (vanillaResult != NoEntranceResult &&
                 vanillaResult != DisconnectedEntranceResult))
            {
                return vanillaResult;
            }

            if (mode == AlwaysPreventMode)
                return AccessibleResult;
            if (mode != ImprovedReachabilityMode)
                return vanillaResult;

            // Stables create horses without requiring a worker route to the keep.
            if (buildingType == eStructs.STRUCT_STABLES)
                return AccessibleResult;

            return vanillaResult == DisconnectedEntranceResult &&
                   classificationAvailable && reachableViaFriendlyPortals
                ? AccessibleResult
                : vanillaResult;
        }

        internal static GateBlockageEvaluation Evaluate(
            int buildingPcl,
            int keepPcl,
            IReadOnlyList<PclPortalConnection> friendlyPortals)
        {
            if (buildingPcl <= 0 || keepPcl <= 0)
                return Unreachable();
            if (buildingPcl == keepPcl)
            {
                return new GateBlockageEvaluation(
                    GateBlockageEvaluationKind.ReachableWithoutFriendlyGate,
                    null);
            }

            IReadOnlyList<PclPortalConnection> portals =
                friendlyPortals ?? Array.Empty<PclPortalConnection>();
            var adjacency = new Dictionary<int, List<PortalEdge>>();
            for (int portalIndex = 0; portalIndex < portals.Count; portalIndex++)
            {
                PclPortalConnection portal = portals[portalIndex];
                AddPair(adjacency, portal.First, portal.Second, portalIndex);
                AddPair(adjacency, portal.First, portal.Third, portalIndex);
                AddPair(adjacency, portal.Second, portal.Third, portalIndex);
            }

            var visited = new HashSet<int> { buildingPcl };
            var pending = new Queue<int>();
            var traversal = new Dictionary<int, PortalTraversalStep>();
            pending.Enqueue(buildingPcl);
            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                if (!adjacency.TryGetValue(current, out List<PortalEdge> edges))
                    continue;

                foreach (PortalEdge edge in edges)
                {
                    if (!visited.Add(edge.DestinationPcl))
                        continue;

                    traversal[edge.DestinationPcl] =
                        new PortalTraversalStep(current, edge.PortalIndex);
                    if (edge.DestinationPcl == keepPcl)
                    {
                        return new GateBlockageEvaluation(
                            GateBlockageEvaluationKind.ReachableViaFriendlyGate,
                            ReconstructPortalPath(keepPcl, traversal));
                    }
                    pending.Enqueue(edge.DestinationPcl);
                }
            }

            return Unreachable();
        }

        private static GateBlockageEvaluation Unreachable() =>
            new GateBlockageEvaluation(
                GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates,
                null);

        private static void AddPair(
            Dictionary<int, List<PortalEdge>> adjacency,
            int first,
            int second,
            int portalIndex)
        {
            if (first <= 0 || second <= 0 || first == second)
                return;

            AddEdge(adjacency, first, new PortalEdge(second, portalIndex));
            AddEdge(adjacency, second, new PortalEdge(first, portalIndex));
        }

        private static void AddEdge(
            Dictionary<int, List<PortalEdge>> adjacency,
            int source,
            PortalEdge edge)
        {
            if (!adjacency.TryGetValue(source, out List<PortalEdge> edges))
            {
                edges = new List<PortalEdge>();
                adjacency.Add(source, edges);
            }
            edges.Add(edge);
        }

        private static int[] ReconstructPortalPath(
            int destinationPcl,
            IReadOnlyDictionary<int, PortalTraversalStep> traversal)
        {
            var reversed = new List<int>();
            int current = destinationPcl;
            while (traversal.TryGetValue(current, out PortalTraversalStep step))
            {
                reversed.Add(step.PortalIndex);
                current = step.PreviousPcl;
            }
            reversed.Reverse();
            return reversed.ToArray();
        }

        private readonly struct PortalEdge
        {
            internal PortalEdge(int destinationPcl, int portalIndex)
            {
                DestinationPcl = destinationPcl;
                PortalIndex = portalIndex;
            }

            internal int DestinationPcl { get; }
            internal int PortalIndex { get; }
        }

        private readonly struct PortalTraversalStep
        {
            internal PortalTraversalStep(int previousPcl, int portalIndex)
            {
                PreviousPcl = previousPcl;
                PortalIndex = portalIndex;
            }

            internal int PreviousPcl { get; }
            internal int PortalIndex { get; }
        }
    }
}
