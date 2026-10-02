using System;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Hooks;

namespace EnemyGatePathfindingTest
{
    internal static class Program
    {
        private static int assertions;

        private static int Main()
        {
            try
            {
                assertions += GateRoutePolicyTests.Run();
                assertions += DrawbridgeClosureTests.Run();
                UncapturedEnemyPreservesVanillaExclusion();
                OwnAndAlliedOwnersRemainEligible();
                OwnAndAlliedCaptureRemainEligible();
                UnrelatedThirdPlayerCaptureIsExcluded();
                CaptureAndRecaptureApplyImmediately();
                InvalidStateFailsOpen();
                ImmutableGateSnapshotIsAllianceAwareAndFailOpen();
                SnapshotDecisionDiagnosticsCoverEveryAccessClass();
                SnapshotMetadataCountsTrackedPolicy();
                SnapshotGateIdentityRequiresBothIds();
                AccessPolicyEqualityIgnoresRawScanOnlyChanges();
                CaptureTransitionCoverageIsDeterministic();
                CapturerComparisonAndFlagRestorationAreExact();
                CapturerAdapterMachineTests.Run();
                NativeContractIncludesDrawbridgePclAndExactFilterSite();
                CapturerHooksCoverBothNativeSitesAtomically();
                SnapshotRefreshPathsAreSeparatedAndBounded();
                RoutePolicyFingerprintIgnoresDynamicTileState();
                DiagnosticLifecycleAndSamplesAreBounded();
                AcceptanceVerdictsAreMachineReadable();
                StableDiagnosticBaselinesSurviveFailOpenClears();
                CompactTopologyAndInvariantTimingAreEnforced();
                SamePclCandidatePolicyIsFailOpenAndAllianceAware();
                RectangleDistanceSupportsSpatialBridgeDiagnosis();
                NativeHookByteContractsRejectMutation();
                VanillaDirectionFilterContractsAreAtomic();
                AiTacticalTargetContractsAreAtomicAndExecutable();
                AttackOrderCorrelationIsLosslessAndObservational();
                GateDiagnosticRolesAndBuildingContextsAreLossless();
                CaptureRecoveryRequiresFreshExactPublication();
                DirectionAdapterTileRegistersMatchNativeDataFlow();
                CrashDumpRegisterRegressionsFailOpen();
                DirectionAdaptersActuallyAssembleAndDecode();
                BaselinePlayerScopesUseNativeArguments();
                BuildingSearchContextsSeparateVanillaAndMovement();
                DirectCursorCallsiteContractIsExact();
                NormalCursorPreviewContractIsExact();
                CursorPreviewDecisionIsCausalAndStable();
                CursorCacheIsExactAndBounded();
                NativeSnapshotPoolAcquisitionIsSynchronized();
                PassageAxisEvidenceIsDeterministic();
                TopologyRejectionClassificationIsDeterministic();
                FootprintAdjacencyIgnoresBrokenEditorBounds();
                SparseFootprintsFollowVanillaSlotSemantics();
                InvalidFootprintsRemainEntityLocal();
                TopologySignatureAndOrphanDiagnosticsAreNonThrowing();
                UniqueSpatialGateAssociationFailsOpenWhenAmbiguous();
                DirectionEdgesRequireBothNativeDirections();
                DirectionMaskBlocksOnlyTheGatePassage();
                GatehouseUsesBothOuterBoundaries();
                TileRouteNativeContractIsPinned();
                NativeRouteHotPathsRemainPrimitiveOnly();
                UnsafeGlobalMutationAndWholePclDetourAreAbsent();
                ScriptExtenderPathfindingGlobalsAreComparedReadOnly();
                ScriptExtender2120AndFixesContractsArePinned();
                SearchDiagnosticIdentitySurvivesPublication();
                GateStateDefinitionsReconstructEveryObservation();
                PartialPathfindingCoverageIsNotMutation();
                AssassinRouteDiagnosisIsLosslessAndReadOnly();
                Console.WriteLine("EnemyGatePathfindingPolicy: {0} assertions passed.", assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void SearchDiagnosticIdentitySurvivesPublication()
        {
            var aggregate = new AiGateDecisionAggregate();
            long countedAi = 0;
            for (int i = 0; i < 100; i++)
            {
                bool playerIsAi = (i % 2) == 0;
                var context = new SearchDiagnosticContext(playerIsAi ? QueryKind.AiBuilder : QueryKind.HumanBuilder);
                if (context.IsAiBuilder) countedAi++;
                playerIsAi = !playerIsAi; // simulated refreshed player-kind publication
                if (context.IsAiBuilder)
                    aggregate.Record(5, 0, "builder", "positive", 3, i + 1, 0, 0);
                Assert(context.IsAiBuilder != playerIsAi, "entry identity survives a changed player-kind publication");
            }
            var rows = aggregate.Drain(); long observed = 0;
            foreach (var row in rows) observed += row.Count;
            Assert(countedAi == 50 && observed == countedAi, "count and result aggregates use the same entry classification");
            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string correlation = File.ReadAllText(Path.Combine("src", "AttackOrderCorrelationDiagnostics.cs"));
            int begin = runtime.IndexOf("void IEnemyGatePathPolicy.ExitNativeSearch", StringComparison.Ordinal);
            int end = runtime.IndexOf("private void CountSharedSearch", begin, StringComparison.Ordinal);
            string exit = runtime.Substring(begin, end - begin);
            Assert(exit.Contains("query.Diagnostic.Kind") && exit.Contains("query.PlayerId, query.Diagnostic") &&
                !exit.Contains("IsAi("), "shared exit never reclassifies a query");
            Assert(runtime.Contains("Enter(player, diagnosticKind: kind)") &&
                runtime.Contains("ObserveBuilder(player, scope.Diagnostic"), "standalone builder carries entry identity too");
            begin = correlation.IndexOf("internal void ObserveBuilder", StringComparison.Ordinal);
            end = correlation.IndexOf("internal void BeginBuilder", begin, StringComparison.Ordinal);
            Assert(!correlation.Substring(begin, end - begin).Contains("IsAi(player)"),
                "builder result observer does not query a second live AI classification");
            foreach (string counter in new[] { "attackQueries", "buildingApproachQueries", "buildingConsumerQueries", "cursorCommandQueries", "aiQueries" })
                Assert(runtime.Substring(runtime.IndexOf("private void CountSharedSearch", StringComparison.Ordinal),
                    runtime.IndexOf("private QueryKind SharedKind", StringComparison.Ordinal) -
                    runtime.IndexOf("private void CountSharedSearch", StringComparison.Ordinal)).Contains("ref " + counter),
                    "shared per-kind count includes " + counter);
            Assert(runtime.Contains("CountSharedSearch(kind, localKind);") &&
                runtime.Contains("Enter(playerId, diagnosticKind: localKind)"), "shared counters and scope use one classification");
        }

        private static void GateStateDefinitionsReconstructEveryObservation()
        {
            var aggregate = new AiGateDecisionAggregate(); aggregate.Reset();
            var definitions = new System.Collections.Generic.Dictionary<string, AiGateDecisionAggregate.GateStateDefinition>();
            long decoded = 0; long outputCharacters = 0; long uncompressedCharacters = 0;
            for (int window = 0; window < 3; window++)
            {
                for (int state = 0; state < 100; state++)
                    for (int repeat = 0; repeat < 5; repeat++)
                    {
                        string value = "ownerRelation=own,captureRelation=" + (state % 2 == 0 ? "captured-by-other" : "captured-by-self") +
                            ",generation=" + state + ",owner=1,captured=" + (state % 8 + 1) + ",gateGlobal=" + (2000 + state);
                        string detail = "bridgeGlobal=" + (3000 + state) + "," + new string('x', 120);
                        aggregate.RecordGateState(1, 819, "gate-live-target-pre", value, 9, 100 + repeat,
                            state, repeat, detail);
                        uncompressedCharacters += ("result=" + value + ",first=100:0/0:" + detail + ",last=104:0/4:" + detail).Length / 5;
                    }
                var rows = aggregate.Drain(out var fresh);
                Assert(fresh.Length == (window == 0 ? 100 : 0), "every new full state is defined once across windows");
                foreach (var definition in fresh)
                { definitions.Add(definition.Reference, definition); outputCharacters += definition.ToString().Length; }
                Assert(rows.Length == 100, "more than 32 state combinations are retained");
                foreach (var row in rows)
                {
                    string reference = row.Result.Substring("gateState=".Length);
                    Assert(definitions.TryGetValue(reference, out var definition), "definition precedes every referencing row");
                    Assert(definition.Player == row.Player && definition.GateId == row.GateId &&
                        definition.State.Contains("generation=") && definition.Detail.Contains("bridgeGlobal="),
                        "full role, gate identity and bridge data can be reconstructed");
                    Assert(row.Count == 5 && row.First.StartsWith("100:") && row.Last.StartsWith("104:"),
                        "counts and concrete first/last tribe values survive compression");
                    decoded += row.Count; outputCharacters += row.ToString().Length;
                }
            }
            Assert(decoded == 1500 && aggregate.Observations == decoded, "compression loses no observations");
            Assert(outputCharacters < uncompressedCharacters, "state references reduce repeated log output");
            aggregate.Reset();
            aggregate.RecordGateState(1, 819, "gate-live-checkpoint", "unknown:identity-changed", 0, 0, 0, 0, "gateGlobal=9999");
            var nextRows = aggregate.Drain(out var nextDefinitions);
            Assert(nextDefinitions.Length == 1 && nextDefinitions[0].Epoch == 2 && nextDefinitions[0].Id == 1 &&
                !definitions.ContainsKey(nextDefinitions[0].Reference) && nextRows[0].Count == 1,
                "map reset cannot reuse an old epoch reference");
        }

        private static void AssassinRouteDiagnosisIsLosslessAndReadOnly()
        {
            var masks = new byte[9][]; masks[2] = new byte[400];
            for (int tile = 0; tile < 400; tile++) masks[2][tile] = 0xFF;
            var owners = new GateEdgeOwnership[9]; owners[2] = new GateEdgeOwnership();
            var totals = new AiGateDecisionAggregate();
            for (int gate = 1; gate <= 80; gate++)
            {
                masks[2][gate] = 0xFB;
                owners[2].Record(gate, 2, gate);
            }
            byte[] before = (byte[])masks[2].Clone();
            var snapshot = new RouteTilePolicySnapshot(masks, 123, edgeOwners: owners);
            var probe = new AssassinRouteProbe(snapshot);
            for (int round = 0; round < 5; round++)
            for (int gate = 1; gate <= 80; gate++)
            {
                bool blocked = probe.Observe(2, gate, 2, round % 2 == 1, out int actual);
                Assert(blocked && actual == gate, "exact mask-construction gate attribution");
                totals.Record(2, actual, "assassin-route-edge", round % 2 == 1 ? "climb" : "ground", 3, 4410, 402, 305, value: 1);
            }
            Assert(probe.Ground == 240 && probe.Climb == 160 &&
                probe.BlockedGround == 240 && probe.BlockedClimb == 160, "every ground and climb edge counted beyond 32");
            var rows = totals.Drain(); long count = 0, value = 0;
            foreach (var row in rows) { count += row.Count; value += row.Value; }
            Assert(rows.Length == 160 && count == 400 && value == 400, "all gate/type buckets and summed values survive aggregation");
            for (int tile = 0; tile < before.Length; tile++)
                Assert(before[tile] == masks[2][tile], "diagnosis cannot change a direction mask");
            Assert(!probe.Observe(1, 1, 2, false, out _), "own or otherwise unmasked player remains allowed");
            Assert(!probe.Observe(2, 0, 2, true, out _), "ordinary wall climbing remains outside gate diagnosis");
            owners[2].Record(1, 2, 200); owners[2].Record(1, 2, 1);
            Assert(probe.Observe(2, 1, 2, false, out int ambiguous) && ambiguous == -1,
                "overlap is ambiguous and cannot silently regain a unique identity");
            Assert(!probe.Observe(0, 1, 2, false, out _) && !probe.Observe(2, 400, 2, false, out _),
                "invalid context remains unclassified");
            Assert(probe.Unknown == 2, "unknown contexts are counted");
            Assert(ReferenceEquals(probe.Snapshot, snapshot), "probe keeps its entry snapshot across later publications");
        }

        private static void PartialPathfindingCoverageIsNotMutation()
        {
            var profiles = new int[89]; var permissions = new int[534];
            for (int i = 0; i < profiles.Length; i++) profiles[i] = PathfindingGlobalsBaseline.GetExpectedProfile(i);
            for (int i = 0; i < permissions.Length; i++) permissions[i] = PathfindingGlobalsBaseline.GetExpectedPermission(i / 90 + 1, i % 90);
            var comparison = PathfindingGlobalsBaseline.Compare(profiles, permissions);
            Assert(comparison.ComparedValuesMatch && !comparison.HasExpectedLengths && !comparison.MatchesCanonical,
                "matching prefix is not full native coverage or a mutation");
            Assert(comparison.ComparedProfiles == 89 && comparison.ComparedPermissions == 534 &&
                comparison.MissingCoverage == "profiles=89..89;permissions=534..539", "exact missing native indices remain explicit");
            permissions[90] ^= 1; comparison = PathfindingGlobalsBaseline.Compare(profiles, permissions);
            Assert(!comparison.ComparedValuesMatch && comparison.PermissionMismatches == 1 &&
                comparison.Samples[0].ConnectionClass == 2 && comparison.Samples[0].UnitType == 0,
                "partial API view retains native stride90 and detects a changed class2 value");
            string source = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(source.Contains("comparison.ComparedValuesMatch && !comparison.HasExpectedLengths") &&
                source.Contains("This is not evidence of table mutation"), "partial coverage is diagnosed separately from changed values");
        }

        private static void ScriptExtenderPathfindingGlobalsAreComparedReadOnly()
        {
            var profiles = new int[PathfindingGlobalsBaseline.UnitTypeCount];
            var permissions = new int[PathfindingGlobalsBaseline.PermissionCount];
            for (int unitType = 0; unitType < profiles.Length; unitType++)
            {
                profiles[unitType] = PathfindingGlobalsBaseline.GetExpectedProfile(unitType);
                for (int connectionClass = 1;
                     connectionClass <= PathfindingGlobalsBaseline.ConnectionClassCount;
                     connectionClass++)
                {
                    permissions[(connectionClass - 1) * profiles.Length + unitType] =
                        PathfindingGlobalsBaseline.GetExpectedPermission(
                            connectionClass, unitType);
                }
            }

            PathfindingGlobalsComparison canonical =
                PathfindingGlobalsBaseline.Compare(profiles, permissions);
            Assert(canonical.MatchesCanonical,
                "canonical pathfinding globals compare without mismatches");
            Assert(canonical.LargeProfiles == 13 && canonical.DefaultProfiles == 77,
                "canonical profile counts remain pinned to FBCB9319");
            Assert(canonical.AllowedByClass.Length == 6 &&
                canonical.AllowedByClass[0] == 4 && canonical.AllowedByClass[1] == 16 &&
                canonical.AllowedByClass[2] == 89 && canonical.AllowedByClass[3] == 89 &&
                canonical.AllowedByClass[4] == 89 && canonical.AllowedByClass[5] == 83,
                "canonical connection-class permission counts remain pinned to FBCB9319");
            Assert(canonical.ActualFingerprint ==
                    PathfindingGlobalsBaseline.CanonicalFingerprint &&
                canonical.ExpectedFingerprint ==
                    PathfindingGlobalsBaseline.CanonicalFingerprint,
                "canonical aggregate fingerprint is independent of reconstructed counts");

            Assert(PathfindingGlobalsBaseline.FirstPublicClassTableRva ==
                    PathfindingGlobalsBaseline.UnknownClassTableRva +
                    PathfindingGlobalsBaseline.ConnectionClassRowByteLength &&
                PathfindingGlobalsBaseline.LastPublicClassTableRva ==
                    PathfindingGlobalsBaseline.FirstPublicClassTableRva +
                    5 * PathfindingGlobalsBaseline.ConnectionClassRowByteLength,
                "public classes 1 through 6 exclude native class zero and end at class six");
            Assert(PathfindingGlobalsBaseline.UnknownClassTableRva == 0x32BC48 &&
                PathfindingGlobalsBaseline.FirstPublicClassTableRva == 0x32BDB0 &&
                PathfindingGlobalsBaseline.LastPublicClassTableRva == 0x32C4B8,
                "canonical class-zero, class-one and class-six RVAs are exact");

            ulong classSixLow = 0;
            ulong classSixHigh = 0;
            for (int unitType = 0;
                 unitType < PathfindingGlobalsBaseline.UnitTypeCount;
                 unitType++)
            {
                if (PathfindingGlobalsBaseline.GetExpectedPermission(6, unitType) == 0)
                    continue;
                if (unitType < 64)
                    classSixLow |= 1UL << unitType;
                else
                    classSixHigh |= 1UL << (unitType - 64);
            }
            Assert(classSixLow == 0xFFFC0FFFFFFFFFFEUL &&
                classSixHigh == 0x0000000003FFFFFFUL,
                "canonical class-six permission mask is complete");

            for (int connectionClass = 1;
                 connectionClass <= PathfindingGlobalsBaseline.ConnectionClassCount;
                 connectionClass++)
            {
                int index = (connectionClass - 1) *
                    PathfindingGlobalsBaseline.UnitTypeCount;
                permissions[index] ^= 1;
                PathfindingGlobalsComparison perClassChange =
                    PathfindingGlobalsBaseline.Compare(profiles, permissions);
                Assert(perClassChange.PermissionMismatches == 1 &&
                    perClassChange.Samples.Length == 1 &&
                    perClassChange.Samples[0].ConnectionClass == connectionClass,
                    $"connection class {connectionClass} mutation retains its public class id");
                permissions[index] ^= 1;
            }

            profiles[7] ^= 1;
            permissions[2 * PathfindingGlobalsBaseline.UnitTypeCount + 22] ^= 1;
            PathfindingGlobalsComparison changed =
                PathfindingGlobalsBaseline.Compare(profiles, permissions);
            Assert(!changed.MatchesCanonical && changed.ProfileMismatches == 1 &&
                changed.PermissionMismatches == 1 && changed.Samples.Length == 2,
                "one-time comparison identifies profile and permission changes");
            Assert(changed.Samples[0].Kind == PathfindingGlobalTableKind.Profile &&
                changed.Samples[0].UnitType == 7 &&
                changed.Samples[1].Kind == PathfindingGlobalTableKind.ConnectionPermission &&
                changed.Samples[1].ConnectionClass == 3 && changed.Samples[1].UnitType == 22,
                "comparison samples retain exact table coordinates");

            PathfindingGlobalsComparison truncated = PathfindingGlobalsBaseline.Compare(
                new int[PathfindingGlobalsBaseline.UnitTypeCount - 1],
                new int[PathfindingGlobalsBaseline.PermissionCount],
                maximumSamples: 1);
            Assert(!truncated.HasExpectedLengths && !truncated.MatchesCanonical,
                "unexpected SE table lengths cannot pass the canonical comparison");

            string runtimeSource = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(runtimeSource.IndexOf(
                    "GetUnitTypePathfindingConnectionClasses()", StringComparison.Ordinal) >= 0,
                "startup comparison consumes the public Script Extender connection-class span");
            Assert(runtimeSource.IndexOf(
                    "PathfindingConnectionPermissionTableRva", StringComparison.Ordinal) < 0 &&
                runtimeSource.IndexOf("new ReadOnlySpan<int>(", StringComparison.Ordinal) < 0,
                "startup comparison contains no direct native connection-table view");
        }

        private static void ScriptExtender2120AndFixesContractsArePinned()
        {
            Assert(EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderVersion == "2.12.0" &&
                EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderTag == "v2.12.0" &&
                EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit ==
                    "f8d51730fcb54b25af43d3c9348d57db058e077f",
                "Script Extender 2.12.0 provenance is pinned to the audited commit");
            Assert(EnemyGatePathfindingNativeDefinition.AuditedRedBirdVersion ==
                typeof(X64InlineHook).Assembly.GetName().Version.ToString(),
                "installed RedBird audit version is documented without replacing byte contracts");

            string pluginSource = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingTestPlugin.cs"));
            string runtimeSource = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(pluginSource.IndexOf("new Version(2, 12, 0, 0)",
                    StringComparison.Ordinal) >= 0 &&
                pluginSource.IndexOf("audited version 2.7.1",
                    StringComparison.Ordinal) < 0 &&
                runtimeSource.IndexOf("Script Extender 2.6 pathfinding",
                    StringComparison.Ordinal) < 0,
                "runtime diagnostics contain no stale Script Extender audit identity");

            string buildDriver = File.ReadAllText("build.bat");
            int dependencyCheck = buildDriver.IndexOf("API_SHARED_DIR%\\APIShared.dll",
                StringComparison.Ordinal);
            int packageReplacement = buildDriver.IndexOf(
                "if exist \"%LOCAL_PLUGIN_DIR%\\\" rmdir", StringComparison.Ordinal);
            Assert(dependencyCheck >= 0 &&
                buildDriver.IndexOf("API_SHARED_DIR%\\info.json", StringComparison.Ordinal) >= 0 &&
                buildDriver.IndexOf("[Version]'0.3.6'", StringComparison.Ordinal) >= 0 &&
                packageReplacement > dependencyCheck,
                "APIShared DLL and manifest version are checked before package replacement");

            // Fixes 1.17.1 extends PCL rebuild storage at these FBCB9319 spans.
            // They are deliberately separate from this mod's search-time filters.
            var fixesSpans = new[]
            {
                Tuple.Create(0xE4AA3, 0xE4ABB),
                Tuple.Create(0xE4B61, 0xE4B70),
                Tuple.Create(0xE4DF4, 0xE4E00),
                Tuple.Create(0xE7CE6, 0xE7CF9),
                Tuple.Create(0xE7E92, 0xE7EB4)
            };
            foreach (Tuple<int, int> fixes in fixesSpans)
            {
                Assert(!Overlaps(fixes.Item1, fixes.Item2,
                        EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva,
                        EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterEndRva) &&
                    !Overlaps(fixes.Item1, fixes.Item2,
                        EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva,
                        EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterEndRva),
                    "Fixes PCL-rebuild spans do not overlap capturer filters");
                Assert(!Overlaps(fixes.Item1, fixes.Item2,
                        EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva,
                        EnemyGatePathfindingNativeDefinition.DirectCursorSearchReturnRva) &&
                    !Overlaps(fixes.Item1, fixes.Item2,
                        EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                        EnemyGatePathfindingNativeDefinition.CursorPclDecisionReturnRva),
                    "Fixes PCL-rebuild spans do not overlap cursor adapters");
                for (int index = 0;
                     index < EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length;
                     index++)
                {
                    int start = EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[index];
                    int end = start +
                        EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[index];
                    Assert(!Overlaps(fixes.Item1, fixes.Item2, start, end),
                        "Fixes PCL-rebuild spans do not overlap direction adapter " + index);
                }
                for (int index = 0;
                     index < EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas.Length;
                     index++)
                {
                    int start = EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas[index];
                    int end = start +
                        EnemyGatePathfindingNativeDefinition.AiTacticalFilterLengths[index];
                    Assert(!Overlaps(fixes.Item1, fixes.Item2, start, end),
                        "Fixes PCL-rebuild spans do not overlap AI tactical adapter " + index);
                }
                int[] scopeRvas =
                {
                    EnemyGatePathfindingNativeDefinition.PathBuilderRva,
                    EnemyGatePathfindingNativeDefinition.AttackApproachRva,
                    EnemyGatePathfindingNativeDefinition.BuildingApproachRva,
                    EnemyGatePathfindingNativeDefinition.BuildingConsumerRva,
                    EnemyGatePathfindingNativeDefinition.AlternateBuildingConsumerRva,
                    EnemyGatePathfindingNativeDefinition.CursorMoveStagerRva,
                    EnemyGatePathfindingNativeDefinition.PlayerAwareCandidateSearchRva,
                    EnemyGatePathfindingNativeDefinition.AiTacticalTargetSelectionRva
                };
                for (int index = 0; index < scopeRvas.Length; index++)
                    Assert(!Overlaps(fixes.Item1, fixes.Item2,
                            scopeRvas[index], scopeRvas[index] + 32),
                        "Fixes PCL-rebuild spans do not overlap search scope " + index);
            }
            Assert(!Overlaps(0x10ECC3, 0x10ECC8,
                    EnemyGatePathfindingNativeDefinition.AiTacticalTargetSelectionRva,
                    EnemyGatePathfindingNativeDefinition.AiTacticalTargetSelectionRva + 32),
                "BugfixesAndQoL AiWallTargetingFix does not overlap the tactical target scope");
            for (int index = 0;
                 index < EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas.Length;
                 index++)
                Assert(!Overlaps(0x10ECC3, 0x10ECC8,
                        EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas[index],
                        EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas[index] +
                            EnemyGatePathfindingNativeDefinition.AiTacticalFilterLengths[index]),
                    "BugfixesAndQoL AiWallTargetingFix does not overlap AI tactical adapter " + index);
        }

        private static bool Overlaps(int leftStart, int leftEnd, int rightStart, int rightEnd) =>
            leftStart < rightEnd && rightStart < leftEnd;

        private static void UncapturedEnemyPreservesVanillaExclusion()
        {
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 0);
        }

        private static void OwnAndAlliedOwnersRemainEligible()
        {
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 0, ownerPlayer: 1);
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 0, ownerPlayer: 2);
        }

        private static void OwnAndAlliedCaptureRemainEligible()
        {
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 1);
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 2);
        }

        private static void UnrelatedThirdPlayerCaptureIsExcluded()
        {
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 3);
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 4);
        }

        private static void CaptureAndRecaptureApplyImmediately()
        {
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 4);
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 2);
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 4);
        }

        private static void InvalidStateFailsOpen()
        {
            AssertDecision(CapturedGateFilterDecision.FailOpen, 0, 3);
            AssertDecision(CapturedGateFilterDecision.FailOpen, 1, 99);
            AssertDecision(CapturedGateFilterDecision.FailOpen, 1, 0, ownerPlayer: 99);
            Assert(
                EnemyGatePathfindingPolicy.EvaluateGateAccess(1, 3, 3, null, Allied) ==
                    CapturedGateFilterDecision.FailOpen,
                "missing validity callback fails open");
            Assert(
                EnemyGatePathfindingPolicy.EvaluateGateAccess(1, 3, 3, ValidPlayer, null) ==
                    CapturedGateFilterDecision.FailOpen,
                "missing alliance callback fails open");
        }

        private static void ImmutableGateSnapshotIsAllianceAwareAndFailOpen()
        {
            ushort ownerRelated = unchecked((ushort)(1 << 7));
            ushort capturerRelated = unchecked((ushort)((1 << 1) | (1 << 2)));
            ushort unrelated = unchecked((ushort)((1 << 3) | (1 << 4)));
            var records = new NativeGateAccessRecord[8];
            records[5] = new NativeGateAccessRecord(
                true, 7, 2, ownerRelated, capturerRelated, unrelated);
            var snapshot = new NativeGateAccessSnapshot(records, 0x1234);
            Assert(snapshot.Evaluate(3, 5, 7, 2) ==
                    NativeGateSnapshotDecision.ExcludeForeignCapture,
                "snapshot excludes an unrelated foreign capturer");
            Assert(snapshot.Evaluate(1, 5, 7, 2) ==
                    NativeGateSnapshotDecision.PreserveCapturerAlly,
                "snapshot allows a player allied to the capturer");
            Assert(snapshot.Evaluate(3, 5, 7, 0) ==
                    NativeGateSnapshotDecision.CaptureMismatch,
                "capture-state mismatch invalidates a stale snapshot");
            Assert(snapshot.Evaluate(3, 5, 6, 2) ==
                    NativeGateSnapshotDecision.OwnerMismatch,
                "owner mismatch fails open");
            Assert(snapshot.Evaluate(0, 5, 7, 2) ==
                    NativeGateSnapshotDecision.InvalidQueryPlayer,
                "invalid query player is classified separately");
            Assert(NativeGateAccessSnapshot.Empty.Evaluate(3, 5, 7, 2) ==
                    NativeGateSnapshotDecision.UntrackedConnection,
                "empty snapshot fails open");

            records[5] = new NativeGateAccessRecord(
                true, 7, 0, ownerRelated, 0, unrelated);
            var recaptured = new NativeGateAccessSnapshot(records, 0x1235);
            Assert(recaptured.Evaluate(3, 5, 7, 0) ==
                    NativeGateSnapshotDecision.PreserveUncaptured,
                "next snapshot preserves Vanilla for an uncaptured enemy gate");
        }

        private static void SnapshotDecisionDiagnosticsCoverEveryAccessClass()
        {
            ushort ownerRelated = unchecked((ushort)((1 << 1) | (1 << 2)));
            ushort capturerRelated = unchecked((ushort)((1 << 3) | (1 << 4)));
            ushort unrelated = unchecked((ushort)((1 << 5) | (1 << 6)));
            var records = new NativeGateAccessRecord[3];
            records[2] = new NativeGateAccessRecord(
                true, 1, 3, ownerRelated, capturerRelated, unrelated);
            var snapshot = new NativeGateAccessSnapshot(records, 7);
            Assert(snapshot.Evaluate(1, 2, 1, 3) == NativeGateSnapshotDecision.PreserveOwner,
                "owner is classified separately");
            Assert(snapshot.Evaluate(2, 2, 1, 3) == NativeGateSnapshotDecision.PreserveOwnerAlly,
                "owner ally is classified separately");
            Assert(snapshot.Evaluate(3, 2, 1, 3) == NativeGateSnapshotDecision.PreserveCapturer,
                "capturer is classified separately");
            Assert(snapshot.Evaluate(4, 2, 1, 3) == NativeGateSnapshotDecision.PreserveCapturerAlly,
                "capturer ally is classified separately");
            Assert(snapshot.Evaluate(5, 2, 1, 3) == NativeGateSnapshotDecision.ExcludeForeignCapture,
                "foreign capturer is classified separately");
        }

        private static void SnapshotMetadataCountsTrackedPolicy()
        {
            var records = new NativeGateAccessRecord[4];
            records[1] = new NativeGateAccessRecord(true, 1, 0, 0x0006, 0, 0x0018);
            records[3] = new NativeGateAccessRecord(true, 3, 4, 0x0008, 0x0010, 0x0060);
            var snapshot = new NativeGateAccessSnapshot(records, 9);
            Assert(snapshot.TrackedRecords == 2, "snapshot counts tracked records");
            Assert(snapshot.CapturedRecords == 1, "snapshot counts captured records");
            Assert(snapshot.UncapturedRecords == 1, "snapshot counts uncaptured records");
            Assert(snapshot.BlockedPlayerGatePairs == 4,
                "snapshot counts blocked player/gate pairs");
        }

        private static void AccessPolicyEqualityIgnoresRawScanOnlyChanges()
        {
            var firstRecords = new NativeGateAccessRecord[4];
            firstRecords[2] = new NativeGateAccessRecord(true, 3, 0, 0x0008, 0, 0x0006);
            var sameRecordsWithDifferentCapacity = new NativeGateAccessRecord[8];
            sameRecordsWithDifferentCapacity[2] = firstRecords[2];
            var first = new NativeGateAccessSnapshot(firstRecords, 0x1000);
            var rawOnlyChange = new NativeGateAccessSnapshot(
                sameRecordsWithDifferentCapacity, 0x2000);
            Assert(first.RawFingerprint != rawOnlyChange.RawFingerprint,
                "raw scan fingerprints retain diagnostic changes");
            Assert(first.PolicyEquals(rawOnlyChange),
                "raw fingerprint and trailing capacity do not change access policy");
            Assert(first.TopologyFingerprint == rawOnlyChange.TopologyFingerprint,
                "semantic access fingerprint remains stable across raw-only changes");

            sameRecordsWithDifferentCapacity[2] = new NativeGateAccessRecord(
                true, 3, 4, 0x0008, 0x0010, 0x0006);
            var captured = new NativeGateAccessSnapshot(sameRecordsWithDifferentCapacity, 0x3000);
            Assert(!first.PolicyEquals(captured), "capture changes access policy");
            Assert(first.TopologyFingerprint != captured.TopologyFingerprint,
                "capture changes semantic access fingerprint");

            var ownerChangedRecords = (NativeGateAccessRecord[])firstRecords.Clone();
            ownerChangedRecords[2] = new NativeGateAccessRecord(true, 4, 0, 0x0010, 0, 0x0006);
            Assert(!first.PolicyEquals(new NativeGateAccessSnapshot(ownerChangedRecords, 0x4000)),
                "owner and owner-alliance mask changes access policy");
            var maskChangedRecords = (NativeGateAccessRecord[])firstRecords.Clone();
            maskChangedRecords[2] = new NativeGateAccessRecord(true, 3, 0, 0x0008, 0, 0x0020);
            Assert(!first.PolicyEquals(new NativeGateAccessSnapshot(maskChangedRecords, 0x5000)),
                "blocking-mask changes access policy");
            var removedRecords = (NativeGateAccessRecord[])firstRecords.Clone();
            removedRecords[2] = default;
            Assert(!first.PolicyEquals(new NativeGateAccessSnapshot(removedRecords, 0x6000)),
                "record validity changes access policy");
        }

        private static void CaptureTransitionCoverageIsDeterministic()
        {
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 0, true, 3) ==
                    CaptureTransitionKind.Captured,
                "uncaptured to captured is a capture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 3, true, 4) ==
                    CaptureTransitionKind.Recaptured,
                "capturer replacement is a recapture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 3, true, 0) ==
                    CaptureTransitionKind.Recaptured,
                "capture removal is retained as a recapture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(false, 0, true, 3) ==
                    CaptureTransitionKind.None,
                "initial snapshot population is not a runtime capture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 3, false, 0) ==
                    CaptureTransitionKind.None,
                "record removal is not misclassified as recapture");
        }

        private static void CapturerComparisonAndFlagRestorationAreExact()
        {
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCaptureCompareIsEqual(0),
                "PCL-graph CMP is equal only for an uncaptured record");
            Assert(!EnemyGatePathfindingNativeDefinition.PclGraphCaptureCompareIsEqual(3),
                "PCL-graph CMP is unequal for a captured record");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCaptureCompareIsEqual(3, 3),
                "builder CMP uses the actual AX operand");
            Assert(!EnemyGatePathfindingNativeDefinition.BuilderPrecheckCaptureCompareIsEqual(3, 0),
                "builder CMP detects unequal capture and AX values");

            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string body = ExtractMethodBody(runtimeSource, "FilterUnrelatedCapturedEnemyGate");
            Assert(body.IndexOf("registers->R11 & 0xFFUL", StringComparison.Ordinal) >= 0,
                "callback uses the native comparison Boolean secured before cleanup");
            Assert(body.IndexOf("(registers->Rflags &", StringComparison.Ordinal) < 0,
                "callback never treats RedBird's saved flags as the displaced CMP result");
        }

        private static void NativeContractIncludesDrawbridgePclAndExactFilterSite()
        {
            Assert(EnemyGatePathfindingNativeDefinition.NativeRecordStride == 0x204, "record stride");
            Assert(EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset == 0x1C, "first PCL");
            Assert(EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset == 0x20, "second PCL");
            Assert(EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset == 0x1D0, "drawbridge PCL");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva == 0xE2705,
                "PCL-graph filter RVA");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterHookLength == 20,
                "PCL-graph filter span");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterEndRva == 0xE2719,
                "PCL-graph exclusive end");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphAllowedRecordTargetRva == 0xE271B,
                "PCL-graph branch target is outside the span");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva == 0xE3024,
                "builder-precheck filter RVA");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterHookLength == 20,
                "builder-precheck filter span");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterEndRva == 0xE3038,
                "builder-precheck exclusive end");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckAllowedRecordTargetRva == 0xE303A,
                "builder-precheck branch target is outside the span");
        }

        private static void NativeHookByteContractsRejectMutation()
        {
            var memory = new byte[EnemyGatePathfindingNativeDefinition.PathBuilderRva + 64];
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.PclGraphPredecessorJumpRva,
                "74 16 49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11 FF C3");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.BuilderPrecheckPredecessorJumpRva,
                "74 16 49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06 74 0D FF C3");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.PathBuilderRva,
                "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 48 63 41 0C 48 8B D9 41 8B F0 44 8B D2");

            EnemyGatePathfindingNativeDefinition.ValidateNativeHookContracts(memory);
            EnemyGatePathfindingNativeDefinition.ValidateSamePclBuilderContract(memory);
            int pclMutation = EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva + 3;
            memory[pclMutation] ^= 1;
            AssertNativeContractRejected(memory, "mutated PCL-graph block fails closed");
            memory[pclMutation] ^= 1;

            int builderMutation = EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva + 15;
            memory[builderMutation] ^= 1;
            AssertNativeContractRejected(memory, "mutated builder-precheck block fails closed");
            memory[builderMutation] ^= 1;
            int pathBuilderMutation = EnemyGatePathfindingNativeDefinition.PathBuilderRva + 24;
            memory[pathBuilderMutation] ^= 1;
            bool builderRejected = false;
            try
            {
                EnemyGatePathfindingNativeDefinition.ValidateSamePclBuilderContract(memory);
            }
            catch (InvalidOperationException)
            {
                builderRejected = true;
            }
            Assert(builderRejected, "mutated F4930 function entry fails closed");
        }

        private static void AssertNativeContractRejected(byte[] memory, string message)
        {
            bool rejected = false;
            try
            {
                EnemyGatePathfindingNativeDefinition.ValidateNativeHookContracts(memory);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Assert(rejected, message);
        }

        private static void CapturerHooksCoverBothNativeSitesAtomically()
        {
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string pclGraphBody = ExtractMethodBody(
                runtimeSource, "FilterUnrelatedCapturedEnemyGatePclGraph");
            string builderBody = ExtractMethodBody(
                runtimeSource, "FilterUnrelatedCapturedEnemyGateBuilderPrecheck");
            string sharedBody = ExtractMethodBody(runtimeSource, "FilterUnrelatedCapturedEnemyGate");

            Assert(pclGraphBody.IndexOf("context, false", StringComparison.Ordinal) >= 0,
                "PCL-graph hook selects the R14 query context");
            Assert(builderBody.IndexOf("context, true", StringComparison.Ordinal) >= 0,
                "builder-precheck hook selects the RBP query context");
            Assert(sharedBody.IndexOf("registers->R14", StringComparison.Ordinal) >= 0,
                "PCL-graph query player is read from R14");
            Assert(sharedBody.IndexOf("registers->RBP", StringComparison.Ordinal) >= 0,
                "builder-precheck query player is read from RBP");
            Assert(sharedBody.IndexOf("registers->RCX", StringComparison.Ordinal) >= 0,
                "both filters read the building id from RCX");
            Assert(sharedBody.IndexOf("registers->R9", StringComparison.Ordinal) >= 0,
                "both filters read the gate record from R9");
            Assert(runtimeSource.IndexOf("RollbackAndThrow", StringComparison.Ordinal) >= 0,
                "both filters use the atomic rollback transaction");
            Assert(runtimeSource.IndexOf("pclGraphCapturedByFilterHook,", StringComparison.Ordinal) >= 0,
                "PCL-graph filter is registered");
            Assert(runtimeSource.IndexOf("builderPrecheckCapturedByFilterHook,", StringComparison.Ordinal) >= 0,
                "builder-precheck filter is registered");
            Assert(runtimeSource.IndexOf("OwnsHooks = false", StringComparison.Ordinal) >= 0,
                "process-lifetime hook ownership is explicit");
            Assert(runtimeSource.IndexOf("commitResult.IsCompleteSuccess", StringComparison.Ordinal) >= 0,
                "transaction commit result is checked");
            Assert(runtimeSource.IndexOf("transaction.AddContextHook", StringComparison.Ordinal) < 0 &&
                    runtimeSource.IndexOf("CapturerComparisonAdapterEmitter.Emit", StringComparison.Ordinal) >= 0,
                "both capturer comparisons use the explicit post-cleanup TEST emitter");
            Assert(runtimeSource.IndexOf("ProbeExactHookLength", StringComparison.Ordinal) >= 0,
                "RedBird spans are probed before publication");
            Assert(runtimeSource.IndexOf("DisplacedByteCount", StringComparison.Ordinal) >= 0,
                "committed RedBird spans are checked");
            Assert(runtimeSource.IndexOf("transaction.DisableAll()", StringComparison.Ordinal) >= 0,
                "unexpected committed spans roll back before publication");
            Assert(runtimeSource.IndexOf("typeof(X64InlineHook).Assembly.GetName().Version",
                    StringComparison.Ordinal) >= 0 &&
                    runtimeSource.IndexOf("new Version(1, 1, 0, 0)",
                        StringComparison.Ordinal) < 0 &&
                    runtimeSource.IndexOf("is not the audited 1.1.0 implementation",
                        StringComparison.Ordinal) < 0,
                "RedBird version is diagnostic while concrete hook contracts remain authoritative");
            Assert(sharedBody.IndexOf("originalZeroKnown", StringComparison.Ordinal) >= 0 &&
                    sharedBody.IndexOf("registers->Rflags", StringComparison.Ordinal) < 0 &&
                    sharedBody.IndexOf("originalZero ? 0UL : 1UL", StringComparison.Ordinal) >= 0,
                "callback restores the original Boolean on policy failures");
            Assert(runtimeSource.IndexOf("NativeGateSnapshotDecision.RecordIdMismatch",
                    StringComparison.Ordinal) >= 0,
                "record-ID mismatch has a dedicated diagnostic outcome");
            Assert(runtimeSource.IndexOf("CapturerSample[]", StringComparison.Ordinal) >= 0 &&
                    runtimeSource.IndexOf("CompareExchange(ref sample.State", StringComparison.Ordinal) >= 0,
                "capturer samples are preallocated and atomically published");
            Assert(sharedBody.IndexOf("RecordFirstPclOffset", StringComparison.Ordinal) >= 0 &&
                    sharedBody.IndexOf("RecordSecondPclOffset", StringComparison.Ordinal) >= 0 &&
                    sharedBody.IndexOf("RecordThirdPclOffset", StringComparison.Ordinal) >= 0 &&
                    runtimeSource.IndexOf("portalPcls=", StringComparison.Ordinal) >= 0,
                "first samples identify all native portal components");
        }

        private static void RoutePolicyFingerprintIgnoresDynamicTileState()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string body = ExtractMethodBody(source, "MixRoutePolicy");
            foreach (string dynamicField in new[]
            {
                "IsOpen", "EntryPcl", "ExitPcl", "GatePath", "Flags", "Walkable"
            })
                Assert(body.IndexOf(dynamicField, StringComparison.Ordinal) < 0,
                    "route policy fingerprint ignores dynamic field " + dynamicField);
            foreach (string policyField in new[]
            {
                "GateId", "GateGlobal", "BridgeId", "BridgeGlobal",
                "UnrelatedByPlayer", "tile.TileId", "tile.Footprint"
            })
                Assert(body.IndexOf(policyField, StringComparison.Ordinal) >= 0,
                    "route policy fingerprint includes " + policyField);
        }

        private static void DiagnosticLifecycleAndSamplesAreBounded()
        {
            string plugin = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingTestPlugin.cs"));
            string runtime = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(plugin.IndexOf("Shared.MissionEvents.Ended", StringComparison.Ordinal) >= 0,
                "map summary uses the common mission end");
            Assert(runtime.IndexOf("replacement before MissionStart",
                    StringComparison.Ordinal) >= 0,
                "new map defensively finalizes a missed unload");
            Assert(runtime.IndexOf("DiagnosticInterval = Stopwatch.Frequency * 10L",
                    StringComparison.Ordinal) >= 0,
                "one central ten-second diagnostic cadence is used");
            Assert(runtime.IndexOf(":NOT_OBSERVED", StringComparison.Ordinal) >= 0,
                "uncovered capturer cases are explicit");
            Assert(runtime.IndexOf("implicit editor map-size probe", StringComparison.Ordinal) < 0 &&
                plugin.Contains("Shared.GameplaySessionLifecycle.SubscribeStarted"),
                "editor maps must start exclusively through the common lifecycle");
        }

        private static void AcceptanceVerdictsAreMachineReadable()
        {
            Assert(EnemyGatePathfindingPolicy.ObservationVerdict(1) == DiagnosticVerdict.PASS,
                "observed acceptance case passes");
            Assert(EnemyGatePathfindingPolicy.ObservationVerdict(0) ==
                    DiagnosticVerdict.NOT_OBSERVED,
                "missing acceptance case is explicit");
            Assert(EnemyGatePathfindingPolicy.IntegrityVerdict(true, false) ==
                    DiagnosticVerdict.PASS,
                "observed error-free runtime passes");
            Assert(EnemyGatePathfindingPolicy.IntegrityVerdict(true, true) ==
                    DiagnosticVerdict.FAIL,
                "runtime error dominates observed activity");
            Assert(EnemyGatePathfindingPolicy.IntegrityVerdict(false, false) ==
                    DiagnosticVerdict.NOT_OBSERVED,
                "unexercised integrity remains unobserved");

            string runtime = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(runtime.IndexOf("Enemy-gate acceptance verdict:", StringComparison.Ordinal) >= 0,
                "final machine-readable verdict is logged");
            Assert(runtime.IndexOf("ownerAtHook={DiagnosticVerdict.NOT_APPLICABLE}",
                    StringComparison.Ordinal) >= 0,
                "upstream owner short-circuit is not reported as missing coverage");
            Assert(runtime.IndexOf("untrackedTransitionCalls=", StringComparison.Ordinal) >= 0,
                "transient untracked calls remain separately visible");
            Assert(runtime.IndexOf("nativeCursorScope=", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("nativeCursorEdgesFiltered=", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("samePclHookExecution=", StringComparison.Ordinal) >= 0,
                "native cursor scope and active Same-PCL verdicts are explicit");
        }

        private static void StableDiagnosticBaselinesSurviveFailOpenClears()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string tick = ExtractMethodBody(source, "OnGameTick");
            Assert(source.IndexOf("lastStableAccessSnapshot", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("lastStableTopologySnapshot", StringComparison.Ordinal) >= 0,
                "diagnostic baselines are separate from live fail-open snapshots");
            Assert(tick.IndexOf("lastStableAccessSnapshot = NativeGateAccessSnapshot.Empty",
                    StringComparison.Ordinal) < 0 &&
                    tick.IndexOf("lastStableTopologySnapshot = TopologySnapshot.Empty",
                    StringComparison.Ordinal) < 0,
                "tick-side fail-open clear preserves stable diagnostic baselines");
            string access = ExtractMethodBody(source, "RefreshGateAccess");
            Assert(access.IndexOf("previous.PolicyEquals(rebuilt)", StringComparison.Ordinal) >= 0,
                "access publication uses semantic snapshot equality");
            Assert(access.IndexOf("accessRepublishes", StringComparison.Ordinal) >= 0,
                "equivalent policy is republished after a fail-open window");
            Assert(access.IndexOf("suppressedRawAccessChanges", StringComparison.Ordinal) >= 0,
                "raw-only changes are counted without policy churn");
        }

        private static void CompactTopologyAndInvariantTimingAreEnforced()
        {
            string topology = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            Assert(topology.IndexOf("footprintTiles=[", StringComparison.Ordinal) < 0,
                "topology logs no longer dump every footprint tile");
            Assert(topology.IndexOf("/tileRange=", StringComparison.Ordinal) >= 0 &&
                    topology.IndexOf("/hash=0x", StringComparison.Ordinal) >= 0,
                "compact footprint range and hash are logged");
            Assert(topology.IndexOf("AppendTopologyDetail(detail, gateInfo.Format())",
                    StringComparison.Ordinal) < 0,
                "initial accepted gate details are not duplicated");
            Assert(!File.Exists(Path.Combine("src", "CursorGateRouteFilter.cs")),
                "the managed cursor BFS implementation is removed");
        }

        private static void SnapshotRefreshPathsAreSeparatedAndBounded()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            int accessCall = source.IndexOf("RefreshGateAccessIfDue(now);", StringComparison.Ordinal);
            int topologyCall = source.IndexOf("RefreshTopologyIfDue(now);", StringComparison.Ordinal);
            Assert(accessCall >= 0,
                "deferred work uses the bounded access refresh gate");
            Assert(topologyCall > accessCall,
                "deferred work retains separately throttled topology rebuilding");
            Assert(source.IndexOf("TopologySafetyInterval = Math.Max(1, Stopwatch.Frequency)",
                    StringComparison.Ordinal) >= 0,
                "expensive topology safety rebuild is capped at one per second");
            Assert(source.IndexOf("AccessSafetyInterval = Math.Max(1, Stopwatch.Frequency)",
                    StringComparison.Ordinal) >= 0,
                "access safety scans are capped at one per second");
            string access = ExtractMethodBody(source, "RefreshGateAccess");
            Assert(access.IndexOf("ComputeGateAccessFingerprint", StringComparison.Ordinal) >= 0,
                "access refresh probes a fingerprint before publishing");
            Assert(access.IndexOf("fingerprint == lastAccessFingerprint", StringComparison.Ordinal) >= 0,
                "unchanged access state avoids snapshot allocation");
        }

        private static void SamePclCandidatePolicyIsFailOpenAndAllianceAware()
        {
            Assert(EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 3, 0, ValidPlayer, Allied),
                "uncaptured hostile bridge is a candidate");
            Assert(EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 3, 4, ValidPlayer, Allied),
                "foreign third-party capture remains a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 1, 0, ValidPlayer, Allied),
                "own bridge is not a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 2, 0, ValidPlayer, Allied),
                "allied bridge is not a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 3, 2, ValidPlayer, Allied),
                "allied capture is not a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(0, 3, 0, ValidPlayer, Allied),
                "invalid query fails open");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 99, 0, ValidPlayer, Allied),
                "invalid owner fails open");
        }

        private static void RectangleDistanceSupportsSpatialBridgeDiagnosis()
        {
            Assert(EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                1, 1, 3, 3, 2, 2, 4, 4) == 0,
                "overlapping rectangles have zero distance");
            Assert(EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                1, 1, 3, 3, 4, 1, 6, 3) == 1,
                "touching tile columns have distance one");
            Assert(EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                1, 1, 3, 3, 8, 9, 10, 11) == 6,
                "Chebyshev rectangle distance uses the farther axis");
        }

        private static void TopologyRejectionClassificationIsDeterministic()
        {
            AssertTopology(TopologyDiagnosticDisposition.InvalidBridge,
                false, true, true, true, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidGlobalId,
                true, false, true, true, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidGatehouseId,
                true, true, false, true, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidGateState,
                true, true, true, false, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.MissingGatehouseEntry,
                true, true, true, true, true, false, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InconsistentReread,
                true, true, true, true, true, true, false, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidDoorTiles,
                true, true, true, true, true, true, true, false, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InconsistentReread,
                true, true, true, true, true, true, true, true, false, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidFootprint,
                true, true, true, true, true, true, true, true, true, false);
            AssertTopology(TopologyDiagnosticDisposition.Accepted,
                true, true, true, true, true, true, true, true, true, true);
        }

        private static void FootprintAdjacencyIgnoresBrokenEditorBounds()
        {
            var bridge = new[] { new RouteTilePoint(385, 416), new RouteTilePoint(386, 416) };
            var gate = new[] { new RouteTilePoint(385, 417), new RouteTilePoint(386, 417) };
            var distant = new[] { new RouteTilePoint(390, 420) };
            Assert(EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(bridge, gate),
                "occupied bridge and gate footprints are directly adjacent");
            Assert(!EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(bridge, distant),
                "distant footprints are not associated");
            Assert(!EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(null, gate),
                "missing footprint fails open");
        }

        private static void SparseFootprintsFollowVanillaSlotSemantics()
        {
            SparseFootprintAccumulator dense3 = BuildFootprint(3, -1, -1);
            Assert(dense3.IsValid && dense3.ValidTileCount == 9 && dense3.EmptyCellCount == 0,
                "dense 3x3 footprint is valid");

            SparseFootprintAccumulator sparse3 = BuildFootprint(3, 4, -1);
            Assert(sparse3.IsValid && sparse3.ValidTileCount == 8 && sparse3.EmptyCellCount == 1,
                "zero is an unused sparse 3x3 cell");
            Assert(sparse3.MinX == 0 && sparse3.MinY == 0 &&
                sparse3.MaxX == 2 && sparse3.MaxY == 2,
                "sparse bounds come from valid occupied tiles");

            SparseFootprintAccumulator sparse5 = BuildFootprint(5, 0, 24);
            Assert(sparse5.IsValid && sparse5.ValidTileCount == 23 &&
                sparse5.EmptyCellCount == 2,
                "sparse 5x5 footprint accepts multiple holes");
            SparseFootprintAccumulator sparse6 = BuildFootprint(6, 7, 29);
            Assert(sparse6.IsValid && sparse6.ValidTileCount == 34 &&
                sparse6.CellCount == SparseFootprintAccumulator.MaximumCellCount,
                "sparse 6x6 footprint stays within inline capacity");

            var invalidTile = new SparseFootprintAccumulator(3);
            invalidTile.AddCell(0, 320801, false, 0, 0);
            Assert(invalidTile.Status == SparseFootprintStatus.InvalidNonZeroTile &&
                invalidTile.InvalidCellIndex == 0 && invalidTile.InvalidTileId == 320801,
                "invalid nonzero tile is categorized and retained for diagnosis");
            var empty = new SparseFootprintAccumulator(3);
            for (int index = 0; index < empty.CellCount; index++)
                empty.AddCell(index, 0, false, 0, 0);
            Assert(empty.Status == SparseFootprintStatus.Empty,
                "all-zero footprint is categorized as empty");
            Assert(new SparseFootprintAccumulator(0).Status == SparseFootprintStatus.InvalidGridSize &&
                new SparseFootprintAccumulator(7).Status == SparseFootprintStatus.InvalidGridSize,
                "grid sizes outside one through six are rejected before inline reads");
        }

        private static SparseFootprintAccumulator BuildFootprint(
            uint gridSize, int firstHole, int secondHole)
        {
            var result = new SparseFootprintAccumulator(gridSize);
            for (int index = 0; index < result.CellCount; index++)
            {
                if (index == firstHole || index == secondHole)
                    result.AddCell(index, 0, false, 0, 0);
                else
                    result.AddCell(index, (uint)(1000 + index), true,
                        index % (int)gridSize, index / (int)gridSize);
            }
            return result;
        }

        private static void InvalidFootprintsRemainEntityLocal()
        {
            var rejected = new SparseFootprintAccumulator(3);
            rejected.AddCell(0, uint.MaxValue, false, 0, 0);
            SparseFootprintAccumulator valid = BuildFootprint(3, 4, -1);
            Assert(!rejected.IsValid && valid.IsValid,
                "one rejected building cannot invalidate another building footprint");
            Assert(rejected.Fingerprint != valid.Fingerprint,
                "failure marker and valid sparse footprint signatures differ");
        }

        private static void TopologySignatureAndOrphanDiagnosticsAreNonThrowing()
        {
            string topology = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            Assert(topology.IndexOf("Shared.GameBuildingFootprint", StringComparison.Ordinal) < 0,
                "topology provider no longer invokes strict shared footprint bounds");
            Assert(topology.IndexOf("GetOccupiedTileIds", StringComparison.Ordinal) < 0,
                "bounded inline reader replaces allocating occupied-tile API");
            Assert(topology.IndexOf("TryReadSparseFootprint", StringComparison.Ordinal) >= 0 &&
                topology.IndexOf("footprint.Status", StringComparison.Ordinal) >= 0,
                "builder and topology signature share sparse-footprint semantics");
            Assert(topology.IndexOf("!IsDiagnosticActive(building.r_AliveState) || building.r_GlobalId == 0",
                StringComparison.Ordinal) >= 0,
                "dead, deleted and zero-global slots cannot poison the signature");
            Assert(topology.IndexOf("record->r_SubjectGlobalId", StringComparison.Ordinal) >= 0 &&
                topology.IndexOf("record->r_EntryTilePositionX", StringComparison.Ordinal) >= 0 &&
                topology.IndexOf("record->r_ExitTilePositionY", StringComparison.Ordinal) >= 0,
                "connection identity and geometry participate in topology signatures");
            Assert(topology.IndexOf("GetSpatialGateCandidates(tiles, gateInfos)",
                StringComparison.Ordinal) >= 0,
                "orphan diagnostics derive distances only from validated tile diagnostics");
        }

        private static void UniqueSpatialGateAssociationFailsOpenWhenAmbiguous()
        {
            var bridge = new[] { new RouteTilePoint(10, 10) };
            var gates = new[]
            {
                new[] { new RouteTilePoint(10, 11) },
                new[] { new RouteTilePoint(11, 10) },
                new[] { new RouteTilePoint(30, 30) }
            };
            Assert(EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                    bridge, gates, new[] { true, false, true }) == 0,
                "same-owner eligibility selects one adjacent gate");
            Assert(EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                    bridge, gates, new[] { true, true, true }) == -1,
                "two adjacent candidates fail open");
            Assert(EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                    bridge, gates, new[] { false, false, true }) == -1,
                "no eligible adjacent candidate fails open");
        }

        private static void DirectionEdgesRequireBothNativeDirections()
        {
            Assert(EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0x04, 0x40, 2),
                "east edge and west opposite edge form a native connection");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0x04, 0x00, 2),
                "missing opposite edge is closed");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0x00, 0x40, 2),
                "missing source edge is closed");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0xFF, 0xFF, -1),
                "invalid negative direction fails closed inside the managed search");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0xFF, 0xFF, 8),
                "direction above native range fails closed inside the managed search");
        }

        private static void TileRouteNativeContractIsPinned()
        {
            Assert(EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva == 0x8F251 &&
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength == 29,
                "direct cursor DB650 call block and span");
            Assert(EnemyGatePathfindingNativeDefinition.DirectCursorSearchCallRva == 0x8F269 &&
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchReturnRva == 0x8F26E &&
                    EnemyGatePathfindingNativeDefinition.DirectTileSearchRva == 0xDB650,
                "direct cursor call, target and return RVAs");
            Assert(EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva == 0x8F1BF &&
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength == 14 &&
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionReturnRva == 0x8F1CD &&
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionConsumerRva == 0x8F1D2,
                "normal cursor PCL decision span and consumer RVAs");
            Assert(EnemyGatePathfindingNativeDefinition.CursorManagerRva == 0x3A11DC0 &&
                    EnemyGatePathfindingNativeDefinition.CursorMouseTileXRva == 0x3A11E2C &&
                    EnemyGatePathfindingNativeDefinition.CursorMouseTileYRva == 0x3A11E30 &&
                    EnemyGatePathfindingNativeDefinition.CursorMouseTileIdRva == 0x3A11E38 &&
                    EnemyGatePathfindingNativeDefinition.TileRowStartRva == 0x402FF2C,
                "cursor coordinates and row-start table RVAs");
            Assert(EnemyGatePathfindingNativeDefinition.NativePathManagerRva == 0x60AD660 &&
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchNodeLimit == 400000,
                "Vanilla DB650 manager and node limit");
            Assert(EnemyGatePathfindingNativeDefinition.PathDirectionGridRva == 0x51890D0,
                "native direction grid RVA");
            Assert(EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive == 320800,
                "native tile-grid capacity");
            Assert(EnemyGatePathfindingNativeDefinition.MapGridWidth == 800,
                "native tile-grid width");
            Assert(EnemyGatePathfindingNativeDefinition.PathBuilderRva == 0xF4930,
                "central tile builder function entry RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement == 0x64CCED2,
                "native capture-table displacement");
        }

        private static void NativeRouteHotPathsRemainPrimitiveOnly()
        {
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePclSource = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string emitterSource = File.ReadAllText(Path.Combine("src", "DirectionFilterAdapterEmitter.cs"));
            string tacticalEmitterSource = File.ReadAllText(
                Path.Combine("src", "AiTacticalTargetAdapterEmitter.cs"));
            string[] forbidden =
            {
                "GamePlayerManagerAPI", "GameUnitManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console.",
                "new List", "new Dictionary", "new int[", "new byte[", "new string"
            };
            foreach (string method in new[]
            {
                "FilterUnrelatedCapturedEnemyGatePclGraph",
                "FilterUnrelatedCapturedEnemyGateBuilderPrecheck",
                "FilterUnrelatedCapturedEnemyGate"
            })
            {
                string capturedBody = ExtractMethodBody(runtimeSource, method);
                foreach (string token in forbidden)
                    Assert(capturedBody.IndexOf(token, StringComparison.Ordinal) < 0,
                        method + " hot path excludes " + token);
            }
            string builderBody = ExtractMethodBody(emitterSource, "Emit");
            string tacticalBody = ExtractMethodBody(tacticalEmitterSource, "Emit");
            foreach (string token in new[]
            {
                "GameUnitManagerAPI", "GamePlayerManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console."
            })
                Assert(builderBody.IndexOf(token, StringComparison.Ordinal) < 0,
                    "native direction adapter excludes " + token);
            foreach (string token in new[]
            {
                "GameUnitManagerAPI", "GamePlayerManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console.", "Interlocked"
            })
                Assert(tacticalBody.IndexOf(token, StringComparison.Ordinal) < 0,
                    "native AI tactical adapter excludes " + token);
            Assert(samePclSource.IndexOf("managedReplacementSearches=0", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("managedCursorSearches=0", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("GateGridRouteSearch", StringComparison.Ordinal) < 0 &&
                    !File.Exists(Path.Combine("src", "CursorGateRouteFilter.cs")),
                "all managed whole-map and cursor searches are absent");
            foreach (string wrapper in new[]
            {
                "FilterBuilder", "FilterAttack", "FilterBuilding", "FilterConsumer",
                "FilterAlternateConsumer", "FilterCandidateSearch", "FilterCursor",
                "FilterDirectCursorSearch", "FilterCursorPclDecision", "FilterAiTacticalTarget"
            })
            {
                string body = ExtractMethodBody(samePclSource, wrapper);
                Assert(body.IndexOf("=>", StringComparison.Ordinal) < 0 &&
                        body.IndexOf("new ", StringComparison.Ordinal) < 0,
                    wrapper + " creates no closure or per-query object");
            }
        }

        private static void UnsafeGlobalMutationAndWholePclDetourAreAbsent()
        {
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePclSource = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            Assert(!File.Exists(Path.Combine("src", "CursorGateRouteFilter.cs")),
                "managed cursor filter and its overlay/search fallbacks are absent");
            Assert(runtimeSource.IndexOf("GetNextReachablePclDelegate", StringComparison.Ordinal) < 0,
                "whole PCL function detour delegate is absent");
            Assert(runtimeSource.IndexOf("AddDetour", StringComparison.Ordinal) < 0,
                "whole PCL function detour installation is absent");
            Assert(samePclSource.IndexOf("PathBuilderRva =", StringComparison.Ordinal) < 0 &&
                    samePclSource.IndexOf("transaction.AddDetour", StringComparison.Ordinal) >= 0,
                "Same-PCL uses the separately validated F4930 function detour");
            Assert(samePclSource.IndexOf("globalDirectionGridWrites=0", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("PathDirectionGridRva", StringComparison.Ordinal) < 0,
                "Same-PCL filters loaded bytes without addressing the global grid for writes");
            Assert(samePclSource.IndexOf("if (!ownerConflict)", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("EnemyGatePathPolicyBridge.TryRegister(this)",
                        StringComparison.Ordinal) >= 0,
                "shared mode leaves existing function detours with Bugfixes and registers the policy");
        }

        private static void DirectionMaskBlocksOnlyTheGatePassage()
        {
            string topology = File.ReadAllText(Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string boundary = ExtractMethodBody(topology, "ClearBoundary");
            Assert(boundary.IndexOf("1 << direction", StringComparison.Ordinal) >= 0 &&
                    boundary.IndexOf("(direction + 4) & 7", StringComparison.Ordinal) >= 0,
                "gate boundary clears both directions of the crossing edge");
            Assert(boundary.IndexOf("direction - 1", StringComparison.Ordinal) >= 0 &&
                    boundary.IndexOf("direction + 1", StringComparison.Ordinal) >= 0,
                "gate boundary also rejects diagonal corner cuts");
            string axis = ExtractMethodBody(topology, "TryResolvePassageAxis");
            Assert(axis.IndexOf("PassageAxisResolver.TryResolve", StringComparison.Ordinal) >= 0,
                "topology delegates axis selection to the tested evidence resolver");
        }

        private static void VanillaDirectionFilterContractsAreAtomic()
        {
            int[] expectedRvas =
            {
                0xD9EA6, 0xDA783, 0xDACB2, 0xDB242, 0xF31A8, 0xF33F5,
                0xDB857, 0xDB947, 0xDBA36, 0xDBB26, 0xDC536
            };
            int[] expectedLengths = { 14, 18, 18, 17, 15, 14, 17, 17, 17, 17, 16 };
            Assert(EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length == 11,
                "all eleven player-aware native direction loads are represented");
            for (int index = 0; index < expectedRvas.Length; index++)
            {
                Assert(EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[index] == expectedRvas[index],
                    "direction-filter RVA " + index);
                Assert(EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[index] == expectedLengths[index],
                    "direction-filter displaced length " + index);
            }
            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string source = File.ReadAllText(Path.Combine("src", "DirectionFilterAdapterEmitter.cs"));
            Assert(runtime.IndexOf("for (int index = 0; index < edgeHooks.Length; index++)",
                    StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("transaction.AddInline(directCursorHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("transaction.AddInline(cursorPclDecisionHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorPclDecisionHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("transaction.Commit()", StringComparison.Ordinal) >= 0,
                "both cursor sites and all edge adapters share one atomic transaction");
            Assert(source.IndexOf("__dword_ptr.gs[0x48]", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("ThreadSlotCount", StringComparison.Ordinal) >= 0,
                "native adapters validate the fixed TEB thread slot");
            Assert(source.IndexOf("assembler.and(al", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("assembler.and(r11b", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("assembler.and(cl", StringComparison.Ordinal) >= 0,
                "all native direction-load destination registers are masked");
            foreach (string method in new[]
            {
                "EmitRaxDestination", "EmitR11Destination", "EmitRcxDestination",
                "EmitRcxR12Destination"
            })
            {
                string body = ExtractMethodBody(source, method);
                Assert(CountOccurrences(body, "AddInstruction(original[0])") == 1 &&
                        body.IndexOf("for (int index = 0; index < original.Length", StringComparison.Ordinal) < 0,
                    method + " emits its original load once and has no duplicate full replay");
            }
        }

        private static void AttackOrderCorrelationIsLosslessAndObservational()
        {
            string plugin = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingTestPlugin.cs"));
            string runtime = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePcl = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string correlation = File.ReadAllText(
                Path.Combine("src", "AttackOrderCorrelationDiagnostics.cs"));
            string topology = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string bridge = File.ReadAllText(Path.Combine("..", "..", "APIShared",
                "src", "EnemyGatePathPolicyBridge.cs"));
            string sharedOwner = File.ReadAllText(Path.Combine("..", "..",
                "BugfixesAndQoL", "src", "FriendlyMoatMovementRuntime.cs"));

            Assert(plugin.Contains("Subscribe(ObserveTargetOrder)") &&
                    plugin.Contains("Subscribe(ObserveTribeMove)") &&
                    plugin.Contains("Subscribe(ObserveUnitMove)"),
                "all three existing Script Extender command events are observed");
            Assert(correlation.Contains("[ThreadStatic]") &&
                    !correlation.Contains("MaximumSamples") &&
                    !correlation.Contains("MaximumNestedOrders"),
                "command contexts have no lossy example or pattern limit");
            Assert(correlation.IndexOf("EventHookPhase.Pre", StringComparison.Ordinal) >= 0 &&
                    correlation.IndexOf("EventHookPhase.Post", StringComparison.Ordinal) >= 0 &&
                    correlation.IndexOf("post-without-matching-pre", StringComparison.Ordinal) >= 0,
                "AI order correlation handles nested pre/post event phases defensively");
            Assert(correlation.Contains("IsAIPlayer(player)") &&
                    correlation.Contains("ObserveGatePrecheck") &&
                    correlation.Contains("ObserveBuilder"),
                "AI gate precheck and route result use a short command context");
            Assert(correlation.IndexOf("FindPath", StringComparison.OrdinalIgnoreCase) < 0 &&
                    correlation.IndexOf("Breadth", StringComparison.OrdinalIgnoreCase) < 0 &&
                    correlation.IndexOf("Queue<", StringComparison.Ordinal) < 0,
                "order diagnostics start no managed route search");
            Assert(samePcl.IndexOf("attackOrderDiagnostics?.ObserveBuilder", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("plannerState0x419Queries", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("Enemy-gate AI order checkpoint", StringComparison.Ordinal) >= 0,
                "F4930 results are correlated and state 0x419 is named separately");
            Assert(correlation.Contains("DebugLogHelper.LogInfo") &&
                    correlation.Contains("totals.Drain(out var definitions)") &&
                    correlation.Contains("order-gate-switch") &&
                    correlation.Contains("CurrentTribeOrder(player, frame?.Tribe ?? 0)"),
                "aggregate output is emitted from the deferred checkpoint");
            Assert(correlation.Contains("CaptureGateStates(player)") &&
                    correlation.Contains("ObserveGateStates(player, \"checkpoint\"") &&
                    topology.Contains("gate->r_GateState") &&
                    topology.Contains("gate->r_AIWalkableState") &&
                    topology.Contains("connection->r_IsEnabledOrOpen") &&
                    topology.Contains("bridge->r_AliveState"),
                "AI order checkpoints read real gate and drawbridge state without a new hook");
            Assert(correlation.Contains("stage") &&
                    correlation.Contains("\"unattributed-player\"") &&
                    correlation.Contains("\"region-pair\"") &&
                    bridge.Contains("interface IEnemyGateRegionPairObserver") &&
                    sharedOwner.Contains("policy is IEnemyGateRegionPairObserver observer") &&
                    sharedOwner.Contains("ObserveScopedRegionPairReachabilityCore(") &&
                    sharedOwner.Contains("out int vanillaResult"),
                "shared E2610 owner reports both results only to a registered observer");

            var aggregate = new AiGateDecisionAggregate();
            for (int i = 0; i < 160; i++)
            {
                int gate = i % 2 == 0 ? 101 : 202;
                aggregate.Record(2, gate, "gate-precheck", "vanilla=open,policy=closed",
                    4, 77, i, 9);
                aggregate.Record(2, gate, "unit-context", "one:" + gate,
                    4, 77, 55, 9);
            }
            var rows = aggregate.Drain();
            long count = 0, changes = 0;
            foreach (var row in rows) { count += row.Count; changes += row.TargetChanges; }
            Assert(aggregate.Observations == 320 && count == 320 && rows.Length == 4,
                "more than 32 distinct commands are counted exactly in four compact rows");
            Assert(changes >= 159, "repeated A/B gate alternation remains visible");
            Assert(aggregate.Drain().Length == 0, "a drained interval does not replay rows");
        }

        private static void GateDiagnosticRolesAndBuildingContextsAreLossless()
        {
            var records = new NativeGateAccessRecord[2];
            var globals = new uint[] { 0, 1234 };
            foreach (int captured in new[] { 0, 2, 3, 4, 0 })
            {
                records[1] = new NativeGateAccessRecord(true, 1, captured,
                    (ushort)((1 << 1) | (1 << 2)), (ushort)((1 << captured) | (1 << 5)), (ushort)(1 << 6));
                var snapshot = new NativeGateAccessSnapshot((NativeGateAccessRecord[])records.Clone(), 7, globals);
                ulong fingerprint = snapshot.TopologyFingerprint;
                for (int player = 1; player <= 8; player++)
                {
                    NativeGateSnapshotDecision before = snapshot.Evaluate(player, 1, 1, captured);
                    string actual = GateDiagnosticClassification.Describe(snapshot, player, 1, 1234, 1, captured);
                    string owner = player == 1 ? "own" : player == 2 ? "allied" : "enemy";
                    string capture = captured == 0 ? "uncaptured" : captured == player ? "captured-by-self" :
                        player == 5 ? "captured-by-ally" : "captured-by-other";
                    Assert(actual.Contains("ownerRelation=" + owner) && actual.Contains("captureRelation=" + capture),
                        "owner and capture classifications remain independent through capture/recapture");
                    Assert(actual.Contains("policyDecision=" + before) &&
                        snapshot.Evaluate(player, 1, 1, captured) == before && snapshot.TopologyFingerprint == fingerprint,
                        "diagnostics preserve the exact policy decision and snapshot");
                }
                Assert(GateDiagnosticClassification.Describe(snapshot, 1, 1, 1234, 1, captured)
                    .Contains("captureRelation=" + (captured == 0 ? "uncaptured" : "captured-by-other")),
                    "own label never hides foreign capture");
                Assert(GateDiagnosticClassification.Describe(snapshot, 1, 1, 999, 1, captured).Contains("identity-unverified"),
                    "reused building slot requires matching global ID");
                Assert(GateDiagnosticClassification.Describe(snapshot, 1, 1, 1234, 2, captured).Contains("owner-snapshot-mismatch"),
                    "changed owner cannot receive a trusted role");
                Assert(GateDiagnosticClassification.Describe(snapshot, 1, 1, 1234, 1, captured == 0 ? 2 : 0)
                    .Contains("capture-snapshot-mismatch"), "stale capture stays unknown");
            }
            var empty = NativeGateAccessSnapshot.Empty;
            foreach (int invalid in new[] { -1, 0, 9 })
                Assert(GateDiagnosticClassification.Describe(empty, invalid, 1, 1234, 1, 0)
                    .Contains("ownerRelation=unknown"), "invalid player is not an enemy label");
            Assert(GateDiagnosticClassification.Describe(empty, 1, 1, 1234, 1, 0).Contains("identity-unverified"),
                "untracked and empty snapshots stay unknown");
            string previousCapture = null;
            foreach (int captured in new[] { 3, 4 })
            {
                records[1] = new NativeGateAccessRecord(true, 1, captured, 2, (ushort)(1 << captured), 64);
                var snapshot = new NativeGateAccessSnapshot((NativeGateAccessRecord[])records.Clone(), 7, globals);
                string role = GateDiagnosticClassification.Describe(snapshot, 6, 1, 1234, 1, captured);
                Assert(previousCapture == null || previousCapture != role,
                    "different foreign capturers produce different aggregate keys");
                previousCapture = role;
            }
            var aggregate = new AiGateDecisionAggregate();
            int[] previousMismatchCounts = { 26, 9, 3 };
            for (int epoch = 0; epoch < 3; epoch++)
                for (int i = 0; i < previousMismatchCounts[epoch]; i++)
                    aggregate.Record(5, 0, "building-search-context",
                        GateDiagnosticClassification.BuildingContextResult(new BuildingSearchPlayerContext(
                            1, 4000 + i, 5, 10000, 5, 10000, 1, 20000, 5, 2, false)), epoch, 4000 + i, i, 7,
                        "tribeGlobal=" + (10000 + i));
            var scopes = aggregate.Drain();
            Assert(scopes.Length == 3 && aggregate.Observations == 38, "all previous 26/9/3 conflicts counted");
            foreach (var row in scopes)
                Assert(row.Count == previousMismatchCounts[row.Command] && row.Result.Contains("argumentRole=unexplained") &&
                    row.Result.Contains("usedPlayer=-1"), "scope provenance neither substitutes a player nor drops conflicts");
            for (int i = 0; i < 100; i++)
                for (int repeat = 0; repeat < 10; repeat++)
                    aggregate.Record(1, i + 1, "gate-live-checkpoint", "ownerRelation=own,captureRelation=uncaptured",
                        0, 0, 0, 0);
            var gates = aggregate.Drain();
            long count = 0; foreach (var row in gates) count += row.Count;
            Assert(gates.Length == 100 && count == 1000, "more than 32 gate combinations remain exact and compact");
            string provider = File.ReadAllText(Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            int begin = provider.IndexOf("internal GateLiveStateObservation[] CaptureGateStates", StringComparison.Ordinal);
            int end = provider.IndexOf("internal void ProcessDeferred()", begin, StringComparison.Ordinal);
            string live = provider.Substring(begin, end - begin);
            Assert(!live.Contains("UnrelatedByPlayer") && live.Contains("!observed.Add(info.GateId)"),
                "all gate roles included once, even with multiple or no bridges");
            string correlation = File.ReadAllText(Path.Combine("src", "AttackOrderCorrelationDiagnostics.cs"));
            Assert(correlation.Contains("nativeComparison=") && correlation.Contains("effectiveComparison=") &&
                !correlation.Contains("\"vanilla=\" + (vanillaExcluded"), "comparison is not called physical open/closed");
        }

        private static void CaptureRecoveryRequiresFreshExactPublication()
        {
            NativeGateAccessSnapshot Publication(int capture, long generation, long epoch = 1,
                uint global = 1234, int owner = 1)
            {
                var records = new NativeGateAccessRecord[2];
                records[1] = new NativeGateAccessRecord(true, owner, capture, 2, 4, 32);
                return new NativeGateAccessSnapshot(records, 77, new uint[] { 0, global }, epoch, generation);
            }
            var requests = new DeferredCaptureRefreshRequest();
            var recovery = new CaptureTransitionDiagnostics();
            recovery.Reset(1);
            NativeGateAccessSnapshot source = Publication(0, 1);
            recovery.Publish(source);
            Assert(!recovery.Observe(null, 1, 1234, 1, 2) &&
                !recovery.Observe(Publication(0, 99), 1, 1234, 1, 2),
                "missing or unpublished generation cannot register a transition");
            for (int i = 0; i < 43; i++)
            {
                Assert(recovery.Observe(source, 1, 1234, 1, 2), "verified capture observation counted");
                requests.Request();
            }
            Assert(requests.Consume() && !requests.Consume(), "43 callbacks request one deferred refresh");
            requests.Request();
            Assert(requests.Consume(), "request after consumption survives for next deferred pass");
            string[] pending = recovery.DrainChanges();
            Assert(pending.Length == 1 && pending[0].Contains("unresolved=43"), "repetitions compact, none lost");
            recovery.Publish(Publication(2, 1));
            recovery.Publish(Publication(2, 2, 2));
            Assert(recovery.Recovered == 0, "old generation and foreign map cannot confirm");
            recovery.Publish(Publication(3, 2));
            Assert(recovery.Recovered == 0, "another capturer does not confirm the observed capturer");
            recovery.Publish(Publication(2, 3, global: 999));
            Assert(recovery.Recovered == 0, "reused building ID requires original global ID");
            recovery.Publish(Publication(2, 4, owner: 3));
            Assert(recovery.Recovered == 0, "changed owner cannot confirm old owner observation");
            recovery.Publish(Publication(2, 5));
            Assert(recovery.Recovered == 43 && recovery.Summary.Contains("unresolved=0"), "exact later publication confirms all 43");
            string[] confirmed = recovery.DrainChanges();
            Assert(confirmed.Length == 1 && confirmed[0].Contains("proofGeneration=5") &&
                recovery.DrainChanges().Length == 0, "changed proof logged once");
            Assert(recovery.Observe(source, 1, 1234, 1, 2), "late observation using old snapshot still counted");
            recovery.Publish(Publication(2, 5));
            Assert(recovery.Recovered == 43, "publication predating observation cannot confirm it");
            recovery.Publish(Publication(2, 6));
            Assert(recovery.Recovered == 44, "fresh equivalent publication confirms late observation");
            Assert(!recovery.Observe(source, 1, 999, 1, 2) && !recovery.Observe(source, 1, 1234, 3, 2) &&
                !recovery.Observe(source, 1, 1234, 1, 9), "unverified mismatch cannot earn recovered credit");
            Assert(recovery.Observe(Publication(2, 6), 1, 1234, 1, 0), "recapture-to-zero is observed");
            Assert(recovery.Summary.Contains("unresolved=1"), "unconfirmed transition stays unresolved at final checkpoint");
            Assert(recovery.Observe(Publication(2, 6), 1, 1234, 1, 3), "second capturer change is counted independently");
            recovery.Publish(Publication(3, 7));
            Assert(recovery.Recovered == 45 && recovery.Summary.Contains("unresolved=1"),
                "different capturer confirms only its own observation, not the missing recapture");
            recovery.Publish(Publication(0, 8));
            Assert(recovery.Recovered == 46 && recovery.Summary.Contains("unresolved=0"),
                "later exact recapture confirms the remaining observation");
            recovery.Reset(3);
            Assert(recovery.Recovered == 0 && recovery.Summary.Contains("observed=0") &&
                !recovery.Observe(source, 1, 1234, 1, 2), "map reset rejects old callbacks and resets generation");

            var oldRecords = new NativeGateAccessRecord[81];
            var newRecords = new NativeGateAccessRecord[81];
            var globals = new uint[81];
            for (int id = 1; id <= 80; id++)
            {
                oldRecords[id] = new NativeGateAccessRecord(true, 1, 0, 2, 0, 32);
                newRecords[id] = new NativeGateAccessRecord(true, 1, 2, 2, 4, 32);
                globals[id] = (uint)(1000 + id);
            }
            var oldSnapshot = new NativeGateAccessSnapshot(oldRecords, 7, globals, 3, 1);
            recovery.Publish(oldSnapshot);
            for (int id = 1; id <= 80; id++)
                for (int repeat = 0; repeat < 5; repeat++)
                    Assert(recovery.Observe(oldSnapshot, id, globals[id], 1, 2), "more than 32 identities counted");
            recovery.Publish(new NativeGateAccessSnapshot(newRecords, 8, globals, 3, 2));
            Assert(recovery.Recovered == 400 && recovery.DrainChanges().Length == 80,
                "400 observations across 80 identities confirmed in 80 compact rows");
            NativeGateAccessSnapshot stamped = oldSnapshot.WithDiagnosticPublication(3, 99);
            Assert(oldSnapshot.PolicyEquals(stamped) && oldSnapshot.TopologyFingerprint == stamped.TopologyFingerprint &&
                oldSnapshot.Evaluate(5, 1, 1, 0) == stamped.Evaluate(5, 1, 1, 0),
                "diagnostic generation never changes the access policy");
            requests.Request(); requests.Reset(); Assert(!requests.Consume(), "map clear drops stale refresh request");
        }

        private static void AiTacticalTargetContractsAreAtomicAndExecutable()
        {
            const ulong library = 0x180000000UL;
            const ulong slots = 0x123456789ABC0000UL;
            int[] rvas = { 0xF1910, 0xEF1F0, 0xEEAD0 };
            int[] lengths = { 17, 17, 18 };
            int[] rejects = { 0xF1A17, 0xEF2D4, 0xEEC1B };
            Register[] sources = { Register.R14, Register.R11, Register.RDI };
            Register[] targets = { Register.R8, Register.R10, Register.None };
            Register[] directions = { Register.RSI, Register.R9, Register.AL };
            Assert(EnemyGatePathfindingNativeDefinition.AiTacticalTargetSelectionRva == 0x113BC0,
                "AI tactical target-selection scope is pinned to the audited Vanilla entry");
            Assert(EnemyGatePathfindingNativeDefinition.NativeTribeRecordStride == 0x688 &&
                    EnemyGatePathfindingNativeDefinition.NativeTribePlayerIdOffset == 0x2C,
                "AI tactical scope uses the audited tribe player field");
            for (int site = 0; site < rvas.Length; site++)
            {
                Assert(EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas[site] == rvas[site] &&
                        EnemyGatePathfindingNativeDefinition.AiTacticalFilterLengths[site] == lengths[site] &&
                        EnemyGatePathfindingNativeDefinition.AiTacticalRejectRvas[site] == rejects[site],
                    "AI tactical adapter " + site + " has its exact span and rejection target");
                Assert(AiTacticalTargetAdapterEmitter.GetSourceRegister(site) == sources[site] &&
                        AiTacticalTargetAdapterEmitter.GetTargetRegister(site) == targets[site] &&
                        AiTacticalTargetAdapterEmitter.GetDirectionRegister(site) == directions[site],
                    "AI tactical adapter " + site + " uses the audited source/target/direction registers");
                byte[] original = EnemyGatePathfindingNativeDefinition.GetAiTacticalFilterBytes(site);
                ulong ip = library + unchecked((ulong)rvas[site]);
                ulong reject = library + unchecked((ulong)rejects[site]);
                ulong stub = library + 0x02300000UL + unchecked((ulong)(site * 0x1000));
                byte[] emitted = AiTacticalTargetAdapterEmitter.AssembleAndValidate(
                    original, ip, site, slots, reject, stub);
                Assert(emitted.Length > original.Length,
                    "AI tactical adapter " + site + " really assembles");
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(emitted)); decoder.IP = stub;
                int invalid = 0, bounds = 0, rejectionBranches = 0;
                while (decoder.IP < stub + (ulong)emitted.Length)
                {
                    decoder.Decode(out Instruction instruction);
                    if (instruction.Code == Code.INVALID) invalid++;
                    if (instruction.Mnemonic == Mnemonic.Cmp &&
                        instruction.Op1Kind == OpKind.Immediate32 &&
                        instruction.Immediate32 == EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                        bounds++;
                    if ((instruction.Mnemonic == Mnemonic.Jmp || instruction.Mnemonic == Mnemonic.Je) &&
                        instruction.NearBranchTarget == reject) rejectionBranches++;
                }
                Assert(invalid == 0 && bounds == (site == 2 ? 1 : 2) && rejectionBranches == 1,
                    "AI tactical adapter " + site + " disassembles with complete bounds and reject flow");

                byte[] probeBytes = new byte[64];
                for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
                Array.Copy(original, probeBytes, original.Length);
                IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
                try
                {
                    Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                    using (var probe = new X64InlineHook(
                        unchecked((ulong)probeMemory.ToInt64()), lengths[site]))
                        Assert(probe.DisplacedByteCount == lengths[site],
                            "RedBird displaces the exact AI tactical span " + site);
                }
                finally { Marshal.FreeHGlobal(probeMemory); }
            }
            Assert(!AiTacticalTargetAdapterEmitter.IsTileIndexInRange(-1) &&
                    !AiTacticalTargetAdapterEmitter.IsTileIndexInRange(320800) &&
                    AiTacticalTargetAdapterEmitter.IsTileIndexInRange(320799),
                "AI tactical adapters fail open outside the native tile capacity");
            Assert((0xFF & 0x04) != 0 && (0xFB & 0x04) == 0,
                "the immutable direction mask leaves ordinary edges visible and hides only cleared gate edges");

            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            Assert(runtime.IndexOf("aiTacticalTarget = AddDetour", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("for (int index = 0; index < tacticalEdgeHooks.Length; index++)",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("!aiTacticalTarget.Committed", StringComparison.Ordinal) >= 0,
                "AI tactical scope and all three adapters are members of the atomic hook transaction");
            string filter = ExtractMethodBody(runtime, "FilterAiTacticalTarget");
            Assert(filter.IndexOf("NativeTribeRecordStride", StringComparison.Ordinal) >= 0 &&
                    filter.IndexOf("NativeTribePlayerIdOffset", StringComparison.Ordinal) >= 0 &&
                    filter.IndexOf("ResolveTribePlayer(tribe)", StringComparison.Ordinal) >= 0,
                "AI tactical player comes from the native tribe record and is checked against the snapshot");
        }

        private static void SnapshotGateIdentityRequiresBothIds()
        {
            var records = new NativeGateAccessRecord[5];
            records[4] = new NativeGateAccessRecord(true, 1, 0, 0x0002, 0, 0x001C);
            var gateGlobals = new uint[5];
            gateGlobals[4] = 0x12345678;
            var snapshot = new NativeGateAccessSnapshot(records, 9, gateGlobals);
            Assert(snapshot.MatchesGateIdentity(4, 0x12345678),
                "matching building and global IDs identify a tracked gate");
            Assert(!snapshot.MatchesGateIdentity(4, 0x12345679),
                "a stale global ID does not identify the current gate");
            Assert(!snapshot.MatchesGateIdentity(3, 0x12345678),
                "a neighboring building ID is not inferred to be the gate");
            Assert(!snapshot.MatchesGateIdentity(4, 0),
                "a missing subject global ID remains harmless and unclassified");
        }

        private static void DirectionAdapterTileRegistersMatchNativeDataFlow()
        {
            Register[] expectedTiles =
            {
                Register.R8, Register.RDI, Register.RDI, Register.R10, Register.RAX,
                Register.R8, Register.RDI, Register.RDI, Register.RDI, Register.RDI,
                Register.R12
            };
            Register[] moduleBases =
            {
                Register.RDX, Register.RDX, Register.RDX, Register.RBX, Register.RDX,
                Register.RCX, Register.R13, Register.R13, Register.R13, Register.R13,
                Register.R9
            };
            Register[] zeroExtendedIndices =
            {
                Register.RAX, Register.R11, Register.R11, Register.RAX, Register.R9,
                Register.R9, Register.RAX, Register.RAX, Register.RAX, Register.RAX,
                Register.RAX
            };
            for (int site = 0; site < expectedTiles.Length; site++)
            {
                Register actual = DirectionFilterAdapterEmitter.GetTileRegister(site);
                Assert(actual == expectedTiles[site],
                    "direction adapter " + site + " uses its audited native tile register");
                Assert(actual != moduleBases[site],
                    "direction adapter " + site + " never indexes policy by its module base");
                Register index = DirectionFilterAdapterEmitter.GetZeroExtendedIndexRegister(site);
                Assert(index == zeroExtendedIndices[site] && index != moduleBases[site],
                    "direction adapter " + site +
                    " zero-extends through a scratch register that is not the native module base");

                byte[] original = EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(site);
                ulong ip = 0x180000000UL + unchecked((ulong)
                    EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[site]);
                Instruction directionRead = DecodeAt(original, ip,
                    site >= 6 && site <= 9 ? 1 : 0);
                bool containsTileAndBase =
                    (directionRead.MemoryBase == actual && directionRead.MemoryIndex == moduleBases[site]) ||
                    (directionRead.MemoryIndex == actual && directionRead.MemoryBase == moduleBases[site]);
                Assert(containsTileAndBase,
                    "direction adapter " + site +
                    " mapping matches the native Direction-Grid memory operand");
            }
        }

        private static void CrashDumpRegisterRegressionsFailOpen()
        {
            const long dumpModuleBase = 0x00007FFA80260000;
            Assert(DirectionFilterAdapterEmitter.GetTileRegister(5) == Register.R8,
                "21:59 crash regression: F33F5 uses R8 rather than RCX");
            Assert(DirectionFilterAdapterEmitter.GetTileRegister(0) == Register.R8,
                "D9EA6 uses R8 rather than RDX");
            Assert(DirectionFilterAdapterEmitter.GetTileRegister(3) == Register.R10,
                "DB242 uses R10 rather than RBX");
            Assert(!DirectionFilterAdapterEmitter.IsTileIndexInRange(dumpModuleBase),
                "the DLL base observed in the crash cannot index a policy mask");
            Assert(!DirectionFilterAdapterEmitter.IsTileIndexInRange(-1),
                "negative native tile indices fail open");
            Assert(DirectionFilterAdapterEmitter.IsTileIndexInRange(0) &&
                    DirectionFilterAdapterEmitter.IsTileIndexInRange(
                        EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive - 1),
                "both valid tile-grid boundaries are accepted");
            Assert(!DirectionFilterAdapterEmitter.IsTileIndexInRange(
                    EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive),
                "the first tile beyond the native grid fails open");
        }

        private static void DirectionAdaptersActuallyAssembleAndDecode()
        {
            const ulong library = 0x180000000UL;
            const ulong slots = 0x123456789ABC0000UL;
            for (int site = 0; site < EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length; site++)
            {
                byte[] originalBytes = EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(site);
                ulong originalIp = library + unchecked((ulong)
                    EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[site]);
                ulong stubIp = library + 0x02000000UL + unchecked((ulong)(site * 0x1000));
                byte[] emitted = DirectionFilterAdapterEmitter.AssembleAndValidate(
                    originalBytes, originalIp, site, slots, stubIp);
                Assert(emitted.Length > originalBytes.Length,
                    "direction adapter " + site + " assembles to a validated native stub");

                Instruction originalFirst = DecodeFirst(originalBytes, originalIp);
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(emitted));
                decoder.IP = stubIp;
                int matchingOriginalLoads = 0;
                int matchingTileAdds = 0;
                int matchingTileCopies = 0;
                int matchingRangeGuards = 0;
                Register expectedTile = DirectionFilterAdapterEmitter.GetTileRegister(site);
                Register expectedMask = site == 1 || site == 2
                    ? Register.RAX : site == 4 || site == 5 || site == 10
                    ? Register.R10 : Register.R9;
                Register expectedIndex = site == 1 || site == 2
                    ? Register.R11 : site == 4 || site == 5 ? Register.R9 : Register.RAX;
                while (decoder.IP < stubIp + (ulong)emitted.Length)
                {
                    decoder.Decode(out Instruction instruction);
                    Assert(instruction.Code != Code.INVALID,
                        "direction adapter " + site + " disassembles without invalid opcodes");
                    if (SameMemoryInstruction(instruction, originalFirst)) matchingOriginalLoads++;
                    if (instruction.Mnemonic == Mnemonic.Add &&
                        instruction.Op0Kind == OpKind.Register &&
                        instruction.Op0Register == expectedMask &&
                        instruction.Op1Kind == OpKind.Register)
                    {
                        Assert(instruction.Op1Register == expectedIndex,
                            "direction adapter " + site + " adds only its zero-extended tile index");
                        matchingTileAdds++;
                    }
                    if (instruction.Mnemonic == Mnemonic.Mov &&
                        instruction.Op0Kind == OpKind.Register &&
                        instruction.Op0Register == ToRegister32(expectedIndex) &&
                        instruction.Op1Kind == OpKind.Register &&
                        instruction.Op1Register == ToRegister32(expectedTile))
                        matchingTileCopies++;
                    if (instruction.Mnemonic == Mnemonic.Cmp &&
                        instruction.Op0Kind == OpKind.Register &&
                        instruction.Op0Register == ToRegister32(expectedTile) &&
                        instruction.Op1Kind == OpKind.Immediate32 &&
                        instruction.Immediate32 ==
                            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                        matchingRangeGuards++;
                }
                Assert(matchingOriginalLoads == 1,
                    "direction adapter " + site + " contains exactly one original producer load");
                Assert(matchingTileAdds == 1 && matchingTileCopies == 1 &&
                        matchingRangeGuards == 1,
                    "direction adapter " + site +
                    " has one zero-extended tile add and one bounds guard");

                byte[] probeBytes = new byte[64];
                for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
                Array.Copy(originalBytes, probeBytes, originalBytes.Length);
                IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
                try
                {
                    Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                    using (var probe = new X64InlineHook(
                        unchecked((ulong)probeMemory.ToInt64()), 14))
                    {
                        Assert(probe.DisplacedByteCount ==
                                EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[site],
                            "installed RedBird decodes the exact direction span " + site);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(probeMemory);
                }
            }

            byte[] duplicateBytes = EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(0);
            Instruction duplicate = DecodeFirst(duplicateBytes,
                library + unchecked((ulong)EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[0]));
            var invalid = new Assembler(64);
            invalid.AddInstruction(duplicate);
            invalid.AddInstruction(duplicate);
            bool rejected = false;
            try
            {
                using (var stream = new MemoryStream())
                    invalid.Assemble(new StreamCodeWriter(stream), library + 0x03000000UL);
            }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "Iced regression proves duplicate instruction IPs are rejected");
        }

        private static void BaselinePlayerScopesUseNativeArguments()
        {
            string source = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string attack = ExtractMethodBody(source, "FilterAttack");
            Assert(attack.IndexOf("QueryScope scope = Enter(player)", StringComparison.Ordinal) >= 0,
                "DBC60 binds its explicit eighth player argument");
            Assert(source.Contains("ResolveBuildingMovementPlayer(player, tribe)") &&
                source.Contains("=> ResolveBuildingMovementPlayer(rawSearchArgument, tribeId)"),
                "standalone/shared DA020 use one verified movement resolver independently of query player");
            Assert(source.IndexOf("ActivePlayerIdRva", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("GetLocalPlayerId", StringComparison.Ordinal) < 0,
                "195E30 follows Vanilla's active native player rather than the editor local-player API");
            Assert(source.IndexOf("FilterAlternateConsumer", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("FilterCandidateSearch", StringComparison.Ordinal) >= 0,
                "1232E0 and DC3C0 have explicit query scopes");
            Assert(source.IndexOf("Interlocked.Increment(ref aiQueries)",
                        StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Interlocked.Increment(ref aiNoRoutes)",
                        StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Interlocked.Increment(ref attackQueries)",
                        StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Interlocked.Increment(ref candidateQueries)",
                        StringComparison.Ordinal) >= 0,
                "AI builder, no-route, attack, and candidate coverage are independently observable");
            Assert(EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva == 0x88E3D70 &&
                    EnemyGatePathfindingNativeDefinition.AlternateBuildingConsumerRva == 0x1232E0 &&
                    EnemyGatePathfindingNativeDefinition.PlayerAwareCandidateSearchRva == 0xDC3C0,
                "baseline-bound player globals and function entries are pinned");
        }

        private static void BuildingSearchContextsSeparateVanillaAndMovement()
        {
            BuildingSearchPlayerContext Context(int raw = 2, int plan = 2, bool editor = false,
                int snapshotOwner = 5, uint snapshotGlobal = 100, int liveOwner = 5,
                uint liveGlobal = 100, int leader = 42, uint leaderGlobal = 200, int control = 5) =>
                new BuildingSearchPlayerContext(raw, 7, snapshotOwner, snapshotGlobal,
                    liveOwner, liveGlobal, leader, leaderGlobal, control, plan, editor);
            var planner = Context();
            Assert(planner.MovementPlayer == 5 && planner.RawSearchPlayer == 2 && planner.RoleDifference &&
                planner.ArgumentRole == "compatible-planner", "query opponent 2 and actor 5 are separate roles");
            foreach (int command in new[] { 9, 38 })
            {
                var leader = Context(raw: 5);
                Assert(leader.MovementPlayer == 5 && !leader.RoleDifference &&
                    leader.ArgumentRole == "compatible-leader-control", "leader query for building command " + command);
            }
            Assert(Context(raw: 0).MovementPlayer == 5 && Context(raw: 0).ArgumentRole == "explicit-zero",
                "zero original query retains independently verified actor");
            Assert(Context(raw: 5, plan: 5).ArgumentRole == "compatible-planner-or-leader",
                "overlapping roles never invent a caller");
            Assert(Context(raw: 1, plan: 0).MovementPlayer == 5 &&
                Context(raw: 1, plan: 0).EffectivePlanningPlayer == 1, "non-editor fallback player one");
            Assert(Context(raw: 1, plan: 0, editor: true).MovementPlayer == -1,
                "editor does not receive Vanilla's normal-game fallback");
            Assert(Context(raw: 3).Failure == "unexplained-search-player", "unexplained raw query stays open");
            Assert(Context(snapshotGlobal: 101).Failure == "tribe-identity-mismatch", "reused tribe slot stays open");
            Assert(Context(snapshotGlobal: 0).Failure == "snapshot-untracked", "unpublished snapshot stays open");
            Assert(Context(liveOwner: 6).Failure == "tribe-owner-mismatch", "stale ownership stays open");
            Assert(Context(leader: 0).Failure == "leader-identity-unverified" &&
                Context(leaderGlobal: 0).Failure == "leader-identity-unverified", "missing leader identity stays open");
            Assert(Context(control: 6).Failure == "leader-control-mismatch", "control conflict cannot substitute tribe owner");
            Assert(BuildingSearchPlayerContext.NativeControlWord(5, 1) == 261 &&
                Context(control: BuildingSearchPlayerContext.NativeControlWord(5, 1)).MovementPlayer == -1,
                "native control read includes adjacent high byte");
            Assert(BuildingSearchPlayerContext.NativeControlWord(5, 128) == -32763 &&
                Context(control: -32763).Failure == "invalid-leader-control-word", "native word sign extension");
            foreach (int raw in new[] { -1, 9, 261, int.MaxValue })
                Assert(Context(raw: raw, plan: raw).MovementPlayer == -1, "invalid planner values never authorize a role");
            string description = GateDiagnosticClassification.BuildingContextResult(planner);
            Assert(description.Contains("caller=unobserved") && description.Contains("usedPlayer=5") &&
                description.Contains("rawSearchArgument=2"), "diagnostics report values without caller attribution");
            var aggregate = new AiGateDecisionAggregate();
            for (int i = 0; i < 100; i++)
                for (int repeat = 0; repeat < 10; repeat++)
                    aggregate.Record(5, 0, "building-search-context", description,
                        i, 7, 0, 0);
            var rows = aggregate.Drain(); long total = 0;
            foreach (var row in rows) { total += row.Count; Assert(row.Count == 10, "each context counted exactly"); }
            Assert(rows.Length == 100 && total == 1000, "more than 32 combinations survive aggregation");

            string source = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string building = ExtractMethodBody(source, "FilterBuilding");
            Assert(building.Contains("originalBuilding(manager, tribe, buildingId, count, targetPcl, player)") &&
                building.Contains("finally") && building.Contains("Complete(scope"),
                "original sixth argument is unchanged and scope exits on exceptions");
            Assert(source.Contains("scope.PreviousMask") && source.Contains("scope.PreviousTouched") &&
                source.Contains("Snapshot.Readers--"), "nested masks, counters and snapshot reader ownership restore");
            Assert(source.Contains("globals[tribeId] = tribes[tribeId].r_GlobalId") &&
                source.Contains("tribePlayers = TribePlayerSnapshot.Empty"), "identity snapshot replaces across maps");
            string shared = File.ReadAllText(Path.Combine("..", "..", "BugfixesAndQoL", "src", "FriendlyMoatMovementRuntime.cs"));
            string sharedBuilding = ExtractMethodBody(shared, "ObserveBuildingApproachBuilder");
            Assert(sharedBuilding.Contains("ResolveEnemyGateBuildingPlayer(movementClass, tribeId)") &&
                sharedBuilding.Contains("sourceRegion, movementClass") && sharedBuilding.Contains("finally"),
                "mainmod preserves native arguments and closes shared building scope");
        }

        private static void DirectCursorCallsiteContractIsExact()
        {
            const ulong library = 0x180000000UL;
            const ulong wrapper = 0x7FFF12345678UL;
            byte[] original = EnemyGatePathfindingNativeDefinition.GetDirectCursorSearchBlockBytes();
            byte[] emitted = DirectCursorCallAdapterEmitter.AssembleAndValidate(
                original,
                library + (ulong)EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva,
                wrapper,
                library + 0x02200000UL);
            Assert(emitted.Length > 0, "direct cursor call adapter assembles and disassembles");

            byte[] probeBytes = new byte[64];
            for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
            Array.Copy(original, probeBytes, original.Length);
            IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
            try
            {
                Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)probeMemory.ToInt64()),
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength))
                    Assert(probe.DisplacedByteCount ==
                            EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength,
                        "installed RedBird decodes the exact 29-byte direct cursor block");
            }
            finally { Marshal.FreeHGlobal(probeMemory); }

            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string wrapperBody = ExtractMethodBody(runtime, "FilterDirectCursorSearch");
            Assert(wrapperBody.IndexOf("originalDirectTileSearch", StringComparison.Ordinal) >= 0 &&
                    wrapperBody.IndexOf("Enter(player)", StringComparison.Ordinal) >= 0 &&
                    wrapperBody.IndexOf("Complete(scope", StringComparison.Ordinal) >= 0,
                "direct cursor wrapper scopes exactly the original DB650 call");
            Assert(wrapperBody.IndexOf("new ", StringComparison.Ordinal) < 0 &&
                    wrapperBody.IndexOf("GetPathComponentGrid", StringComparison.Ordinal) < 0,
                "direct cursor wrapper performs no allocation or PCL work");
        }

        private static void NormalCursorPreviewContractIsExact()
        {
            const ulong imageBase = 0x180000000UL;
            byte[] original = EnemyGatePathfindingNativeDefinition.GetCursorPclDecisionBytes();
            Assert(original.Length == EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength,
                "normal cursor decision has the exact 14-byte baseline block");

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(original));
            decoder.IP = imageBase +
                (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva;
            Instruction call = decoder.Decode();
            Instruction test = decoder.Decode();
            Instruction lea = decoder.Decode();
            Assert(call.Mnemonic == Mnemonic.Call &&
                    call.NearBranchTarget == imageBase + 0xE2610UL,
                "cursor decision calls the confirmed E2610 PCL reachability function");
            Assert(test.Mnemonic == Mnemonic.Test && test.Op0Register == Register.EAX &&
                    test.Op1Register == Register.EAX,
                "cursor decision tests E2610's EAX result");
            Assert(lea.Mnemonic == Mnemonic.Lea &&
                    decoder.IP == imageBase +
                        (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionReturnRva,
                "cursor decision displacement ends exactly at 8F1CD");

            byte[] probeBytes = new byte[64];
            for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
            Array.Copy(original, probeBytes, original.Length);
            IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
            try
            {
                Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)probeMemory.ToInt64()),
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength))
                    Assert(probe.DisplacedByteCount ==
                            EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength,
                        "installed RedBird decodes exactly the 14-byte cursor decision block");
            }
            finally { Marshal.FreeHGlobal(probeMemory); }

            const ulong wrapper = 0x7FFF76543210UL;
            byte[] emitted = CursorPclCallAdapterEmitter.AssembleAndValidate(
                original, imageBase +
                    (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                wrapper, imageBase + 0x02300000UL);
            Assert(emitted.Length > original.Length,
                "cursor PCL call adapter assembles and disassembles");

            var emittedDecoder = Decoder.Create(64, new ByteArrayCodeReader(emitted));
            emittedDecoder.IP = imageBase + 0x02300000UL;
            int calls = 0, tests = 0, leas = 0, stackDelta = 0;
            int modeCopies = 0, unitCopies = 0;
            while (emittedDecoder.IP < imageBase + 0x02300000UL + (ulong)emitted.Length)
            {
                Instruction instruction = emittedDecoder.Decode();
                Assert(instruction.Code != Code.INVALID,
                    "cursor PCL adapter disassembles without invalid opcodes");
                if (instruction.Mnemonic == Mnemonic.Call) calls++;
                if (instruction.Mnemonic == Mnemonic.Test &&
                    instruction.Op0Register == Register.EAX) tests++;
                if (instruction.Mnemonic == Mnemonic.Lea &&
                    instruction.Op0Register == Register.RDI) leas++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.RSP)
                    stackDelta += unchecked((int)instruction.Immediate32);
                if (instruction.Mnemonic == Mnemonic.Add &&
                    instruction.Op0Register == Register.RSP)
                    stackDelta -= unchecked((int)instruction.Immediate32);
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x20 &&
                    instruction.Op1Register == Register.R10D) modeCopies++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x28 &&
                    instruction.Op1Register == Register.R14D) unitCopies++;
            }
            Assert(calls == 1 && tests == 1 && leas == 1 && stackDelta == 0 &&
                    modeCopies == 1 && unitCopies == 1,
                "cursor PCL adapter has one wrapper call, balanced stack, both stack arguments, and one TEST/LEA replay");
            Assert(lea.IPRelativeMemoryAddress == imageBase +
                    (ulong)EnemyGatePathfindingNativeDefinition.NativeDirectionGridRva &&
                    lea.IPRelativeMemoryAddress != 1UL,
                "RedBird post-displacement RDI is the global grid base, not a small source PCL");

            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string callback = ExtractMethodBody(runtime, "FilterCursorPclDecision");
            string deferred = ExtractMethodBody(runtime, "TryValidateCursorPreview");
            Assert(runtime.IndexOf("transaction.AddInline(cursorPclDecisionHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("FilterNormalCursorPclDecision",
                        StringComparison.Ordinal) < 0,
                "cursor decision uses the ABI adapter and removes the stale post-LEA context callback");
            Assert(runtime.IndexOf("!cursorPclDecisionHook.Success", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorPclDecisionHook.Hook.DisplacedByteCount",
                        StringComparison.Ordinal) >= 0,
                "cursor decision hook participates in the atomic committed-span contract");
            Assert(callback.IndexOf("originalPclReachability", StringComparison.Ordinal) >= 0 &&
                    callback.IndexOf("targetPcl != sourcePcl", StringComparison.Ordinal) >= 0 &&
                    callback.IndexOf("ApplyCursorPreviewResult", StringComparison.Ordinal) >= 0,
                "wrapper uses the real ABI PCL arguments and preserves Vanilla unless a cache blocks it");
            Assert(callback.IndexOf("if (vanillaResult > 0)", StringComparison.Ordinal) >= 0 &&
                    callback.IndexOf("else if (vanillaResult > 0)", StringComparison.Ordinal) < 0 &&
                    callback.IndexOf("cursorDifferentPclEligible", StringComparison.Ordinal) >= 0,
                "positive cursor decisions share immediate tile validation");
            foreach (string forbidden in new[]
            {
                "GameUnitManagerAPI", "originalDirectTileSearch", "lock (", "new ",
                "DebugLogHelper", "GetSelectedChimps", "registers->"
            })
                Assert(callback.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "cursor callback hot path excludes " + forbidden);
            Assert(CountOccurrences(deferred, "TryGetUnitById") == 1 &&
                    CountOccurrences(deferred, "originalDirectTileSearch") == 2 &&
                    deferred.IndexOf("Enter(player, true)", StringComparison.Ordinal) <
                        deferred.IndexOf("Enter(player);", StringComparison.Ordinal) &&
                    deferred.IndexOf("GetSelectedChimps", StringComparison.Ordinal) < 0 &&
                    deferred.IndexOf("for (", StringComparison.Ordinal) < 0 &&
                    deferred.IndexOf("foreach", StringComparison.Ordinal) < 0,
                "immediate preview validates one unit with reference-first and filtered-last DB650 searches");
            Assert(runtime.IndexOf("Stopwatch.Frequency / 5L", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorCacheTtlMs=200", StringComparison.Ordinal) >= 0 &&
                    callback.Contains("TryValidateCursorPreview") &&
                    !runtime.Contains("ProcessCursorPreview"),
                "cursor cache has a 200ms TTL without deferred/global target throttling");
        }

        private static void CursorCacheIsExactAndBounded()
        {
            var cache = new CursorPreviewCache(200);
            var key = new CursorPreviewCache.Key(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
            cache.Put(key, 1000, false);
            bool allowed;
            Assert(cache.TryGet(key, 1200, out allowed) && !allowed, "exact blocked target is reused through TTL");
            Assert(!cache.TryGet(key, 1201, out allowed) && allowed, "cache reads never extend TTL");
            cache.Put(key, 1000, false);
            Assert(!cache.TryGet(key, 999, out allowed), "backwards clock invalidates cache");
            cache.Put(key, 1000, false);
            for (int field = 0; field < 12; field++)
            {
                int[] values = { 1,2,3,4,5,6,7,8,9,10,11,12 };
                values[field]++;
                var changed = new CursorPreviewCache.Key(values[0],values[1],values[2],values[3],
                    values[4],values[5],values[6],values[7],values[8],values[9],values[10],(ulong)values[11]);
                Assert(!cache.TryGet(changed, 1000, out allowed), "cache field " + field + " is exact");
            }
            cache.Clear();
            for (int target = 0; target < 100; target++)
            {
                var current = new CursorPreviewCache.Key(1,2,3,4,5,target,7,8,9,10,11,12);
                if (!cache.TryGet(current, 1000, out allowed)) cache.Put(current, 1000, target % 2 == 0);
                Assert(cache.TryGet(current, 1000, out allowed) && allowed == (target % 2 == 0),
                    "eviction does not omit target " + target);
            }
            cache.Clear();
            Assert(!cache.TryGet(key, 1000, out allowed), "map reset clears approvals and denials");
        }

        private static void CursorPreviewDecisionIsCausalAndStable()
        {
            Assert(!EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(0, 0, 12),
                "reference no-route remains open even when the filtered search rejects edges");
            Assert(EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(1, 0, 1),
                "only a successful reference and failed filtered search with rejected edges blocks");
            Assert(!EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(1, 0, 0),
                "an unexplained filtered failure remains open");
            Assert(!EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(1, 1, 12),
                "a successful filtered detour remains green despite rejected direct edges");

            Assert(EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL),
                "an identical cursor key reuses its stable decision");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 99, 280, 281, 301, 301, 7, 7, 0x1234UL),
                "a changed target in the same PCL invalidates a negative decision immediately");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1235UL),
                "a changed gate policy invalidates the cursor decision immediately");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    false, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL),
                "an unpublished cache never changes Vanilla's cursor decision");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 99, 280, 281, 300, 301, 8, 8, 0x1234UL),
                "changed source and target PCLs invalidate a cached cursor decision");

            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 99, 281, 281, 300, 301, 7, 7, 0x1234UL),
                "a moved unit invalidates a cached block");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 100, 280, 281, 300, 301, 7, 7, 0x1234UL),
                "reuse of a unit ID invalidates a cached block");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL,
                    3, 41, 99, 280, 281, 300, 301, 7, 7, 0x1234UL),
                "a different player invalidates a cached block");

            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, true, false) == 0,
                "a blocking Different-PCL cache can reject Vanilla for a gatehouse");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, false, false) == 7,
                "without a published cache Vanilla remains unchanged");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, true, true) == 7,
                "an allowing cache preserves Vanilla");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, true, false) == 0,
                "a blocking cache changes a positive result to zero");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    0, true, false) == 0,
                "a Vanilla failure remains byte-equivalent zero");
        }

        private static void GatehouseUsesBothOuterBoundaries()
        {
            string topology = File.ReadAllText(Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string gate = ExtractMethodBody(topology, "ClearGatehouseOuterDirections");
            Assert(gate.IndexOf("minY - 1, x, minY, 4", StringComparison.Ordinal) >= 0 &&
                    gate.IndexOf("maxY, x, maxY + 1, 4", StringComparison.Ordinal) >= 0,
                "vertical gatehouse blocks both outer entry/exit boundaries");
            Assert(gate.IndexOf("minX - 1, y, minX, y, 2", StringComparison.Ordinal) >= 0 &&
                    gate.IndexOf("maxX, y, maxX + 1, y, 2", StringComparison.Ordinal) >= 0,
                "horizontal gatehouse blocks both outer entry/exit boundaries");
            Assert(gate.IndexOf("Math.Min(info.EntryY, info.ExitY) != minY - 1",
                        StringComparison.Ordinal) >= 0 &&
                    gate.IndexOf("Math.Max(info.EntryY, info.ExitY) != maxY + 1",
                        StringComparison.Ordinal) >= 0,
                "logged 401/372 to 401/366 gate coordinates validate against 367..371 bounds");
            string bridge = ExtractMethodBody(topology, "ClearDrawbridgePassageDirections");
            Assert(bridge.IndexOf("DrawbridgeClosurePolicy.BlockCell", StringComparison.Ordinal) >= 0 &&
                    bridge.IndexOf("tile.ClosedBridgeCell", StringComparison.Ordinal) >= 0 &&
                    bridge.IndexOf("horizontalPassage", StringComparison.Ordinal) < 0,
                "drawbridge isolates native closure cells independently of gate axis");
            Assert(topology.IndexOf("entry-exit-outer", StringComparison.Ordinal) >= 0,
                "snapshot diagnostics identify the gatehouse barrier contract");
        }

        private static void NativeSnapshotPoolAcquisitionIsSynchronized()
        {
            string source = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string enter = ExtractMethodBody(source, "Enter");
            string complete = ExtractMethodBody(source, "Complete");
            Assert(source.IndexOf("NativeSnapshotPoolSize = 4", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Marshal.FreeHGlobal", StringComparison.Ordinal) < 0,
                "four static mask slots are never freed while the runtime is published");
            Assert(enter.IndexOf("lock (maskGate)", StringComparison.Ordinal) >= 0 &&
                    complete.IndexOf("lock (maskGate)", StringComparison.Ordinal) >= 0,
                "snapshot acquisition and release share one synchronization gate");
            Assert(source.IndexOf("SlotDepthOffset", StringComparison.Ordinal) >= 0,
                "nested zero-mask scopes use explicit depth rather than pointer nullness");
            Assert(source.IndexOf("CompareExchange(ref samplePublished[index], 1, 0)",
                        StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Volatile.Write(ref samplePublished[index], 2)",
                        StringComparison.Ordinal) >= 0,
                "scope samples publish only after all primitive fields are written");
        }

        private static void PassageAxisEvidenceIsDeterministic()
        {
            AssertAxis(true, 10, 10, 14, 10, 8, 8, 12, 12,
                false, 0, 0, 0, 0, true, PassageAxisSource.EntryExitCoordinates,
                "entry/exit coordinates resolve a horizontal passage even without an exit tile id");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 20, 25, 24, 29, false, PassageAxisSource.LinkedDrawbridge,
                "south drawbridge resolves a vertical gate passage");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 20, 15, 24, 19, false, PassageAxisSource.LinkedDrawbridge,
                "north drawbridge resolves a vertical gate passage");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 25, 20, 29, 24, true, PassageAxisSource.LinkedDrawbridge,
                "east drawbridge resolves a horizontal gate passage");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 15, 20, 19, 24, true, PassageAxisSource.LinkedDrawbridge,
                "west drawbridge resolves a horizontal gate passage");
            AssertAxis(false, 0, 0, 0, 0, 30, 30, 32, 36,
                false, 0, 0, 0, 0, true, PassageAxisSource.ElongatedFootprint,
                "elongated fallback resolves perpendicular passage axis");
            bool resolved = PassageAxisResolver.TryResolve(false, 0, 0, 0, 0,
                325, 483, 329, 487, false, 0, 0, 0, 0,
                out _, out PassageAxisSource ambiguous);
            Assert(!resolved && ambiguous == PassageAxisSource.None,
                "square gate without coordinate or bridge evidence remains fail-open");
        }

        private static void AssertAxis(bool coordinatesAvailable,
            int entryX, int entryY, int exitX, int exitY,
            int gateMinX, int gateMinY, int gateMaxX, int gateMaxY,
            bool bridgeAvailable, int bridgeMinX, int bridgeMinY, int bridgeMaxX, int bridgeMaxY,
            bool expectedHorizontal, PassageAxisSource expectedSource, string message)
        {
            bool resolved = PassageAxisResolver.TryResolve(coordinatesAvailable,
                entryX, entryY, exitX, exitY, gateMinX, gateMinY, gateMaxX, gateMaxY,
                bridgeAvailable, bridgeMinX, bridgeMinY, bridgeMaxX, bridgeMaxY,
                out bool horizontal, out PassageAxisSource source);
            Assert(resolved && horizontal == expectedHorizontal && source == expectedSource, message);
        }

        private static Instruction DecodeFirst(byte[] bytes, ulong ip)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            decoder.Decode(out Instruction instruction);
            return instruction;
        }

        private static Instruction DecodeAt(byte[] bytes, ulong ip, int requestedIndex)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            Instruction instruction = default;
            for (int index = 0; index <= requestedIndex; index++)
                decoder.Decode(out instruction);
            return instruction;
        }

        private static Register ToRegister32(Register register)
        {
            switch (register)
            {
                case Register.RAX: return Register.EAX;
                case Register.R8: return Register.R8D;
                case Register.R9: return Register.R9D;
                case Register.R10: return Register.R10D;
                case Register.R11: return Register.R11D;
                case Register.R12: return Register.R12D;
                case Register.RDI: return Register.EDI;
                default: throw new ArgumentOutOfRangeException(nameof(register));
            }
        }

        private static bool SameMemoryInstruction(Instruction left, Instruction right) =>
            left.Code == right.Code && left.MemoryBase == right.MemoryBase &&
            left.MemoryIndex == right.MemoryIndex && left.MemoryIndexScale == right.MemoryIndexScale &&
            left.MemoryDisplacement64 == right.MemoryDisplacement64;

        private static int CountOccurrences(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }
            return count;
        }

        private static void WriteBytes(byte[] destination, int offset, string hexadecimal)
        {
            string[] bytes = hexadecimal.Split(' ');
            for (int index = 0; index < bytes.Length; index++)
                destination[offset + index] = Convert.ToByte(bytes[index], 16);
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int name = source.LastIndexOf(methodName + "(", StringComparison.Ordinal);
            if (name < 0)
                throw new InvalidOperationException("Method not found for hot-path audit: " + methodName);
            int open = source.IndexOf('{', name);
            if (open < 0)
                throw new InvalidOperationException("Method body not found for hot-path audit: " + methodName);
            int depth = 0;
            for (int index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(open, index - open + 1);
            }
            throw new InvalidOperationException("Unterminated method body: " + methodName);
        }

        private static void AssertTopology(TopologyDiagnosticDisposition expected,
            bool bridgeActive, bool bridgeGlobal, bool gateId, bool gateActive,
            bool gateGlobal, bool entry, bool entryMatches, bool doors,
            bool reread, bool footprint)
        {
            Assert(EnemyGatePathfindingPolicy.ClassifyTopologyCandidate(
                    bridgeActive, bridgeGlobal, gateId, gateActive, gateGlobal,
                    entry, entryMatches, doors, reread, footprint) == expected,
                "topology disposition " + expected);
        }

        private static void AssertDecision(
            CapturedGateFilterDecision expected,
            int queryPlayer,
            int capturedByPlayer,
            int ownerPlayer = 3)
        {
            Assert(
                EnemyGatePathfindingPolicy.EvaluateGateAccess(
                    queryPlayer,
                    ownerPlayer,
                    capturedByPlayer,
                    ValidPlayer,
                    Allied) == expected,
                expected + " for query=" + queryPlayer + ", owner=" + ownerPlayer +
                ", captured=" + capturedByPlayer);
        }

        private static bool ValidPlayer(int player) => player >= 1 && player <= 8;

        private static bool Allied(int first, int second) =>
            first == second || (first <= 2 && second <= 2);

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
