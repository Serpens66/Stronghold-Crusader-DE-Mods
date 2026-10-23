// TEMP_GATE_MIGRATION_DIAGNOSTICS: retained for old/new acceptance; see removal inventory.
// TEMP_GATE_ROUTE_ACCEPTANCE: synchronous execution evidence, no retained unit/route history.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Assembly;

namespace BugfixesAndQoL.GateRouting
{
    internal sealed unsafe partial class NativeAssassinMovementDiagnostics
    {
        internal struct Sample
        {
            internal int Unit, Player, State, Command, X, Y, TX, TY, Length, Index, PathState, Direction, NextDirection;
            internal int BaseHeight, RenderOffset, ClimbHeight, ClimbProgress, WorldX, WorldY;
            internal int InvocationMode; // 0 regular 1855A0, 1 speculative 1855A0, 2 physical dispatcher
            internal int Gate, GateOwner, Capturer, GateState, GateContextRaw;
            internal uint Global, GateGlobal;
            internal ulong Buffer;
            internal long Mono, Epoch, Generation;
            internal object Snapshot;
            internal DateTime Utc;
            internal bool Valid;
            public override string ToString() => "unit=" + Unit + "/" + Global + ",player=" + Player +
                ",movementCall=" + ModeName(InvocationMode) + ",state=" + State + ",storedCommand=" + Command + ",position=" + X + "/" + Y +
                ",world=" + WorldX + "/" + WorldY + ",baseHeightRaw=" + BaseHeight + ",renderOffsetRaw=" + RenderOffset +
                ",climbHeightRaw=" + ClimbHeight + ",climbProgressRaw=" + ClimbProgress +
                ",target=" + TX + "/" + TY + ",routeBuffer=0x" + Buffer.ToString("X") +
                ",index=" + Index + ",length=" + Length + ",pathState=" + PathState +
                ",consumedDirectionRaw=" + Direction + ",nextDirection=" + NextDirection +
                ",gate=" + Gate + "/" + GateGlobal + ",owner=" + GateOwner + ",capturer=" + Capturer +
                ",gateStateRaw=" + GateState + ",unit40ERaw=" + GateContextRaw +
                ",epoch=" + Epoch + ",policyGeneration=" + Generation + ",mono=" + Mono + ",observedUtc=" + Utc.ToString("O");
        }
        private sealed class Frame
        {
            internal Frame Previous;
            internal Sample Before;
            internal int Site, Builders;
            internal ulong Stack;
        }
        private struct Key : IEquatable<Key>
        {
            internal int Site, Player, Gate, Outcome, Owner, Capturer, GateState, Raw, Mode;
            internal uint Global;
            public bool Equals(Key b) => Site == b.Site && Player == b.Player && Gate == b.Gate && Outcome == b.Outcome && Global == b.Global && Owner == b.Owner && Capturer == b.Capturer && GateState == b.GateState && Raw == b.Raw && Mode == b.Mode;
            public override bool Equals(object b) => b is Key && Equals((Key)b);
            public override int GetHashCode() => ((Site * 397 + Player) * 397 + Gate) * 397 + Outcome;
        }
        private struct Evidence
        {
            internal Sample Before, After;
            internal int NativeResult, Builders;
            internal long Call;
            public override string ToString() => "call=" + Call + ",before=[" + Before + "],after=[" + After +
                "],nativeReturnRaw=" + NativeResult + ",nestedBuilders=" + Builders +
                ",earlierPublicationAssociation=not-inferred";
        }
        private sealed class Row { internal long Count, Changes; internal Evidence First, Last; }
        private readonly Dictionary<Key, Row> rows = new Dictionary<Key, Row>();
        private readonly ulong module;
        private readonly Func<int, Sample> reader;
        [ThreadStatic] private static Frame current;
        private long sequence, total, unmatched;
        internal NativeAssassinMovementDiagnostics(ulong module, Func<int, Sample> reader) { this.module = module; this.reader = reader; }
        internal void Reset() { current = null; rows.Clear(); sequence = total = unmatched = 0; }
        internal void Close() { while (current != null) { unmatched++; current = current.Previous; } }
        internal void NoteBuilder() { if (current != null) current.Builders++; }
        internal void Observe(int site, IntPtr pointer, bool post)
        {
            var c = (X64SmartCPUContext*)pointer;
            if (!post)
            {
                // Sites in 1855A0 retain the unit in EBX; the Assassin dispatcher uses its current-unit global.
                int unit = site == 14 || site == 15 || site == 17 || site == 18
                    ? *(int*)(module + 0x9302C4) : (int)c->RBX;
                Sample before = reader(unit);
                before.InvocationMode = site == 14 || site == 15 || site == 17 || site == 18 ? 2 : ((c->RBP & 255) == 0 ? 0 : 1);
                if (site == 18)
                {
                    if (before.Valid && (int)c->RAX != 0)
                        Add(site, before, before, (int)c->RAX, 0);
                    return;
                }
                while (current != null && c->RSP >= current.Stack) { unmatched++; current = current.Previous; }
                current = new Frame { Previous = current, Before = before, Site = site, Stack = c->RSP };
                return;
            }
            Frame f = current;
            if (f == null || f.Site != site || f.Stack != c->RSP) { unmatched++; return; }
            current = f.Previous;
            Sample after = reader(f.Before.Unit); after.InvocationMode = f.Before.InvocationMode;
            Add(site, f.Before, after, (int)c->RAX, f.Builders);
        }
        internal static int Outcome(int site, Sample before, Sample after, int result)
        {
            if (!before.Valid || !after.Valid || before.Global != after.Global || before.Unit != after.Unit ||
                before.Player != after.Player || before.Epoch != after.Epoch || before.Generation != after.Generation ||
                !ReferenceEquals(before.Snapshot, after.Snapshot) || (before.Gate == after.Gate && before.GateGlobal != after.GateGlobal)) return 0;
            if (site == 19) return 17;
            if (site == 10) return after.Length == 0 && after.Index == 0 ? 1 : 2;
            if (site == 11) return result == 0 ? 3 : after.State == 126 ? 4 : after.State == 128 ? 5 : 6;
            if (site == 12) return before.GateContextRaw != after.GateContextRaw ? 7 : 8;
            if (site == 13) return 9;
            if (site == 14) return after.State == 101 ? 10 : 16;
            if (site == 15) return after.State == 101 ? 11 : 16;
            if (site == 16) return after.State == 106 ? 12 : 16;
            if (site == 17) return 13;
            // 188340 can report completion after target clearing. Coordinate equality is a separate observation.
            return before.X == before.TX && before.Y == before.TY ? 14 : 15;
        }
        private static readonly string[] Outcomes = { "unverified-identity-or-policy", "closed-gate-route-cleared", "closed-gate-stop-without-clear",
            "step-rejected", "ascent-state-selected", "descent-state-selected", "step-accepted-no-climb-state", "gate-context-writer-changed",
            "gate-context-writer-unchanged", "route-consumed", "ascent-completed", "descent-completed", "combat-interruption",
            "combat-finish-resume-call", "native-completion-current-target-equal", "native-completion-target-differs", "unexpected-post-state", "closed-gate-descent-exception" };
        private void Add(int site, Sample before, Sample after, int result, int builders)
        {
            int outcome = Outcome(site, before, after, result);
            var key = new Key { Site = site, Player = before.Player, Gate = before.Gate, Outcome = outcome, Global = before.GateGlobal, Owner = before.GateOwner, Capturer = before.Capturer, GateState = before.GateState, Raw = before.GateContextRaw, Mode = before.InvocationMode };
            var evidence = new Evidence { Call = ++sequence, Before = before, After = after, NativeResult = result, Builders = builders };
            if (!rows.TryGetValue(key, out Row row)) rows.Add(key, row = new Row { First = evidence });
            if (row.Count != 0 && (row.Last.Before.TX != before.TX || row.Last.Before.TY != before.TY)) row.Changes++;
            row.Last = evidence; row.Count++; total++;
        }
        private static string ModeName(int value) => value == 1 ? "speculative-restored-by-caller" : value == 2 ? "physical-dispatcher" : "regular";
        internal void NoteDescentException(int unit)
        { Sample s = reader(unit); s.InvocationMode = 0; Add(19, s, s, 0, 0); }
        internal IEnumerable<string> Drain()
        {
            var output = new List<string>();
            foreach (var pair in rows)
                output.Add("TEMP_GATE_ROUTE_ACCEPTANCE execution: player=" + pair.Key.Player + ",gate=" + pair.Key.Gate +
                    "/" + pair.Key.Global + ",gateStateRaw=" + pair.Key.GateState + ",capturer=" + pair.Key.Capturer + ",unit40ERaw=" + pair.Key.Raw + ",movementCall=" + ModeName(pair.Key.Mode) + ",site=" + pair.Key.Site + ",result=" + Outcomes[pair.Key.Outcome] + ",count=" + pair.Value.Count +
                    ",targetChanges=" + pair.Value.Changes + ",first=[" + pair.Value.First + "],last=[" + pair.Value.Last + "]");
            rows.Clear(); output.Sort(StringComparer.Ordinal);
            output.Add("TEMP_GATE_ROUTE_ACCEPTANCE execution-summary: observations=" + total + ",unmatchedPairs=" + unmatched +
                ",physicalExecutionSeparateFromPlannedRoute=true,unexecutedBranchesAreNotPass=true");
            return output;
        }
    }
}
