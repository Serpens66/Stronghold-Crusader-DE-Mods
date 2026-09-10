using System;
using System.Collections.Generic;
using System.Linq;

namespace PreplacedTest
{
    internal static class EconomyPclModel
    {
        internal static int SelectMostFrequentPositive(IEnumerable<int> pcls)
        {
            if (pcls == null) return 0;
            var counts = new Dictionary<int, int>();
            foreach (int pcl in pcls)
            {
                if (pcl <= 0) continue;
                counts.TryGetValue(pcl, out int count);
                counts[pcl] = count + 1;
            }
            int selected = 0;
            int maximum = 0;
            foreach (KeyValuePair<int, int> pair in counts.OrderBy(pair => pair.Key))
            {
                // Vanilla retains the earlier ID on equality while scanning IDs ascending.
                if (pair.Value <= maximum) continue;
                selected = pair.Key;
                maximum = pair.Value;
            }
            return selected;
        }

        internal static int CountDifferentFromReference(IEnumerable<int> pcls, int referencePcl) =>
            pcls == null ? 0 : pcls.Count(pcl => pcl != referencePcl);

        internal static int ApplyPeriodicDecay(int value) => value > 0 ? value - 1 : value;
    }

    internal static class AicSlotIndexResolver
    {
        public static bool TryResolve(int oneBasedSlot, int arrayLength, out int zeroBasedIndex)
        {
            zeroBasedIndex = oneBasedSlot - 1;
            return oneBasedSlot > 0 && zeroBasedIndex < arrayLength;
        }
    }

    internal static class PlacementValidatorResult
    {
        // Vanilla RVA 0x7B060 returns zero only for an allowed tile.
        public static string Classify(int result) => result == 0 ? "allowed" :
            result == 1 ? "rejected" : result == 2 ? "occupied-building" : "unknown-" + result;

        public static bool IsRejected(int result) => result != 0;
    }

    internal static class CrushedTimerTransition
    {
        public static bool IsActivation(int before, int after) => before == 0 && after == 1;
    }

    internal readonly struct PreplacedIdentity : IEquatable<PreplacedIdentity>
    {
        public PreplacedIdentity(int buildingId, uint globalId, int ownerId, int structureType)
        {
            BuildingId = buildingId;
            GlobalId = globalId;
            OwnerId = ownerId;
            StructureType = structureType;
        }

        public int BuildingId { get; }
        public uint GlobalId { get; }
        public int OwnerId { get; }
        public int StructureType { get; }

        public bool Matches(int buildingId, uint globalId, int ownerId, int structureType) =>
            BuildingId == buildingId && GlobalId == globalId && OwnerId == ownerId &&
            StructureType == structureType;

        public bool Equals(PreplacedIdentity other) =>
            Matches(other.BuildingId, other.GlobalId, other.OwnerId, other.StructureType);

        public override bool Equals(object obj) => obj is PreplacedIdentity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BuildingId;
                hash = hash * 397 ^ (int)GlobalId;
                hash = hash * 397 ^ OwnerId;
                return hash * 397 ^ StructureType;
            }
        }
    }

    internal static class PreplacedCountProjection
    {
        public static int WithoutPreplaced(int vanillaCount, int matchingPreplacedCount) =>
            Math.Max(0, vanillaCount - Math.Max(0, matchingPreplacedCount));
    }

    internal readonly struct PortalConnection
    {
        public PortalConnection(int portalId, int first, int second, int third, int rawOwnerValue, int buildingId,
            int actualOwnerId = 0, bool economyModeZeroEligible = true)
        {
            PortalId = portalId;
            First = first;
            Second = second;
            Third = third;
            RawOwnerValue = rawOwnerValue;
            BuildingId = buildingId;
            ActualOwnerId = actualOwnerId;
            EconomyModeZeroEligible = economyModeZeroEligible;
        }

        public int PortalId { get; }
        public int First { get; }
        public int Second { get; }
        public int Third { get; }
        public int RawOwnerValue { get; }
        public int BuildingId { get; }
        public int ActualOwnerId { get; }
        public bool EconomyModeZeroEligible { get; }
    }

    internal enum PortalRouteKind
    {
        Unreachable,
        Direct,
        RawPortalGraph
    }

    internal readonly struct PortalRouteResult
    {
        public PortalRouteResult(PortalRouteKind kind, int[] usedPortalIds)
        {
            Kind = kind;
            UsedPortalIds = usedPortalIds ?? Array.Empty<int>();
        }

        public PortalRouteKind Kind { get; }
        public int[] UsedPortalIds { get; }
    }

    internal static class PortalRouteModel
    {
        public static PortalRouteResult Evaluate(
            int startPcl,
            int destinationPcl,
            IReadOnlyList<PortalConnection> portals)
        {
            if (startPcl <= 0 || destinationPcl <= 0)
                return new PortalRouteResult(PortalRouteKind.Unreachable, null);
            if (startPcl == destinationPcl)
                return new PortalRouteResult(PortalRouteKind.Direct, null);

            IReadOnlyList<PortalConnection> source = portals ?? Array.Empty<PortalConnection>();
            return Search(startPcl, destinationPcl, source,
                portal => true, PortalRouteKind.RawPortalGraph);
        }

        public static PortalRouteResult EvaluateEconomyModeZero(
            int startPcl, int destinationPcl, IReadOnlyList<PortalConnection> portals) =>
            EvaluateFiltered(startPcl, destinationPcl, portals,
                portal => portal.EconomyModeZeroEligible);

        public static PortalRouteResult EvaluateFriendly(
            int startPcl, int destinationPcl, IReadOnlyList<PortalConnection> portals,
            int playerId, Func<int, int, bool> allied) =>
            EvaluateFiltered(startPcl, destinationPcl, portals,
                portal => portal.ActualOwnerId == playerId ||
                    (portal.ActualOwnerId > 0 && allied != null && allied(playerId, portal.ActualOwnerId)));

        public static HashSet<int> ReachableFriendlyPcls(
            int startPcl, IReadOnlyList<PortalConnection> portals, int playerId,
            Func<int, int, bool> allied)
        {
            var reachable = new HashSet<int>();
            if (startPcl <= 0) return reachable;
            reachable.Add(startPcl);
            bool changed;
            do
            {
                changed = false;
                foreach (PortalConnection portal in portals ?? Array.Empty<PortalConnection>())
                {
                    bool friendly = portal.ActualOwnerId == playerId ||
                        (portal.ActualOwnerId > 0 && allied != null && allied(playerId, portal.ActualOwnerId));
                    if (!friendly) continue;
                    int[] values = { portal.First, portal.Second, portal.Third };
                    if (!values.Any(value => value > 0 && reachable.Contains(value))) continue;
                    foreach (int value in values)
                        if (value > 0 && reachable.Add(value)) changed = true;
                }
            } while (changed);
            return reachable;
        }

        private static PortalRouteResult EvaluateFiltered(
            int startPcl, int destinationPcl, IReadOnlyList<PortalConnection> portals,
            Func<PortalConnection, bool> include)
        {
            if (startPcl <= 0 || destinationPcl <= 0)
                return new PortalRouteResult(PortalRouteKind.Unreachable, null);
            if (startPcl == destinationPcl)
                return new PortalRouteResult(PortalRouteKind.Direct, null);
            return Search(startPcl, destinationPcl, portals ?? Array.Empty<PortalConnection>(),
                include, PortalRouteKind.RawPortalGraph);
        }

        private static PortalRouteResult Search(
            int startPcl,
            int destinationPcl,
            IReadOnlyList<PortalConnection> portals,
            Func<PortalConnection, bool> include,
            PortalRouteKind successKind)
        {
            var adjacency = new Dictionary<int, List<PortalEdge>>();
            for (int index = 0; index < portals.Count; index++)
            {
                PortalConnection portal = portals[index];
                if (!include(portal)) continue;
                AddPair(adjacency, portal.First, portal.Second, portal.PortalId);
                AddPair(adjacency, portal.First, portal.Third, portal.PortalId);
                AddPair(adjacency, portal.Second, portal.Third, portal.PortalId);
            }

            var visited = new HashSet<int> { startPcl };
            var pending = new Queue<int>();
            var previous = new Dictionary<int, PortalStep>();
            pending.Enqueue(startPcl);
            while (pending.Count != 0)
            {
                int current = pending.Dequeue();
                if (!adjacency.TryGetValue(current, out List<PortalEdge> edges)) continue;
                foreach (PortalEdge edge in edges)
                {
                    if (!visited.Add(edge.Destination)) continue;
                    previous[edge.Destination] = new PortalStep(current, edge.PortalIndex);
                    if (edge.Destination == destinationPcl)
                        return new PortalRouteResult(successKind, Reconstruct(destinationPcl, previous));
                    pending.Enqueue(edge.Destination);
                }
            }
            return new PortalRouteResult(PortalRouteKind.Unreachable, null);
        }

        private static void AddPair(Dictionary<int, List<PortalEdge>> adjacency, int first, int second, int portalIndex)
        {
            if (first <= 0 || second <= 0 || first == second) return;
            AddEdge(adjacency, first, new PortalEdge(second, portalIndex));
            AddEdge(adjacency, second, new PortalEdge(first, portalIndex));
        }

        private static void AddEdge(Dictionary<int, List<PortalEdge>> adjacency, int pcl, PortalEdge edge)
        {
            if (!adjacency.TryGetValue(pcl, out List<PortalEdge> edges))
            {
                edges = new List<PortalEdge>();
                adjacency.Add(pcl, edges);
            }
            edges.Add(edge);
        }

        private static int[] Reconstruct(int destination, IReadOnlyDictionary<int, PortalStep> previous)
        {
            var result = new List<int>();
            int current = destination;
            while (previous.TryGetValue(current, out PortalStep step))
            {
                result.Add(step.PortalIndex);
                current = step.PreviousPcl;
            }
            result.Reverse();
            return result.ToArray();
        }

        private readonly struct PortalEdge
        {
            public PortalEdge(int destination, int portalIndex)
            {
                Destination = destination;
                PortalIndex = portalIndex;
            }
            public int Destination { get; }
            public int PortalIndex { get; }
        }

        private readonly struct PortalStep
        {
            public PortalStep(int previousPcl, int portalIndex)
            {
                PreviousPcl = previousPcl;
                PortalIndex = portalIndex;
            }
            public int PreviousPcl { get; }
            public int PortalIndex { get; }
        }
    }

    internal readonly struct PclConnectivityTransition
    {
        public PclConnectivityTransition(int insideTileId, int outsideTileId,
            int oldInsidePcl, int oldOutsidePcl, int newPcl)
        {
            InsideTileId = insideTileId;
            OutsideTileId = outsideTileId;
            OldInsidePcl = oldInsidePcl;
            OldOutsidePcl = oldOutsidePcl;
            NewPcl = newPcl;
        }

        public int InsideTileId { get; }
        public int OutsideTileId { get; }
        public int OldInsidePcl { get; }
        public int OldOutsidePcl { get; }
        public int NewPcl { get; }
    }

    internal static class PclConnectivityTransitionDetector
    {
        public static IReadOnlyList<PclConnectivityTransition> Detect(
            ushort[] before, ushort[] after, IEnumerable<int> insideTileIds,
            IEnumerable<int> outsideTileIds)
        {
            if (before == null || after == null || before.Length != after.Length)
                return Array.Empty<PclConnectivityTransition>();
            int[] inside = (insideTileIds ?? Array.Empty<int>()).Distinct().OrderBy(value => value).ToArray();
            int[] outside = (outsideTileIds ?? Array.Empty<int>()).Distinct().OrderBy(value => value).ToArray();
            var result = new List<PclConnectivityTransition>();
            foreach (int insideTileId in inside)
            {
                if ((uint)insideTileId >= (uint)before.Length) continue;
                foreach (int outsideTileId in outside)
                {
                    if ((uint)outsideTileId >= (uint)before.Length) continue;
                    int oldInside = before[insideTileId];
                    int oldOutside = before[outsideTileId];
                    int newInside = after[insideTileId];
                    int newOutside = after[outsideTileId];
                    // Label renumbering is irrelevant: only the equality relation may change.
                    if (oldInside <= 0 || oldOutside <= 0 || oldInside == oldOutside ||
                        newInside <= 0 || newInside != newOutside) continue;
                    result.Add(new PclConnectivityTransition(insideTileId, outsideTileId,
                        oldInside, oldOutside, newInside));
                }
            }
            return result;
        }
    }

    internal enum WallTestRole
    {
        None,
        GatedWallCandidate,
        ClosedWallCandidate
    }

    internal static class WallTestRoleClassifier
    {
        public static WallTestRole Classify(int livingWallCount, int livingPortalCount) =>
            livingWallCount <= 0 ? WallTestRole.None :
            livingPortalCount > 0 ? WallTestRole.GatedWallCandidate : WallTestRole.ClosedWallCandidate;
    }

    internal static class EconomySearchGateClassifier
    {
        public static string Classify(bool generationChanged, int cooldownBefore, int cooldownAfter,
            int queueDepthBefore, int queueDepthAfter)
        {
            if (generationChanged) return "traversal-performed";
            if (cooldownBefore > 0 || cooldownAfter > 0) return "early-return-cooldown";
            if (queueDepthBefore > 0 || queueDepthAfter > 0) return "early-return-shared-queue-state";
            return "early-return-unresolved-mode-or-state";
        }
    }

    internal static class DamageObservationModel
    {
        public static bool IsLethalInput(int currentHealth, int damage) => currentHealth > 0 && damage >= currentHealth;
    }

    internal static class BuildingAccessibilityResult
    {
        public static string Classify(int result) => result == 0 ? "rejected-zero" :
            result == 1 ? "accessible" : result == 2 ? "rejected-two" : "unknown-" + result;

        public static bool IsRejected(int result) => result == 0 || result == 2;
    }

    internal static class FirstAivBuildingEligibility
    {
        public static bool IsEligible(bool validOwnedLivingRecord, bool isWall) =>
            validOwnedLivingRecord && !isWall;
    }

    internal static class FirstAivSpawnCorrelation
    {
        public static bool Matches(int buildingBeginX, int buildingBeginY, int buildingEndX, int buildingEndY,
            int buildingType, int signalX, int signalY, int signalType) =>
            buildingType == signalType && signalX >= buildingBeginX && signalX <= buildingEndX &&
            signalY >= buildingBeginY && signalY <= buildingEndY;
    }

    internal static class EconomyCooldownTransition
    {
        public static string Classify(int before, int after) =>
            before < -1 || after < -1 ? "unexpected-negative" :
            before == -1 && after == -1 ? "ready-sentinel-unchanged" :
            before == -1 && after > 0 ? "armed-from-ready-sentinel" :
            before >= 0 && after == -1 ? "reset-to-ready-sentinel" :
            before == 0 && after > 0 ? "armed" :
            after < before ? "decremented" :
            after == before ? "unchanged" : "increased";
    }

    internal static class LosslessGridCoordinateFormatter
    {
        public static string Format(IEnumerable<int> indices, int width)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            int[] ordered = indices.Distinct().OrderBy(value => value).ToArray();
            var result = new List<string>();
            int position = 0;
            while (position < ordered.Length)
            {
                int index = ordered[position];
                if (index < 0) throw new ArgumentOutOfRangeException(nameof(indices));
                int x = index / width;
                int beginY = index % width;
                int endY = beginY;
                position++;
                while (position < ordered.Length && ordered[position] / width == x &&
                    ordered[position] % width == endY + 1)
                {
                    endY++;
                    position++;
                }
                result.Add(beginY == endY ? $"({x},{beginY})" : $"({x},{beginY}-{endY})");
            }
            return string.Join(",", result);
        }
    }

    internal static class EconomySearchOutcome
    {
        public static string Classify(int searchCount, bool candidateFound, int constructionCount) =>
            constructionCount > 0 ? "construction-called" :
            searchCount == 0 ? "rejected-before-search" :
            candidateFound ? "candidate-without-construction" : "search-no-candidate";
    }

    internal static class AivAreaClassifier
    {
        public static bool Intersects(int originX, int originY, int size, int beginX, int beginY, int endX, int endY) =>
            endX >= originX && beginX < originX + size && endY >= originY && beginY < originY + size;
    }

    internal sealed class EarlyOwnerEventBuffer
    {
        private readonly Dictionary<int, List<string>> events = new Dictionary<int, List<string>>();
        private readonly int minimumOwnerId;
        private readonly int maximumOwnerId;

        public EarlyOwnerEventBuffer(int minimumOwnerId, int maximumOwnerId)
        {
            if (minimumOwnerId > maximumOwnerId) throw new ArgumentOutOfRangeException(nameof(minimumOwnerId));
            this.minimumOwnerId = minimumOwnerId;
            this.maximumOwnerId = maximumOwnerId;
        }

        public void Add(int ownerId, string value)
        {
            if (ownerId < minimumOwnerId || ownerId > maximumOwnerId) throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (!events.TryGetValue(ownerId, out List<string> ownerEvents))
            {
                ownerEvents = new List<string>();
                events.Add(ownerId, ownerEvents);
            }
            ownerEvents.Add(value);
        }

        public string[] Drain(int ownerId)
        {
            if (!events.TryGetValue(ownerId, out List<string> ownerEvents)) return Array.Empty<string>();
            events.Remove(ownerId);
            return ownerEvents.ToArray();
        }

        public int CountFor(int ownerId) => events.TryGetValue(ownerId, out List<string> ownerEvents) ? ownerEvents.Count : 0;

        public void Clear() => events.Clear();
    }

    internal sealed class DiagnosticCounterSet
    {
        private readonly SortedDictionary<string, long> interval = new SortedDictionary<string, long>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, long> total = new SortedDictionary<string, long>(StringComparer.Ordinal);
        private bool accepting = true;

        public void Add(string key, long amount = 1)
        {
            if (!accepting)
                return;
            AddTo(interval, key, amount);
            AddTo(total, key, amount);
        }

        public KeyValuePair<string, long>[] DrainInterval()
        {
            KeyValuePair<string, long>[] result = interval.ToArray();
            interval.Clear();
            return result;
        }

        public KeyValuePair<string, long>[] SnapshotTotal() => total.ToArray();

        public long TotalFor(string key) => total.TryGetValue(key, out long value) ? value : 0;

        public void Clear()
        {
            interval.Clear();
            total.Clear();
            accepting = true;
        }

        public void Stop() => accepting = false;

        private static void AddTo(IDictionary<string, long> target, string key, long amount)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A diagnostic key is required.", nameof(key));
            target[key] = checked((target.TryGetValue(key, out long value) ? value : 0) + amount);
        }
    }

    internal readonly struct SchedulerGateState
    {
        public SchedulerGateState(int activeAivSlot, int crushedCounter, int crushedDelay,
            int gold, int buildCounter, int buildRate, int pauseCounter, int currentStepGoal,
            int highestPreparedFrame)
        {
            ActiveAivSlot = activeAivSlot;
            CrushedCounter = crushedCounter;
            CrushedDelay = crushedDelay;
            Gold = gold;
            BuildCounter = buildCounter;
            BuildRate = buildRate;
            PauseCounter = pauseCounter;
            CurrentStepGoal = currentStepGoal;
            HighestPreparedFrame = highestPreparedFrame;
        }

        public int ActiveAivSlot { get; }
        public int CrushedCounter { get; }
        public int CrushedDelay { get; }
        public int Gold { get; }
        public int BuildCounter { get; }
        public int BuildRate { get; }
        public int PauseCounter { get; }
        public int CurrentStepGoal { get; }
        public int HighestPreparedFrame { get; }
    }

    internal static class SchedulerGateClassifier
    {
        // These thresholds are literal machine-contract values in Scheduler RVA 0x539B0.
        internal const int NormalGoldThresholdExclusive = 5001;
        internal const int FastGoldThresholdExclusive = 16001;

        public static string ClassifyBeforeCall(SchedulerGateState state)
        {
            if (state.ActiveAivSlot <= 0)
                return "inactive-aiv-slot";
            if (state.CrushedCounter != 0 && state.CrushedDelay <= 0)
                return "crushed-building-delay-unresolved-threshold";
            if (state.CrushedCounter != 0 && state.CrushedCounter + 1 < state.CrushedDelay)
                return "crushed-building-delay";
            if (state.Gold < NormalGoldThresholdExclusive && state.BuildCounter + 1 < state.BuildRate)
                return "build-rate";
            if (state.PauseCounter != 0)
                return "aiv-pause-countdown";
            if (state.HighestPreparedFrame <= 0)
                return "no-prepared-frames";
            if (state.CurrentStepGoal <= 0)
                return "step-goal-not-released";
            return "scheduler-work-eligible";
        }
    }

    internal sealed class FirstBuildingWindow
    {
        private readonly TimeSpan followUp;

        public FirstBuildingWindow(TimeSpan followUp) => this.followUp = followUp;

        public DateTime? ConfirmedAtUtc { get; private set; }

        public bool TryConfirm(DateTime nowUtc, bool executeSucceeded, bool matchingBuildingSpawn,
            bool frameStatusAdvanced)
        {
            // A frame transition alone can also be a wall, moat, or pure AIV command.
            // Only the low-level building-spawn event is a safe building confirmation.
            if (ConfirmedAtUtc.HasValue || !executeSucceeded || !matchingBuildingSpawn)
                return false;
            ConfirmedAtUtc = nowUtc;
            return true;
        }

        public bool FollowUpComplete(DateTime nowUtc) =>
            ConfirmedAtUtc.HasValue && nowUtc - ConfirmedAtUtc.Value >= followUp;
    }

    internal static class DiagnosticChunker
    {
        public static string[] Split(string value, int maximumLength)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (maximumLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumLength));
            if (value.Length == 0) return new[] { string.Empty };
            string[] result = new string[(value.Length + maximumLength - 1) / maximumLength];
            for (int index = 0; index < result.Length; index++)
            {
                int start = index * maximumLength;
                result[index] = value.Substring(start, Math.Min(maximumLength, value.Length - start));
            }
            return result;
        }
    }
}
