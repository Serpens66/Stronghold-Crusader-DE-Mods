// Feature: Pure PCL graph policy for improved AI-building reachability checks.
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal readonly struct PclGateConnection
    {
        internal PclGateConnection(int first, int second, int ownerId = 0, int buildingId = 0, uint globalId = 0)
        {
            First = first;
            Second = second;
            OwnerId = ownerId;
            BuildingId = buildingId;
            GlobalId = globalId;
        }

        internal int First { get; }
        internal int Second { get; }
        internal int OwnerId { get; }
        internal int BuildingId { get; }
        internal uint GlobalId { get; }
    }

    internal enum GateBlockageEvaluationKind
    {
        ReachableWithoutFriendlyGate,
        ReachableViaFriendlyGate,
        ReachableByNativeCurrentStateOnly,
        UnreachableEvenWithFriendlyGates
    }

    internal readonly struct GateBlockageEvaluation
    {
        internal GateBlockageEvaluation(
            GateBlockageEvaluationKind kind,
            bool hasDirectPclPath,
            bool hasPathWithFriendlyGates,
            bool? nativePlayerAwareReachable,
            int[] usedGateIndices)
        {
            Kind = kind;
            HasDirectPclPath = hasDirectPclPath;
            HasPathWithFriendlyGates = hasPathWithFriendlyGates;
            NativePlayerAwareReachable = nativePlayerAwareReachable;
            UsedGateIndices = usedGateIndices ?? Array.Empty<int>();
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal bool HasDirectPclPath { get; }
        internal bool HasPathWithFriendlyGates { get; }
        internal bool? NativePlayerAwareReachable { get; }
        internal int[] UsedGateIndices { get; }
        internal bool IsReachableUnderImprovedCheck =>
            Kind != GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates;
    }

    internal static class TemporaryGateBlockagePolicy
    {
        internal const int VanillaMode = 0;
        internal const int ImprovedReachabilityMode = 1;
        internal const int AlwaysPreventMode = 2;

        internal static bool ShouldSuppressDemolition(
            int mode,
            bool isLivingAiBuilding,
            bool classificationAvailable,
            bool isReachableUnderImprovedCheck)
        {
            if (!isLivingAiBuilding || mode == VanillaMode)
                return false;

            if (mode == AlwaysPreventMode)
                return true;

            return mode == ImprovedReachabilityMode && classificationAvailable && isReachableUnderImprovedCheck;
        }

        internal static GateBlockageEvaluation Evaluate(
            IReadOnlyCollection<int> buildingPcls,
            IReadOnlyCollection<int> keepPcls,
            IReadOnlyList<PclGateConnection> friendlyGates,
            Func<int, int, bool> nativePlayerAwareReachable)
        {
            List<int> sources = ValidDistinct(buildingPcls);
            List<int> destinations = ValidDistinct(keepPcls);
            if (sources.Count == 0 || destinations.Count == 0 || nativePlayerAwareReachable == null)
                return Unreachable(nativePlayerAwareReachable: null);

            bool nativeReachable = AnyNativePairReachable(sources, destinations, nativePlayerAwareReachable);
            if (HasSharedPcl(sources, destinations))
            {
                return new GateBlockageEvaluation(
                    GateBlockageEvaluationKind.ReachableWithoutFriendlyGate,
                    hasDirectPclPath: true,
                    hasPathWithFriendlyGates: true,
                    nativePlayerAwareReachable: nativeReachable,
                    usedGateIndices: null);
            }

            IReadOnlyList<PclGateConnection> gates = friendlyGates ?? Array.Empty<PclGateConnection>();
            Dictionary<int, List<GateEdge>> adjacency = BuildAdjacency(gates);
            if (TryFindPath(sources, destinations, adjacency, out int[] usedGateIndices))
            {
                return new GateBlockageEvaluation(
                    GateBlockageEvaluationKind.ReachableViaFriendlyGate,
                    hasDirectPclPath: false,
                    hasPathWithFriendlyGates: true,
                    nativePlayerAwareReachable: nativeReachable,
                    usedGateIndices: usedGateIndices);
            }

            if (nativeReachable)
            {
                return new GateBlockageEvaluation(
                    GateBlockageEvaluationKind.ReachableByNativeCurrentStateOnly,
                    hasDirectPclPath: false,
                    hasPathWithFriendlyGates: false,
                    nativePlayerAwareReachable: true,
                    usedGateIndices: null);
            }

            return Unreachable(nativePlayerAwareReachable: false);
        }

        private static GateBlockageEvaluation Unreachable(bool? nativePlayerAwareReachable) =>
            new GateBlockageEvaluation(
                GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates,
                hasDirectPclPath: false,
                hasPathWithFriendlyGates: false,
                nativePlayerAwareReachable: nativePlayerAwareReachable,
                usedGateIndices: null);

        private static bool HasSharedPcl(IReadOnlyList<int> sources, IReadOnlyList<int> destinations)
        {
            var destinationSet = new HashSet<int>(destinations);
            foreach (int source in sources)
            {
                if (destinationSet.Contains(source))
                    return true;
            }
            return false;
        }

        private static Dictionary<int, List<GateEdge>> BuildAdjacency(IReadOnlyList<PclGateConnection> gates)
        {
            var adjacency = new Dictionary<int, List<GateEdge>>();
            for (int gateIndex = 0; gateIndex < gates.Count; gateIndex++)
            {
                PclGateConnection gate = gates[gateIndex];
                if (gate.First <= 0 || gate.Second <= 0 || gate.First == gate.Second)
                    continue;

                AddEdge(adjacency, gate.First, new GateEdge(gate.Second, gateIndex));
                AddEdge(adjacency, gate.Second, new GateEdge(gate.First, gateIndex));
            }
            return adjacency;
        }

        private static void AddEdge(Dictionary<int, List<GateEdge>> adjacency, int from, GateEdge edge)
        {
            if (!adjacency.TryGetValue(from, out List<GateEdge> edges))
            {
                edges = new List<GateEdge>();
                adjacency.Add(from, edges);
            }
            edges.Add(edge);
        }

        private static bool TryFindPath(
            IReadOnlyList<int> sources,
            IReadOnlyList<int> destinations,
            IReadOnlyDictionary<int, List<GateEdge>> adjacency,
            out int[] usedGateIndices)
        {
            var destinationSet = new HashSet<int>(destinations);
            var visited = new HashSet<int>();
            var pending = new Queue<int>();
            var traversal = new Dictionary<int, GateTraversalStep>();

            foreach (int source in sources)
            {
                if (visited.Add(source))
                    pending.Enqueue(source);
            }

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                if (!adjacency.TryGetValue(current, out List<GateEdge> edges))
                    continue;

                foreach (GateEdge edge in edges)
                {
                    if (!visited.Add(edge.DestinationPcl))
                        continue;

                    traversal[edge.DestinationPcl] = new GateTraversalStep(current, edge.GateIndex);
                    if (destinationSet.Contains(edge.DestinationPcl))
                    {
                        usedGateIndices = ReconstructGatePath(edge.DestinationPcl, traversal);
                        return true;
                    }
                    pending.Enqueue(edge.DestinationPcl);
                }
            }

            usedGateIndices = Array.Empty<int>();
            return false;
        }

        private static bool AnyNativePairReachable(
            IReadOnlyList<int> sources,
            IReadOnlyList<int> destinations,
            Func<int, int, bool> nativePlayerAwareReachable)
        {
            foreach (int source in sources)
            {
                foreach (int destination in destinations)
                {
                    if (source == destination || nativePlayerAwareReachable(source, destination))
                        return true;
                }
            }
            return false;
        }

        private static int[] ReconstructGatePath(
            int destinationPcl,
            IReadOnlyDictionary<int, GateTraversalStep> traversal)
        {
            var reversed = new List<int>();
            int current = destinationPcl;
            while (traversal.TryGetValue(current, out GateTraversalStep step))
            {
                reversed.Add(step.GateIndex);
                current = step.PreviousPcl;
            }
            reversed.Reverse();
            return reversed.ToArray();
        }

        private static List<int> ValidDistinct(IReadOnlyCollection<int> values)
        {
            if (values == null || values.Count == 0)
                return new List<int>();

            var result = new List<int>(values.Count);
            var seen = new HashSet<int>();
            foreach (int value in values)
            {
                if (value > 0 && seen.Add(value))
                    result.Add(value);
            }
            return result;
        }

        private readonly struct GateEdge
        {
            internal GateEdge(int destinationPcl, int gateIndex)
            {
                DestinationPcl = destinationPcl;
                GateIndex = gateIndex;
            }

            internal int DestinationPcl { get; }
            internal int GateIndex { get; }
        }

        private readonly struct GateTraversalStep
        {
            internal GateTraversalStep(int previousPcl, int gateIndex)
            {
                PreviousPcl = previousPcl;
                GateIndex = gateIndex;
            }

            internal int PreviousPcl { get; }
            internal int GateIndex { get; }
        }
    }
}
