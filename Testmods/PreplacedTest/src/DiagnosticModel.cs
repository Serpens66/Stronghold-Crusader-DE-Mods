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

    internal enum WallOwnerEncoding
    {
        Unresolved,
        OneBased,
        ZeroBased
    }

    internal static class WallOwnerEncodingResolver
    {
        public static WallOwnerEncoding Resolve(int oneBasedMatches, int zeroBasedMatches)
        {
            if (oneBasedMatches <= 0 && zeroBasedMatches <= 0) return WallOwnerEncoding.Unresolved;
            if (oneBasedMatches == zeroBasedMatches) return WallOwnerEncoding.Unresolved;
            return oneBasedMatches > zeroBasedMatches ? WallOwnerEncoding.OneBased : WallOwnerEncoding.ZeroBased;
        }

        public static int Decode(byte rawOwner, WallOwnerEncoding encoding) =>
            encoding == WallOwnerEncoding.OneBased ? rawOwner :
            encoding == WallOwnerEncoding.ZeroBased ? rawOwner + 1 : 0;
    }

    internal static class WallBreachConfirmation
    {
        public static bool IsConfirmed(bool baselineWallLost, int oldInsidePcl, int oldOutsidePcl,
            int newInsidePcl, int newOutsidePcl) =>
            baselineWallLost && oldInsidePcl > 0 && oldOutsidePcl > 0 &&
            oldInsidePcl != oldOutsidePcl && newInsidePcl > 0 && newInsidePcl == newOutsidePcl;
    }

    internal static class LegacyTimerFixEligibility
    {
        public static string Classify(bool isAi, bool isSave, int mapVersion, int legacyVersionExclusive,
            int sourceBefore, int sourceAfter, int destinationBefore, int destinationAfter)
        {
            if (!isAi) return "ineligible-non-ai";
            if (isSave) return "ineligible-loaded-save";
            if (mapVersion < 0 || mapVersion >= legacyVersionExclusive) return "ineligible-not-legacy-conversion";
            if (destinationBefore != 0) return "ineligible-runtime-timer-already-active";
            if (sourceBefore != 1 || sourceAfter != 1) return "ineligible-no-stable-single-activation-source";
            if (destinationAfter != sourceAfter) return "ineligible-not-copied-exclusively-from-serialized-source";
            return "eligible-fresh-map-legacy-transfer";
        }

        public static bool IsEligible(string classification) =>
            string.Equals(classification, "eligible-fresh-map-legacy-transfer", StringComparison.Ordinal);
    }

    internal enum ShadowEconomySearchKind
    {
        Farm,
        Resource,
        Wood,
        Nearby
    }

    internal readonly struct ShadowEconomyCell
    {
        public ShadowEconomyCell(int projected04, int raw16, byte raw07, byte raw08, byte raw09,
            byte raw0A, byte raw0B, byte raw0C, byte raw0D, byte raw0E, byte raw0F,
            byte raw11, byte raw12, byte raw13, byte raw15, bool ownerClassMatches = true)
        {
            Projected04 = projected04; Raw16 = raw16; Raw07 = raw07; Raw08 = raw08;
            Raw09 = raw09; Raw0A = raw0A; Raw0B = raw0B; Raw0C = raw0C; Raw0D = raw0D;
            Raw0E = raw0E; Raw0F = raw0F; Raw11 = raw11; Raw12 = raw12;
            Raw13 = raw13; Raw15 = raw15; OwnerClassMatches = ownerClassMatches;
        }

        public int Projected04 { get; }
        public int Raw16 { get; }
        public byte Raw07 { get; }
        public byte Raw08 { get; }
        public byte Raw09 { get; }
        public byte Raw0A { get; }
        public byte Raw0B { get; }
        public byte Raw0C { get; }
        public byte Raw0D { get; }
        public byte Raw0E { get; }
        public byte Raw0F { get; }
        public byte Raw11 { get; }
        public byte Raw12 { get; }
        public byte Raw13 { get; }
        public byte Raw15 { get; }
        public bool OwnerClassMatches { get; }
    }

    internal sealed class ShadowEconomySearchResult
    {
        public ShadowEconomySearchResult(int reachableCount, int[] reachedIndices,
            int[] blockedIndices, int[] candidateIndices)
        {
            ReachableCount = reachableCount; ReachedIndices = reachedIndices;
            BlockedIndices = blockedIndices; CandidateIndices = candidateIndices;
        }
        public int ReachableCount { get; }
        public int[] ReachedIndices { get; }
        public int[] BlockedIndices { get; }
        public int[] CandidateIndices { get; }
        public int FirstCandidateIndex => CandidateIndices.Length == 0 ? -1 : CandidateIndices[0];
    }

    internal static class ShadowEconomySearch
    {
        public static ShadowEconomySearchResult Run(ShadowEconomyCell[] cells, int width, int startIndex,
            ShadowEconomySearchKind kind, int resourceMode)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (width <= 0 || cells.Length != width * width) throw new ArgumentOutOfRangeException(nameof(width));
            if ((uint)startIndex >= (uint)cells.Length) throw new ArgumentOutOfRangeException(nameof(startIndex));
            var visited = new bool[cells.Length];
            var depths = new byte[cells.Length];
            var queue = new Queue<int>();
            var blocked = new HashSet<int>();
            var candidates = new List<int>();
            visited[startIndex] = true;
            depths[startIndex] = 1;
            queue.Enqueue(startIndex);
            while (queue.Count != 0)
            {
                int current = queue.Dequeue();
                if (depths[current] > 60) break;
                int x = current / width;
                int y = current % width;
                foreach (int next in Neighbors(x, y, width, kind == ShadowEconomySearchKind.Nearby))
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    ShadowEconomyCell cell = cells[next];
                    if (!CanExpand(cell, kind))
                    {
                        blocked.Add(next);
                        continue;
                    }
                    depths[next] = checked((byte)(depths[current] + 1));
                    queue.Enqueue(next);
                    if (IsCandidate(cell, kind, resourceMode)) candidates.Add(next);
                }
            }
            int[] reached = Enumerable.Range(0, visited.Length)
                .Where(index => visited[index] && !blocked.Contains(index)).ToArray();
            return new ShadowEconomySearchResult(reached.Length, reached,
                blocked.OrderBy(value => value).ToArray(), candidates.ToArray());
        }

        private static IEnumerable<int> Neighbors(int x, int y, int width, bool includeDiagonals)
        {
            if (y > 0) yield return x * width + y - 1;
            if (includeDiagonals && x + 1 < width && y > 0) yield return (x + 1) * width + y - 1;
            if (x + 1 < width) yield return (x + 1) * width + y;
            if (includeDiagonals && x + 1 < width && y + 1 < width) yield return (x + 1) * width + y + 1;
            if (y + 1 < width) yield return x * width + y + 1;
            if (includeDiagonals && x > 0 && y + 1 < width) yield return (x - 1) * width + y + 1;
            if (x > 0) yield return (x - 1) * width + y;
            if (includeDiagonals && x > 0 && y > 0) yield return (x - 1) * width + y - 1;
        }

        private static bool CanExpand(ShadowEconomyCell cell, ShadowEconomySearchKind kind)
        {
            if (kind == ShadowEconomySearchKind.Farm) return cell.Projected04 < 17;
            if (kind == ShadowEconomySearchKind.Wood) return cell.Projected04 < 16 && cell.Raw13 == 0;
            if (kind == ShadowEconomySearchKind.Nearby) return cell.Projected04 < 15;
            return cell.Projected04 - cell.Raw16 < 16;
        }

        private static bool IsCandidate(ShadowEconomyCell cell, ShadowEconomySearchKind kind, int resourceMode)
        {
            if (kind == ShadowEconomySearchKind.Wood)
                return cell.Projected04 < 6 && cell.Raw07 > 0 && cell.Raw13 == 0;
            if (kind == ShadowEconomySearchKind.Nearby)
                return cell.Projected04 == 0 && cell.Raw0E == 0 && cell.Raw0F == 0 &&
                    cell.Raw13 == 0 && cell.Raw07 == 0 && cell.Raw08 == 0;
            if (kind == ShadowEconomySearchKind.Farm)
                return cell.Projected04 == 0 && cell.Raw0F == 0 && cell.Raw07 == 0 &&
                    cell.Raw13 == 0 && cell.Raw11 > 24 && cell.Raw12 > 13;
            if (ResourceCandidateRejectionReason(cell, resourceMode) != "candidate") return false;
            return true;
        }

        public static string ResourceCandidateRejectionReason(ShadowEconomyCell cell, int resourceMode)
        {
            switch (ResourceCandidateRejectionCode(cell, resourceMode))
            {
                case 0: return "candidate";
                case 1: return "pcl-difference";
                case 2: return "occupied-or-reserved-byte+0F";
                case 3: return "blocked-byte+13";
                case 4: return "owner-class-mismatch-byte+15";
                case 5: return "quarry-density-byte+08";
                case 6: return "quarry-height";
                case 7: return "iron-density-byte+09";
                case 8: return "iron-height";
                case 9: return "pitch-density-byte+0A";
                case 10: return "pitch-density-byte+0B";
                case 11: return "pitch-height";
                default: return "unsupported-resource-mode";
            }
        }

        // Numeric codes keep full-grid signatures allocation-free while preserving the first native rejection branch.
        public static int ResourceCandidateRejectionCode(ShadowEconomyCell cell, int resourceMode)
        {
            int pclDifference = cell.Projected04 - cell.Raw16;
            if (cell.Projected04 != cell.Raw16 && (resourceMode != 3 || pclDifference >= 5))
                return 1;
            if (cell.Raw0F != 0) return 2;
            if (cell.Raw13 != 0) return 3;
            if (cell.Raw15 != 0 && !cell.OwnerClassMatches) return 4;
            int heightDifference = unchecked((int)(uint)cell.Raw0D - (int)(uint)cell.Raw0C);
            if (resourceMode == 2)
            {
                if ((sbyte)cell.Raw08 <= 7) return 5;
                return heightDifference < 40 ? 0 : 6;
            }
            if (resourceMode == 3)
            {
                if ((sbyte)cell.Raw09 <= 6) return 7;
                return heightDifference < 30 ? 0 : 8;
            }
            if (resourceMode == 4)
            {
                if ((sbyte)cell.Raw0A <= 2) return 9;
                if ((sbyte)cell.Raw0B <= 9) return 10;
                return heightDifference < 12 ? 0 : 11;
            }
            return 12;
        }

        public static string ResourceTerrainReason(ShadowEconomyCell cell, int resourceMode)
        {
            int heightDifference = unchecked((int)(uint)cell.Raw0D - (int)(uint)cell.Raw0C);
            if (resourceMode == 2)
            {
                if ((sbyte)cell.Raw08 <= 7) return "quarry-density-byte+08";
                return heightDifference < 40 ? "candidate" : "quarry-height";
            }
            if (resourceMode == 3)
            {
                if ((sbyte)cell.Raw09 <= 6) return "iron-density-byte+09";
                return heightDifference < 30 ? "candidate" : "iron-height";
            }
            if (resourceMode == 4)
            {
                if ((sbyte)cell.Raw0A <= 2) return "pitch-density-byte+0A";
                if ((sbyte)cell.Raw0B <= 9) return "pitch-density-byte+0B";
                return heightDifference < 12 ? "candidate" : "pitch-height";
            }
            return "unsupported-resource-mode";
        }

        public static string DescribeResourceChecks(ShadowEconomyCell cell, int resourceMode)
        {
            int difference = cell.Projected04 - cell.Raw16;
            int heightDifference = unchecked((int)(uint)cell.Raw0D - (int)(uint)cell.Raw0C);
            return $"mode={resourceMode}/projected04={cell.Projected04}/raw16={cell.Raw16}" +
                $"/pclExact={cell.Projected04 == cell.Raw16}/ironTolerance={resourceMode == 3 && difference < 5}" +
                $"/raw0F={cell.Raw0F}/raw13={cell.Raw13}/raw15={cell.Raw15}/ownerClassMatches={cell.OwnerClassMatches}" +
                $"/density07={unchecked((sbyte)cell.Raw07)}/density08={unchecked((sbyte)cell.Raw08)}" +
                $"/density09={unchecked((sbyte)cell.Raw09)}/density0A={unchecked((sbyte)cell.Raw0A)}" +
                $"/density0B={unchecked((sbyte)cell.Raw0B)}/height={heightDifference}" +
                $"/firstRejection={ResourceCandidateRejectionReason(cell, resourceMode)}";
        }
    }

    internal static class EconomySearchGateClassifier
    {
        public static string Classify(bool generationChanged, int cooldownBefore, int cooldownAfter,
            int queueReadBefore, int queueWriteBefore, int queueReadAfter, int queueWriteAfter,
            int resultXBefore, int resultYBefore, int resultXAfter, int resultYAfter)
        {
            if (generationChanged) return "traversal-performed";
            if (cooldownBefore > 0 || cooldownAfter > 0) return "early-return-cooldown";
            if (queueReadBefore != queueWriteBefore || queueReadAfter != queueWriteAfter)
                return "early-return-shared-queue-pending";
            if (resultXAfter >= 0 && resultYAfter >= 0)
                return resultXBefore == resultXAfter && resultYBefore == resultYAfter ?
                    "early-return-cached-result" : "early-return-result-without-traversal";
            return "early-return-unresolved-mode-or-state";
        }
    }

    internal static class ChoreTransferDirection
    {
        public static string Classify(int direction) => direction == 0 ? "runtime-to-buffer" :
            direction == 1 ? "buffer-to-runtime" : "no-transfer-or-unknown";
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
