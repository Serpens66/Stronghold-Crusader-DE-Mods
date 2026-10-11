using System;
using System.IO;
using APIShared;

namespace EnemyGatePathfindingTest
{
    internal static class CompactDiagnosticTests
    {
        internal static int Run()
        {
            int assertions = 0;
            Action<bool, string> check = (value, message) => { assertions++; if (!value) throw new InvalidOperationException(message); };
            var source = new Probe();
            IEnemyGatePathPolicy compact = new FunctionalGatePolicyAdapter(source, source);
            check(source is IEnemyGateRegionPairObserver && compact.GetType().GetInterfaces().Length == 2, "detail source and functional facade remain distinct");
            check(!(compact is IEnemyGateRegionPairObserver) && !(compact is IEnemyGateAssassinObserver), "compact provider exposes no optional observer");
            check(compact.HasPublishedMask == source.HasPublishedMask, "same mask");
            for (int cycle = 0; cycle < 1000; cycle++)
                for (int player = 1; player <= 8; player++)
                {
                    int tile = cycle * 8 + player;
                    check(compact.IsDirectionAllowed(player, tile, cycle % 8) == source.IsDirectionAllowed(player, tile, cycle % 8), "same edge decision");
                    check(compact.ResolveTribePlayer(player) == source.ResolveTribePlayer(player), "same tribe resolver");
                    check(compact.ResolveBuildingPlayer(player, cycle) == source.ResolveBuildingPlayer(player, cycle), "same building resolver");
                    check(compact.ResolveCursorPlayer(player) == source.ResolveCursorPlayer(player), "same cursor resolver");
                    var kind = (EnemyGateSearchKind)(cycle % 5);
                    object scope = compact.EnterNativeSearch(player, kind);
                    check(ReferenceEquals(scope, source.Token), "scope token unwrapped and unchanged");
                    compact.ExitNativeSearch(scope, kind, true, cycle % 2 == 0);
                    check(source.LastPlayer == player && source.LastKind == kind && source.LastSuccess == (cycle % 2 == 0), "exact scope arguments/outcome");
                    bool ok = ((IEnemyGateRoutePolicyProvider)compact).TryCaptureRoutePolicy(player, out var snapshot);
                    check(ok && ReferenceEquals(snapshot, source.Snapshot), "cache policy identity preserved");
                }
            check(source.Entries == 8000 && source.Exits == 8000 && source.Captures == 8000 && source.Observed == 0, "exact functional calls, no extra observer work");
            var routes = new GateRoutePolicySource();
            var provider = new FunctionalGatePolicyAdapter(source, routes);
            byte[][] masks = new byte[9][];
            masks[5] = new byte[] { 0xFE, 0xFF };
            routes.Publish(new RouteTilePolicySnapshot(masks, 123));
            check(provider.TryCaptureRoutePolicy(5, out var before) && before.IsCurrent && !before.IsDirectionAllowed(0, 0), "actual immutable gate mask forwarded");
            using (var scope = new TestRouteScope(routes, routes.Enter(5, true, true)))
            {
                check(provider.TryCaptureRoutePolicy(5, out var unmasked) && unmasked.IsDirectionAllowed(0, 0), "explicit unmasked scope retained");
                check(!provider.TryCaptureRoutePolicy(2, out _), "mismatched player scope remains open");
            }
            check(provider.TryCaptureRoutePolicy(5, out var after) && ReferenceEquals(before, after), "nested scope restores exact cache identity");
            routes.Publish(RouteTilePolicySnapshot.Empty);
            check(!before.IsCurrent && provider.TryCaptureRoutePolicy(5, out var empty) && empty.IsDirectionAllowed(0, 0), "capture/map publication invalidates previous policy");
            var errors = new DeferredGateDiagnosticErrors();
            for (int cause = 0; cause < 40; cause++)
                for (int repetition = 0; repetition < 1000; repetition++) errors.Record("cause" + cause, "detail" + repetition);
            check(errors.DrainNewCauses().Length == 40, "all causes retained beyond 32");
            check(errors.DrainNewCauses().Length == 0, "no repeated warning spam");
            for (int cause = 0; cause < 40; cause++) check(errors.Summary.Contains("cause" + cause + ",count=1000,first=detail0,last=detail999"), "lossless repetition summary");
            errors.Record("cause0", "latest");
            check(errors.DrainNewCauses().Length == 0 && errors.Summary.Contains("cause0,count=1001,first=detail0,last=latest"), "new repetitions counted, no event history");
            errors.Reset();
            check(errors.Summary == "" && errors.DrainNewCauses().Length == 0, "map reset");
            string plugin = File.ReadAllText("src/EnemyGatePathfindingTestPlugin.cs");
            check(plugin.Contains("Config.Bind(\"Diagnostics\", \"DetailedDiagnostics\", false") && !plugin.Contains("SettingChanged"), "default false, startup-only option");
            foreach (string subscription in new[] { "targetOrderSubscription", "tribeMoveSubscription", "unitMoveSubscription" })
                check(plugin.Contains("if (detailedDiagnostics && " + subscription + " == null)"), "order subscription guarded");
            string runtime = File.ReadAllText("src/EnemyGatePathfindingRuntime.cs");
            check(runtime.Contains("if (detailedDiagnostics) APIShared.TemporaryGateRouteAcceptanceBridge.Register") &&
                runtime.Contains("if (detailedDiagnostics)\n            {".Replace("\n", "\r\n")), "temporary registration and objects guarded");
            check(runtime.Contains("Raid/Assassin routes=NOT_INSPECTED") && runtime.Contains("if (detailedDiagnostics && Volatile.Read(ref mapActive)"), "no periodic standard success claims");
            return assertions;
        }
        private sealed class TestRouteScope : IDisposable
        {
            private readonly GateRoutePolicySource source;
            private readonly GateRoutePolicySource.Query query;
            internal TestRouteScope(GateRoutePolicySource source, GateRoutePolicySource.Query query) { this.source = source; this.query = query; }
            public void Dispose() => source.Leave(query);
        }
        private sealed class Probe : IEnemyGatePathPolicy, IEnemyGateRoutePolicyProvider, IEnemyGateRegionPairObserver
        {
            internal readonly object Token = new object();
            internal readonly IEnemyGateRoutePolicySnapshot Snapshot = new ProbeSnapshot();
            internal int Entries, Exits, Captures, Observed, LastPlayer;
            internal EnemyGateSearchKind LastKind;
            internal bool LastSuccess;
            public bool HasPublishedMask => true;
            public bool IsDirectionAllowed(int playerId, int tileId, int direction) => (playerId + tileId + direction) % 3 != 0;
            public int ResolveTribePlayer(int tribeId) => tribeId % 9;
            public int ResolveBuildingPlayer(int explicitPlayerId, int tribeId) => explicitPlayerId == tribeId % 9 ? explicitPlayerId : -1;
            public int ResolveCursorPlayer(int tribeId) => tribeId == 1 ? 1 : -1;
            public object EnterNativeSearch(int playerId, EnemyGateSearchKind kind) { Entries++; LastPlayer = playerId; LastKind = kind; return Token; }
            public void ExitNativeSearch(object scope, EnemyGateSearchKind kind, bool completed, bool success)
            { if (!ReferenceEquals(scope, Token) || !completed || kind != LastKind) throw new InvalidOperationException("scope changed"); Exits++; LastSuccess = success; }
            public bool TryCaptureRoutePolicy(int playerId, out IEnemyGateRoutePolicySnapshot snapshot) { Captures++; snapshot = Snapshot; return true; }
            public void ObserveRegionPair(int playerId, int sourceComponentId, int destinationComponentId, int queryMode, int vanillaResult, int effectiveResult, string source) => Observed++;
        }
        private sealed class ProbeSnapshot : IEnemyGateRoutePolicySnapshot
        {
            public int PlayerId => 1;
            public bool IsCurrent => true;
            public bool IsDirectionAllowed(int tileId, int direction) => true;
        }
    }
}
