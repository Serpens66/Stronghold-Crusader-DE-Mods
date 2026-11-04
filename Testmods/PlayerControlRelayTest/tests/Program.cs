using System;
using System.Collections.Generic;
using PlayerControlRelayTest;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        assertions++;
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected malformed request rejection.");
    }
    private static RelayCommand Command(long id = 1, RelayUnit[] units = null, string epoch = "world-a", int x = 12)
        => new RelayCommand(epoch, 2, 123, id, RelayAction.Move, x, 20,
            units ?? new[] { new RelayUnit(1, 101, 2), new RelayUnit(2, 102, 2) });
    private static bool Same(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }
    private static void Main()
    {
        ImmutableRequests(); ReplayProtection(); ExactTribes(); PendingRequestRaces(); WorldPreparation();
        Console.WriteLine("PASS: " + assertions + " core assertions; no game, transport or native execution is simulated.");
    }
    private static void ImmutableRequests()
    {
        var units = new RelayUnit[64];
        for (int i = 0; i < units.Length; i++) units[i] = new RelayUnit(i + 1, (uint)(100 + i), 2);
        var command = Command(units: units); byte[] digest = command.ComputeDigest();
        units[0] = default; command.CopyUnits()[1] = default; digest[0] ^= 255;
        Check(command.Units[0].GlobalId == 100 && command.Units[1].GlobalId == 101, "Mutable request escaped.");
        Check(!Same(digest, command.ComputeDigest()), "Mutable digest escaped.");
        Check(Command().ComputeDigest().Length == 32, "SHA-256 digest missing.");
        Check(Same(Command().ComputeDigest(), Command(units: new[] { new RelayUnit(2, 102, 2), new RelayUnit(1, 101, 2) }).ComputeDigest()), "Selection order altered canonical digest.");
        Check(!Same(Command().ComputeDigest(), Command(x: 13).ComputeDigest()), "Target is not digest-bound.");
        Reject(() => Command(units: new RelayUnit[65]));
        Reject(() => Command(units: Array.Empty<RelayUnit>()));
        Reject(() => Command(units: new[] { new RelayUnit(1, 101, 2), new RelayUnit(1, 102, 2) }));
        Reject(() => Command(units: new[] { new RelayUnit(1, 101, 3) }));
        Reject(() => Command(units: new[] { new RelayUnit(0, 101, 2) }));
        Reject(() => Command(units: new[] { new RelayUnit(1, 0, 2) }));
        Reject(() => Command(epoch: ""));
        Check(RelayValidation.TryValidate(Command(), 100, 100, out _), "Valid target rejected.");
        Check(!RelayValidation.TryValidate(Command(x: 100), 100, 100, out _), "Outside target admitted.");
        Check(!RelayValidation.TryValidate(Command(x: -1), 100, 100, out _), "Negative target admitted.");
        Check(!RelayValidation.TryValidate(Command(), 0, 100, out _), "Unknown map admitted.");
    }
    private static void ReplayProtection()
    {
        var ledger = new RelayLedger(2); var command = Command();
        Check(!ledger.Reserve(command, out _, out _), "Unbound ledger admitted request.");
        Check(ledger.Clear("world-a"), "Initial epoch rejected.");
        Check(ledger.Reserve(command, out bool duplicate, out _) && !duplicate, "New request rejected.");
        Check(ledger.Record(command, RelayOutcome.Submitted, "queued"), "Submission not recorded.");
        Check(ledger.Reserve(command, out duplicate, out _) && duplicate, "Duplicate lost tombstone.");
        Check(ledger.TryGet(command, out var state, out var reason) && state == RelayOutcome.Submitted && reason == "queued", "Duplicate outcome lost.");
        Check(!ledger.Reserve(Command(x: 13), out _, out _), "Request-ID collision admitted.");
        Check(!ledger.Record(command, RelayOutcome.Accepted, "retry"), "Submission regressed to executable admission.");
        Check(ledger.Record(command, RelayOutcome.Uncertain, "unknown"), "Unknown submission cannot be retained.");
        Check(ledger.Record(command, RelayOutcome.Executed, "confirmed"), "Confirmed execution not recorded.");
        Check(!ledger.Record(command, RelayOutcome.Rejected, "late"), "Executed request regressed.");
        Check(!ledger.Clear("world-a") && ledger.Count == 1, "Same epoch cleared tombstones.");
        Check(ledger.Reserve(Command(2), out _, out _) && ledger.Count == 2, "Second request rejected.");
        Check(ledger.Record(Command(2), RelayOutcome.Rejected, "execution ledger full"), "Synchronous terminal rejection was not cached.");
        Check(!ledger.Record(Command(2), RelayOutcome.Submitted, "late send return"), "Late send return downgraded terminal rejection.");
        Check(ledger.TryGet(Command(2), out state, out reason) && state == RelayOutcome.Rejected && reason == "execution ledger full", "Duplicate cache lost its terminal rejection.");
        Check(!ledger.Reserve(Command(3), out _, out _), "Full ledger silently evicted replay protection.");
        Check(ledger.Reserve(command, out duplicate, out _) && duplicate, "Full ledger cannot recognize duplicate.");
        Check(ledger.Clear("world-b") && ledger.Count == 0, "New epoch did not retire entries.");
        Check(!ledger.Reserve(command, out _, out _), "Stale epoch revived.");
        Check(ledger.Reserve(Command(epoch: "world-b"), out duplicate, out _) && !duplicate, "New-world request rejected.");
    }
    private static void ExactTribes()
    {
        var a = new RelayUnit(1, 101, 2); var b = new RelayUnit(2, 102, 2); var c = new RelayUnit(3, 103, 2);
        var live = new Dictionary<int, RelayUnit> { [1] = a, [2] = b, [3] = c };
        var slots = new Dictionary<int, int> { [1] = 7, [2] = 7, [3] = 8 };
        var tribes = new Dictionary<int, RelayTribe> { [7] = new RelayTribe(7, 701, 2, new[] { a, b }), [8] = new RelayTribe(8, 801, 2, new[] { c }) };
        RelayUnitResolver unit = (RelayUnit captured, out RelayUnit current, out int tribeSlot) => {
            tribeSlot = slots[captured.Slot]; return live.TryGetValue(captured.Slot, out current);
        };
        RelayTribeResolver tribe = (int id, out RelayTribe current) => tribes.TryGetValue(id, out current);
        Check(RelayValidation.TryPlan(Command(units: new[] { c, a, b }), 100, 100, unit, tribe, out var plan, out _) && plan.Groups.Count == 2 && plan.Groups[0].Slot == 7, "Exact union rejected or unordered.");
        Check(!RelayValidation.TryPlan(Command(units: new[] { a }), 100, 100, unit, tribe, out _, out _), "Partial selection widened to tribe.");
        live[1] = new RelayUnit(1, 999, 2);
        Check(!RelayValidation.TryPlan(Command(), 100, 100, unit, tribe, out _, out _), "Recycled unit admitted.");
        live[1] = new RelayUnit(1, 101, 3);
        Check(!RelayValidation.TryPlan(Command(), 100, 100, unit, tribe, out _, out _), "Transferred unit admitted.");
        live[1] = a;
        tribes[7] = new RelayTribe(7, 701, 2, new[] { a, b, c });
        Check(!RelayValidation.TryPlan(Command(), 100, 100, unit, tribe, out _, out _), "New unselected member admitted.");
        tribes[7] = new RelayTribe(7, 701, 2, new[] { a, a });
        Check(!RelayValidation.TryPlan(Command(), 100, 100, unit, tribe, out _, out _), "Duplicate tribe member admitted.");
        tribes[7] = new RelayTribe(7, 0, 2, new[] { a, b });
        Check(!RelayValidation.TryPlan(Command(), 100, 100, unit, tribe, out _, out _), "Invalid tribe identity admitted.");
        tribes[7] = new RelayTribe(7, 701, 2, new[] { a });
        Check(!RelayValidation.TryPlan(Command(), 100, 100, unit, tribe, out _, out _), "Selected member missing from tribe admitted.");
    }
    private static void PendingRequestRaces()
    {
        var pending = new RelayPendingRequests(); var first = Command(); var next = Command(2);
        Check(pending.TryAdd(first) && pending.ContainsOwner(2), "Pending owner request was not installed.");
        Check(!pending.TryAdd(next), "Second owner request replaced an uncompleted request.");
        Check(!pending.RemoveIfMatches(Command(x: 13)), "Changed request payload cleared pending request.");
        Check(!pending.RemoveIfMatches(Command(epoch: "world-b")), "Other-world reply cleared pending request.");
        Check(pending.RemoveIfMatches(first) && !pending.ContainsOwner(2), "Matching terminal reply did not clear its request.");
        Check(pending.TryAdd(next), "New request could not follow terminal reply.");
        Check(!pending.RemoveIfMatches(first) && pending.TryGet(2, out var retained) && ReferenceEquals(next, retained), "Stale terminal reply cleared newer request.");
        var independent = new RelayCommand("world-a", 3, 123, 2, RelayAction.Stop, 0, 0,
            new[] { new RelayUnit(9, 109, 3) });
        Check(pending.TryAdd(independent) && pending.Count == 2, "Independent owner request rejected.");
        Check(pending.RemoveIfMatches(next) && pending.ContainsOwner(3), "One owner's result cleared another owner.");
        pending.Clear(); Check(pending.Count == 0, "World retirement left pending requests.");
        for (int owner = 1; owner <= 8; owner++)
            Check(pending.TryAdd(new RelayCommand("world-b", owner, 123, owner, RelayAction.Stop, 0, 0,
                new[] { new RelayUnit(owner, (uint)(100 + owner), owner) })), "Valid bounded owner slot rejected.");
        Check(pending.Count == 8 && !pending.TryAdd(null), "Pending owner budget/invalid input contract failed.");
    }
    private static void WorldPreparation()
    {
        var readiness = new RelayWorldReadiness();
        Check(!readiness.Prepare(null) && !readiness.IsPrepared(null), "Absent world was prepared.");
        Reject(() => readiness.Begin(0));
        var first = readiness.Begin(1);
        Check(first.World == 1 && !readiness.IsPrepared(first), "New world admitted before Unity handshake preparation.");
        Check(readiness.Prepare(first) && readiness.IsPrepared(first), "Current preparation was not published.");
        var next = readiness.Begin(2);
        Check(!readiness.IsPrepared(first) && !readiness.Prepare(first), "Old UI callback prepared a replacement world.");
        Check(!readiness.IsPrepared(next), "Replacement world inherited preparation.");
        Check(readiness.Prepare(next) && readiness.IsPrepared(next), "Replacement world could not prepare.");
        var sameNumber = readiness.Begin(2);
        Check(!ReferenceEquals(next, sameNumber) && !readiness.Prepare(next) && !readiness.IsPrepared(next), "Reused numeric world ID revived an old offer token.");
        Check(!readiness.IsPrepared(sameNumber) && readiness.Prepare(sameNumber), "New token did not require fresh preparation.");
        readiness.Reset();
        Check(!readiness.IsPrepared(sameNumber) && !readiness.Prepare(sameNumber), "Retired world accepted delayed preparation.");
        var foreign = new RelayWorldReadiness().Begin(2);
        var active = readiness.Begin(2);
        Check(!readiness.Prepare(foreign) && !readiness.IsPrepared(active), "Another readiness owner's token was adopted.");
    }
}
