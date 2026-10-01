using System;
using static BugfixesAndQoL.RaidAttackFieldEvaluation;

namespace BugfixesAndQoL
{
    // Pure association state: no native writes and no command decisions.
    internal sealed class RaidSearchEvidence
    {
        internal readonly long Session, Epoch, Sequence;
        internal readonly int ThreadId, TribeId, BuildingId;
        internal readonly int PlayerId, RaidRole;
        internal readonly uint TribeGlobalId, BuildingGlobalId;
        internal readonly IntPtr Context;
        internal int Observations;
        internal string Association = "searchCompletionNotObserved";
        internal AttackCandidateSnapshot DecisionSnapshot;

        internal RaidSearchEvidence(long session, long epoch, long sequence, int threadId,
            int tribeId, uint tribeGlobalId, int buildingId, uint buildingGlobalId, IntPtr context,
            int playerId = -1, int raidRole = -1)
        {
            Session = session; Epoch = epoch; Sequence = sequence; ThreadId = threadId;
            TribeId = tribeId; TribeGlobalId = tribeGlobalId;
            BuildingId = buildingId; BuildingGlobalId = buildingGlobalId; Context = context;
            PlayerId = playerId; RaidRole = raidRole;
        }

        internal void Observe(long session, long epoch, int threadId, int tribeId,
            uint tribeGlobalId, int buildingId, uint buildingGlobalId, IntPtr context,
            AttackCandidateSnapshot snapshot, int playerId = -1, int raidRole = -1)
        {
            Observations++;
            if (Observations != 1) { Association = "multipleSearchCompletions"; return; }
            if (Session != session || Epoch != epoch) Association = "sessionOrEpochChanged";
            else if (ThreadId != threadId) Association = "threadMismatch";
            else if (TribeId != tribeId || TribeGlobalId != tribeGlobalId) Association = "tribeIdentityMismatch";
            else if (BuildingId != buildingId || BuildingGlobalId != buildingGlobalId) Association = "buildingIdentityMismatch";
            else if (PlayerId != playerId || RaidRole != raidRole) Association = "raidRoleOrOwnerMismatch";
            else if (Context == IntPtr.Zero || context == IntPtr.Zero) Association = "contextUnavailable";
            else if (Context != context) Association = "contextMismatch";
            else { Association = "matchedNativeConsumerReturn"; DecisionSnapshot = snapshot; }
        }

        internal string GetFreshness(bool prePostMatch, AttackCandidateSnapshot post)
        {
            if (!prePostMatch) return "unmatchedPrePost";
            if (Association != "matchedNativeConsumerReturn") return Association;
            if (DecisionSnapshot == null || !DecisionSnapshot.SameRecords(post))
                return "decisionPostSnapshotMismatch";
            return "nativeSearchObserved";
        }

        internal string Describe(bool prePostMatch, AttackCandidateSnapshot post)
        {
            string freshness = GetFreshness(prePostMatch, post);
            return $"sequence={Sequence},thread={ThreadId},epoch={Epoch},commandPlayer={PlayerId},commandRole={RaidRole},context=0x{Context.ToInt64():X}," +
                $"searchObserved={Observations > 0},searchCompletions={Observations}," +
                $"searchAssociation={freshness},decision=[" +
                (DecisionSnapshot == null ? "unavailable" : DecisionSnapshot.DescribeCompact()) + "]";
        }
    }
}
