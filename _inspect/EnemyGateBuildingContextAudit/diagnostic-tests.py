from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
MOD = ROOT / 'Testmods/EnemyGatePathfindingTest'
def edit(relative, transform):
    path=MOD/relative
    old=path.read_text(encoding='utf-8-sig'); new=transform(old)
    assert new!=old,relative
    expected=new.replace('\r\n','\n').replace('\n','\r\n').encode()
    path.write_bytes(expected); assert path.read_bytes()==expected
def rep(s,a,b):
    assert a in s,a
    return s.replace(a,b)
def scope(s):
    s=rep(s,'            long aiTacticalExceptions)','            long aiTacticalExceptions, long cursorCommandQueries)')
    s=rep(s,'            Installed = installed;', '            CursorCommandQueries = cursorCommandQueries;\n            Installed = installed;')
    s=rep(s,'        internal long CursorCommandEdges { get; }','        internal long CursorCommandEdges { get; }\n        internal long CursorCommandQueries { get; }')
    s=rep(s,'        private long cursorCommandEdges,','        private long cursorCommandQueries, cursorCommandEdges,')
    s=rep(s,'                Read(ref aiTacticalExceptions));','                Read(ref aiTacticalExceptions), Read(ref cursorCommandQueries));')
    s=rep(s,'            Reset(ref aiQueries);','            Reset(ref cursorCommandQueries);\n            Reset(ref aiQueries);')
    s=rep(s,'        private void FilterCursor(IntPtr manager, int tribe, int x, int y, int context, int flags)\n        {','        private void FilterCursor(IntPtr manager, int tribe, int x, int y, int context, int flags)\n        {\n            Interlocked.Increment(ref cursorCommandQueries);')
    return s
edit('src/SamePclGateRouteRuntime.cs',scope)
edit('src/EnemyGatePathfindingRuntime.cs',lambda s:rep(s,'                $"candidateQueries={same.CandidateQueries}," +','                $"candidateQueries={same.CandidateQueries},cursorCommandQueries={same.CursorCommandQueries}," +'))

def tests(s):
    s=s.replace('ScriptExtender280AndFixesContractsArePinned','ScriptExtender2120AndFixesContractsArePinned')
    s=s.replace('"2.8.0"','"2.12.0"').replace('"v2.8.0"','"v2.12.0"').replace('new Version(2, 8, 0, 0)','new Version(2, 12, 0, 0)').replace('5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33','f8d51730fcb54b25af43d3c9348d57db058e077f').replace('Script Extender 2.8.0 provenance','Script Extender 2.12.0 provenance')
    s=rep(s,'correlation.Contains("totals.Drain()")','correlation.Contains("totals.Drain(out var definitions)")')
    s=rep(s,'                ScriptExtender2120AndFixesContractsArePinned();','''                ScriptExtender2120AndFixesContractsArePinned();
                SearchDiagnosticIdentitySurvivesPublication();
                GateStateDefinitionsReconstructEveryObservation();
                PartialPathfindingCoverageIsNotMutation();''')
    anchor='        private static void ScriptExtenderPathfindingGlobalsAreComparedReadOnly()'
    new='''        private static void SearchDiagnosticIdentitySurvivesPublication()
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

'''
    return rep(s,anchor,new+anchor)
edit('tests/Program.cs',tests)
