using System;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe partial class EconomyDiagnostics
    {
        private readonly RemovalEvidenceLedger removals = new RemovalEvidenceLedger();
        private readonly bool removalLayout;
        internal void HookCaptureFailed(Exception error) => Safe(() => { throw error; });

        internal static void ValidateRemovalLayout()
        {
            if (Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_TotalMissingWorkers)).ToInt32() != 0x19C ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_ProdTargetPathPlanOfUnit)).ToInt32() != 0x2D4 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N0000176C)).ToInt32() != 0x1B8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N0000183D)).ToInt32() != 0x2DC ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TotalGoodsWoodPlanks)).ToInt32() != 0x4D8 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_WoodcuttersHutsActive)).ToInt32() != 0x2B1C ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_WoodcutterWorstDistance)).ToInt32() != 0x2B20 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_WoodcutterWorstBuildingId)).ToInt32() != 0x2B24)
                throw new InvalidOperationException("Removal field layout differs from audited native writers/readers");
        }

        internal EconomyOperation SafeBeginCounter(int slot)
        {
            EconomyOperation op = null;
            Safe(() =>
            {
                lock (sync)
                {
                    if (!active || !nativeLayout || !removalLayout) return;
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(slot, out GameBuilding* building) || building == null) return;
                    int player = building->r_PlayerIdOwner;
                    if (!Observe(player)) return;
                    Count(player + ":wood-counter-retirement:entered");
                    // Zero-counter updates need no operation allocation. Preserve unexpected negative values.
                    if (*(short*)((byte*)building + 0x2DC) == 0) { Count(player + ":wood-counter-zero-detail-skipped"); return; }
                    op = SafeBegin("wood-counter-retirement", player, slot, 0, countEntry: false);
                }
            });
            return op;
        }

        private short BuildingShort(int slot, int offset)
        {
            if (!nativeLayout || offset < 0 || offset + 2 > Marshal.SizeOf(typeof(GameBuilding)) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(slot, out GameBuilding* b) || b == null)
                throw new InvalidOperationException("Removal building field unavailable: " + slot);
            return *(short*)((byte*)b + offset);
        }

        private void CaptureRemovalInputs(EconomyOperation op)
        {
            if (!nativeLayout || !removalLayout) return;
            long p = op.Player * 0x583CL;
            if (op.Kind == "wood-retirement")
            {
                var inputs = new RemovalInputs { Kind = op.Kind, Profile = Read(0x379D0D0 + p),
                    GridWood = Read(0x3505470), Wood = Read(0x379B2A8 + p),
                    StaffedHuts = Read(0x379D8EC + p), WorstPath = Read(0x379D8F0 + p), WorstSlot = Read(0x379D8F4 + p) };
                // With zero grid wood Vanilla selects its first existing hut later, inside the original.
                if (inputs.GridWood > 0 && inputs.WorstSlot > 0) inputs.Target = Identity(inputs.WorstSlot);
                op.RemovalInputs = inputs;
            }
            else if (op.Kind == "wood-counter-retirement")
                op.RemovalInputs = new RemovalInputs { Kind = op.Kind, Target = Identity(op.ArgumentA),
                    Counter = BuildingShort(op.ArgumentA, 0x2DC), RawGuard1B8 = BuildingShort(op.ArgumentA, 0x1B8),
                    Control = Read(0x8574BCC + op.Player * 4L), Enabled = Read(0x8574C44 + op.Player * 4L),
                    GameActive = Read(0x8574B90), RotatingPlayer = Read(0x382D080) };
        }

        private EconomyOperation RemovalContext()
        {
            for (var op = Current; op != null && op.Session == session; op = op.Parent)
                if (op.Kind == "wood-retirement" || op.Kind == "wood-counter-retirement" || op.Kind == "accessibility-sweep") return op;
            return null;
        }

        private BuildingEvidenceIdentity RemovalTargetAt(EconomyOperation op, int x, int y)
        {
            var target = op?.RemovalInputs?.Target;
            if (target != null && target.X == x && target.Y == y)
            {
                var now = Identity(target.Slot);
                return now != null && target.Matches(now.Slot, now.GlobalId) && now.X == x && now.Y == y ? now : null;
            }
            BuildingEvidenceIdentity candidate = null;
            // Managed inventory only; no new native building/tile scan or search call.
            foreach (var known in knownBuildings.Values)
                if (known.X == x && known.Y == y)
                {
                    var now = Identity(known.Slot);
                    if (now != null && known.Matches(now.Slot, now.GlobalId) && now.X == x && now.Y == y)
                    {
                        if (candidate != null) return null; // Ambiguous origins cannot establish a building cause.
                        candidate = now;
                    }
                }
            return candidate;
        }

        private string RemovalCell(ulong state, int x, int y)
        {
            if (!nativeLayout || state != unchecked((ulong)(module + 0x34A9F70)) || x < 0 || y < 0 || x / 5 >= 160 || y / 5 >= 160)
                return "unavailable; invalid-root-or-cell";
            long cell = 0x34A9F70L + ((long)(x / 5) * 160 + y / 5) * 0x30;
            if (cell + 0x5B848 > moduleSize) return "unavailable; cell-outside-image";
            int tree = unchecked((sbyte)Marshal.ReadByte(new IntPtr(module + cell + 0x5B837)));
            int retry = unchecked((sbyte)Marshal.ReadByte(new IntPtr(module + cell + 0x5B843)));
            int count = unchecked((sbyte)Marshal.ReadByte(new IntPtr(module + cell + 0x5B847)));
            return $"gridWoodWeight={tree}, retryDelay={retry}, removalCount={count}";
        }

        internal RemovalCellObservation SafeBeginCell(ulong state, int x, int y)
        {
            RemovalCellObservation capture = null;
            Safe(() =>
            {
                lock (sync)
                {
                    if (!active || !removalLayout) return;
                    var op = RemovalContext();
                    capture = new RemovalCellObservation { Session = session, Operation = op, State = state,
                        X = x, Y = y, Before = RemovalCell(state, x, y), Target = RemovalTargetAt(op, x, y) };
                    if (op != null && capture.Target != null) op.RemovalTarget = capture.Target;
                }
            });
            return capture;
        }

        internal void SafeEndCell(RemovalCellObservation capture)
        {
            if (capture == null) return;
            Safe(() =>
            {
                lock (sync)
                {
                    if (!active || capture.Session != session) return;
                    var op = capture.Operation;
                    int player = capture.Target?.Player ?? op?.Player ?? 0;
                    Emit("REMOVAL_CELL", player, op?.Id ?? 0,
                        $"tile=({capture.X},{capture.Y}), cell=({capture.X / 5},{capture.Y / 5}), source={op?.Kind ?? "unknown"}, before=[{capture.Before}], after=[{RemovalCell(capture.State, capture.X, capture.Y)}], target={capture.Target?.ToString() ?? "unavailable"}; refreshStepsAreNotSimulationTicks=true");
                    if (op?.Kind != "accessibility-sweep" || capture.Target == null) return;
                    var after = Identity(capture.Target.Slot);
                    if (after == null || !capture.Target.Matches(after.Slot, after.GlobalId)) return;
                    RecordRemovalMark(op, after, "accessibility-sweep", false);
                    if (after.Group == 0) return;
                    foreach (var known in knownBuildings.Values)
                    {
                        if (known.Slot == after.Slot || known.Group != after.Group) continue;
                        var member = Identity(known.Slot);
                        if (member != null && known.Matches(member.Slot, member.GlobalId) && member.Group == after.Group)
                            RecordRemovalMark(op, member, "accessibility-group-followup", true);
                    }
                }
            });
        }

        private void FinishRemovalDecision(EconomyOperation op, int? result)
        {
            lock (sync)
            {
                if (!active || op.Session != session || op.Kind != "wood-retirement" && op.Kind != "wood-counter-retirement") return;
                var inputs = op.RemovalInputs;
                var target = op.RemovalTarget ?? inputs?.Target;
                string detail = $"kind={op.Kind}, nativeResult={result?.ToString() ?? "unavailable"}, inputs=[{inputs?.ToString() ?? "unavailable"}], actualTarget={target?.ToString() ?? "unavailable"}; returnIsNotRemoval=true";
                if (result == 1) Emit("REMOVAL_DECISION", op.Player, op.Id, detail);
                else if (inputs != null)
                {
                    string key = op.Player + ":removal-gates:" + op.Kind + ":" + op.ArgumentA;
                    if (last.TryGetValue(key, out string gates) && gates == inputs.GateSignature) Omit("removal-input-same-gates");
                    else if (!last.ContainsKey(key) && last.Count >= 8192) Omit("removal-input-keys");
                    else { last[key] = inputs.GateSignature; Emit("REMOVAL_DECISION", op.Player, op.Id, detail); }
                }
                if (result != 1 || target == null) return;
                if (target.State == (int)AliveState.None || target.State == (int)AliveState.MarkedForDeletion) return;
                var after = Identity(target.Slot);
                if (after == null || !target.Matches(after.Slot, after.GlobalId)) return;
                bool agrees = inputs != null && inputs.Eligible && after.Player == op.Player &&
                    after.Type == (int)eStructs.STRUCT_WOODCUTTERS_HUT &&
                    (inputs.Target == null || inputs.Target.Matches(target.Slot, target.GlobalId));
                RecordRemovalMark(op, after, agrees ? inputs.CandidateReason : "unknown-input-or-target-mismatch", false);
            }
        }

        private void RecordRemovalMark(EconomyOperation op, BuildingEvidenceIdentity after, string reason, bool group)
        {
            if (!Observe(after.Player)) return;
            var mark = new RemovalMark { Session = session, Operation = op.Id, Tick = tick, Identity = after,
                Reason = reason, GroupFollowup = group, WorkerEvidence = workerCauses.Describe(after) };
            if (!removals.Mark(mark)) return;
            Count(after.Player + ":removal-mark:" + reason);
            Emit("REMOVAL_MARKED", after.Player, op.Id,
                $"{after}, reason={reason}, groupFollowup={group}, workerEvidence=[{mark.WorkerEvidence}]; originalAccessibilityResult=unavailable; groupPriorState={(group ? "not-measured" : "see-decision-context")}; terminalRemoval=false");
        }

        private void ConfirmMarkedRemoval(BuildingEvidenceIdentity before, BuildingEvidenceIdentity after)
        {
            var mark = removals.Confirm(before, after, tick);
            if (mark == null) return;
            Count(before.Player + ":removal-confirmed:" + mark.Reason);
            if (knownBuildings.TryGetValue(before.Slot, out var known) && known.Matches(before.Slot, before.GlobalId)) knownBuildings.Remove(before.Slot);
            Emit("REMOVAL_CAUSE_CONFIRMED", before.Player, mark.Operation,
                $"{before}, reason={mark.Reason}, groupFollowup={mark.GroupFollowup}, causeConfidence={(mark.GroupFollowup ? "possible-group-followup" : "measured-native-path")}, markedTick={mark.Tick}, removedTick={tick}, after={after}, workerEvidence=[{mark.WorkerEvidence}]; unknownHistoryRemainsUnknown=true");
        }

        private void ObserveRebuild(BuildingEvidenceIdentity id)
        {
            var rebuild = removals.Spawn(id);
            if (rebuild == null) return;
            var previous = rebuild.Previous;
            Count(id.Player + ":rebuild-after-removal:" + previous.Reason);
            Emit("REBUILD_AFTER_REMOVAL", id.Player, previous.Operation,
                $"spawn=[{id}], previous=[{previous.Identity}], previousReason={previous.Reason}, sameCoarseCell={rebuild.SameCell}, ticksSinceMark={Elapsed(previous.Tick)}, ticksSinceConfirmedRemoval={Elapsed(previous.ConfirmedTick)}, repeatCount={rebuild.Count}, workerEvidence=[{previous.WorkerEvidence}], construction=[{CurrentConstruction(id.Player)}]; spatialCorrelationIsNotSameCauseProof=true");
        }
        private string Elapsed(int before) => before < 0 || tick < 0 ? "unavailable-before-first-tick" : ((long)tick - before).ToString();
    }
}
