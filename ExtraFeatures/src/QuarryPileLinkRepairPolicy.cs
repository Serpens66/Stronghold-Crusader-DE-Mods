// Plans deterministic repairs for the bidirectional Quarry <-> stone-pile relationship.
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal readonly struct QuarryPileLinkCandidate
    {
        public QuarryPileLinkCandidate(
            int quarryId,
            int pileId,
            int currentPileQuarryId,
            bool isValid)
        {
            QuarryId = quarryId;
            PileId = pileId;
            CurrentPileQuarryId = currentPileQuarryId;
            IsValid = isValid;
        }

        public int QuarryId { get; }
        public int PileId { get; }
        public int CurrentPileQuarryId { get; }
        public bool IsValid { get; }
    }

    internal readonly struct QuarryPileLinkRepair
    {
        public QuarryPileLinkRepair(int quarryId, int pileId)
        {
            QuarryId = quarryId;
            PileId = pileId;
        }

        public int QuarryId { get; }
        public int PileId { get; }
    }

    internal readonly struct QuarryPileLinkRepairSummary
    {
        public QuarryPileLinkRepairSummary(
            int validPairs,
            int alreadyLinked,
            int plannedRepairs,
            int invalidCandidates,
            int conflictingPiles)
        {
            ValidPairs = validPairs;
            AlreadyLinked = alreadyLinked;
            PlannedRepairs = plannedRepairs;
            InvalidCandidates = invalidCandidates;
            ConflictingPiles = conflictingPiles;
        }

        public int ValidPairs { get; }
        public int AlreadyLinked { get; }
        public int PlannedRepairs { get; }
        public int InvalidCandidates { get; }
        public int ConflictingPiles { get; }
    }

    internal static class QuarryPileLinkRepairPolicy
    {
        public static QuarryPileLinkRepairSummary PlanRepairs(
            IReadOnlyList<QuarryPileLinkCandidate> candidates,
            ICollection<QuarryPileLinkRepair> repairs,
            ICollection<int> conflictingPileIds)
        {
            var claimCounts = new Dictionary<int, int>();
            int invalidCandidates = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                QuarryPileLinkCandidate candidate = candidates[index];
                if (!candidate.IsValid)
                {
                    invalidCandidates++;
                    continue;
                }

                claimCounts.TryGetValue(candidate.PileId, out int count);
                claimCounts[candidate.PileId] = count + 1;
            }

            var recordedConflicts = new HashSet<int>();
            int validPairs = 0;
            int alreadyLinked = 0;
            int plannedRepairs = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                QuarryPileLinkCandidate candidate = candidates[index];
                if (!candidate.IsValid)
                    continue;

                validPairs++;
                if (claimCounts[candidate.PileId] != 1)
                {
                    if (recordedConflicts.Add(candidate.PileId))
                        conflictingPileIds.Add(candidate.PileId);
                    continue;
                }

                if (candidate.CurrentPileQuarryId == candidate.QuarryId)
                {
                    alreadyLinked++;
                    continue;
                }

                repairs.Add(new QuarryPileLinkRepair(candidate.QuarryId, candidate.PileId));
                plannedRepairs++;
            }

            return new QuarryPileLinkRepairSummary(
                validPairs,
                alreadyLinked,
                plannedRepairs,
                invalidCandidates,
                recordedConflicts.Count);
        }
    }
}
