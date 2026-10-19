namespace BugfixesAndQoL.GateRouting
{
    // Evidence only. Never used to choose a route, player, command or policy mask.
    internal static class GateSearchEvidence
    {
        // Classification of existing topology data, never a path query or a policy input.
        internal static string TopologyCase(int entry, int exit, bool hasBridge) => hasBridge
            ? "bridge-present-not-isolated" : entry <= 0 || exit <= 0
                ? "pcl-unavailable" : entry == exit
                    ? "same-pcl-gate-candidate-query-and-route-unproven"
                    : "different-pcl-gate-ordinary-native-check";

        internal static string Search(bool completed, bool success, long rejectedEdges) =>
            !completed ? "incomplete" : success
                ? (rejectedEdges > 0 ? "success-with-rejected-edges" : "success-without-rejected-edges")
                : (rejectedEdges > 0 ? "negative-with-rejected-edges" : "negative-without-rejected-edges");

        internal static string Comparison(bool nativeReject, bool effectiveReject,
            NativeGateSnapshotDecision decision) => (int)decision > (int)NativeGateSnapshotDecision.ExcludeAccessRule
                ? "comparison-unclassified" : nativeReject && effectiveReject
                ? "native-rejection-preserved"
                : !nativeReject && effectiveReject && decision == NativeGateSnapshotDecision.ExcludeAccessRule
                    ? "access-rule-acceptance-rejected"
                : !nativeReject && effectiveReject && decision == NativeGateSnapshotDecision.ExcludeForeignCapture
                    ? "foreign-capture-acceptance-rejected"
                    : !nativeReject && !effectiveReject &&
                      (decision == NativeGateSnapshotDecision.PreserveOwner ||
                       decision == NativeGateSnapshotDecision.PreserveOwnerAlly ||
                       decision == NativeGateSnapshotDecision.PreserveCapturer ||
                       decision == NativeGateSnapshotDecision.PreserveCapturerAlly ||
                       decision == NativeGateSnapshotDecision.PreserveAccessRule)
                        ? "permitted-acceptance-preserved" : "comparison-unclassified";

        internal static string Region(int source, int target) => source <= 0 || target <= 0
            ? "invalid-pcl-input" : source == target
                ? "same-pcl-shortcut-in-observed-region-call" : "different-pcl-region-call";
    }
}
