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
                TestPreplacedIdentityAndCountProjection();
                TestPortalRoutes();
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

        private static void TestPortalRoutes()
        {
            var portals = new List<PortalConnection>
            {
                new PortalConnection(101, 10, 20, 0, 8, 40),
                new PortalConnection(102, 20, 30, 0, 8, 41),
                new PortalConnection(103, 10, 40, 0, 7, 42)
            };
            Check(PortalRouteModel.Evaluate(10, 10, portals, 8, owner => owner == 7).Kind == PortalRouteKind.Direct,
                "direct PCL route");
            PortalRouteResult own = PortalRouteModel.Evaluate(10, 30, portals, 8, owner => owner == 7);
            Check(own.Kind == PortalRouteKind.OwnPortal && own.UsedPortalIds.SequenceEqual(new[] { 101, 102 }),
                "multi-gate own route");
            Check(PortalRouteModel.Evaluate(10, 40, portals, 8, owner => false).Kind == PortalRouteKind.RequiresForeignPortal,
                "foreign-only portal route");
            Check(PortalRouteModel.Evaluate(10, 50, portals, 8, owner => owner == 7).Kind == PortalRouteKind.Unreachable,
                "sealed destination route");
            Check(PortalRouteModel.Evaluate(10, 20, Array.Empty<PortalConnection>(), 8, owner => owner == 7).Kind == PortalRouteKind.Unreachable,
                "missing gatehouse route");
            Check(PortalRouteModel.Evaluate(0, 30, portals, 8, owner => owner == 7).Kind == PortalRouteKind.Unreachable,
                "invalid start PCL route");
            Check(PortalRouteModel.Evaluate(10, 40, portals, 8, owner => owner == 7).Kind == PortalRouteKind.AlliedPortal,
                "allied portal route");
            Check(PortalRouteModel.Evaluate(30, 40, portals, 8, owner => owner == 7).Kind == PortalRouteKind.MixedFriendlyPortals,
                "mixed own/allied portal route");
            var fourOwnGates = new List<PortalConnection>
            {
                new PortalConnection(201, 1, 2, 0, 8, 51),
                new PortalConnection(202, 2, 3, 0, 8, 52),
                new PortalConnection(203, 3, 4, 0, 8, 53),
                new PortalConnection(204, 4, 5, 0, 8, 54)
            };
            Check(PortalRouteModel.Evaluate(1, 5, fourOwnGates, 8, owner => false).UsedPortalIds.Length == 4,
                "four own gatehouses were not traversed");
            fourOwnGates.RemoveAt(2);
            Check(PortalRouteModel.Evaluate(1, 5, fourOwnGates, 8, owner => false).Kind == PortalRouteKind.Unreachable,
                "destroyed gatehouse did not break the modeled portal chain");
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
            string helper = File.ReadAllText(Path.Combine("..", "Shared", "DebugLogHelper.cs"));
            Check(helper.Contains("FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"), "native hash contract missing");
            foreach (string rva in new[] { "0x50680", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xB8270", "0xC3BF0", "0xC8F50", "0xC90E0", "0x50D80", "0x50E00", "0x50F90", "0x51190", "0x51270", "0x51540", "0x575B0", "0x57B80", "0x58020", "0x58950", "0x6D580", "0xE2610", "0x60AD660" })
                Check(source.Contains(rva), "RVA missing: " + rva);
            foreach (string contract in new[] { "AivSpecStride = 0x6D98", "PlayerRuntimeStateStride = 0x583C", "PreparedLayoutFrameCount = 0x922", "PreparedEntrySize = 0x0C", "PauseTableEntryCount =", "pauseIndex < PauseTableEntryCount", "EconomyGridWidth = 160", "EconomyGridCellStride = 0x30", "EconomyGridBaseOffset = 0x5B830", "EconomyVisitGenerationOffset = 0x5B50C", "WoodSearchCooldownRelativeOffset = 0x167C", "FarmSearchCooldownRelativeOffset = 0x167E", "QuarrySearchCooldownRelativeOffset = 0x1680", "IronSearchCooldownRelativeOffset = 0x1682", "PitchSearchCooldownRelativeOffset = 0x1684", "ValidateSize(typeof(GameBuilding), 0x32C)", "ValidateSize(typeof(GameGatehouseEntry), 0x204)", "UnmanagedFunctionPointer(CallingConvention.Cdecl)" })
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
                "delegate int RegionPairReachabilityDelegate("
            })
                Check(source.Contains(nativeDelegate), "native delegate ABI missing: " + nativeDelegate);
            Check(source.Contains("ulong pathManager, int playerId, int targetPcl, int sourcePcl, int routeMode"),
                "Script Extender 2.3.0 E2610 parameter order is not preserved");
            Check(source.Contains("players.Clear()"), "map transition does not reset sessions");
            Check(source.Contains("activeEconomyContexts?.Clear()") && source.Contains("lastRoutingSnapshot = null"),
                "map transition retains economy or routing diagnostic state");
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
                source.Contains("rva575B0(sbyte+04<17)"),
                "frontier rejection diagnostics are incomplete");
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
            Check(definitions.Count >= 38, "not all native signatures were discovered by the static test");
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

            string functions = File.ReadAllText(Path.Combine("..", "_inspect", "CrusaderDE-Native-Baseline", "sem", "FBCB9319", "exports", "semantic-functions.jsonl"));
            foreach (string rva in new[] { "0x50680", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xCC420", "0x414A0", "0x41230", "0x41380", "0x41280", "0x3B1D0", "0x50340", "0x504F0", "0xB8270", "0xC3BF0", "0xC8F50", "0xC90E0", "0x50D80", "0x50E00", "0x50F90", "0x51190", "0x51270", "0x51540", "0x575B0", "0x57B80", "0x58020", "0x58950", "0x6D580", "0xE2610" })
                Check(functions.Contains("\"rva\":\"" + rva + "\""), "baseline function boundary missing: " + rva);
        }

        private static int RvaToRaw(byte[] file, int rva)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            int sectionTable = optional + optionalSize;
            for (int index = 0; index < sections; index++)
            {
                int header = sectionTable + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int virtualSize = BitConverter.ToInt32(file, header + 8);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                    return rawOffset + rva - virtualAddress;
            }
            throw new InvalidOperationException("RVA outside PE sections: 0x" + rva.ToString("X"));
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
