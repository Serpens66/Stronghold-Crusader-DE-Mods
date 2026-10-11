// TEMP_GATE_ROUTE_ACCEPTANCE: entirely read-only; remove with documented attachment points.
using APIShared;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class TemporaryGateRouteAcceptance : ITemporaryGateRouteAcceptanceObserver, ITemporaryAssassinGateObserver
    {
        private readonly TemporaryGateCaptureTimeline captureTimeline = new TemporaryGateCaptureTimeline();
        private long callSequence;
        private readonly ManualLogSource log;
        private readonly Func<RouteTilePolicySnapshot> policy;
        private readonly TemporaryGateAcceptanceAggregate counts = new TemporaryGateAcceptanceAggregate();
        private readonly long[,] coverage = new long[9, 12];
        private bool active;
        private long epoch, nextFlush, checkedRoutes, violated, unclear, negativeSearches, lastFailures, unattributedSearches, permittedClimbRoutes, maskOverlapRoutes, suspectedNativeGroundRoutes;
        private sealed class Route
        {
            internal RouteTilePolicySnapshot Snapshot;
            internal long Epoch, CallId, Started = Stopwatch.GetTimestamp(), EntryGeneration;
            internal readonly Dictionary<string, GateUse> GateUses = new Dictionary<string, GateUse>();
            internal int Player, Role = -1, Tribe;
            internal int StartX, StartY, TargetX, TargetY;
            internal uint TribeGlobal;
            internal bool Invalid, GateIdentityInvalid;
            internal long Edges, Climb, GroundViolations, AllowedClimb, UnknownOverlap, SuspectedNativeGround;
            internal string Kind, Target, Detail, SourceEvidence, PolicyEvidence, FirstFunctionalClimb, LastFunctionalClimb;
            internal string Dimensions = "";
            internal int TargetBuilding;
            internal uint TargetBuildingGlobal;
            internal int AttackBuilding;
            internal uint AttackBuildingGlobal;
            internal readonly Dictionary<int, string> Violations = new Dictionary<int, string>();
            internal readonly Dictionary<int, string> FirstViolations = new Dictionary<int, string>();
            internal readonly Dictionary<int, long> ViolationCounts = new Dictionary<int, long>();
            internal readonly Dictionary<int, RouteTilePolicySnapshot.GateIdentity> SnapshotGateIdentities = new Dictionary<int, RouteTilePolicySnapshot.GateIdentity>();
            internal readonly Dictionary<int, uint> LiveGateGlobals = new Dictionary<int, uint>();
            internal readonly Dictionary<int, string> ViolationMeanings = new Dictionary<int, string>();
        }
        private sealed class GateUse
        {
            internal int Gate;
            internal string Movement, First, Last;
            internal long Edges;
        }
        internal void ObserveCapture(int building, int player, bool post)
        {
            if (!active) return;
            long at = Stopwatch.GetTimestamp();
            uint global = captureTimeline.ObserveCapture(building, player, at);
            Record(player, -1, "capture-event", "gate=" + building + "/global=" + global + "/phase=" + (post ? "post" : "pre"), "",
                TemporaryGateCaptureTimeline.Time(at) + ",capturingPlayer=" + player + ",postIsNotNativeSuccessProof=true");
        }
        internal void ObservePolicyPublication(RouteTilePolicySnapshot snapshot, int generation)
        {
            if (!active) return;
            long at = Stopwatch.GetTimestamp();
            captureTimeline.Publish(snapshot, generation, at);
            if (snapshot == null || snapshot == RouteTilePolicySnapshot.Empty)
            { Record(0, -1, "policy-publication", "logical-open-transition", "", "generation=" + generation + "," + TemporaryGateCaptureTimeline.Time(at)); return; }
            foreach (var pair in snapshot.GateIdentities)
                Record(0, -1, "policy-publication", "gate=" + pair.Key + "/global=" + pair.Value.Global + "/capturer=" + pair.Value.Capturer, "",
                    "generation=" + generation + ",fingerprint=" + snapshot.TopologyFingerprint + "," + TemporaryGateCaptureTimeline.Time(at) + "," + captureTimeline.Describe(pair.Key));
        }
        internal TemporaryGateRouteAcceptance(ManualLogSource log, Func<RouteTilePolicySnapshot> policy)
        { this.log = log; this.policy = policy; }
        internal void Begin()
        {
            counts.Reset(); Array.Clear(coverage, 0, coverage.Length); epoch++; active = true;
            captureTimeline.Reset(epoch); callSequence = 0;
            checkedRoutes = violated = unclear = negativeSearches = unattributedSearches = permittedClimbRoutes = maskOverlapRoutes = suspectedNativeGroundRoutes = 0; nextFlush = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
            lastFailures = TemporaryGateRouteAcceptanceBridge.Failures;
            Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE begin epoch=" + epoch +
                ",format=7,emissionTimeIsNotObservationTime=true,monoFrequency=" + Stopwatch.Frequency + ",intervalSeconds=60,weightedLengthUnit=nodes,packedLengthUnit=edges," +
                "publicationContext=frame-or-owned-buffer,noExtraSearches=true,noNewHooks=true,unmeasuredNativeFallbacks=unknown," +
                "fixesLoaded=" + Chainloader.PluginInfos.ContainsKey("fixes") + ",fixesLiveLordOverrides=unknown,fixesHookEffect=unmeasured");
        }
        internal void End()
        {
            if (!active) return;
            Flush(true); active = false; counts.Reset(); Array.Clear(coverage, 0, coverage.Length); captureTimeline.Reset(epoch);
        }
        internal void Deferred() { if (active && Stopwatch.GetTimestamp() >= nextFlush) Flush(false); }
        private void Flush(bool final)
        {
            nextFlush = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
            foreach (string row in counts.Drain()) Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE aggregate emittedUtc=" + DateTime.UtcNow.ToString("O") + "," + row);
            Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE summary epoch=" + epoch + ",final=" + final +
                ",observations=" + counts.Total + ",checked=" + checkedRoutes + ",violated=" + violated +
                ",unclear=" + unclear + ",suspectedNativeGroundRoutes=" + suspectedNativeGroundRoutes + ",permittedClimbRoutes=" + permittedClimbRoutes + ",maskOverlapRoutes=" + maskOverlapRoutes + ",unattributedSearches=" + unattributedSearches + ",negativeSearches=" + negativeSearches + ",adapterFailures=" + (TemporaryGateRouteAcceptanceBridge.Failures - lastFailures) +
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
            if (player <= 0 || player > 8 || global == 0 || !GameTribeManagerAPI.Instance.IsValidId(tribe)) return -1;
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
        private string Group(int player, int tribe, uint global, int role)
        {
            if (role >= 0) return "raid-" + role;
            if (player <= 0 || player > 8 || global == 0 || !GameTribeManagerAPI.Instance.IsValidId(tribe)) return "unknown";
            var api = GameTribeManagerAPI.Instance;
            if (api.TryGetAITribeStorageRole(player, AITribeStorageRole16.SiegeAssassins, out ushort id, out uint generation) &&
                id == tribe && generation == global && api.TryResolveAITribeStorageRole(player, AITribeStorageRole16.SiegeAssassins, out GameTribe* live) &&
                live != null && live->r_GlobalId == global && live->r_PlayerIdOwner == player) return "siege-assassins";
            return api.TryGetTribeById(tribe, out GameTribe* other) && other != null &&
                other->r_GlobalId == global && other->r_PlayerIdOwner == player ? "other-verified-tribe" : "unknown";
        }
        private static uint BuildingGlobal(int building)
        {
            return GameBuildingManagerAPI.Instance.IsValidId(building) &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(building, out GameBuilding* live) && live != null
                ? live->r_GlobalId : 0;
        }
        private void Record(int player, int role, string kind, string result, string target, string detail, string dimensions = "")
        {
            if (active) counts.Record("player=" + player + ",role=" + role + ",kind=" + kind + ",result=" + result + dimensions, target, detail);
        }
        internal void RaidMove(int player, int tribe, bool pre, int x, int y, long result)
        {
            if (!active) return;
            if (!GameTribeManagerAPI.Instance.IsValidId(tribe))
            { MissingContext(player, "raid-move", "invalid-tribe", tribe); return; }
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* live) || live == null)
            { MissingContext(player, "raid-move", "missing-tribe", tribe); return; }
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
            if (type < 0)
            { unclear++; Record(player, -1, "publication-context", orderContext, "", "unit=" + unit + "/" + unitGlobal); return null; }
            int role = Role(player, tribe, tribeGlobal);
            bool assassin = type == (int)eChimps.CHIMP_TYPE_ARAB_ASSASIN;
            if (role < 0 && !assassin) return null;
            if (tribeGlobal == 0) MissingContext(player, "published-route", "unverified-tribe", tribe);
            int targetBuilding = 0;
            int targetTile = (uint)tx < 800 && (uint)ty < 800 ? GameTileManagerAPI.Instance.GetTileId(tx, ty) : -1;
            if ((uint)targetTile < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                targetBuilding = GameTileManagerAPI.Instance.GetTileBuildingId(targetTile);
            uint targetGlobal = BuildingGlobal(targetBuilding);
            int attackBuilding = 0; uint attackGlobal = 0;
            if (DiagnosticValue(orderContext ?? "", "command") == "AttackBuilding")
            {
                string[] pair = DiagnosticValue(orderContext, "buildingOrUnit").Split('/');
                if (pair.Length != 2 || !int.TryParse(pair[0], out attackBuilding) || !uint.TryParse(pair[1], out attackGlobal) ||
                    attackGlobal == 0 || BuildingGlobal(attackBuilding) != attackGlobal) { attackBuilding = 0; attackGlobal = 0; }
            }
            return new Route { Snapshot = policy(), Epoch = epoch, CallId = ++callSequence, EntryGeneration = captureTimeline.Generation, Player = player, Tribe = tribe, TribeGlobal = tribeGlobal, Role = role,
                TargetBuilding = targetBuilding, TargetBuildingGlobal = targetGlobal,
                AttackBuilding = attackBuilding, AttackBuildingGlobal = attackGlobal,
                Dimensions = ",group=" + Group(player, tribe, tribeGlobal, role),
                StartX = x, StartY = y, TargetX = tx, TargetY = ty,
                Kind = assassin ? "assassin-published" : "raid-published", Target = tx + "/" + ty,
                Detail = "group=" + Group(player, tribe, tribeGlobal, role) + ",tribe=" + tribe + "/" + tribeGlobal + ",unit=" + unit + "/" + unitGlobal +
                    ",type=" + type + ",start=" + x + "/" + y + ",target=" + tx + "/" + ty + "," + orderContext +
                    ",publicationOrigin=final-builder-output,edgeClassification=packed-directions-climb-unknown," +
                    "endpointBuilding=" + targetBuilding + "/" + targetGlobal + ",attackBuilding=" + attackBuilding + "/" + attackGlobal +
                    ",endpointIsNotAttackTargetProof=true,fixesOrderOrigin=not-attributed" };
        }
        public void RouteEdge(object token, int from, int to, int direction) => Edge(token, from, to, direction, false);
        internal void Edge(object token, int from, int to, int direction, bool climb,
            AssassinTransitionKind? functionalMovement = null, bool functionalAllowed = false, int functionalGate = 0, uint functionalGlobal = 0, string functionalEvidence = null)
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
            bool policyAllowed = route.Snapshot.IsDirectionAllowed(route.Player, from, direction);
            int diagnosticGate = route.Snapshot.DiagnosticGateEdges?.Resolve(from, direction) ?? 0;
            if (diagnosticGate > 0)
            {
                string movementName = route.Kind.EndsWith("-published", StringComparison.Ordinal) ? "packed-movement-unproven" :
                    functionalMovement.HasValue ? functionalMovement.Value.ToString() : "weighted-movement-unproven";
                if (!route.Snapshot.GateIdentities.TryGetValue(diagnosticGate, out var diagnosticIdentity))
                { route.Invalid = true; return; }
                route.SnapshotGateIdentities[diagnosticGate] = diagnosticIdentity;
                string key = diagnosticGate + "/" + movementName + "/allowed=" + policyAllowed;
                if (!route.GateUses.TryGetValue(key, out GateUse use))
                    route.GateUses.Add(key, use = new GateUse { Gate = diagnosticGate, Movement = movementName });
                string edge = "from=" + from + ",to=" + to + ",direction=" + direction + ",index=" + (route.Edges - 1);
                if (use.First == null) use.First = edge;
                use.Last = edge; use.Edges++;
            }
            if (policyAllowed) return;
            int gate = route.Snapshot.EdgeOwners?[route.Player]?.Resolve(from, direction) ?? 0;
            bool packed = route.Kind.EndsWith("-published", StringComparison.Ordinal);
            AssassinTransitionKind movement = functionalMovement ?? AssassinPathAPI.ClassifyNativeTransition(from, to, direction, true);
            RouteTilePolicySnapshot.GateIdentity identity = default;
            bool stableGate = gate > 0 && route.Snapshot.GateIdentities.TryGetValue(gate, out identity) &&
                identity.Global != 0 && GameBuildingManagerAPI.Instance.IsValidId(gate) &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(gate, out GameBuilding* gateLive) && gateLive != null &&
                gateLive->r_GlobalId == identity.Global && gateLive->r_PlayerIdOwner == identity.Owner && gateLive->r_CapturedByPlayerId == identity.Capturer &&
                (int)gateLive->r_AliveState != 0 && (int)gateLive->r_AliveState != 3 &&
                ((int)gateLive->r_BuildingType == 45 || (int)gateLive->r_BuildingType == 46);
            if (stableGate) route.SnapshotGateIdentities[gate] = identity;
            bool matchingGateEndpoint = stableGate && (GameTileManagerAPI.Instance.GetTileBuildingId(from) == gate ||
                GameTileManagerAPI.Instance.GetTileBuildingId(to) == gate);
            if (!packed && climb && matchingGateEndpoint &&
                (!functionalMovement.HasValue || (functionalAllowed && functionalGate == gate &&
                    route.Snapshot.GateIdentities.TryGetValue(gate, out var functionalIdentity) && functionalIdentity.Global == functionalGlobal)) &&
                (movement == AssassinTransitionKind.ClimbUp || movement == AssassinTransitionKind.ClimbDown))
            {
                route.AllowedClimb++;
                if (functionalEvidence != null)
                {
                    string proof = "from=" + from + ",to=" + to + ",direction=" + direction + ",movement=" + movement + "," + functionalEvidence;
                    if (route.FirstFunctionalClimb == null) route.FirstFunctionalClimb = proof;
                    route.LastFunctionalClimb = proof;
                }
                return;
            }
            bool provenGround = !packed && movement == AssassinTransitionKind.Ground && stableGate;
            if (provenGround) route.GroundViolations++;
            else route.UnknownOverlap++;
            if (packed && movement == AssassinTransitionKind.Ground && stableGate) route.SuspectedNativeGround++;
            string detail = "movement=" + movement + ",assessment=" +
                (provenGround ? "confirmed-ground-policy-violation" : packed && movement == AssassinTransitionKind.Ground && stableGate
                    ? "suspected-ground-overlap-native-branch-unproven" : "mask-overlap-unresolved") + ",from=" + from + ",to=" + to + ",direction=" + direction + ",edgeIndex=" + (route.Edges - 1) +
                ",climb=" + (packed ? "unknown" : climb.ToString()) +
                (functionalEvidence == null ? "" : ",functionalValidator=[" + functionalEvidence + "]");
            if (packed)
            {
                var tiles = GameTileManagerAPI.Instance;
                int fromBuilding = tiles.GetTileBuildingId(from), toBuilding = tiles.GetTileBuildingId(to);
                uint gateGlobal = BuildingGlobal(gate);
                if (gate > 0 && gateGlobal != 0)
                {
                    if (route.LiveGateGlobals.TryGetValue(gate, out uint priorGlobal) && priorGlobal != gateGlobal) route.Invalid = true;
                    route.LiveGateGlobals[gate] = gateGlobal;
                }
                string meaning = gate > 0 && gateGlobal != 0 && gate == route.AttackBuilding && gateGlobal == route.AttackBuildingGlobal
                    ? "gate-attack-order-cut-overlap" : gate > 0 && gateGlobal != 0 && gate == route.TargetBuilding && gateGlobal == route.TargetBuildingGlobal
                    ? "gate-endpoint-approach-or-interior" : "blocked-cut-crossing-purpose-unknown";
                route.ViolationMeanings[gate] = meaning;
                detail += ",meaning=" + meaning + ",gateLive=" + gate + "/" + gateGlobal +
                    ",fromBuilding=" + fromBuilding + "/" + BuildingGlobal(fromBuilding) +
                    ",toBuilding=" + toBuilding + "/" + BuildingGlobal(toBuilding) +
                    ",surfaceRaw=" + ((uint)tiles.GetTilePropertyFlag(from)).ToString("X") + "/" +
                    ((uint)tiles.GetTilePropertyFlag(to)).ToString("X") + ",nativeAcceptedBranch=not-observed";
                if (gateGlobal != 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(gate, out GameBuilding* live) && live != null)
                    detail += ",gateTypeRaw=" + (int)live->r_BuildingType + ",gateOwnerRaw=" + live->r_PlayerIdOwner +
                        ",gateCapturerRaw=" + live->r_CapturedByPlayerId + ",gateAliveRaw=" + (int)live->r_AliveState;
            }
            if (!route.FirstViolations.ContainsKey(gate)) route.FirstViolations[gate] = detail;
            route.Violations[gate] = detail;
            route.ViolationCounts.TryGetValue(gate, out long count); route.ViolationCounts[gate] = count + 1;
        }
        public void EndRoute(object token, string status, int result) => EndRouteCore(token, status, result, false);
        private static string DiagnosticValue(string text, string key)
        {
            int index = text.IndexOf(key + "=", StringComparison.Ordinal);
            if (index < 0) return "unknown";
            int start = index + key.Length + 1, end = text.IndexOf(',', start);
            return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
        }
        private void EndRouteCore(object token, string status, int expectedEdges, bool stationary)
        {
            if (!(token is Route route) || !active) return;
            long ended = Stopwatch.GetTimestamp();
            route.Detail += ",routeCall=" + route.CallId + ",entryGeneration=" + route.EntryGeneration +
                ",endGeneration=" + captureTimeline.Generation + ",started=[" + TemporaryGateCaptureTimeline.Time(route.Started) +
                "],ended=[" + TemporaryGateCaptureTimeline.Time(ended) + "]";
            int separator = status.IndexOf("|diag=", StringComparison.Ordinal);
            if (separator >= 0)
            {
                string diagnostic = status.Substring(separator + 6);
                route.Detail += "," + diagnostic;
                route.Dimensions += ",builderSource=" + DiagnosticValue(diagnostic, "builderSource") +
                    ",reconstructionRelaxation=" + DiagnosticValue(diagnostic, "reconstructionRelaxation");
                status = status.Substring(0, separator);
            }
            foreach (var gateIdentity in route.SnapshotGateIdentities)
            {
                if (!GameBuildingManagerAPI.Instance.IsValidId(gateIdentity.Key) ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(gateIdentity.Key, out GameBuilding* completionGate) || completionGate == null ||
                    completionGate->r_GlobalId != gateIdentity.Value.Global ||
                    (int)completionGate->r_AliveState == 0 || (int)completionGate->r_AliveState == 3 ||
                    ((int)completionGate->r_BuildingType != 45 && (int)completionGate->r_BuildingType != 46))
                { route.Invalid = true; route.GateIdentityInvalid = true; }
                else if (completionGate->r_PlayerIdOwner != gateIdentity.Value.Owner ||
                    completionGate->r_CapturedByPlayerId != gateIdentity.Value.Capturer) route.Invalid = true;
            }
            foreach (var identity in route.LiveGateGlobals)
                if (BuildingGlobal(identity.Key) != identity.Value) route.Invalid = true;
            if (route.TargetBuildingGlobal != 0 && BuildingGlobal(route.TargetBuilding) != route.TargetBuildingGlobal) route.Invalid = true;
            if (route.AttackBuildingGlobal != 0 && BuildingGlobal(route.AttackBuilding) != route.AttackBuildingGlobal) route.Invalid = true;
            if (route.Epoch != epoch) status = "map-epoch-changed";
            if (status == "search-negative")
            { negativeSearches++; Record(route.Player, route.Role, route.Kind, "negative-search-no-route", route.Target, route.Detail, route.Dimensions); return; }
            string verdict = TemporaryGateAcceptanceAggregate.RouteVerdict(route.Snapshot, policy(), route.Player,
                route.Edges, expectedEdges, status, route.Invalid, route.GroundViolations != 0, stationary);
            if (verdict == "checked" && route.UnknownOverlap > 0) verdict = route.SuspectedNativeGround > 0
                ? "unclear:suspected-ground-overlap-native-branch-unproven" : "unclear:mask-overlap-movement-unproven";
            foreach (var pair in route.GateUses)
            {
                GateUse use = pair.Value;
                string phase = captureTimeline.Phase(route.Snapshot, use.Gate, route.Started, ended);
                if (route.GateIdentityInvalid || route.Epoch != epoch || (route.Invalid && phase != "transition")) phase = "not-attributed";
                var identity = route.Snapshot.GateIdentities[use.Gate];
                Record(route.Player, route.Role, "gate-route-timing",
                    "gate=" + use.Gate + "/global=" + identity.Global + "/phase=" + phase + "/source=" + route.Kind + "/verdict=" + verdict + "/" + pair.Key,
                    route.Target, route.Detail + ",routeStatus=" + status + ",gateEdges=" + use.Edges +
                    ",owner=" + identity.Owner + ",capturer=" + identity.Capturer +
                    ",selfCapturer=" + (identity.Capturer == route.Player) + ",actualNativeClimbExecution=not-observed," +
                    captureTimeline.Describe(use.Gate) + ",firstEdge=[" + use.First + "],lastEdge=[" + use.Last + "]",
                    route.Dimensions);
            }
            bool complete = verdict == "checked" || verdict == "violated";
            if (route.Kind.EndsWith("-published", StringComparison.Ordinal) && route.Player > 0 && route.Player <= 8) coverage[route.Player, 11]++;
            if (complete) { checkedRoutes++; if (route.GroundViolations != 0) violated++; } else unclear++;
            if (verdict == "checked" && route.AllowedClimb > 0) permittedClimbRoutes++;
            if (route.Violations.Count > 0) maskOverlapRoutes++;
            if (route.SuspectedNativeGround > 0) suspectedNativeGroundRoutes++;
            Record(route.Player, route.Role, route.Kind, verdict, route.Target,
                route.Detail + ",confirmedGroundEdges=" + route.GroundViolations + ",suspectedNativeGroundEdges=" + route.SuspectedNativeGround + ",permittedClimbEdges=" + route.AllowedClimb + ",unresolvedOverlapEdges=" + route.UnknownOverlap + ",expectedEdges=" + expectedEdges + ",observedEdges=" + route.Edges + ",climbEdges=" +
                (route.Kind.EndsWith("-published", StringComparison.Ordinal) ? "unknown" : route.Climb.ToString()), route.Dimensions);
            if (complete || verdict == "unclear:mask-overlap-movement-unproven" || verdict == "unclear:suspected-ground-overlap-native-branch-unproven")
                foreach (var pair in route.Violations)
                    Record(route.Player, route.Role, "mask-overlap", "gate=" + pair.Key + "/attribution=" + (pair.Key > 0 ? "exact" : pair.Key < 0 ? "ambiguous" : "unknown") +
                        "/meaning=" + (route.ViolationMeanings.TryGetValue(pair.Key, out string meaning) ? meaning : "weighted-policy-edge"), route.Target,
                        route.Detail + ",blockedEdges=" + route.ViolationCounts[pair.Key] +
                        ",firstBlockedEdge=[" + route.FirstViolations[pair.Key] + "],lastBlockedEdge=[" + pair.Value + "]", route.Dimensions);
        }
        private void MissingContext(int player, string source, string reason, int tribe)
        { Record(player, -1, "context", source + "/" + reason, "", "tribe=" + tribe); }
        internal object BeginAssassin(int player, int tribe, int x, int y, int tx, int ty)
        {
            if (!active) return null;
            uint global = 0;
            if (tribe == 0) { /* No synchronous tribe context is normal for unassigned queries. */ }
            else if (!GameTribeManagerAPI.Instance.IsValidId(tribe)) MissingContext(player, "assassin", "invalid-tribe", tribe);
            else if (GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* live) && live != null && live->r_PlayerIdOwner == player)
                global = live->r_GlobalId;
            else MissingContext(player, "assassin", "missing-or-owner-mismatched-tribe", tribe);
            return new Route { Snapshot = policy(), Epoch = epoch, CallId = ++callSequence, EntryGeneration = captureTimeline.Generation, Player = player, Tribe = tribe, TribeGlobal = global, Role = Role(player, tribe, global),
                StartX = x, StartY = y, TargetX = tx, TargetY = ty,
                Kind = tx < 0 || ty < 0 ? "assassin-flood" : "assassin-weighted", Target = tx + "/" + ty,
                Detail = "tribe=" + tribe + "/" + global + ",start=" + x + "/" + y + ",target=" + tx + "/" + ty };
        }
        internal void AssassinEdge(object token, int player, int from, int to, int direction, bool climb)
        { if (token is Route route) { if (route.Edges > 0 && route.Player != player) route.Invalid = true; route.Player = player; Edge(route, from, to, direction, climb); } }
        internal void EndAssassin(object token, int player, int native, int effective, string outcome, bool cache, int length, int continuation)
        {
            if (!(token is Route route) || !active) return;
            if (player > 0) { if (route.Edges > 0 && route.Player != player) route.Invalid = true; route.Player = player; }
            if (route.TribeGlobal != 0 && (!GameTribeManagerAPI.Instance.IsValidId(route.Tribe) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(route.Tribe, out GameTribe* live) || live == null ||
                live->r_GlobalId != route.TribeGlobal || live->r_PlayerIdOwner != route.Player))
            { route.Invalid = true; MissingContext(route.Player, "assassin-completion", "tribe-identity-changed", route.Tribe); }
            route.Role = Role(route.Player, route.Tribe, route.TribeGlobal);
            bool target = route.Kind != "assassin-flood";
            if (route.Player > 0 && route.Player <= 8 && target) { coverage[route.Player, 6]++; if (cache) coverage[route.Player, 7]++; }
            if (route.Player > 0 && route.Player <= 8) { if (route.Climb > 0) coverage[route.Player, 8]++; if (!target) coverage[route.Player, 9]++; if (continuation != 0) coverage[route.Player, 10]++; }
            route.Kind += cache ? "-cache" : "-fresh";
            route.Detail += ",native=" + native + ",effective=" + effective + ",cache=" + cache + ",continuation=" + continuation + ",outcome=" + outcome;
            route.Detail += ",group=" + Group(route.Player, route.Tribe, route.TribeGlobal, route.Role);
            if (route.FirstFunctionalClimb != null) route.Detail += ",firstFunctionalClimb=[" + route.FirstFunctionalClimb +
                "],lastFunctionalClimb=[" + route.LastFunctionalClimb + "]";
            Record(route.Player, route.Role, route.Kind, "native=" + native + "/effective=" + effective + "/cache=" + cache + "/continuation=" + (continuation != 0),
                route.Target, route.Detail);
            if (route.Player < 1 || route.Player > 8)
            {
                unattributedSearches++;
                Record(0, -1, "unattributed-search", "context-not-assigned/no-route-inspected", route.Target, route.Detail);
                return;
            }
            if (target)
            {
                // Weighted producer supplies nodes including the start; packed output supplies directions.
                route.Detail += ",nodeCount=" + length;
                EndRouteCore(route, effective <= 0 ? "search-negative" : length > 0 ? "decoded" : "positive-without-materialized-route",
                    length > 0 ? length - 1 : -1, length == 1 && route.StartX == route.TargetX && route.StartY == route.TargetY);
            }
        }
        // TEMP_GATE_ROUTE_ACCEPTANCE: count actual validator evaluations separately from route edges.
        public void ObserveAssassinDecision(object token, int player, int from, int to, int direction,
            bool prepared, AssassinTransitionKind movement, bool allowed, int gate, uint global, string evidence)
        {
            if (!active) return;
            Route route = token as Route;
            if (prepared && route != null)
            {
                if (route.Edges > 0 && route.Player != player) route.Invalid = true;
                route.Player = player;
                Edge(route, from, to, direction, movement == AssassinTransitionKind.ClimbUp || movement == AssassinTransitionKind.ClimbDown ||
                    (movement == AssassinTransitionKind.Unknown && evidence != null && evidence.Contains("weightedClimb=True")),
                    movement, allowed, gate, global, evidence);
            }
            else Record(player, route?.Role ?? -1, "assassin-functional-transition",
                "movement=" + movement + "/allowed=" + allowed, route?.Target ?? "",
                "from=" + from + ",to=" + to + ",direction=" + direction + ",gate=" + gate + "/" + global + "," + evidence);
        }
        public void ObserveAssassinStage(object token, int player, string stage, string result, string detail)
        {
            if (!active) return;
            detail += ",stageObservation=[" + TemporaryGateCaptureTimeline.Time(Stopwatch.GetTimestamp()) + "]";
            Route route = token as Route;
            if (route != null)
            {
                if (player > 0) route.Player = player;
                if (stage == "policy-entry") route.PolicyEvidence = detail;
                if (stage == "search-entry") route.SourceEvidence = detail;
                else if (route.SourceEvidence != null) detail = route.SourceEvidence + "," + detail;
                route.Detail += "," + stage + "=[" + detail + "]";
                detail += "," + (route.PolicyEvidence ?? "policyGeneration=unknown");
                detail += ",policyEpoch=" + route.Epoch + ",policyFingerprint=" + (route.Snapshot?.TopologyFingerprint ?? 0);
            }
            detail += ",gateMapEpoch=" + epoch;
            // Source is a finite category; coordinates/identities never enter aggregate keys.
            string source = detail.Contains("source=building-query") ? "building-query" :
                detail.Contains("source=single-unit") ? "single-unit" :
                detail.Contains("source=group-move") ? "group-move" : detail.Contains("source=group-target") ? "group-target" : "unknown";
            if (stage == "search-exit" && result == "weighted-exact-fallback")
            {
                const string marker = "requestReason=";
                int at = detail.LastIndexOf(marker, StringComparison.Ordinal);
                string reason = at < 0 ? "unknown" : detail.Substring(at + marker.Length).Split(',')[0];
                result += "/reason=" + reason;
            }
            Record(player > 0 ? player : route?.Player ?? 0, route?.Role ?? -1, "assassin-" + stage,
                "source=" + source + "/" + result, route?.Target ?? "", detail);
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
