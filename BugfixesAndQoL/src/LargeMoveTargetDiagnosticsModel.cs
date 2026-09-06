using System;
using System.Collections.Generic;

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
        public int Deviated { get; set; }
        public int MaximumManhattan { get; set; }
        public int MaximumChebyshev { get; set; }
        public IReadOnlyList<string> Examples { get; set; }
    }

    internal static class LargeMoveTargetDiagnosticsModel
    {
        public const int MinimumTrackedUnits = MoveFormationCommandSnapshotStore.MinimumTrackedUnits;
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
            var plannedCounts = new Dictionary<MoveTargetCoordinate, int>();
            var actualCounts = new Dictionary<MoveTargetCoordinate, int>();
            var settledCounts = new Dictionary<MoveTargetCoordinate, int>();
            var examples = new List<string>(3);
            int actualCount = 0;

            foreach (MoveTargetOutcome outcome in outcomes)
            {
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

                actualCount++;
                AddCount(actualCounts, outcome.Actual);
                if (outcome.Kind == MoveTargetOutcomeKind.Exact ||
                    outcome.Kind == MoveTargetOutcomeKind.SettledElsewhere)
                {
                    AddCount(settledCounts, outcome.Actual);
                }
                int dx = Math.Abs(outcome.Actual.X - outcome.Planned.X);
                int dy = Math.Abs(outcome.Actual.Y - outcome.Planned.Y);
                int manhattan = dx + dy;
                int chebyshev = Math.Max(dx, dy);
                summary.MaximumManhattan = Math.Max(summary.MaximumManhattan, manhattan);
                summary.MaximumChebyshev = Math.Max(summary.MaximumChebyshev, chebyshev);
                if (outcome.Kind != MoveTargetOutcomeKind.Exact)
                    AddExample(examples, outcome);
            }

            foreach (KeyValuePair<MoveTargetCoordinate, int> pair in plannedCounts)
            {
                if (settledCounts.TryGetValue(pair.Key, out int count))
                    summary.CollectiveMatches += Math.Min(pair.Value, count);
            }

            summary.Reassigned = Math.Max(0, summary.CollectiveMatches - summary.Exact);
            summary.Deviated = Math.Max(0, summary.SettledElsewhere - summary.Reassigned);
            summary.PlannedUnique = plannedCounts.Count;
            summary.ActualUnique = actualCounts.Count;
            summary.PlannedDuplicates = outcomes.Count - plannedCounts.Count;
            summary.ActualDuplicates = actualCount - actualCounts.Count;
            summary.Examples = examples;
            return summary;
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
            if (examples.Count >= 3)
                return;
            examples.Add(
                $"u{outcome.UnitId}/g{outcome.GlobalId}:{outcome.Planned}->{outcome.Actual}/{outcome.Kind}");
        }

    }
}
