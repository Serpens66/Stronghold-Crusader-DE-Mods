using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    // Observes only the lifetime of a native command call. No unit is tracked after Post.
    internal sealed unsafe class AttackOrderCorrelationDiagnostics
    {
        private readonly ManualLogSource log;
        private readonly GateTopologySnapshotProvider topology;
        // TEMP_GATE_ROUTE_ACCEPTANCE: generic counters remain, new focused rows carry evidence.
        private readonly AiGateDecisionAggregate totals = new AiGateDecisionAggregate { TemporaryCountsOnly = true };
        internal TemporaryGateRouteAcceptance TemporaryAcceptance;
        [ThreadStatic] private static List<Frame> active;
        [ThreadStatic] private static List<HashSet<int>> searches;
        private long errors, unattributedPlayers, scopeMismatches,
            rawTargetPre, rawTargetPost, rawMovePre, rawMovePost,
            rawUnitPre, rawUnitPost, unattributedPrechecks, buildingRoleDifferences, buildingContextFailures;

        private sealed class Frame
        {
            internal string Kind;
            internal int Id, Player, Tribe, Command, Target1, Target2;
            internal readonly HashSet<int> Gates = new HashSet<int>();
            internal long Builders, BuilderSuccess, BuilderFailure, RejectedEdges;
            internal string FirstBuilderCategory, LastBuilderCategory;
            internal long BuilderCategoryChanges;
        }

        internal AttackOrderCorrelationDiagnostics(ManualLogSource log,
            GateTopologySnapshotProvider topology)
        { this.log = log; this.topology = topology; }

        internal void Reset()
        {
            totals.Reset();
            Interlocked.Exchange(ref errors, 0);
            Interlocked.Exchange(ref unattributedPlayers, 0);
            Interlocked.Exchange(ref scopeMismatches, 0);
            Interlocked.Exchange(ref buildingRoleDifferences, 0);
            Interlocked.Exchange(ref buildingContextFailures, 0);
            Interlocked.Exchange(ref rawTargetPre, 0);
            Interlocked.Exchange(ref rawTargetPost, 0);
            Interlocked.Exchange(ref rawMovePre, 0);
            Interlocked.Exchange(ref rawMovePost, 0);
            Interlocked.Exchange(ref rawUnitPre, 0);
            Interlocked.Exchange(ref rawUnitPost, 0);
            Interlocked.Exchange(ref unattributedPrechecks, 0);
            active?.Clear();
            searches?.Clear();
            nativeFrames?.Clear();
        }

        internal void ObserveOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre && args.Phase != EventHookPhase.Post)
            { ObserveUnexpectedPhase("tribe-target", (int)args.Phase); return; }
            bool pre = args.Phase == EventHookPhase.Pre;
            if (pre) Interlocked.Increment(ref rawTargetPre);
            else Interlocked.Increment(ref rawTargetPost);
            int player = ResolveTribePlayer(args.TribeId);
            if (pre && player == 0) ObserveUnattributedPlayer("tribe-target", args.TribeId);
            int command = (int)args.AICommand;
            string detail = "kind=" + args.AICommand + ",a6=" + args.a6;
            if (pre) Push("target", args.TribeId, player, args.TribeId,
                command, args.TargetValue1, args.TargetValue2);
            else Finish("target", args.TribeId, player, args.TribeId, command,
                args.TargetValue1, args.TargetValue2, args.ReturnValue);
            ObserveGateStates(player, pre ? "target-pre" : "target-post",
                command, args.TribeId, args.TargetValue1, args.TargetValue2);
            if (!IsAi(player)) return;
            totals.Record(player, 0, pre ? "tribe-target-pre" : "tribe-target-post",
                pre ? "called" : Result(args.ReturnValue), command, args.TribeId,
                args.TargetValue1, args.TargetValue2, detail);
        }

        internal void ObserveTribeMove(TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre && args.Phase != EventHookPhase.Post)
            { ObserveUnexpectedPhase("tribe-move", (int)args.Phase); return; }
            bool pre = args.Phase == EventHookPhase.Pre;
            if (pre) Interlocked.Increment(ref rawMovePre);
            else Interlocked.Increment(ref rawMovePost);
            int player = ResolveTribePlayer(args.TribeId);
            if (pre && player == 0) ObserveUnattributedPlayer("tribe-move", args.TribeId);
            // TEMP_GATE_ROUTE_ACCEPTANCE: all six identity-verified raid roles.
            try { TemporaryAcceptance?.RaidMove(player, args.TribeId, pre, args.TileX, args.TileY, args.ReturnValue); }
            catch (Exception error) { APIShared.TemporaryGateRouteAcceptanceBridge.ReportFailure("raid-move", error); }
            int command = (int)args.MoveType;
            string detail = "moveType=" + args.MoveType + ",patrol=" + args.IsPatrolPath +
                ",new=" + args.IsNewOrder;
            if (pre) Push("move", args.TribeId, player, args.TribeId,
                command, args.TileX, args.TileY);
            else Finish("move", args.TribeId, player, args.TribeId, command,
                args.TileX, args.TileY, args.ReturnValue);
            ObserveGateStates(player, pre ? "move-pre" : "move-post",
                command, args.TribeId, args.TileX, args.TileY);
            if (!IsAi(player)) return;
            totals.Record(player, 0, pre ? "tribe-move-pre" : "tribe-move-post",
                pre ? "called" : Result(args.ReturnValue), command, args.TribeId,
                args.TileX, args.TileY, detail);
        }

        internal void ObserveUnitMove(UnitMoveHereEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre && args.Phase != EventHookPhase.Post)
            { ObserveUnexpectedPhase("unit-move", (int)args.Phase); return; }
            bool pre = args.Phase == EventHookPhase.Pre;
            if (pre) Interlocked.Increment(ref rawUnitPre);
            else Interlocked.Increment(ref rawUnitPost);
            int player = ResolveUnitPlayer(args.UnitId, out int tribe, out int command, out string identity);
            if (pre && player == 0) ObserveUnattributedPlayer("unit-move", args.UnitId);
            if (pre) Push("unit", args.UnitId, player, tribe, command,
                args.TileX, args.TileY);
            else Finish("unit", args.UnitId, player, tribe, command,
                args.TileX, args.TileY, args.ReturnValue);
            if (!IsAi(player)) return;
            totals.Record(player, 0, pre ? "unit-move-pre" : "unit-move-post",
                 (pre ? "called" : Result(args.ReturnValue)) + "," + identity.Substring(identity.IndexOf("unitType=", StringComparison.Ordinal) >= 0 ? identity.IndexOf("unitType=", StringComparison.Ordinal) : 0), command, tribe,
                args.TileX, args.TileY, "unit=" + args.UnitId + ",unknown=" + args.Unknown + "," + identity);
        }

        internal void ObserveGatePrecheck(int player, int buildingId, int exactGateId,
            bool vanillaExcluded,
            bool policyExcluded, NativeGateSnapshotDecision decision, int owner, int captured, uint globalId)
        {
            int gateId = exactGateId > 0 ? exactGateId : 0;
            if (gateId == 0) Interlocked.Increment(ref unattributedPrechecks);
            Frame frame = Current();
            if (frame != null && frame.Player == player && gateId > 0)
                frame.Gates.Add(gateId);
            if (gateId > 0 && searches != null && searches.Count > 0)
                searches[searches.Count - 1].Add(gateId);
            totals.Record(player, gateId, "gate-precheck",
                "nativeComparison=" + (vanillaExcluded ? "reject" : "accept") +
                ",effectiveComparison=" + (policyExcluded ? "reject" : "accept") +
                ",appliedDecision=" + decision + "," +
                topology.DescribeGateRole(player, gateId, globalId, owner, captured),
                frame?.Command ?? 0, frame?.Tribe ?? 0,
                frame?.Target1 ?? 0, frame?.Target2 ?? 0,
                "rawBuildingId=" + buildingId + ",owner=" + owner +
                ",captured=" + captured + ",gateGlobal=" + globalId);
        }

        internal void ObserveBuilder(int player, SearchDiagnosticContext context, bool completed, bool success,
            long rejectedEdges, ulong fingerprint)
        {
            HashSet<int> checkedGates = null;
            if (searches != null && searches.Count > 0)
            {
                checkedGates = searches[searches.Count - 1];
                searches.RemoveAt(searches.Count - 1);
            }
            else
            {
                Interlocked.Increment(ref errors);
                totals.Record(player, 0, "diagnostic-error", "builder-without-begin",
                    0, 0, 0, 0);
            }
            if (!context.IsAiBuilder) return;
            Frame frame = Current();
            if (frame != null && frame.Player == player)
            {
                frame.Builders++;
                if (completed && success) frame.BuilderSuccess++;
                else frame.BuilderFailure++;
                frame.RejectedEdges += rejectedEdges;
            }
            totals.Record(player, 0, "builder",
                !completed ? "exception" : success ? "positive" : "no-route",
                frame?.Command ?? 0, frame?.Tribe ?? 0,
                frame?.Target1 ?? 0, frame?.Target2 ?? 0,
                "rejectedEdges=" + rejectedEdges + ",fingerprint=" + fingerprint);
            string gateSet = checkedGates == null || checkedGates.Count == 0 ? "none" :
                checkedGates.Count == 1 ? "one:" + FirstGate(checkedGates) :
                "multiple:" + JoinGates(checkedGates);
            int gateKey = checkedGates == null || checkedGates.Count == 0 ? 0 :
                checkedGates.Count == 1 ? FirstGate(checkedGates) : -1;
            totals.Record(player, gateKey, "builder-gates", gateSet,
                frame?.Command ?? 0, frame?.Tribe ?? 0,
                frame?.Target1 ?? 0, frame?.Target2 ?? 0,
                "completed=" + completed + ",success=" + success +
                ",rejectedEdges=" + rejectedEdges);
            Frame order = CurrentTribeOrder(player, frame?.Tribe ?? 0);
            if (order != null)
            {
                if (order.FirstBuilderCategory == null)
                    order.FirstBuilderCategory = gateSet;
                else if (order.LastBuilderCategory != gateSet)
                {
                    order.BuilderCategoryChanges++;
                    totals.Record(player, gateKey, "order-gate-switch",
                        order.LastBuilderCategory + "->" + gateSet,
                        order.Command, order.Tribe, order.Target1, order.Target2);
                }
                order.LastBuilderCategory = gateSet;
            }
        }

        internal void BeginBuilder()
        {
            if (searches == null) searches = new List<HashSet<int>>();
            searches.Add(new HashSet<int>());
        }

        internal void ObserveScopeMismatch(string source, int nativePlayer, int tribePlayer)
        {
            Interlocked.Increment(ref scopeMismatches);
            Interlocked.Increment(ref errors);
            totals.Record(nativePlayer, 0, "scope-" + source,
                "mismatch:tribePlayer=" + tribePlayer, 0, 0, 0, 0);
        }

        internal void ObserveBuildingContext(BuildingSearchPlayerContext context)
        {
            Frame frame = Current();
            if (context.RoleDifference) Interlocked.Increment(ref buildingRoleDifferences);
            if (context.Failure != null)
            {
                Interlocked.Increment(ref buildingContextFailures);
                Interlocked.Increment(ref scopeMismatches);
                Interlocked.Increment(ref errors);
            }
            totals.Record(context.SnapshotOwner, 0, "building-search-context",
                GateDiagnosticClassification.BuildingContextResult(context) +
                ",orderContext=" + (frame == null ? "none" : frame.Kind),
                frame?.Command ?? 0, context.TribeId, frame?.Target1 ?? 0, frame?.Target2 ?? 0,
                "tribeGlobal=" + context.LiveGlobal + ",snapshotGlobal=" + context.SnapshotGlobal +
                ",leaderId=" + context.LeaderId + ",leaderGlobal=" + context.LeaderGlobal +
                ",orderPlayer=" + (frame?.Player ?? 0) +
                ",orderTribe=" + (frame?.Tribe ?? 0) + ",orderId=" + (frame?.Id ?? 0));
        }

        [ThreadStatic] private static List<NativeFrame> nativeFrames;
        private sealed class NativeFrame
        {
            internal string Source, Building;
            internal int Player, Tribe;
            internal Frame Order;
            internal long AssassinBuilders;
        }

        internal void BeginNativeSearch(string source, int player)
        {
            if (nativeFrames == null) nativeFrames = new List<NativeFrame>();
            nativeFrames.Add(new NativeFrame { Source = source, Player = player, Order = Current() });
        }
        internal void ObserveAssassinBuildingSearch(int tribeId, int buildingId,
            int sourceRegion, int rawSearchPlayer)
        {
            if (nativeFrames == null || nativeFrames.Count == 0)
            { ObserveBuildingException(rawSearchPlayer, tribeId, "assassin-building-without-native-scope"); return; }
            NativeFrame frame = nativeFrames[nativeFrames.Count - 1];
            frame.Tribe = tribeId;
            string identity = "buildingIdentity=unavailable";
            if (GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) && building != null)
                identity = "buildingGlobal=" + building->r_GlobalId + ",buildingOwner=" + building->r_PlayerIdOwner;
            frame.Building = "buildingId=" + buildingId + "," + identity +
                ",sourcePcl=" + sourceRegion + ",rawSearchPlayer=" + rawSearchPlayer;
        }

        internal void EndNativeSearch(string source, bool completed, bool success)
        {
            if (nativeFrames == null || nativeFrames.Count == 0 ||
                nativeFrames[nativeFrames.Count - 1].Source != source)
            { ObserveBuildingException(0, 0, "native-context-pair:" + source); return; }
            NativeFrame frame = nativeFrames[nativeFrames.Count - 1];
            nativeFrames.RemoveAt(nativeFrames.Count - 1);
            if (nativeFrames.Count > 0) nativeFrames[nativeFrames.Count - 1].AssassinBuilders += frame.AssassinBuilders;
            if (frame.AssassinBuilders == 0 && source == "Builder") return;
            totals.Record(frame.Player, 0, "native-search-context",
                "source=" + source + ",completed=" + completed + ",result=" +
                (source == "Builder" || source == "CursorCommand" ? (success ? "positive" : "zero") : "void-no-result") +
                ",assassinBuilderObserved=" + (frame.AssassinBuilders > 0),
                frame.Order?.Command ?? 0, frame.Tribe > 0 ? frame.Tribe : frame.Order?.Tribe ?? 0,
                frame.Order?.Target1 ?? 0, frame.Order?.Target2 ?? 0,
                (frame.Building ?? "building=none") + ",assassinBuilderCalls=" + frame.AssassinBuilders + ",noBuilderDoesNotProveCacheHit=True");
        }

        private sealed class AssassinFrame
        {
            internal object Temporary;
            internal AssassinRouteProbe Probe;
            internal Frame Order;
            internal int Tribe, ScopePlayer;
            internal string Building;
            internal int StartX, StartY, TargetX, TargetY, Nodes, Continuation;
            internal string NativeState, Source;
        }

        // TEMP_GATE_ROUTE_ACCEPTANCE: a diagnostic exception never interrupts existing observers.
        private object BeginTemporaryAssassin(int player, int tribe, int x, int y, int tx, int ty)
        {
            try { return TemporaryAcceptance?.BeginAssassin(player, tribe, x, y, tx, ty); }
            catch (Exception error) { APIShared.TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-begin", error); return null; }
        }

        internal object BeginAssassinSearch(RouteTilePolicySnapshot snapshot, int startX,
            int startY, int targetX, int targetY, int maximumNodes, int continuation, string nativeState) =>
            BeginAssassinFrame(snapshot, startX, startY, targetX, targetY, maximumNodes, continuation, nativeState);

        private AssassinFrame BeginAssassinFrame(RouteTilePolicySnapshot snapshot, int startX,
            int startY, int targetX, int targetY, int maximumNodes, int continuation, string nativeState)
        {
            NativeFrame native = nativeFrames != null && nativeFrames.Count > 0 ? nativeFrames[nativeFrames.Count - 1] : null;
            if (native != null) native.AssassinBuilders++;
            Frame order = Current();
            int tribe = native?.Tribe > 0 ? native.Tribe : order?.Tribe ?? 0;
            return new AssassinFrame { Probe = new AssassinRouteProbe(snapshot), Order = order,
                Source = native?.Source ?? "outside-native-scope",
                Tribe = tribe,
                Building = native?.Building, ScopePlayer = native?.Player ?? 0,
                Temporary = BeginTemporaryAssassin(native?.Player ?? order?.Player ?? 0,
                    tribe, startX, startY, targetX, targetY),
                StartX = startX, StartY = startY, TargetX = targetX, TargetY = targetY,
                Nodes = maximumNodes, Continuation = continuation, NativeState = nativeState };
        }

        // TEMP_GATE_ROUTE_ACCEPTANCE: functional evidence shares the existing synchronous frame token.
        internal void ObserveTemporaryAssassinStage(object token, int player, string stage, string result, string detail)
        {
            if (!(token is AssassinFrame frame)) return;
            TemporaryAcceptance?.ObserveAssassinStage(frame.Temporary, player, stage, result, detail);
        }
        internal void ObserveTemporaryAssassinDecision(object token, int player, int from, int to, int direction,
            bool prepared, APIShared.AssassinTransitionKind movement, bool allowed, int gate, uint global, string evidence)
        {
            if (!(token is AssassinFrame frame)) return;
            TemporaryAcceptance?.ObserveAssassinDecision(frame.Temporary, player, from, to, direction, prepared, movement, allowed, gate, global, evidence);
            if (prepared) ObserveAssassinEdge(token, player, from, to, direction,
                movement == APIShared.AssassinTransitionKind.ClimbUp || movement == APIShared.AssassinTransitionKind.ClimbDown, false);
        }
        internal void ObserveAssassinEdge(object token, int player, int fromTile, int toTile,
            int direction, bool climb, bool reportTemporary = true)
        {
            if (!(token is AssassinFrame frame)) return;
            try { if (reportTemporary) TemporaryAcceptance?.AssassinEdge(frame.Temporary, player, fromTile, toTile, direction, climb); }
            catch (Exception error) { APIShared.TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-edge", error); }
            if (!frame.Probe.Observe(player, fromTile, direction, climb, out int gateId)) return;
            totals.Record(player, gateId > 0 ? gateId : 0, "assassin-route-edge",
                (climb ? "climb" : "ground") + ",policy=blocked,attribution=" +
                (gateId > 0 ? "exact-mask-construction" : gateId < 0 ? "ambiguous" : "unknown") +
                ",fingerprint=0x" + frame.Probe.Snapshot.TopologyFingerprint.ToString("X"),
                frame.Order?.Command ?? 0, frame.Tribe,
                frame.TargetX, frame.TargetY, "from=" + fromTile + ",to=" + toTile +
                ",direction=" + direction + ",fingerprint=" + frame.Probe.Snapshot.TopologyFingerprint);
        }

        internal void ObserveAssassinPolicyFiltering(object token, int player, long ground, long climb)
        {
            if (!(token is AssassinFrame frame)) return;
            if (ground != 0) totals.Record(player, 0, "assassin-policy-filter", frame.Source + "/ground",
                frame.Order?.Command ?? 0, frame.Tribe, frame.TargetX, frame.TargetY, value: ground);
            if (climb != 0) totals.Record(player, 0, "assassin-policy-filter", frame.Source + "/climb",
                frame.Order?.Command ?? 0, frame.Tribe, frame.TargetX, frame.TargetY, value: climb);
        }

        internal void EndAssassinSearch(object token, RouteTilePolicySnapshot current,
            int player, int vanillaResult, int effectiveResult, string outcome, bool cacheHit, int routeLength)
        {
            if (!(token is AssassinFrame frame)) return;
            string playerContext = player > 0 ? "builder-resolved" : frame.ScopePlayer > 0 ? "native-scope" : "order-observed";
            if (player <= 0) player = frame.ScopePlayer > 0 ? frame.ScopePlayer : frame.Order?.Player ?? 0;
            try { TemporaryAcceptance?.EndAssassin(frame.Temporary, player, vanillaResult, effectiveResult, outcome, cacheHit, routeLength, frame.Continuation); }
            catch (Exception error) { APIShared.TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-end", error); }
            AssassinRouteProbe probe = frame.Probe;
            bool stable = ReferenceEquals(current, probe.Snapshot);
            foreach (var metric in new[] {
                new KeyValuePair<string, long>("ground", probe.Ground),
                new KeyValuePair<string, long>("climb", probe.Climb),
                new KeyValuePair<string, long>("blocked-ground", probe.BlockedGround),
                new KeyValuePair<string, long>("blocked-climb", probe.BlockedClimb),
                new KeyValuePair<string, long>("unknown", probe.Unknown) })
                if (metric.Value != 0)
                    totals.Record(player, 0, "assassin-edge-total", frame.Source + "/" + metric.Key,
                        frame.Order?.Command ?? 0, frame.Tribe,
                        frame.TargetX, frame.TargetY, value: metric.Value);
            totals.RecordGateState(player, 0, "assassin-native-state", frame.NativeState,
                frame.Order?.Command ?? 0, frame.Tribe, frame.TargetX, frame.TargetY,
                "source=" + frame.Source);
            string blocked = probe.BlockedGround > 0 && probe.BlockedClimb > 0 ? "both" :
                probe.BlockedGround > 0 ? "ground" : probe.BlockedClimb > 0 ? "climb" : "none";
            totals.Record(player, 0, "assassin-search",
                "source=" + frame.Source + ",order=" + (frame.Order?.Kind ?? "outside-order") + ",outcome=" + outcome +
                ",native=" + Result(vanillaResult) + ",effective=" + Result(effectiveResult) +
                ",cache=" + cacheHit + ",blocked=" + blocked + ",snapshotStable=" + stable + ",playerContext=" + playerContext +
                ",fingerprint=0x" + probe.Snapshot.TopologyFingerprint.ToString("X"),
                frame.Order?.Command ?? 0, frame.Tribe,
                frame.TargetX, frame.TargetY,
                "unit=" + (frame.Order?.Kind == "unit" ? frame.Order.Id : 0) +
                ",orderTarget=" + (frame.Order == null ? "none" : frame.Order.Target1 + "/" + frame.Order.Target2) +
                "," + (frame.Building ?? "building=none") + ",edgeCoverage=" + (routeLength > 0 ? "weighted-prepared-route" : "no-materialized-route") +
                ",start=" + frame.StartX + "/" + frame.StartY + ",maximumNodes=" + frame.Nodes +
                ",continuation=" + frame.Continuation + ",rawResult=" + vanillaResult + "/" + effectiveResult +
                ",routeLength=" + routeLength + ",ground=" + probe.Ground + ",climb=" + probe.Climb +
                ",blockedGround=" + probe.BlockedGround + ",blockedClimb=" + probe.BlockedClimb +
                ",unknownEdges=" + probe.Unknown + ",nativeState=" + frame.NativeState);
        }

        internal void ObserveBuildingException(int rawArgument, int tribeId, string cause)
        {
            Interlocked.Increment(ref errors);
            totals.Record(0, 0, "diagnostic-error", "building-context:" + cause,
                0, tribeId, rawArgument, 0);
        }

        internal void ObserveRegionPair(int player, int sourceComponentId,
            int destinationComponentId, int queryMode, int vanillaResult,
            int effectiveResult, string source)
        {
            try
            {
                if (!IsAi(player)) return;
                Frame frame = Current();
                if (frame != null && frame.Player != player) frame = null;
                string stage = sourceComponentId > 0 && sourceComponentId == destinationComponentId
                    ? "same-pcl" : "regions";
                totals.Record(player, 0, "region-pair",
                    "searchStage=" + stage + ",source=" + source + ",vanilla=" + Result(vanillaResult) +
                    ",effective=" + Result(effectiveResult),
                    frame?.Command ?? 0, frame?.Tribe ?? 0,
                    sourceComponentId, destinationComponentId,
                    "queryMode=" + queryMode + ",raw=" + vanillaResult +
                    "/" + effectiveResult + ",orderTarget=" +
                    (frame == null ? "none" : frame.Target1 + "/" + frame.Target2));
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                totals.Record(player, 0, "diagnostic-error",
                    "region-pair:" + ex.GetType().Name, 0, 0,
                    sourceComponentId, destinationComponentId);
            }
        }

        private void ObserveUnexpectedPhase(string source, int phase)
        {
            Interlocked.Increment(ref errors);
            totals.Record(0, 0, "diagnostic-error", "unexpected-phase:" + phase,
                0, 0, 0, 0, source);
        }

        private void ObserveUnattributedPlayer(string source, int id)
        {
            Interlocked.Increment(ref unattributedPlayers);
            totals.Record(0, 0, "unattributed-player", source,
                0, 0, id, 0);
        }

        private void Push(string kind, int id, int player, int tribe, int command,
            int target1, int target2)
        {
            if (active == null) active = new List<Frame>();
            active.Add(new Frame { Kind = kind, Id = id, Player = player,
                Tribe = tribe, Command = command, Target1 = target1, Target2 = target2 });
        }

        private void Finish(string kind, int id, int player, int tribe, int command,
            int target1, int target2, long returnValue)
        {
            Frame frame = Current();
            if (frame == null || frame.Kind != kind || frame.Id != id)
            {
                Interlocked.Increment(ref errors);
                totals.Record(player, 0, "diagnostic-error", "post-without-matching-pre",
                    command, tribe, target1, target2, "kind=" + kind + ",id=" + id);
                return;
            }
            active.RemoveAt(active.Count - 1);
            string category = frame.Gates.Count == 0 ? "none" :
                frame.Gates.Count == 1 ? "one:" + FirstGate(frame.Gates) :
                "multiple:" + JoinGates(frame.Gates);
            if (IsAi(frame.Player))
                totals.Record(frame.Player,
                    kind == "unit" ? (frame.Gates.Count == 1 ? FirstGate(frame.Gates) :
                        frame.Gates.Count > 1 ? -1 : 0) : 0,
                    kind + "-context",
                    category + ",return=" + Result(returnValue),
                    frame.Command, frame.Tribe, frame.Target1, frame.Target2,
                    "id=" + id + ",builders=" + frame.Builders +
                    ",positive=" + frame.BuilderSuccess + ",noRoute=" + frame.BuilderFailure +
                    ",rejectedEdges=" + frame.RejectedEdges +
                    ",firstBuilderGates=" + (frame.FirstBuilderCategory ?? "none") +
                    ",lastBuilderGates=" + (frame.LastBuilderCategory ?? "none") +
                    ",builderCategoryChanges=" + frame.BuilderCategoryChanges);
            Frame parent = Current();
            if (parent != null && parent.Player == player)
            {
                parent.Gates.UnionWith(frame.Gates);
                parent.Builders += frame.Builders;
                parent.BuilderSuccess += frame.BuilderSuccess;
                parent.BuilderFailure += frame.BuilderFailure;
                parent.RejectedEdges += frame.RejectedEdges;
            }
        }

        private static Frame Current() => active != null && active.Count > 0
            ? active[active.Count - 1] : null;
        private static Frame CurrentTribeOrder(int player, int tribe)
        {
            if (active == null) return null;
            for (int index = active.Count - 1; index >= 0; index--)
            {
                Frame frame = active[index];
                if (frame.Player == player && frame.Tribe == tribe &&
                    frame.Kind != "unit") return frame;
            }
            return null;
        }
        private static int FirstGate(HashSet<int> gates)
        { foreach (int gate in gates) return gate; return 0; }
        private static string JoinGates(HashSet<int> gates)
        { var sorted = new List<int>(gates); sorted.Sort(); return string.Join("/", sorted); }
        private static string Result(long value) => value > 0 ? "positive" :
            value == 0 ? "zero" : "negative";
        private static bool IsAi(int player) => player > 0 && player <= 8 &&
            GamePlayerManagerAPI.Instance.IsAIPlayer(player);
        private static int ResolveTribePlayer(int tribeId)
        {
            if (tribeId <= 0 || !GameTribeManagerAPI.Instance.TryGetTribeById(
                tribeId, out GameTribe* tribe) || tribe == null) return 0;
            return tribe->r_PlayerIdOwner;
        }
        private static int ResolveUnitPlayer(int unitId, out int tribeId, out int command, out string identity)
        {
            tribeId = command = 0;
            identity = "identity=unavailable";
            if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(
                unitId, out GameUnit* unit, out _) || unit == null) return 0;
            identity = "unitGlobal=" + unit->r_GlobalId + ",unitType=" + unit->r_UnitChimp +
                ",controlWord=" + (unit->r_ControllableForPlayerId);
            tribeId = unit->r_TribeId;
            command = (int)unit->r_AI_LastIssuedTribeCommand;
            return tribeId > 0 ? ResolveTribePlayer(tribeId) : 0;
        }

        private void ObserveGateStates(int player, string stage, int command,
            int tribe, int target1, int target2)
        {
            if (player <= 0 || player > 8) return;
            try
            {
                foreach (GateLiveStateObservation observation in
                    topology.CaptureGateStates(player))
                    totals.RecordGateState(player, observation.GateId, "gate-live-" + stage,
                        observation.State, command, tribe, target1, target2,
                        observation.Detail);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                totals.Record(player, 0, "diagnostic-error",
                    "gate-live:" + ex.GetType().Name, command, tribe, target1, target2);
            }
        }

        internal void ProcessDeferred() { }
        internal string DescribeCheckpoint()
        {
            for (int player = 1; player <= 8; player++)
                ObserveGateStates(player, "checkpoint", 0, 0, 0, 0);
            AiGateDecisionAggregate.RowSnapshot[] rows = totals.Drain(out var definitions);
            foreach (var definition in definitions)
                Shared.DebugLogHelper.LogInfo(log, "Enemy-gate state: " + definition + ".");
            foreach (var row in rows)
                Shared.DebugLogHelper.LogInfo(log, "Enemy-gate decision: " + row + ".");
            return "observations=" + totals.Observations + ",activeCombinations=" + rows.Length +
                ",newStateDefinitions=" + definitions.Length +
                ",raw(targetPre=" + Interlocked.Read(ref rawTargetPre) +
                ",targetPost=" + Interlocked.Read(ref rawTargetPost) +
                ",tribeMovePre=" + Interlocked.Read(ref rawMovePre) +
                ",tribeMovePost=" + Interlocked.Read(ref rawMovePost) +
                ",unitPre=" + Interlocked.Read(ref rawUnitPre) +
                ",unitPost=" + Interlocked.Read(ref rawUnitPost) +
                "),unattributedPrechecks=" + Interlocked.Read(ref unattributedPrechecks) +
                ",unattributedPlayers=" + Interlocked.Read(ref unattributedPlayers) +
                ",scopeMismatches=" + Interlocked.Read(ref scopeMismatches) +
                ",buildingRoleDifferences=" + Interlocked.Read(ref buildingRoleDifferences) +
                ",buildingContextFailures=" + Interlocked.Read(ref buildingContextFailures) +
                ",diagnosticErrors=" + Interlocked.Read(ref errors);
        }
    }
}
