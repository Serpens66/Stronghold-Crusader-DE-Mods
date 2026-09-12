using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PreplacedTest.Tests
{
    internal static class Program
    {
        private static int checks;

        private static int Main()
        {
            try
            {
                TestCountersHaveNoCap();
                TestChunkingIsLossless();
                TestLosslessGridCoordinateFormatting();
                TestSchedulerModels();
                TestAicSlotConversion();
                TestValidatorResults();
                TestBuildingAccessibilityResults();
                TestDamageTransitions();
                TestInlineHookBranchSafety();
                TestFirstAivBuildingEligibility();
                TestEconomyDiagnosticModels();
                TestEconomyPclModels();
                TestEconomyOverlayProjection();
                TestEconomyFixActivationStates();
                TestPreplacedIdentityAndCountProjection();
                TestPortalRoutes();
                TestPclConnectivityTransitions();
                TestDynamicWallRolesAndSearchGates();
                TestWallTileAndShadowSearchModels();
                TestWoodScoreFloorModel();
                TestLegacyTimerFixEligibility();
                TestChoreTransferDirection();
                TestAivAreaClassification();
                TestEarlyOwnerBuffer();
                TestFirstBuildingWindow();
                TestStaticNativeContracts();
                TestNativeSignaturesAgainstCanonicalDll();
                Console.WriteLine($"PreplacedTest tests passed: {checks} checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void TestCountersHaveNoCap()
        {
            DiagnosticCounterSet counters = new DiagnosticCounterSet();
            for (int index = 0; index < 250000; index++) counters.Add("attempt");
            Check(counters.TotalFor("attempt") == 250000, "counter capped events");
            Check(counters.DrainInterval().Single().Value == 250000, "interval sum differs");
            Check(counters.SnapshotTotal().Single().Value == 250000, "total lost on drain");
            counters.Stop(); counters.Add("attempt");
            Check(counters.TotalFor("attempt") == 250000, "terminal observation window kept accumulating");
            counters.Clear();
            Check(counters.SnapshotTotal().Length == 0, "session reset retained totals");
        }

        private static void TestChunkingIsLossless()
        {
            string input = string.Concat(Enumerable.Range(0, 5000).Select(i => (char)('A' + i % 26)));
            string[] chunks = DiagnosticChunker.Split(input, 137);
            Check(chunks.Length > 1, "long group was not split");
            Check(string.Concat(chunks) == input, "split output was truncated or reordered");
            Check(chunks.All(c => c.Length <= 137), "chunk exceeded limit");
        }

        private static void TestLosslessGridCoordinateFormatting()
        {
            string formatted = LosslessGridCoordinateFormatter.Format(
                new[] { 0, 1, 2, 5, 160, 161, 163, 163 }, 160);
            Check(formatted == "(0,0-2),(0,5),(1,0-1),(1,3)", "coordinate runs were not encoded losslessly");
            Check(LosslessGridCoordinateFormatter.Format(Array.Empty<int>(), 160) == string.Empty,
                "empty coordinate set was not preserved");
        }

        private static void TestSchedulerModels()
        {
            Check(Classify(0, 0, 0, 10000, 0, 5, 0, 1, 2) == "inactive-aiv-slot", "inactive AIV model");
            Check(Classify(1, 1, 10, 10000, 9, 5, 0, 1, 2) == "crushed-building-delay", "crushed delay model");
            Check(Classify(1, 1, -1, 10000, 9, 5, 0, 1, 2) == "crushed-building-delay-unresolved-threshold", "unresolved crushed delay model");
            Check(Classify(1, 0, 10, 1000, 0, 5, 0, 1, 2) == "build-rate", "build rate model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 3, 1, 2) == "aiv-pause-countdown", "pause model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 0, 1, 0) == "no-prepared-frames", "prepared frame model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 0, 0, 2) == "step-goal-not-released", "step goal model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 0, 1, 2) == "scheduler-work-eligible", "eligible model");
        }

        private static string Classify(int a, int c, int cd, int g, int bc, int br, int p, int goal, int high) =>
            SchedulerGateClassifier.ClassifyBeforeCall(new SchedulerGateState(a, c, cd, g, bc, br, p, goal, high));

        private static void TestAicSlotConversion()
        {
            Check(AicSlotIndexResolver.TryResolve(1, 8, out int first) && first == 0, "first AIC slot conversion");
            Check(AicSlotIndexResolver.TryResolve(8, 8, out int last) && last == 7, "last AIC slot conversion");
            Check(!AicSlotIndexResolver.TryResolve(0, 8, out _), "zero AIC slot accepted");
            Check(!AicSlotIndexResolver.TryResolve(9, 8, out _), "out-of-range AIC slot accepted");
        }

        private static void TestValidatorResults()
        {
            Check(PlacementValidatorResult.Classify(0) == "allowed" && !PlacementValidatorResult.IsRejected(0), "validator allowed contract");
            Check(PlacementValidatorResult.Classify(1) == "rejected" && PlacementValidatorResult.IsRejected(1), "validator rejection contract");
            Check(PlacementValidatorResult.Classify(2) == "occupied-building" && PlacementValidatorResult.IsRejected(2), "validator occupied-building contract");
        }

        private static void TestDamageTransitions()
        {
            Check(!DamageObservationModel.IsLethalInput(100, 99), "nonlethal input marked lethal");
            Check(DamageObservationModel.IsLethalInput(100, 100), "lethal input not recognized");
            Check(CrushedTimerTransition.IsActivation(0, 1), "damage activation not recognized");
            Check(!CrushedTimerTransition.IsActivation(0, 0), "nonlethal damage marked as activation");
            Check(!CrushedTimerTransition.IsActivation(7, 7), "active timer marked as activation");
            Check(!CrushedTimerTransition.IsActivation(7, 8), "scheduler increment marked as activation");
            int[] lethalLossTimerSequence = { 0, 1, 2, 3 };
            Check(CrushedTimerTransition.IsActivation(lethalLossTimerSequence[0], lethalLossTimerSequence[1]) &&
                !CrushedTimerTransition.IsActivation(lethalLossTimerSequence[1], lethalLossTimerSequence[2]) &&
                !CrushedTimerTransition.IsActivation(lethalLossTimerSequence[2], lethalLossTimerSequence[3]),
                "normal lethal-loss timer progression 0->1->2->3 was not preserved");
        }

        private static void TestInlineHookBranchSafety()
        {
            Check(InlineHookBranchSafety.HasInboundTargetInside(0x7F074, 15,
                new[] { 0x7F07C }), "inbound branch into the removed writer-hook span was not rejected");
            Check(!InlineHookBranchSafety.HasInboundTargetInside(0x7F074, 8,
                new[] { 0x7F07C }), "branch to the exact end of a hook span was treated as interior");
            Check(!InlineHookBranchSafety.HasInboundTargetInside(0x1000, 14,
                new[] { 0x0FFF, 0x1000, 0x100E }), "non-interior branch target was rejected");
        }

        private static void TestBuildingAccessibilityResults()
        {
            Check(BuildingAccessibilityResult.Classify(0) == "rejected-zero" && BuildingAccessibilityResult.IsRejected(0),
                "accessibility result zero contract");
            Check(BuildingAccessibilityResult.Classify(1) == "accessible" && !BuildingAccessibilityResult.IsRejected(1),
                "accessibility result one contract");
            Check(BuildingAccessibilityResult.Classify(2) == "rejected-two" && BuildingAccessibilityResult.IsRejected(2),
                "accessibility result two contract");
        }

        private static void TestFirstAivBuildingEligibility()
        {
            Check(FirstAivBuildingEligibility.IsEligible(true, false), "valid building spawn rejected");
            Check(!FirstAivBuildingEligibility.IsEligible(false, false), "unresolved building spawn accepted");
            Check(!FirstAivBuildingEligibility.IsEligible(true, true), "wall spawn accepted as first AIV building");
        }

        private static void TestEconomyDiagnosticModels()
        {
            Check(FirstAivSpawnCorrelation.Matches(10, 20, 12, 23, 30, 11, 22, 30),
                "matching spawn signal was rejected");
            Check(!FirstAivSpawnCorrelation.Matches(10, 20, 12, 23, 30, 13, 22, 30),
                "out-of-footprint spawn signal was accepted");
            Check(!FirstAivSpawnCorrelation.Matches(10, 20, 12, 23, 30, 11, 22, 31),
                "wrong-type spawn signal was accepted");
            Check(EconomyCooldownTransition.Classify(0, 5) == "armed", "search cooldown arm transition");
            Check(EconomyCooldownTransition.Classify(-1, 5) == "armed-from-ready-sentinel",
                "native -1 ready sentinel was treated as unavailable");
            Check(EconomyCooldownTransition.Classify(-1, -1) == "ready-sentinel-unchanged",
                "unchanged native ready sentinel classification");
            Check(EconomyCooldownTransition.Classify(0, -1) == "reset-to-ready-sentinel",
                "native ready sentinel reset classification");
            Check(EconomyCooldownTransition.Classify(5, 4) == "decremented", "search cooldown decrement transition");
            Check(EconomyCooldownTransition.Classify(4, 4) == "unchanged", "search cooldown unchanged transition");
            Check(EconomySearchOutcome.Classify(0, false, 0) == "rejected-before-search", "pre-search rejection model");
            Check(EconomySearchOutcome.Classify(1, false, 0) == "search-no-candidate", "no-candidate model");
            Check(EconomySearchOutcome.Classify(1, true, 0) == "candidate-without-construction", "candidate-without-build model");
            Check(EconomySearchOutcome.Classify(1, true, 1) == "construction-called", "construction model");
        }

        private static void TestPreplacedIdentityAndCountProjection()
        {
            int structureIdentity = StringComparer.Ordinal.GetHashCode("preplaced-structure");
            var identity = new PreplacedIdentity(19, 1001, 8, structureIdentity);
            Check(identity.Matches(19, 1001, 8, structureIdentity), "identical preplaced record was not recognized");
            Check(!identity.Matches(19, 1002, 8, structureIdentity), "reused slot with another global ID was treated as preplaced");
            Check(!identity.Matches(19, 1001, 7, structureIdentity), "changed owner was treated as preplaced");
            Check(!identity.Matches(19, 1001, 8, unchecked(structureIdentity + 1)), "changed structure type was treated as preplaced");
            Check(identity.MatchesStableRecord(19, 1001, structureIdentity),
                "stable map-load identity was not recognized after owner resolution");
            Check(!identity.MatchesStableRecord(19, 1002, structureIdentity) &&
                !identity.MatchesStableRecord(20, 1001, structureIdentity),
                "slot reuse was accepted as a stable map-load identity");
            Check(PreplacedCountProjection.WithoutPreplaced(5, 2) == 3, "preplaced count projection");
            Check(PreplacedCountProjection.WithoutPreplaced(1, 4) == 0, "preplaced count projection underflow");
        }

        private static void TestEconomyPclModels()
        {
            Check(EconomyPclModel.SelectMostFrequentPositive(new[] { 0, 2, 2, 4 }) == 2,
                "dominant positive PCL selection");
            Check(EconomyPclModel.SelectMostFrequentPositive(new[] { 1, 1, 3, 3 }) == 1,
                "Vanilla equal-frequency PCL tie behavior");
            Check(EconomyPclModel.SelectMostFrequentPositive(new[] { 0, -1 }) == 0,
                "empty positive PCL selection");
            Check(EconomyPclModel.CountDifferentFromReference(
                Enumerable.Repeat(7, 20).Concat(Enumerable.Repeat(9, 5)), 7) == 5,
                "5x5 non-reference PCL count");
            Check(EconomyPclModel.CountDifferentFromReference(Enumerable.Repeat(9, 25), 7) == 25,
                "fully separated coarse cell count");
            Check(EconomyPclModel.ApplyPeriodicDecay(25) == 24 &&
                EconomyPclModel.ApplyPeriodicDecay(0) == 0 &&
                EconomyPclModel.ApplyPeriodicDecay(-1) == -1,
                "periodic grid decay model");
        }

        private static void TestEconomyOverlayProjection()
        {
            byte projected = EconomyGridOverlayProjection.ProjectOutsideCount(
                Enumerable.Repeat(1, 9).Concat(Enumerable.Repeat(2, 8)).Concat(Enumerable.Repeat(3, 8)),
                new HashSet<int> { 1, 2 });
            Check(projected == 8, "portal-reachable PCLs were still counted as outside");
            Check(EconomyGridOverlayProjection.ProjectOutsideCount(
                Enumerable.Repeat(5, 25), new HashSet<int> { 1 }) == 25,
                "closed-wall cells were made reachable");
            Check(EconomyGridOverlayProjection.ProjectOutsideCount(
                Enumerable.Repeat(0, 25), new HashSet<int> { 1 }) == 25,
                "unlabelled tiles were made reachable");
            byte[] before = Enumerable.Range(0, 25600).Select(index => (byte)(index % 26)).ToArray();
            byte[] restored = (byte[])before.Clone();
            Check(EconomyGridOverlayProjection.RestoredExactly(before, restored),
                "exact full-grid restoration was rejected");
            byte[] outerProjection = Enumerable.Repeat((byte)3, before.Length).ToArray();
            byte[] innerBefore = (byte[])outerProjection.Clone();
            byte[] innerRestored = (byte[])innerBefore.Clone();
            Check(EconomyGridOverlayProjection.RestoredExactly(outerProjection, innerRestored) &&
                EconomyGridOverlayProjection.RestoredExactly(before, restored),
                "nested overlay restoration model did not preserve both parent states");
            restored[25599]++;
            Check(!EconomyGridOverlayProjection.RestoredExactly(before, restored),
                "last-cell restoration corruption was missed");
        }

        private static void TestEconomyFixActivationStates()
        {
            Check(EconomyFixActivationModel.Initial(true, WallTestRole.GatedWallCandidate, true) ==
                EconomyFixActivationState.None, "savegame incorrectly entered the economy fix state machine");
            Check(EconomyFixActivationModel.Initial(false, WallTestRole.GatedWallCandidate, true) ==
                EconomyFixActivationState.PendingPortal, "preplaced friendly portal was not kept pending");
            Check(EconomyFixActivationModel.Initial(false, WallTestRole.ClosedWallCandidate, false) ==
                EconomyFixActivationState.PendingBreach, "closed torless AI was not kept pending for a breach");
            Check(EconomyFixActivationModel.Initial(false, WallTestRole.None, false) ==
                EconomyFixActivationState.None, "open map incorrectly entered the economy fix state machine");
            Check(EconomyFixActivationModel.Initial(false, WallTestRole.None, true) ==
                EconomyFixActivationState.None, "unrelated portal on an open map activated the economy fix");
            Check(EconomyFixActivationModel.Activate(EconomyFixActivationState.PendingPortal) ==
                EconomyFixActivationState.ActivePortal, "portal activation transition");
            Check(EconomyFixActivationModel.Activate(EconomyFixActivationState.PendingBreach) ==
                EconomyFixActivationState.ActiveBreach, "breach activation transition");
            Check(EconomyFixActivationModel.Resume(true, false, WallTestRole.ClosedWallCandidate) ==
                EconomyFixActivationState.PendingBreach, "confirmed breach was not preferred on resume");
            Check(EconomyFixActivationModel.Resume(false, true, WallTestRole.GatedWallCandidate) ==
                EconomyFixActivationState.PendingPortal, "friendly preplaced portal did not resume pending");
            Check(EconomyFixActivationModel.Resume(false, false, WallTestRole.ClosedWallCandidate) ==
                EconomyFixActivationState.PendingBreach, "closed control did not resume breach observation");
            Check(EconomyFixActivationModel.Resume(false, false, WallTestRole.None) ==
                EconomyFixActivationState.None, "open topology resumed an economy correction");
        }

        private static void TestPortalRoutes()
        {
            var portals = new List<PortalConnection>
            {
                new PortalConnection(101, 10, 20, 0, 88, 40, 2),
                new PortalConnection(102, 20, 30, 0, 88, 41, 6),
                new PortalConnection(103, 10, 40, 0, 77, 42, 5, false)
            };
            Check(PortalRouteModel.Evaluate(10, 10, portals).Kind == PortalRouteKind.Direct,
                "direct PCL route");
            PortalRouteResult raw = PortalRouteModel.Evaluate(10, 30, portals);
            Check(raw.Kind == PortalRouteKind.RawPortalGraph && raw.UsedPortalIds.SequenceEqual(new[] { 101, 102 }),
                "multi-gate raw portal route");
            Check(portals[0].RawOwnerValue == 88, "raw portal owner value was reinterpreted or discarded");
            Check(PortalRouteModel.Evaluate(10, 40, portals).Kind == PortalRouteKind.RawPortalGraph,
                "raw portal owner field incorrectly filtered the graph");
            Check(PortalRouteModel.EvaluateEconomyModeZero(10, 40, portals).Kind == PortalRouteKind.Unreachable,
                "mode-zero-ineligible portal was not filtered");
            Check(!PortalRouteModel.ReachableFriendlyPcls(10, portals, 5, (first, second) => false).Contains(40),
                "ladder-only portal leaked into the friendly economy reachability set");
            Check(PortalRouteModel.EvaluateFriendly(10, 30, portals, 2, (first, second) => second == 6).Kind ==
                PortalRouteKind.RawPortalGraph, "own plus allied portal route was not found");
            Check(PortalRouteModel.EvaluateFriendly(10, 40, portals, 2, (first, second) => false).Kind ==
                PortalRouteKind.Unreachable, "foreign portal was treated as friendly");
            Check(PortalRouteModel.Evaluate(10, 50, portals).Kind == PortalRouteKind.Unreachable,
                "sealed destination route");
            Check(PortalRouteModel.Evaluate(10, 20, Array.Empty<PortalConnection>()).Kind == PortalRouteKind.Unreachable,
                "missing gatehouse route");
            Check(PortalRouteModel.Evaluate(0, 30, portals).Kind == PortalRouteKind.Unreachable,
                "invalid start PCL route");
            var fourOwnGates = new List<PortalConnection>
            {
                new PortalConnection(201, 1, 2, 0, 99, 51, 5),
                new PortalConnection(202, 2, 3, 0, 99, 52, 5),
                new PortalConnection(203, 3, 4, 0, 99, 53, 5),
                new PortalConnection(204, 4, 5, 0, 99, 54, 5)
            };
            Check(PortalRouteModel.Evaluate(1, 5, fourOwnGates).UsedPortalIds.Length == 4,
                "four own gatehouses were not traversed");
            fourOwnGates.RemoveAt(2);
            Check(PortalRouteModel.Evaluate(1, 5, fourOwnGates).Kind == PortalRouteKind.Unreachable,
                "destroyed gatehouse did not break the modeled portal chain");
        }

        private static void TestPclConnectivityTransitions()
        {
            ushort[] before = { 3, 3, 7, 7, 9, 9 };
            ushort[] after = { 3, 3, 3, 3, 9, 9 };
            IReadOnlyList<PclConnectivityTransition> transitions =
                PclConnectivityTransitionDetector.Detect(before, after, new[] { 0, 1 }, new[] { 2, 3 });
            Check(transitions.Count == 4 && transitions.All(value => value.NewPcl == 3),
                "local inside/outside connectivity transition was not detected");
            Check(PclConnectivityTransitionDetector.Detect(before, (ushort[])before.Clone(),
                new[] { 0, 1 }, new[] { 2, 3 }).Count == 0,
                "unchanged closed topology was marked as a merge");
            Check(PclConnectivityTransitionDetector.Detect(new ushort[] { 3, 3, 7, 7 },
                new ushort[] { 4, 4, 8, 8 }, new[] { 0, 1 }, new[] { 2, 3 }).Count == 0,
                "pure component relabeling was marked as a merge");
        }

        private static void TestDynamicWallRolesAndSearchGates()
        {
            foreach (int playerId in new[] { 1, 3, 6, 8 })
            {
                Check(WallTestRoleClassifier.Classify(40, 4) == WallTestRole.GatedWallCandidate,
                    "gated role depended on a fixed player ID " + playerId);
                Check(WallTestRoleClassifier.Classify(40, 0) == WallTestRole.ClosedWallCandidate,
                    "closed role depended on a fixed player ID " + playerId);
            }
            Check(WallTestRoleClassifier.Classify(0, 4) == WallTestRole.None,
                "portal without wall was assigned a wall-test role");
            Check(EconomySearchGateClassifier.Classify(true, 9, 8, 0, 0, 0, 0, -1, -1, -1, -1) == "traversal-performed",
                "performed traversal was hidden by cooldown state");
            Check(EconomySearchGateClassifier.Classify(false, 5, 4, 0, 0, 0, 0, -1, -1, -1, -1) == "early-return-cooldown",
                "cooldown early return classification");
            Check(EconomySearchGateClassifier.Classify(false, -1, -1, 1, 2, 1, 2, -1, -1, -1, -1) ==
                "early-return-shared-queue-pending", "shared queue early return classification");
            Check(EconomySearchGateClassifier.Classify(false, -1, -1, 0, 0, 0, 0, 7, 8, 7, 8) ==
                "early-return-cached-result", "cached result early return classification");
            Check(EconomySearchGateClassifier.Classify(false, -1, -1, 0, 0, 0, 0, -1, -1, -1, -1) ==
                "early-return-unresolved-mode-or-state", "unresolved early return classification");
        }

        private static void TestChoreTransferDirection()
        {
            Check(ChoreTransferDirection.Classify(0) == "runtime-to-buffer",
                "chore save direction changed");
            Check(ChoreTransferDirection.Classify(1) == "buffer-to-runtime",
                "chore load direction changed");
            Check(ChoreTransferDirection.Classify(2) == "no-transfer-or-unknown",
                "unknown chore direction was guessed");
        }

        private static void TestWallTileAndShadowSearchModels()
        {
            Check(WallOwnerEncodingResolver.Resolve(12, 0) == WallOwnerEncoding.OneBased,
                "one-based wall owner evidence was not resolved");
            Check(WallOwnerEncodingResolver.Resolve(0, 12) == WallOwnerEncoding.ZeroBased,
                "zero-based wall owner evidence was not resolved");
            Check(WallOwnerEncodingResolver.Resolve(4, 4) == WallOwnerEncoding.Unresolved,
                "ambiguous wall owner evidence was guessed");
            Check(WallOwnerEncodingResolver.Decode(7, WallOwnerEncoding.OneBased) == 7 &&
                WallOwnerEncodingResolver.Decode(7, WallOwnerEncoding.ZeroBased) == 8,
                "wall owner decoding contract");
            Check(WallBreachConfirmation.IsConfirmed(true, 3, 7, 9, 9),
                "lost wall plus connected anchors was not confirmed");
            Check(!WallBreachConfirmation.IsConfirmed(false, 3, 7, 9, 9),
                "PCL relabeling without a lost wall was confirmed");
            Check(!WallBreachConfirmation.IsConfirmed(true, 3, 7, 9, 10),
                "lost wall without connected anchors was confirmed");

            ShadowEconomyCell pass = Cell(0, 0, 1);
            ShadowEconomyCell blocked = Cell(25, 0, 0);
            ShadowEconomyCell[] sealedGrid = Enumerable.Repeat(blocked, 25).ToArray();
            sealedGrid[12] = pass;
            ShadowEconomySearchResult sealedResult = ShadowEconomySearch.Run(sealedGrid, 5, 12,
                ShadowEconomySearchKind.Wood, 0);
            Check(sealedResult.ReachableCount == 1 && sealedResult.BlockedIndices.Length == 4,
                "closed wall shadow traversal escaped its start cell");
            ShadowEconomyCell[] gatedGrid = Enumerable.Repeat(pass, 25).ToArray();
            ShadowEconomySearchResult gatedResult = ShadowEconomySearch.Run(gatedGrid, 5, 12,
                ShadowEconomySearchKind.Wood, 0);
            Check(gatedResult.ReachableCount > 1 && gatedResult.CandidateIndices.Length != 0 &&
                gatedResult.FirstCandidateIndex >= 0,
                "friendly gate shadow traversal did not reach and score the open region");
            ShadowEconomyCell[] farmGrid = Enumerable.Repeat(new ShadowEconomyCell(0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 25, 14, 0, 0), 9).ToArray();
            Check(ShadowEconomySearch.Run(farmGrid, 3, 4, ShadowEconomySearchKind.Farm, 0)
                .CandidateIndices.Length > 0, "farm raw candidate predicate was not replayed");
            ShadowEconomyCell[] signedNegativeFarmDensity = Enumerable.Repeat(new ShadowEconomyCell(0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0xFF, 14, 0, 0), 9).ToArray();
            Check(ShadowEconomySearch.Run(signedNegativeFarmDensity, 3, 4, ShadowEconomySearchKind.Farm, 0)
                .CandidateIndices.Length == 0, "farm density was compared as unsigned");
            ShadowEconomyCell resource2 = new ShadowEconomyCell(0, 0, 0, 8, 0, 0, 0, 0, 39,
                0, 0, 0, 0, 0, 0);
            ShadowEconomyCell resource3 = new ShadowEconomyCell(0, 0, 0, 0, 7, 0, 0, 0, 29,
                0, 0, 0, 0, 0, 0);
            ShadowEconomyCell resource4 = new ShadowEconomyCell(0, 0, 0, 0, 0, 3, 10, 0, 11,
                0, 0, 0, 0, 0, 0);
            Check(ShadowEconomySearch.Run(Enumerable.Repeat(resource2, 9).ToArray(), 3, 4,
                ShadowEconomySearchKind.Resource, 2).CandidateIndices.Length > 0,
                "quarry candidate predicate was not replayed");
            Check(ShadowEconomySearch.Run(Enumerable.Repeat(resource3, 9).ToArray(), 3, 4,
                ShadowEconomySearchKind.Resource, 3).CandidateIndices.Length > 0,
                "iron candidate predicate was not replayed");
            Check(ShadowEconomySearch.Run(Enumerable.Repeat(resource4, 9).ToArray(), 3, 4,
                ShadowEconomySearchKind.Resource, 4).CandidateIndices.Length > 0,
                "pitch candidate predicate was not replayed");
            ShadowEconomySearchResult immediateResource = ShadowEconomySearch.Run(
                Enumerable.Repeat(resource2, 9).ToArray(), 3, 4,
                ShadowEconomySearchKind.Resource, 2);
            Check(immediateResource.CandidateIndices.Length == 1 &&
                immediateResource.QueueOrderIndices.Length == 1,
                "resource search did not return at Vanilla's first accepted neighbor");
            ShadowEconomyCell wrongPclForQuarry = new ShadowEconomyCell(6, 0, 0, 8, 0, 0, 0, 0, 39,
                0, 0, 0, 0, 0, 0);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(wrongPclForQuarry, 2) ==
                "pcl-difference", "quarry PCL equality predicate was omitted");
            ShadowEconomyCell ironTolerance = new ShadowEconomyCell(4, 0, 0, 0, 7, 0, 0, 0, 29,
                0, 0, 0, 0, 0, 0);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(ironTolerance, 3) == "candidate",
                "iron PCL difference tolerance was not preserved");
            ShadowEconomyCell foreignOwnerClass = new ShadowEconomyCell(0, 0, 0, 8, 0, 0, 0, 0, 39,
                0, 0, 0, 0, 0, 2, false);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(foreignOwnerClass, 2) ==
                "owner-class-mismatch-byte+15", "resource owner-class predicate was omitted");
            ShadowEconomyCell[] diagonalGrid = Enumerable.Repeat(blocked, 9).ToArray();
            diagonalGrid[4] = pass;
            diagonalGrid[0] = pass;
            Check(ShadowEconomySearch.Run(diagonalGrid, 3, 4, ShadowEconomySearchKind.Nearby, 0)
                .ReachableCount == 2, "nearby search did not preserve Vanilla's diagonal neighbor order/set");
            ShadowEconomyCell[] nearbyImmediateGrid = Enumerable.Repeat(blocked, 9).ToArray();
            nearbyImmediateGrid[4] = pass;
            nearbyImmediateGrid[3] = Cell(0, 0, 0);
            ShadowEconomySearchResult nearbyImmediate = ShadowEconomySearch.Run(
                nearbyImmediateGrid, 3, 4, ShadowEconomySearchKind.Nearby, 0);
            Check(nearbyImmediate.FirstCandidateIndex == 3 &&
                nearbyImmediate.QueueOrderIndices.Length == 1,
                "nearby search did not return at Vanilla's first free orthogonal neighbor");
            ShadowEconomyCell quarryHeightBoundary = new ShadowEconomyCell(0, 0, 0, 8, 0, 0, 0, 0, 40,
                0, 0, 0, 0, 0, 0);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(quarryHeightBoundary, 2) == "quarry-height",
                "quarry signed height boundary was inverted");
            ShadowEconomyCell negativeHeightDifference = new ShadowEconomyCell(0, 0, 0, 8, 0, 0, 0, 50, 10,
                0, 0, 0, 0, 0, 0);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(negativeHeightDifference, 2) == "candidate",
                "negative signed height difference was not accepted");
            ShadowEconomyCell signedNegativeDensity = new ShadowEconomyCell(0, 0, 0, 0xFF, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(signedNegativeDensity, 2) ==
                "quarry-density-byte+08", "resource density was compared as unsigned");
            ShadowEconomyCell orderedRejections = new ShadowEconomyCell(1, 0, 0, 8, 0, 0, 0, 0, 39,
                0, 1, 0, 0, 0, 0);
            Check(ShadowEconomySearch.ResourceCandidateRejectionReason(orderedRejections, 2) == "pcl-difference",
                "resource first-rejection order diverges from Vanilla");
            Check(ShadowEconomySearch.ResourceCandidateRejectionCode(resource2, 2) == 0 &&
                ShadowEconomySearch.ResourceCandidateRejectionCode(quarryHeightBoundary, 2) == 6,
                "allocation-free resource signature codes diverge from named decisions");
            Check(ShadowEconomySearch.WoodCandidateRejectionReason(Cell(5, 0, 1)) == "candidate" &&
                ShadowEconomySearch.WoodCandidateRejectionReason(Cell(6, 0, 1)) == "pcl-threshold" &&
                ShadowEconomySearch.WoodCandidateRejectionReason(Cell(0, 0, 0)) == "wood-density-byte+07" &&
                ShadowEconomySearch.WoodCandidateRejectionReason(new ShadowEconomyCell(0, 0, 1, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 1, 0)) == "blocked-byte+13",
                "wood candidate rejection order diverges from Vanilla");
            ShadowEconomyCell[] scoredWood = Enumerable.Repeat(Cell(0, 0, 1), 9).ToArray();
            scoredWood[1] = new ShadowEconomyCell(0, 0, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0);
            ShadowEconomySearchResult scoredWoodResult = ShadowEconomySearch.Run(scoredWood, 3, 4,
                ShadowEconomySearchKind.Wood, 0);
            Check(scoredWoodResult.FirstCandidateIndex == 1,
                "wood search did not retain Vanilla's highest-scoring candidate");

            int[] matchingDepths = (int[])gatedResult.Depths.Clone();
            ShadowNativeTraversalComparison match = ShadowNativeTraversalComparison.Compare(gatedResult,
                gatedResult.ReachedIndices, matchingDepths, gatedResult.QueueOrderIndices,
                gatedResult.FirstCandidateIndex);
            Check(match.Classification == "match", "matching shadow/native traversal was rejected");
            ShadowNativeTraversalComparison noResult = ShadowNativeTraversalComparison.Compare(gatedResult,
                gatedResult.ReachedIndices, matchingDepths, gatedResult.QueueOrderIndices, -1);
            Check(noResult.Classification == "shadow-candidate-native-no-result",
                "shadow candidate/native no-result mismatch was not detected");
            int[] shorterQueue = gatedResult.QueueOrderIndices.Take(gatedResult.QueueOrderIndices.Length - 1).ToArray();
            ShadowNativeTraversalComparison queueMismatch = ShadowNativeTraversalComparison.Compare(gatedResult,
                gatedResult.ReachedIndices, matchingDepths, shorterQueue, gatedResult.FirstCandidateIndex);
            Check(queueMismatch.Classification == "queue-order-divergence" &&
                queueMismatch.FirstQueueDivergence >= 0, "queue-order divergence was not localized");
            int[] missingVisit = gatedResult.ReachedIndices.Skip(1).ToArray();
            ShadowNativeTraversalComparison visitMismatch = ShadowNativeTraversalComparison.Compare(gatedResult,
                missingVisit, matchingDepths, gatedResult.QueueOrderIndices, gatedResult.FirstCandidateIndex);
            Check(visitMismatch.Classification == "visited-set-divergence" &&
                visitMismatch.FirstVisitDivergence >= 0, "visited-set divergence was not localized");
            int[] wrongDepths = (int[])matchingDepths.Clone();
            wrongDepths[gatedResult.QueueOrderIndices.Last()]++;
            ShadowNativeTraversalComparison depthMismatch = ShadowNativeTraversalComparison.Compare(gatedResult,
                gatedResult.ReachedIndices, wrongDepths, gatedResult.QueueOrderIndices,
                gatedResult.FirstCandidateIndex);
            Check(depthMismatch.Classification == "depth-divergence" &&
                depthMismatch.FirstDepthDivergence >= 0, "depth divergence was not localized");
            Check(ShadowNativeTraversalComparison.Compare(sealedResult, sealedResult.ReachedIndices
                    .Concat(sealedResult.BlockedIndices).ToArray(), sealedResult.Depths,
                    sealedResult.QueueOrderIndices, -1).Classification == "both-no-result",
                "matching no-result traversal was rejected");
        }

        private static void TestLegacyTimerFixEligibility()
        {
            string eligible = LegacyTimerFixEligibility.Classify(true, false, 172, 0xD5, 1, 1, 0, 1);
            Check(LegacyTimerFixEligibility.IsEligible(eligible), "fresh legacy timer transfer was not eligible");
            string applicable = LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, false, 1);
            Check(LegacyTimerFixEligibility.IsApplicationEligible(applicable),
                "verified destroyed-tower transfer was not applicable");
            Check(LegacyTimerFixEligibility.ClassifyAtApplication(eligible, false, false, 1) ==
                "ineligible-no-matching-preplaced-destroyed-tower", "transfer without a tower ruin was applicable");
            Check(LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, true, 1) ==
                "ineligible-later-damage-activation", "later damage activation was applicable");
            Check(LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, false, 2) ==
                "ineligible-runtime-timer-changed-before-application", "changed timer was applicable");
            Check(LegacyTimerFixEligibility.Classify(true, true, 172, 0xD5, 1, 1, 0, 1) ==
                "ineligible-loaded-save", "loaded save timer was eligible");
            Check(LegacyTimerFixEligibility.Classify(true, false, 172, 0xD5, 1, 1, 2, 1) ==
                "ineligible-runtime-timer-already-active", "already-active runtime timer was eligible");
            Check(LegacyTimerFixEligibility.Classify(true, false, 172, 0xD5, 0, 1, 0, 1) ==
                "ineligible-no-stable-single-activation-source", "timer created during transfer was treated as stable source");
            Check(LegacyTimerFixEligibility.Classify(true, false, 172, 0xD5, 2, 2, 0, 2) ==
                "ineligible-no-stable-single-activation-source", "unexpected serialized timer value was eligible");
            Check(LegacyTimerFixEligibility.Classify(true, false, 0xD5, 0xD5, 1, 1, 0, 1) ==
                "ineligible-not-legacy-conversion", "current map format was treated as legacy conversion");
            Check(LegacyTimerFixEligibility.Classify(true, false, 172, 0xD5, 1, 1, 0, 0) ==
                "ineligible-not-copied-exclusively-from-serialized-source",
                "a later activation outside the transfer could be normalized");
            Check(LegacyTimerFixEligibility.Classify(false, false, 172, 0xD5, 1, 1, 0, 1) ==
                "ineligible-non-ai", "non-AI record was eligible");
        }

        private static ShadowEconomyCell Cell(int projected04, int raw16, byte wood) =>
            new ShadowEconomyCell(projected04, raw16, wood, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        private static void TestWoodScoreFloorModel()
        {
            ShadowEconomyCell[] close = Enumerable.Repeat(Cell(127, 0, 0), 9).ToArray();
            close[4] = Cell(0, 0, 0);
            close[5] = Cell(0, 0, 1);
            ShadowEconomySearchResult vanillaClose = ShadowEconomySearch.Run(close, 3, 4,
                ShadowEconomySearchKind.Wood, 0, -100);
            ShadowEconomySearchResult fixedClose = ShadowEconomySearch.Run(close, 3, 4,
                ShadowEconomySearchKind.Wood, 0, int.MinValue);
            Check(vanillaClose.SelectedCandidateIndex == fixedClose.SelectedCandidateIndex &&
                vanillaClose.WoodAcceptedCandidateCount == 1 && vanillaClose.BestWoodScore > -100,
                "the score-floor fix changed a Vanilla-accepted wood result");

            const int width = 70;
            ShadowEconomyCell[] distant = Enumerable.Repeat(Cell(127, 0, 0), width * width).ToArray();
            for (int y = 0; y <= 40; y++) distant[y] = Cell(0, 0, 0);
            distant[40] = Cell(0, 0, 1);
            ShadowEconomySearchResult vanillaDistant = ShadowEconomySearch.Run(distant, width, 0,
                ShadowEconomySearchKind.Wood, 0, -100);
            ShadowEconomySearchResult fixedDistant = ShadowEconomySearch.Run(distant, width, 0,
                ShadowEconomySearchKind.Wood, 0, int.MinValue);
            Check(vanillaDistant.WoodCandidateCount == 1 && vanillaDistant.WoodAcceptedCandidateCount == 0 &&
                vanillaDistant.BestWoodScore <= -100 && vanillaDistant.SelectedCandidateIndex == -1,
                "Vanilla's implicit -100 wood-score floor was not reproduced");
            Check(fixedDistant.SelectedCandidateIndex == 40 && fixedDistant.WoodAcceptedCandidateCount == 1 &&
                fixedDistant.BestWoodScore == vanillaDistant.BestWoodScore,
                "the direct score-floor correction did not select Vanilla's best formal candidate");

            const int branchWidth = 50;
            ShadowEconomyCell[] multiple = Enumerable.Repeat(Cell(127, 0, 0), branchWidth * branchWidth).ToArray();
            for (int y = 0; y <= 25; y++) multiple[20 * branchWidth + y] = Cell(0, 0, 0);
            multiple[19 * branchWidth + 20] = new ShadowEconomyCell(0, 0, 1, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, true, 1);
            multiple[19 * branchWidth + 21] = new ShadowEconomyCell(0, 0, 3, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, true, 1);
            ShadowEconomySearchResult multipleFixed = ShadowEconomySearch.Run(multiple, branchWidth,
                20 * branchWidth, ShadowEconomySearchKind.Wood, 0, int.MinValue);
            Check(multipleFixed.WoodCandidateCount == 2 &&
                multipleFixed.SelectedCandidateIndex == 19 * branchWidth + 21 &&
                multipleFixed.BestWoodScore == -102,
                "the corrected floor selected the first/last candidate instead of Vanilla's highest score");

            ShadowEconomyCell penalized = new ShadowEconomyCell(0, 0, 1, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, true, 1);
            Check(ShadowEconomySearch.CalculateWoodScore(Cell(0, 0, 3), 38) == -99 &&
                ShadowEconomySearch.CalculateWoodScore(penalized, 40) == -230,
                "wood score signedness, distance, or byte+06 adjustment diverges from 0x58020");

            ShadowEconomyCell[] closed = Enumerable.Repeat(Cell(127, 0, 0), 9).ToArray();
            closed[4] = Cell(0, 0, 0);
            ShadowEconomySearchResult closedResult = ShadowEconomySearch.Run(closed, 3, 4,
                ShadowEconomySearchKind.Wood, 0, int.MinValue);
            Check(closedResult.WoodCandidateCount == 0 && closedResult.SelectedCandidateIndex == -1,
                "the score correction escaped a closed economy region");
        }

        private static void TestAivAreaClassification()
        {
            Check(AivAreaClassifier.Intersects(100, 200, 100, 110, 210, 112, 212), "inside building classified outside");
            Check(AivAreaClassifier.Intersects(100, 200, 100, 98, 210, 101, 212), "overlapping building classified outside");
            Check(!AivAreaClassifier.Intersects(100, 200, 100, 200, 210, 202, 212), "right-edge outside building classified inside");
            Check(!AivAreaClassifier.Intersects(100, 200, 100, 90, 190, 99, 199), "outside building classified inside");
        }

        private static void TestEarlyOwnerBuffer()
        {
            EarlyOwnerEventBuffer buffer = new EarlyOwnerEventBuffer(1, 8);
            for (int index = 0; index < 10000; index++) buffer.Add(8, "event-" + index);
            Check(buffer.CountFor(8) == 10000, "early events were capped");
            string[] events = buffer.Drain(8);
            Check(events.Length == 10000 && events[0] == "event-0" && events[9999] == "event-9999", "early event order or content lost");
            Check(buffer.CountFor(8) == 0, "early events survived drain");
            buffer.Add(7, "old-map");
            buffer.Clear();
            Check(buffer.CountFor(7) == 0, "map reset retained early events");
        }

        private static void TestFirstBuildingWindow()
        {
            DateTime start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            FirstBuildingWindow window = new FirstBuildingWindow(TimeSpan.FromSeconds(10));
            Check(!window.TryConfirm(start, true, false, false), "nonbuilding command confirmed");
            Check(!window.TryConfirm(start, true, false, true), "wall/moat-style frame transition confirmed");
            Check(!window.TryConfirm(start, false, true, true), "failed execute confirmed");
            Check(window.TryConfirm(start, true, true, false), "spawn success not confirmed");
            Check(!window.FollowUpComplete(start.AddMilliseconds(9999)), "follow-up ended early");
            Check(window.FollowUpComplete(start.AddSeconds(10)), "follow-up did not end");
        }

        private static void TestStaticNativeContracts()
        {
            string source = File.ReadAllText(Path.Combine("src", "PreplacedTestRuntime.cs"));
            string model = File.ReadAllText(Path.Combine("src", "DiagnosticModel.cs"));
            string assemblyInfo = File.ReadAllText(Path.Combine("src", "AssemblyInfo.cs"));
            string plugin = File.ReadAllText(Path.Combine("src", "PreplacedTestPlugin.cs"));
            string manifest = File.ReadAllText("info.json");
            string spanReport = File.ReadAllText("ScriptExtenderPathConnectionGridSpanReport.md");
            string updateGuide = File.ReadAllText("UpdateToNewDLL.md");
            string helper = File.ReadAllText(Path.Combine("..", "..", "Shared", "DebugLogHelper.cs"));
            Check(helper.Contains("FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"), "native hash contract missing");
            Check(spanReport.Contains("`0x50720` | `0x4FB20`") &&
                spanReport.Contains("`0x572B0` | `0x566B0`") &&
                spanReport.Contains("zero-filled virtual part of `.data`") &&
                spanReport.Contains("## Practical impact"),
                "Script Extender PCL span report lacks file offsets or practical impact");
            Check(updateGuide.Contains("`0xD4290`") && updateGuide.Contains("`0x96CE`") &&
                updateGuide.Contains("`0x37CC7EC`") && updateGuide.Contains("`0x15B90`") &&
                updateGuide.Contains("`0x1F5F0..0x1F68D`") && updateGuide.Contains("`+0x2AE0`") &&
                updateGuide.Contains("`0x55FE0`") && updateGuide.Contains("`0x96E30`") &&
                updateGuide.Contains("`0x3B270`, `0x3B360`, `0x55E10`") &&
                updateGuide.Contains("marker set but not in the queue") &&
                updateGuide.Contains("`0x379AFA8`, `0x379AFAC`") &&
                updateGuide.Contains("RollbackAndThrow"),
                "native update guide does not cover the new timer-copy contract");
            foreach (string rva in new[] { "0x50680", "0x50720", "0x55E10", "0x55FE0", "0x572B0", "0x7EB00", "0xD4290", "0x15B90", "0x1F5F0", "0x96CE", "0x37CC7EC", "0x379ADD0", "0x379D0CC", "0x379AFA8", "0x379AFAC", "0x8574320", "0x86C132C", "0x85F8FEC", "0x32DC084", "0x50EC690", "0x51890D0", "0xC3FA0", "0xC43A0", "0xB8310", "0x115830", "0x102C30", "0x2A340", "0x3B270", "0x3B360", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xB8270", "0xC3BF0", "0xC3C5D", "0xC8F50", "0xC90E0", "0x50D80", "0x50E00", "0x50F90", "0x51190", "0x51270", "0x51540", "0x57330", "0x575B0", "0x57B80", "0x58020", "0x58057", "0x583A0", "0x58950", "0x58BE0", "0x6D580", "0xE2610", "0x60AD660", "0x60AD4AC", "0x2D13B0", "0x2D2E50" })
                Check(source.Contains(rva), "RVA missing: " + rva);
            foreach (string contract in new[] { "AivSpecStride = 0x6D98", "PlayerRuntimeStateStride = 0x583C", "PreparedLayoutFrameCount = 0x922", "PreparedEntrySize = 0x0C", "PauseTableEntryCount =", "pauseIndex < PauseTableEntryCount", "EconomyGridWidth = 160", "EconomyGridCellStride = 0x30", "EconomyGridBaseOffset = 0x5B830", "EconomyReferencePclOffset = 0x5B504", "EconomyVisitGenerationOffset = 0x5B50C", "WoodSearchCooldownRelativeOffset = 0x167C", "FarmSearchCooldownRelativeOffset = 0x167E", "QuarrySearchCooldownRelativeOffset = 0x1680", "IronSearchCooldownRelativeOffset = 0x1682", "PitchSearchCooldownRelativeOffset = 0x1684", "ValidateSize(typeof(GameBuilding), 0x32C)", "ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_BuildingId), 0x0C)", "ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_SubjectGlobalId), 0x14)", "ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_EntryTileId), 0x24)", "ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_ExitTileId), 0x30)", "ValidateSize(typeof(PathConnectionRecord), 0x204)", "UnmanagedFunctionPointer(CallingConvention.Cdecl)" })
                Check(source.Contains(contract), "native ABI/offset contract missing: " + contract);
            foreach (string nativeDelegate in new[]
            {
                "delegate int CountBuildingsDelegate(ulong manager, int playerId, int structureType, int mode)",
                "delegate int PlacementReachabilityDelegate(ulong manager, int playerId, int structureType, int x, int y)",
                "delegate void AccessibilitySweepDelegate(ulong manager, int playerId)",
                "delegate int BuildingAccessibilityDelegate(ulong manager, int buildingId, int mode)",
                "delegate int InaccessibleBuildingCheckDelegate(ulong state, int buildingId)",
                "delegate int InaccessibleBuildingSelectionDelegate(ulong state, int playerId)",
                "delegate void EconomyCellPenaltyDelegate(ulong state, int x, int y)",
                "delegate long EconomyFarmDelegate(ulong state, int playerId, int desiredStructureType)",
                "delegate void ResourceSearchDelegate(ulong state, int playerId, int mode)",
                "delegate void ConstructBuildingDelegate(",
                "delegate int RegionPairReachabilityDelegate(",
                "delegate void EconomyGridUpdateDelegate(ulong state, int mode)",
                "delegate void InitializeEconomyAvailabilityDelegate(ulong state, int playerId)",
                "delegate int SelectDominantPclDelegate(ulong state)",
                "delegate void PlayerBuildingInitializationDelegate(ulong manager, int playerId)",
                "delegate void BuildingInitializationDelegate(ulong manager, int buildingId)",
                "delegate void LegacyPlayerStateCopyDelegate()",
                "delegate void InitializationStateDelegate(ulong state)",
                "delegate void PlayerStateChoreDelegate()",
                "delegate void ChoreCopyFieldDelegate("
            })
                Check(source.Contains(nativeDelegate), "native delegate ABI missing: " + nativeDelegate);
            Check(source.Contains("ulong pathManager, int playerId, int targetPcl, int sourcePcl, int routeMode"),
                "The audited Script Extender E2610 parameter order is not preserved");
            Check(source.Contains("players.Clear()"), "map transition does not reset sessions");
            Check(source.Contains("activeEconomyContexts?.Clear()") && source.Contains("lastRoutingSnapshot = null"),
                "map transition retains economy or routing diagnostic state");
            Check(source.Contains("activeEconomyOverlayScopes?.Clear()"),
                "map transition retains the nested active overlay context");
            Check(source.Contains("mapLoadBuildingIdentities.Clear(); mapLoadWallTiles.Clear();") &&
                source.Contains("wallBaselines.Clear()") && source.Contains("economyOverlayCaches.Clear()"),
                "map transition retains preplaced wall or economy topology state");
            Check(source.Contains("emittedGridUpdateSignatures.Clear()") &&
                source.Contains("emittedDominantPclSignatures.Clear()"),
                "map transition retains grid/PCL signature aggregation state");
            Check(source.Contains("activeAic - 1") || File.ReadAllText(Path.Combine("src", "DiagnosticModel.cs")).Contains("oneBasedSlot - 1"), "AIC slot is not converted from one-based exactly once");
            Check(source.Contains("CRUSHED_TIMER_ACTIVATED_BY_DAMAGE"), "damage-triggered timer activation diagnostic missing");
            Check(source.Contains("PREPLACED_BASELINE_WALL_DAMAGE") &&
                source.Contains("manager.HeightGrid[tileId]") && source.Contains("manager.DefaultHeightGrid[tileId]") &&
                source.Contains("baseline.LostWallTiles.Contains(anchor.WallTileId)") &&
                !source.Contains("!IsBaselineInteriorConnectedToExterior(baseline)"),
                "tile-based baseline wall damage or breach correlation is incomplete");
            Check(source.Contains("PREPLACED_WOOD_SCORE_DIAGNOSTIC") &&
                source.Contains("PREPLACED_WOOD_SCORE_FIX_APPLIED") &&
                source.Contains("PREPLACED_WOOD_SCORE_MODEL_MISMATCH") &&
                source.Contains("PREPLACED_WOOD_DISPATCH_CORRELATION") &&
                source.Contains("vanillaAcceptedAboveMinus100") &&
                source.Contains("correctedReplay") &&
                model.Contains("WoodAcceptedCandidateCount") &&
                model.Contains("CalculateWoodScore"),
                "compact direct wood-score correlation is missing");
            Check(source.Contains("InaccessibleBuildingCheckRva = 0x3B270") &&
                source.Contains("InaccessibleBuildingSelectionRva = 0x3B360") &&
                source.Contains("EconomyCellPenaltyRva = 0x55E10") &&
                source.Contains("PREPLACED_ECONOMY_CELL_PENALTY") &&
                source.Contains("economyCellPenaltyHook.Original(state, x, y)") &&
                Regex.Matches(source, @"economyCellPenaltyHook\.Original\(").Count == 1 &&
                Regex.Matches(source, @"inaccessibleBuildingCheckHook\.Original\(").Count == 1 &&
                Regex.Matches(source, @"inaccessibleBuildingSelectionHook\.Original\(").Count == 1,
                "byte+13 writer attribution is incomplete");
            Check(source.Contains("MAP_START_POST") && source.Contains("FirstSchedulerSnapshotEmitted") &&
                source.Contains("first-active-crushed-delay-observed") &&
                !source.Contains("FIRST_ACTIVE_CRUSHED_DELAY_RAW"),
                "compact map-start, scheduler, or active-delay observation is missing");
            Check(source.Contains("PollFrame") && plugin.Contains("persistentRuntime?.PollFrame()"), "per-frame crushed timer observation missing");
            Check(source.Contains("PREPLACED_RAW_BUILDINGS") && source.Contains("PREPLACED_RAW_DELTA") &&
                source.Contains("CapturePreplacedBaseline") && source.Contains("HasAnyNonZeroByte") &&
                source.Contains("ReclassifyPendingRawInventories"), "raw building baseline diagnostics missing");
            Check(source.Contains("placement-pcl-unreachable") && source.Contains("PREPLACED_PORTAL_TOPOLOGY"), "placement reachability diagnostics missing");
            Check(source.Contains("economyMode0Eligible=") &&
                source.Contains("kind != NativePortalExcludedKindForEconomyModeZero"),
                "mode-zero economy portal filtering is not explicit in the raw topology diagnostic");
            Check(source.Contains("observationContinues=") && source.Contains("session.Counters.Stop()") &&
                source.Contains("session.DamageCounters") &&
                !source.Contains("FinalizePlayer(playerId, \"first-building-follow-up-complete\")"),
                "Ruins AIV-start shutdown or continuing damage observation is missing");
            Check(source.Contains("BuildingCountModeFieldOffset = 0x2C8"), "building-count mode field contract missing");
            Check(source.Contains("PlacementValidatorResult.Classify"), "validator result contract not used");
            Check(source.Contains("EconomyGridWidth = 160") && source.Contains("EconomyGridCellStride = 0x30") &&
                source.Contains("EconomyGridBaseOffset = 0x5B830") && source.Contains("EconomyVisitGenerationOffset = 0x5B50C"),
                "economy flood-fill layout contract missing");
            Check(source.Contains("index / EconomyGridWidth, index % EconomyGridWidth") &&
                source.Contains("x * EconomyGridWidth + y"), "economy grid x-major index contract is not preserved");
            Check(source.Contains("PREPLACED_ROUTING_SNAPSHOT_FULL") && source.Contains("PREPLACED_ROUTING_CHANGE") &&
                source.Contains("PREPLACED_ECONOMY_SEARCH") && source.Contains("BuildCanonicalPclMap"),
                "economy routing diagnostics or PCL-renumbering canonicalization missing");
            Check(source.Contains("stateGroupCount=") && source.Contains("transitionGroupCount=") &&
                source.Contains("LosslessGridCoordinateFormatter.Format"),
                "routing snapshots are not grouped losslessly");
            Check(source.Contains("frontierRawGroups=") && source.Contains("frontierExpansionPredicates=") &&
                source.Contains("rva58020(sbyte+04<16&&byte+13==0)") &&
                source.Contains("rva575B0(sbyte+04<17)") &&
                source.Contains("rva57B80(sbyte+04-sbyte+16<16)") &&
                source.Contains("rva58950(sbyte+04<15)"),
                "frontier rejection diagnostics are incomplete");
            Check(source.Contains("PREPLACED_PRE_AIV_BASELINE") &&
                source.Contains("PREPLACED_DOMINANT_PCL") &&
                source.Contains("PREPLACED_INIT_CHECKPOINT_TIMER_CHANGE"),
                "compact baseline/PCL/initialization diagnostics are incomplete");
            Check(source.Contains("NativePclEntryCount = (NativePclGridEndRva - NativePclGridRva) / NativePclEntrySize") &&
                source.Contains("NativePclEntryCount != 320800") &&
                !source.Contains("TileManager.PathConnectionGrid"),
                "PCL diagnostics are not restricted to Vanilla's audited 320800-entry range");
            Check(source.Contains("PREPLACED_INVALID_PCL_TILE_ACCESS") &&
                source.Contains("PREPLACED_CONFIRMED_WALL_BREACH") &&
                source.Contains("PREPLACED_POST_BREACH_ECONOMY_SEARCH") &&
                source.Contains("PREPLACED_POST_WALL_LOSS_ECONOMY_SEARCH"),
                "invalid PCL, wall-breach, or post-breach search diagnostics are missing");
            Check(source.Contains("PREPLACED_CRUSHED_TIMER_BULK_COPY") &&
                source.Contains("CaptureSerializedCrushedCounters") &&
                source.Contains("legacyPlayerStateCopyHook.Original()") &&
                source.Contains("PREPLACED_LEGACY_TIMER_FIX_ELIGIBILITY") &&
                source.Contains("destroyedTowerOwnerTransitions") &&
                model.Contains("LegacyTimerFixEligibility"),
                "legacy player-state timer copy diagnostic is incomplete");
            Check(source.Contains("PREPLACED_DYNAMIC_WALL_ROLES") &&
                source.Contains("WallTestRoleClassifier.Classify") &&
                !source.Contains("PREPLACED_PCL_COMPONENT_MERGE"),
                "dynamic wall roles or label-independent breach detection are incomplete");
            Check(source.Contains("PREPLACED_WALL_BASELINE") && source.Contains("PREPLACED_WALL_TILE_CHANGE") &&
                source.Contains("selected-baseline-wall-lost+stable-anchor-connectivity") &&
                source.Contains("wallOnlyClosed=") && source.Contains("wallAwareClosed=") &&
                source.Contains("AddPclAdjacencyAnchors") && source.Contains("IsEnclosureBuilding") &&
                source.Contains("WallOwnerEncodingResolver.Decode") &&
                source.Contains("baseline.GeometryClosed") && source.Contains("BuildingFootprintOverlaps"),
                "tile-based wall role or breach diagnostics are incomplete");
            Check(source.Contains("PREPLACED_SHADOW_ECONOMY_SEARCH") &&
                source.Contains("ShadowEconomySearch.Run") && source.Contains("CountPclTilesOutsideSet") &&
                source.Contains("EmitProactiveShadowSuite") && source.Contains("PREPLACED_NATIVE_RESULT_PRE_RESTORE_MISMATCH") &&
                source.Contains("PREPLACED_SHADOW_NATIVE_NO_RESULT_MISMATCH") &&
                model.Contains("heightDifference < 40") && model.Contains("heightDifference < 30") &&
                model.Contains("heightDifference < 12"),
                "full player-specific shadow economy traversal is missing");
            Check(source.Contains("ReachableFriendlyPcls") && source.Contains("projected04=") &&
                source.Contains("projected16=raw-vanilla-tile-logic") && source.Contains("gate={observation.GateReason}"),
                "player-specific counterfactual or early search gate diagnostic is incomplete");
            Check(source.Contains("PREPLACED_ECONOMY_FIX_OVERLAY") &&
                source.Contains("FindNextComponentTowardDestination") &&
                source.Contains("PathConnectionQueryMode.ExcludeLadderClimb") &&
                source.Contains("context == null || context.State != state") &&
                source.Contains("Even a partial write must leave Vanilla's shared grid byte-identical") &&
                source.Contains("finally") && source.Contains("RestoreEconomyGridOverlay") &&
                source.Contains("Not all 25,600 economy byte+04 values were restored"),
                "active economy overlay or exact restoration guard is incomplete");
            Check(source.Contains("InitializeEconomyAvailabilityRva = 0x55FE0") &&
                source.Contains("InitializeEconomyAvailabilityPattern") &&
                source.Contains("initializeEconomyAvailabilityHook.Original(state, playerId)") &&
                source.Contains("economy-availability-init") &&
                source.Contains("HasParticipatingFriendlyBaselinePortal") &&
                source.Contains("NativeEconomyStartXRva = 0x379AFA8") &&
                source.Contains("NativeEconomyStartYRva = 0x379AFAC") &&
                source.Contains("ValidateNativeEconomyStartRanges") &&
                source.Contains("FriendlyPortalEdge") &&
                source.Contains("edge.Connects(currentPcl, nextPcl)"),
                "startup economy availability census or exact native search origins are not covered");
            Check(source.Contains("PREPLACED_MAP_PROFILE") && source.Contains("PREPLACED_RUINS_BASELINE") &&
                source.Contains("lightweightRuins=") && source.Contains("economyFixEligiblePlayers") &&
                source.Contains("economyOverlayCaches") && source.Contains("IsLikelyRuinsOnlyMap") &&
                source.Contains("suppressDominantPclDiagnostics"),
                "automatic lightweight ruins / walled-economy profiles or overlay cache are missing");
            Check(!source.Contains("FIRST_ACTIVE_CRUSHED_DELAY_RAW") &&
                !source.Contains("CRUSHED_ACTIVATION_RAW") &&
                source.Contains("crushed-timer-step delta="),
                "ruins timer ticks still force full building dumps or are not aggregated");
            Check(source.Contains("FarmPlacementOffsetTablePairCount = 32") &&
                source.Contains("FarmPlacementSelectableOffsetCount = 31") &&
                source.Contains("ValidateFarmPlacementOffsetTable") &&
                source.Contains("ValidateFarmPlacementOffsetRing") &&
                source.Contains("ValidateEconomyNeighborOffsetTable") &&
                source.Contains("nativeSearchTables=") &&
                !source.Contains("FarmPlacementOffsetTableRva + 9 * 2") &&
                source.Contains("capturedBeforeNativeConstruction"),
                "the full 32-entry farm offset ring or pre-construction oracle is missing");
            Check(source.Contains("PREPLACED_NATIVE_ECONOMY_ROUTE_MATRIX") &&
                source.Contains("PathConnectionQueryMode.IncludeAll") &&
                source.Contains("PathConnectionQueryMode.LadderClimbOnly") &&
                source.Contains("PlacementReachabilityRouteCallSiteRva = 0xC3C5D") &&
                source.Contains("ValidatePlacementReachabilityRouteCall"),
                "native gate route matrix or C3BF0-to-E2610 call contract is incomplete");
            Check(Regex.Matches(source, @"farmSearchHook\.Original\(").Count == 2 &&
                Regex.Matches(source, @"resourceSearchHook\.Original\(").Count == 2 &&
                Regex.Matches(source, @"woodSearchHook\.Original\(").Count == 2 &&
                Regex.Matches(source, @"nearbySearchHook\.Original\(").Count == 3 &&
                source.Contains("if (!walledEconomyProfile)"),
                "profile passthrough and overlay branches do not preserve one Vanilla search call per invocation");
            Check(Regex.Matches(source, @"initializeEconomyAvailabilityHook\.Original\(").Count == 2 &&
                source.Contains("private bool ReconcileEconomyAvailability(") &&
                source.Contains("reconcilingEconomyAvailability = true") &&
                source.Contains("PREPLACED_ECONOMY_CENSUS_RECONCILED"),
                "startup and delayed economy censuses do not each have one explicit Vanilla call");
            Check(source.Contains("EconomyFixActivationState.PendingPortal") &&
                source.Contains("EconomyFixActivationState.PendingBreach") &&
                source.Contains("EconomyFixActivationState.ActivePortal") &&
                source.Contains("EconomyFixActivationState.ActiveBreach") &&
                source.Contains("EconomyFixActivationState.Suspended") &&
                source.Contains("PREPLACED_ECONOMY_FIX_") &&
                source.Contains("HasConfirmedBreachEconomyAccess") &&
                source.Contains("IsBaselineInteriorConnectedToExterior") &&
                source.Contains("baseline.LostWallTiles.Any(baseline.ComponentTiles.Contains)") &&
                source.Contains("allowPendingActivation"),
                "delayed portal/breach activation or its physical breach gates are incomplete");
            Check(source.Contains("TryActivateOrRefreshEconomyFix(state, playerId, \"farm-search\")") &&
                source.Contains("TryActivateOrRefreshEconomyFix(state, playerId, \"resource-search-mode-\"") &&
                source.Contains("TryActivateOrRefreshEconomyFix(state, playerId, \"wood-search\")") &&
                source.Contains("TryActivateOrRefreshEconomyFix(state, context.PlayerId, \"nearby-search-\"") &&
                source.Contains("currentMapIsSave") && source.Contains("EconomyFixState = currentMapIsSave"),
                "economy searches do not recheck pending activation or save/breach guards");
            Check(source.Contains("PREPLACED_LEGACY_TIMER_FIX_APPLIED") &&
                source.Contains("PREPLACED_LEGACY_TIMER_FIX_SKIPPED") &&
                source.Contains("damageActivatedTimerOwners") &&
                model.Contains("ClassifyAtApplication"),
                "active legacy timer normalization is not narrowly guarded");
            Check(source.Contains("building.OwnerId == playerId && building.Id > 0 &&") &&
                source.Contains("IsDestroyedTower(building.Type)") &&
                !Regex.IsMatch(source, @"building\.OwnerId == playerId\s*&&\s*!IsLiving\(building\)\s*&&\s*IsDestroyedTower"),
                "destroyed tower types are still excluded by their misleading AliveState");
            Check(source.Contains("PREPLACED_FARM_NATIVE_ORACLE_MATCH") &&
                source.Contains("PREPLACED_FARM_NATIVE_ORACLE_MISMATCH") &&
                source.Contains("ConstructionObservations") && source.Contains("prefilter-candidate"),
                "farm oracle is not correlated with the native construction call");
            Check(source.Contains("PREPLACED_PLAYER_STATE_CHORE") &&
                source.Contains("PREPLACED_PLAYER_STATE_FIELD_COPY") &&
                source.Contains("SerializedCrushedCounterOffset") &&
                source.Contains("PlayerResourcesOffsetInSerializedRecord") &&
                source.Contains("PlayerStateRecordCopyCallSiteRva") &&
                source.Contains("ChoreCopyFieldMemcpyCallSiteRva") &&
                source.Contains("ChoreCopyFieldEndRva"),
                "current-format player-state timer transfer diagnostic is incomplete");
            Check(source.Contains("PREPLACED_INIT_CHECKPOINT_REACHED") &&
                source.Contains("emittedInitializationCheckpoints.Clear()"),
                "initialization checkpoint reachability is not logged or reset");
            Check(source.Contains("ResourceCandidateRejectionReason") &&
                model.Contains("owner-class-mismatch-byte+15") &&
                source.Contains("outside-depth-or-disconnected"),
                "resource shadow rejection diagnostics are incomplete");
            Check(source.Contains("AddContextHook(woodScoreFloorHook") &&
                source.Contains("WoodScoreFloorHookRva = 0x58057") &&
                source.Contains("WoodScoreFloorHookLength = 15") &&
                source.Contains("OverwrittenInstructionPlacement.BeforeCallback") &&
                !source.Contains("CrushedTimerWriter") && model.Contains("InlineHookBranchSafety"),
                "the safe scoped wood-score hook or removed damage-writer contract is inconsistent");
            Check(source.Contains("origin={(IsCurrentPreplaced(building.Id) ? \"baseline\" : \"runtime-aiv\")}") &&
                source.Contains("FullPortalTopologyEmitted") && source.Contains("FullNativeRouteMatrixEmitted"),
                "baseline/runtime portals or compact topology transitions are not distinguished");
            Check(source.Contains("if (args.Phase == EventHookPhase.Pre) initializationTracingActive = true"),
                "initialization tracing does not start at OnStartMap Pre");
            Check(source.Contains("0x115830-unit-subsystem") && source.Contains("0x102C30-map-object-reset") &&
                source.Contains("0x2A340-player-pathing"),
                "timer checkpoints around the final map initialization sequence are incomplete");
            Check(!plugin.Contains("OnDestroy(") && !plugin.Contains("OnDisable(") &&
                !plugin.Contains("OnApplicationQuit("), "forbidden Unity teardown callback found");
            Check(Regex.Matches(source, @"economyGridUpdateHook\.Original\(state, mode\)").Count == 2 &&
                source.Contains("finally { suppressDominantPclDiagnostics = false; }") &&
                !source.Contains("before = EconomyGridBuildSnapshot.Capture"),
                "hot economy-grid updates still perform full diagnostic snapshots or lose the Vanilla call");
            Check(Regex.Matches(source, @"CaptureRoutingSnapshot\(").Count == 1 &&
                Regex.Matches(source, @"CaptureNativeTraversalSnapshot\(").Count == 0 &&
                Regex.Matches(source, @"ObservePortalTopology\(").Count == 1 &&
                source.Contains("session.EmittedWoodDifferentialStates.Add(scoreState)") &&
                source.Contains("wood-score compact-repeat") &&
                source.Contains("AnalyzeEconomySearchCompact") &&
                source.Contains("PREPLACED_ECONOMY_SEARCH_COMPACT"),
                "wall-map hot paths still invoke full routing/traversal diagnostics");
            Check(source.Contains("mapLoadBuildingIdentities") && source.Contains("mapLoadWallTiles") &&
                source.Contains("preAivBaselineCaptured") && source.Contains("MatchesStableRecord") &&
                source.Contains("IsCurrentPreplaced(value.Id)") && source.Contains("IsPortalStructure(value.Type)") &&
                source.Contains("if (!mapLoadWallTiles.Contains(tileId)) continue;"),
                "preplaced portal/wall roles are not constrained to the map-load baseline");
            int baselineCapture = source.IndexOf(
                "CaptureMapLoadBuildingIdentities(\"first-allocate-spec.pre\")", StringComparison.Ordinal);
            int allocateOriginal = source.IndexOf(
                "int result = allocateHook.Original(state, playerId);", StringComparison.Ordinal);
            Check(baselineCapture >= 0 && allocateOriginal > baselineCapture &&
                source.Contains("CaptureMapLoadBuildingIdentities(\"map-load.post-fallback\")") &&
                source.Contains("if (preAivBaselineCaptured) return;") &&
                source.Contains("preAivBaselineCaptured = false;") &&
                !source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => line.Contains("ObservePhase(") && line.Contains("true")),
                "pre-AIV baseline is late, overwriteable, not reset, or still emits full phase inventories");
            Check(!source.Contains("pos=({args.TileX},{args.TileY}) free={args.IsFree}") &&
                !source.Contains("pos=({args.TileX},{args.TileY}) custom={args.CustomValidationRules}"),
                "high-frequency placement counters still create one aggregation key per coordinate");
            Check(!source.Contains("execute.frame={frame}") &&
                !source.Contains("placement-helper result={result} mapper={(eMappers)mapperValue}{position}") &&
                !source.Contains("native-validator outcome={outcome} result={result} mapper={(eMappers)mapperValue}{tile}") &&
                !source.Contains("NativeByte04") && !source.Contains("cache.ChangedCells") &&
                !source.Contains("Counters.Add(\"construct-building \" + call)") &&
                source.Contains("construct-building context=") &&
                source.Contains("economy-overlay helper={overlay.Helper} restored={restored}"),
                "hot-path aggregates or overlay caching still depend on transient coordinates/frames/Vanilla decay values");
            Check(source.Contains("confirmed == null || !nativeEconomyAccess") &&
                source.Contains("baseline.LostWallTiles.Contains(anchor.WallTileId)") &&
                source.Contains("insidePcl == outsidePcl") &&
                source.Contains("reachablePcls.Contains(insidePcl)"),
                "breach activation is not gated by the lost tile's stable PCL anchor and native keep reachability");
            Check(source.Contains("if (offset != 0x05) signature = Hash") &&
                !source.Contains("Hash(Hash(1469598103934665603UL, after.Generation)"),
                "transient search generation/distance still defeats aggregation");
            Check(source.Contains("PREPLACED_LETHAL_DAMAGE") && source.Contains("DescribeDamageAggregate"),
                "damage logging does not combine compact aggregation with complete lethal evidence");
            Check(source.Contains("CaptureAndAnalyzePclTopology(\"FIRST_ECONOMY_SEARCH_PLAYER_\" + playerId)") &&
                !source.Contains("CaptureRoutingSnapshot(state, \"FIRST_ECONOMY_SEARCH_PLAYER_"),
                "first economy search still forces a duplicate full routing snapshot");
            Check(source.Contains("before, result != 0") && source.Contains("search.CandidateFound"),
                "farm result is not used instead of stale shared result coordinates");
            Check(source.Contains("CaptureOwnedIdentities") && source.Contains("FirstAivSpawnCorrelation.Matches"),
                "first AIV building identity correlation missing");
            Check(source.Contains("transaction?.DisableAll()"), "native diagnostic failure does not defensively disable committed hooks");
            Check(!source.Contains("MaximumCapture") && !source.Contains("Take(100"), "fixed event cap found");
            Check(assemblyInfo.Contains("AssemblyVersion(\"0.1.1.0\")") &&
                assemblyInfo.Contains("AssemblyFileVersion(\"0.1.1.0\")") &&
                assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.1\")") &&
                plugin.Contains("PluginVersion = \"0.1.1\"") && manifest.Contains("\"Version\": \"0.1.1\""),
                "active version declarations are inconsistent");
            Check(plugin.Contains("BepInDependency(ScriptExtenderGuid, \"2.4.0\")") &&
                plugin.Contains("testedScriptExtender=2.5.0") &&
                plugin.Contains("5f02af6d074af7c741ebdaaccb48add39eba1bf4") &&
                manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.4.0\"") &&
                manifest.Contains("\"NetworkMode\": 1"),
                "Script Extender compatibility or active test-fix network contract is inconsistent");
        }

        private static void TestNativeSignaturesAgainstCanonicalDll()
        {
            const string expectedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
            string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
            byte[] file = File.ReadAllBytes(dll);
            using (SHA256 sha = SHA256.Create())
                Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == expectedHash, "canonical native hash changed");

            byte[] image = file;
            string source = File.ReadAllText(Path.Combine("src", "PreplacedTestRuntime.cs"));
            MatchCollection definitions = Regex.Matches(source,
                @"private const string (?<name>\w+Pattern)\s*=\s*(?<body>.*?);", RegexOptions.Singleline);
            Check(definitions.Count >= 52, "not all native signatures were discovered by the static test");
            foreach (Match definition in definitions)
            {
                string name = definition.Groups["name"].Value;
                string stem = name.Substring(0, name.Length - "Pattern".Length);
                string pattern = string.Concat(Regex.Matches(definition.Groups["body"].Value, "\"(?<s>[^\"]*)\"")
                    .Cast<Match>().Select(m => m.Groups["s"].Value));
                Match rvaMatch = Regex.Match(source, @"private const int " + Regex.Escape(stem) + @"Rva\s*=\s*0x(?<rva>[0-9A-Fa-f]+)");
                Check(rvaMatch.Success, "reference RVA missing for " + name);
                int rva = Convert.ToInt32(rvaMatch.Groups["rva"].Value, 16);
                PatternByte[] parsed = ParsePattern(pattern);
                Check(parsed.Length != 0 && parsed[0].Wildcard == false &&
                    parsed[0].Value != 0xE8 && parsed[0].Value != 0xE9 && parsed[0].Value != 0xFF,
                    name + " begins like a call/jump site instead of a function target");
                int rawOffset = RvaToRaw(file, rva);
                Check(Matches(image, rawOffset, parsed), name + " does not match its reference RVA");
                int matches = 0;
                for (int offset = 0; offset <= image.Length - parsed.Length; offset++)
                    if (Matches(image, offset, parsed)) matches++;
                Check(matches == 1, name + " is not unique: " + matches);
            }

            int firstWriterBranchRaw = RvaToRaw(file, 0x7F05D);
            int secondWriterBranchRaw = RvaToRaw(file, 0x7F072);
            Check(file[firstWriterBranchRaw] == 0x75 && file[secondWriterBranchRaw] == 0x75,
                "audited damage-writer inbound branches changed opcode");
            int firstWriterTarget = 0x7F05D + 2 + unchecked((sbyte)file[firstWriterBranchRaw + 1]);
            int secondWriterTarget = 0x7F072 + 2 + unchecked((sbyte)file[secondWriterBranchRaw + 1]);
            Check(firstWriterTarget == 0x7F07C && secondWriterTarget == 0x7F07C &&
                InlineHookBranchSafety.HasInboundTargetInside(0x7F074, 15,
                    new[] { firstWriterTarget, secondWriterTarget }),
                "the known unsafe writer-hook span is no longer recognized as having an inbound branch");
            byte[] woodScoreHookBytes =
            {
                0x48, 0x63, 0xC2,
                0x4C, 0x69, 0xC8, 0x3C, 0x58, 0x00, 0x00,
                0xB8, 0x67, 0x66, 0x66, 0x66
            };
            int woodScoreHookRaw = RvaToRaw(file, 0x58057);
            Check(file.Skip(woodScoreHookRaw).Take(woodScoreHookBytes.Length).SequenceEqual(woodScoreHookBytes) &&
                woodScoreHookBytes.Length == 15 && 0x58057 + woodScoreHookBytes.Length == 0x58066,
                "wood-score hook bytes or exact instruction boundary changed");
            int[] woodSearchBranchTargets =
            {
                0x580DD, 0x5837D, 0x58348, 0x581CE, 0x58309, 0x58304, 0x582B9,
                0x58265, 0x58263, 0x58318, 0x58297, 0x5831F, 0x582B2, 0x582B9,
                0x581A0, 0x5831F, 0x5833C, 0x58150, 0x58378
            };
            Check(!InlineHookBranchSafety.HasInboundTargetInside(0x58057, 15, woodSearchBranchTargets),
                "an audited 0x58020 branch enters the wood-score hook interior");
            Check((0x51890D0 - 0x50EC690) / sizeof(ushort) == 320800 &&
                !TryRvaToRaw(file, 0x50EC690, out _) && !TryRvaToRaw(file, 0x51890D0 - 1, out _),
                "native PCL range length or PE bounds changed");
            int farmOffsetTableRaw = RvaToRaw(file, 0x2D13B0);
            int[] farmOffsets = Enumerable.Range(0, 64)
                .Select(index => BitConverter.ToInt32(file, farmOffsetTableRaw + index * sizeof(int))).ToArray();
            Check(farmOffsetTableRaw == 0x2CF9B0 && farmOffsets.SequenceEqual(new[]
                {
                    0, 0, 0, -1, 1, -1, 1, 0, 1, 1, 0, 1, -1, 1, -1, 0,
                    -1, -1, 0, -2, 2, -2, 2, 0, 2, 2, 0, 2, -2, 2, -2, 0,
                    -2, -2, 0, -3, 3, -3, 3, 0, 3, 3, 0, 3, -3, 3, -3, 0,
                    -3, -3, 0, -4, 4, -4, 4, 0, 4, 4, 0, 4, -4, 4, -4, 0
                }) &&
                !TryRvaToRaw(file, 0x60AD4AC, out _),
                "farm placement-offset table or construction-error storage contract changed");
            byte[] farmRingWriter =
            {
                0xFF, 0x81, 0x08, 0xB5, 0x05, 0x00,
                0x48, 0x8D, 0x1D, 0x25, 0xCC, 0x74, 0x03,
                0x4C, 0x89, 0x60, 0xE8,
                0x4C, 0x8D, 0x35, 0x7E, 0x4E, 0x61, 0x03,
                0x8B, 0x81, 0x08, 0xB5, 0x05, 0x00,
                0x44, 0x8D, 0x67, 0x04, 0x33, 0xF6, 0x8B, 0xEF,
                0x83, 0xF8, 0x1F, 0x89, 0xB1, 0x64, 0xDA, 0x03, 0x00,
                0x0F, 0x4D, 0xC6, 0x89, 0x81, 0x08, 0xB5, 0x05, 0x00
            };
            int farmRingRaw = RvaToRaw(file, 0x5737A);
            Check(file.Skip(farmRingRaw).Take(farmRingWriter.Length).SequenceEqual(farmRingWriter),
                "farm placement ring no longer selects only indices 0..30");
            int neighborTableRaw = RvaToRaw(file, 0x2D2E50);
            int[] neighborOffsets = Enumerable.Range(0, 16)
                .Select(index => BitConverter.ToInt32(file, neighborTableRaw + index * sizeof(int))).ToArray();
            Check(neighborOffsets.SequenceEqual(new[]
                { 0, -1, 1, -1, 1, 0, 1, 1, 0, 1, -1, 1, -1, 0, -1, -1 }),
                "economy search neighbor order or diagonal table changed");
            byte[] reachabilityCallBlock =
            {
                0x48, 0x8D, 0x0D, 0x0B, 0x9A, 0xFE, 0x05,
                0xC7, 0x44, 0x24, 0x20, 0x00, 0x00, 0x00, 0x00,
                0xE8, 0xAE, 0xE9, 0x01, 0x00, 0x85, 0xC0, 0x75, 0x05
            };
            int reachabilityCallRaw = RvaToRaw(file, 0xC3C4E);
            Check(file.Skip(reachabilityCallRaw).Take(reachabilityCallBlock.Length).SequenceEqual(reachabilityCallBlock) &&
                0xC3C5D + 5 + BitConverter.ToInt32(file, RvaToRaw(file, 0xC3C5D) + 1) == 0xE2610,
                "C3BF0 no longer passes mode zero to E2610 with the audited source/target register contract");
            Check(RvaToRaw(file, 0x50720) == 0x4FB20 && RvaToRaw(file, 0x55FE0) == 0x553E0 &&
                RvaToRaw(file, 0x96E30) == 0x96230 && RvaToRaw(file, 0x572B0) == 0x566B0 &&
                RvaToRaw(file, 0xD4290) == 0xD3690 && RvaToRaw(file, 0x96CE) == 0x8ACE,
                "audited code RVA to FileOffset mapping changed");
            int[] finalSites = { 0x96D2C, 0x96D38, 0x96D49, 0x96D55, 0x96E30 };
            int[] finalTargets = { 0x115830, 0x102C30, 0x50720, 0x2A340, 0x55FE0 };
            for (int index = 0; index < finalSites.Length; index++)
            {
                int raw = RvaToRaw(file, finalSites[index]);
                Check(file[raw] == 0xE8 && finalSites[index] + 5 + BitConverter.ToInt32(file, raw + 1) ==
                    finalTargets[index], "final map-start checkpoint call target changed");
            }
            int copyCallRaw = RvaToRaw(file, 0x96CE);
            Check(file[copyCallRaw] == 0xE8 && copyCallRaw + 5 + BitConverter.ToInt32(file, copyCallRaw + 1) ==
                RvaToRaw(file, 0xD4290), "legacy player-state copy call target changed");
            byte[] versionGate = { 0x81, 0xFA, 0xD5, 0x00, 0x00, 0x00, 0x7D, 0x0B };
            Check(file.Skip(copyCallRaw - versionGate.Length).Take(versionGate.Length).SequenceEqual(versionGate) &&
                source.Contains("LegacyPlayerStateCopyVersionExclusive = 0xD5"),
                "legacy player-state copy map-version gate changed");

            int recordCopyCallRaw = RvaToRaw(file, 0x15C4A);
            Check(file[recordCopyCallRaw] == 0xE8 && 0x15C4A + 5 +
                BitConverter.ToInt32(file, recordCopyCallRaw + 1) == 0x1F5F0,
                "current player-state chore no longer calls the audited field-copy helper");
            byte[] recordCopySetup =
            {
                0x48, 0x63, 0x05, 0xFF, 0xB6, 0x6A, 0x08, 0x45, 0x33, 0xC9,
                0x48, 0x69, 0xD0, 0x3C, 0x58, 0x00, 0x00,
                0x48, 0x8D, 0x05, 0x92, 0x51, 0x78, 0x03,
                0x41, 0xB8, 0x3C, 0x58, 0x00, 0x00, 0x48, 0x03, 0xD0, 0x48, 0x8B, 0xCB, 0xE8
            };
            int recordSetupRaw = RvaToRaw(file, 0x15C26);
            Check(file.Skip(recordSetupRaw).Take(recordCopySetup.Length).SequenceEqual(recordCopySetup),
                "current player-state record base, stride, size, or call setup changed");
            int memcpyCallRaw = RvaToRaw(file, 0x1F65D);
            Check(file[memcpyCallRaw] == 0xE8 && 0x1F65D + 5 +
                BitConverter.ToInt32(file, memcpyCallRaw + 1) == 0x7140 &&
                file[RvaToRaw(file, 0x1F68C)] == 0xC3,
                "chore field-copy memcpy target or function boundary changed");

            string functions = File.ReadAllText(Path.Combine("..", "..", "_inspect", "CrusaderDE-Native-Baseline", "sem", "FBCB9319", "exports", "semantic-functions.jsonl"));
            foreach (string rva in new[] { "0x50680", "0x50720", "0x55E10", "0x55FE0", "0x572B0", "0x7EB00", "0xD4290", "0x15B90", "0x1F5F0", "0xC3FA0", "0xC43A0", "0xB8310", "0x115830", "0x102C30", "0x2A340", "0x3B270", "0x3B360", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xCC420", "0x414A0", "0x41230", "0x41380", "0x41280", "0x3B1D0", "0x50340", "0x504F0", "0xB8270", "0xC3BF0", "0xC8F50", "0xC90E0", "0x50D80", "0x50E00", "0x50F90", "0x51190", "0x51270", "0x51540", "0x57330", "0x575B0", "0x57B80", "0x58020", "0x583A0", "0x58950", "0x58BE0", "0x6D580", "0xE2610", "0x94350" })
                Check(functions.Contains("\"rva\":\"" + rva + "\""), "baseline function boundary missing: " + rva);
        }

        private static int RvaToRaw(byte[] file, int rva)
        {
            if (TryRvaToRaw(file, rva, out int rawOffset)) return rawOffset;
            throw new InvalidOperationException("RVA is not backed by PE file data: 0x" + rva.ToString("X"));
        }

        private static bool TryRvaToRaw(byte[] file, int rva, out int result)
        {
            result = -1;
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            int sectionTable = optional + optionalSize;
            for (int index = 0; index < sections; index++)
            {
                int header = sectionTable + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rva >= virtualAddress && rva < virtualAddress + rawSize)
                {
                    result = rawOffset + rva - virtualAddress;
                    return result >= 0 && result < file.Length;
                }
            }
            return false;
        }

        private static PatternByte[] ParsePattern(string pattern) => pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token == "??" ? new PatternByte(0, true) : new PatternByte(Convert.ToByte(token, 16), false)).ToArray();

        private static bool Matches(byte[] image, int offset, PatternByte[] pattern)
        {
            if (offset < 0 || offset > image.Length - pattern.Length) return false;
            for (int index = 0; index < pattern.Length; index++)
                if (!pattern[index].Wildcard && image[offset + index] != pattern[index].Value) return false;
            return true;
        }

        private readonly struct PatternByte
        {
            public PatternByte(byte value, bool wildcard) { Value = value; Wildcard = wildcard; }
            public byte Value { get; }
            public bool Wildcard { get; }
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Check failed: " + message);
        }
    }
}
