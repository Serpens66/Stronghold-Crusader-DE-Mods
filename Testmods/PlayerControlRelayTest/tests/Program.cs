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
    private static RelayCommand Command(long id = 1, int[] slots = null, string epoch = "world-a", int x = 12, uint maxGlobalId = 500)
        => new RelayCommand(epoch, 2, 123, id, RelayAction.Move, x, 20, maxGlobalId, slots ?? new[] { 1, 2 });
    private static bool Same(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }
    private static void Main()
    {
        ImmutableRequests(); SlotEncoding(); ReplayProtection(); OrderPlans(); WorldPreparation();
        Console.WriteLine("PASS: " + assertions + " core assertions; no game, transport or native execution is simulated.");
    }
    private static void ImmutableRequests()
    {
        var slots = new[] { 1, 2, 3 };
        var command = Command(slots: slots); byte[] digest = command.ComputeDigest();
        slots[0] = 9; digest[0] ^= 255;
        Check(command.Slots[0] == 1, "Mutable request escaped.");
        Check(!Same(digest, command.ComputeDigest()), "Mutable digest escaped.");
        Check(Command().ComputeDigest().Length == 32, "SHA-256 digest missing.");
        Check(!Same(Command().ComputeDigest(), Command(x: 13).ComputeDigest()), "Target is not digest-bound.");
        Check(!Same(Command().ComputeDigest(), Command(slots: new[] { 1, 3 }).ComputeDigest()), "Units are not digest-bound.");
        Check(!Same(Command().ComputeDigest(), Command(maxGlobalId: 501).ComputeDigest()), "Identity limit is not digest-bound.");
        Reject(() => Command(slots: Array.Empty<int>()));
        Reject(() => Command(slots: new[] { 2, 1 }));
        Reject(() => Command(slots: new[] { 1, 1 }));
        Reject(() => Command(slots: new[] { 0 }));
        Reject(() => Command(slots: new[] { RelaySlotCodec.MaximumSlot + 1 }));
        Reject(() => Command(maxGlobalId: 0));
        Reject(() => Command(epoch: ""));
        Check(RelayValidation.TryValidate(Command(), 100, 100, out _), "Valid target rejected.");
        Check(!RelayValidation.TryValidate(Command(x: 100), 100, 100, out _), "Outside target admitted.");
        Check(!RelayValidation.TryValidate(Command(x: -1), 100, 100, out _), "Negative target admitted.");
        Check(!RelayValidation.TryValidate(Command(), 0, 100, out _), "Unknown map admitted.");
    }
    private static void SlotEncoding()
    {
        var mixed = new[] { 1, 2, 127, 128, 500, 9999 };
        Check(string.Join(",", RelaySlotCodec.Decode(RelaySlotCodec.Encode(mixed))) == string.Join(",", mixed), "Slots did not round trip.");
        Reject(() => RelaySlotCodec.Decode(new byte[0]));
        Reject(() => RelaySlotCodec.Decode(new byte[] { 0 }));              // A zero delta repeats a slot.
        Reject(() => RelaySlotCodec.Decode(new byte[] { 0x80 }));           // Truncated value.
        Reject(() => RelaySlotCodec.Decode(new byte[] { 0x80, 0x80, 0x80, 1 })); // Overlong value.
        Reject(() => RelaySlotCodec.Decode(new byte[] { 0xFF, 0xFF, 1 }));  // Beyond the native range.

        var all = new int[RelaySlotCodec.MaximumSlot];
        for (int i = 0; i < all.Length; i++) all[i] = i + 1;
        var chunks = RelaySlotCodec.Split(all);
        int total = 0, previous = 0;
        foreach (int[] chunk in chunks)
        {
            Check(RelaySlotCodec.Encode(chunk).Length <= RelaySlotCodec.MaximumEncodedBytes, "Chunk exceeds its Chore limit.");
            foreach (int slot in chunk) { Check(slot > previous, "Chunks lost the slot order."); previous = slot; }
            total += chunk.Length;
        }
        Check(total == all.Length, "Splitting lost units.");
        // Worst spacing still keeps a whole chunk well above any usual selection.
        var sparse = new List<int>();
        for (int slot = 200; slot <= RelaySlotCodec.MaximumSlot; slot += 200) sparse.Add(slot);
        Check(RelaySlotCodec.Split(sparse).Count == 1, "A sparse selection was split unnecessarily.");
        Check(RelaySlotCodec.Split(new[] { 7 }).Count == 1 && RelaySlotCodec.Split(new int[0]).Count == 0, "Small selections were split.");
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
        var submitted = new RelayLedger(1); submitted.Clear("world-a");
        Check(submitted.Reserve(command, out _, out _) && submitted.Record(command, RelayOutcome.Submitted, "queued"), "Submission precondition failed.");
        Check(submitted.Record(command, RelayOutcome.Rejected, "no native call") && submitted.TryGet(command, out state, out _) && state == RelayOutcome.Rejected, "Execution-time rejection did not replace Submitted.");
        Check(!ledger.Reserve(Command(3), out _, out _), "Full ledger silently evicted replay protection.");
        Check(ledger.Reserve(command, out duplicate, out _) && duplicate, "Full ledger cannot recognize duplicate.");
        Check(ledger.Clear("world-b") && ledger.Count == 0, "New epoch did not retire entries.");
        Check(!ledger.Reserve(command, out _, out _), "Stale epoch revived.");
        Check(ledger.Reserve(Command(epoch: "world-b"), out duplicate, out _) && !duplicate, "New-world request rejected.");
    }
    private static void OrderPlans()
    {
        var tribes = new Dictionary<int, int> { [1] = 7, [2] = 7, [3] = 8, [4] = 0 };
        var stances = new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 2, [4] = 0 };
        var members = new Dictionary<int, int> { [7] = 2, [8] = 5 };
        RelayUnitResolver unit = (int slot, int owner, uint limit, out int tribe, out int stance) => {
            tribe = 0; stance = 0;
            if (!tribes.TryGetValue(slot, out tribe)) return false;
            stance = stances[slot]; return true;
        };
        RelayTribeMemberCount count = id => members.TryGetValue(id, out int value) ? value : -1;
        bool Plan(int[] slots, out RelayOrderPlan plan) => RelayValidation.TryPlan(Command(slots: slots), 100, 100, unit, count, out plan, out _);

        // Exactly one whole group keeps it, so repeated orders do not create new groups.
        Check(Plan(new[] { 1, 2 }, out var result) && result.ExistingTribe == 7 && result.Slots.Count == 2, "Whole group was regrouped.");
        // Everything else regroups exactly what was selected, including a single unit.
        Check(Plan(new[] { 1 }, out result) && result.ExistingTribe == 0 && result.Slots.Count == 1 && result.SourceTribes[0] == 7, "Single unit did not regroup.");
        Check(Plan(new[] { 3 }, out result) && result.ExistingTribe == 0, "Part of a larger group kept that group.");
        Check(Plan(new[] { 1, 2, 3 }, out result) && result.ExistingTribe == 0 && result.Slots.Count == 3, "Units from two groups kept a group.");
        Check(Plan(new[] { 4 }, out result) && result.ExistingTribe == 0 && result.SourceTribes[0] == 0, "Unit without a group was not planned.");
        // Units that cannot be ordered drop out; a remaining whole group still counts as whole.
        Check(Plan(new[] { 1, 2, 9 }, out result) && result.ExistingTribe == 7 && result.Slots.Count == 2 && result.Skipped == 1, "Missing unit changed the others.");
        Check(!Plan(new[] { 9 }, out result) && result == null, "Order without any unit admitted.");
        Check(!RelayValidation.TryPlan(Command(x: 100), 100, 100, unit, count, out _, out _), "Outside target planned.");
        Check(!RelayValidation.TryPlan(Command(), 100, 100, null, count, out _, out _), "Missing resolver admitted.");
        // Tie order matches Vanilla's selection: stance 2, then 1, then 0.
        Check(RelayValidation.MajorityStance(new[] { 1, 1, 1 }) == 2, "Three-way tie.");
        Check(RelayValidation.MajorityStance(new[] { 2, 2, 0 }) == 1, "Tie between 0 and 1.");
        Check(RelayValidation.MajorityStance(new[] { 3, 1, 1 }) == 0, "Clear majority.");
        Check(Plan(new[] { 1, 2, 3 }, out result) && result.Stance == 1, "Plan ignored the majority stance.");
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
