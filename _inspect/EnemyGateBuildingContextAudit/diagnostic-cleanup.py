from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MOD = ROOT / 'Testmods/EnemyGatePathfindingTest'

def edit(relative, operation):
    path = MOD / relative
    old = path.read_text(encoding='utf-8-sig')
    new = operation(old)
    assert new != old, relative
    expected = new.replace('\r\n', '\n').replace('\r', '\n').replace('\n', '\r\n').encode('utf-8')
    path.write_bytes(expected)
    assert path.read_bytes() == expected
    assert b'\n' not in expected.replace(b'\r\n', b'')
    print(relative)

def replace(text, old, new):
    assert old in text, old[:100]
    return text.replace(old, new)

def aggregate(s):
    s = replace(s, '    // Every observation', '''    internal enum QueryKind
    {
        HumanBuilder, AiBuilder, Attack, BuildingApproach,
        AlternateBuildingApproach, CandidateSearch, CursorCommand, DirectCursor,
        CursorPreview, AiTacticalTarget
    }

    // Frozen at entry; later player-kind publications do not reclassify this call.
    internal readonly struct SearchDiagnosticContext
    {
        internal SearchDiagnosticContext(QueryKind kind) { Kind = kind; }
        internal QueryKind Kind { get; }
        internal bool IsAiBuilder => Kind == QueryKind.AiBuilder;
    }

    // Every observation''')
    s = replace(s, '        private long observations;', '''        private long observations, epoch;
        private readonly Dictionary<StateKey, GateStateDefinition> states =
            new Dictionary<StateKey, GateStateDefinition>();
        private readonly List<GateStateDefinition> pendingStates = new List<GateStateDefinition>();

        internal void RecordGateState(int player, int gateId, string stage, string state,
            int command, int tribeId, int target1, int target2, string detail)
        {
            lock (gate)
            {
                var key = new StateKey(player, gateId, state, detail);
                if (!states.TryGetValue(key, out GateStateDefinition definition))
                {
                    definition = new GateStateDefinition(epoch, states.Count + 1,
                        player, gateId, state, detail);
                    states.Add(key, definition);
                    pendingStates.Add(definition);
                }
                Record(player, gateId, stage, "gateState=" + definition.Reference,
                    command, tribeId, target1, target2, "state=" + definition.Reference);
            }
        }

        internal readonly struct GateStateDefinition
        {
            internal readonly long Epoch;
            internal readonly int Id, Player, GateId;
            internal readonly string State, Detail;
            internal GateStateDefinition(long epoch, int id, int player, int gateId,
                string state, string detail)
            { Epoch = epoch; Id = id; Player = player; GateId = gateId;
              State = state; Detail = detail; }
            internal string Reference => Epoch + "/" + Id;
            public override string ToString() => "epoch=" + Epoch + ",id=" + Id +
                ",player=" + Player + ",gate=" + GateId + "," + State + ",detail=" + Detail;
        }

        private readonly struct StateKey : IEquatable<StateKey>
        {
            private readonly int player, gateId;
            private readonly string state, detail;
            internal StateKey(int player, int gateId, string state, string detail)
            { this.player = player; this.gateId = gateId; this.state = state; this.detail = detail; }
            public bool Equals(StateKey other) => player == other.player && gateId == other.gateId &&
                state == other.state && detail == other.detail;
            public override bool Equals(object value) => value is StateKey other && Equals(other);
            public override int GetHashCode() => unchecked(((player * 397 ^ gateId) * 397 ^
                (state?.GetHashCode() ?? 0)) * 397 ^ (detail?.GetHashCode() ?? 0));
        }''')
    s = replace(s, '        internal RowSnapshot[] Drain()\n        {', '''        internal RowSnapshot[] Drain() => Drain(out _);

        // Definitions and referencing rows are captured under the same lock.
        internal RowSnapshot[] Drain(out GateStateDefinition[] definitions)
        {''')
    s = replace(s, '                var result = new RowSnapshot[rows.Count];', '''                definitions = pendingStates.ToArray();
                pendingStates.Clear();
                var result = new RowSnapshot[rows.Count];''')
    s = replace(s, '                lastTargets.Clear();', '''                lastTargets.Clear();
                states.Clear();
                pendingStates.Clear();
                epoch++;''')
    return s

def scope(s):
    start = s.index('        private enum QueryKind')
    end = s.index('        [UnmanagedFunctionPointer', start)
    s = s[:start] + s[end:]
    s = replace(s, '            QueryScope scope = Enter(playerId);', '            CountSharedSearch(kind, localKind);\n            QueryScope scope = Enter(playerId, diagnosticKind: localKind);')
    # CountSharedSearch owns the shared AI counter too.
    s = replace(s, '            if (localKind == QueryKind.AiBuilder) Interlocked.Increment(ref aiQueries);\n', '')
    s = replace(s, '''            QueryKind localKind = SharedKind(kind, -1);
            if (kind == EnemyGateSearchKind.Builder)
                localKind = playerKinds.IsAi(query.PlayerId)
                    ? QueryKind.AiBuilder : QueryKind.HumanBuilder;''', '            QueryKind localKind = query.Diagnostic.Kind;')
    s = replace(s, '                    query.PlayerId, completed, success, touched, query.Snapshot.Fingerprint);', '                    query.PlayerId, query.Diagnostic, completed, success, touched, query.Snapshot.Fingerprint);')
    s = replace(s, '        private QueryKind SharedKind', '''        private void CountSharedSearch(EnemyGateSearchKind kind, QueryKind localKind)
        {
            switch (kind)
            {
                case EnemyGateSearchKind.Builder:
                    if (localKind == QueryKind.AiBuilder) Interlocked.Increment(ref aiQueries);
                    break;
                case EnemyGateSearchKind.Attack: Interlocked.Increment(ref attackQueries); break;
                case EnemyGateSearchKind.BuildingApproach:
                    Interlocked.Increment(ref buildingApproachQueries); break;
                case EnemyGateSearchKind.BuildingConsumer:
                    Interlocked.Increment(ref buildingConsumerQueries); break;
                case EnemyGateSearchKind.CursorCommand:
                    Interlocked.Increment(ref cursorCommandQueries); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private QueryKind SharedKind''')
    s = s.replace('            QueryScope scope = Enter(player); int result = 0; bool completed = false;', '            QueryScope scope = Enter(player, diagnosticKind: kind); int result = 0; bool completed = false;', 1)
    s = replace(s, 'attackOrderDiagnostics?.ObserveBuilder(player, completed, result > 0,', 'attackOrderDiagnostics?.ObserveBuilder(player, scope.Diagnostic, completed, result > 0,')
    s = replace(s, '        private QueryScope Enter(int player, bool unmasked = false)', '        private QueryScope Enter(int player, bool unmasked = false,\n            QueryKind diagnosticKind = QueryKind.HumanBuilder)')
    s = s.replace('new QueryScope(snapshot, null, IntPtr.Zero, 0, player)', 'new QueryScope(snapshot, null, IntPtr.Zero, 0, player, diagnosticKind)')
    s = replace(s, 'new QueryScope(snapshot, slot, previous, previousTouched, player)', 'new QueryScope(snapshot, slot, previous, previousTouched, player, diagnosticKind)')
    s = replace(s, '                long previousTouched, int playerId)', '                long previousTouched, int playerId, QueryKind diagnosticKind)')
    s = replace(s, '              PreviousTouched = previousTouched; PlayerId = playerId; }', '              PreviousTouched = previousTouched; PlayerId = playerId;\n              Diagnostic = new SearchDiagnosticContext(diagnosticKind); }')
    s = replace(s, '            internal int PlayerId { get; }', '            internal int PlayerId { get; }\n            internal SearchDiagnosticContext Diagnostic { get; }')
    return s

def correlation(s):
    s = replace(s, 'internal void ObserveBuilder(int player, bool completed, bool success,', 'internal void ObserveBuilder(int player, SearchDiagnosticContext context, bool completed, bool success,')
    start = s.index('        internal void ObserveBuilder(')
    end = s.index('        internal ', start + 25)
    block = s[start:end]
    block = replace(block, 'if (!IsAi(player)) return;', 'if (!context.IsAiBuilder) return;')
    s = s[:start] + block + s[end:]
    s = replace(s, '                    totals.Record(player, observation.GateId, "gate-live-" + stage,', '                    totals.RecordGateState(player, observation.GateId, "gate-live-" + stage,')
    s = replace(s, '            AiGateDecisionAggregate.RowSnapshot[] rows = totals.Drain();', '''            AiGateDecisionAggregate.RowSnapshot[] rows = totals.Drain(out var definitions);
            foreach (var definition in definitions)
                Shared.DebugLogHelper.LogInfo(log, "Enemy-gate state: " + definition + ".");''')
    s = replace(s, '",activeCombinations=" + rows.Length +', '",activeCombinations=" + rows.Length +\n                ",newStateDefinitions=" + definitions.Length +')
    return s

def globals(s):
    s = replace(s, '        internal bool MatchesCanonical =>', '''        internal bool ComparedValuesMatch => ProfileMismatches == 0 &&
            PermissionMismatches == 0 && InvalidValues == 0;
        internal int ComparedProfiles => Math.Min(ProfileLength, PathfindingGlobalsBaseline.UnitTypeCount);
        internal int ComparedPermissions => Math.Min(PermissionLength, PathfindingGlobalsBaseline.PermissionCount);
        internal string MissingCoverage => "profiles=" + ComparedProfiles + ".." +
            (PathfindingGlobalsBaseline.UnitTypeCount - 1) + ";permissions=" +
            ComparedPermissions + ".." + (PathfindingGlobalsBaseline.PermissionCount - 1);

        internal bool MatchesCanonical =>''')
    return s

def runtime(s):
    s = replace(s, '''                Shared.DebugLogHelper.LogWarning(log,
                    "Script Extender pathfinding globals differ from the canonical " +''', '''                if (comparison.ComparedValuesMatch && !comparison.HasExpectedLengths)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        "Script Extender pathfinding-global coverage is incomplete or has an " +
                        "unexpected API extent; all compared values match the canonical FBCB9319 " +
                        $"tables. This is not evidence of table mutation: {counts}, " +
                        $"missingNativeCoverage={comparison.MissingCoverage}, " +
                        "nativeRowStride=90; policy unchanged.");
                    return;
                }

                Shared.DebugLogHelper.LogWarning(log,
                    "Compared Script Extender pathfinding-global values differ from the canonical " +''')
    s = replace(s, '                .Append(", classAllowed=");', '                .Append(", comparedClassAllowed=");')
    return s

def provenance(s):
    s = s.replace('"2.8.0"', '"2.12.0"').replace('"v2.8.0"', '"v2.12.0"')
    s = replace(s, '5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33', 'f8d51730fcb54b25af43d3c9348d57db058e077f')
    return s

edit('src/AiGateDecisionAggregate.cs', aggregate)
edit('src/SamePclGateRouteRuntime.cs', scope)
edit('src/AttackOrderCorrelationDiagnostics.cs', correlation)
edit('src/PathfindingGlobalsBaseline.cs', globals)
edit('src/EnemyGatePathfindingRuntime.cs', runtime)
edit('src/EnemyGatePathfindingNativeDefinition.cs', provenance)
edit('src/EnemyGatePathfindingTestPlugin.cs', lambda s: replace(s, 'new Version(2, 8, 0, 0)', 'new Version(2, 12, 0, 0)'))
