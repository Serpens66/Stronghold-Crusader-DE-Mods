using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace EnemyGatePathfindingTest
{
    public unsafe class Fixture
    {
        private static List<Frame> active;
        private static List<NativeFrame> nativeFrames;
        private static List<HashSet<int>> searches;
        private long errors, unattributedPrechecks;
        private readonly AiGateDecisionAggregate totals = new AiGateDecisionAggregate { TemporaryCountsOnly = true };
        private readonly GateEvidenceAggregate evidence = new GateEvidenceAggregate();
        private readonly Topology topology = new Topology();
        private readonly object maskGate = new object();
        private class Snapshot { internal int Readers = 1; }
        private class Policy { internal int Leaves; internal void Leave(object value) { Leaves++; } }
        private readonly Policy routePolicySource = new Policy();
        private Fixture attackOrderDiagnostics;
        private long exceptions, incompleteSearches, negativeWithoutRejectedEdges, aiNegativeWithoutRejectedEdges,
            preserved, rejectedEdges, cursorRejectedEdges, attackEdges, buildingEdges, candidateEdges, cursorCommandEdges,
            directCursorEdges, negativeWithRejectedEdges, aiNegativeWithRejectedEdges, successWithRejectedEdges,
            aiSuccessWithRejectedEdges, humanSuccessWithRejectedEdges;
        private struct QueryScope
        { internal byte* Slot; internal IntPtr PreviousMask; internal long PreviousTouched; internal Snapshot Snapshot; internal object RouteQuery; internal int PlayerId; }
        /* COMPLETE */
        private class Topology { internal string DescribeGateRole(int p, int g, uint global, int owner, int capture) => "fixture"; }
        private static bool IsAi(int player) => player >= 1 && player <= 8;
        /* ACTUAL */
        private static int assertions;
        private static void Check(bool value, string why) { assertions++; if (!value) throw new Exception(why); }
        public static void Run()
        {
            var f = new Fixture();
            f.Push("target", 12, 2, 12, 38, 134, 6658);
            Frame target = Current();
            f.Push("move", 12, 2, 12, 3, 100, 120);
            f.Push("unit", 101, 2, 12, 3, 100, 120);
            f.ObserveGatePrecheck(2, 134, 134, true, true, NativeGateSnapshotDecision.PreserveUncaptured, 1, 0, 6658, true);
            Check(Current().Evidence.IndividualRejections == 0, "already rejected is not changed");
            f.ObserveScopeResult(2, "AiBuilder", true, true, true, 3);
            f.ObserveGatePrecheck(2, 134, 134, false, true, NativeGateSnapshotDecision.ExcludeForeignCapture, 1, 5, 6658, true);
            f.Finish("unit", 101, 2, 12, 3, 100, 120, 1);
            f.Finish("move", 12, 2, 12, 3, 100, 120, 1);
            Check(target.Evidence.IndividualRejections == 1 && target.Evidence.EarlyRejections == 0, "single route propagated but not called early");
            f.ObserveGatePrecheck(2, 134, 134, false, true, NativeGateSnapshotDecision.ExcludeForeignCapture, 1, 5, 6658, false);
            f.ObserveRegionPair(2, 1, 1, 0, 1, 1, "fixture");
            f.Finish("target", 12, 2, 12, 38, 134, 6658, 1);
            Check(target.Command == 38 && target.Target1 == 134 && target.Evidence.EarlyRejections == 1, "incoming building approach retained");
            Check(target.Evidence.RegionCalls == 1 && target.Evidence.SamePclCalls == 1 && Current() == null, "region shortcut and balanced synchronous lifetime");
            string text = string.Join("\n", f.evidence.Drain());
            Check(text.Contains("upstream-policy-rejection-observed") && text.Contains("individual-filter-only-observed-upstream-effect-unproven"), "early and late separate");
            Check(text.Contains("nativeWithActiveHooks=positive") && !text.Contains("vanilla="), "region result not pristine vanilla");
            Check(text.Contains("commandMeaning=incoming-target-command") && text.Contains("commandMeaning=stored-unit-command"), "stored and incoming not confused");
            f.Push("target", 12, 2, 12, 9, 200, 7000);
            f.ObserveGatePrecheck(5, 134, 134, false, true, NativeGateSnapshotDecision.ExcludeForeignCapture, 1, 3, 6658, false);
            Check(Current().Evidence.EarlyRejections == 0, "different player not attributed to order");
            f.Finish("target", 12, 2, 12, 9, 200, 7000, 0);
            for (int i = 0; i < 80; i++)
            {
                f.Push("target", i + 1, 2, i + 1, 38, i, i + 2);
                f.ObserveGatePrecheck(2, i + 1, i + 1, true, true, NativeGateSnapshotDecision.PreserveUncaptured, 1, 0, (uint)i + 1, false);
                f.Finish("target", i + 1, 2, i + 1, 38, i, i + 2, 1);
            }
            Check(f.evidence.Drain().Count(s => s.Contains("native-rejection-preserved")) == 80, "more than32 gates retained");
            Check(f.evidence.Drain().Length == 0 && f.errors == 0 && active.Count == 0, "no pending rows/contexts/errors");
            // Run the actual unsafe completion path with a copied thread slot, diagnostics on/off.
            for (int diagnostic = 0; diagnostic < 2; diagnostic++)
            {
                var c = new Fixture(); if (diagnostic != 0) c.attackOrderDiagnostics = c;
                byte* slot = stackalloc byte[32];
                long Call(long touched, bool done, bool success, QueryKind kind = QueryKind.AiBuilder)
                {
                    *(int*)slot = 7; *(IntPtr*)(slot + 8) = (IntPtr)999;
                    *(long*)(slot + 16) = touched; *(int*)(slot + 24) = 1;
                    var snapshot = new Snapshot();
                    long value = c.Complete(new QueryScope { Slot = slot, PreviousMask = (IntPtr)1234,
                        PreviousTouched = 77, Snapshot = snapshot, PlayerId = 2 }, kind, true, success, done);
                    Check(value == touched && *(IntPtr*)(slot + 8) == (IntPtr)1234 && *(long*)(slot + 16) == 77 &&
                        *(int*)slot == 0 && *(int*)(slot + 24) == 0 && snapshot.Readers == 0, "actual scope restoration unaffected");
                    return value;
                }
                Call(0, true, false); Call(3, true, false); Call(4, true, true); Call(5, false, false); Call(0, false, false);
                Call(2, true, true, QueryKind.HumanBuilder); Call(2, true, false, QueryKind.CursorPreview);
                Check(c.aiNegativeWithoutRejectedEdges == 1 && c.aiNegativeWithRejectedEdges == 1 && c.negativeWithRejectedEdges == 1, "exact negative counts including zero filtered edges");
                Check(c.incompleteSearches == 2 && c.aiSuccessWithRejectedEdges == 1 && c.humanSuccessWithRejectedEdges == 1, "incomplete is not negative; positive is not counterfactual detour");
                Check(c.routePolicySource.Leaves == 7 && c.exceptions == 0, "same scope count with/without diagnostic observer");
            }
            Console.WriteLine("PASS actual synchronous correlation methods: " + assertions + " assertions; no native calls or SDK queries.");
        }
    }
}
