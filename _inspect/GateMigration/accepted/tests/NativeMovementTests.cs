using System;
using System.Linq;
using System.Runtime.InteropServices;
using RedBird.X64.Assembly;

namespace EnemyGatePathfindingTest
{
    internal static unsafe class NativeMovementTests
    {
        internal static int Run()
        {
            int checks = 0;
            void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
            bool Rule(int d = 0, int consumed = 0, int index = 16, int length = 19, int mode = 0,
                int special = 0, int alternate = 0, int state = 101, bool enabled = true,
                byte source = 0, byte target = 0, uint sourceFlags = 0x100, uint targetFlags = 0) =>
                AssassinGateDescentRule.Allows(d,consumed,index,length,mode,special,alternate,state,enabled,source,target,1,16,sourceFlags,targetFlags);
            Check(Rule() && Rule(index:11,length:13), "both logged index/length pairs");
            for (int d = -1; d <= 8; d++) Check(Rule(d:d,consumed:d) == (d >= 0 && d < 8 && (d & 1) == 0), "four orientations/cardinal only");
            Check(!Rule(consumed:4) && !Rule(index:0) && !Rule(index:20) && !Rule(length:2001), "consumed direction and bounded route");
            Check(!Rule(mode:1) && !Rule(special:1) && !Rule(alternate:1) && !Rule(state:106) && !Rule(enabled:false), "native mode, combat, climb switch");
            Check(!Rule(source:1) && !Rule(target:16) && !Rule(sourceFlags:0) && !Rule(targetFlags:0x100), "ground, reverse connection and roof-to-roof excluded");
            for (int bit = 0; bit < 32; bit++) if ((0x4A5014B1U & (1U << bit)) != 0) Check(!Rule(targetFlags:1U << bit), "ambiguous/blocked target surface");
            IntPtr memory = Marshal.AllocHGlobal(0x9302C8);
            try
            {
                Marshal.WriteInt32(memory, 0x9302C4, 1);
                var sample = new NativeAssassinMovementDiagnostics.Sample {
                    Valid = true, Unit = 1, Global = 21, Player = 2, Gate = 4, GateGlobal = 7,
                    GateState = 2, GateContextRaw = 1, Length = 15, Index = 3, X = 8, Y = 9, TX = 12, TY = 9,
                    Epoch = 1, Generation = 4, Snapshot = new object() };
                var baseline = sample;
                var d = new NativeAssassinMovementDiagnostics((ulong)memory.ToInt64(), id => sample);
                X64SmartCPUContext* c = stackalloc X64SmartCPUContext[1]; *c = default; c->RBX = 1; c->RSP = 4096; c->RAX = 1;
                IntPtr ptr = (IntPtr)c;
                void Pair(int site, Action mutation, string outcome)
                {
                    d.Reset(); sample = baseline; c->RSP = 4096; c->RAX = 1;
                    d.Observe(site, ptr, false); mutation(); d.Observe(site, ptr, true);
                    string output = string.Join("\n", d.Drain());
                    Check(output.Contains("result=" + outcome + ",count=1"), outcome);
                    Check(output.Contains("unmatchedPairs=0"), "paired " + outcome);
                }
                Pair(10, () => { sample.Length = sample.Index = 0; }, "closed-gate-route-cleared");
                Pair(10, () => { }, "closed-gate-stop-without-clear");
                Pair(11, () => { c->RAX = 0; }, "step-rejected");
                Pair(11, () => { sample.State = 126; }, "ascent-state-selected");
                Pair(11, () => { sample.State = 128; }, "descent-state-selected");
                Pair(11, () => { }, "step-accepted-no-climb-state");
                Pair(12, () => { sample.GateContextRaw = 0; }, "gate-context-writer-changed");
                Pair(13, () => { sample.PathState = 8; }, "route-consumed");
                Pair(14, () => { sample.State = 101; }, "ascent-completed");
                Pair(15, () => { sample.State = 101; }, "descent-completed");
                Pair(16, () => { sample.State = 106; }, "combat-interruption");
                Pair(17, () => { d.NoteBuilder(); }, "combat-finish-resume-call");
                Pair(11, () => { sample.Global++; }, "unverified-identity-or-policy");
                Pair(11, () => { sample.Epoch++; }, "unverified-identity-or-policy");
                Pair(11, () => { sample.Snapshot = new object(); }, "unverified-identity-or-policy");
                Pair(11, () => { sample.Generation++; }, "unverified-identity-or-policy");
                d.Reset(); sample = baseline; c->RAX = 1;
                d.Observe(18, ptr, false);
                sample.X = sample.TX; sample.Y = sample.TY; d.Observe(18, ptr, false);
                string completion = string.Join("\n", d.Drain());
                Check(completion.Contains("native-completion-target-differs") && completion.Contains("native-completion-current-target-equal"), "completion is not proof of original destination");
                d.Reset(); sample = baseline; c->RBP = 0;
                d.Observe(11, ptr, false); sample.State = 128; d.Observe(11, ptr, true);
                sample = baseline; c->RBP = 1;
                d.Observe(11, ptr, false); sample.State = 128; d.Observe(11, ptr, true); c->RBP = 0;
                string modes = string.Join("\n", d.Drain());
                Check(modes.Contains("movementCall=regular") && modes.Contains("movementCall=speculative-restored-by-caller") && !modes.Contains("descent-started"), "speculation separate from actual execution");
                d.NoteDescentException(1); Check(d.Drain().First().Contains("closed-gate-descent-exception"), "narrow exception observed");
                // Real observer nesting: outer combat-resume, inner step; publication belongs to the innermost call only.
                d.Reset(); sample = baseline; c->RSP = 4096; d.Observe(17, ptr, false);
                c->RSP = 4000; d.Observe(11, ptr, false); d.NoteBuilder(); d.Observe(11, ptr, true);
                c->RSP = 4096; d.Observe(17, ptr, true);
                var nested = d.Drain().ToArray();
                Check(nested.Single(x => x.Contains("site=11,")).Contains("nestedBuilders=1"), "nested builder");
                Check(nested.Single(x => x.Contains("site=17,")).Contains("nestedBuilders=0"), "no inferred outer builder");
                d.Reset(); sample = baseline;
                for (int i = 0; i < 100000; i++)
                {
                    sample.Unit = 1 + i % 100; sample.Global = (uint)(1000 + i); sample.TX = i;
                    d.Observe(11, ptr, false); d.Observe(11, ptr, true);
                }
                string[] rows = d.Drain().ToArray();
                Check(rows.Length == 2 && rows[0].Contains("count=100000") && rows[0].Contains("targetChanges=99999"), "complete compact long run");
                Check(d.Drain().Count() == 1, "no retained event history");
                for (int i = 0; i < 64; i++) { sample.Gate = i + 1; sample.GateGlobal = (uint)(i + 100); d.Observe(11, ptr, false); d.Observe(11, ptr, true); }
                Check(d.Drain().Count() == 65, "more than 32 distinct gate combinations retained");
                d.Observe(11, ptr, false); d.Close();
                Check(d.Drain().Single().Contains("unmatchedPairs=1"), "unfinished pair remains explicit");
                d.Reset(); Check(d.Drain().Single().Contains("observations=0,unmatchedPairs=0"), "epoch reset");
            }
            finally { Marshal.FreeHGlobal(memory); }
            Console.WriteLine("PASS: physical execution observer: paired branches, identity/policy changes, nested calls, 100000 events and bounded aggregate history.");
            return checks;
        }
    }
}
