using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using BugfixesAndQoL.EditorRegions;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    { checks++; if (!value) throw new Exception(name); }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--native-contract")
            {
                File.WriteAllText(args[1], JsonSerializer.Serialize(new {
                    hash = NativeContract.ReferenceHash, rva = NativeContract.RebuildRva, pattern = NativeContract.RebuildPattern }));
                return 0;
            }
            var state = new RefreshState();
            Check(!state.Changed(1) && !state.HasPending, "no mutation before a ready editor session");
            state.Start(1, false, true);
            Check(!state.Changed(1), "ordinary game cannot arm editor repair");
            state.Start(2, true, true);
            Check(!state.HasPending && state.Active, "editor start does not rebuild");
            Check(state.Changed(7), "first edit accepted");
            Check(state.HasPending && !state.CanFlush, "new batch waits for actual main-thread input");
            for (int i = 0; i < 1000; i++)
            {
                state.Changed(7);
                if (state.CanFlush || state.BeginFlush(7, false) != RefreshDecision.None)
                    throw new Exception("unknown input allowed a premature rebuild");
            }
            Sample(state, true);
            for (int i = 0; i < 1000; i++)
            {
                state.Changed(7);
                if (state.CanFlush || state.BeginFlush(7, false) != RefreshDecision.None)
                    throw new Exception("held brush allowed a premature rebuild");
            }
            Check(state.HasPending, "unknown/held passes retain all brush changes");
            Sample(state, false); // Includes release over UI: no KeyManager/UI filter.
            Check(state.BeginFlush(7, true) == RefreshDecision.Rebuild, "many brush changes coalesce");
            Check(state.BeginFlush(7, true) == RefreshDecision.None, "no recursive rebuild");
            state.CompleteFlush();
            Check(!state.HasPending && state.BeginFlush(8, false) == RefreshDecision.None, "one completed pass consumes the batch");
            int retiredTicket = state.PendingInputTicket;
            state.ObservePlacementButton(retiredTicket, false);
            Check(!state.CanFlush, "empty input alone does not create work");
            state.Changed(8); state.Changed(9);
            Sample(state, false);
            Check(state.BeginFlush(9, false) == RefreshDecision.Rebuild, "Vanilla rebuild midway through a stroke cannot cover later unmarked edits");
            state.CompleteFlush();
            state.Changed(9); Sample(state, false);
            Check(state.BeginFlush(10, false) == RefreshDecision.AlreadyRebuilt, "Vanilla rebuild after the latest edit is sufficient");
            Check(!state.HasPending, "Vanilla completed consumes pending work");
            state.Changed(9);
            Sample(state, false);
            Check(state.BeginFlush(10, true) == RefreshDecision.Rebuild, "new dirty edits survive an intervening rebuild");
            state.Changed(10);
            Check(!state.CanFlush, "nested batch cannot inherit the previous release sample");
            Sample(state, false);
            Check(state.BeginFlush(10, true) == RefreshDecision.None, "nested edit cannot recurse");
            state.CompleteFlush();
            Check(state.HasPending && state.BeginFlush(11, true) == RefreshDecision.Rebuild, "nested edit survives completion");
            state.CompleteFlush();
            state.Changed(11);
            Sample(state, false);
            Check(state.BeginFlush(11, false) == RefreshDecision.Rebuild, "clean marker alone is not proof of a rebuild");
            state.CompleteFlush();
            state.Changed(uint.MaxValue);
            Sample(state, false);
            Check(state.BeginFlush(0, false) == RefreshDecision.AlreadyRebuilt, "generation wrap is accepted by inequality");
            state.Changed(1); Sample(state, true); state.Start(2, true, true);
            Check(state.HasPending && !state.CanFlush, "ready-session replay preserves pending held work");
            state.End(1);
            Check(state.HasPending, "an old end cannot clear a newer session");
            retiredTicket = state.PendingInputTicket;
            state.End(2);
            Check(!state.Active && !state.HasPending && !state.Changed(1), "end retires pending work");
            state.Start(3, true, true); state.Changed(1); state.SetEnabled(false);
            Check(!state.Active && !state.HasPending, "master disable cancels work");
            state.SetEnabled(true);
            Check(!state.HasPending, "reenabling does not invent an edit");
            state.Changed(1);
            state.ObservePlacementButton(retiredTicket, false);
            Check(!state.CanFlush, "late input from a previous session cannot release a new batch");
            Sample(state, true);
            Sample(state, false); // Same state publication used on focus loss.
            Check(state.CanFlush, "focus loss ends held deferral");
            state.InvalidatePlacementInput();
            Check(!state.CanFlush, "focus gain requires a fresh physical input sample");
            Sample(state, true);
            Check(!state.CanFlush, "regaining focus with a pressed mouse continues deferral");
            state.Changed(1); state.Start(4, false, true);
            Check(!state.Active && !state.HasPending, "editor to gameplay transition cancels work");
            state.Start(5, true, true);
            state.Changed(1); Sample(state, false);
            int firstTicket = state.PendingInputTicket;
            Check(state.BeginFlush(1, false) == RefreshDecision.Rebuild, "a short click or edit without a held mouse needs no key-up event");
            state.CompleteFlush();
            state.Changed(1);
            state.ObservePlacementButton(firstTicket, false);
            Check(!state.CanFlush, "late input from a completed batch cannot release a later change");
            Sample(state, false); Sample(state, true); state.Changed(2);
            Check(!state.CanFlush, "a quick next stroke defers an unconsumed released batch");
            Sample(state, false);
            Check(state.BeginFlush(2, false) == RefreshDecision.Rebuild, "last change arriving after release is retained");
            state.CompleteFlush();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
                if (state.HasPending || state.CanFlush) throw new Exception("idle unexpectedly armed");
            watch.Stop();
            Console.WriteLine("PASS: " + checks + " production refresh-state checks; idle 1M checks=" + watch.Elapsed.TotalMilliseconds.ToString("F2") + "ms (fixture, not game timing).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Sample(RefreshState state, bool held) =>
        state.ObservePlacementButton(state.PendingInputTicket, held);
}
