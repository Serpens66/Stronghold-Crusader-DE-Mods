// TEMP_GATE_ROUTE_ACCEPTANCE: remove alongside the diagnostic module.
using System;
using System.Globalization;
namespace EnemyGatePathfindingTest
{
    internal static class TemporaryGateAcceptanceTests
    {
        internal static int Run()
        {
            int assertions = 0;
            Action<bool> check = ok => { assertions++; if (!ok) throw new Exception("TEMP_GATE_ROUTE_ACCEPTANCE regression"); };
            var aggregate = new TemporaryGateAcceptanceAggregate();
            long observed = 0, expected = 0;
            for (int minute = 0; minute < 120; minute++)
            {
                for (int player = 1; player <= 8; player++) for (int role = 0; role < 6; role++)
                    for (int command = 0; command < 100; command++)
                    { aggregate.Record(player + "/" + role, command.ToString(), "building=" + (minute * 100 + command)); expected++; }
                string[] rows = aggregate.Drain();
                check(rows.Length == 48);
                foreach (string row in rows)
                {
                    int start = row.IndexOf(",count=", StringComparison.Ordinal) + 7;
                    int end = row.IndexOf(',', start);
                    observed += long.Parse(row.Substring(start, end - start), CultureInfo.InvariantCulture);
                    check(row.Contains("targetChanges=99"));
                }
                check(aggregate.Drain().Length == 0);
            }
            check(observed == expected && expected == 576000 && aggregate.Total == expected);
            aggregate.Reset(); check(aggregate.Total == 0 && aggregate.Drain().Length == 0);
            var generic = new AiGateDecisionAggregate { TemporaryCountsOnly = true };
            for (int i = 0; i < 10000; i++) generic.RecordGateState(5, i, "state", "changing-" + i, 0, i, i, i, "detail");
            check(generic.Observations == 10000 && generic.Drain(out var definitions).Length == 0 && definitions.Length == 0);
            generic.Record(5, 0, "diagnostic-error", "failure", 0, 0, 0, 0); check(generic.Drain().Length == 1);
            int[] dx = {0,1,1,1,0,-1,-1,-1}, dy = {-1,-1,0,1,1,1,0,-1};
            for (int d = 0; d < 8; d++)
            {
                byte[] packed = {(byte)(d | (d << 4)), (byte)d};
                check(Shared.TemporaryPackedRouteInspection.Decode(packed, 3, 100, 100, 100 + 3 * dx[d], 100 + 3 * dy[d], dx, dy, out var route) == "decoded" && route.Length == 3);
            }
            check(Shared.TemporaryPackedRouteInspection.Decode(new byte[]{15}, 1, 100,100,100,100,dx,dy,out _) != "decoded");
            check(Shared.TemporaryPackedRouteInspection.Decode(new byte[]{0}, 1, 100,100,100,100,dx,dy,out _) == "partial-endpoint");
            check(Shared.TemporaryPackedRouteInspection.Decode(new byte[]{0}, 1, 100,0,100,0,dx,dy,out _) == "invalid-coordinate");
            check(Shared.TemporaryPackedRouteInspection.Decode(new byte[]{0}, 3, 100,100,100,97,dx,dy,out _) == "invalid-length");
            var snapshot = new RouteTilePolicySnapshot(new byte[9][], 42);
            for (int player = 1; player <= 8; player++)
            {
                check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,snapshot,player,3,3,"decoded",false,false) == "checked");
                check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,snapshot,player,3,3,"decoded",false,true) == "violated");
            }
            check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,new RouteTilePolicySnapshot(new byte[9][],42),5,3,3,"decoded",false,false) == "unclear:snapshot-changed");
            check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,snapshot,0,3,3,"decoded",false,false) == "unclear:invalid-edge-or-player");
            check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,snapshot,5,3,3,"decoded",true,false) == "unclear:invalid-edge-or-player");
            check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,snapshot,5,2,3,"decoded",false,false) == "unclear:incomplete-edges");
            check(TemporaryGateAcceptanceAggregate.RouteVerdict(snapshot,snapshot,5,3,3,"partial-endpoint",false,false) == "unclear:partial-endpoint");
            check(TemporaryGateAcceptanceAggregate.RouteVerdict(RouteTilePolicySnapshot.Empty,RouteTilePolicySnapshot.Empty,5,3,3,"decoded",false,false) == "unclear:no-policy-snapshot");
            check(APIShared.TemporaryGateRouteAcceptanceBridge.Current == null);
            APIShared.TemporaryGateRouteAcceptanceBridge.ReportRaid(1,0,1,1,1,1,"command","positive","unused");
            check(APIShared.TemporaryGateRouteAcceptanceBridge.Failures == 0);
            var observer = new ThrowingObserver();
            APIShared.TemporaryGateRouteAcceptanceBridge.Register(observer);
            int nativeCalls = 0;
            Func<int> native = () => { nativeCalls++; return 37; };
            int nativeResult = native();
            APIShared.TemporaryGateRouteAcceptanceBridge.ReportRaid(1,0,1,1,1,1,"command","positive","outer");
            check(nativeCalls == 1 && nativeResult == 37 && observer.Calls == 1 && APIShared.TemporaryGateRouteAcceptanceBridge.Failures == 1);
            check(APIShared.TemporaryGateRouteAcceptanceBridge.LastFailureCause == "raid-observer:InvalidOperationException");
            bool rejected = false;
            try { APIShared.TemporaryGateRouteAcceptanceBridge.Register(observer); } catch (InvalidOperationException) { rejected = true; }
            check(rejected && ReferenceEquals(APIShared.TemporaryGateRouteAcceptanceBridge.Current, observer));
            Console.WriteLine("TEMP_GATE_ROUTE_ACCEPTANCE: " + assertions + " assertions, 576000 events across 120 minute windows");
            return assertions;
        }
        private sealed class ThrowingObserver : APIShared.ITemporaryGateRouteAcceptanceObserver
        {
            internal int Calls;
            public object BeginRoute(int player,int tribe,uint global,int unit,uint unitGlobal,int type,int x,int y,int tx,int ty,string context) => null;
            public void RouteEdge(object token,int from,int to,int direction) { }
            public void EndRoute(object token,string status,int result) { }
            public void Raid(int player,int role,int tribe,uint global,int building,uint buildingGlobal,string stage,string result,string detail)
            { Calls++; throw new InvalidOperationException("synthetic observer failure"); }
        }
    }
}
