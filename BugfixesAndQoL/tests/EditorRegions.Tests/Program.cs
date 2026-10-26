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
            for (int i = 0; i < 100; i++) state.Changed(7);
            Check(state.BeginFlush(7, true) == RefreshDecision.Rebuild, "many brush changes coalesce");
            Check(state.BeginFlush(7, true) == RefreshDecision.None, "no recursive rebuild");
            state.CompleteFlush();
            Check(!state.HasPending && state.BeginFlush(8, false) == RefreshDecision.None, "one completed pass consumes the batch");
            state.Changed(8); state.Changed(9);
            Check(state.BeginFlush(9, false) == RefreshDecision.AlreadyRebuilt, "retain first generation across a pending batch");
            Check(!state.HasPending, "Vanilla completed consumes pending work");
            state.Changed(9);
            Check(state.BeginFlush(10, true) == RefreshDecision.Rebuild, "new dirty edits survive an intervening rebuild");
            state.Changed(10);
            Check(state.BeginFlush(10, true) == RefreshDecision.None, "nested edit cannot recurse");
            state.CompleteFlush();
            Check(state.HasPending && state.BeginFlush(11, true) == RefreshDecision.Rebuild, "nested edit survives completion");
            state.CompleteFlush();
            state.Changed(11);
            Check(state.BeginFlush(11, false) == RefreshDecision.Rebuild, "clean marker alone is not proof of a rebuild");
            state.CompleteFlush();
            state.Changed(uint.MaxValue);
            Check(state.BeginFlush(0, false) == RefreshDecision.AlreadyRebuilt, "generation wrap is accepted by inequality");
            state.Changed(1); state.Start(2, true, true);
            Check(state.HasPending, "ready-session replay preserves pending work");
            state.End(1);
            Check(state.HasPending, "an old end cannot clear a newer session");
            state.End(2);
            Check(!state.Active && !state.HasPending && !state.Changed(1), "end retires pending work");
            state.Start(3, true, true); state.Changed(1); state.SetEnabled(false);
            Check(!state.Active && !state.HasPending, "master disable cancels work");
            state.SetEnabled(true);
            Check(!state.HasPending, "reenabling does not invent an edit");
            state.Changed(1); state.Start(4, false, true);
            Check(!state.Active && !state.HasPending, "editor to gameplay transition cancels work");
            state.Start(5, true, true);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
                if (state.HasPending) throw new Exception("idle unexpectedly armed");
            watch.Stop();
            Console.WriteLine("PASS: " + checks + " production refresh-state checks; idle 1M checks=" + watch.Elapsed.TotalMilliseconds.ToString("F2") + "ms (fixture, not game timing).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
