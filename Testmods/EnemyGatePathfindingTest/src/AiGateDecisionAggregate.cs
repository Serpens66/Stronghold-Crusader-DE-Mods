using System;
using System.Collections.Generic;

namespace EnemyGatePathfindingTest
{
    // Every observation increments an exact semantic bucket. Concrete identities are
    // retained at both ends of the interval, never used to cap or discard events.
    internal sealed class AiGateDecisionAggregate
    {
        private readonly object gate = new object();
        private readonly Dictionary<Key, Row> rows = new Dictionary<Key, Row>();
        private readonly Dictionary<TribeKey, Target> lastTargets =
            new Dictionary<TribeKey, Target>();
        private long observations;

        internal long Observations { get { lock (gate) return observations; } }

        internal void Record(int player, int gateId, string stage, string result,
            int command, int tribeId, int target1, int target2, string detail = null)
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

        internal RowSnapshot[] Drain()
        {
            lock (gate)
            {
                var result = new RowSnapshot[rows.Count];
                int index = 0;
                foreach (Row row in rows.Values)
                    result[index++] = new RowSnapshot(row.Key.Player, row.Key.GateId,
                        row.Key.Command, row.Key.Stage, row.Key.Result,
                        row.First.ToString(), row.Last.ToString(), row.Count, row.TargetChanges);
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
            internal long Count, TargetChanges;
            internal Row(Key key, Target first) { Key = key; First = Last = first; }
        }

        internal readonly struct RowSnapshot
        {
            internal readonly int Player, GateId, Command;
            internal readonly string Stage, Result, First, Last;
            internal readonly long Count, TargetChanges;
            internal RowSnapshot(int player, int gateId, int command, string stage,
                string result, string first, string last, long count, long targetChanges)
            {
                Player = player; GateId = gateId; Command = command; Stage = stage;
                Result = result; First = first; Last = last; Count = count;
                TargetChanges = targetChanges;
            }
            internal string SortKey => Player.ToString("D2") + ":" + GateId.ToString("D6") +
                ":" + Stage + ":" + Result + ":" + Command.ToString("D3");
            public override string ToString() => "player=" + Player + ",gate=" + GateId +
                ",stage=" + Stage + ",result=" + Result + ",command=" + Command +
                ",count=" + Count + ",targetChanges=" + TargetChanges +
                ",first=" + First + ",last=" + Last;
        }
    }
}
