using System;

namespace BugfixesAndQoL.GateRouting
{
    internal static class IntegrityErrorFlushTests
    {
        internal static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, reason) => { checks++; if (!ok) throw new Exception(reason); };
            var gate = new IntegrityErrorFlushGate();
            check(!gate.HasChanges(0, 0, 0), "No clock or snapshot is needed before any error");
            check(gate.HasChanges(1, 0, 0), "An own error schedules support aggregation");
            check(gate.TryReserve(1, 0, 0, 100, 1000, false), "First error can be reported immediately");
            check(!gate.HasChanges(1, 0, 0), "Repeated renders without new failures do no aggregation");
            check(gate.HasChanges(1, 1, 0), "A different producer remains visible after the prior reservation");
            check(!gate.TryReserve(1, 1, 0, 101, 1000, false), "New errors retain the existing throttle");
            check(gate.HasChanges(1, 1, 0), "Throttling must not acknowledge pending errors");
            check(gate.TryReserve(1, 1, 0, 1100, 1000, false), "The pending change flushes when due");
            check(gate.HasChanges(1, 1, 1), "An error arriving during snapshot collection stays pending");
            check(gate.TryReserve(1, 1, 1, 1101, 1000, true), "Map end forces complete totals before the interval");
            check(gate.TryReserve(1, 1, 1, 1102, 1000, true), "Final reporting is independent of a new revision");
            gate.Reset(0, 0, 0);
            check(!gate.HasChanges(0, 0, 0), "A new session discards the previous schedule");
            check(gate.TryReserve(0, 0, 1, 1, 1000, false), "The new session has no inherited throttle deadline");
            return checks;
        }
    }
}
