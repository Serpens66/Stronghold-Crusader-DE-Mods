using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerRestartTest;

// Independent reliable sender streams are FIFO; their interleaving is not ordered.
// The fixture executes production protocols and includes old-payload sentinels before
// every Stopped marker, so premature lobby creation is observable under each schedule.
internal static class ProtocolTests
{
    private const string Session = "source-session", Digest = "source-digest";
    private static readonly string Operation = Guid.NewGuid().ToString("N");
    private static int checks;
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    private static RestartPacket Packet(RestartMessageKind kind, string session = Session, string operation = null, string digest = Digest, ulong lobby = 0) =>
        new RestartPacket { Kind = kind, Session = session, Operation = operation ?? Operation, Digest = digest, LobbyId = lobby };
    private static void Rejected(RestartProtocol node, ulong sender, RestartPacket packet, long now)
    {
        var phase = node.Phase; var step = node.Receive(sender, packet, now);
        Check(!step.Accepted && step.Effects == RestartEffect.None && step.Outbound.Count == 0 && node.Phase == phase,
            "Rejected input changed state or emitted work.");
    }
    private sealed class Envelope
    {
        internal int Sender, Recipient;
        internal RestartPacket Packet;
        internal bool Old;
    }
    private sealed class Mesh
    {
        internal readonly RestartProtocol[] Nodes;
        internal readonly List<Envelope> Queue = new List<Envelope>();
        internal readonly int[,] Effects;
        private readonly bool[,] markers;
        private readonly int schedule;
        private readonly Random random;
        internal long Now = 1;
        internal const long Timeout = 10000;
        internal static ulong Id(int n) => (ulong)(1001 + n);
        internal Mesh(int count, int schedule)
        {
            this.schedule = schedule; random = new Random(701 + count * 31 + schedule);
            ulong[] peers = Enumerable.Range(0, count).Select(Id).ToArray();
            Nodes = peers.Select(id => new RestartProtocol(id, peers[0], peers, Session, Digest, Timeout)).ToArray();
            Effects = new int[count, 7]; markers = new bool[count, count];
        }
        internal void Emit(int sender, RestartStep step)
        {
            Check(step.Accepted, "Unexpected local rejection: " + step.Rejection);
            for (int bit = 0; bit < 7; bit++) if (((int)step.Effects & (1 << bit)) != 0) Effects[sender, bit]++;
            if ((step.Effects & RestartEffect.CreateLobby) != 0)
            {
                Check(sender == 0 && !Queue.Any(item => item.Old), "Lobby creation bypassed unread old peer traffic.");
                Check(Nodes.All(node => node.Phase == RestartPhase.WaitingLobby), "Lobby creation bypassed a peer's stream-drain barrier.");
            }
            foreach (var packet in step.Outbound)
                for (int peer = 0; peer < Nodes.Length; peer++) if (peer != sender)
                    Queue.Add(new Envelope { Sender = sender, Recipient = peer, Packet = packet });
        }
        private bool Head(int i) => !Queue.Take(i).Any(item => item.Sender == Queue[i].Sender && item.Recipient == Queue[i].Recipient);
        internal Envelope Take(Func<Envelope, bool> predicate = null)
        {
            int[] choices = Enumerable.Range(0, Queue.Count).Where(i => Head(i) && (predicate == null || predicate(Queue[i]))).ToArray();
            if (choices.Length == 0) throw new Exception("No requested FIFO-admissible delivery.");
            int i = schedule == 0 ? choices[0] : schedule == 1 ? choices[choices.Length - 1] : choices[random.Next(choices.Length)];
            Envelope item = Queue[i]; Queue.RemoveAt(i); return item;
        }
        internal void Deliver(Envelope item)
        {
            Now++;
            if (item.Old) { Check(!markers[item.Recipient, item.Sender], "Old payload followed its sender's stopped marker."); return; }
            if (item.Packet.Kind == RestartMessageKind.Stopped) markers[item.Recipient, item.Sender] = true;
            var step = Nodes[item.Recipient].Receive(Id(item.Sender), item.Packet, Now);
            bool hostOnly = item.Packet.Kind == RestartMessageKind.Ready || item.Packet.Kind == RestartMessageKind.StreamsDrained ||
                item.Packet.Kind == RestartMessageKind.Joined || item.Packet.Kind == RestartMessageKind.StartAcknowledged ||
                item.Packet.Kind == RestartMessageKind.SimulationReady || item.Packet.Kind == RestartMessageKind.CompleteAck;
            if (hostOnly && item.Recipient != 0)
            {
                Check(!step.Accepted && step.Effects == RestartEffect.None && step.Outbound.Count == 0, "Client treated another peer's acknowledgement as authority.");
                return;
            }
            if (!step.Accepted && item.Packet.Kind == RestartMessageKind.Abort && Nodes[item.Recipient].Phase == RestartPhase.Failed)
            { Check(step.Effects == RestartEffect.None && step.Outbound.Count == 0, "Repeated abort replayed failure cleanup."); return; }
            Emit(item.Recipient, step);
        }
        internal void Drain() { while (Queue.Count != 0) Deliver(Take()); }
        private void OldTraffic(int sender)
        {
            for (int peer = 0; peer < Nodes.Length; peer++) if (peer != sender)
                Queue.Add(new Envelope { Sender = sender, Recipient = peer, Old = true });
        }
        internal void Prepare()
        {
            Emit(0, Nodes[0].Begin(Operation, Now++)); Drain();
            int[] order = Enumerable.Range(0, Nodes.Length).ToArray(); if (schedule != 0) Array.Reverse(order);
            foreach (int peer in order) Emit(peer, Nodes[peer].Prepared(Now++));
            while (Queue.Any(item => item.Packet.Kind == RestartMessageKind.Ready)) Deliver(Take(item => item.Packet.Kind == RestartMessageKind.Ready));
            Check(Nodes[0].Phase == RestartPhase.Stopping && Nodes.Skip(1).All(node => node.Phase == RestartPhase.Preparing), "Preparation skipped a peer.");
        }
        internal void StopAndDrain()
        {
            Deliver(Take(item => item.Packet.Kind == RestartMessageKind.Stop && item.Recipient == 1));
            OldTraffic(1); Emit(1, Nodes[1].Stopped(Now++));
            if (Nodes.Length > 2)
            {
                Deliver(Take(item => item.Old && item.Sender == 1 && item.Recipient == 2));
                Deliver(Take(item => !item.Old && item.Packet.Kind == RestartMessageKind.Stopped && item.Sender == 1 && item.Recipient == 2));
                Check(Nodes[2].Phase == RestartPhase.Preparing && !Nodes[2].IsCommitted, "Remote stopped marker granted local host authority.");
            }
            while (Queue.Any(item => !item.Old && item.Packet.Kind == RestartMessageKind.Stop)) Deliver(Take(item => !item.Old && item.Packet.Kind == RestartMessageKind.Stop));
            int[] order = Enumerable.Range(0, Nodes.Length).Where(peer => peer != 1).ToArray(); if (schedule != 0) Array.Reverse(order);
            foreach (int peer in order) { OldTraffic(peer); Emit(peer, Nodes[peer].Stopped(Now++)); }
            Check(!Nodes[0].PublishLobby(77, Now).Accepted, "Lobby published before every old stream drained.");
            if (schedule == 5 && Nodes.Length > 2)
            {
                // Client 1 has drained every stream while the host has deliberately
                // not read client 2's marker. Its receipt must be retained early,
                // but it cannot substitute for the host's own drain confirmation.
                for (int sender = 0; sender < Nodes.Length; sender++) if (sender != 1)
                {
                    int source = sender;
                    Deliver(Take(item => item.Old && item.Sender == source && item.Recipient == 1));
                    Deliver(Take(item => !item.Old && item.Packet.Kind == RestartMessageKind.Stopped && item.Sender == source && item.Recipient == 1));
                }
                Deliver(Take(item => item.Old && item.Sender == 1 && item.Recipient == 0));
                Deliver(Take(item => !item.Old && item.Packet.Kind == RestartMessageKind.Stopped && item.Sender == 1 && item.Recipient == 0));
                Deliver(Take(item => !item.Old && item.Packet.Kind == RestartMessageKind.StreamsDrained && item.Sender == 1 && item.Recipient == 0));
                Check(Nodes[0].Phase == RestartPhase.Draining && Effects[0, 2] == 0,
                    "An early peer drain receipt bypassed the host's unread stream.");
            }
            while (Queue.Any(item => item.Old || item.Packet.Kind == RestartMessageKind.Stopped))
                Deliver(Take(item => item.Old || item.Packet.Kind == RestartMessageKind.Stopped));
            Check(Effects[0, 2] == 0, "Host substituted its own drained streams for all-peer convergence.");
            int delayed = Nodes.Length - 1;
            while (Queue.Any(item => item.Packet.Kind == RestartMessageKind.StreamsDrained && !(item.Sender == delayed && item.Recipient == 0)))
                Deliver(Take(item => item.Packet.Kind == RestartMessageKind.StreamsDrained && !(item.Sender == delayed && item.Recipient == 0)));
            Check(Effects[0, 2] == 0, "Host omitted final peer stream-drain receipt.");
            Drain(); Check(Effects[0, 2] == 1 && Nodes.All(node => node.Phase == RestartPhase.WaitingLobby), "Drain convergence did not create exactly one lobby.");
        }
        internal void Start()
        {
            Emit(0, Nodes[0].PublishLobby(77, Now++)); Drain();
            Check(Nodes.All(node => node.LobbyId == 77 && node.Phase == RestartPhase.Joining), "Target lobby differs.");
            foreach (var node in Nodes) Check(!node.LocalSimulationReady(Now).Accepted, "Native readiness admitted before launch.");
            for (int peer = Nodes.Length - 1; peer >= 0; peer--) Emit(peer, Nodes[peer].Verified(Now++));
            while (Queue.Any(item => item.Packet.Kind == RestartMessageKind.Joined)) Deliver(Take(item => item.Packet.Kind == RestartMessageKind.Joined));
            Check(Effects[0, 4] == 0 && !Nodes[0].LocalSimulationReady(Now).Accepted, "Host skipped launch-grant barrier.");
            while (Queue.Any(item => item.Packet.Kind == RestartMessageKind.Start)) Deliver(Take(item => item.Packet.Kind == RestartMessageKind.Start));
            Check(Effects[0, 4] == 0, "Host started without receiving client launch grants.");
            Drain(); Check(Nodes.All(node => node.Phase == RestartPhase.Starting) && Enumerable.Range(0, Nodes.Length).All(peer => Effects[peer, 4] == 1), "Launch was not granted once per node.");
        }
        internal void SimulationReady()
        {
            for (int peer = Nodes.Length - 1; peer >= 0; peer--)
            { Emit(peer, Nodes[peer].LocalSimulationReady(Now++)); Check(!Nodes[peer].LocalSimulationReady(Now).Accepted, "Duplicate local ready emitted work."); }
            while (Queue.Any(item => item.Packet.Kind == RestartMessageKind.SimulationReady)) Deliver(Take(item => item.Packet.Kind == RestartMessageKind.SimulationReady));
            Check(Nodes[0].Phase == RestartPhase.Completing && Effects[0, 6] == 0, "Host retired before completion acknowledgements.");
        }
    }
    private static Mesh Starting(int count = 3, int schedule = 0)
    { var mesh = new Mesh(count, schedule); mesh.Prepare(); mesh.StopAndDrain(); mesh.Start(); return mesh; }

    private static void FullRoundTrip(int count, int schedule)
    {
        Mesh mesh = Starting(count, schedule);
        Rejected(mesh.Nodes[1], Mesh.Id(0), Packet(RestartMessageKind.Complete), mesh.Now);
        for (int peer = 0; peer < count - 1; peer++) mesh.Emit(peer, mesh.Nodes[peer].LocalSimulationReady(mesh.Now++));
        mesh.Drain(); Check(mesh.Nodes.All(node => node.Phase == RestartPhase.Starting), "Missing target-simulation readiness was ignored.");
        mesh.Emit(count - 1, mesh.Nodes[count - 1].LocalSimulationReady(mesh.Now++));
        while (mesh.Queue.Any(item => item.Packet.Kind == RestartMessageKind.SimulationReady)) mesh.Deliver(mesh.Take(item => item.Packet.Kind == RestartMessageKind.SimulationReady));
        Check(mesh.Nodes[0].Phase == RestartPhase.Completing, "All native-ready receipts did not begin completion.");
        mesh.Deliver(mesh.Take(item => item.Packet.Kind == RestartMessageKind.Complete && item.Recipient == 1));
        Check(mesh.Nodes[1].Phase == RestartPhase.Completed && mesh.Effects[1, 6] == 1, "Client did not retain completion before its acknowledgement.");
        mesh.Take(item => item.Packet.Kind == RestartMessageKind.CompleteAck && item.Sender == 1 && item.Recipient == 0); // Lost ACK.
        mesh.Drain(); Check(mesh.Nodes[0].Phase == RestartPhase.Completing && mesh.Effects[0, 6] == 0, "Host ignored lost CompleteAck.");
        mesh.Emit(0, mesh.Nodes[0].RetryCompletion(mesh.Now++)); mesh.Drain();
        Check(mesh.Nodes.All(node => node.Phase == RestartPhase.Completed) && Enumerable.Range(0, count).All(peer => mesh.Effects[peer, 6] == 1), "Retry repeated cleanup or failed to converge.");
        Check(mesh.Nodes.All(node => node.Session == Session && node.Digest == Digest && node.Operation == Operation), "Receipt lost immutable source identity.");
        Rejected(mesh.Nodes[1], Mesh.Id(0), Packet(RestartMessageKind.Abort, session: "new-session"), mesh.Now);
        Rejected(mesh.Nodes[1], Mesh.Id(0), Packet(RestartMessageKind.Abort, operation: Guid.NewGuid().ToString("N")), mesh.Now);
        Rejected(mesh.Nodes[1], Mesh.Id(0), Packet(RestartMessageKind.Start), mesh.Now);
        if (count > 2) Rejected(mesh.Nodes[1], Mesh.Id(2), Packet(RestartMessageKind.Abort), mesh.Now);
        var duplicate = mesh.Nodes[1].Receive(Mesh.Id(0), Packet(RestartMessageKind.Complete), mesh.Now + Mesh.Timeout * 2);
        Check(duplicate.Accepted && duplicate.Effects == RestartEffect.None && duplicate.Outbound.Count == 1 &&
            duplicate.Outbound[0].Kind == RestartMessageKind.CompleteAck,
            "Retained completion receipt expired or repeated local cleanup on a duplicate Complete.");
        Check(!mesh.Nodes[0].Begin(Guid.NewGuid().ToString("N"), mesh.Now).Accepted, "Retained receipt reused for a new attempt.");
    }
    private static void PartialCompletionFailure()
    {
        Mesh mesh = Starting(); mesh.SimulationReady();
        mesh.Deliver(mesh.Take(item => item.Packet.Kind == RestartMessageKind.Complete && item.Recipient == 1));
        mesh.Queue.RemoveAll(item => item.Packet.Kind == RestartMessageKind.Complete); // Remaining send never submitted.
        mesh.Emit(0, mesh.Nodes[0].Abort("partial Complete send failed"));
        while (mesh.Queue.Any(item => item.Packet.Kind == RestartMessageKind.CompleteAck)) mesh.Take(item => item.Packet.Kind == RestartMessageKind.CompleteAck);
        mesh.Drain(); Check(mesh.Nodes.All(node => node.Phase == RestartPhase.Failed) && Enumerable.Range(0, 3).All(peer => mesh.Effects[peer, 5] == 1), "Partial Complete could not abort completed and pending nodes.");
        Rejected(mesh.Nodes[1], Mesh.Id(0), Packet(RestartMessageKind.Abort), mesh.Now);
        mesh = Starting(2); mesh.SimulationReady(); mesh.Drain();
        mesh.Emit(1, mesh.Nodes[1].Abort("late current-target failure")); mesh.Drain();
        Check(mesh.Nodes.All(node => node.Phase == RestartPhase.Failed), "Completed host discarded a valid target-bound peer Abort.");
    }
    private static void AuthorityAndTimeouts()
    {
        ulong[] peers = { 1001, 1002, 1003 };
        Func<ulong, RestartProtocol> fresh = id => new RestartProtocol(id, 1001, peers, Session, Digest, 10);
        Check(!fresh(1002).Begin(Operation, 0).Accepted, "Client began restart.");
        foreach (RestartMessageKind kind in Enum.GetValues(typeof(RestartMessageKind)))
        {
            Rejected(fresh(1002), 1003, Packet(kind), 0); Rejected(fresh(1002), 9999, Packet(kind), 0); Rejected(fresh(1002), 1002, Packet(kind), 0);
            if (kind != RestartMessageKind.Prepare) Rejected(fresh(1002), 1001, Packet(kind), 0);
        }
        Rejected(fresh(1002), 1001, Packet(RestartMessageKind.Prepare, lobby: 77), 0);
        Rejected(fresh(1002), 1001, Packet(RestartMessageKind.Prepare, session: "previous"), 0);
        Rejected(fresh(1002), 1001, Packet(RestartMessageKind.Prepare, digest: "different"), 0);
        Rejected(fresh(1002), 1001, Packet(RestartMessageKind.Prepare, operation: "invalid"), 0);
        var host = fresh(1001); host.Begin(Operation, 0);
        Check(host.Tick(9).Effects == RestartEffect.None && host.Tick(10).Effects == RestartEffect.Cancelled && host.Phase == RestartPhase.Aborted && !host.IsCommitted, "Prepare timeout crossed commitment boundary.");
        var client = fresh(1002); client.Receive(1001, Packet(RestartMessageKind.Prepare), 0); client.Prepared(1); client.Receive(1001, Packet(RestartMessageKind.Stop), 2);
        Check(client.Tick(12).Effects == RestartEffect.Cancelled && client.Phase == RestartPhase.Failed, "Post-commit timeout promised rollback.");
        host = fresh(1001); host.Begin(Operation, 5); Check(host.Tick(4).Effects == RestartEffect.Cancelled, "Clock regression hung attempt.");
        Check(fresh(1001).RosterMatches(peers.Reverse()) && !fresh(1001).RosterMatches(new ulong[] { 1001, 1002, 1002 }), "Roster accepted duplicate identities.");
        Mesh mesh = Starting(); long waiting = mesh.Now;
        mesh.Emit(0, mesh.Nodes[0].LocalSimulationReady(mesh.Now++)); mesh.Drain();
        Check(mesh.Nodes[0].Phase == RestartPhase.Starting && mesh.Nodes[0].Tick(waiting + Mesh.Timeout).Effects == RestartEffect.Cancelled, "Missing simulation-ready peer did not time out.");
        mesh = Starting(); mesh.SimulationReady(); long completing = mesh.Now;
        Check(mesh.Nodes[0].RetryCompletion(completing + Mesh.Timeout - 2).Accepted, "Premature completion retry timeout.");
        Check(mesh.Nodes[0].RetryCompletion(completing + Mesh.Timeout).Effects == RestartEffect.Cancelled && mesh.Nodes[0].Phase == RestartPhase.Failed, "Completion retry renewed missing-ACK deadline.");
    }
    public static int Main()
    {
        try
        {
            AuthorityAndTimeouts();
            foreach (int count in new[] { 2, 3, 8 }) for (int schedule = 0; schedule < 6; schedule++) FullRoundTrip(count, schedule);
            PartialCompletionFailure();
            Console.WriteLine("PASS: " + checks + " restart protocol behavior checks. No game/network acceptance is inferred."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
