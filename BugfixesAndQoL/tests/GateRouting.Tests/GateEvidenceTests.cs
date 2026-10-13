using APIShared.GateRouting.Native;
using System;

namespace BugfixesAndQoL.GateRouting
{
    internal static class GateEvidenceTests
    {
        internal static int Run()
        {
            int assertions = 0;
            Action<bool, string> check = (ok, reason) => { assertions++; if (!ok) throw new Exception(reason); };
            check(EnemyGatePathfindingPolicy.ObservationVerdict(100000) == DiagnosticVerdict.OBSERVED, "coverage is not acceptance");
            check(GateSearchEvidence.Search(true, true, 1000) == "success-with-rejected-edges", "no counterfactual detour claim");
            check(GateSearchEvidence.Search(true, false, 0) == "negative-without-rejected-edges", "unfiltered negative");
            check(GateSearchEvidence.Search(true, false, 3) == "negative-with-rejected-edges", "filtered negative without cause claim");
            check(GateSearchEvidence.Search(false, false, 3) == "incomplete", "exception is not NoRoute");
            check(GateSearchEvidence.Comparison(true, true, NativeGateSnapshotDecision.PreserveUncaptured) == "native-rejection-preserved", "vanilla already rejects hostile uncaptured gate");
            check(GateSearchEvidence.Comparison(false, true, NativeGateSnapshotDecision.ExcludeForeignCapture) == "foreign-capture-acceptance-rejected", "actual changed local comparison");
            check(GateSearchEvidence.Comparison(false, false, NativeGateSnapshotDecision.PreserveCapturerAlly) == "permitted-acceptance-preserved", "capturer ally allowed");
            check(GateSearchEvidence.Comparison(false, false, NativeGateSnapshotDecision.CaptureMismatch) == "comparison-unclassified", "snapshot conflict is not success");
            check(GateSearchEvidence.Region(1, 1) == "same-pcl-shortcut-in-observed-region-call", "actual equal PCL call");
            check(GateSearchEvidence.Region(0, 0) == "invalid-pcl-input", "zero is not valid equality");
            check(GateSearchEvidence.Region(1, 2) == "different-pcl-region-call", "region traversal entered");
            check(GateSearchEvidence.TopologyCase(1, 1, false) == "same-pcl-gate-candidate-query-and-route-unproven", "topology candidate is not a proven query or violation");
            check(GateSearchEvidence.TopologyCase(1, 2, false) == "different-pcl-gate-ordinary-native-check", "normal native region case distinguished");
            check(GateSearchEvidence.TopologyCase(0, 0, false) == "pcl-unavailable", "unknown topology not equality");
            check(GateSearchEvidence.TopologyCase(1, 1, true) == "bridge-present-not-isolated", "bridge excluded from isolated gate experiment");
            var parent = new GateOrderEvidence();
            parent.Comparison(false, false, true);
            check(parent.Classification == "no-policy-rejection-observed", "native reject does not prove mod effect");
            var child = new GateOrderEvidence { IndividualFilteredSearches = 1 };
            child.Region(1, 1); child.Comparison(true, true, false); parent.Merge(child);
            check(parent.Classification == "individual-filter-only-observed-upstream-effect-unproven", "nested single route does not prove earlier planning correction");
            check(parent.RegionCalls == 1 && parent.SamePclCalls == 1 && parent.IndividualRejections == 1, "inclusive nested counts exact");
            parent.Comparison(true, false, true);
            check(parent.Classification == "upstream-policy-rejection-observed", "changed early comparison retained");
            check(parent.Describe().Contains("commandExecuted=not-proven"), "order post does not prove execution");
            parent = default;
            check(parent.RegionCalls == 0 && parent.Describe().Contains("unobserved-not-proven-skipped"), "fresh context cannot inherit missing graph explanation");
            var aggregate = new GateEvidenceAggregate();
            const int variants = 64, repeats = 2000;
            for (int i = 0; i < repeats; i++)
                for (int gate = 1; gate <= variants; gate++)
                    aggregate.Record(1 + gate % 8, gate, "graph-gate-comparison", "native-rejection-preserved", 38,
                        i + 1, i, i + 1, "gateGlobal=" + (1000 + gate));
            string[] rows = aggregate.Drain();
            check(aggregate.Total == variants * repeats && rows.Length == variants, "all combinations retained, identity/coordinate changes do not create keys");
            foreach (string row in rows)
                check(row.Contains("count=2000,targetChanges=1999") && row.Contains("tribe=1,target=0/1") && row.Contains("tribe=2000,target=1999/2000"), "exact aggregate first/last and changes");
            check(aggregate.Drain().Length == 0, "window discarded without retained tribe history");
            aggregate.Reset(); check(aggregate.Total == 0, "map reset clears totals");
            return assertions;
        }
    }
}
