using APIShared;
using BugfixesAndQoL;

internal static partial class Program
{
    private static void TestGateCompatibility()
    {
        Check(AssassinGateRoutePolicy.TryCapture(null, 2, out var absent) && absent == null,
            "no provider retains the existing mainmod path");
        var policy = new GateTestProvider();
        Check(AssassinGateRoutePolicy.TryCapture(policy, 2, out absent) && absent == null,
            "inactive registered policy performs no capture");
        policy.HasPublishedMask = true;
        Check(AssassinGateRoutePolicy.TryCapture(policy, 2, out var captured) && captured == policy.Snapshot,
            "one immutable policy is captured for the request");
        Check(!AssassinGateRoutePolicy.TryCapture(policy, 258, out _), "full invalid control fails open");
        policy.Snapshot.Current = false;
        Check(!AssassinGateRoutePolicy.TryCapture(policy, 2, out _), "stale capture fails open");
        policy.Snapshot.Current = true;
        Check(AssassinGateRoutePolicy.ReadControlPlayer(2, 1) == 258, "native high control byte is retained");
        Check(AssassinGateRoutePolicy.IdentityHash(captured) == AssassinGateRoutePolicy.IdentityHash(captured),
            "snapshot identity is stable across cache lookups");
        Check(!ReferenceEquals(captured, new GateTestSnapshot()), "new publication has a new cache identity");
        TestGateCacheKeys(captured);

        var random = new Random(7114);
        for (int run = 0; run < 80; run++)
        {
            var graph = new TestGraph(12, 10, 3);
            var reference = new TestGraph(12, 10, 3);
            var snapshot = new GateTestSnapshot();
            graph.GatePolicy = snapshot;
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 12; x++)
            for (int direction = 0; direction < 8; direction++)
            {
                Edge edge = random.Next(8) == 0 ? Edge.Blocked :
                    (direction & 1) == 0 && random.Next(6) == 0 ? Edge.Climb(80) : new Edge(EdgeKind.Ground, 0);
                bool gateBlocked = random.Next(7) == 0;
                if (gateBlocked) snapshot.Blocked.Add((y * 12 + x, direction));
                graph.SetEdge(x, y, direction, edge);
                // Independent reference graph physically removes the forbidden edges.
                reference.SetEdge(x, y, direction, gateBlocked ? Edge.Blocked : edge);
            }
            for (int request = 0; request < 20; request++)
            {
                int sx = random.Next(12), sy = random.Next(10), tx = random.Next(12), ty = random.Next(10);
                var expected = Search(reference, sx, sy, tx, ty, false, int.MaxValue);
                var actual = Search(graph, sx, sy, tx, ty, true, int.MaxValue);
                Check(expected.Found == actual.Found && (!expected.Found || expected.Cost == actual.Cost),
                    "filtered weighted search agrees with independently restricted Dijkstra graph");
            }
        }
        var corridor = new TestGraph(3, 1, 2);
        var restriction = new GateTestSnapshot();
        restriction.Blocked.Add((0, 2));
        corridor.GatePolicy = restriction;
        Check(!Search(corridor, 0, 0, 2, 0, true, 100).Found, "blocked sole gate gives NoRoute");
        corridor.SetEdge(0, 0, 2, Edge.Climb(80));
        Check(!Search(corridor, 0, 0, 2, 0, true, 100).Found, "climb fallback cannot reopen a masked gate");
        restriction.Blocked.Clear();
        Check(Search(corridor, 0, 0, 2, 0, true, 100).ClimbEdges == 1,
            "unmasked regular wall climbing remains usable");
    }

    private static void TestGateCacheKeys(IEnemyGateRoutePolicySnapshot snapshot)
    {
        var flags = System.Reflection.BindingFlags.NonPublic;
        Type route = typeof(AssassinPathfindingRuntime).GetNestedType("RouteCacheKey", flags);
        Type suffix = typeof(AssassinPathfindingRuntime).GetNestedType("SuffixCacheKey", flags);
        object Route(int player, IEnemyGateRoutePolicySnapshot state) =>
            Activator.CreateInstance(route, 1, 2, 3, 4, 400000, 2, player, true, true, state);
        object Suffix(int player, IEnemyGateRoutePolicySnapshot state) =>
            Activator.CreateInstance(suffix, 3, 4, 2, true, true, player, state);
        Check(Route(2, snapshot).Equals(Route(2, snapshot)), "production route key reuses identical publication");
        Check(!Route(2, snapshot).Equals(Route(3, snapshot)), "production route key isolates players");
        Check(!Route(2, snapshot).Equals(Route(2, new GateTestSnapshot())), "production route key isolates generations");
        Check(Suffix(2, snapshot).Equals(Suffix(2, snapshot)), "production suffix key reuses identical publication");
        Check(!Suffix(2, snapshot).Equals(Suffix(3, snapshot)), "production suffix key isolates players");
        Check(!Suffix(2, snapshot).Equals(Suffix(2, new GateTestSnapshot())), "production suffix key isolates generations");
        Check(Route(2, null).Equals(Route(2, null)) && Suffix(0, null).Equals(Suffix(0, null)),
            "production cache keys retain equality without gate provider");
        Check(!Route(2, null).Equals(Route(2, snapshot)) && !Suffix(0, null).Equals(Suffix(2, snapshot)),
            "ungated cache entries cannot leak into gated queries");
    }

    private sealed class GateTestSnapshot : IEnemyGateRoutePolicySnapshot
    {
        internal readonly HashSet<(int, int)> Blocked = new();
        internal bool Current = true;
        public int PlayerId => 2;
        public bool IsCurrent => Current;
        public bool IsDirectionAllowed(int tile, int direction) => !Blocked.Contains((tile, direction));
    }

    private sealed class GateTestProvider : IEnemyGatePathPolicy, IEnemyGateRoutePolicyProvider
    {
        internal readonly GateTestSnapshot Snapshot = new();
        public bool HasPublishedMask { get; set; }
        public bool TryCaptureRoutePolicy(int player, out IEnemyGateRoutePolicySnapshot snapshot)
        { snapshot = Snapshot; return true; }
        public bool IsDirectionAllowed(int player, int tile, int direction) => Snapshot.IsDirectionAllowed(tile, direction);
        public int ResolveTribePlayer(int tribe) => 2;
        public int ResolveBuildingPlayer(int player, int tribe) => 2;
        public int ResolveCursorPlayer(int tribe) => 2;
        public object EnterNativeSearch(int player, EnemyGateSearchKind kind) => null;
        public void ExitNativeSearch(object scope, EnemyGateSearchKind kind, bool completed, bool success) { }
    }
}
