using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BugfixesAndQoL
{
    internal readonly struct MoveTargetCoordinate : IEquatable<MoveTargetCoordinate>, IComparable<MoveTargetCoordinate>
    {
        public MoveTargetCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public int CompareTo(MoveTargetCoordinate other)
        {
            int y = Y.CompareTo(other.Y);
            return y != 0 ? y : X.CompareTo(other.X);
        }

        public bool Equals(MoveTargetCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is MoveTargetCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"{X},{Y}";
    }

    internal enum MoveTargetOutcomeKind
    {
        Moving,
        Exact,
        SettledElsewhere,
        Interrupted,
        Lost
    }

    internal readonly struct MoveTargetOutcome
    {
        public MoveTargetOutcome(
            int unitId,
            uint globalId,
            MoveTargetCoordinate planned,
            MoveTargetCoordinate actual,
            MoveTargetOutcomeKind kind)
        {
            UnitId = unitId;
            GlobalId = globalId;
            Planned = planned;
            Actual = actual;
            Kind = kind;
        }

        public int UnitId { get; }
        public uint GlobalId { get; }
        public MoveTargetCoordinate Planned { get; }
        public MoveTargetCoordinate Actual { get; }
        public MoveTargetOutcomeKind Kind { get; }
        public bool HasActualPosition => Kind != MoveTargetOutcomeKind.Lost;
    }

    internal sealed class MoveTargetComparisonSummary
    {
        public int Total { get; set; }
        public int Exact { get; set; }
        public int Reassigned { get; set; }
        public int SettledElsewhere { get; set; }
        public int Interrupted { get; set; }
        public int Lost { get; set; }
        public int Moving { get; set; }
        public int PlannedUnique { get; set; }
        public int ActualUnique { get; set; }
        public int PlannedDuplicates { get; set; }
        public int ActualDuplicates { get; set; }
        public int CollectiveMatches { get; set; }
        public double AverageManhattan { get; set; }
        public int MaximumManhattan { get; set; }
        public double AverageChebyshev { get; set; }
        public int MaximumChebyshev { get; set; }
        public string PlannedFingerprint { get; set; }
        public string ActualFingerprint { get; set; }
        public string PlannedBounds { get; set; }
        public string ActualBounds { get; set; }
        public IReadOnlyList<string> Examples { get; set; }
    }

    internal static class LargeMoveTargetDiagnosticsModel
    {
        public const int MinimumTrackedUnits = 200;
        public const int VanillaDrawCapacity = 0xFA;
        public const int RequiredStableIdleTicks = 3;

        public static bool ShouldTrack(int unitCount) => unitCount >= MinimumTrackedUnits;

        public static bool IsVanillaMoveTargetMarker(
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int flags)
        {
            return category == 0x6B && spriteId >= 0x52 && spriteId <= 0x59 &&
                layer == 0xC && verticalOffset == 6 && (flags & 0x1FFFF) == 2;
        }

        public static MoveTargetComparisonSummary Compare(IReadOnlyList<MoveTargetOutcome> outcomes)
        {
            if (outcomes == null)
                throw new ArgumentNullException(nameof(outcomes));

            var summary = new MoveTargetComparisonSummary { Total = outcomes.Count };
            var planned = new List<MoveTargetCoordinate>(outcomes.Count);
            var actual = new List<MoveTargetCoordinate>(outcomes.Count);
            var plannedCounts = new Dictionary<MoveTargetCoordinate, int>();
            var actualCounts = new Dictionary<MoveTargetCoordinate, int>();
            var examples = new List<string>(8);
            long manhattanTotal = 0;
            long chebyshevTotal = 0;
            int distanceCount = 0;

            foreach (MoveTargetOutcome outcome in outcomes)
            {
                planned.Add(outcome.Planned);
                AddCount(plannedCounts, outcome.Planned);
                switch (outcome.Kind)
                {
                    case MoveTargetOutcomeKind.Exact:
                        summary.Exact++;
                        break;
                    case MoveTargetOutcomeKind.SettledElsewhere:
                        summary.SettledElsewhere++;
                        break;
                    case MoveTargetOutcomeKind.Interrupted:
                        summary.Interrupted++;
                        break;
                    case MoveTargetOutcomeKind.Lost:
                        summary.Lost++;
                        break;
                    default:
                        summary.Moving++;
                        break;
                }

                if (!outcome.HasActualPosition)
                {
                    AddExample(examples, outcome);
                    continue;
                }

                actual.Add(outcome.Actual);
                AddCount(actualCounts, outcome.Actual);
                int dx = Math.Abs(outcome.Actual.X - outcome.Planned.X);
                int dy = Math.Abs(outcome.Actual.Y - outcome.Planned.Y);
                int manhattan = dx + dy;
                int chebyshev = Math.Max(dx, dy);
                manhattanTotal += manhattan;
                chebyshevTotal += chebyshev;
                distanceCount++;
                summary.MaximumManhattan = Math.Max(summary.MaximumManhattan, manhattan);
                summary.MaximumChebyshev = Math.Max(summary.MaximumChebyshev, chebyshev);
                if (outcome.Kind != MoveTargetOutcomeKind.Exact)
                    AddExample(examples, outcome);
            }

            foreach (KeyValuePair<MoveTargetCoordinate, int> pair in plannedCounts)
            {
                if (actualCounts.TryGetValue(pair.Key, out int count))
                    summary.CollectiveMatches += Math.Min(pair.Value, count);
            }

            summary.Reassigned = Math.Max(0, summary.CollectiveMatches - summary.Exact);
            summary.PlannedUnique = plannedCounts.Count;
            summary.ActualUnique = actualCounts.Count;
            summary.PlannedDuplicates = planned.Count - plannedCounts.Count;
            summary.ActualDuplicates = actual.Count - actualCounts.Count;
            summary.AverageManhattan = distanceCount == 0 ? 0 : (double)manhattanTotal / distanceCount;
            summary.AverageChebyshev = distanceCount == 0 ? 0 : (double)chebyshevTotal / distanceCount;
            summary.PlannedFingerprint = Fingerprint(planned);
            summary.ActualFingerprint = Fingerprint(actual);
            summary.PlannedBounds = Bounds(planned);
            summary.ActualBounds = Bounds(actual);
            summary.Examples = examples;
            return summary;
        }

        public static string Fingerprint(IEnumerable<MoveTargetCoordinate> coordinates)
        {
            MoveTargetCoordinate[] sorted = coordinates.OrderBy(value => value).ToArray();
            ulong hash = 14695981039346656037UL;
            foreach (MoveTargetCoordinate coordinate in sorted)
            {
                hash = Mix(hash, unchecked((uint)coordinate.X));
                hash = Mix(hash, unchecked((uint)coordinate.Y));
            }
            hash = Mix(hash, unchecked((uint)sorted.Length));
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        public static string Bounds(IReadOnlyList<MoveTargetCoordinate> coordinates)
        {
            if (coordinates.Count == 0)
                return "none";
            int minX = coordinates[0].X;
            int maxX = minX;
            int minY = coordinates[0].Y;
            int maxY = minY;
            for (int index = 1; index < coordinates.Count; index++)
            {
                MoveTargetCoordinate value = coordinates[index];
                minX = Math.Min(minX, value.X);
                maxX = Math.Max(maxX, value.X);
                minY = Math.Min(minY, value.Y);
                maxY = Math.Max(maxY, value.Y);
            }
            return $"{minX},{minY}-{maxX},{maxY}";
        }

        private static void AddCount(
            IDictionary<MoveTargetCoordinate, int> counts,
            MoveTargetCoordinate coordinate)
        {
            counts.TryGetValue(coordinate, out int count);
            counts[coordinate] = count + 1;
        }

        private static void AddExample(ICollection<string> examples, MoveTargetOutcome outcome)
        {
            if (examples.Count >= 8)
                return;
            examples.Add(
                $"u{outcome.UnitId}/g{outcome.GlobalId}:{outcome.Planned}->{outcome.Actual}/{outcome.Kind}");
        }

        private static ulong Mix(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
                hash = (hash ^ ((value >> shift) & 0xFF)) * 1099511628211UL;
            return hash;
        }
    }
}
