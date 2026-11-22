using System;
using System.Threading;
using AIMarketOverbuyDiagnostic;

// Exercise the production lifecycle state: a Main-thread transition while a
// simulation original is paused cannot publish/reset its enclosing frame stack.
internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Trade lifecycle contract failed."); }
    private static void Main()
    {
        var state = new TradeSessionState();
        state.RequestStart(10, true);
        Check(state.TakePending(out long id, out bool enabled) && id == 10 && enabled);
        state.Enter(); state.Enter();
        state.RequestEnd();
        Check(!state.TakePending(out _, out _) && state.Depth == 2);
        state.Leave();
        Check(!state.TakePending(out _, out _) && state.Depth == 1);
        state.RequestStart(20, true);
        state.Leave();
        Check(state.TakePending(out id, out enabled) && id == 20 && enabled && state.Depth == 0);
        Check(!state.TakePending(out _, out _));
        state.RequestStart(30, true); state.RequestEnd();
        Check(state.TakePending(out _, out enabled) && !enabled);

        object gate = new object();
        using var entered = new ManualResetEventSlim();
        using var originalMayReturn = new ManualResetEventSlim();
        Exception failure = null;
        var producer = new Thread(() =>
        {
            try
            {
                lock (gate) state.Enter();
                entered.Set();
                // Represents original(), outside the shared gate.
                Check(originalMayReturn.Wait(TimeSpan.FromSeconds(5)));
                lock (gate)
                {
                    Check(state.Depth == 1);
                    state.Leave();
                    Check(state.TakePending(out long next, out bool capture) && next == 40 && capture);
                }
            }
            catch (Exception error) { failure = error; }
        });
        producer.Start();
        Check(entered.Wait(TimeSpan.FromSeconds(5)));
        bool acquired = Monitor.TryEnter(gate, TimeSpan.FromSeconds(1));
        Check(acquired);
        try
        {
            state.RequestEnd(); state.RequestStart(40, true);
            Check(!state.TakePending(out _, out _) && state.Depth == 1);
        }
        finally { Monitor.Exit(gate); originalMayReturn.Set(); }
        Check(producer.Join(TimeSpan.FromSeconds(5)));
        if (failure != null) throw failure;
        Check(state.Depth == 0);
        MarketDemandTargetTests.Run();
        MarketDemandPersistenceTests.Run();
        Console.WriteLine("PASS: nested, deferred, last-wins and concurrent original lifecycle.");
    }
}
