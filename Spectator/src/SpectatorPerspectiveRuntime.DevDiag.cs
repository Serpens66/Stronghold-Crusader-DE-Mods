#if SERP_DEVDIAG
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorPerspectiveRuntime
    {
        // SelectPlayer runs on the presentation thread. Retain only the current operation,
        // never a history of switches; measuring does not collect or mutate game state.
        private static long diagSwitchNumber, diagSwitchStarted, diagSwitchStageStarted;
        private static int diagSwitchFrom, diagSwitchTo;
        private static string diagSwitchPreviousStage;
        private static bool diagSwitchActive;

        static partial void DiagPerspectiveSwitchBegin(int player)
        {
            if (diagSwitchActive) DiagPerspectiveSwitchStage("superseded", true);
            diagSwitchNumber++;
            diagSwitchFrom = selectedPlayer;
            diagSwitchTo = player;
            diagSwitchPreviousStage = "entry";
            diagSwitchStarted = diagSwitchStageStarted = Stopwatch.GetTimestamp();
            diagSwitchActive = true;
            DiagPerspectiveSwitchStage("entry", false);
        }

        static partial void DiagPerspectiveSwitchStage(string stage, bool completed)
        {
            if (!diagSwitchActive) return;
            long measuredAt = Stopwatch.GetTimestamp();
            double stageMs = (measuredAt - diagSwitchStageStarted) * 1000.0 / Stopwatch.Frequency;
            double totalMs = (measuredAt - diagSwitchStarted) * 1000.0 / Stopwatch.Frequency;
            // Samples are read-only: no forced collection, tree walk or reflected state.
            Shared.DevDiag.Log(() => "SPECTATOR_VIEW_SWITCH switch=" + diagSwitchNumber +
                " session=" + preparedSessionId + " from=" + diagSwitchFrom + " to=" + diagSwitchTo +
                " previous=" + diagSwitchPreviousStage + " stage=" + stage +
                " stageMs=" + stageMs.ToString("F3", CultureInfo.InvariantCulture) +
                " totalMs=" + totalMs.ToString("F3", CultureInfo.InvariantCulture) +
                " heapBytes=" + GC.GetTotalMemory(false) +
                " gc0=" + GC.CollectionCount(0) + " gc1=" + GC.CollectionCount(1) +
                " gc2=" + GC.CollectionCount(2) + " thread=" + Thread.CurrentThread.ManagedThreadId +
                " reportPending=" + reportRefresh.Pending + " alliesPending=" + alliesRefresh.Pending +
                " completed=" + completed);
            diagSwitchPreviousStage = stage;
            // Exclude this marker's sampling/formatting/logging from the next phase duration.
            diagSwitchStageStarted = Stopwatch.GetTimestamp();
            if (!completed) return;
            diagSwitchActive = false;
            diagSwitchStarted = diagSwitchStageStarted = 0;
            diagSwitchPreviousStage = null;
        }
    }
}
#endif
