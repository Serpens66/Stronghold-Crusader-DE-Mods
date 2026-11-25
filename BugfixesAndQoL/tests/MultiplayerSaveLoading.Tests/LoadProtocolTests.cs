using System;
using System.Collections.Generic;
using System.Linq;
using BugfixesAndQoL.Multiplayer.SaveLoading;

internal static class LoadProtocolTests
{
    private const ulong Source = 987654321;
    private const ulong Lobby = 123456789;
    private const string Operation = "00112233445566778899aabbccddeeff";
    private static int checks;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }
    private static bool Has(LoadStep step, LoadEffect effect) => (step.Effects & effect) != 0;
    private static ulong[] Peers(int count) => Enumerable.Range(1, count).Select(index => 1000UL + (ulong)index).ToArray();
    private static LoadPacket Packet(LoadMessageKind kind, string operation = Operation, ulong session = Source, ulong lobby = 0) =>
        new LoadPacket { Kind = kind, Session = session, Operation = operation, LobbyId = lobby };
    private static LoadProtocol Node(int index = 0, int count = 3, long timeout = 100) =>
        new LoadProtocol(Peers(count)[index], Peers(count)[0], Peers(count), Source, timeout);
    private static void Throws(Action action, string reason)
    {
        bool threw = false;
        try { action(); } catch (ArgumentException) { threw = true; }
        Check(threw, reason);
    }
    private sealed class Cluster
    {
        internal readonly LoadProtocol[] Nodes;
        internal readonly bool[] Stopped, Released, Joined;
        internal readonly List<Action> Pending = new List<Action>();
        private readonly ulong[] ids;
        private readonly Random random;
        internal long Clock;
        internal int Creates, Menus, Events;
        internal Cluster(int count, int seed)
        {
            ids = Peers(count); random = new Random(seed);
            Nodes = ids.Select(id => new LoadProtocol(id, ids[0], ids, Source, 10000)).ToArray();
            Stopped = new bool[count]; Released = new bool[count]; Joined = new bool[count];
        }
        internal void Begin() => Apply(0, Nodes[0].Begin(Operation, Clock));
        internal void Pump(int limit = 1000)
        {
            while (Pending.Count != 0)
            {
                Check(Events++ < limit, "Transition did not converge with finite reliable delivery.");
                int index = random.Next(Pending.Count);
                Action action = Pending[index]; Pending.RemoveAt(index); Clock++; action();
            }
        }
        internal void Apply(int receiver, LoadStep step)
        {
            Check(step.Accepted, "A valid scheduled transition was rejected: " + step.Rejection);
            if (Has(step, LoadEffect.ReleaseLocal)) Released[receiver] = true;
            if (Has(step, LoadEffect.OpenLoadMenu))
            {
                Check(receiver == 0 && Released.All(value => value) && Joined.All(value => value),
                    "The host opened its normal load requester before every joined peer installed native-load permission.");
                Menus++;
            }
            // This models the documented runtime ordering, not the protocol decisions:
            // client release is applied before its outgoing acknowledgement can arrive.
            foreach (LoadPacket packet in step.Outbound)
            {
                if (packet.Kind == LoadMessageKind.ReleaseAcknowledged)
                    Check(Released[receiver], "Release acknowledgement escaped before local native-load permission.");
                foreach (ulong recipient in Nodes[receiver].Recipients(packet))
                {
                    int target = Array.IndexOf(ids, recipient);
                    var copy = Packet(packet.Kind, packet.Operation, packet.Session, packet.LobbyId);
                    int sender = receiver;
                    Pending.Add(() => Apply(target, Nodes[target].Receive(ids[sender], copy, Clock)));
                }
            }
            if (Has(step, LoadEffect.PrepareLocal)) Pending.Add(() => Apply(receiver, Nodes[receiver].Prepared(Clock)));
            if (Has(step, LoadEffect.StopLocal)) Pending.Add(() => {
                Stopped[receiver] = true; Apply(receiver, Nodes[receiver].Stopped(Clock));
            });
            if (Has(step, LoadEffect.CreateLobby))
            {
                Check(receiver == 0 && Stopped.All(value => value) &&
                    Nodes.All(node => node.Phase == LoadPhase.WaitingLobby),
                    "A private lobby was created before every participant drained every old sender stream.");
                Creates++; Pending.Add(() => Apply(receiver, Nodes[receiver].PublishLobby(Lobby, Clock)));
            }
            if (Has(step, LoadEffect.JoinLocal)) Pending.Add(() => {
                Joined[receiver] = true; Apply(receiver, Nodes[receiver].Joined(Clock));
            });
            Check(!Has(step, LoadEffect.Cancelled), "A valid handoff cancelled under reordered reliable delivery.");
        }
    }
    private static void SuccessfulSchedules()
    {
        for (int count = 1; count <= 8; count++)
            for (int seed = 0; seed < 45; seed++)
            {
                var cluster = new Cluster(count, seed);
                cluster.Begin(); cluster.Pump();
                Check(cluster.Creates == 1 && cluster.Menus == 1 && cluster.Nodes.All(node => node.IsCompleted),
                    "Reliable reordered transition did not transfer exactly once to the ordinary shared lobby.");
                foreach (LoadProtocol node in cluster.Nodes)
                {
                    LoadStep idle = node.Tick(long.MaxValue);
                    Check(idle.Accepted && idle.Effects == LoadEffect.None && idle.Outbound.Count == 0 && node.IsCompleted,
                        "The ordinary lobby's save selection inherited the transition deadline.");
                    LoadStep lateAbort = node.Receive(node.IsHost ? 1002UL : 1001UL, Packet(LoadMessageKind.Abort), long.MaxValue);
                    Check(!lateAbort.Accepted && lateAbort.Effects == LoadEffect.None && node.IsCompleted,
                        "A delayed old Abort cancelled an ordinary lobby or a later separately started save.");
                    Check(!node.Receive(node.IsHost ? 1002UL : 1001UL,
                        Packet(LoadMessageKind.CommittedAbort), long.MaxValue).Accepted && node.IsCompleted,
                        "A delayed committed failure cancelled an ordinary lobby or a later separately started save.");
                    Check(!node.Abort("late-local-failure").Accepted && node.IsCompleted,
                        "A retired local failure destroyed a completed ordinary lobby handoff.");
                }
                if (count > 1)
                {
                    var client = cluster.Nodes[1];
                    LoadStep repeat = client.Receive(Peers(count)[0], Packet(LoadMessageKind.ReleaseLobby), long.MaxValue);
                    Check(repeat.Accepted && repeat.Effects == LoadEffect.None && repeat.Outbound.Count == 1 &&
                        repeat.Outbound[0].Kind == LoadMessageKind.ReleaseAcknowledged,
                        "A duplicate authenticated release lost its bounded acknowledgement receipt.");
                }
            }
    }
    private static void AuthorityAndFraming()
    {
        foreach (int sender in new[] { 0, 1, 2 })
        {
            var client = Node(1);
            LoadStep prepare = client.Receive(Peers(3)[sender], Packet(LoadMessageKind.Prepare), 0);
            Check(prepare.Accepted == (sender == 0), "A non-host or self admitted an idle client.");
        }
        var host = Node();
        Check(!host.Receive(1002, Packet(LoadMessageKind.Prepare), 0).Accepted && host.Phase == LoadPhase.Idle,
            "A peer initiated the current host's load transition.");
        foreach (LoadPacket invalid in new[] {
            null, Packet(LoadMessageKind.Prepare, session: 1), Packet(LoadMessageKind.Prepare, operation: "stale"),
            Packet((LoadMessageKind)999), Packet(LoadMessageKind.Prepare, lobby: Lobby), Packet(LoadMessageKind.Lobby) })
        {
            var client = Node(1);
            Check(!client.Receive(1001, invalid, 0).Accepted && client.Phase == LoadPhase.Idle,
                "A malformed or wrong-session envelope changed idle state.");
        }
        var clientActive = Node(1);
        clientActive.Receive(1001, Packet(LoadMessageKind.Prepare), 0); clientActive.Prepared(1);
        Check(!clientActive.Receive(9999, Packet(LoadMessageKind.Stop), 2).Accepted && !clientActive.IsCommitted,
            "An unrelated sender acquired stop authority.");
        Check(!clientActive.Receive(1003, Packet(LoadMessageKind.Stop), 2).Accepted && !clientActive.IsCommitted,
            "Another participant acquired the host's stop authority.");
        Check(!clientActive.Receive(1001, Packet(LoadMessageKind.Stop, operation: "ffffffffffffffffffffffffffffffff"), 2).Accepted,
            "An old operation acquired stop authority.");
        Check(!clientActive.Receive(1003, Packet(LoadMessageKind.Abort), 2).Accepted,
            "A client cancelled another client without host relay.");
        Check(!clientActive.PublishLobby(Lobby, 2).Accepted && !clientActive.Joined(2).Accepted,
            "A client published a lobby or joined without a host publication.");
        Check(clientActive.Receive(1001, Packet(LoadMessageKind.Abort), 3).Accepted && clientActive.Phase == LoadPhase.Aborted,
            "A real host could not abort a prepared client.");

        ulong[] original = Peers(3);
        var immutable = new LoadProtocol(1001, 1001, original, Source, 100);
        original[1] = 9999;
        Check(immutable.RosterMatches(Peers(3)) && !immutable.RosterMatches(original) &&
            !immutable.RosterMatches(new ulong[] { 1001, 1002, 1002 }), "The transition retained mutable/duplicate human roster state.");
        Check(immutable.Recipients(Packet(LoadMessageKind.Prepare)).SequenceEqual(new ulong[] { 1002, 1003 }),
            "Host broadcasts include self or lose an original participant.");
        Check(Node(1).Recipients(Packet(LoadMessageKind.Stopped)).SequenceEqual(new ulong[] { 1001, 1003 }) &&
            Node(1).Recipients(Packet(LoadMessageKind.Drained)).SequenceEqual(new ulong[] { 1001 }),
            "Stopped fences must cross all old streams; drain acknowledgements must go to the host only.");
        Throws(() => new LoadProtocol(1001, 1001, null, Source, 10), "Missing human roster accepted.");
        Throws(() => new LoadProtocol(1001, 1001, Array.Empty<ulong>(), Source, 10), "Empty controlled party accepted.");
        Throws(() => new LoadProtocol(1001, 1001, new ulong[] { 1001, 1001 }, Source, 10), "Duplicate human roster accepted.");
        Throws(() => new LoadProtocol(1001, 1001, new ulong[] { 1001, 0 }, Source, 10), "Zero human identity accepted.");
        Throws(() => new LoadProtocol(1001, 9999, Peers(3), Source, 10), "Foreign host identity accepted.");
        Throws(() => new LoadProtocol(9999, 1001, Peers(3), Source, 10), "Foreign local identity accepted.");
        Throws(() => new LoadProtocol(1001, 1001, Peers(3), 0, 10), "Missing source session accepted.");
        Throws(() => new LoadProtocol(1001, 1001, Peers(3), Source, 0), "Nonpositive deadline accepted.");
    }
    private static void EarlyCrossSenderFence()
    {
        var client = Node(1);
        client.Receive(1001, Packet(LoadMessageKind.Prepare), 0);
        Check(!client.Receive(1003, Packet(LoadMessageKind.Stopped), 0).Accepted,
            "An unprepared client retained an unauthorised stopped fence.");
        client.Prepared(1);
        LoadStep early = client.Receive(1003, Packet(LoadMessageKind.Stopped), 2);
        Check(early.Accepted && early.Effects == LoadEffect.None && early.Outbound.Count == 0 && !client.IsCommitted,
            "A cross-sender fence either vanished or granted host stop authority.");
        Check(!client.Receive(1003, Packet(LoadMessageKind.Stopped), 2).Accepted,
            "A duplicate early sender fence was counted twice.");
        client.Receive(1001, Packet(LoadMessageKind.Stop), 3);
        LoadStep stopped = client.Stopped(4);
        Check(stopped.Outbound.Count == 1 && stopped.Outbound[0].Kind == LoadMessageKind.Stopped,
            "A participant drained before receiving the host's reliable stopped fence.");
        LoadStep drained = client.Receive(1001, Packet(LoadMessageKind.Stopped), 5);
        Check(drained.Outbound.Count == 1 && drained.Outbound[0].Kind == LoadMessageKind.Drained &&
            client.Phase == LoadPhase.WaitingLobby, "All old sender fences did not produce one drain acknowledgement.");
        Check(!client.Receive(1001, Packet(LoadMessageKind.Stopped), 6).Accepted,
            "An old duplicate fence reopened the stream-drain barrier.");
    }
    private static void TimeoutsAndLocalGuards()
    {
        var host = Node(timeout: 10);
        Check(!host.Begin(Operation, -1).Accepted && host.Phase == LoadPhase.Idle, "Negative initial clock accepted.");
        host.Begin(Operation, 0); host.Prepared(1);
        Check(!host.PublishLobby(Lobby, 2).Accepted && !host.Stopped(2).Accepted && !host.Joined(2).Accepted,
            "Local callbacks bypassed preparation/stop/lobby barriers.");
        LoadStep timeout = host.Tick(10);
        Check(Has(timeout, LoadEffect.Cancelled) && host.Phase == LoadPhase.Aborted && !host.IsCommitted,
            "Pre-commit timeout did not leave the old-world cancellation category intact.");
        Check(timeout.Outbound.Count == 1 && timeout.Outbound[0].Kind == LoadMessageKind.Abort,
            "A timed-out host did not notify its original participants.");

        var client = Node(1, timeout: 10);
        client.Receive(1001, Packet(LoadMessageKind.Prepare), 0); client.Prepared(1);
        client.Receive(1001, Packet(LoadMessageKind.Stop), 2);
        Check(client.IsCommitted && Has(client.Tick(12), LoadEffect.Cancelled) && client.Phase == LoadPhase.Failed,
            "Committed stop timeout was confused with a reversible old-world cancellation.");
        Check(!client.Receive(1001, Packet(LoadMessageKind.Stop), 13).Accepted && client.IsTerminal,
            "A stale host command revived a failed transition.");

        var regressed = Node(); regressed.Begin(Operation, 5);
        Check(Has(regressed.Prepared(4), LoadEffect.Cancelled) && regressed.Phase == LoadPhase.Aborted,
            "Clock regression was accepted at a local callback.");
        var abortHost = Node(); abortHost.Begin(Operation, 0);
        LoadStep cancelled = abortHost.Receive(1002, Packet(LoadMessageKind.Abort), 1);
        Check(Has(cancelled, LoadEffect.Cancelled) && cancelled.Outbound.Count == 1 && abortHost.IsTerminal,
            "An expected participant's failure was not relayed through its host.");
        var notReady = Node(1); notReady.Receive(1001, Packet(LoadMessageKind.Prepare), 0);
        Check(!notReady.Receive(1001, Packet(LoadMessageKind.Stop), 1).Accepted && !notReady.IsCommitted,
            "The host stopped a client before positive local preparation.");
    }
    private static void PartialStopFailure()
    {
        var host = Node(); var first = Node(1); var second = Node(2);
        host.Begin(Operation, 0); host.Prepared(1);
        foreach (LoadProtocol client in new[] { first, second })
        { client.Receive(1001, Packet(LoadMessageKind.Prepare), 0); client.Prepared(1); }
        host.Receive(1002, Packet(LoadMessageKind.Prepared), 2);
        LoadStep stop = host.Receive(1003, Packet(LoadMessageKind.Prepared), 3);
        Check(host.IsCommitted && Has(stop, LoadEffect.StopLocal), "Partial-send fixture did not reach host commitment.");
        // The first peer receives Stop, the second does not. A failed Send must
        // not allow the still-prepared peer to treat cancellation as reversible.
        first.Receive(1001, Packet(LoadMessageKind.Stop), 4);
        LoadStep failure = host.Abort("injected failure sending Stop to one participant");
        Check(failure.Outbound.Count == 1 && failure.Outbound[0].Kind == LoadMessageKind.CommittedAbort,
            "Host lost group commitment when only part of its Stop broadcast was submitted.");
        foreach (LoadProtocol client in new[] { first, second })
        {
            LoadStep received = client.Receive(1001, failure.Outbound[0], 5);
            Check(received.Accepted && Has(received, LoadEffect.Cancelled) && client.IsCommitted &&
                client.Phase == LoadPhase.Failed, "A partial Stop recipient resumed a world already retired by its peers.");
        }

        var timedOut = Node(2, timeout: 10);
        timedOut.Receive(1001, Packet(LoadMessageKind.Prepare), 0); timedOut.Prepared(1);
        timedOut.Tick(10);
        Check(timedOut.Phase == LoadPhase.Aborted && !timedOut.IsCommitted,
            "Crossed-clock fixture did not retain a reversible local abort.");
        Check(!timedOut.Receive(1002, Packet(LoadMessageKind.CommittedAbort), 11).Accepted && !timedOut.IsCommitted,
            "A non-host converted another client's cancelled receipt into group commitment.");
        LoadStep late = timedOut.Receive(1001, failure.Outbound[0], 11);
        Check(late.Accepted && Has(late, LoadEffect.Cancelled) && timedOut.IsCommitted && timedOut.Phase == LoadPhase.Failed,
            "A local timeout lost the authoritative host's crossed commitment failure.");
        foreach (LoadMessageKind kind in new[] { LoadMessageKind.Stop, LoadMessageKind.CommittedAbort })
        {
            var crossed = Node(2, timeout: 10);
            crossed.Receive(1001, Packet(LoadMessageKind.Prepare), 0); crossed.Prepared(1);
            LoadStep expired = crossed.Receive(1001, Packet(kind), 10);
            Check(expired.Accepted && Has(expired, LoadEffect.Cancelled) && crossed.IsCommitted && crossed.Phase == LoadPhase.Failed,
                "A just-expired local clock downgraded the host's positive commitment evidence.");
        }

        var waitingHost = Node(); waitingHost.Begin(Operation, 0); waitingHost.Prepared(1);
        LoadStep reported = waitingHost.Receive(1002, Packet(LoadMessageKind.CommittedAbort), 2);
        Check(waitingHost.IsCommitted && Has(reported, LoadEffect.Cancelled) &&
            reported.Outbound.Single().Kind == LoadMessageKind.CommittedAbort,
            "A participant's positive commitment report was downgraded to an uncommitted host abort.");
    }
    internal static int Main()
    {
        AuthorityAndFraming(); EarlyCrossSenderFence(); TimeoutsAndLocalGuards(); PartialStopFailure(); SuccessfulSchedules();
        Console.WriteLine("PASS: " + checks + " multiplayer load protocol behavior checks; no game/network acceptance inferred.");
        return 0;
    }
}
