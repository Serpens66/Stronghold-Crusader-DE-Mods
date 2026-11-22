using System;
using AIMarketOverbuyDiagnostic;

internal static class StaleEquipmentTargetTests
{
    private static void Check(bool value, string detail)
    {
        if (!value) throw new Exception("Stale equipment target contract failed: " + detail);
    }

    private static StaleEquipmentTarget NewTarget()
    {
        Check(StaleEquipmentTarget.TryCreate(0, 0, 0, 5, 32, 5, 0, out var state), "valid native source accepted");
        return state;
    }

    internal static void Run()
    {
        var state = NewTarget();
        Check(!state.TryReduce(5, 0, 32, 5, 0, out int adjusted) && adjusted == 5 && state.Active,
            "empty stock preserves normal purchase");
        Check(state.TryReduce(5, 8, 32, 5, 0, out adjusted) && adjusted == 0 && !state.Active,
            "stock above original target cancels stale request");

        state = NewTarget();
        Check(state.TryReduce(5, 2, 32, 5, 0, out adjusted) && adjusted == 3, "partial stock reduces request");
        state.ObservePending(3);
        Check(state.Active && state.OriginalTarget == 5 && state.ExpectedPending == 3, "owned reduction retains original target");
        Check(state.TryReduce(3, 4, 32, 5, 0, out adjusted) && adjusted == 1, "further production reduces against original target");
        Check(!state.TryReduce(1, 1, 32, 5, 0, out adjusted) && adjusted == 1 && state.Active && state.ExpectedPending == 1,
            "later consumption never increases pending");

        AssertInvalid(3, 0, 32, 5, 0, "foreign smaller pending");
        AssertInvalid(6, 0, 32, 5, 0, "foreign larger pending");
        AssertInvalid(0, 0, 32, 5, 0, "pending clear");
        AssertInvalid(-1, 0, 32, 5, 0, "negative pending");
        AssertInvalid(5, -1, 32, 5, 0, "negative stock");
        AssertInvalid(5, 0, 31, 5, 0, "different AIC identity");
        AssertInvalid(5, 0, 32, 8, 0, "changed purchase batch");
        AssertInvalid(5, 0, 32, 5, 1, "last stand");

        state = NewTarget();
        state.ObservePending(0);
        state.ObservePending(5);
        Check(!state.TryReduce(5, 8, 32, 5, 0, out adjusted) && adjusted == 5 && !state.Active,
            "clear then same-value renewal cannot inherit old ownership");
        state = NewTarget();
        state.ObservePending(4);
        state.ObservePending(5);
        Check(!state.Active, "foreign change then restoration cannot rearm ownership");
        state = default;
        Check(!state.TryReduce(5, 8, 32, 5, 0, out adjusted) && adjusted == 5, "session reset disables correction");

        Check(!StaleEquipmentTarget.TryCreate(1, 0, 0, 5, 32, 5, 0, out _), "nonempty initial stock rejected");
        Check(!StaleEquipmentTarget.TryCreate(0, 1, 0, 5, 32, 5, 0, out _), "stock gained inside source rejected");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, 5, 5, 32, 5, 0, out _), "unchanged pending is not a new source");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, -1, 5, 32, 5, 0, out _), "negative prior pending rejected");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, 0, -1, 32, -1, 0, out _), "negative request rejected");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, 0, 0, 32, 0, 0, out _), "zero request rejected");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, 0, 5, 0, 5, 0, out _), "invalid identity rejected");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, 0, 5, 32, 4, 0, out _), "request must match native batch");
        Check(!StaleEquipmentTarget.TryCreate(0, 0, 0, 5, 32, 5, -1, out _), "nonzero last stand rejected");
        Check(StaleEquipmentTarget.TryCreate(0, 0, 8, 5, 32, 5, 0, out state) && state.Active,
            "confirmed producer may replace an older larger request");
        Check(StaleEquipmentTarget.TryCreate(0, 0, 0, int.MaxValue, 1, int.MaxValue, 0, out state) &&
            state.TryReduce(int.MaxValue, int.MaxValue, 1, int.MaxValue, 0, out adjusted) && adjusted == 0,
            "maximum signed values avoid overflow");
        Console.WriteLine("PASS: production stale equipment target ownership, monotone reduction, invalidation and reset.");
    }

    private static void AssertInvalid(int pending, int stock, int aic, int batch, int lastStand, string detail)
    {
        var state = NewTarget();
        Check(!state.TryReduce(pending, stock, aic, batch, lastStand, out int adjusted) && adjusted == pending && !state.Active, detail);
    }
}
