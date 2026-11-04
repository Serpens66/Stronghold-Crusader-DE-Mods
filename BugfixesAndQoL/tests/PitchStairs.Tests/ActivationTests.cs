using BugfixesAndQoL.PitchStairs;
using System;
using System.Threading;

// Production controller; only native publication and loader/tick boundaries are doubles.
internal static class ActivationTests
{
    private static int checks;
    internal static void Run()
    {
        checks = 0;
        foreach (bool ready in new[] { false, true })
        foreach (bool mod in new[] { false, true })
        foreach (bool feature in new[] { false, true })
        foreach (bool outside in new[] { false, true })
        {
            var f = new Fixture();
            bool desired = ready && mod && feature;
            if (outside)
            {
                Require(f.Activation.BeginLoad(1, ready, mod, feature), "outside phase queues");
                Require(f.Writes == 0, "outside phase leaves old world intact");
            }
            int result = f.Activation.RunLoad(() =>
            {
                if (!outside)
                {
                    Require(f.Writes == 0, "inside hook has no premature snapshot");
                    Require(f.Activation.BeginLoad(1, ready, mod, feature), "inside phase publishes");
                }
                Require(f.Native == desired && f.Writes == 1, "original loader sees finalized gates in either hook order");
                Require(!f.Activation.BeginLoad(1, !ready, !mod, !feature), "nested phase cannot resnapshot");
                f.Activation.RunLoad(() => { Require(f.Writes == 1, "nested wrapper cannot republish"); return 0; });
                return 42;
            });
            Require(result == 42, "return value retained");
            Require(!f.Activation.End(0), "stale end ignored");
            Require(f.Activation.End(1), "matching end retires once");
            Require(!f.Activation.End(1) && f.Writes == 1, "end never changes native state");
            // Save/editor/restart/failed replacements differ only in their fresh lifecycle ID here.
            for (long session = 2; session <= 6; session++)
            {
                bool next = session % 2 == 0;
                int before = f.Writes;
                f.Activation.BeginLoad(session, true, true, next);
                Require(f.Writes == before, "next snapshot preserves old world's rule");
                Require(!f.Activation.End(session - 1), "old end cannot cancel next attempt");
                Require(!f.Activation.BeginLoad(session - 1, false, false, false), "stale phase ignored");
                f.Activation.RunLoad(() => { Require(f.Native == next && f.Writes == before + 1, "next load uses current settings"); return 0; });
                f.Activation.End(session);
                Require(f.Native == next && f.Writes == before + 1, "retirement preserves world and rule pairing");
            }
        }
        ThrowsAndCancellation();
        WaitingTicks();
        RequireThrows<ArgumentNullException>(() => new PitchStairsActivation(null, _ => { }));
        RequireThrows<ArgumentNullException>(() => new PitchStairsActivation(new object(), null));
        RequireThrows<ArgumentNullException>(() => new Fixture().Activation.RunLoad<int>(null));
        Console.WriteLine("PASS: production pitch-stairs activation: both hook orders, continuous engine lock, queued old tick, nested loads, throw/cancel and host/master gates (" + checks + " checks).");
    }

    private static void ThrowsAndCancellation()
    {
        var f = new Fixture();
        f.Activation.BeginLoad(1, true, true, true);
        RequireThrows<InvalidOperationException>(() => f.Activation.RunLoad<int>(() => throw new InvalidOperationException("native boundary")));
        Require(f.Native && f.Writes == 1, "throw preserves rule of the world the loader began creating");
        f.Activation.End(1);
        f.Activation.BeginLoad(2, true, true, false);
        Require(f.Native && f.Writes == 1, "finally resets depth; next outside phase only queues");
        f.Activation.End(2);
        f.Activation.RunLoad(() => 0);
        Require(f.Native && f.Writes == 1, "cancelled queued attempt cannot publish in unrelated wrapper");
        f.Activation.BeginLoad(3, true, true, false);
        f.Activation.RunLoad(() => { Require(!f.Native && f.Writes == 2, "valid replacement publishes after throw"); return 0; });
    }

    private static void WaitingTicks()
    {
        var f = new Fixture();
        f.Activation.BeginLoad(1, true, true, true);
        f.Activation.RunLoad(() => 0);
        using (var queued = new ManualResetEventSlim())
        using (var finished = new ManualResetEventSlim())
        {
            bool observed = false;
            var tick = new Thread(() => { queued.Set(); lock (f.Gate) observed = f.Native; finished.Set(); });
            lock (f.Gate)
            {
                tick.Start();
                Require(queued.Wait(5000), "old tick reached lock boundary");
                f.Activation.BeginLoad(2, true, true, false);
                Require(f.Native && f.Writes == 1, "new snapshot cannot alter old world before wrapper scope");
            }
            Require(finished.Wait(5000) && tick.Join(5000), "previously requested tick completes");
            Require(observed, "old tick really observed old rule after BeforeLoad queued");
        }
        using (var queued = new ManualResetEventSlim())
        using (var finished = new ManualResetEventSlim())
        {
            bool observed = true;
            var tick = new Thread(() => { queued.Set(); lock (f.Gate) observed = f.Native; finished.Set(); });
            f.Activation.RunLoad(() =>
            {
                Require(!f.Native && f.Writes == 2, "new rule selected inside load scope");
                tick.Start();
                Require(queued.Wait(5000), "next tick waits during original loader");
                Require(!finished.IsSet, "no unlocked gap between publication and original native load");
                return 0;
            });
            Require(finished.Wait(5000) && tick.Join(5000), "scope releases gate on return");
            Require(!observed, "next tick observes loaded world's new rule");
        }
        // A thrown loader must release the monitor as well as its reentrant depth.
        RequireThrows<InvalidOperationException>(() => f.Activation.RunLoad<int>(() => throw new InvalidOperationException()));
        bool entered = false;
        var afterThrow = new Thread(() => { lock (f.Gate) entered = true; });
        afterThrow.Start();
        Require(afterThrow.Join(5000) && entered, "finally releases monitor after throw");
    }

    private sealed class Fixture
    {
        internal readonly object Gate = new object();
        internal readonly PitchStairsActivation Activation;
        internal bool Native;
        internal int Writes;
        internal Fixture()
        {
            Activation = new PitchStairsActivation(Gate, value =>
            {
                Require(Monitor.IsEntered(Gate), "publication holds Vanilla's engine gate");
                Native = value; Writes++;
            });
        }
    }
    private static void Require(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException("Pitch stairs activation: " + message); }
    private static void RequireThrows<T>(Action action) where T : Exception
    {
        checks++;
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected boundary rejection: " + typeof(T).Name);
    }
}
