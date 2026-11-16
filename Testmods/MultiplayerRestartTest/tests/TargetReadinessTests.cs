using System;
using MultiplayerRestartTest;

// Callback order is intentionally varied. The fixture never models or invokes the
// runtime: it checks the target identity and positive-receipt contract directly.
internal static class TargetReadinessTests
{
    private static int checks;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }
    private static void Throws<T>(Action action, string reason) where T : Exception
    {
        bool rejected = false;
        try { action(); } catch (T) { rejected = true; }
        Check(rejected, reason);
    }
    private static RestartTargetState Bound(bool inactive)
    {
        var state = new RestartTargetState(41);
        if (inactive) state.NoteInactive();
        state.AuthorizeLaunch();
        state.BindTarget(42);
        return state;
    }
    internal static void Run()
    {
        Throws<ArgumentOutOfRangeException>(() => new RestartTargetState(0), "Zero source accepted.");
        Throws<ArgumentOutOfRangeException>(() => new RestartTargetState(-1), "Negative source accepted.");
        var unauthorized = new RestartTargetState(41);
        Throws<InvalidOperationException>(() => unauthorized.BindTarget(42), "Target bound without a launch grant.");
        Check(!unauthorized.MarkManagedReady(42) && !unauthorized.MarkPlayStateReady(42) &&
            !unauthorized.ObserveNativeStarted(42, true, true) && !unauthorized.IsReady,
            "Unbound receipts were accepted.");
        unauthorized.AuthorizeLaunch();
        Throws<ArgumentOutOfRangeException>(() => unauthorized.BindTarget(0), "Zero target accepted.");
        Throws<ArgumentOutOfRangeException>(() => unauthorized.BindTarget(-1), "Negative target accepted.");
        Throws<InvalidOperationException>(() => unauthorized.BindTarget(41), "Source was rebound as target.");
        unauthorized.BindTarget(42);
        Throws<InvalidOperationException>(() => unauthorized.AuthorizeLaunch(), "A second launch grant was accepted.");
        Throws<InvalidOperationException>(() => unauthorized.BindTarget(43), "A different replacement target was accepted.");
        Check(unauthorized.MatchesTarget(42) && !unauthorized.MatchesTarget(41) &&
            !unauthorized.MatchesTarget(43) && !unauthorized.MatchesTarget(0), "Target identity changed after rejected binds.");

        int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        foreach (int[] order in orders)
        {
            var state = Bound(true);
            foreach (long wrong in new long[] { 0, 41, 43 })
                Check(!state.MarkManagedReady(wrong) && !state.MarkPlayStateReady(wrong) &&
                    !state.ObserveNativeStarted(wrong, true, true), "Foreign mission receipt was accepted.");
            Check(!state.IsReady, "Foreign receipts completed the target.");
            for (int i = 0; i < order.Length; i++)
            {
                bool accepted = order[i] == 0 ? state.MarkManagedReady(42) :
                    order[i] == 1 ? state.MarkPlayStateReady(42) : state.ObserveNativeStarted(42, true, true);
                Check(accepted && state.IsReady == (i == order.Length - 1), "Receipt order changed readiness.");
                state.BindTarget(42); // A repeated matching bind must preserve prior receipts.
            }
            Check(state.MarkManagedReady(42) && state.MarkPlayStateReady(42) &&
                state.ObserveNativeStarted(42, true, true) && state.IsReady, "Duplicate matching receipts reset readiness.");
        }

        var stale = Bound(false);
        stale.MarkManagedReady(42); stale.MarkPlayStateReady(42);
        Check(!stale.ObserveNativeStarted(42, true, true) && !stale.IsReady,
            "Stale active flags completed the target without an inactive observation.");
        stale.NoteInactive();
        Check(!stale.IsReady, "Adding inactive proof reused the previously rejected active observation.");
        Check(!stale.ObserveNativeStarted(42, false, true) && !stale.IsReady, "Inactive multiplayer completed startup.");
        Check(!stale.ObserveNativeStarted(42, true, false) && !stale.IsReady, "Stopped simulation completed startup.");
        Check(stale.ObserveNativeStarted(42, true, true) && stale.IsReady, "Fresh matching native receipt failed.");

        foreach (bool committed in new[] { false, true })
            foreach (bool departure in new[] { false, true })
            {
                Check(stale.CanIgnoreSourceEnd(41, committed, departure) == (committed && departure),
                    "Source End suppression exceeded the committed expected departure.");
                Check(!stale.CanIgnoreSourceEnd(42, committed, departure) &&
                    !stale.CanIgnoreSourceEnd(43, committed, departure), "Target or unrelated End was suppressed.");
            }
        Console.WriteLine("Target readiness fixtures passed: " + checks + " checks.");
    }
}
