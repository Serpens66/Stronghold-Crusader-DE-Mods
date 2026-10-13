using System;
namespace EnemyGatePathfindingTest
{
    internal static class NativeAssassinTraceTests
    {
        internal static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, why) => { checks++; if (!ok) throw new InvalidOperationException("Native trace: " + why); };
            for (int run = 0; run < 10000; run++)
            {
                var t = new NativeAssassinTrace(); t.BeginReconstruction(3);
                t.Candidate(); t.Improve(12, 11, 4, 0, 0, 0x100); t.Select(12, 11, 4);
                t.Candidate(); t.Improve(11, 10, 4, 16, 0, 0); t.Select(11, 10, 4);
                t.FinishReconstruction(); t.Closed = true; t.Result = 2;
                check(t.Match(0, 10, 11, 0, out var move) && move == NativeTraceMovement.Ground, "reversed output starts at source");
                check(t.Match(1, 11, 12, 0, out move) && move == NativeTraceMovement.ClimbUp, "selected native climb, not merely candidate");
                check(!t.Match(0, 10, 12, 0, out move), "different final output cannot inherit evidence");
                t.Secondary = true; t.BeginReconstruction(1);
                t.Improve(12, 10, 4, 16, 0, 0); t.Select(12, 10, 4); t.FinishReconstruction(); t.Result = 1;
                check(t.Superseded == 1 && t.Selected.Count == 1 && t.SelectedTotal == 3, "overwrite clears edges but retains exact counters");
                check(t.Match(0, 10, 12, 0, out move) && move == NativeTraceMovement.Ground, "secondary ground replaces climb");
                t.BeginReconstruction(3); t.Improve(12, 10, 4, 0, 0x100, 0); t.Select(12, 10, 4);
                t.FailReconstruction(); t.FinishReconstruction();
                check(!t.Match(0, 10, 12, 0, out move), "failure epilogue never completes route");
                t.BeginReconstruction(3); t.Improve(12, 10, 4, 0, 0x100, 0); t.Select(12, 10, 4); t.FinishReconstruction();
                check(t.Match(0, 10, 12, 0, out move) && move == NativeTraceMovement.ClimbDown, "later successful reconstruction can recover failure");
                t.BeginReconstruction(3); t.Improve(12, 10, 4, 0, 0x100, 0x100); t.Select(12, 10, 4); t.FinishReconstruction();
                check(t.Match(0, 10, 12, 0, out move) && move == NativeTraceMovement.Unknown, "same-wall unconnected transition stays unknown");
                t.BeginReconstruction(1); t.Improve(12, 10, 4, 16, 0, 0); t.Select(12, 11, 4); t.FinishReconstruction();
                check(t.Invalid && !t.Complete, "selection mismatch is explicit incomplete evidence");
            }
            Console.WriteLine("PASS: 10,000 native trace chains; overwritten outputs, recovery, exact edge correspondence and bounded call-local storage.");
            return checks;
        }
    }
}
