using System;
using System.Collections.Generic;

namespace AIBuildDiagnoseTest
{
    // Immutable scalar copies only; a deleted/reused native unit can never change these facts.
    internal sealed class WorkerSnapshot
    {
        internal readonly int Slot, Player, Chimp, State, Alive, X, Y, TargetX, TargetY, BuildingSlot;
        internal readonly uint GlobalId, Health, BuildingGlobalId;
        internal readonly ushort DeathMarker;
        internal readonly BuildingEvidenceIdentity Building;
        internal WorkerSnapshot(int slot, uint global, int player, int chimp, int state, int alive, uint health,
            int x, int y, int tx, int ty, int buildingSlot, uint buildingGlobal, BuildingEvidenceIdentity building, ushort deathMarker = 0)
        { Slot = slot; GlobalId = global; Player = player; Chimp = chimp; State = state; Alive = alive; Health = health;
          X = x; Y = y; TargetX = tx; TargetY = ty; BuildingSlot = buildingSlot; BuildingGlobalId = buildingGlobal; Building = building; DeathMarker = deathMarker; }
        internal bool AssignmentMatches(WorkerSnapshot other) => other != null && BuildingSlot == other.BuildingSlot &&
            BuildingGlobalId == other.BuildingGlobalId;
        internal bool BuildingMatches => Building != null && Building.Matches(BuildingSlot, BuildingGlobalId);
        public override string ToString() => $"unit={Slot}, unitGlobalId={GlobalId}, unitPlayer={Player}, chimp={Chimp}, aiState={State}, alive={Alive}, health={Health}, deathMarker={DeathMarker}, workerTile=({X},{Y}), movementTarget=({TargetX},{TargetY}), linkedBuilding={BuildingSlot}/{BuildingGlobalId}, building=[{Building?.ToString() ?? "unavailable"}], assignmentMatches={BuildingMatches}";
    }

    internal sealed class WorkerCauseEvent
    {
        internal long Session, Id;
        internal int Tick;
        internal string Kind, Source;
        internal WorkerSnapshot Worker;
        public override string ToString() => $"workerEvent={Id}, kind={Kind}, tick={Tick}, {Worker}, source=[{Source}]";
    }

    internal sealed class WorkerIncrementEvidence
    {
        internal long Id;
        internal string Cause;
        internal WorkerCauseEvent Trigger;
        internal int After;
        internal bool RepeatedUnit;
    }

    internal sealed class WorkerCauseLedger
    {
        internal const int Limit = 8192;
        private sealed class Trace
        {
            internal WorkerCauseEvent Death, Return;
            internal int Increments;
            internal bool DeathConflict;
        }
        private sealed class Hut
        {
            internal long Increments, Combat, Return, Unknown;
            internal readonly Queue<string> Recent = new Queue<string>();
        }
        private readonly Dictionary<ulong, Trace> units = new Dictionary<ulong, Trace>();
        private readonly Dictionary<ulong, Hut> huts = new Dictionary<ulong, Hut>();
        private long sequence;
        internal long Session { get; private set; }
        internal long Omitted { get; private set; }
        internal long DuplicateDeaths { get; private set; }
        private static ulong Key(int slot, uint global) => (ulong)(uint)slot << 32 | global;
        internal void Reset(long session) { Session = session; sequence = Omitted = DuplicateDeaths = 0; units.Clear(); huts.Clear(); }
        private Trace Get(WorkerSnapshot worker)
        {
            ulong key = Key(worker.Slot, worker.GlobalId);
            if (units.TryGetValue(key, out var found)) return found;
            if (units.Count >= Limit) { Omitted++; return null; }
            return units[key] = new Trace();
        }
        internal WorkerCauseEvent Trigger(long session, WorkerSnapshot worker, int tick, string kind, string source, bool death)
        {
            if (session != Session || worker == null) return null;
            var trace = Get(worker);
            if (trace == null) return null;
            if (death && trace.Death != null)
            {
                DuplicateDeaths++;
                if (trace.Death.Kind != kind || !worker.AssignmentMatches(trace.Death.Worker) ||
                    worker.Health != trace.Death.Worker.Health || worker.BuildingMatches != trace.Death.Worker.BuildingMatches)
                    trace.DeathConflict = true;
                return null;
            }
            var item = new WorkerCauseEvent { Session = Session, Id = ++sequence, Worker = worker, Tick = tick, Kind = kind, Source = source };
            if (death) trace.Death = item; else trace.Return = item;
            return item;
        }
        internal WorkerIncrementEvidence Increment(long session, WorkerSnapshot worker, int after)
        {
            if (session != Session || worker == null) return null;
            var trace = Get(worker);
            var result = new WorkerIncrementEvidence { Id = ++sequence, After = after, Cause = "unobserved-history" };
            if (trace != null)
            {
                result.RepeatedUnit = trace.Increments++ != 0;
                if (!worker.BuildingMatches) result.Cause = "building-identity-conflict";
                else if (trace.DeathConflict) result.Cause = "conflicting-death-observations";
                else if (trace.Death != null && trace.Return != null) result.Cause = "multiple-trigger-observations";
                else if (trace.Death != null)
                {
                    result.Trigger = trace.Death;
                    result.Cause = !worker.AssignmentMatches(trace.Death.Worker) || !trace.Death.Worker.BuildingMatches ? "assignment-changed-after-death" :
                        worker.Health != 0 || trace.Death.Worker.Health != 0 ? "death-health-conflict" : trace.Death.Kind;
                }
                else if (trace.Return != null)
                {
                    result.Trigger = trace.Return;
                    result.Cause = worker.AssignmentMatches(trace.Return.Worker) ? trace.Return.Kind : "assignment-changed-after-return";
                }
            }
            if (worker.Building == null) { Omitted++; return result; }
            // Native RSI selects the actual counter destination, which may differ from a stale link.
            ulong key = Key(worker.Building.Slot, worker.Building.GlobalId);
            if (!huts.TryGetValue(key, out var hut))
            {
                if (huts.Count >= Limit) { Omitted++; return result; }
                hut = huts[key] = new Hut();
            }
            hut.Increments++;
            if (result.Cause.StartsWith("killed-", StringComparison.Ordinal)) hut.Combat++;
            else if (result.Cause.StartsWith("return-", StringComparison.Ordinal)) hut.Return++;
            else hut.Unknown++;
            if (hut.Recent.Count == 8) hut.Recent.Dequeue();
            hut.Recent.Enqueue($"increment#{result.Id}/trigger#{result.Trigger?.Id.ToString() ?? "unknown"}/unit={worker.Slot}:{worker.GlobalId}/after={after}/cause={result.Cause}/repeatedUnit={result.RepeatedUnit}");
            return result;
        }
        internal string Describe(BuildingEvidenceIdentity building)
        {
            if (building == null || !huts.TryGetValue(Key(building.Slot, building.GlobalId), out var hut))
                return "history=unobserved; loaded-counter-does-not-prove-prior-deaths";
            return $"increments={hut.Increments}, combatMatchedIncrements={hut.Combat}, returnMatchedIncrements={hut.Return}, unknownIncrements={hut.Unknown}, olderIncrementDetailsOmitted={Math.Max(0, hut.Increments - 8)}, recent={string.Join("|", hut.Recent)}; matchedIncrementsAreNotUniqueDeaths=true";
        }
    }
}
