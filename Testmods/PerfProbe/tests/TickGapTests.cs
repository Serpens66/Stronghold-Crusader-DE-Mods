using System;

namespace PerfProbe
{
    // Executes the production tick-gap classification and sim->main hand-off. Regressions covered:
    // pauses/loading producing false stall lines, stalls right after unpausing being hidden, slow game
    // speeds reported as stalls, gaps across missions, and a blocking or reordering hand-off.
    public static class TickGapTests
    {
        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        public static string Run()
        {
            const long threshold = 100, expected = 18;
            long active;
            Check(!PerfProbeTickGap.Classify(1000, 1050, 0, expected, threshold, 5, 6, out active), "Normal tick interval reported");
            Check(PerfProbeTickGap.Classify(1000, 1200, 0, expected, threshold, 5, 6, out active) && active == 1000, "Unhalted stall missed");
            Check(!PerfProbeTickGap.Classify(1000, 1110, 0, expected, threshold, 5, 6, out active), "Expected game-speed interval counted as stall");
            Check(!PerfProbeTickGap.Classify(1000, 1200, 1190, expected, threshold, 5, 6, out active) &&
                PerfProbeTickGap.ExcludedByHalt(1000, 1200, 1190, expected, threshold), "Pause was reported or not counted as excluded");
            Check(PerfProbeTickGap.Classify(1000, 1300, 1100, expected, threshold, 5, 6, out active) && active == 1100,
                "Stall after unpausing hidden or halted part not removed");
            Check(!PerfProbeTickGap.Classify(1000, 1200, 1300, expected, threshold, 5, 6, out active) && active == 1200,
                "Halt observed after the tick stamp must leave no active gap");
            Check(!PerfProbeTickGap.Classify(1000, 1150, 0, 100, threshold, 5, 6, out active), "Slow game speed (100 per tick) reported");
            Check(!PerfProbeTickGap.Classify(1000, 5000, 0, expected, threshold, 900, 1, out active), "Gap across missions reported");
            Check(!PerfProbeTickGap.Classify(0, 5000, 0, expected, threshold, -1, 1, out active), "First tick reported");
            Check(!PerfProbeTickGap.Classify(1000, 5000, 0, expected, 0, 5, 6, out active), "Disabled monitor reported");

            var queue = new PerfProbeGapQueue(2);
            PerfProbeTickGap gap;
            Check(!queue.TryDequeue(out gap), "Empty queue returned an entry");
            for (int i = 1; i <= 3; i++) queue.TryEnqueue(new PerfProbeTickGap { TickAfter = i });
            Check(queue.Dropped == 1, "Overflow not counted");
            Check(queue.TryDequeue(out gap) && gap.TickAfter == 1, "FIFO order lost");
            queue.TryEnqueue(new PerfProbeTickGap { TickAfter = 4 });
            Check(queue.TryDequeue(out gap) && gap.TickAfter == 2 && queue.TryDequeue(out gap) && gap.TickAfter == 4 && !queue.TryDequeue(out gap),
                "Wrap-around order lost");
            return "PASS tick gaps: pause/load exclusion, stall after unpause, game speed, mission reset, bounded FIFO hand-off.";
        }
    }
}
