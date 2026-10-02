using System;
using System.Collections.Generic;

namespace EnemyGatePathfindingTest
{
    internal enum QueryKind
    {
        HumanBuilder, AiBuilder, Attack, BuildingApproach,
        AlternateBuildingApproach, CandidateSearch, CursorCommand, DirectCursor,
        CursorPreview, AiTacticalTarget
    }

    // Frozen at entry; later player-kind publications do not reclassify this call.
    internal readonly struct SearchDiagnosticContext
    {
        internal SearchDiagnosticContext(QueryKind kind) { Kind = kind; }
        internal QueryKind Kind { get; }
        internal bool IsAiBuilder => Kind == QueryKind.AiBuilder;
    }

    // Every observation increments an exact semantic bucket. Concrete identities are
    // retained at both ends of the interval, never used to cap or discard events.
    internal sealed class AiGateDecisionAggregate
    {
        private readonly object gate = new object();
        private readonly Dictionary<Key, Row> rows = new Dictionary<Key, Row>();
        private readonly Dictionary<TribeKey, Target> lastTargets =
            new Dictionary<TribeKey, Target>();
        private long observations, epoch;
        private readonly Dictionary<StateKey, GateStateDefinition> states =
            new Dictionary<StateKey, GateStateDefinition>();
        private readonly List<GateStateDefinition> pendingStates = new List<GateStateDefinition>();

        internal void RecordGateState(int player, int gateId, string stage, string state,
            int command, int tribeId, int target1, int target2, string detail)
        {
            lock (gate)
            {
                var key = new StateKey(player, gateId, state, detail);
                if (!states.TryGetValue(key, out GateStateDefinition definition))
                {
                    definition = new GateStateDefinition(epoch, states.Count + 1,
                        player, gateId, state, detail);
                    states.Add(key, definition);
                    pendingStates.Add(definition);
                }
                Record(player, gateId, stage, "gateState=" + definition.Reference,
                    command, tribeId, target1, target2, "state=" + definition.Reference);
            }
        }

        internal readonly struct GateStateDefinition
        {
            internal readonly long Epoch;
            internal readonly int Id, Player, GateId;
            internal readonly string State, Detail;
            internal GateStateDefinition(long epoch, int id, int player, int gateId,
                string state, string detail)
            { Epoch = epoch; Id = id; Player = player; GateId = gateId;
              State = state; Detail = detail; }
            internal string Reference => Epoch + "/" + Id;
            public override string ToString() => "epoch=" + Epoch + ",id=" + Id +
                ",player=" + Player + ",gate=" + GateId + "," + State + ",detail=" + Detail;
        }

        private readonly struct StateKey : IEquatable<StateKey>
        {
            private readonly int player, gateId;
            private readonly string state, detail;
            internal StateKey(int player, int gateId, string state, string detail)
            { this.player = player; this.gateId = gateId; this.state = state; this.detail = detail; }
            public bool Equals(StateKey other) => player == other.player && gateId == other.gateId &&
                state == other.state && detail == other.detail;
            public override bool Equals(object value) => value is StateKey other && Equals(other);
            public override int GetHashCode() => unchecked(((player * 397 ^ gateId) * 397 ^
                (state?.GetHashCode() ?? 0)) * 397 ^ (detail?.GetHashCode() ?? 0));
        }

        internal long Observations { get { lock (gate) return observations; } }

        internal void Record(int player, int gateId, string stage, string result,
            int command, int tribeId, int target1, int target2, string detail = null, long value = 0)
        {
            var key = new Key(player, gateId, stage, result, command);
            var target = new Target(tribeId, target1, target2, detail ?? "none");
            lock (gate)
            {
                observations++;
                if (!rows.TryGetValue(key, out Row row))
                {
                    row = new Row(key, target);
                    rows.Add(key, row);
                }
                row.Count++;
                row.Value += value;
                row.Last = target;
                if (tribeId > 0 && (stage == "tribe-target-pre" ||
                    stage == "tribe-move-pre" || stage == "unit-context"))
                {
                    var tribeKey = new TribeKey(stage, tribeId);
                    if (lastTargets.TryGetValue(tribeKey, out Target previous) &&
                        (previous.Target1 != target1 || previous.Target2 != target2 ||
                         (stage == "unit-context" && previous.GateId != gateId)))
                        row.TargetChanges++;
                    lastTargets[tribeKey] = target.WithGate(gateId);
                }
            }
        }

        internal RowSnapshot[] Drain() => Drain(out _);

        // Definitions and referencing rows are captured under the same lock.
        internal RowSnapshot[] Drain(out GateStateDefinition[] definitions)
        {
            lock (gate)
            {
                definitions = pendingStates.ToArray();
                pendingStates.Clear();
                var result = new RowSnapshot[rows.Count];
                int index = 0;
                foreach (Row row in rows.Values)
                    result[index++] = new RowSnapshot(row.Key.Player, row.Key.GateId,
                        row.Key.Command, row.Key.Stage, row.Key.Result,
                        row.First.ToString(), row.Last.ToString(), row.Count, row.TargetChanges, row.Value);
                rows.Clear();
                Array.Sort(result, (left, right) => string.CompareOrdinal(
                    left.SortKey, right.SortKey));
                return result;
            }
        }

        internal void Reset()
        {
            lock (gate)
            {
                rows.Clear();
                lastTargets.Clear();
                states.Clear();
                pendingStates.Clear();
                epoch++;
                observations = 0;
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            internal readonly int Player, GateId, Command;
            internal readonly string Stage, Result;
            internal Key(int player, int gateId, string stage, string result, int command)
            { Player = player; GateId = gateId; Stage = stage; Result = result; Command = command; }
            public bool Equals(Key other) => Player == other.Player && GateId == other.GateId &&
                Command == other.Command && Stage == other.Stage && Result == other.Result;
            public override bool Equals(object value) => value is Key other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Player * 397 ^ GateId;
                    hash = hash * 397 ^ Command;
                    hash = hash * 397 ^ (Stage?.GetHashCode() ?? 0);
                    return hash * 397 ^ (Result?.GetHashCode() ?? 0);
                }
            }
        }

        private readonly struct TribeKey : IEquatable<TribeKey>
        {
            private readonly string stage;
            private readonly int tribe;
            internal TribeKey(string stage, int tribe) { this.stage = stage; this.tribe = tribe; }
            public bool Equals(TribeKey other) => tribe == other.tribe && stage == other.stage;
            public override bool Equals(object value) => value is TribeKey other && Equals(other);
            public override int GetHashCode() => unchecked(tribe * 397 ^ stage.GetHashCode());
        }

        private readonly struct Target
        {
            internal readonly int Tribe, Target1, Target2, GateId;
            internal readonly string Detail;
            internal Target(int tribe, int target1, int target2, string detail, int gateId = 0)
            { Tribe = tribe; Target1 = target1; Target2 = target2; Detail = detail; GateId = gateId; }
            internal Target WithGate(int gateId) => new Target(Tribe, Target1, Target2, Detail, gateId);
            public override string ToString() => Tribe + ":" + Target1 + "/" + Target2 + ":" + Detail;
        }

        private sealed class Row
        {
            internal readonly Key Key;
            internal readonly Target First;
            internal Target Last;
            internal long Count, TargetChanges, Value;
            internal Row(Key key, Target first) { Key = key; First = Last = first; }
        }

        internal readonly struct RowSnapshot
        {
            internal readonly int Player, GateId, Command;
            internal readonly string Stage, Result, First, Last;
            internal readonly long Count, TargetChanges, Value;
            internal RowSnapshot(int player, int gateId, int command, string stage,
                string result, string first, string last, long count, long targetChanges, long value = 0)
            {
                Player = player; GateId = gateId; Command = command; Stage = stage;
                Result = result; First = first; Last = last; Count = count;
                TargetChanges = targetChanges; Value = value;
            }
            internal string SortKey => Player.ToString("D2") + ":" + GateId.ToString("D6") +
                ":" + Stage + ":" + Result + ":" + Command.ToString("D3");
            public override string ToString() => "player=" + Player + ",gate=" + GateId +
                ",stage=" + Stage + ",result=" + Result + ",command=" + Command +
                ",count=" + Count + ",value=" + Value + ",targetChanges=" + TargetChanges +
                ",first=" + First + ",last=" + Last;
        }
    }
}
