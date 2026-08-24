// Feature: Pure graph policy for AI buildings blocked only by closed friendly gates.
using System;
using System.Collections.Generic;
using System.Text;

namespace ExtraFeatures
{
    internal readonly struct PclGateConnection
    {
        public PclGateConnection(int first, int second, int buildingId = 0, uint globalId = 0)
            : this(first, second, isOpen: false, buildingId, globalId)
        {
        }

        public PclGateConnection(int first, int second, bool isOpen, int buildingId = 0, uint globalId = 0)
        {
            First = first;
            Second = second;
            IsOpen = isOpen;
            BuildingId = buildingId;
            GlobalId = globalId;
        }

        public int First { get; }
        public int Second { get; }
        public bool IsOpen { get; }
        public int BuildingId { get; }
        public uint GlobalId { get; }
    }

    internal enum GateBlockageEvaluationKind
    {
        NormallyReachable,
        TemporaryViaClosedFriendlyGate,
        NoVirtualGatePath
    }

    internal readonly struct GateBlockageEvaluation
    {
        internal GateBlockageEvaluation(
            GateBlockageEvaluationKind kind,
            bool hasPathWithoutClosedGate,
            bool hasPathUsingClosedGate,
            bool? nativePlayerAwareReachable,
            int[] usedGateIndices)
        {
            Kind = kind;
            HasPathWithoutClosedGate = hasPathWithoutClosedGate;
            HasPathUsingClosedGate = hasPathUsingClosedGate;
            NativePlayerAwareReachable = nativePlayerAwareReachable;
            UsedGateIndices = usedGateIndices ?? Array.Empty<int>();
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal bool HasPathWithoutClosedGate { get; }
        internal bool HasPathUsingClosedGate { get; }
        internal bool? NativePlayerAwareReachable { get; }
        internal int[] UsedGateIndices { get; }
        internal bool IsOnlyTemporarilyBlocked =>
            Kind == GateBlockageEvaluationKind.TemporaryViaClosedFriendlyGate;
    }

    internal static class TemporaryGateBlockagePolicy
    {
        internal const int VanillaMode = 0;
        internal const int TemporaryGateMode = 1;
        internal const int AlwaysPreventMode = 2;

        internal static string BuildExactTopologyKey(
            IReadOnlyList<PclGateConnection> sortedGates,
            int skippedNoOpGates)
        {
            IReadOnlyList<PclGateConnection> gates = sortedGates ?? Array.Empty<PclGateConnection>();
            var builder = new StringBuilder(gates.Count * 32 + 16);
            builder.Append("noop=").Append(skippedNoOpGates).Append('|');
            foreach (PclGateConnection gate in gates)
            {
                builder.Append(gate.GlobalId).Append(':')
                    .Append(gate.BuildingId).Append(':')
                    .Append(gate.First).Append(':')
                    .Append(gate.Second).Append(':')
                    .Append(gate.IsOpen ? '1' : '0').Append('|');
            }
            return builder.ToString();
        }

        internal static bool ShouldSuppressDemolition(
            int mode,
            bool isLivingAiBuilding,
            bool classificationAvailable,
            bool isOnlyTemporarilyBlocked)
        {
            if (!isLivingAiBuilding || mode == VanillaMode)
                return false;

            if (mode == AlwaysPreventMode)
                return true;

            return mode == TemporaryGateMode && classificationAvailable && isOnlyTemporarilyBlocked;
        }

        internal static bool IsOnlyTemporarilyBlocked(
            IReadOnlyCollection<int> buildingPcls,
            IReadOnlyCollection<int> keepPcls,
            IReadOnlyList<PclGateConnection> ownedGates,
            Func<int, int, bool> nativePlayerAwareReachable)
        {
            return Evaluate(
                buildingPcls,
                keepPcls,
                ownedGates,
                nativePlayerAwareReachable).IsOnlyTemporarilyBlocked;
        }

        internal static GateBlockageEvaluation Evaluate(
            IReadOnlyCollection<int> buildingPcls,
            IReadOnlyCollection<int> keepPcls,
            IReadOnlyList<PclGateConnection> ownedGates,
            Func<int, int, bool> nativePlayerAwareReachable)
        {
            List<int> sources = ValidDistinct(buildingPcls);
            List<int> destinations = ValidDistinct(keepPcls);
            if (sources.Count == 0 || destinations.Count == 0 || nativePlayerAwareReachable == null)
                return NoPath();

            bool nativeReachable = AnyNativePairReachable(sources, destinations, nativePlayerAwareReachable);
            IReadOnlyList<PclGateConnection> gates = ownedGates ?? Array.Empty<PclGateConnection>();
            Dictionary<int, List<GateEdge>> adjacency = BuildAdjacency(gates);

            if (TryFindPath(sources, destinations, adjacency, allowClosedGates: false, out _))
            {
                return new GateBlockageEvaluation(
                    nativeReachable
                        ? GateBlockageEvaluationKind.NormallyReachable
                        : GateBlockageEvaluationKind.NoVirtualGatePath,
                    hasPathWithoutClosedGate: true,
                    hasPathUsingClosedGate: false,
                    nativePlayerAwareReachable: nativeReachable,
                    usedGateIndices: null);
            }

            if (!TryFindPath(sources, destinations, adjacency, allowClosedGates: true, out int[] usedGateIndices) ||
                !PathUsesClosedGate(gates, usedGateIndices))
            {
                return new GateBlockageEvaluation(
                    GateBlockageEvaluationKind.NoVirtualGatePath,
                    hasPathWithoutClosedGate: false,
                    hasPathUsingClosedGate: false,
                    nativePlayerAwareReachable: nativeReachable,
                    usedGateIndices: null);
            }

            return new GateBlockageEvaluation(
                nativeReachable
                    ? GateBlockageEvaluationKind.TemporaryViaClosedFriendlyGate
                    : GateBlockageEvaluationKind.NoVirtualGatePath,
                hasPathWithoutClosedGate: false,
                hasPathUsingClosedGate: true,
                nativePlayerAwareReachable: nativeReachable,
                usedGateIndices: usedGateIndices);
        }

        private static GateBlockageEvaluation NoPath() =>
            new GateBlockageEvaluation(
                GateBlockageEvaluationKind.NoVirtualGatePath,
                hasPathWithoutClosedGate: false,
                hasPathUsingClosedGate: false,
                nativePlayerAwareReachable: null,
                usedGateIndices: null);

        private static Dictionary<int, List<GateEdge>> BuildAdjacency(IReadOnlyList<PclGateConnection> gates)
        {
            var adjacency = new Dictionary<int, List<GateEdge>>();
            for (int gateIndex = 0; gateIndex < gates.Count; gateIndex++)
            {
                PclGateConnection gate = gates[gateIndex];
                if (gate.First <= 0 || gate.Second <= 0 || gate.First == gate.Second)
                    continue;

                AddEdge(adjacency, gate.First, new GateEdge(gate.Second, gateIndex, gate.IsOpen));
                AddEdge(adjacency, gate.Second, new GateEdge(gate.First, gateIndex, gate.IsOpen));
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
            bool allowClosedGates,
            out int[] usedGateIndices)
        {
            var destinationSet = new HashSet<int>(destinations);
            var visited = new HashSet<int>();
            var pending = new Queue<int>();
            var traversal = new Dictionary<int, GateTraversalStep>();

            foreach (int source in sources)
            {
                if (!visited.Add(source))
                    continue;
                if (destinationSet.Contains(source))
                {
                    usedGateIndices = Array.Empty<int>();
                    return true;
                }
                pending.Enqueue(source);
            }

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                if (!adjacency.TryGetValue(current, out List<GateEdge> edges))
                    continue;

                foreach (GateEdge edge in edges)
                {
                    if ((!allowClosedGates && !edge.IsOpen) || !visited.Add(edge.DestinationPcl))
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

        private static bool PathUsesClosedGate(
            IReadOnlyList<PclGateConnection> gates,
            IReadOnlyList<int> usedGateIndices)
        {
            foreach (int gateIndex in usedGateIndices)
            {
                if ((uint)gateIndex < (uint)gates.Count && !gates[gateIndex].IsOpen)
                    return true;
            }
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
            internal GateEdge(int destinationPcl, int gateIndex, bool isOpen)
            {
                DestinationPcl = destinationPcl;
                GateIndex = gateIndex;
                IsOpen = isOpen;
            }

            internal int DestinationPcl { get; }
            internal int GateIndex { get; }
            internal bool IsOpen { get; }
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
