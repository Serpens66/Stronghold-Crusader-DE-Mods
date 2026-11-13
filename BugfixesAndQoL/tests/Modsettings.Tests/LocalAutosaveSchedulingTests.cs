using System;
using BugfixesAndQoL;

internal static partial class Program
{
    // The production scheduler receives a deterministic monotonic clock. No sleeps,
    // simulated Unity components or duplicate scheduling implementation are needed.
    private static void LocalAutosaveSchedulingTests()
    {
        const long frequency = 100;
        long At(int seconds) => seconds * frequency;
        LocalAutosaveSchedule New(int minutes = 20, bool enabled = true) =>
            new LocalAutosaveSchedule(71, minutes, 0, frequency, enabled);
        void Run(LocalAutosaveSchedule s, int seconds)
        { s.Sample(0, true); s.Sample(At(seconds), true); }

        var schedule = New();
        Run(schedule, 420);
        Check(schedule.ActiveElapsed == At(420), "active real time is accumulated without game speed input");
        schedule.SetMinutes(10, At(420));
        Check(schedule.ActiveElapsed == At(420) && !schedule.IsDue(At(420)), "positive interval edit keeps seven active minutes");
        schedule.Sample(At(420), true); schedule.Sample(At(600), true);
        Check(schedule.IsDue(At(600)), "seven minutes followed by three reaches the new ten-minute interval");

        schedule = New(); Run(schedule, 420);
        schedule.Sample(At(420), false);
        schedule.Sample(At(5000), false);
        schedule.Sample(At(5001), true);
        Check(schedule.ActiveElapsed == At(420), "a long pause and its resume segment add no active time");
        schedule.SetMinutes(5, At(5001));
        Check(schedule.IsDue(At(5001)), "shortening below accumulated real time is immediately due");
        Check(schedule.TryQueue(At(5001), out long ticket), "one due save can be queued");
        Check(!schedule.TryQueue(At(5001), out _), "due frames do not queue duplicate saves");
        schedule.SetMinutes(10, At(5001));
        Check(!schedule.IsCurrentAttempt(ticket, At(5001)), "positive interval edits invalidate queued decisions");
        schedule.SetMinutes(5, At(5001));
        Check(schedule.TryQueue(At(5001), out ticket), "shortening allows a new decision at the same active age");
        schedule.Complete(ticket, LocalAutosaveAttemptResult.Saved, At(5005));
        Check(schedule.ActiveElapsed == 0 && !schedule.IsDue(At(99999)), "success resets progress with no overdue catch-up series");
        schedule.Sample(At(5005), true); schedule.Sample(At(5304), true);
        Check(!schedule.IsDue(At(5304)), "time spent in the writer is excluded from the new interval");
        schedule.Sample(At(5305), true);
        Check(schedule.IsDue(At(5305)), "a full new interval is due after successful publication");

        schedule = New(5); Run(schedule, 300);
        schedule.SetMinutes(5, At(300));
        Check(schedule.ActiveElapsed == At(300), "committing an unchanged value preserves progress");
        Check(schedule.TryQueue(At(300), out ticket), "five active minutes are due");
        schedule.Complete(ticket, LocalAutosaveAttemptResult.Deferred, At(300));
        Check(schedule.ActiveElapsed == At(300) && !schedule.IsDue(At(300) + 99) && schedule.IsDue(At(301)),
            "a temporary refusal preserves progress and waits one real second");
        Check(schedule.TryQueue(At(301), out ticket), "a deferred save becomes eligible again");
        schedule.Complete(ticket, LocalAutosaveAttemptResult.Failed, At(301));
        Check(schedule.ActiveElapsed == At(300) && !schedule.IsDue(At(600)) && schedule.IsDue(At(601)),
            "a writer failure preserves last-success age but waits the failure-time interval");
        schedule.SetMinutes(1, At(400));
        Check(!schedule.IsDue(At(401)) && schedule.IsDue(At(601)), "positive edits cannot bypass error cooldown");
        schedule.SetMinutes(0, At(601));
        Check(schedule.ActiveElapsed == 0 && !schedule.IsDue(At(9999)), "off discards accumulated progress");
        schedule.SetMinutes(1, At(700)); schedule.Sample(At(700), true); schedule.Sample(At(760), true);
        Check(schedule.IsDue(At(760)), "off then on begins fresh and clears the old error cooldown");
        Check(schedule.TryQueue(At(760), out ticket), "reactivated timer can queue");
        schedule.SetEnabled(false, At(760));
        Check(!schedule.IsCurrentAttempt(ticket, At(760)) && !schedule.IsDue(At(9999)), "master disable cancels pending work");
        schedule.SetEnabled(true, At(800)); schedule.Sample(At(800), true); schedule.Sample(At(859), true);
        Check(!schedule.IsDue(At(859)), "reenabling starts fresh with the selected interval");
        schedule.Sample(At(860), true); Check(schedule.TryQueue(At(860), out ticket), "fresh reenabled interval becomes due");
        schedule.Cancel(At(860));
        Check(!schedule.IsCurrentAttempt(ticket, At(900)), "mission cancellation invalidates queued work");
        schedule.Complete(ticket, LocalAutosaveAttemptResult.Saved, At(900));
        Check(!schedule.IsDue(At(9999)), "a stale save completion cannot revive a canceled mission");

        foreach (int minutes in new[] { 1, 7, 1440 })
        {
            schedule = New(minutes); Run(schedule, minutes * 60 - 1);
            Check(!schedule.IsDue(At(minutes * 60 - 1)), "arbitrary interval is not early: " + minutes);
            schedule.Sample(At(minutes * 60), true);
            Check(schedule.IsDue(At(minutes * 60)), "arbitrary interval reaches exact boundary: " + minutes);
        }
        schedule = New(1, false); Run(schedule, 1000);
        Check(schedule.ActiveElapsed == 0 && !schedule.IsDue(At(1000)), "disabled option never accumulates or queues");

        int parsed;
        foreach (string text in new[] { "0", "Aus", "aUs", "  Aus  " })
            Check(LocalAutosaveInputPolicy.TryParse(text, "Aus", out parsed) && parsed == 0, "zero and localized off: " + text);
        foreach (string text in new[] { "1", "7", "1440", " 10 " })
            Check(LocalAutosaveInputPolicy.TryParse(text, "Aus", out parsed), "valid free minute input: " + text);
        foreach (string text in new[] { "", " ", "-1", "+1", "1.5", "1,5", "1441", "99999999999999999", "10 min", "Aus1" })
            Check(!LocalAutosaveInputPolicy.TryParse(text, "Aus", out parsed), "invalid drafts do not become intervals: " + text);
        Check(LocalAutosaveInputPolicy.Format(0, "Aus") == "Aus" && LocalAutosaveInputPolicy.Format(7, "Aus") == "7",
            "display maps only zero to localized off");
        Check(LocalAutosaveInputPolicy.Step("Aus", "Aus", 20, 1) == 1 &&
            LocalAutosaveInputPolicy.Step("0", "Aus", 20, -1) == 0 &&
            LocalAutosaveInputPolicy.Step("1440", "Aus", 20, 1) == 1440,
            "spinner starts from off and clamps both boundaries");
        Check(LocalAutosaveInputPolicy.Step("7", "Aus", 20, 1) == 8 &&
            LocalAutosaveInputPolicy.Step("invalid", "Aus", 20, -1) == 19,
            "spinner uses a valid draft or falls back to the committed interval");

        var pending = new LocalAutosavePendingChoice();
        schedule = New(20); Run(schedule, 420);
        pending.Set(schedule, 0, At(420));
        Check(schedule.Minutes == 20 && schedule.ActiveElapsed == At(420) && pending.EditorMinutes(schedule) == 0,
            "editing through off updates the editor without resetting the running schedule");
        pending.Set(schedule, 10, At(425));
        Check(pending.RemainingTicks(At(435), frequency) == At(5),
            "an earlier coalesced idle callback finds the remaining debounce delay");
        Check(pending.RemainingTicks(At(440), frequency) == 0,
            "the final choice is eligible after fifteen idle seconds");
        Check(pending.TryTake(schedule, out int choice) && choice == 10,
            "idle or menu-close takes only the latest authorized numeric choice");
        schedule.SetMinutes(choice, At(440));
        Check(schedule.ActiveElapsed == At(420), "passing through zero in a pending edit does not reset elapsed time");
        Check(!pending.TryTake(schedule, out _) && pending.RemainingTicks(At(441), frequency) < 0,
            "an applied edit has no second delayed application");

        pending.Set(schedule, 14, At(450));
        pending.Invalidate(At(451));
        Check(pending.RemainingTicks(At(999), frequency) < 0 && !pending.TryTake(schedule, out _),
            "an invalid draft prevents both idle and close from committing its numeric prefix");
        pending.Set(schedule, 7, At(500));
        pending.Clear();
        Check(pending.EditorMinutes(schedule) == schedule.Minutes && !pending.TryTake(schedule, out _),
            "Escape discards unapplied edits without undoing an already applied interval");
        pending.Set(schedule, 1, At(510));
        var replacement = New(20);
        Check(!pending.TryTake(replacement, out _), "a pending edit cannot target a replacement mission");
        pending.Set(schedule, 0, At(520));
        Check(pending.TryTake(schedule, out choice) && choice == 0,
            "closing the menu applies a valid final off choice without waiting for idle");
        schedule.SetMinutes(choice, At(520));
        Check(schedule.Minutes == 0 && schedule.ActiveElapsed == 0,
            "only an actually applied off choice resets the scheduler");
    }
}
