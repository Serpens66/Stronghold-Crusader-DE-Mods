// TEMP_MACEMEN_GATE_DIAGNOSTICS: remove with MacemenCommandDiagnostics and its startup option.
using System;
using System.Collections.Generic;

namespace EnemyGatePathfindingTest
{
    internal enum MacemenStage { TargetOrder, GroupMove, UnitMove, Search, GateComparison }
    internal enum MacemenTarget { Unknown, GroundElsewhere, GroundOnGate, GateAttack, OtherBuildingAttack, Moat, Wall, Unit, GateApproach, OtherBuildingApproach }

    internal readonly struct MacemenUnit
    {
        internal readonly int Id, Player, Tribe, Command, State, X, Y, Control;
        internal readonly uint Global, TribeGlobal, BuildingTile;
        internal readonly bool Valid, Maceman;
        internal MacemenUnit(int id, uint global, int player, int tribe, uint tribeGlobal,
            int command, int state, int x, int y, int control, uint buildingTile, bool maceman)
        { Id = id; Global = global; Player = player; Tribe = tribe; TribeGlobal = tribeGlobal;
          Command = command; State = state; X = x; Y = y; Control = control;
          BuildingTile = buildingTile; Maceman = maceman; Valid = global != 0 && player >= 1 && player <= 8; }
        internal bool SameIdentity(MacemenUnit other) => Valid && other.Valid &&
            Id == other.Id && Global == other.Global && Player == other.Player &&
            Control == other.Control && Tribe == other.Tribe && TribeGlobal == other.TribeGlobal;
    }

    internal readonly struct MacemenGate
    {
        internal readonly int Id, Owner, Capturer, RawState, Walkable;
        internal readonly uint Global;
        internal MacemenGate(int id, uint global, int owner, int capturer, int rawState, int walkable)
        { Id = id; Global = global; Owner = owner; Capturer = capturer; RawState = rawState; Walkable = walkable; }
    }

    internal readonly struct MacemenSample
    {
        internal readonly long Call, Timestamp, Generation;
        internal readonly int Epoch, Target1, Target2, SearchKind, ComparisonSite, MoveType, OrderTarget1, OrderTarget2;
        internal readonly long ReturnValue, RejectedEdges;
        internal readonly MacemenUnit Before, After;
        internal readonly MacemenGate Gate;
        internal readonly bool Stable;
        internal MacemenSample(long call, long timestamp, int epoch, long generation, int t1, int t2,
            int searchKind, long result, long rejected, MacemenUnit before, MacemenUnit after,
            MacemenGate gate, bool stable, int comparisonSite = -1, int moveType = -1, int orderTarget1 = 0, int orderTarget2 = 0)
        { Call = call; Timestamp = timestamp; Epoch = epoch; Generation = generation;
          Target1 = t1; Target2 = t2; SearchKind = searchKind; ReturnValue = result;
          RejectedEdges = rejected; Before = before; After = after; Gate = gate; Stable = stable; ComparisonSite = comparisonSite;
          MoveType = moveType; OrderTarget1 = orderTarget1; OrderTarget2 = orderTarget2; }
        internal string Format() => "call=" + Call + ",observedTicks=" + Timestamp +
            ",epoch=" + Epoch + ",generation=" + Generation + ",unit=" + Before.Id + "/" + Before.Global +
            ",tribe=" + Before.Tribe + "/" + Before.TribeGlobal + ",control=" + Before.Control +
            ",start=" + Before.X + "/" + Before.Y + ",target=" + Target1 + "/" + Target2 +
            ",orderTarget=" + OrderTarget1 + "/" + OrderTarget2 + ",moveType=" + MoveType +
            ",storedCommand=" + Before.Command + "->" + After.Command +
            ",unitState=" + Before.State + "->" + After.State +
            ",contextBuildingTile=" + Before.BuildingTile + "->" + After.BuildingTile +
            ",gate=" + Gate.Id + "/" + Gate.Global + ",owner=" + Gate.Owner +
            ",capturer=" + Gate.Capturer + ",gateRaw=" + Gate.RawState + ",walkableRaw=" + Gate.Walkable +
            ",searchKind=" + SearchKind + ",return=" + ReturnValue + ",identityStable=" + Stable;
    }

    internal readonly struct MacemenKey : IEquatable<MacemenKey>
    {
        internal readonly MacemenStage Stage;
        internal readonly MacemenTarget Target;
        internal readonly int Player, Command, SearchKind, BeforeCommand, AfterCommand, BeforeState, AfterState, Result, ComparisonSite, MoveType;
        internal readonly MacemenGate Gate;
        internal MacemenKey(MacemenStage stage, MacemenTarget target, int command, int result, MacemenSample sample)
        { Stage = stage; Target = target; Command = command; Result = result; Player = sample.Before.Player;
          SearchKind = sample.SearchKind; ComparisonSite = sample.ComparisonSite; MoveType = sample.MoveType; Gate = sample.Gate; BeforeCommand = sample.Before.Command;
          AfterCommand = sample.After.Command; BeforeState = sample.Before.State; AfterState = sample.After.State; }
        public bool Equals(MacemenKey b) => Stage == b.Stage && Target == b.Target && Player == b.Player &&
            Command == b.Command && SearchKind == b.SearchKind && ComparisonSite == b.ComparisonSite && BeforeCommand == b.BeforeCommand &&
            AfterCommand == b.AfterCommand && BeforeState == b.BeforeState && AfterState == b.AfterState &&
            Result == b.Result && MoveType == b.MoveType && Gate.Id == b.Gate.Id && Gate.Global == b.Gate.Global &&
            Gate.Owner == b.Gate.Owner && Gate.Capturer == b.Gate.Capturer &&
            Gate.RawState == b.Gate.RawState && Gate.Walkable == b.Gate.Walkable;
        public override bool Equals(object obj) => obj is MacemenKey key && Equals(key);
        public override int GetHashCode()
        { unchecked { int h = (int)Stage; h = h * 31 + (int)Target; h = h * 31 + Player;
          h = h * 31 + Command; h = h * 31 + SearchKind; h = h * 31 + BeforeCommand;
          h = h * 31 + AfterCommand; h = h * 31 + BeforeState; h = h * 31 + AfterState;
          h = h * 31 + Result; h = h * 31 + Gate.Id; h = h * 31 + (int)Gate.Global;
          h = h * 31 + Gate.Owner; h = h * 31 + Gate.Capturer;
          h = h * 31 + Gate.RawState; h = h * 31 + Gate.Walkable; h = h * 31 + MoveType; return h * 31 + ComparisonSite; } }
        internal string Format() => "stage=" + Stage + ",player=" + Player + ",targetKind=" + Target +
            ",incomingCommand=" + Command + ",moveType=" + MoveType + ",storedCommand=" + BeforeCommand + "->" + AfterCommand +
            ",state=" + BeforeState + "->" + AfterState + ",gate=" + Gate.Id + "/" + Gate.Global +
            ",owner=" + Gate.Owner + ",capturer=" + Gate.Capturer + ",gateRaw=" + Gate.RawState +
            ",walkableRaw=" + Gate.Walkable + ",searchKind=" + SearchKind + ",comparisonSite=" + ComparisonSite + ",resultClass=" + Result +
            (Stage == MacemenStage.GateComparison ? ",nativeComparison=" + ((Result & 1) != 0 ? "reject" : "accept") +
                ",effectiveComparison=" + ((Result & 2) != 0 ? "reject" : "accept") : "");
    }

    internal sealed class MacemenDiagnosticAggregate
    {
        internal sealed class Bucket
        {
            internal long Count, TargetChanges, RejectedEdges, Unstable;
            internal MacemenSample First, Last;
        }
        private readonly Dictionary<MacemenKey, Bucket> rows = new Dictionary<MacemenKey, Bucket>();
        internal long Total { get; private set; }
        internal int ActiveCombinations => rows.Count;
        internal void Record(MacemenStage stage, MacemenTarget target, int command, int result, MacemenSample sample)
        {
            var key = new MacemenKey(stage, target, command, result, sample);
            if (!rows.TryGetValue(key, out Bucket row))
                rows.Add(key, row = new Bucket { First = sample });
            else if (row.Last.Target1 != sample.Target1 || row.Last.Target2 != sample.Target2) row.TargetChanges++;
            row.Count++; row.Last = sample; row.RejectedEdges += sample.RejectedEdges;
            if (!sample.Stable) row.Unstable++;
            Total++;
        }
        internal void Drain(Action<MacemenKey, Bucket> emit)
        { foreach (var pair in rows) emit(pair.Key, pair.Value); rows.Clear(); }
        internal void Reset() { rows.Clear(); Total = 0; }
        internal static MacemenTarget Classify(int command, bool exactGate, bool targetOrder)
        {
            if (!targetOrder || command == 3) return exactGate ? MacemenTarget.GroundOnGate : MacemenTarget.GroundElsewhere;
            if (command == 9 || command == 36) return exactGate ? MacemenTarget.GateAttack : MacemenTarget.OtherBuildingAttack;
            if (command == 38) return exactGate ? MacemenTarget.GateApproach : MacemenTarget.OtherBuildingApproach;
            if (command == 6 || command == 7) return MacemenTarget.Moat;
            if (command == 23) return MacemenTarget.Wall;
            if (command == 4) return MacemenTarget.Unit;
            return MacemenTarget.Unknown;
        }
    }
}
