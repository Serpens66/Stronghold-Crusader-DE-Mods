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
        private readonly AiGateDecisionAggregate totals = new AiGateDecisionAggregate();
        [ThreadStatic] private static List<Frame> active;
        [ThreadStatic] private static List<HashSet<int>> searches;
        private long errors, unattributedPlayers, scopeMismatches,
            rawTargetPre, rawTargetPost, rawMovePre, rawMovePost,
            rawUnitPre, rawUnitPost, unattributedPrechecks;

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
            Interlocked.Exchange(ref rawTargetPre, 0);
            Interlocked.Exchange(ref rawTargetPost, 0);
            Interlocked.Exchange(ref rawMovePre, 0);
            Interlocked.Exchange(ref rawMovePost, 0);
            Interlocked.Exchange(ref rawUnitPre, 0);
            Interlocked.Exchange(ref rawUnitPost, 0);
            Interlocked.Exchange(ref unattributedPrechecks, 0);
            active?.Clear();
            searches?.Clear();
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
            int player = ResolveUnitPlayer(args.UnitId, out int tribe, out int command);
            if (pre && player == 0) ObserveUnattributedPlayer("unit-move", args.UnitId);
            if (pre) Push("unit", args.UnitId, player, tribe, command,
                args.TileX, args.TileY);
            else Finish("unit", args.UnitId, player, tribe, command,
                args.TileX, args.TileY, args.ReturnValue);
            if (!IsAi(player)) return;
            totals.Record(player, 0, pre ? "unit-move-pre" : "unit-move-post",
                pre ? "called" : Result(args.ReturnValue), command, tribe,
                args.TileX, args.TileY, "unit=" + args.UnitId + ",unknown=" + args.Unknown);
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

        internal void ObserveBuilder(int player, bool completed, bool success,
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
            if (!IsAi(player)) return;
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

        internal void ObserveBuildingContext(int rawArgument, int tribeId, int tribePlayer, int usedPlayer)
        {
            Frame frame = Current();
            uint tribeGlobal = 0;
            int liveTribeOwner = 0;
            string identity = "unknown";
            if (tribeId > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null)
            {
                tribeGlobal = tribe->r_GlobalId;
                liveTribeOwner = tribe->r_PlayerIdOwner;
                identity = tribeGlobal == 0 ? "global-id-missing" :
                    liveTribeOwner == tribePlayer ? "verified" : "owner-changed";
            }
            totals.Record(tribePlayer, 0, "building-search-context",
                GateDiagnosticClassification.BuildingContextResult(rawArgument, tribePlayer, usedPlayer) +
                ",liveTribeOwner=" + liveTribeOwner + ",tribeIdentity=" + identity +
                ",orderContext=" + (frame == null ? "none" : frame.Kind),
                frame?.Command ?? 0, tribeId, frame?.Target1 ?? 0, frame?.Target2 ?? 0,
                "tribeGlobal=" + tribeGlobal + ",orderPlayer=" + (frame?.Player ?? 0) +
                ",orderTribe=" + (frame?.Tribe ?? 0) + ",orderId=" + (frame?.Id ?? 0));
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
                totals.Record(player, 0, "region-pair",
                    "source=" + source + ",vanilla=" + Result(vanillaResult) +
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
        private static int ResolveUnitPlayer(int unitId, out int tribeId, out int command)
        {
            tribeId = command = 0;
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(
                unitId, out GameUnit* unit) || unit == null) return 0;
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
                    totals.Record(player, observation.GateId, "gate-live-" + stage,
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
            AiGateDecisionAggregate.RowSnapshot[] rows = totals.Drain();
            foreach (var row in rows)
                Shared.DebugLogHelper.LogInfo(log, "Enemy-gate decision: " + row + ".");
            return "observations=" + totals.Observations + ",activeCombinations=" + rows.Length +
                ",raw(targetPre=" + Interlocked.Read(ref rawTargetPre) +
                ",targetPost=" + Interlocked.Read(ref rawTargetPost) +
                ",tribeMovePre=" + Interlocked.Read(ref rawMovePre) +
                ",tribeMovePost=" + Interlocked.Read(ref rawMovePost) +
                ",unitPre=" + Interlocked.Read(ref rawUnitPre) +
                ",unitPost=" + Interlocked.Read(ref rawUnitPost) +
                "),unattributedPrechecks=" + Interlocked.Read(ref unattributedPrechecks) +
                ",unattributedPlayers=" + Interlocked.Read(ref unattributedPlayers) +
                ",scopeMismatches=" + Interlocked.Read(ref scopeMismatches) +
                ",diagnosticErrors=" + Interlocked.Read(ref errors);
        }
    }
}
