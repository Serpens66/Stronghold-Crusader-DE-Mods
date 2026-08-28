// Resolves and plans repairs for Vanilla's shared multi-building structure group.
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal enum QuarryPileVanillaGroupStatus
    {
        Valid,
        RepairMissingPileGroup,
        MissingQuarryGroup,
        ConflictingGroups
    }

    internal readonly struct QuarryPileVanillaGroupResolution
    {
        public QuarryPileVanillaGroupResolution(
            QuarryPileVanillaGroupStatus status,
            uint groupId)
        {
            Status = status;
            GroupId = groupId;
        }

        public QuarryPileVanillaGroupStatus Status { get; }
        public uint GroupId { get; }
        public bool CanUse => Status == QuarryPileVanillaGroupStatus.Valid ||
            Status == QuarryPileVanillaGroupStatus.RepairMissingPileGroup;
        public bool RepairsPileGroup => Status == QuarryPileVanillaGroupStatus.RepairMissingPileGroup;
    }

    internal readonly struct QuarryPileVanillaGroupCandidate
    {
        public QuarryPileVanillaGroupCandidate(
            int quarryId,
            int pileId,
            uint quarryGroupId,
            uint pileGroupId,
            ushort pileLegacyReverseLink,
            bool isValid)
        {
            QuarryId = quarryId;
            PileId = pileId;
            QuarryGroupId = quarryGroupId;
            PileGroupId = pileGroupId;
            PileLegacyReverseLink = pileLegacyReverseLink;
            IsValid = isValid;
        }

        public int QuarryId { get; }
        public int PileId { get; }
        public uint QuarryGroupId { get; }
        public uint PileGroupId { get; }
        public ushort PileLegacyReverseLink { get; }
        public bool IsValid { get; }
    }

    internal readonly struct QuarryPileVanillaGroupRepair
    {
        public QuarryPileVanillaGroupRepair(
            int quarryId,
            int pileId,
            uint groupId,
            bool assignPileGroup,
            bool clearLegacyReverseLink)
        {
            QuarryId = quarryId;
            PileId = pileId;
            GroupId = groupId;
            AssignPileGroup = assignPileGroup;
            ClearLegacyReverseLink = clearLegacyReverseLink;
        }

        public int QuarryId { get; }
        public int PileId { get; }
        public uint GroupId { get; }
        public bool AssignPileGroup { get; }
        public bool ClearLegacyReverseLink { get; }
    }

    internal readonly struct QuarryPileVanillaGroupRepairSummary
    {
        public QuarryPileVanillaGroupRepairSummary(
            int validPairs,
            int alreadyValid,
            int plannedRepairs,
            int invalidCandidates,
            int ambiguousPiles,
            int rejectedGroups)
        {
            ValidPairs = validPairs;
            AlreadyValid = alreadyValid;
            PlannedRepairs = plannedRepairs;
            InvalidCandidates = invalidCandidates;
            AmbiguousPiles = ambiguousPiles;
            RejectedGroups = rejectedGroups;
        }

        public int ValidPairs { get; }
        public int AlreadyValid { get; }
        public int PlannedRepairs { get; }
        public int InvalidCandidates { get; }
        public int AmbiguousPiles { get; }
        public int RejectedGroups { get; }
    }

    internal static class QuarryPileVanillaGroupPolicy
    {
        public static QuarryPileVanillaGroupResolution Resolve(uint quarryGroupId, uint pileGroupId)
        {
            if (quarryGroupId == 0)
            {
                return new QuarryPileVanillaGroupResolution(
                    QuarryPileVanillaGroupStatus.MissingQuarryGroup,
                    0);
            }

            if (pileGroupId == quarryGroupId)
            {
                return new QuarryPileVanillaGroupResolution(
                    QuarryPileVanillaGroupStatus.Valid,
                    quarryGroupId);
            }

            if (pileGroupId == 0)
            {
                return new QuarryPileVanillaGroupResolution(
                    QuarryPileVanillaGroupStatus.RepairMissingPileGroup,
                    quarryGroupId);
            }

            return new QuarryPileVanillaGroupResolution(
                QuarryPileVanillaGroupStatus.ConflictingGroups,
                0);
        }

        public static bool IsLegacyReverseLink(int quarryId, ushort pileLinkValue)
        {
            return quarryId > 0 &&
                quarryId <= ushort.MaxValue &&
                pileLinkValue == (ushort)quarryId;
        }

        public static QuarryPileVanillaGroupRepairSummary PlanRepairs(
            IReadOnlyList<QuarryPileVanillaGroupCandidate> candidates,
            ICollection<QuarryPileVanillaGroupRepair> repairs,
            ICollection<int> ambiguousPileIds)
        {
            var claimCounts = new Dictionary<int, int>();
            int invalidCandidates = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                QuarryPileVanillaGroupCandidate candidate = candidates[index];
                if (!candidate.IsValid)
                {
                    invalidCandidates++;
                    continue;
                }

                claimCounts.TryGetValue(candidate.PileId, out int count);
                claimCounts[candidate.PileId] = count + 1;
            }

            var recordedAmbiguities = new HashSet<int>();
            int validPairs = 0;
            int alreadyValid = 0;
            int plannedRepairs = 0;
            int rejectedGroups = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                QuarryPileVanillaGroupCandidate candidate = candidates[index];
                if (!candidate.IsValid)
                    continue;

                validPairs++;
                if (claimCounts[candidate.PileId] != 1)
                {
                    if (recordedAmbiguities.Add(candidate.PileId))
                        ambiguousPileIds.Add(candidate.PileId);
                    continue;
                }

                QuarryPileVanillaGroupResolution resolution = Resolve(
                    candidate.QuarryGroupId,
                    candidate.PileGroupId);
                if (!resolution.CanUse)
                {
                    rejectedGroups++;
                    continue;
                }

                bool clearLegacyReverseLink = IsLegacyReverseLink(
                    candidate.QuarryId,
                    candidate.PileLegacyReverseLink);
                if (!resolution.RepairsPileGroup && !clearLegacyReverseLink)
                {
                    alreadyValid++;
                    continue;
                }

                repairs.Add(new QuarryPileVanillaGroupRepair(
                    candidate.QuarryId,
                    candidate.PileId,
                    resolution.GroupId,
                    resolution.RepairsPileGroup,
                    clearLegacyReverseLink));
                plannedRepairs++;
            }

            return new QuarryPileVanillaGroupRepairSummary(
                validPairs,
                alreadyValid,
                plannedRepairs,
                invalidCandidates,
                recordedAmbiguities.Count,
                rejectedGroups);
        }
    }
}
