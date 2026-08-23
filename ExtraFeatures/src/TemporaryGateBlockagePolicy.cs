// Feature: Pure graph policy for AI buildings blocked only by closed friendly gates.
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal readonly struct PclGateConnection
    {
        public PclGateConnection(int first, int second, int buildingId = 0, uint globalId = 0)
        {
            First = first;
            Second = second;
            BuildingId = buildingId;
            GlobalId = globalId;
        }

        public int First { get; }
        public int Second { get; }
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
        internal GateBlockageEvaluation(GateBlockageEvaluationKind kind, int[] usedGateIndices)
        {
            Kind = kind;
            UsedGateIndices = usedGateIndices ?? Array.Empty<int>();
        }

        internal GateBlockageEvaluationKind Kind { get; }
        internal int[] UsedGateIndices { get; }
        internal bool IsOnlyTemporarilyBlocked =>
            Kind == GateBlockageEvaluationKind.TemporaryViaClosedFriendlyGate;
    }

    internal static class TemporaryGateBlockagePolicy
    {
        internal const int VanillaMode = 0;
        internal const int TemporaryGateMode = 1;
        internal const int AlwaysPreventMode = 2;

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
            IReadOnlyList<PclGateConnection> closedFriendlyGates,
            Func<int, int, bool> normallyReachable)
        {
            return Evaluate(
                buildingPcls,
                keepPcls,
                closedFriendlyGates,
                normallyReachable).IsOnlyTemporarilyBlocked;
        }

        internal static GateBlockageEvaluation Evaluate(
            IReadOnlyCollection<int> buildingPcls,
            IReadOnlyCollection<int> keepPcls,
            IReadOnlyList<PclGateConnection> closedFriendlyGates,
            Func<int, int, bool> normallyReachable)
        {
            if (buildingPcls == null || buildingPcls.Count == 0 ||
                keepPcls == null || keepPcls.Count == 0 ||
                normallyReachable == null)
            {
                return new GateBlockageEvaluation(GateBlockageEvaluationKind.NoVirtualGatePath, null);
            }

            List<int> sources = ValidDistinct(buildingPcls);
            List<int> destinations = ValidDistinct(keepPcls);
            if (sources.Count == 0 || destinations.Count == 0)
                return new GateBlockageEvaluation(GateBlockageEvaluationKind.NoVirtualGatePath, null);

            foreach (int source in sources)
            {
                foreach (int destination in destinations)
                {
                    if (CanReach(source, destination, normallyReachable))
                        return new GateBlockageEvaluation(GateBlockageEvaluationKind.NormallyReachable, null);
                }
            }

            if (closedFriendlyGates == null || closedFriendlyGates.Count == 0)
                return new GateBlockageEvaluation(GateBlockageEvaluationKind.NoVirtualGatePath, null);

            Queue<int> pending = new Queue<int>();
            HashSet<int> visitedAfterGate = new HashSet<int>();
            Dictionary<int, GateTraversalStep> traversal = new Dictionary<int, GateTraversalStep>();
            for (int gateIndex = 0; gateIndex < closedFriendlyGates.Count; gateIndex++)
            {
                PclGateConnection gate = closedFriendlyGates[gateIndex];
                if (gate.First <= 0 || gate.Second <= 0 || gate.First == gate.Second)
                    continue;

                if (AnyCanReach(sources, gate.First, normallyReachable) && visitedAfterGate.Add(gate.Second))
                {
                    traversal[gate.Second] = new GateTraversalStep(0, gateIndex);
                    pending.Enqueue(gate.Second);
                }
                if (AnyCanReach(sources, gate.Second, normallyReachable) && visitedAfterGate.Add(gate.First))
                {
                    traversal[gate.First] = new GateTraversalStep(0, gateIndex);
                    pending.Enqueue(gate.First);
                }
            }

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                if (AnyDestinationReachable(current, destinations, normallyReachable))
                {
                    return new GateBlockageEvaluation(
                        GateBlockageEvaluationKind.TemporaryViaClosedFriendlyGate,
                        ReconstructGatePath(current, traversal));
                }

                for (int gateIndex = 0; gateIndex < closedFriendlyGates.Count; gateIndex++)
                {
                    PclGateConnection gate = closedFriendlyGates[gateIndex];
                    if (gate.First <= 0 || gate.Second <= 0 || gate.First == gate.Second)
                        continue;

                    if (CanReach(current, gate.First, normallyReachable) && visitedAfterGate.Add(gate.Second))
                    {
                        traversal[gate.Second] = new GateTraversalStep(current, gateIndex);
                        pending.Enqueue(gate.Second);
                    }
                    if (CanReach(current, gate.Second, normallyReachable) && visitedAfterGate.Add(gate.First))
                    {
                        traversal[gate.First] = new GateTraversalStep(current, gateIndex);
                        pending.Enqueue(gate.First);
                    }
                }
            }

            return new GateBlockageEvaluation(GateBlockageEvaluationKind.NoVirtualGatePath, null);
        }

        private static int[] ReconstructGatePath(
            int destinationPcl,
            IReadOnlyDictionary<int, GateTraversalStep> traversal)
        {
            List<int> reversed = new List<int>();
            int current = destinationPcl;
            while (traversal.TryGetValue(current, out GateTraversalStep step))
            {
                reversed.Add(step.GateIndex);
                if (step.PreviousPcl == 0)
                    break;
                current = step.PreviousPcl;
            }
            reversed.Reverse();
            return reversed.ToArray();
        }

        private static List<int> ValidDistinct(IReadOnlyCollection<int> values)
        {
            List<int> result = new List<int>(values.Count);
            HashSet<int> seen = new HashSet<int>();
            foreach (int value in values)
            {
                if (value > 0 && seen.Add(value))
                    result.Add(value);
            }
            return result;
        }

        private static bool AnyCanReach(
            IReadOnlyList<int> sources,
            int destination,
            Func<int, int, bool> normallyReachable)
        {
            foreach (int source in sources)
            {
                if (CanReach(source, destination, normallyReachable))
                    return true;
            }
            return false;
        }

        private static bool AnyDestinationReachable(
            int source,
            IReadOnlyList<int> destinations,
            Func<int, int, bool> normallyReachable)
        {
            foreach (int destination in destinations)
            {
                if (CanReach(source, destination, normallyReachable))
                    return true;
            }
            return false;
        }

        private static bool CanReach(int source, int destination, Func<int, int, bool> normallyReachable) =>
            source > 0 && destination > 0 && (source == destination || normallyReachable(source, destination));

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
