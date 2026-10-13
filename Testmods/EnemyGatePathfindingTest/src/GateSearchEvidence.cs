namespace EnemyGatePathfindingTest
{
    // TEMP_GATE_ROUTE_ACCEPTANCE: optional detailed window data, no persistent unit/tribe history.
    internal sealed class GateEvidenceAggregate
    {
        private readonly TemporaryGateAcceptanceAggregate rows = new TemporaryGateAcceptanceAggregate();
        internal long Total => rows.Total;
        internal void Record(int player, int gate, string stage, string result, int command,
            int tribe, int target1, int target2, string detail)
        {
            rows.Record("player=" + player + ",gate=" + gate + ",stage=" + stage +
                ",result=" + result + ",command=" + command,
                target1 + "/" + target2,
                "tribe=" + tribe + ",target=" + target1 + "/" + target2 + "," + detail);
        }
        internal string[] Drain() => rows.Drain();
        internal void Reset() => rows.Reset();
    }

    // Evidence only. Never used to choose a route, player, command or policy mask.
    internal static class GateSearchEvidence
    {
        internal static string Search(bool completed, bool success, long rejectedEdges) =>
            !completed ? "incomplete" : success
                ? (rejectedEdges > 0 ? "success-with-rejected-edges" : "success-without-rejected-edges")
                : (rejectedEdges > 0 ? "negative-with-rejected-edges" : "negative-without-rejected-edges");

        internal static string Comparison(bool nativeReject, bool effectiveReject,
            NativeGateSnapshotDecision decision) => (int)decision > (int)NativeGateSnapshotDecision.ExcludeForeignCapture
                ? "comparison-unclassified" : nativeReject && effectiveReject
                ? "native-rejection-preserved"
                : !nativeReject && effectiveReject && decision == NativeGateSnapshotDecision.ExcludeForeignCapture
                    ? "foreign-capture-acceptance-rejected"
                    : !nativeReject && !effectiveReject &&
                      (decision == NativeGateSnapshotDecision.PreserveOwner ||
                       decision == NativeGateSnapshotDecision.PreserveOwnerAlly ||
                       decision == NativeGateSnapshotDecision.PreserveCapturer ||
                       decision == NativeGateSnapshotDecision.PreserveCapturerAlly)
                        ? "permitted-acceptance-preserved" : "comparison-unclassified";

        internal static string Region(int source, int target) => source <= 0 || target <= 0
            ? "invalid-pcl-input" : source == target
                ? "same-pcl-shortcut-in-observed-region-call" : "different-pcl-region-call";
    }

    // Lives only inside an existing synchronous order frame. Parent merging is inclusive;
    // order totals must not be added to child totals as if they were different searches.
    internal struct GateOrderEvidence
    {
        internal long EarlyRejections, IndividualRejections, UnattributedRejections;
        internal long IndividualFilteredSearches, RegionCalls, SamePclCalls;
        internal void Comparison(bool changedToReject, bool individual, bool upstream)
        {
            if (!changedToReject) return;
            if (individual) IndividualRejections++;
            else if (upstream) EarlyRejections++;
            else UnattributedRejections++;
        }
        internal void Region(int source, int target)
        { RegionCalls++; if (source > 0 && source == target) SamePclCalls++; }
        internal void Merge(GateOrderEvidence child)
        {
            EarlyRejections += child.EarlyRejections; IndividualRejections += child.IndividualRejections;
            UnattributedRejections += child.UnattributedRejections;
            IndividualFilteredSearches += child.IndividualFilteredSearches;
            RegionCalls += child.RegionCalls; SamePclCalls += child.SamePclCalls;
        }
        internal string Classification => EarlyRejections > 0 ? "upstream-policy-rejection-observed"
            : IndividualRejections > 0 || IndividualFilteredSearches > 0
                ? "individual-filter-only-observed-upstream-effect-unproven"
                : UnattributedRejections > 0 ? "policy-rejection-stage-unknown" : "no-policy-rejection-observed";
        internal string Describe() => "evidence=" + Classification + ",earlyRejections=" + EarlyRejections +
            ",individualRejections=" + IndividualRejections + ",unattributedRejections=" + UnattributedRejections +
            ",individualFilteredSearches=" + IndividualFilteredSearches + ",regionCalls=" + RegionCalls +
            ",samePclRegionCalls=" + SamePclCalls + ",missingRegionCallMeans=unobserved-not-proven-skipped," +
            "counterfactualVanillaRoute=not-measured,commandExecuted=not-proven,scopeCounts=inclusive";
    }
}
