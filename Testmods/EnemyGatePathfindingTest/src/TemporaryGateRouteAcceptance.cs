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
        private long epoch, nextFlush, checkedRoutes, violated, unclear, negativeSearches, lastFailures, unattributedSearches, permittedClimbRoutes, maskOverlapRoutes, suspectedNativeGroundRoutes, unobservedPublications, aiBuilderResults, aiBuilderNegative, aiBuilderFilteredNegative;
        private long expectedAiBuilders = -1, expectedFilteredNegative = -1;
        internal NativeAssassinDiagnosticHooks NativeDiagnostics;
        internal void StampExecution(ref NativeAssassinMovementDiagnostics.Sample sample)
        { sample.Epoch = epoch; sample.Generation = captureTimeline.Generation; sample.Snapshot = policy(); }
        [ThreadStatic] private static Route currentPublication;
        private long nativeBuilders, nativeSecondary, nativeFallbacks, nativeUnassigned, nativeFinalMatched,
            nativeGroundEdges, nativeClimbEdges, nativeUnknownEdges, nativeWithoutReconstruction, nativeIncomplete;
        private bool nativeHookConfirmed;
        private sealed class Route
        {
            internal Route PreviousPublication;
            internal NativeAssassinTrace NativeTrace;
            internal int Unit, NativeDeferredCount;
            internal bool NativeDeferDisabled;
            internal RouteTilePolicySnapshot Snapshot;
            internal long Epoch, CallId, Started = Stopwatch.GetTimestamp(), EntryGeneration;
            internal readonly Dictionary<string, GateUse> GateUses = new Dictionary<string, GateUse>();
            internal int Player, Role = -1, Tribe;
            internal int StartX, StartY, TargetX, TargetY;
            internal uint TribeGlobal;
            internal bool Invalid, GateIdentityInvalid;
            internal long Edges, Climb, GroundViolations, AllowedClimb, UnknownOverlap, SuspectedNativeGround, PublishedWeightedEdges;
            internal FinalEdgeEvidence? PendingFinalEdge;
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
        private struct FinalEdgeEvidence
        {
            internal int From, To, Direction, Gate;
            internal uint Global;
            internal AssassinTransitionKind Movement;
            internal bool Allowed;
            internal string Detail;
        }
        private sealed class GateUse
        {
            internal int Gate;
            internal string Movement, First, Last;
            internal long Edges, GroundViolations, AllowedClimb, UnknownOverlap, SuspectedNativeGround;
            internal bool PolicyAllowed;
        }
        internal void ObserveCapture(int building, int player, bool post)
        {
            if (!active) return;
            long at = Stopwatch.GetTimestamp();
            uint global = captureTimeline.ObserveCapture(building, player, at, post);
            Record(player, -1, "capture-event", "gate=" + building + "/global=" + global + "/phase=" + (post ? "post" : "pre"), "",
                TemporaryGateCaptureTimeline.Time(at) + ",capturingPlayer=" + player + ",postIsNotNativeSuccessProof=true,postTiming=extender-delayed-notification");
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
            NativeDiagnostics?.SetActive(true);
            currentPublication = null;
            counts.Reset(); Array.Clear(coverage, 0, coverage.Length); epoch++; active = true;
            nativeBuilders = nativeSecondary = nativeFallbacks = nativeUnassigned = nativeFinalMatched =
                nativeGroundEdges = nativeClimbEdges = nativeUnknownEdges = nativeWithoutReconstruction = nativeIncomplete = 0;
            captureTimeline.Reset(epoch); callSequence = 0;
            checkedRoutes = violated = unclear = negativeSearches = unattributedSearches = permittedClimbRoutes = maskOverlapRoutes = suspectedNativeGroundRoutes = unobservedPublications = aiBuilderResults = aiBuilderNegative = aiBuilderFilteredNegative = 0; nextFlush = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
            expectedAiBuilders = expectedFilteredNegative = -1;
            lastFailures = TemporaryGateRouteAcceptanceBridge.Failures;
            Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE begin epoch=" + epoch +
                ",format=10,emissionTimeIsNotObservationTime=true,monoFrequency=" + Stopwatch.Frequency + ",intervalSeconds=60,weightedLengthUnit=nodes,packedLengthUnit=edges," +
                "publicationContext=frame-or-owned-buffer,noExtraSearches=true,nativeTraceInstalled=" + (NativeDiagnostics?.Installed == true) + "," +
                "fixesLoaded=" + Chainloader.PluginInfos.ContainsKey("fixes") + ",fixesLiveLordOverrides=unknown,fixesHookEffect=unmeasured");
        }
        internal void End()
        {
            if (!active) return;
            NativeDiagnostics?.SetActive(false);
            currentPublication = null;
            Flush(true); active = false; counts.Reset(); Array.Clear(coverage, 0, coverage.Length); captureTimeline.Reset(epoch);
        }
        internal void Deferred() { if (active && Stopwatch.GetTimestamp() >= nextFlush) Flush(false); }
        private void Flush(bool final)
        {
            nextFlush = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
            if (NativeDiagnostics != null) Shared.DebugLogHelper.LogInfo(log,
                "TEMP_GATE_ROUTE_ACCEPTANCE native-observer coverage: installed=" + NativeDiagnostics.Installed +
                ",callbackErrors=" + NativeDiagnostics.Failures + ",reconstructionsOutsideObservedBuilder=" + NativeDiagnostics.UnscopedReconstructions +
                ",abandonedBuilders=" + NativeDiagnostics.AbandonedBuilders +
                ",processBuilderEntries=" + NativeDiagnostics.BuilderEntries + ",processBuilderExits=" + NativeDiagnostics.BuilderExits +
                ",builders=" + nativeBuilders + ",secondaryGroundPasses=" + nativeSecondary + ",secondaryFallbacks=" + nativeFallbacks +
                ",unassignedBuilders=" + nativeUnassigned + ",withoutObservedReconstruction=" + nativeWithoutReconstruction +
                ",incompleteChains=" + nativeIncomplete + ",fullyMatchedFinalOutputs=" + nativeFinalMatched +
                ",matchedGroundEdges=" + nativeGroundEdges + ",matchedClimbEdges=" + nativeClimbEdges + ",matchedUnknownEdges=" + nativeUnknownEdges +
                ",processCallbackCount=" + NativeDiagnostics.CallbackCount + ",processCallbackTicks=" + NativeDiagnostics.CallbackTicks +
                ",maximumCallbackTicks=" + NativeDiagnostics.MaximumCallbackTicks + ",tickFrequency=" + Stopwatch.Frequency +
                ",timingExcludesNativeManagedTransition=true" +
                ",zeroCountsMeanNotObserved=true");
            if (NativeDiagnostics != null) foreach (string executionRow in NativeDiagnostics.Execution.Drain()) Shared.DebugLogHelper.LogInfo(log, executionRow);
            foreach (string row in counts.Drain()) Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE aggregate emittedUtc=" + DateTime.UtcNow.ToString("O") + "," + row);
            Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE summary epoch=" + epoch + ",final=" + final +
                ",observations=" + counts.Total + ",checked=" + checkedRoutes + ",violated=" + violated +
                ",checkedAllowedRoutes=" + (checkedRoutes - violated) + ",confirmedGroundViolations=" + violated +
                ",counterfactualVanillaRoute=not-measured,nativeReturnBasis=active-hooks" +
                ",unclear=" + unclear + ",unobservedPublications=" + unobservedPublications +
                ",aiBuilderResults=" + aiBuilderResults + ",aiBuilderNegative=" + aiBuilderNegative + ",aiBuilderFilteredNegative=" + aiBuilderFilteredNegative +
                ",builderCounterAgreement=" + (expectedAiBuilders < 0 ? "not-compared" : expectedAiBuilders == aiBuilderResults && expectedFilteredNegative == aiBuilderFilteredNegative ? "PASS" : "FAIL") +
                ",expectedAiBuilders=" + expectedAiBuilders + ",expectedFilteredNegative=" + expectedFilteredNegative + ",suspectedNativeGroundRoutes=" + suspectedNativeGroundRoutes + ",permittedClimbRoutes=" + permittedClimbRoutes + ",maskOverlapRoutes=" + maskOverlapRoutes + ",unattributedSearches=" + unattributedSearches + ",negativeSearches=" + negativeSearches + ",adapterFailures=" + (TemporaryGateRouteAcceptanceBridge.Failures - lastFailures) +
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
            { unobservedPublications++; Record(player, -1, "publication-context", "not-inspected/" + orderContext, "", "unit=" + unit + "/" + unitGlobal + ",coverage=unobserved,noRouteFailureImplied=true"); return null; }
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
            var publication = new Route { Snapshot = policy(), Epoch = epoch, CallId = ++callSequence, EntryGeneration = captureTimeline.Generation, Player = player, Tribe = tribe, TribeGlobal = tribeGlobal, Role = role,
                Unit = unit, PreviousPublication = currentPublication,
                TargetBuilding = targetBuilding, TargetBuildingGlobal = targetGlobal,
                AttackBuilding = attackBuilding, AttackBuildingGlobal = attackGlobal,
                Dimensions = ",group=" + Group(player, tribe, tribeGlobal, role),
                StartX = x, StartY = y, TargetX = tx, TargetY = ty,
                Kind = assassin ? "assassin-published" : "raid-published", Target = tx + "/" + ty,
                Detail = "group=" + Group(player, tribe, tribeGlobal, role) + ",tribe=" + tribe + "/" + tribeGlobal + ",unit=" + unit + "/" + unitGlobal +
                    ",type=" + type + ",start=" + x + "/" + y + ",target=" + tx + "/" + ty + "," + orderContext +
                    ",publicationOrigin=final-builder-output,edgeClassification=packed-unknown-unless-exact-weighted-validation," +
                    "endpointBuilding=" + targetBuilding + "/" + targetGlobal + ",attackBuilding=" + attackBuilding + "/" + attackGlobal +
                    ",endpointIsNotAttackTargetProof=true,fixesOrderOrigin=not-attributed" };
            currentPublication = publication;
            return publication;
        }
        internal void BeginNativeTrace(NativeAssassinTrace trace)
        {
            trace.Epoch = epoch;
            Route route = currentPublication;
            if (route == null || route.Epoch != epoch || route.Unit != trace.Unit || trace.Unit == 0 ||
                route.StartX != trace.X || route.StartY != trace.Y || route.TargetX != trace.TX || route.TargetY != trace.TY ||
                (route.NativeTrace != null && !route.NativeTrace.Closed)) return;
            trace.Publication = route; trace.Player = route.Player; route.NativeTrace = trace;
        }
        internal void EndNativeTrace(NativeAssassinTrace trace)
        {
            if (trace.Epoch != epoch) trace.Invalid = true;
            nativeBuilders++;
            if (trace.Secondary) nativeSecondary++;
            if (trace.SecondaryFallback) nativeFallbacks++;
            if (trace.Publication == null) nativeUnassigned++;
            if (!trace.CollectEdges)
            { Record(0, -1, "ordinary-builder-not-traced", "result=" + (trace.Result > 0 ? "positive" : "nonpositive"), "", "unrelated-route-edges-not-collected"); return; }
            if (trace.Reconstructions == 0) nativeWithoutReconstruction++;
            if (trace.Invalid || !trace.Closed || (trace.Result > 0 && !trace.Complete)) nativeIncomplete++;
            if (!nativeHookConfirmed && trace.Publication != null && !trace.Invalid)
            {
                nativeHookConfirmed = true;
                Record(trace.Player, -1, "native-hook-confirmed", "synchronous-builder", trace.TX + "/" + trace.TY,
                    "unit=" + trace.Unit + ",start=" + trace.X + "/" + trace.Y + "," + trace.Summary);
            }
            Record(trace.Player, -1, "native-reconstruction-chain",
                "secondary=" + trace.Secondary + "/fallback=" + trace.SecondaryFallback + "/mode=" + trace.Mode +
                "/complete=" + trace.Complete + "/invalid=" + trace.Invalid + "/buffer=" + trace.BufferKind,
                trace.TX + "/" + trace.TY, trace.Summary + ",unit=" + trace.Unit +
                ",publicationAssigned=" + (trace.Publication != null) + ",executionIsNotPhysicalMovementProof=true");
        }
        private void FlushNativeEdges(Route route, bool proof)
        {
            NativeAssassinTrace trace = route.NativeTrace;
            int count = route.NativeDeferredCount; route.NativeDeferredCount = 0;
            if (proof && count > 0) nativeFinalMatched++;
            for (int i = 0; i < count; i++)
            {
                NativeTraceEdge edge = trace.Selected[trace.Selected.Count - 1 - i];
                AssassinTransitionKind? movement = !proof || edge.Movement == NativeTraceMovement.Unknown ? (AssassinTransitionKind?)null :
                    edge.Movement == NativeTraceMovement.Ground ? AssassinTransitionKind.Ground :
                    edge.Movement == NativeTraceMovement.ClimbUp ? AssassinTransitionKind.ClimbUp : AssassinTransitionKind.ClimbDown;
                if (proof)
                {
                    if (movement == AssassinTransitionKind.Ground) nativeGroundEdges++;
                    else if (movement.HasValue) nativeClimbEdges++;
                    else nativeUnknownEdges++;
                }
                int gate = route.Snapshot.DiagnosticGateEdges?.Resolve(edge.From, edge.Direction) ?? 0;
                uint global = gate > 0 && route.Snapshot.GateIdentities.TryGetValue(gate, out var identity) ? identity.Global : 0;
                Edge(route, edge.From, edge.To, edge.Direction,
                    movement == AssassinTransitionKind.ClimbUp || movement == AssassinTransitionKind.ClimbDown,
                    movement, movement.HasValue, gate, global,
                    proof && gate > 0 ? "movementSource=observed-native-reconstruction,trace=" + trace.Sequence + ",mode=" + trace.Mode +
                        ",connectionBits=" + edge.ConnectionBits + ",sourceFlags=" + edge.SourceFlags.ToString("X") + ",targetFlags=" + edge.TargetFlags.ToString("X") +
                        ",nativeTransition=" + edge.Movement + ",physicalExecution=not-observed" : null);
            }
        }
        public void RouteEdge(object token, int from, int to, int direction)
        {
            if (token is Route route && route.PendingFinalEdge.HasValue)
            {
                FinalEdgeEvidence proof = route.PendingFinalEdge.Value;
                route.PendingFinalEdge = null;
                if (proof.From == from && proof.To == to && proof.Direction == direction)
                {
                    FlushNativeEdges(route, false); route.NativeDeferDisabled = true;
                    route.PublishedWeightedEdges++;
                    Edge(route, from, to, direction,
                        proof.Movement == AssassinTransitionKind.ClimbUp || proof.Movement == AssassinTransitionKind.ClimbDown,
                        proof.Movement, proof.Allowed, proof.Gate, proof.Global, proof.Detail);
                    return;
                }
            }
            if (token is Route native && !native.NativeDeferDisabled && native.NativeTrace != null)
            {
                if (native.NativeTrace.Match(native.NativeDeferredCount, from, to, direction, out _))
                { native.NativeDeferredCount++; return; }
                FlushNativeEdges(native, false); native.NativeDeferDisabled = true;
            }
            Edge(token, from, to, direction, false);
        }
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
            GateUse gateUse = null;
            if (diagnosticGate > 0)
            {
                string movementName = functionalMovement.HasValue ? functionalMovement.Value.ToString() :
                    route.Kind.EndsWith("-published", StringComparison.Ordinal) ? "packed-movement-unproven" : "weighted-movement-unproven";
                if (!route.Snapshot.GateIdentities.TryGetValue(diagnosticGate, out var diagnosticIdentity))
                { route.Invalid = true; return; }
                route.SnapshotGateIdentities[diagnosticGate] = diagnosticIdentity;
                string key = diagnosticGate + "/" + movementName + "/groundMaskAllowed=" + policyAllowed;
                if (!route.GateUses.TryGetValue(key, out GateUse use))
                    route.GateUses.Add(key, use = new GateUse { Gate = diagnosticGate, Movement = movementName, PolicyAllowed = policyAllowed });
                gateUse = use;
                string edge = "from=" + from + ",to=" + to + ",direction=" + direction + ",index=" + (route.Edges - 1);
                if (use.First == null) use.First = edge;
                use.Last = edge; use.Edges++;
            }
            if (policyAllowed) return;
            int gate = route.Snapshot.EdgeOwners?[route.Player]?.Resolve(from, direction) ?? 0;
            bool packed = route.Kind.EndsWith("-published", StringComparison.Ordinal) && !functionalMovement.HasValue;
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
                if (gateUse != null && gateUse.Gate == gate) gateUse.AllowedClimb++;
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
            if (gateUse != null && gateUse.Gate == gate)
            {
                if (provenGround) gateUse.GroundViolations++; else gateUse.UnknownOverlap++;
                if (packed && movement == AssassinTransitionKind.Ground && stableGate) gateUse.SuspectedNativeGround++;
            }
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
                    ((uint)tiles.GetTilePropertyFlag(to)).ToString("X") + ",nativeAcceptedBranch=" +
                    (functionalEvidence != null && functionalEvidence.StartsWith("movementSource=observed-native-reconstruction", StringComparison.Ordinal)
                        ? "observed-reconstruction/movement-unresolved" : "not-observed");
                if (gateGlobal != 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(gate, out GameBuilding* live) && live != null)
                    detail += ",gateTypeRaw=" + (int)live->r_BuildingType + ",gateOwnerRaw=" + live->r_PlayerIdOwner +
                        ",gateCapturerRaw=" + live->r_CapturedByPlayerId + ",gateAliveRaw=" + (int)live->r_AliveState;
            }
            if (!route.FirstViolations.ContainsKey(gate)) route.FirstViolations[gate] = detail;
            route.Violations[gate] = detail;
            route.ViolationCounts.TryGetValue(gate, out long count); route.ViolationCounts[gate] = count + 1;
        }
        public void EndRoute(object token, string status, int result)
        {
            Route route = token as Route;
            try
            {
                if (route != null)
                {
                    bool nativeProof = status.StartsWith("decoded", StringComparison.Ordinal) &&
                        status.Contains("exactWeightedPublished=False") && route.NativeDeferredCount == result &&
                        result > 0 && ReferenceEquals(route.Snapshot, policy()) && route.Epoch == epoch;
                    FlushNativeEdges(route, nativeProof);
                    if (route.NativeTrace != null)
                    {
                        route.Detail += ",nativeChain=[" + route.NativeTrace.Summary + "]";
                        status = status.Replace("secondaryGroundPassExecution=not-observed", "secondaryGroundPassExecution=" +
                            (route.NativeTrace.Invalid || !route.NativeTrace.Closed ? "incomplete" : route.NativeTrace.Secondary ? "executed" : "not-executed"));
                    }
                }
                EndRouteCore(token, status, result, false);
            }
            finally { if (route != null && ReferenceEquals(currentPublication, route)) currentPublication = route.PreviousPublication; }
        }
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
                string gateVerdict = route.Invalid || !ReferenceEquals(route.Snapshot, policy()) || route.Epoch != epoch
                    ? "not-inspected/context-changed" : use.PolicyAllowed ? "access-permitted"
                    : use.GroundViolations > 0 ? "confirmed-ground-violation"
                    : use.SuspectedNativeGround > 0 ? "suspected-native-ground-overlap"
                    : use.UnknownOverlap > 0 ? "movement-unproven" : use.AllowedClimb > 0 ? "climb-permitted" : "not-inspected";
                Record(route.Player, route.Role, "gate-route-timing",
                    "gate=" + use.Gate + "/global=" + identity.Global + "/phase=" + phase + "/source=" + route.Kind + "/gateVerdict=" + gateVerdict + "/" + pair.Key,
                    route.Target, route.Detail + ",routeStatus=" + status + ",routeVerdict=" + verdict + ",gateEdges=" + use.Edges +
                    ",gateGroundViolations=" + use.GroundViolations + ",gateSuspectedNativeGround=" + use.SuspectedNativeGround +
                    ",gateAllowedClimb=" + use.AllowedClimb + ",gateUnknownOverlap=" + use.UnknownOverlap +
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
                route.Detail + ",validatedWeightedPublishedEdges=" + route.PublishedWeightedEdges + ",confirmedGroundEdges=" + route.GroundViolations + ",suspectedNativeGroundEdges=" + route.SuspectedNativeGround + ",permittedClimbEdges=" + route.AllowedClimb + ",unresolvedOverlapEdges=" + route.UnknownOverlap + ",expectedEdges=" + expectedEdges + ",observedEdges=" + route.Edges + ",climbEdges=" +
                (route.Kind.EndsWith("-published", StringComparison.Ordinal) ? "unknown" : route.Climb.ToString()), route.Dimensions);
            if (complete || verdict == "unclear:mask-overlap-movement-unproven" || verdict == "unclear:suspected-ground-overlap-native-branch-unproven")
                foreach (var pair in route.Violations)
                    Record(route.Player, route.Role, "mask-overlap", "gate=" + pair.Key + "/attribution=" + (pair.Key > 0 ? "exact" : pair.Key < 0 ? "ambiguous" : "unknown") +
                        "/meaning=" + (route.ViolationMeanings.TryGetValue(pair.Key, out string meaning) ? meaning : "weighted-policy-edge"), route.Target,
                        route.Detail + ",blockedEdges=" + route.ViolationCounts[pair.Key] +
                        ",firstBlockedEdge=[" + route.FirstViolations[pair.Key] + "],lastBlockedEdge=[" + pair.Value + "]", route.Dimensions);
        }
        internal void CompareBuilderTotals(long queries, long filteredNegative)
        {
            if (!active) return;
            expectedAiBuilders = queries; expectedFilteredNegative = filteredNegative;
            if (queries != aiBuilderResults || filteredNegative != aiBuilderFilteredNegative)
                TemporaryGateRouteAcceptanceBridge.ReportFailure("ai-builder-counter-mismatch");
        }
        // TEMP_GATE_ROUTE_ACCEPTANCE: one row for every AI builder completion, using entry-bound contexts only.
        internal void ObserveAiBuilderResult(int player, string source, string unitType, bool completed, bool success,
            long rejectedEdges, int command, int tribe, string target, string detail)
        {
            if (!active) return;
            aiBuilderResults++;
            if (completed && !success) { aiBuilderNegative++; if (rejectedEdges > 0) aiBuilderFilteredNegative++; }
            Record(player, -1, "ai-builder-result", "source=" + source + "/unitType=" + unitType +
                "/result=" + (!completed ? "incomplete" : success ? "positive" : "no-route") +
                "/filterTouched=" + (rejectedEdges > 0) + "/command=" + command,
                target, detail + ",tribe=" + tribe + ",rejectedEdges=" + rejectedEdges + ",gateCauseProven=false,aiBuilderCall=" + aiBuilderResults +
                ",policyGeneration=" + captureTimeline.Generation + "," + TemporaryGateCaptureTimeline.Time(Stopwatch.GetTimestamp()));
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
                Kind = tx < 0 || ty < 0 ? "assassin-flood" : "assassin-target-search", Target = tx + "/" + ty,
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
            if (target && length > 0) route.Kind = "assassin-weighted";
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
                if (length <= 0 && effective > 0 && outcome == "weighted-disabled")
                {
                    Record(route.Player, route.Role, "native-target-result", "positive/output-observed-separately", route.Target, route.Detail);
                    return;
                }
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
            // TEMP_GATE_ROUTE_ACCEPTANCE: optional evidence immediately precedes the existing RouteEdge callback.
            // Never count this notification as a second materialized edge; never retain it across callbacks.
            if (!prepared && route != null && route.Kind.EndsWith("-published", StringComparison.Ordinal) &&
                player == route.Player && evidence != null &&
                evidence.StartsWith("movementSource=exact-published-weighted-post-validation,", StringComparison.Ordinal))
            {
                route.PendingFinalEdge = new FinalEdgeEvidence { From = from, To = to, Direction = direction,
                    Movement = movement, Allowed = allowed, Gate = gate, Global = global, Detail = evidence };
                return;
            }
            if (prepared && route != null)
            {
                if (route.Kind.EndsWith("-published", StringComparison.Ordinal)) route.PublishedWeightedEdges++;
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
                detail += "," + (route.PolicyEvidence ?? (route.Snapshot == null || ReferenceEquals(route.Snapshot, RouteTilePolicySnapshot.Empty) ? "policyGeneration=unknown" :
                    "policyGeneration=" + route.EntryGeneration + ",generationSource=route-entry-snapshot"));
                detail += ",policyEpoch=" + route.Epoch + ",policyFingerprint=" + (route.Snapshot?.TopologyFingerprint ?? 0);
            }
            detail += ",gateMapEpoch=" + epoch;
            // Source is a finite category; coordinates/identities never enter aggregate keys.
            string source = detail.Contains("source=building-query") ? "building-query" :
                detail.Contains("source=single-unit") ? "single-unit" :
                detail.Contains("source=group-move") ? "group-move" : detail.Contains("source=group-target") ? "group-target" : "unknown";
            if (stage == "route-validation")
            {
                string phase = DiagnosticValue(detail, "validationPhase");
                result += "/phase=" + phase;
                if (route?.Snapshot != null && int.TryParse(DiagnosticValue(detail, "fromTile"), out int fromTile) &&
                    int.TryParse(DiagnosticValue(detail, "direction"), out int direction) && fromTile >= 0 && direction >= 0 && direction <= 7)
                {
                    int gate = route.Snapshot.DiagnosticGateEdges?.Resolve(fromTile, direction) ?? 0;
                    if (gate > 0 && route.Snapshot.GateIdentities.TryGetValue(gate, out var identity))
                    { result += "/gate=" + gate + "/global=" + identity.Global; detail += ",validationGate=" + gate + "/" + identity.Global; }
                    else detail += ",validationGate=not-attributed";
                }
                else detail += ",validationGate=not-attributed";
            }
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
        // TEMP_GATE_ROUTE_ACCEPTANCE: exact quarry buckets retain every target's outcome;
        // other building types stay compact. No extra search or persistent order history.
        private static string RaidTargetDimensions(int building, uint global)
        {
            var api = GameBuildingManagerAPI.Instance;
            if (global == 0 || !api.IsValidId(building) ||
                !api.TryGetBuildingById(building, out GameBuilding* live) || live == null || live->r_GlobalId != global)
                return ",targetIdentity=unverified,targetType=unknown";
            int type = (int)live->r_BuildingType;
            int owner = live->r_PlayerIdOwner;
            if (live->r_GlobalId != global)
                return ",targetIdentity=changed,targetType=unknown";
            return ",targetIdentity=verified,targetType=" + type + ",targetOwner=" + owner +
                (type == 20 ? ",quarry=" + building + "/" + global : ""); // installed STRUCT_QUARRY=20
        }
        internal void RaidOrder(int player, int tribe, int command, int building, uint global, bool pre, long result, bool skipAtObserver)
        {
            if (!active || (command != 9 && command != 36 && command != 38)) return;
            var api = GameTribeManagerAPI.Instance;
            if (!api.IsValidId(tribe) || !api.TryGetTribeById(tribe, out GameTribe* live) || live == null) return;
            uint tribeGlobal = live->r_GlobalId;
            int role = Role(player, tribe, tribeGlobal);
            if (role < 0) return;
            Record(player, role, pre ? "raid-order-pre" : "raid-order-post", "command=" + command,
                building + "/" + global, "tribe=" + tribe + "/" + tribeGlobal + ",building=" + building + "/" + global +
                ",return=" + (pre ? "not-returned" : result.ToString()) + ",skipAtObserver=" + skipAtObserver +
                ",origin=event-source-unproven,preIsNotExecutionProof=true,postIsNotAttackPointProof=true," +
                TemporaryGateCaptureTimeline.Time(Stopwatch.GetTimestamp()), RaidTargetDimensions(building, global));
        }
        public void Raid(int player, int role, int tribe, uint tribeGlobal, int building, uint buildingGlobal, string stage, string result, string detail)
        {
            if (!active) return;
            if (Role(player, tribe, tribeGlobal) != role || role < 0) { unclear++; Record(player, -1, "raid-identity", "unclear", "", detail); return; }
            coverage[player, role]++;
            string dimensions = RaidTargetDimensions(building, buildingGlobal);
            string origin = stage == "raid-command" ? "outside-this-retarget" :
                stage == "raid-replacement-command" ? "raid-fix-replacement" : "raid-fix-outcome";
            bool firstFieldGuard = result == "NoAttackPoint" && detail != null &&
                ("," + detail + ",").Contains(",validation=nativeFirstBuildingGateFailed,");
            Record(player, role, stage, result, building + "/" + buildingGlobal,
                "tribe=" + tribe + "/" + tribeGlobal + ",building=" + building + "/" + buildingGlobal +
                ",orderOrigin=" + origin + ",nativeMovementAssignment=" + (firstFieldGuard ? "skipped-by-first-building-field-guard" : "not-proven") +
                ",counterfactualWithoutRaidFix=not-measured," + detail, dimensions);
        }
    }
}
