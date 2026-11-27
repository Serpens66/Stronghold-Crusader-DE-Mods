using System;
using BugfixesAndQoL.TrappedUnitRescue;

internal static class RescueNativeIntegrityFixture
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message)
        { checks++; if (!value) throw new InvalidOperationException(message); }
        bool relocation = true, worker = true;
        var guard = new RescueNativeIntegrity();
        Check(guard.Check(() => relocation, () => worker), "Both required contracts are healthy.");
        // Probe truth remains authoritative after another caller has already logged
        // a loss; the guard has no diagnostic counter input to consume.
        relocation = false;
        Check(!guard.Check(() => relocation, () => worker), "An already reported relocation loss is rejected.");
        relocation = true;
        Check(!guard.Check(() => relocation, () => worker), "Restoring bytes cannot restore call authority.");
        guard = new RescueNativeIntegrity();
        worker = false;
        Check(!guard.Check(() => relocation, () => worker), "The independent worker guard is required.");
        worker = true;
        Check(!guard.Check(() => relocation, () => worker), "Worker restoration also remains unavailable.");
        guard = new RescueNativeIntegrity();
        Check(guard.Check(() => true, null), "An absent Fixes worker hook requires no fabricated worker probe.");
        Check(!guard.Check(() => throw new InvalidOperationException("unreadable"), null), "An unreadable contract fails closed.");
        Check(!guard.Check(() => true, null), "A read failure is permanent.");
        guard = new RescueNativeIntegrity();
        Check(!guard.Check(null, () => true), "A missing shared relocation probe is unavailable.");
        Console.WriteLine("PASS: production rescue dependency integrity " + checks + " checks; scoped probes, prior reports, exceptions and permanent loss.");
    }
}
