using BugfixesAndQoL.PitchStairs;
using System;
using System.Threading;

// Production snapshot controller: models the external pre-load/settings/end event ordering.
// Native generator execution is covered separately by StubTests; no game or UI is loaded here.
internal static class ActivationTests
{
    internal static void Run()
    {
        int checks = 0;
        foreach (bool ready in new[] { false, true })
        foreach (bool mod in new[] { false, true })
        foreach (bool feature in new[] { false, true })
        {
            object engineGate = new object();
            bool native = false;
            int writes = 0;
            var activation = new PitchStairsActivation(engineGate, value =>
            {
                Require(Monitor.IsEntered(engineGate), "native publication must hold Vanilla's engine gate");
                native = value; writes++;
            });
            // First BeforeLoad sees settings already restored from a save or flushed from the host.
            Require(activation.BeginLoad(1, ready, mod, feature), "first load publishes");
            Require(native == (ready && mod && feature) && writes == 1, "effective host setting snapshot");
            // Later initialization/Started/preset changes cannot overwrite the first snapshot.
            Require(!activation.BeginLoad(1, true, !mod, !feature), "nested load does not resnapshot");
            Require(writes == 1 && native == (ready && mod && feature), "nested path retains actual world rule");
            Require(!activation.End(0), "unrelated old session end ignored");
            Require(activation.End(1), "matching end unlocks UI");
            Require(!activation.End(1) && writes == 1, "duplicate end is inert, native world retained");
            // Restart/new map/editor create/editor load/failed replacement each get a fresh ID.
            for (long session = 2; session <= 6; session++)
            {
                bool desired = session % 2 == 0;
                Require(activation.BeginLoad(session, true, true, desired), "replacement selects next rule");
                Require(native == desired && writes == session, "next load uses current host settings");
                Require(!activation.End(session - 1), "late replaced end cannot unlock next mission");
                Require(!activation.BeginLoad(session - 1, false, false, false), "stale load cannot change world");
                Require(activation.End(session), "failed or successful attempt retires once");
                Require(native == desired && writes == session, "retirement leaves loaded heights and rule paired");
            }
            checks += 37;
        }
        RequireThrows(() => new PitchStairsActivation(null, _ => { }));
        RequireThrows(() => new PitchStairsActivation(new object(), null));
        Console.WriteLine("PASS: production pitch-stairs activation snapshots; engine lock, host/master gates, nested loads, replacements and retirement (" + checks + " checks).");
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Pitch stairs activation: " + message); }
    private static void RequireThrows(Action action)
    {
        try { action(); }
        catch (ArgumentNullException) { return; }
        throw new InvalidOperationException("Missing activation prerequisite accepted.");
    }
}
