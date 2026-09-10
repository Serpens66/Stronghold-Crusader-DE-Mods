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
                TestFirstAivBuildingEligibility();
                TestEconomyDiagnosticModels();
                TestEconomyPclModels();
                TestPreplacedIdentityAndCountProjection();
                TestPortalRoutes();
                TestPclConnectivityTransitions();
                TestDynamicWallRolesAndSearchGates();
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
            Check(EconomySearchGateClassifier.Classify(true, 9, 8, 0, 0) == "traversal-performed",
                "performed traversal was hidden by cooldown state");
            Check(EconomySearchGateClassifier.Classify(false, 5, 4, 0, 0) == "early-return-cooldown",
                "cooldown early return classification");
            Check(EconomySearchGateClassifier.Classify(false, -1, -1, 1, 1) ==
                "early-return-shared-queue-state", "shared queue early return classification");
            Check(EconomySearchGateClassifier.Classify(false, -1, -1, 0, 0) ==
                "early-return-unresolved-mode-or-state", "unresolved early return classification");
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
            string assemblyInfo = File.ReadAllText(Path.Combine("src", "AssemblyInfo.cs"));
            string plugin = File.ReadAllText(Path.Combine("src", "PreplacedTestPlugin.cs"));
            string manifest = File.ReadAllText("info.json");
            string spanReport = File.ReadAllText("ScriptExtenderPathConnectionGridSpanReport.md");
            string updateGuide = File.ReadAllText("UpdateToNewDLL.md");
            string helper = File.ReadAllText(Path.Combine("..", "Shared", "DebugLogHelper.cs"));
            Check(helper.Contains("FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"), "native hash contract missing");
            Check(spanReport.Contains("`0x50720` | `0x4FB20`") &&
                spanReport.Contains("`0x572B0` | `0x566B0`") &&
                spanReport.Contains("zero-filled virtual part of `.data`") &&
                spanReport.Contains("## Practical impact"),
                "Script Extender PCL span report lacks file offsets or practical impact");
            Check(updateGuide.Contains("`0xD4290`") && updateGuide.Contains("`0x96CE`") &&
                updateGuide.Contains("`0x37CC7EC`") && updateGuide.Contains("RollbackAndThrow"),
                "native update guide does not cover the new timer-copy contract");
            foreach (string rva in new[] { "0x50680", "0x50720", "0x572B0", "0x7EB00", "0x7F052", "0x7F074", "0xD4290", "0x96CE", "0x37CC7EC", "0x379ADD0", "0x32DC084", "0x50EC690", "0x51890D0", "0xC3FA0", "0xC43A0", "0xB8310", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xB8270", "0xC3BF0", "0xC8F50", "0xC90E0", "0x50D80", "0x50E00", "0x50F90", "0x51190", "0x51270", "0x51540", "0x575B0", "0x57B80", "0x58020", "0x58950", "0x6D580", "0xE2610", "0x60AD660" })
                Check(source.Contains(rva), "RVA missing: " + rva);
            foreach (string contract in new[] { "AivSpecStride = 0x6D98", "PlayerRuntimeStateStride = 0x583C", "PreparedLayoutFrameCount = 0x922", "PreparedEntrySize = 0x0C", "PauseTableEntryCount =", "pauseIndex < PauseTableEntryCount", "EconomyGridWidth = 160", "EconomyGridCellStride = 0x30", "EconomyGridBaseOffset = 0x5B830", "EconomyReferencePclOffset = 0x5B504", "EconomyVisitGenerationOffset = 0x5B50C", "WoodSearchCooldownRelativeOffset = 0x167C", "FarmSearchCooldownRelativeOffset = 0x167E", "QuarrySearchCooldownRelativeOffset = 0x1680", "IronSearchCooldownRelativeOffset = 0x1682", "PitchSearchCooldownRelativeOffset = 0x1684", "ValidateSize(typeof(GameBuilding), 0x32C)", "ValidateSize(typeof(GameGatehouseEntry), 0x204)", "UnmanagedFunctionPointer(CallingConvention.Cdecl)" })
                Check(source.Contains(contract), "native ABI/offset contract missing: " + contract);
            foreach (string nativeDelegate in new[]
            {
                "delegate int CountBuildingsDelegate(ulong manager, int playerId, int structureType, int mode)",
                "delegate int PlacementReachabilityDelegate(ulong manager, int playerId, int structureType, int x, int y)",
                "delegate void AccessibilitySweepDelegate(ulong manager, int playerId)",
                "delegate int BuildingAccessibilityDelegate(ulong manager, int buildingId, int mode)",
                "delegate long EconomyFarmDelegate(ulong state, int playerId, int desiredStructureType)",
                "delegate void ResourceSearchDelegate(ulong state, int playerId, int mode)",
                "delegate void ConstructBuildingDelegate(",
                "delegate int RegionPairReachabilityDelegate(",
                "delegate void EconomyGridUpdateDelegate(ulong state, int mode)",
                "delegate int SelectDominantPclDelegate(ulong state)",
                "delegate void PlayerBuildingInitializationDelegate(ulong manager, int playerId)",
                "delegate void BuildingInitializationDelegate(ulong manager, int buildingId)",
                "delegate void LegacyPlayerStateCopyDelegate()"
            })
                Check(source.Contains(nativeDelegate), "native delegate ABI missing: " + nativeDelegate);
            Check(source.Contains("ulong pathManager, int playerId, int targetPcl, int sourcePcl, int routeMode"),
                "Script Extender 2.3.0 E2610 parameter order is not preserved");
            Check(source.Contains("players.Clear()"), "map transition does not reset sessions");
            Check(source.Contains("activeEconomyContexts?.Clear()") && source.Contains("lastRoutingSnapshot = null"),
                "map transition retains economy or routing diagnostic state");
            Check(source.Contains("lastPclTopology = null") &&
                source.Contains("pendingCrushedWriterSignals.Clear()"),
                "map transition retains PCL topology or native writer signals");
            Check(source.Contains("emittedGridUpdateSignatures.Clear()") &&
                source.Contains("emittedDominantPclSignatures.Clear()"),
                "map transition retains grid/PCL signature aggregation state");
            Check(source.Contains("activeAic - 1") || File.ReadAllText(Path.Combine("src", "DiagnosticModel.cs")).Contains("oneBasedSlot - 1"), "AIC slot is not converted from one-based exactly once");
            Check(source.Contains("CRUSHED_TIMER_ACTIVATED_BY_DAMAGE"), "damage-triggered timer activation diagnostic missing");
            Check(source.Contains("MAP_START_POST") && source.Contains("FIRST_SCHEDULER") && source.Contains("FIRST_ACTIVE_CRUSHED_DELAY"), "required building snapshots missing");
            Check(source.Contains("PollFrame") && plugin.Contains("persistentRuntime?.PollFrame()"), "per-frame crushed timer observation missing");
            Check(source.Contains("PREPLACED_RAW_BUILDINGS") && source.Contains("PREPLACED_RAW_DELTA") &&
                source.Contains("CapturePreplacedBaseline") && source.Contains("HasAnyNonZeroByte") &&
                source.Contains("ReclassifyPendingRawInventories"), "raw building baseline diagnostics missing");
            Check(source.Contains("placement-pcl-unreachable") && source.Contains("PREPLACED_PORTAL_TOPOLOGY"), "placement reachability diagnostics missing");
            Check(source.Contains("economyMode0Eligible=") &&
                source.Contains("kind != NativePortalExcludedKindForEconomyModeZero"),
                "mode-zero economy portal filtering is not explicit in the raw topology diagnostic");
            Check(source.Contains("observationContinues=true") && !source.Contains("FinalizePlayer(playerId, \"first-building-follow-up-complete\")"), "observation still stops after first AIV building");
            Check(source.Contains("BuildingCountModeFieldOffset = 0x2C8"), "building-count mode field contract missing");
            Check(source.Contains("PlacementValidatorResult.Classify"), "validator result contract not used");
            Check(source.Contains("EconomyGridWidth = 160") && source.Contains("EconomyGridCellStride = 0x30") &&
                source.Contains("EconomyGridBaseOffset = 0x5B830") && source.Contains("EconomyVisitGenerationOffset = 0x5B50C"),
                "economy flood-fill layout contract missing");
            Check(source.Contains("index / EconomyGridWidth, index % EconomyGridWidth") &&
                source.Contains("x * EconomyGridWidth + y"), "economy grid x-major index contract is not preserved");
            Check(source.Contains("PREPLACED_ROUTING_SNAPSHOT_FULL") && source.Contains("PREPLACED_ROUTING_CHANGE") &&
                source.Contains("PREPLACED_ECONOMY_SEARCH"), "economy routing diagnostics missing");
            Check(source.Contains("stateGroupCount=") && source.Contains("transitionGroupCount=") &&
                source.Contains("LosslessGridCoordinateFormatter.Format"),
                "routing snapshots are not grouped losslessly");
            Check(source.Contains("frontierRawGroups=") && source.Contains("frontierExpansionPredicates=") &&
                source.Contains("rva58020(sbyte+04<16&&byte+13==0)") &&
                source.Contains("rva575B0(sbyte+04<17)") &&
                source.Contains("rva57B80(sbyte+04-sbyte+16<16)") &&
                source.Contains("rva58950(sbyte+04<15)"),
                "frontier rejection diagnostics are incomplete");
            Check(source.Contains("PREPLACED_ECONOMY_GRID_UPDATE") &&
                source.Contains("PREPLACED_DOMINANT_PCL") &&
                source.Contains("PREPLACED_INIT_CHECKPOINT_TIMER_CHANGE"),
                "new grid/PCL/initialization diagnostics are incomplete");
            Check(source.Contains("NativePclEntryCount = (NativePclGridEndRva - NativePclGridRva) / NativePclEntrySize") &&
                source.Contains("NativePclEntryCount != 320800") &&
                !source.Contains("TileManager.PathConnectionGrid"),
                "PCL diagnostics are not restricted to Vanilla's audited 320800-entry range");
            Check(source.Contains("PREPLACED_INVALID_PCL_TILE_ACCESS") &&
                source.Contains("PREPLACED_CONFIRMED_WALL_BREACH") &&
                source.Contains("PREPLACED_POST_BREACH_ECONOMY_SEARCH"),
                "invalid PCL, wall-breach, or post-breach search diagnostics are missing");
            Check(source.Contains("PREPLACED_CRUSHED_TIMER_BULK_COPY") &&
                source.Contains("CaptureSerializedCrushedCounters") &&
                source.Contains("legacyPlayerStateCopyHook.Original()"),
                "legacy player-state timer copy diagnostic is incomplete");
            Check(source.Contains("PREPLACED_DYNAMIC_WALL_ROLES") &&
                source.Contains("WallTestRoleClassifier.Classify") &&
                !source.Contains("PREPLACED_PCL_COMPONENT_MERGE"),
                "dynamic wall roles or label-independent breach detection are incomplete");
            Check(source.Contains("ReachableFriendlyPcls") && source.Contains("projected04=") &&
                source.Contains("projected16=unchanged-unproven") && source.Contains("gate={observation.GateReason}"),
                "player-specific counterfactual or early search gate diagnostic is incomplete");
            Check(source.Contains("PREPLACED_CRUSHED_TIMER_NATIVE_WRITE") &&
                source.Contains("CrushedTimerWriterDisplacedLength = 15") &&
                source.Contains("OverwrittenInstructionPlacement.BeforeCallback") &&
                source.Contains("X64SmartCPUContextRegs.All") &&
                source.Contains("crushedTimerWriterHook.Hook.DisplacedByteCount"),
                "passive crushed-timer writer contract is incomplete");
            foreach (string writerRead in new[] { "record + 0x12C", "record + 0x12E", "record + 0x132",
                "record + 0x134", "record + 0x14A", "record + 0x14C", "record + 0x15A",
                "record + 0x15C", "record + 0x168", "record + 0x16A", "stack + 0xC0",
                "stack + 0xC8", "stack + 0xD0", "stack + 0xD8", "stack + 0xE0" })
                Check(source.Contains(writerRead), "crushed timer writer snapshot offset missing: " + writerRead);
            Check(source.Contains("if (args.Phase == EventHookPhase.Pre) initializationTracingActive = true"),
                "initialization tracing does not start at OnStartMap Pre");
            Check(source.Contains("Safe(() => before = EconomyGridBuildSnapshot.Capture") &&
                source.Contains("if (before == null) return;"),
                "new pre-call diagnostics can prevent their Vanilla calls");
            Check(source.Contains("if (offset != 0x05) signature = Hash") &&
                !source.Contains("Hash(Hash(1469598103934665603UL, after.Generation)"),
                "transient search generation/distance still defeats aggregation");
            Check(source.Contains("PREPLACED_LETHAL_DAMAGE") && source.Contains("DescribeDamageAggregate"),
                "damage logging does not combine compact aggregation with complete lethal evidence");
            Check(source.Contains("FIRST_ECONOMY_SEARCH_PLAYER_\" + playerId, false"),
                "first economy search still forces a duplicate full routing snapshot");
            Check(source.Contains("before, result != 0") && source.Contains("search.CandidateFound"),
                "farm result is not used instead of stale shared result coordinates");
            Check(source.Contains("CaptureOwnedIdentities") && source.Contains("FirstAivSpawnCorrelation.Matches"),
                "first AIV building identity correlation missing");
            Check(source.Contains("transaction?.DisableAll()"), "native diagnostic failure does not defensively disable committed hooks");
            Check(!source.Contains("MaximumCapture") && !source.Contains("Take(100"), "fixed event cap found");
            Check(assemblyInfo.Contains("AssemblyVersion(\"0.1.0.0\")") &&
                assemblyInfo.Contains("AssemblyFileVersion(\"0.1.0.0\")") &&
                assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.0\")") &&
                plugin.Contains("PluginVersion = \"0.1.0\"") && manifest.Contains("\"Version\": \"0.1.0\""),
                "active version declarations are inconsistent");
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
            Check(definitions.Count >= 44, "not all native signatures were discovered by the static test");
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

            byte[] writerBytes = { 0x46, 0x89, 0xAC, 0x11, 0xB0, 0xD8, 0x79, 0x03,
                0x4C, 0x8D, 0x2D, 0x2D, 0xDB, 0x44, 0x06 };
            int writerRaw = RvaToRaw(file, 0x7F074);
            Check(file.Skip(writerRaw).Take(writerBytes.Length).SequenceEqual(writerBytes),
                "crushed timer writer does not cover the exact audited store-plus-LEA block");
            Check(0x7F074 >= 0x7EB00 && 0x7F074 + writerBytes.Length <= 0x7EB00 + 0xD7A,
                "crushed timer writer is outside the audited damage function boundary");
            Check((0x51890D0 - 0x50EC690) / sizeof(ushort) == 320800 &&
                !TryRvaToRaw(file, 0x50EC690, out _) && !TryRvaToRaw(file, 0x51890D0 - 1, out _),
                "native PCL range length or PE bounds changed");
            Check(RvaToRaw(file, 0x50720) == 0x4FB20 && RvaToRaw(file, 0x572B0) == 0x566B0 &&
                RvaToRaw(file, 0xD4290) == 0xD3690 && RvaToRaw(file, 0x96CE) == 0x8ACE,
                "audited code RVA to FileOffset mapping changed");
            int copyCallRaw = RvaToRaw(file, 0x96CE);
            Check(file[copyCallRaw] == 0xE8 && copyCallRaw + 5 + BitConverter.ToInt32(file, copyCallRaw + 1) ==
                RvaToRaw(file, 0xD4290), "legacy player-state copy call target changed");
            byte[] versionGate = { 0x81, 0xFA, 0xD5, 0x00, 0x00, 0x00, 0x7D, 0x0B };
            Check(file.Skip(copyCallRaw - versionGate.Length).Take(versionGate.Length).SequenceEqual(versionGate) &&
                source.Contains("LegacyPlayerStateCopyVersionExclusive = 0xD5"),
                "legacy player-state copy map-version gate changed");

            string functions = File.ReadAllText(Path.Combine("..", "_inspect", "CrusaderDE-Native-Baseline", "sem", "FBCB9319", "exports", "semantic-functions.jsonl"));
            foreach (string rva in new[] { "0x50680", "0x50720", "0x572B0", "0x7EB00", "0xD4290", "0xC3FA0", "0xC43A0", "0xB8310", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xCC420", "0x414A0", "0x41230", "0x41380", "0x41280", "0x3B1D0", "0x50340", "0x504F0", "0xB8270", "0xC3BF0", "0xC8F50", "0xC90E0", "0x50D80", "0x50E00", "0x50F90", "0x51190", "0x51270", "0x51540", "0x575B0", "0x57B80", "0x58020", "0x58950", "0x6D580", "0xE2610" })
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
