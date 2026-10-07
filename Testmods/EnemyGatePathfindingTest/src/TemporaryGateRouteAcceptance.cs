// TEMP_GATE_ROUTE_ACCEPTANCE: entirely read-only; remove with documented attachment points.
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class TemporaryGateRouteAcceptance : ITemporaryGateRouteAcceptanceObserver
    {
        private readonly ManualLogSource log;
        private readonly Func<RouteTilePolicySnapshot> policy;
        private readonly TemporaryGateAcceptanceAggregate counts = new TemporaryGateAcceptanceAggregate();
        private readonly long[,] coverage = new long[9, 12];
        private bool active;
        private long epoch, nextFlush, checkedRoutes, violated, unclear, lastFailures;
        private sealed class Route
        {
            internal RouteTilePolicySnapshot Snapshot;
            internal int Player, Role = -1, Tribe;
            internal uint TribeGlobal;
            internal bool Invalid;
            internal long Edges, Climb;
            internal string Kind, Target, Detail;
            internal readonly Dictionary<int, string> Violations = new Dictionary<int, string>();
            internal readonly Dictionary<int, long> ViolationCounts = new Dictionary<int, long>();
        }
        internal TemporaryGateRouteAcceptance(ManualLogSource log, Func<RouteTilePolicySnapshot> policy)
        { this.log = log; this.policy = policy; }
        internal void Begin()
        {
            counts.Reset(); Array.Clear(coverage, 0, coverage.Length); epoch++; active = true;
            checkedRoutes = violated = unclear = 0; nextFlush = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
            lastFailures = TemporaryGateRouteAcceptanceBridge.Failures;
            Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE begin epoch=" + epoch +
                ",intervalSeconds=60,noExtraSearches=true,noNewHooks=true,unmeasuredNativeFallbacks=unknown");
        }
        internal void End()
        {
            if (!active) return;
            Flush(true); active = false; counts.Reset(); Array.Clear(coverage, 0, coverage.Length);
        }
        internal void Deferred() { if (active && Stopwatch.GetTimestamp() >= nextFlush) Flush(false); }
        private void Flush(bool final)
        {
            nextFlush = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
            foreach (string row in counts.Drain()) Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE aggregate " + row);
            Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE summary epoch=" + epoch + ",final=" + final +
                ",observations=" + counts.Total + ",checked=" + checkedRoutes + ",violated=" + violated +
                ",unclear=" + unclear + ",adapterFailures=" + (TemporaryGateRouteAcceptanceBridge.Failures - lastFailures) +
                ",lastAdapterFailure=" + TemporaryGateRouteAcceptanceBridge.LastFailureCause);
            if (final)
                for (int player = 1; player <= 8; player++)
                {
                    string roles = "";
                    for (int role = 0; role < 6; role++) roles += role + ":" + (coverage[player, role] == 0 ? "not-observed" : coverage[player, role].ToString()) + ";";
                    Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE coverage player=" + player + ",raidRoles=[" + roles +
                        "],assassinTarget=" + coverage[player, 6] + ",assassinCache=" + coverage[player, 7] +
                        ",assassinClimb=" + coverage[player, 8] + ",assassinFlood=" + coverage[player, 9] +
                        ",assassinContinuation=" + coverage[player, 10] + ",packedPublications=" + coverage[player, 11] + ",missingCoverageIsNotPass=true");
                }
        }
        private int Role(int player, int tribe, uint global)
        {
            if (player <= 0 || player > 8 || global == 0) return -1;
            var api = GameTribeManagerAPI.Instance;
            for (int role = 0; role < 6; role++)
            {
                var slot = (AITribeStorageRole16)((int)AITribeStorageRole16.HarassmentCombat0 + role);
                if (api.TryGetAITribeStorageRole(player, slot, out ushort id, out uint generation) && id == tribe && generation == global &&
                    api.TryResolveAITribeStorageRole(player, slot, out GameTribe* live) && live != null &&
                    live->r_GlobalId == global && live->r_PlayerIdOwner == player) return role;
            }
            return -1;
        }
        private void Record(int player, int role, string kind, string result, string target, string detail)
        {
            if (active) counts.Record("player=" + player + ",role=" + role + ",kind=" + kind + ",result=" + result, target, detail);
        }
        internal void RaidMove(int player, int tribe, bool pre, int x, int y, long result)
        {
            if (!active || !GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* live) || live == null) return;
            int role = Role(player, tribe, live->r_GlobalId);
            if (role < 0) return;
            coverage[player, role]++;
            Record(player, role, "raid-move-" + (pre ? "pre" : "post"), pre ? "called" : result > 0 ? "positive" : "nonpositive",
                x + "/" + y, "tribe=" + tribe + "/" + live->r_GlobalId + ",target=" + x + "/" + y + ",return=" + result + ",routeSuccessNotImplied=true");
        }
        public object BeginRoute(int player, int tribe, uint tribeGlobal, int unit, uint unitGlobal,
            int type, int x, int y, int tx, int ty, string orderContext)
        {
            if (!active) return null;
            int role = Role(player, tribe, tribeGlobal);
            bool assassin = type == (int)eChimps.CHIMP_TYPE_ARAB_ASSASIN;
            if (role < 0 && !assassin) return null;
            return new Route { Snapshot = policy(), Player = player, Tribe = tribe, TribeGlobal = tribeGlobal, Role = role,
                Kind = assassin ? "assassin-published" : "raid-published", Target = tx + "/" + ty,
                Detail = "tribe=" + tribe + "/" + tribeGlobal + ",unit=" + unit + "/" + unitGlobal +
                    ",type=" + type + ",start=" + x + "/" + y + ",target=" + tx + "/" + ty + "," + orderContext +
                    ",publicationOrigin=final-builder-output,edgeClassification=packed-directions-climb-unknown" };
        }
        public void RouteEdge(object token, int from, int to, int direction) => Edge(token, from, to, direction, false);
        internal void Edge(object token, int from, int to, int direction, bool climb)
        {
            if (!(token is Route route)) return;
            route.Edges++; if (climb) route.Climb++;
            if (route.Player <= 0 || route.Player > 8 || direction < 0 || direction > 7 ||
                (uint)from >= EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive ||
                (uint)to >= EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive || route.Snapshot == null)
            { route.Invalid = true; return; }
            byte[] mask = route.Player < route.Snapshot.DirectionMasks.Length ? route.Snapshot.DirectionMasks[route.Player] : null;
            if (route.Player >= route.Snapshot.DirectionMasks.Length || (mask != null && from >= mask.Length))
            { route.Invalid = true; return; }
            if (route.Snapshot.IsDirectionAllowed(route.Player, from, direction)) return;
            int gate = route.Snapshot.EdgeOwners?[route.Player]?.Resolve(from, direction) ?? 0;
            route.Violations[gate] = "from=" + from + ",to=" + to + ",direction=" + direction + ",climb=" + climb;
            route.ViolationCounts.TryGetValue(gate, out long count); route.ViolationCounts[gate] = count + 1;
        }
        public void EndRoute(object token, string status, int result)
        {
            if (!(token is Route route) || !active) return;
            string verdict = TemporaryGateAcceptanceAggregate.RouteVerdict(route.Snapshot, policy(), route.Player,
                route.Edges, result, status, route.Invalid, route.Violations.Count != 0);
            bool complete = verdict == "checked" || verdict == "violated";
            if (route.Kind.EndsWith("-published", StringComparison.Ordinal) && route.Player > 0 && route.Player <= 8) coverage[route.Player, 11]++;
            if (complete) { checkedRoutes++; if (route.Violations.Count != 0) violated++; } else unclear++;
            Record(route.Player, route.Role, route.Kind, verdict, route.Target,
                route.Detail + ",length=" + result + ",observedEdges=" + route.Edges + ",climbEdges=" + route.Climb);
            if (complete)
                foreach (var pair in route.Violations)
                    Record(route.Player, route.Role, "violation", "gate=" + pair.Key + "/attribution=" + (pair.Key > 0 ? "exact" : pair.Key < 0 ? "ambiguous" : "unknown"), route.Target,
                        route.Detail + ",blockedEdges=" + route.ViolationCounts[pair.Key] + "," + pair.Value);
        }
        internal object BeginAssassin(int player, int tribe, int tx, int ty)
        {
            if (!active) return null;
            uint global = 0;
            if (GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* live) && live != null && live->r_PlayerIdOwner == player)
                global = live->r_GlobalId;
            return new Route { Snapshot = policy(), Player = player, Tribe = tribe, TribeGlobal = global, Role = Role(player, tribe, global),
                Kind = tx < 0 || ty < 0 ? "assassin-flood" : "assassin-weighted", Target = tx + "/" + ty,
                Detail = "tribe=" + tribe + "/" + global + ",target=" + tx + "/" + ty };
        }
        internal void AssassinEdge(object token, int player, int from, int to, int direction, bool climb)
        { if (token is Route route) { if (route.Edges > 0 && route.Player != player) route.Invalid = true; route.Player = player; Edge(route, from, to, direction, climb); } }
        internal void EndAssassin(object token, int player, int native, int effective, string outcome, bool cache, int length, int continuation)
        {
            if (!(token is Route route) || !active) return;
            if (player > 0) { if (route.Edges > 0 && route.Player != player) route.Invalid = true; route.Player = player; }
            route.Role = Role(route.Player, route.Tribe, route.TribeGlobal);
            bool target = route.Kind != "assassin-flood";
            if (route.Player > 0 && route.Player <= 8 && target) { coverage[route.Player, 6]++; if (cache) coverage[route.Player, 7]++; }
            if (route.Player > 0 && route.Player <= 8) { if (route.Climb > 0) coverage[route.Player, 8]++; if (!target) coverage[route.Player, 9]++; if (continuation != 0) coverage[route.Player, 10]++; }
            route.Kind += cache ? "-cache" : "-fresh";
            route.Detail += ",native=" + native + ",effective=" + effective + ",cache=" + cache + ",continuation=" + continuation + ",outcome=" + outcome;
            Record(route.Player, route.Role, route.Kind, "native=" + native + "/effective=" + effective + "/cache=" + cache + "/continuation=" + (continuation != 0),
                route.Target, route.Detail + ",outcome=" + outcome);
            if (target) EndRoute(route, length > 0 ? "decoded" : "no-materialized-route", length);
        }
        public void Raid(int player, int role, int tribe, uint tribeGlobal, int building, uint buildingGlobal, string stage, string result, string detail)
        {
            if (!active) return;
            if (Role(player, tribe, tribeGlobal) != role || role < 0) { unclear++; Record(player, -1, "raid-identity", "unclear", "", detail); return; }
            coverage[player, role]++;
            Record(player, role, stage, result, building + "/" + buildingGlobal,
                "tribe=" + tribe + "/" + tribeGlobal + ",building=" + building + "/" + buildingGlobal + "," + detail);
        }
    }
}
