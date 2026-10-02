"""Generate targeted edits; PowerShell applies them with .NET and CRLF verification."""
import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
edits = {}
def read(path):
    if path not in edits: edits[path] = (root/path).read_text(encoding='utf-8-sig')
    return edits[path]
def replace(path, old, new, count=1):
    text = read(path)
    assert text.count(old) == count, (path, old[:100], text.count(old), count)
    edits[path] = text.replace(old,new)

api='APIShared/src/EnemyGatePathPolicyBridge.cs'
tests='_inspect/APISharedTests/Program.cs'
replace(tests,'"APIShared.IEnemyGateAssassinObserver",','"APIShared.IEnemyGateAssassinObserver",\n                "APIShared.IEnemyGateRoutePolicyProvider",\n                "APIShared.IEnemyGateRoutePolicySnapshot",')

test='Testmods/EnemyGatePathfindingTest/'
rt=test+'src/SamePclGateRouteRuntime.cs'
replace(rt,'IEnemyGateRegionPairObserver, IEnemyGateAssassinObserver','IEnemyGateRegionPairObserver, IEnemyGateAssassinObserver, IEnemyGateRoutePolicyProvider')
replace(rt,'private volatile RouteTilePolicySnapshot publishedPolicy = RouteTilePolicySnapshot.Empty;',
'''private volatile RouteTilePolicySnapshot publishedPolicy = RouteTilePolicySnapshot.Empty;
        private readonly GateRoutePolicySource routePolicySource = new GateRoutePolicySource();''')
replace(rt,'        bool IEnemyGatePathPolicy.IsDirectionAllowed',
'''        bool IEnemyGateRoutePolicyProvider.TryCaptureRoutePolicy(int playerId,
            out IEnemyGateRoutePolicySnapshot snapshot) =>
            routePolicySource.TryCaptureRoutePolicy(playerId, out snapshot);

        bool IEnemyGatePathPolicy.IsDirectionAllowed''')
replace(rt,'\n                    publishedPolicy = RouteTilePolicySnapshot.Empty;\n',
            '\n                    publishedPolicy = RouteTilePolicySnapshot.Empty;\n                    routePolicySource.Publish(RouteTilePolicySnapshot.Empty);\n')
replace(rt,'\n                publishedPolicy = RouteTilePolicySnapshot.Empty;\n',
            '\n                publishedPolicy = RouteTilePolicySnapshot.Empty;\n                routePolicySource.Publish(RouteTilePolicySnapshot.Empty);\n')
replace(rt,'publishedPolicy = policy;','publishedPolicy = policy;\n                    routePolicySource.Publish(policy);')
replace(rt,'            NativeMaskSnapshot snapshot;\n            IntPtr mask;\n            lock (maskGate)',
'''            NativeMaskSnapshot snapshot;
            GateRoutePolicySource.Publication routePublication;
            IntPtr mask;
            lock (maskGate)''',count=2)
# Both native Enter paths acquire the same publication, but only ordinary queries
# need a managed route context (tactical contexts never invoke the Assassin builder).
replace(rt,'                snapshot = currentMasks;\n                snapshot.Readers++;',
'''                snapshot = currentMasks;
                routePublication = routePolicySource.Current;
                snapshot.Readers++;''',count=2)
replace(rt,'new QueryScope(snapshot, null, IntPtr.Zero, 0, player, diagnosticKind)',
'''new QueryScope(snapshot, null, IntPtr.Zero, 0, player, diagnosticKind,
                    routePolicySource.Enter(player, unmasked, false, routePublication))''',count=2)
replace(rt,'return new QueryScope(snapshot, slot, previous, previousTouched, player, diagnosticKind);',
'''return new QueryScope(snapshot, slot, previous, previousTouched, player, diagnosticKind,
                routePolicySource.Enter(player, unmasked, true, routePublication));''')
replace(rt,'            lock (maskGate) scope.Snapshot.Readers--;\n            if (touched == 0)',
'''            lock (maskGate) scope.Snapshot.Readers--;
            routePolicySource.Leave(scope.RouteQuery);
            if (touched == 0)''')
replace(rt,'long previousTouched, int playerId, QueryKind diagnosticKind)',
            'long previousTouched, int playerId, QueryKind diagnosticKind, GateRoutePolicySource.Query routeQuery)')
replace(rt,'Diagnostic = new SearchDiagnosticContext(diagnosticKind); }',
            'Diagnostic = new SearchDiagnosticContext(diagnosticKind); RouteQuery = routeQuery; }')
replace(rt,'            internal SearchDiagnosticContext Diagnostic { get; }',
'''            internal SearchDiagnosticContext Diagnostic { get; }
            internal GateRoutePolicySource.Query RouteQuery { get; }''')

main='BugfixesAndQoL/src/AssassinPathfindingRuntime.cs'
replace(main,'                bool allowClimbing = command?.GetClimbingAllowed',
'''                if (!AssassinGateRoutePolicy.TryCapture(EnemyGatePathPolicyBridge.Current, playerId,
                    out IEnemyGateRoutePolicySnapshot gatePolicy))
                {
                    if (activeObservation != null) activeObservation.Outcome = "gate-context-fallback";
                    return vanillaResult;
                }
                bool allowClimbing = command?.GetClimbingAllowed''')
replace(main,'playerId, allowClimbing, allowWalkableReservedClimbEndpoints);',
            'playerId, allowClimbing, allowWalkableReservedClimbEndpoints, gatePolicy);')
replace(main,'                        command,\n                        out routeSummary);',
            '                        command,\n                        gatePolicy,\n                        out routeSummary);')
replace(main,'                if (!routeReady)\n                    return 0;',
'''                if (!AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                {
                    if (activeObservation != null) activeObservation.Outcome = "gate-snapshot-fallback";
                    return vanillaResult;
                }
                if (!routeReady)
                    return 0;''')
replace(main,'                bool published = CommitPreparedRoute(context, routeSummary.RouteLength);',
'''                // Validate before the first native stamp/distance write. A stale policy
                // must preserve the original native field, not publish a partial replacement.
                if (!ValidatePreparedGateRoute(gatePolicy, routeSummary.RouteLength) ||
                    !AssassinGateRoutePolicy.IsCurrent(gatePolicy))
                {
                    if (activeObservation != null) activeObservation.Outcome = "gate-publication-fallback";
                    return vanillaResult;
                }
                bool published = CommitPreparedRoute(context, routeSummary.RouteLength);''')
replace(main,'            AssassinCommandScope command,\n            out RouteSearchSummary routeSummary)',
            '            AssassinCommandScope command,\n            IEnemyGateRoutePolicySnapshot gatePolicy,\n            out RouteSearchSummary routeSummary)')
replace(main,'                allowWalkableReservedClimbEndpoints);\n            Touch(startNode',
            '                allowWalkableReservedClimbEndpoints, gatePolicy?.PlayerId ?? 0, gatePolicy);\n            Touch(startNode')
replace(main,'                    int movementTicks = (direction & 1) == 0',
'''                    if (!AssassinGateRoutePolicy.Allows(gatePolicy, currentTile, direction))
                        continue;
                    int movementTicks = (direction & 1) == 0''')
replace(main,'                key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints);',
            '                key.AllowClimbing, key.AllowWalkableReservedClimbEndpoints,\n                key.GatePolicy?.PlayerId ?? 0, key.GatePolicy);')
replace(main,'                bool cardinal = (direction & 1) == 0;\n                bool ordinaryEdge',
'''                if (!AssassinGateRoutePolicy.Allows(key.GatePolicy, currentTile, direction))
                    return false;
                bool cardinal = (direction & 1) == 0;
                bool ordinaryEdge''')
replace(main,'        private bool CommitPreparedRoute(IntPtr context, int routeLength)',
'''        private bool ValidatePreparedGateRoute(IEnemyGateRoutePolicySnapshot policy, int routeLength)
        {
            if (policy == null) return true;
            for (int index = routeLength - 1; index > 0; index--)
            {
                int from = route[index], to = route[index - 1];
                int direction = GetDirectionIndex(to % MapWidth - from % MapWidth,
                    to / MapWidth - from / MapWidth);
                int tile = GetTileId(from % MapWidth, from / MapWidth);
                if (direction < 0 || !AssassinGateRoutePolicy.Allows(policy, tile, direction)) return false;
            }
            return true;
        }

        private bool CommitPreparedRoute(IntPtr context, int routeLength)''')
replace(main,'                int candidatePlayer = candidate.r_ControllableForPlayerId;',
'''                int candidatePlayer = candidate.r_ControllableForPlayerId;
                // Preserve the historical mainmod resolver without a gate provider.
                // Gate access uses the full native control WORD, never its low byte alone.
                IEnemyGatePathPolicy gateProvider = EnemyGatePathPolicyBridge.Current;
                if (gateProvider != null && gateProvider.HasPublishedMask)
                    candidatePlayer = AssassinGateRoutePolicy.ReadControlPlayer(
                        candidate.r_ControllableForPlayerId, candidate.N00000569);''')
# Extend both route and suffix keys; no-policy values preserve old key equality.
replace(main,'                bool allowWalkableReservedClimbEndpoints)\n            {\n                StartX',
            '                bool allowWalkableReservedClimbEndpoints, IEnemyGateRoutePolicySnapshot gatePolicy)\n            {\n                GatePolicy = gatePolicy;\n                StartX')
replace(main,'                bool allowWalkableReservedClimbEndpoints)\n            {\n                TargetX',
            '                bool allowWalkableReservedClimbEndpoints, int policyPlayer, IEnemyGateRoutePolicySnapshot gatePolicy)\n            {\n                GatePolicy = gatePolicy;\n                PolicyPlayer = policyPlayer;\n                TargetX')
replace(main,'            public bool AllowWalkableReservedClimbEndpoints { get; }',
            '            public bool AllowWalkableReservedClimbEndpoints { get; }\n            public IEnemyGateRoutePolicySnapshot GatePolicy { get; }',count=2)
replace(main,'            public bool Equals(SuffixCacheKey other) =>',
            '            public int PolicyPlayer { get; }\n\n            public bool Equals(SuffixCacheKey other) =>')
replace(main,'AllowWalkableReservedClimbEndpoints == other.AllowWalkableReservedClimbEndpoints;',
            'AllowWalkableReservedClimbEndpoints == other.AllowWalkableReservedClimbEndpoints &&\n                ReferenceEquals(GatePolicy, other.GatePolicy);',count=2)
replace(main,'                TargetX == other.TargetX && TargetY == other.TargetY &&\n                SpeedDelay',
            '                PolicyPlayer == other.PolicyPlayer &&\n                TargetX == other.TargetX && TargetY == other.TargetY &&\n                SpeedDelay')
replace(main,'                    return hash * 397 ^ (AllowWalkableReservedClimbEndpoints ? 1 : 0);',
'''                    hash = hash * 397 ^ (AllowWalkableReservedClimbEndpoints ? 1 : 0);
                    return hash * 397 ^ AssassinGateRoutePolicy.IdentityHash(GatePolicy);''',count=2)
# Suffix player equality is essential; adding it to the hash is optional but avoids collisions.
replace(main,'                    int hash = TargetX;', '                    int hash = TargetX ^ PolicyPlayer;')

for project, entry, new in [
 ('BugfixesAndQoL/BugfixesAndQoL.csproj','src\\AssassinAStarPolicy.cs','src\\AssassinGateRoutePolicy.cs'),
 (test+'EnemyGatePathfindingTest.csproj','src\\RouteTilePolicySnapshot.cs','src\\GateRoutePolicySource.cs'),
 (test+'EnemyGatePathfindingTest.PolicyTests.csproj','src\\RouteTilePolicySnapshot.cs','src\\GateRoutePolicySource.cs')]:
 text=read(project)
 line=next(line for line in text.splitlines() if '<Compile Include="'+entry+'"' in line)
 replace(project,line,line+'\n    <Compile Include="'+new+'" />')
# Pure tests compile the passive API contract directly, avoiding a premature runtime build.
project=test+'EnemyGatePathfindingTest.PolicyTests.csproj'
replace(project,'    <Compile Include="tests\\Program.cs" />',
'''    <Compile Include="tests\\Program.cs" />
    <Compile Include="..\\..\\APIShared\\src\\EnemyGatePathPolicyBridge.cs"><Link>EnemyGatePathPolicyBridge.cs</Link></Compile>''')
project='BugfixesAndQoL/tests/AssassinPathfinding.Tests/AssassinPathfinding.Tests.csproj'
replace(project,'  </ItemGroup>',
'''    <Compile Include="..\\..\\src\\AssassinGateRoutePolicy.cs" Link="Runtime\\AssassinGateRoutePolicy.cs" />
    <Compile Include="..\\..\\..\\APIShared\\src\\EnemyGatePathPolicyBridge.cs" Link="Runtime\\EnemyGatePathPolicyBridge.cs" />
  </ItemGroup>''')
print(json.dumps([{'path':str(root/path),'text':text} for path,text in edits.items()]))
