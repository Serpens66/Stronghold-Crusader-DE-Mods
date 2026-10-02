from pathlib import Path

def read(p): return Path(p).read_text(encoding='utf-8-sig')
def write(p,s):
    s=s.replace('\r\n','\n').replace('\r','\n')
    data=s.replace('\n','\r\n').encode('utf-8')
    Path(p).write_bytes(data)
    assert Path(p).read_bytes()==data and b'\n' not in data.replace(b'\r\n',b'')
def edit(p,old,new):
    s=read(p); assert s.count(old)==1,(p,old[:100],s.count(old)); write(p,s.replace(old,new))

p='APIShared/src/EnemyGatePathPolicyBridge.cs'
edit(p,'    /// <summary>Passive bridge', '''    /// <summary>Optional read-only Assassin diagnostics; never changes a search result.</summary>
    public interface IEnemyGateAssassinObserver
    {
        /// <summary>Captures one synchronous builder call and its immutable gate snapshot.</summary>
        object BeginAssassinSearch(int startX, int startY, int targetX, int targetY,
            int maximumNodes, int continuation, string nativeState);
        /// <summary>Observes a directed edge of an already prepared weighted route.</summary>
        void ObserveAssassinEdge(object token, int playerId, int fromTile, int toTile,
            int direction, bool climb);
        /// <summary>Closes the call, preserving its actual native and published results.</summary>
        void EndAssassinSearch(object token, int playerId, int vanillaResult,
            int effectiveResult, string outcome, bool cacheHit, int routeLength);
    }

    /// <summary>Passive bridge''')

root='Testmods/EnemyGatePathfindingTest/'
write(root+'src/GateEdgeOwnership.cs', '''using System;
using System.Collections.Generic;

namespace EnemyGatePathfindingTest
{
    // Built alongside the existing masks. An overlapping edge has no unique gate.
    internal sealed class GateEdgeOwnership
    {
        private readonly Dictionary<long, int> owners = new Dictionary<long, int>();
        internal void Record(int tile, int direction, int gateId)
        {
            long key = (long)tile * 8 + direction;
            if (owners.TryGetValue(key, out int previous) && previous != gateId)
                owners[key] = -1;
            else if (!owners.ContainsKey(key)) owners.Add(key, gateId);
        }
        internal int Resolve(int tile, int direction) =>
            owners.TryGetValue((long)tile * 8 + direction, out int owner) ? owner : 0;
    }

    // All edges count; concrete blocked edges retain first/last in the existing aggregate.
    internal sealed class AssassinRouteProbe
    {
        internal readonly RouteTilePolicySnapshot Snapshot;
        internal long Ground, Climb, BlockedGround, BlockedClimb, Unknown;
        internal AssassinRouteProbe(RouteTilePolicySnapshot snapshot) { Snapshot = snapshot; }
        internal bool Observe(int player, int tile, int direction, bool climb, out int gateId)
        {
            gateId = 0;
            if (climb) Climb++; else Ground++;
            if (player <= 0 || player >= Snapshot.DirectionMasks.Length ||
                tile < 0 || direction < 0 || direction > 7 ||
                (Snapshot.DirectionMasks[player] != null && tile >= Snapshot.DirectionMasks[player].Length))
            { Unknown++; return false; }
            if (Snapshot.IsDirectionAllowed(player, tile, direction)) return false;
            if (climb) BlockedClimb++; else BlockedGround++;
            gateId = Snapshot.EdgeOwners?[player]?.Resolve(tile, direction) ?? 0;
            return true;
        }
    }
}
''')
edit(root+'src/RouteTilePolicySnapshot.cs','string directionMaskDiagnostics = null)',
     'string directionMaskDiagnostics = null, GateEdgeOwnership[] edgeOwners = null)')
edit(root+'src/RouteTilePolicySnapshot.cs','            DirectionMasks = directionMasks ?? new byte[9][];',
     '            DirectionMasks = directionMasks ?? new byte[9][];\n            EdgeOwners = edgeOwners;')
edit(root+'src/RouteTilePolicySnapshot.cs','        internal byte[][] DirectionMasks { get; }',
     '        internal byte[][] DirectionMasks { get; }\n        internal GateEdgeOwnership[] EdgeOwners { get; }')

p=root+'src/GateTopologySnapshotProvider.cs';s=read(p)
s=s.replace('byte[][] directionMasks = new byte[9][];', 'byte[][] directionMasks = new byte[9][];\n            var edgeOwners = new GateEdgeOwnership[9];')
s=s.replace('                        int sampleFrom, sampleTo, sampleDirection;', '                        GateEdgeOwnership ownership = edgeOwners[player] ??\n                            (edgeOwners[player] = new GateEdgeOwnership());\n                        int sampleFrom, sampleTo, sampleDirection;')
s=s.replace('tiles, info, horizontalPassage, masks,','tiles, info, horizontalPassage, masks, ownership, info.GateId,')
s=s.replace('tiles, info.Tiles, horizontalPassage, masks,','tiles, info.Tiles, horizontalPassage, masks, ownership, info.GateId,')
s=s.replace('bool horizontalPassage, byte[] masks,','bool horizontalPassage, byte[] masks, GateEdgeOwnership ownership, int gateId,')
# Only mask construction calls use ClearBoundary.
import re
s=re.sub(r'(ClearBoundary\(tiles, masks, [^\n;]+)(\);)',r'\1, ownership, gateId\2',s)
s=s.replace('int fromX, int fromY, int toX, int toY, int direction)',
    'int fromX, int fromY, int toX, int toY, int direction,\n            GateEdgeOwnership ownership, int gateId)')
s=s.replace('            // A diagonal crossing the same barrier would otherwise cut the corner.',
'''            ownership.Record(from, direction, gateId);
            ownership.Record(to, opposite, gateId);
            // A diagonal crossing the same barrier would otherwise cut the corner.''')
s=s.replace('            return CountBits(unchecked((byte)(beforeFrom ^ masks[from]))) +',
'''            ownership.Record(from, sideDirectionA, gateId);
            ownership.Record(from, sideDirectionB, gateId);
            ownership.Record(to, (sideDirectionA + 4) & 7, gateId);
            ownership.Record(to, (sideDirectionB + 4) & 7, gateId);
            return CountBits(unchecked((byte)(beforeFrom ^ masks[from]))) +''')
assert 'axisDiagnostics.ToString()' in s
s=s.replace('ambiguousPassages, axisDiagnostics.ToString());','ambiguousPassages, axisDiagnostics.ToString(), edgeOwners);')
write(p,s)

for project in ['EnemyGatePathfindingTest.csproj','EnemyGatePathfindingTest.PolicyTests.csproj']:
    p=root+project
    edit(p,'    <Compile Include="src\\AiGateDecisionAggregate.cs"', '    <Compile Include="src\\GateEdgeOwnership.cs" />\n    <Compile Include="src\\AiGateDecisionAggregate.cs"')
edit(root+'EnemyGatePathfindingTest.PolicyTests.csproj','    <Compile Include="src\\GateEdgeOwnership.cs" />',
    '    <Compile Include="src\\RouteTilePolicySnapshot.cs" />\n    <Compile Include="src\\GateEdgeOwnership.cs" />')

p=root+'src/SamePclGateRouteRuntime.cs'
edit(p,'IEnemyGateRegionPairObserver\n', 'IEnemyGateRegionPairObserver, IEnemyGateAssassinObserver\n')
edit(p,'        int IEnemyGatePathPolicy.ResolveTribePlayer(int tribeId) =>', '''        object IEnemyGateAssassinObserver.BeginAssassinSearch(int startX, int startY,
            int targetX, int targetY, int maximumNodes, int continuation, string nativeState) =>
            attackOrderDiagnostics?.BeginAssassinSearch(publishedPolicy, startX, startY,
                targetX, targetY, maximumNodes, continuation, nativeState);

        void IEnemyGateAssassinObserver.ObserveAssassinEdge(object token, int playerId,
            int fromTile, int toTile, int direction, bool climb) =>
            attackOrderDiagnostics?.ObserveAssassinEdge(token, playerId, fromTile, toTile, direction, climb);

        void IEnemyGateAssassinObserver.EndAssassinSearch(object token, int playerId,
            int vanillaResult, int effectiveResult, string outcome, bool cacheHit, int routeLength) =>
            attackOrderDiagnostics?.EndAssassinSearch(token, publishedPolicy, playerId,
                vanillaResult, effectiveResult, outcome, cacheHit, routeLength);

        int IEnemyGatePathPolicy.ResolveTribePlayer(int tribeId) =>''')

p=root+'src/AttackOrderCorrelationDiagnostics.cs'
edit(p,'        internal void ObserveBuildingException', '''        private sealed class AssassinFrame
        {
            internal AssassinRouteProbe Probe;
            internal Frame Order;
            internal int StartX, StartY, TargetX, TargetY, Nodes, Continuation;
            internal string NativeState;
        }

        internal object BeginAssassinSearch(RouteTilePolicySnapshot snapshot, int startX,
            int startY, int targetX, int targetY, int maximumNodes, int continuation, string nativeState) =>
            new AssassinFrame { Probe = new AssassinRouteProbe(snapshot), Order = Current(),
                StartX = startX, StartY = startY, TargetX = targetX, TargetY = targetY,
                Nodes = maximumNodes, Continuation = continuation, NativeState = nativeState };

        internal void ObserveAssassinEdge(object token, int player, int fromTile, int toTile,
            int direction, bool climb)
        {
            if (!(token is AssassinFrame frame)) return;
            if (!frame.Probe.Observe(player, fromTile, direction, climb, out int gateId)) return;
            totals.Record(player, gateId > 0 ? gateId : 0, "assassin-route-edge",
                (climb ? "climb" : "ground") + ",policy=blocked,attribution=" +
                (gateId > 0 ? "exact-mask-construction" : gateId < 0 ? "ambiguous" : "unknown"),
                frame.Order?.Command ?? 0, frame.Order?.Tribe ?? 0,
                frame.TargetX, frame.TargetY, "from=" + fromTile + ",to=" + toTile +
                ",direction=" + direction + ",fingerprint=" + frame.Probe.Snapshot.TopologyFingerprint);
        }

        internal void EndAssassinSearch(object token, RouteTilePolicySnapshot current,
            int player, int vanillaResult, int effectiveResult, string outcome, bool cacheHit, int routeLength)
        {
            if (!(token is AssassinFrame frame)) return;
            if (player <= 0) player = frame.Order?.Player ?? 0;
            AssassinRouteProbe probe = frame.Probe;
            bool stable = ReferenceEquals(current, probe.Snapshot);
            string blocked = probe.BlockedGround > 0 && probe.BlockedClimb > 0 ? "both" :
                probe.BlockedGround > 0 ? "ground" : probe.BlockedClimb > 0 ? "climb" : "none";
            totals.Record(player, 0, "assassin-search",
                "source=" + (frame.Order?.Kind ?? "outside-order") + ",outcome=" + outcome +
                ",native=" + Result(vanillaResult) + ",effective=" + Result(effectiveResult) +
                ",cache=" + cacheHit + ",blocked=" + blocked + ",snapshotStable=" + stable,
                frame.Order?.Command ?? 0, frame.Order?.Tribe ?? 0,
                frame.TargetX, frame.TargetY,
                "unit=" + (frame.Order?.Kind == "unit" ? frame.Order.Id : 0) +
                ",start=" + frame.StartX + "/" + frame.StartY + ",maximumNodes=" + frame.Nodes +
                ",continuation=" + frame.Continuation + ",rawResult=" + vanillaResult + "/" + effectiveResult +
                ",routeLength=" + routeLength + ",ground=" + probe.Ground + ",climb=" + probe.Climb +
                ",blockedGround=" + probe.BlockedGround + ",blockedClimb=" + probe.BlockedClimb +
                ",unknownEdges=" + probe.Unknown + ",nativeState=" + frame.NativeState);
        }

        internal void ObserveBuildingException''')
# Identity comes from the existing lookup, with no second unit query.
edit(p,'ResolveUnitPlayer(args.UnitId, out int tribe, out int command);',
     'ResolveUnitPlayer(args.UnitId, out int tribe, out int command, out string identity);')
edit(p,'"unit=" + args.UnitId + ",unknown=" + args.Unknown);',
     '"unit=" + args.UnitId + ",unknown=" + args.Unknown + "," + identity);')
edit(p,'private static int ResolveUnitPlayer(int unitId, out int tribeId, out int command)',
     'private static int ResolveUnitPlayer(int unitId, out int tribeId, out int command, out string identity)')
edit(p,'            tribeId = command = 0;', '            tribeId = command = 0;\n            identity = "identity=unavailable";')
edit(p,'            tribeId = unit->r_TribeId;', '''            identity = "unitGlobal=" + unit->r_GlobalId + ",unitType=" + unit->r_UnitChimp +
                ",control=" + unit->r_ControllableForPlayerId;
            tribeId = unit->r_TribeId;''')

p='BugfixesAndQoL/src/AssassinPathfindingRuntime.cs'
edit(p,'using BepInEx.Logging;', 'using BepInEx.Logging;\nusing APIShared;')
edit(p,'        private int BuildWeightedPath(IntPtr context,', '''        [ThreadStatic] private static AssassinObservation activeObservation;
        private sealed class AssassinObservation
        {
            internal IEnemyGateAssassinObserver Observer;
            internal object Token;
            internal int Player = -1, NativeResult, EffectiveResult, RouteLength;
            internal bool CacheHit;
            internal string Outcome = "native-exception";
        }

        private int BuildWeightedPath(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
            IEnemyGateAssassinObserver observer = policy != null && policy.HasPublishedMask
                ? policy as IEnemyGateAssassinObserver : null;
            AssassinObservation previous = activeObservation;
            AssassinObservation observation = null;
            try
            {
                if (observer != null)
                {
                    try
                    {
                        observation = new AssassinObservation { Observer = observer,
                            Token = observer.BeginAssassinSearch(startX, startY, targetX, targetY,
                                maximumNodes, continuation, DescribeNativeAssassinState(context)) };
                    }
                    catch (Exception ex) { LogWarning("Assassin diagnostic begin failed: " + ex.GetType().Name); }
                }
                activeObservation = observation;
                int result = BuildWeightedPathCore(context, startX, startY, targetX, targetY, maximumNodes, continuation);
                if (observation != null) observation.EffectiveResult = result;
                return result;
            }
            finally
            {
                activeObservation = previous;
                if (observation != null)
                    try { observer.EndAssassinSearch(observation.Token, observation.Player,
                        observation.NativeResult, observation.EffectiveResult, observation.Outcome,
                        observation.CacheHit, observation.RouteLength); }
                    catch (Exception ex) { LogWarning("Assassin diagnostic end failed: " + ex.GetType().Name); }
            }
        }

        private string DescribeNativeAssassinState(IntPtr context)
        {
            // Every audited D9C40 caller passes this singleton; unknown pointers are not read.
            if (context != IntPtr.Add(libraryHandle, 0x60AD660)) return "unknown-context";
            byte* pointer = (byte*)context.ToPointer();
            var positive = new List<string>(10);
            var negative = new List<string>(10);
            for (int index = 0; index < 10; index++)
            {
                int* accepted = (int*)(pointer + 0x416D8C + index * 8);
                int* rejected = (int*)(pointer + 0x416DDC + index * 8);
                positive.Add(accepted[0] + "/" + accepted[1]);
                negative.Add(rejected[0] + "/" + rejected[1]);
            }
            return "flags=" + *(int*)(pointer + 0x84) + "/" + *(int*)(pointer + 0x88) +
                ",pairCacheInitialized=" + *(int*)(pointer + 0x90) +
                ",positivePairs=[" + string.Join(";", positive) + "],negativePairs=[" +
                string.Join(";", negative) + "],pairCacheHit=not-observed-at-builder";
        }

        private void ObservePreparedAssassinRoute(int player, int routeLength)
        {
            AssassinObservation observation = activeObservation;
            if (observation == null) return;
            observation.RouteLength = routeLength;
            try
            {
                for (int index = routeLength - 1; index > 0; index--)
                {
                    int current = route[index], next = route[index - 1];
                    int dx = next % MapWidth - current % MapWidth;
                    int dy = next / MapWidth - current / MapWidth;
                    int direction = -1;
                    for (int candidate = 0; candidate < 8; candidate++)
                        if (DirectionX[candidate] == dx && DirectionY[candidate] == dy)
                        { direction = candidate; break; }
                    int fromTile = GetTileId(current % MapWidth, current / MapWidth);
                    int toTile = GetTileId(next % MapWidth, next / MapWidth);
                    if (direction < 0 || !IsNativeTile(fromTile) || !IsNativeTile(toTile))
                        throw new InvalidOperationException("Invalid prepared diagnostic edge");
                    bool climb = (directionMasks[direction] & occupancyLayer[fromTile]) == 0;
                    observation.Observer.ObserveAssassinEdge(observation.Token, player,
                        fromTile, toTile, direction, climb);
                }
            }
            catch (Exception ex)
            {
                observation.Outcome += "/diagnostic-edge-error:" + ex.GetType().Name;
                LogWarning("Assassin diagnostic route failed: " + ex.GetType().Name);
            }
        }

        private int BuildWeightedPathCore(IntPtr context,''')
edit(p,'            long nativeTicks = Stopwatch.GetTimestamp() - nativeStarted;',
'''            if (activeObservation != null)
            { activeObservation.NativeResult = vanillaResult; activeObservation.Outcome = "native-only"; }
            long nativeTicks = Stopwatch.GetTimestamp() - nativeStarted;''')
edit(p,'            if (!enabled || continuation != 0)\n                return vanillaResult;',
'''            if (!enabled || continuation != 0)
            {
                if (activeObservation != null) activeObservation.Outcome = !enabled ? "weighted-disabled" : "continuation";
                return vanillaResult;
            }''')
edit(p,'            if (targetX < 0 || targetY < 0)\n                return vanillaResult;',
'''            if (targetX < 0 || targetY < 0)
            {
                if (activeObservation != null) activeObservation.Outcome = "native-flood-field";
                return vanillaResult;
            }''')
edit(p,'                if (!EnsureCoordinateTileMappingValidated())',
'''                if (activeObservation != null) activeObservation.Player = playerId;
                if (!EnsureCoordinateTileMappingValidated())''')
edit(p,'                if (!routeReady)\n                    return 0;',
'''                if (activeObservation != null)
                { activeObservation.CacheHit = routeSummary.CacheHit;
                  activeObservation.Outcome = routeReady ? "weighted-prepared" : "weighted-no-route"; }
                if (!routeReady)
                    return 0;
                ObservePreparedAssassinRoute(playerId, routeSummary.RouteLength);''')
write('_inspect/EnemyGateBuildingContextAudit/assassin-edit-complete.txt','Diagnostic changes prepared; no search outcome or cache behavior changed.\n')
