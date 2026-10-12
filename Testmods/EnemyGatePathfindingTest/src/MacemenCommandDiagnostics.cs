// TEMP_MACEMEN_GATE_DIAGNOSTICS: synchronous, read-only command evidence. No unit polling.
using System;
using System.Diagnostics;
using System.Collections.Generic;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class MacemenCommandDiagnostics
    {
        private readonly ManualLogSource log;
        private readonly Func<RouteTilePolicySnapshot> policy;
        private readonly Func<NativeGateAccessSnapshot> access;
        private readonly Func<int, MacemenUnit> unitReader, leaderReader;
        private readonly MacemenDiagnosticAggregate aggregate = new MacemenDiagnosticAggregate();
        [ThreadStatic] private static Frame[] frames;
        [ThreadStatic] private static int depth;
        [ThreadStatic] private static Search[] searches;
        [ThreadStatic] private static int searchDepth;
        private bool enabled;
        private long calls, nextFlush, pre, post, skipped, missingPost, unmatchedPost, identityUnknown, errors, workTicks, callbacks;
        private int epoch;
        private long epochStarted;
        private DateTime epochUtc;
        private readonly Dictionary<string, long> failures = new Dictionary<string, long>();
        private readonly Dictionary<int, long> unattributedComparisons = new Dictionary<int, long>();

        private struct Frame
        {
            internal EventHookBase Args;
            internal MacemenStage Stage;
            internal int Id, Command, T1, T2;
            internal long Call;
            internal MacemenUnit Unit;
            internal MacemenGate Gate;
            internal bool Relevant, TargetOrderContext;
            internal TribeIssueOrderWithTargetEventArgs TargetArgs;
            internal int MoveType;
        }
        private struct Search
        {
            internal Frame Frame;
            internal int Player, Kind;
            internal RouteTilePolicySnapshot Policy;
            internal bool Eligible;
        }
        internal MacemenCommandDiagnostics(ManualLogSource log, Func<RouteTilePolicySnapshot> policy,
            Func<NativeGateAccessSnapshot> access, Func<int, MacemenUnit> unitReader = null,
            Func<int, MacemenUnit> leaderReader = null)
        { this.log = log; this.policy = policy; this.access = access;
          this.unitReader = unitReader; this.leaderReader = leaderReader; }
        internal long Observations => aggregate.Total;
        internal long Skipped => skipped;
        internal long MissingPost => missingPost;
        internal long UnmatchedPost => unmatchedPost;
        internal long Errors => errors;
        internal int ActiveCombinations => aggregate.ActiveCombinations;

        internal void Begin(int epoch)
        {
            this.epoch = epoch; enabled = true; ClearFrames(); aggregate.Reset();
            epochStarted = Stopwatch.GetTimestamp(); epochUtc = DateTime.UtcNow;
            calls = pre = post = skipped = missingPost = unmatchedPost = identityUnknown = errors = workTicks = callbacks = 0;
            failures.Clear();
            unattributedComparisons.Clear();
            nextFlush = Stopwatch.GetTimestamp() + 60L * Stopwatch.Frequency;
        }
        internal void End()
        { if (!enabled) return; CleanupPending(); Flush(true); enabled = false; ClearFrames(); }
        private static void ClearFrames()
        { if (frames != null) Array.Clear(frames, 0, frames.Length); depth = 0;
          if (searches != null) Array.Clear(searches, 0, searches.Length); searchDepth = 0; }
        internal void Deferred()
        {
            if (!enabled) return;
            // This publisher runs after synchronous native work: no frame may escape its call.
            CleanupPending();
            if (Stopwatch.GetTimestamp() >= nextFlush)
            { Flush(false); nextFlush = Stopwatch.GetTimestamp() + 60L * Stopwatch.Frequency; }
        }
        private void CleanupPending()
        {
            while (depth > 0)
            {
                Frame frame = frames[--depth]; frames[depth] = default;
                if (frame.Args?.SkipOriginalFunction == true) skipped++; else missingPost++;
            }
            if (searchDepth > 0) { errors += searchDepth; Array.Clear(searches, 0, searchDepth); searchDepth = 0; }
        }
        internal void TargetOrder(TribeIssueOrderWithTargetEventArgs args)
            => Observe(args, MacemenStage.TargetOrder, args.TribeId, (int)args.AICommand,
                args.TargetValue1, args.TargetValue2, args.ReturnValue);
        internal void GroupMove(TribeIssueOrderMoveHereEventArgs args)
            => Observe(args, MacemenStage.GroupMove, args.TribeId, (int)args.MoveType,
                args.TileX, args.TileY, args.ReturnValue);
        internal void UnitMove(UnitMoveHereEventArgs args)
            => Observe(args, MacemenStage.UnitMove, args.UnitId, -1, args.TileX, args.TileY, args.ReturnValue);

        private void Observe(EventHookBase args, MacemenStage stage, int id, int command, int t1, int t2, long result)
        {
            if (!enabled) return;
            long started = Stopwatch.GetTimestamp(); callbacks++;
            try
            {
                PruneSkipped();
                if (args.Phase == EventHookPhase.Pre)
                {
                    pre++;
                    if (frames == null) frames = new Frame[8];
                    if (depth == frames.Length) Array.Resize(ref frames, depth * 2);
                    int frameIndex = depth++;
                    // Publish an unverified placeholder before fallible SDK reads.
                    frames[frameIndex] = new Frame { Args = args, Stage = stage, Id = id };
                    MacemenUnit unit = stage == MacemenStage.UnitMove ? CaptureUnit(id) : CaptureLeader(id);
                    Frame frame = new Frame { Args = args, Stage = stage, Id = id, Command = stage == MacemenStage.TargetOrder ? command : -1,
                        T1 = t1, T2 = t2, Call = ++calls, Unit = unit, Relevant = unit.Valid && unit.Maceman,
                        TargetOrderContext = stage == MacemenStage.TargetOrder,
                        TargetArgs = args as TribeIssueOrderWithTargetEventArgs,
                        MoveType = stage == MacemenStage.GroupMove ? command : -1 };
                    MacemenGate parentGate = default;
                    if (stage != MacemenStage.TargetOrder && frameIndex > 0)
                    {
                        Frame parent = frames[frameIndex - 1];
                        if (parent.Stage != MacemenStage.UnitMove && unit.Valid &&
                            parent.Unit.Valid && parent.Id == unit.Tribe && parent.Unit.Player == unit.Player &&
                            parent.Unit.TribeGlobal == unit.TribeGlobal && ArgumentsIdentityMatches(parent))
                        { frame.TargetArgs = parent.TargetArgs;
                          frame.TargetOrderContext = frame.TargetArgs != null;
                          frame.Command = frame.TargetOrderContext ? (int)frame.TargetArgs.AICommand : -1;
                          if (stage == MacemenStage.UnitMove) frame.MoveType = parent.MoveType;
                          if (unit.Maceman) { parent.Relevant = true; frames[frameIndex - 1] = parent;
                            RefreshArguments(ref parent); parentGate = ResolveTarget(parent, policy()); } }
                    }
                    frame.Gate = parentGate.Id > 0 ? parentGate : ResolveTarget(frame, policy());
                    frames[frameIndex] = frame;
                }
                else if (args.Phase == EventHookPhase.Post)
                {
                    post++;
                    if (depth == 0 || frames[depth - 1].Stage != stage || frames[depth - 1].Id != id)
                    { unmatchedPost++; return; }
                    Frame frame = frames[--depth]; frames[depth] = default;
                    if (!frame.Relevant) return;
                    MacemenUnit after = stage == MacemenStage.UnitMove ? CaptureUnit(id) : CaptureLeader(id);
                    RefreshArguments(ref frame);
                    if (frame.Stage != MacemenStage.UnitMove) frame.Gate = ResolveTarget(frame, policy());
                    Record(frame, stage, frame.Gate, result > 0 ? 1 : 0, result, 0, -1,
                        after, frame.Unit.SameIdentity(after) && ArgumentsIdentityMatches(frame));
                }
            }
            catch (Exception error) { Failure("command", error); }
            finally { workTicks += Stopwatch.GetTimestamp() - started; }
        }
        private void PruneSkipped()
        {
            while (depth > 0 && frames[depth - 1].Args?.SkipOriginalFunction == true)
            { skipped++; frames[--depth] = default; }
        }
        private static bool ArgumentsIdentityMatches(Frame frame) =>
            (frame.TargetArgs == null || (frame.TargetArgs.TribeId == frame.Unit.Tribe && !frame.TargetArgs.SkipOriginalFunction)) &&
            (frame.Args is TribeIssueOrderWithTargetEventArgs order ? order.TribeId == frame.Id :
            frame.Args is TribeIssueOrderMoveHereEventArgs move ? move.TribeId == frame.Id :
            frame.Args is UnitMoveHereEventArgs unit ? unit.UnitId == frame.Id : false);
        private static void RefreshArguments(ref Frame frame)
        {
            if (frame.Args is TribeIssueOrderWithTargetEventArgs order)
            { frame.Command = (int)order.AICommand; frame.T1 = order.TargetValue1; frame.T2 = order.TargetValue2; }
            else if (frame.Args is TribeIssueOrderMoveHereEventArgs move)
            { frame.MoveType = (int)move.MoveType; frame.T1 = move.TileX; frame.T2 = move.TileY; }
            else if (frame.Args is UnitMoveHereEventArgs unit)
            { frame.T1 = unit.TileX; frame.T2 = unit.TileY; }
            if (frame.TargetArgs != null) frame.Command = (int)frame.TargetArgs.AICommand;
        }
        internal void BeginSearch(int player, int kind)
        {
            if (!enabled) return;
            try
            {
                PruneSkipped();
                if (searches == null) searches = new Search[8];
                if (searchDepth == searches.Length) Array.Resize(ref searches, searchDepth * 2);
                int index = searchDepth++;
                searches[index] = default;
                Frame frame = depth > 0 ? frames[depth - 1] : default;
                RefreshArguments(ref frame);
                if (frame.Stage != MacemenStage.UnitMove && frame.Unit.Valid && frame.Unit.Maceman && frame.Unit.Player == player)
                    frame.Gate = ResolveTarget(frame, policy());
                searches[index] = new Search { Frame = frame, Player = player, Kind = kind, Policy = policy(),
                    Eligible = frame.Unit.Valid && frame.Unit.Maceman && frame.Unit.Player == player && ArgumentsIdentityMatches(frame) };
            }
            catch (Exception error) { Failure("search-begin", error); }
        }
        internal void EndSearch(bool completed, bool success, long rejected)
        {
            if (!enabled) return;
            try
            {
                if (searchDepth == 0) { errors++; return; }
                Search search = searches[--searchDepth]; searches[searchDepth] = default;
                if (!search.Eligible) return;
                MacemenUnit after = search.Frame.Stage == MacemenStage.UnitMove
                    ? CaptureUnit(search.Frame.Id) : CaptureLeader(search.Frame.Id);
                Record(search.Frame, MacemenStage.Search, search.Frame.Gate,
                    !completed ? -2 : success ? 1 : 0, success ? 1 : 0, rejected, search.Kind,
                    after, search.Frame.Unit.SameIdentity(after) && ReferenceEquals(search.Policy, policy()));
            }
            catch (Exception error) { Failure("search-end", error); }
        }
        internal void GateComparison(int player, int gateId, uint global, bool nativeRejected, bool effectiveRejected,
            int site, int nativeOwner, int nativeCapturer)
        {
            if (!enabled || searchDepth == 0) return;
            try
            {
                Search search = searches[searchDepth - 1];
                if (!search.Eligible || search.Player != player) return;
                // Capturer callback: immutable lookup only, no game API or live-state read.
                MacemenGate gate = search.Policy.GateIdentities.TryGetValue(gateId, out var identity) && identity.Global == global
                    ? new MacemenGate(gateId, global, nativeOwner, nativeCapturer, -1, -1) : default;
                if (gate.Id == 0)
                {
                    // Missing identity is coverage, not an identity mismatch or route failure.
                    int key = (player << 8) | (site << 2) | (nativeRejected ? 1 : 0) | (effectiveRejected ? 2 : 0);
                    unattributedComparisons.TryGetValue(key, out long count);
                    unattributedComparisons[key] = count + 1;
                    return;
                }
                Record(search.Frame, MacemenStage.GateComparison, gate,
                    (nativeRejected ? 1 : 0) | (effectiveRejected ? 2 : 0), 0, 0, search.Kind,
                    search.Frame.Unit, gate.Id > 0 && identity.Owner == nativeOwner && identity.Capturer == nativeCapturer &&
                        ReferenceEquals(search.Policy, policy()), site);
            }
            catch (Exception error) { Failure("gate-comparison", error); }
        }
        private void Record(Frame frame, MacemenStage stage, MacemenGate gate, int result,
            long nativeResult, long rejected, int kind, MacemenUnit after, bool stable, int comparisonSite = -1)
        {
            NativeGateAccessSnapshot publication = access();
            var sample = new MacemenSample(frame.Call, Stopwatch.GetTimestamp(), epoch,
                publication.DiagnosticGeneration, frame.T1, frame.T2, kind, nativeResult, rejected,
                frame.Unit, after, gate, stable, comparisonSite, frame.MoveType,
                frame.TargetArgs?.TargetValue1 ?? 0, frame.TargetArgs?.TargetValue2 ?? 0);
            // A comparison visits a gate; it does not prove that gate was the order target.
            MacemenTarget target = MacemenDiagnosticAggregate.Classify(frame.Command,
                frame.Gate.Id > 0, frame.TargetOrderContext);
            aggregate.Record(stage, target, frame.Command, result, sample);
        }
        private MacemenUnit ReadLeader(int tribeId)
        {
            var manager = GameTribeManagerAPI.Instance;
            if (!manager.IsValidId(tribeId) || !manager.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
                return default;
            uint global = tribe->r_GlobalId; int owner = tribe->r_PlayerIdOwner, leader = tribe->r_LeaderUnitId;
            if (!GameUnitManagerAPI.Instance.IsValidId(leader) ||
                !APIShared.UnitAccess.TryGetById(leader, out GameUnit* live, out _) || live == null) return default;
            var unit = new MacemenUnit(leader, live->r_GlobalId, owner, live->r_TribeId, global,
                live->r_AI_LastIssuedTribeCommand, live->r_AIState, live->r_CurrentTilePositionX,
                live->r_CurrentTilePositionY, live->r_ControllableForPlayerId, live->r_AI_ContextTargetBuildingTileId,
                live->r_UnitChimp == SHCDESE.Interop.eChimps.CHIMP_TYPE_MACEMAN);
            return unit.Tribe == tribeId && unit.TribeGlobal == global && unit.Player == owner &&
                unit.Control == owner && live->r_GlobalId == unit.Global &&
                tribe->r_GlobalId == global && tribe->r_LeaderUnitId == leader ? unit : default;
        }
        private MacemenUnit CaptureUnit(int id) => unitReader == null ? ReadUnit(id) : unitReader(id);
        private MacemenUnit CaptureLeader(int id) => leaderReader == null ? ReadLeader(id) : leaderReader(id);
        private MacemenUnit ReadUnit(int unitId)
        {
            if (!GameUnitManagerAPI.Instance.IsValidId(unitId) ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null) return default;
            // Reject other types before tribe lookup, string creation or aggregation.
            bool maceman = unit->r_UnitChimp == SHCDESE.Interop.eChimps.CHIMP_TYPE_MACEMAN;
            if (!maceman) return default;
            uint global = unit->r_GlobalId; int tribeId = unit->r_TribeId, control = unit->r_ControllableForPlayerId;
            var manager = GameTribeManagerAPI.Instance;
            if (!manager.IsValidId(tribeId) || !manager.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
            { identityUnknown++; return default; }
            uint tribeGlobal = tribe->r_GlobalId; int player = tribe->r_PlayerIdOwner;
            // Native control is a complete WORD; never truncate it into a guessed player.
            if (control != player || global == 0 || tribeGlobal == 0) { identityUnknown++; return default; }
            var captured = new MacemenUnit(unitId, global, player, tribeId, tribeGlobal,
                unit->r_AI_LastIssuedTribeCommand, unit->r_AIState, unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY, control, unit->r_AI_ContextTargetBuildingTileId, maceman);
            if (unit->r_GlobalId != global || unit->r_TribeId != tribeId ||
                unit->r_ControllableForPlayerId != control || tribe->r_GlobalId != tribeGlobal ||
                tribe->r_PlayerIdOwner != player) { identityUnknown++; return default; }
            return captured;
        }
        private MacemenGate ResolveTarget(Frame frame, RouteTilePolicySnapshot snapshot)
        {
            if (!frame.Relevant || snapshot == null || snapshot.GateIdentities.Count == 0) return default;
            if (!ArgumentsIdentityMatches(frame)) return default;
            int target1 = frame.TargetArgs?.TargetValue1 ?? frame.T1;
            int target2 = frame.TargetArgs?.TargetValue2 ?? frame.T2;
            if (frame.TargetArgs != null && (frame.Command == 9 || frame.Command == 36 || frame.Command == 38))
                return ReadGate(target1, unchecked((uint)target2), snapshot);
            if (frame.TargetArgs != null && frame.Command != 3) return default;
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(target1, target2)) return default;
            int tile = tiles.GetTileId(target1, target2);
            if (!tiles.IsValidTileId(tile)) return default;
            return ReadGate(tiles.GetTileBuildingId(tile), 0, snapshot);
        }
        private static MacemenGate ReadGate(int id, uint requestedGlobal, RouteTilePolicySnapshot snapshot)
        {
            if (snapshot == null || !snapshot.GateIdentities.TryGetValue(id, out var identity) ||
                (requestedGlobal != 0 && requestedGlobal != identity.Global)) return default;
            var manager = GameBuildingManagerAPI.Instance;
            if (!manager.IsValidId(id) || !manager.TryGetBuildingById(id, out GameBuilding* building) || building == null ||
                building->r_GlobalId != identity.Global || building->r_PlayerIdOwner != identity.Owner ||
                building->r_CapturedByPlayerId != identity.Capturer) return default;
            return new MacemenGate(id, identity.Global, identity.Owner, identity.Capturer,
                building->r_GateState, (int)building->r_AIWalkableState);
        }
        private void Flush(bool final)
        {
            foreach (var pair in unattributedComparisons)
                Shared.DebugLogHelper.LogInfo(log, "TEMP_MACEMEN_GATE_DIAGNOSTICS coverage: player=" + (pair.Key >> 8) +
                    ",comparisonSite=" + ((pair.Key >> 2) & 63) + ",comparisonResult=" + (pair.Key & 3) +
                    ",reason=gate-identity-not-attributed,count=" + pair.Value);
            unattributedComparisons.Clear();
            foreach (var failure in failures)
                Shared.DebugLogHelper.LogInfo(log, "TEMP_MACEMEN_GATE_DIAGNOSTICS failure: cause=" + failure.Key + ",count=" + failure.Value);
            aggregate.Drain((key, row) => Shared.DebugLogHelper.LogInfo(log,
                "TEMP_MACEMEN_GATE_DIAGNOSTICS " + key.Format() + ",count=" + row.Count +
                ",targetChangesWithinCombination=" + row.TargetChanges + ",rejectedEdges=" + row.RejectedEdges +
                ",unstable=" + row.Unstable + ",first={" + row.First.Format() + "},last={" + row.Last.Format() + "}"));
            Shared.DebugLogHelper.LogInfo(log, "TEMP_MACEMEN_GATE_DIAGNOSTICS summary: epoch=" + epoch +
                ",epochUtc=" + epochUtc.ToString("O") + ",epochStartedTicks=" + epochStarted + ",ticksPerSecond=" + Stopwatch.Frequency +
                ",final=" + final + ",observations=" + aggregate.Total + ",pre/post=" + pre + "/" + post +
                ",skipped=" + skipped + ",missingPost=" + missingPost + ",unmatchedPost=" + unmatchedPost +
                ",identityUnknown=" + identityUnknown + ",errors=" + errors + ",commandCallbackCount=" + callbacks +
                ",commandCallbackMs=" + (workTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ". Group evidence uses a verified Maceman leader or synchronous Maceman child; storedCommand is not incomingCommand. " +
                "GroundElsewhere does not prove passage behind/through a gate; gate comparisons prove inspection only. " +
                "Observed Pre parameters and Post return do not prove eventual physical attack execution.");
        }
        private void Failure(string source, Exception error)
        { errors++; string cause = source + ":" + error.GetType().Name;
          failures.TryGetValue(cause, out long count); failures[cause] = count + 1; }
    }
}
