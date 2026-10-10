using APIShared.Networking;

// Historical Git sources are test fixtures only, never runtime fallback code.
internal static class Program
{
    private sealed class Probe { }
    private sealed class Outcome
    {
        internal bool Accepted;
        internal byte[] Body;
        internal int Serialized, Sent, ManagerReads;
        internal Probe SentObject;
        internal short SentId;
    }
    private delegate bool OldSender(Probe packet, short id, bool registered,
        Func<Probe, byte[]> serialize, Func<ulong> manager, Action<Probe, short> send,
        out byte[] body, out string reason);

    private static int Main()
    {
        OldSender[] oldSenders = {
            BugfixesAndQoL.BugfixesAndQoLChoreSender.TrySend,
            ExtraFeatures.ExtraFeaturesChoreSender.TrySend,
            RandomEvents.RandomEventsChoreSender.TrySend,
            ExtremePowers.API.ExtremePowerChoreSender.TrySend
        };
        int cases = 0;
        foreach (OldSender old in oldSenders)
        foreach (int size in new[] { 0, 1197, 1198, 1199, 1300 })
        foreach (int failure in Enumerable.Range(0, 7))
        {
            Probe packet = failure == 2 ? null : new Probe();
            Outcome baseline = Run(old, packet, size, failure);
            Outcome current = Run(null, packet, size, failure);
            Require(baseline.Accepted == current.Accepted, "acceptance differs");
            Require(baseline.Serialized == current.Serialized, "serializer invocation differs");
            Require(baseline.Sent == current.Sent, "transport invocation differs");
            Require(baseline.SentObject == current.SentObject && baseline.SentId == current.SentId,
                "packet identity or ID differs");
            if (baseline.Accepted)
            {
                Require(baseline.Body.SequenceEqual(current.Body), "successful body bytes differ");
                Require(baseline.ManagerReads == current.ManagerReads, "successful manager preflight differs");
            }
            cases++;
        }
        Console.WriteLine("PASS: " + cases + " old-Git/new-policy scenarios across all four former helpers; accepted bytes, original packet/ID, serializer/send counts and rejection outcomes match.");
        Console.WriteLine("Expected diagnostic-only difference: oversized payloads are rejected before reading the manager; status/reason text is structured, and failed preparation need not retain body bytes.");
        return 0;
    }

    private static Outcome Run(OldSender old, Probe packet, int size, int failure)
    {
        var result = new Outcome();
        Func<Probe, byte[]> serialize = _ => {
            result.Serialized++;
            if (failure == 3) throw new InvalidOperationException("serializer");
            if (failure == 4) return null;
            return Enumerable.Range(0, size).Select(value => (byte)(value % 251)).ToArray();
        };
        Func<ulong> manager = () => {
            result.ManagerReads++;
            if (failure == 5) throw new InvalidOperationException("manager");
            return failure == 6 ? 0UL : 123UL;
        };
        Action<Probe, short> send = (value, id) => {
            result.Sent++; result.SentObject = value; result.SentId = id;
            if (failure == 1) throw new InvalidOperationException("transport");
        };
        bool registered = failure != 0 || size != 0;
        if (old != null)
            result.Accepted = old(packet, 37, registered, serialize, manager, send, out result.Body, out _);
        else
        {
            NetworkSendResult submission = ChoreSendPolicy.Send(packet, 37, registered, serialize, manager, send, 1200);
            result.Accepted = submission.Submitted;
            result.Body = submission.CopySerializedBody();
        }
        return result;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
