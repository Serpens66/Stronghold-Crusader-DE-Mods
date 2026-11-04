using System;
using System.Collections.Generic;
using SHCDESE.Interop.Enums;

namespace AIBuildDiagnoseTest
{
    // Small, session-owned facts. No native calls, grid copies or guessed tree searches.
    internal sealed class RemovalInputs
    {
        internal string Kind;
        internal int Profile, GridWood, Wood, StaffedHuts, WorstPath, WorstSlot;
        internal int Counter, RawGuard1B8, Control, Enabled, GameActive, RotatingPlayer;
        internal BuildingEvidenceIdentity Target;
        internal string CandidateReason
        {
            get
            {
                if (Kind == "wood-retirement")
                {
                    if (Profile == 0) return "profile-disabled";
                    if (GridWood < 1) return "wood-grid-empty";
                    if (Wood < 21) return "wood-stock-below-21";
                    if (StaffedHuts < 2) return "staffed-huts-below-2";
                    return WorstPath < 25 ? "stored-path-below-25" : "wood-distance";
                }
                if (Kind == "wood-counter-retirement")
                    return Counter >= 20 && RawGuard1B8 == 0 && Control == -1 && Enabled != 0 && GameActive != 0
                        ? "wood-counter" : "counter-gate-not-satisfied";
                return "unknown";
            }
        }
        internal bool Eligible => CandidateReason == "wood-grid-empty" || CandidateReason == "wood-distance" || CandidateReason == "wood-counter";
        internal string GateSignature => Kind == "wood-retirement"
            ? $"{Profile}:{GridWood < 1}:{Wood >= 21}:{StaffedHuts >= 2}:{WorstPath >= 25}:{WorstSlot}:{Target?.GlobalId}"
            : $"{Counter < 0}:{Counter >= 20}:{RawGuard1B8 == 0}:{Control}:{Enabled != 0}:{GameActive != 0}:{Target?.GlobalId}";
        public override string ToString() => Kind == "wood-retirement"
            ? $"profile={Profile}, gridWoodWeight={GridWood}, woodPlanks={Wood}, fullyStaffedHuts={StaffedHuts}, storedWorstPathLength={WorstPath}, selectedSlot={WorstSlot}, stockAtLeast21={Wood >= 21}, staffedAtLeast2={StaffedHuts >= 2}, pathAtLeast25={WorstPath >= 25}, candidate={CandidateReason}, entryTarget={Target?.ToString() ?? "unavailable"}"
            : $"counterSigned16={Counter}, rawGuard1B8LowSigned16={RawGuard1B8}, aiControl={Control}, playerEnabled={Enabled}, gameActive={GameActive}, rotatingPlayer={RotatingPlayer}, counterAtLeast20={Counter >= 20}, candidate={CandidateReason}, entryTarget={Target?.ToString() ?? "unavailable"}; counterMeaning=not-fully-established";
    }

    internal sealed class RemovalMark
    {
        internal long Session, Operation;
        internal int Tick, ConfirmedTick;
        internal BuildingEvidenceIdentity Identity;
        internal string Reason;
        internal string WorkerEvidence;
        internal bool GroupFollowup;
    }

    internal sealed class RebuildEvidence
    {
        internal RemovalMark Previous;
        internal int Count;
        internal bool SameCell;
    }

    internal sealed class RemovalEvidenceLedger
    {
        internal const int Limit = 8192;
        private readonly Dictionary<ulong, RemovalMark> pending = new Dictionary<ulong, RemovalMark>();
        private readonly Dictionary<string, RemovalMark> removed = new Dictionary<string, RemovalMark>();
        private readonly Dictionary<string, int> rebuilds = new Dictionary<string, int>();
        internal long Session { get; private set; }
        internal long Omitted { get; private set; }
        internal int PendingCount => pending.Count;
        internal void Reset(long session)
        { Session = session; pending.Clear(); removed.Clear(); rebuilds.Clear(); Omitted = 0; }
        private static string Cell(BuildingEvidenceIdentity id, int x, int y) => id.Player + ":" + id.Type + ":" + x + ":" + y;
        private static ulong IdentityKey(BuildingEvidenceIdentity id) => (ulong)(uint)id.Slot << 32 | id.GlobalId;
        internal bool Mark(RemovalMark mark)
        {
            if (mark == null || mark.Identity == null || mark.Session != Session ||
                mark.Identity.State != (int)AliveState.MarkedForDeletion) return false;
            ulong key = IdentityKey(mark.Identity);
            if (pending.ContainsKey(key))
                return false; // Keep the first measured cause; group propagation cannot replace it.
            if (pending.Count >= Limit) { Omitted++; return false; }
            pending[key] = mark;
            return true;
        }
        internal RemovalMark Find(BuildingEvidenceIdentity id) => id != null && pending.TryGetValue(IdentityKey(id), out var mark) &&
            mark.Session == Session && mark.Identity.GlobalId == id.GlobalId ? mark : null;
        internal RemovalMark Confirm(BuildingEvidenceIdentity before, BuildingEvidenceIdentity after, int tick)
        {
            var mark = Find(before);
            if (mark == null || !BuildingEvidenceIdentity.IsRemoved(before, after)) return null;
            pending.Remove(IdentityKey(before));
            mark.ConfirmedTick = tick;
            string key = Cell(before, before.X / 5, before.Y / 5);
            if (removed.ContainsKey(key) || removed.Count < Limit) removed[key] = mark;
            else Omitted++;
            return mark;
        }
        internal IEnumerable<RemovalMark> PendingSnapshot() => new List<RemovalMark>(pending.Values);
        internal RebuildEvidence Spawn(BuildingEvidenceIdentity id)
        {
            if (id == null || id.State == (int)AliveState.None || id.State == (int)AliveState.MarkedForDeletion) return null;
            int x = id.X / 5, y = id.Y / 5;
            RemovalMark latest = null;
            bool same = false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (removed.TryGetValue(Cell(id, x + dx, y + dy), out var found) &&
                        !found.Identity.Matches(id.Slot, id.GlobalId) &&
                        (latest == null || found.ConfirmedTick > latest.ConfirmedTick || found.ConfirmedTick == latest.ConfirmedTick && found.Operation > latest.Operation))
                    { latest = found; same = dx == 0 && dy == 0; }
            if (latest == null) return null;
            string key = Cell(latest.Identity, latest.Identity.X / 5, latest.Identity.Y / 5);
            if (!rebuilds.ContainsKey(key) && rebuilds.Count >= Limit) { Omitted++; return null; }
            int count = rebuilds.TryGetValue(key, out int n) ? n + 1 : 1;
            rebuilds[key] = count;
            return new RebuildEvidence { Previous = latest, Count = count, SameCell = same };
        }
    }

    internal sealed class RemovalCellObservation
    {
        internal long Session;
        internal EconomyOperation Operation;
        internal ulong State;
        internal int X, Y;
        internal string Before;
        internal BuildingEvidenceIdentity Target;
    }
}
