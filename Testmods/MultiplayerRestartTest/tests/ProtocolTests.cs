using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerRestartTest;

internal static class ProtocolTests
{
    private static int checks;
    private static readonly ulong[] Peers = { 1001, 1002, 1003 };
    private const string Session = "old-lobby-and-launch";
    private const string Digest = "verified-start-configuration";
    private static readonly string Op = Guid.NewGuid().ToString("N");
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static RestartProtocol New(ulong self, long timeout = 100) =>
        new RestartProtocol(self, Peers[0], Peers, Session, Digest, timeout);
    private static RestartPacket Packet(RestartMessageKind kind, string operation = null, string session = Session, string digest = Digest, ulong lobby = 0) =>
        new RestartPacket { Kind = kind, Session = session, Operation = operation ?? Op, Digest = digest, LobbyId = lobby };
    private static RestartPacket Only(RestartStep step, RestartMessageKind kind)
    { Check(step.Accepted && step.Outbound.Count == 1 && step.Outbound[0].Kind == kind, "Expected one outbound " + kind); return step.Outbound[0]; }
    private static void Rejected(RestartProtocol protocol, ulong sender, RestartPacket packet, long time = 0)
    {
        var phase = protocol.Phase;
        var step = protocol.Receive(sender, packet, time);
        Check(!step.Accepted && step.Effects == RestartEffect.None && step.Outbound.Count == 0 && protocol.Phase == phase,
            "Rejected packet changed state or emitted work.");
    }

    private static void AuthorityAndAdmission()
    {
        Check(!New(Peers[1]).Begin(Op, 0).Accepted, "Client began restart.");
        Check(!New(Peers[0]).Begin("not-an-operation", 0).Accepted, "Malformed operation accepted.");
        foreach (RestartMessageKind kind in Enum.GetValues(typeof(RestartMessageKind)))
        {
            var client = New(Peers[1]);
            Rejected(client, Peers[2], Packet(kind));
            Rejected(client, 9000, Packet(kind));
            Rejected(client, Peers[1], Packet(kind));
            if (kind != RestartMessageKind.Prepare) Rejected(client, Peers[0], Packet(kind));
        }
        Rejected(New(Peers[1]), Peers[0], Packet(RestartMessageKind.Prepare, session: "previous-session"));
        Rejected(New(Peers[1]), Peers[0], Packet(RestartMessageKind.Prepare, digest: "different-start-settings"));
        Rejected(New(Peers[1]), Peers[0], Packet(RestartMessageKind.Prepare, operation: "invalid"));
        Rejected(New(Peers[1]), Peers[0], Packet(RestartMessageKind.Prepare, lobby: 77));
        var accepted = New(Peers[1]);
        Check(accepted.Receive(Peers[0], Packet(RestartMessageKind.Prepare), 0).Effects == RestartEffect.PrepareLocal, "Host Prepare not admitted.");
        Rejected(accepted, Peers[0], Packet(RestartMessageKind.Prepare));
        Rejected(accepted, Peers[0], Packet(RestartMessageKind.Stop));
        Rejected(accepted, Peers[0], Packet(RestartMessageKind.Lobby, lobby: 77));
        Rejected(accepted, Peers[0], Packet(RestartMessageKind.Start));
        Rejected(accepted, Peers[0], Packet(RestartMessageKind.Stop, operation: Guid.NewGuid().ToString("N")));
        Rejected(accepted, Peers[0], Packet((RestartMessageKind)999));
        Check(accepted.RosterMatches(Peers.Reverse()), "Roster order affected identity.");
        Check(!accepted.RosterMatches(new ulong[] { 1001, 1002, 1002 }), "Duplicate roster accepted.");
        Check(!accepted.RosterMatches(new ulong[] { 1001, 1002 }), "Missing peer accepted.");
        Check(!accepted.RosterMatches(new ulong[] { 1001, 1002, 1004 }), "Replacement peer accepted.");
    }

    private static void BarriersAndRoundTrip(bool hostAcknowledgesFirst, bool reverseClientOrder)
    {
        var host = New(Peers[0]);
        var first = New(Peers[1]);
        var second = New(Peers[2]);
        var clients = reverseClientOrder ? new[] { second, first } : new[] { first, second };
        var ids = reverseClientOrder ? new[] { Peers[2], Peers[1] } : new[] { Peers[1], Peers[2] };
        var prepare = Only(host.Begin(Op, 0), RestartMessageKind.Prepare);
        Check(!host.IsCommitted, "Prepare committed before consensus.");
        foreach (var client in clients)
            Check(client.Receive(Peers[0], prepare, 1).Effects == RestartEffect.PrepareLocal, "Client preparation effect missing.");
        Rejected(host, Peers[1], Packet(RestartMessageKind.Joined), 1);
        if (hostAcknowledgesFirst) Check(host.Prepared(2).Outbound.Count == 0, "Host stopped before peers ready.");
        RestartStep readyStep = null;
        for (int i = 0; i < clients.Length; i++)
        {
            var ready = Only(clients[i].Prepared(2), RestartMessageKind.Ready);
            Rejected(clients[1-i], ids[i], ready, 2);
            readyStep = host.Receive(ids[i], ready, 3);
            if (i == 0)
            {
                Check(readyStep.Effects == RestartEffect.None && host.Phase == RestartPhase.Preparing, "Stop before final Ready.");
                Rejected(host, ids[i], ready, 3);
            }
        }
        if (!hostAcknowledgesFirst)
        {
            Check(host.Phase == RestartPhase.Preparing, "Host's own preparation was omitted.");
            readyStep = host.Prepared(3);
        }
        var stop = Only(readyStep, RestartMessageKind.Stop);
        Check(readyStep.Effects == RestartEffect.StopLocal && host.IsCommitted, "Host commit missing.");
        foreach (var client in clients)
            Check(client.Receive(Peers[0], stop, 4).Effects == RestartEffect.StopLocal && client.IsCommitted, "Client stop missing.");
        Check(!host.PublishLobby(77, 4).Accepted, "Lobby published before stopped barrier.");
        if (hostAcknowledgesFirst) Check(host.Stopped(5).Effects == RestartEffect.None, "Lobby created before peers stopped.");
        RestartStep stoppedStep = null;
        for (int i = 0; i < clients.Length; i++)
        {
            var stopped = Only(clients[i].Stopped(5), RestartMessageKind.Stopped);
            stoppedStep = host.Receive(ids[i], stopped, 6);
            if (i == 0) Check(stoppedStep.Effects == RestartEffect.None, "Lobby created before all stopped.");
        }
        if (!hostAcknowledgesFirst) stoppedStep = host.Stopped(6);
        Check(stoppedStep.Effects == RestartEffect.CreateLobby && host.Phase == RestartPhase.WaitingLobby, "Stopped consensus did not create lobby.");
        var lobbyStep = host.PublishLobby(77, 7);
        var lobby = Only(lobbyStep, RestartMessageKind.Lobby);
        Check(host.LobbyId == 77 && lobbyStep.Effects == RestartEffect.JoinLobby, "Host lobby receipt missing.");
        foreach (var client in clients)
        {
            Rejected(client, Peers[0], Packet(RestartMessageKind.Lobby), 7);
            Check(client.Receive(Peers[0], lobby, 8).Effects == RestartEffect.JoinLobby && client.LobbyId == 77, "Client target lobby missing.");
            Rejected(client, Peers[0], Packet(RestartMessageKind.Start), 8);
        }
        if (hostAcknowledgesFirst) Check(host.Verified(9).Effects == RestartEffect.None, "Start before peers verified.");
        RestartStep verifiedStep = null;
        for (int i = 0; i < clients.Length; i++)
        {
            var joined = Only(clients[i].Verified(9), RestartMessageKind.Joined);
            verifiedStep = host.Receive(ids[i], joined, 10);
            if (i == 0) Check(verifiedStep.Effects == RestartEffect.None, "Start before final verified receipt.");
        }
        if (!hostAcknowledgesFirst) verifiedStep = host.Verified(10);
        var start = Only(verifiedStep, RestartMessageKind.Start);
        Check(verifiedStep.Effects == RestartEffect.None && host.Phase == RestartPhase.Starting, "Host launched before final channel-2 acknowledgement barrier.");
        Check(!host.Complete().Accepted, "Host completed before clients granted launch permission.");
        for (int i = 0; i < clients.Length; i++)
        {
            var client = clients[i];
            var clientStart = client.Receive(Peers[0], start, 11);
            Check(clientStart.Effects == RestartEffect.StartLocal, "Client waiting-for-vanilla start missing.");
            var startAck = Only(clientStart, RestartMessageKind.StartAcknowledged);
            Rejected(client, Peers[0], start, 11);
            var hostLaunch = host.Receive(ids[i], startAck, 12);
            Check(hostLaunch.Accepted && hostLaunch.Effects == (i == clients.Length - 1 ? RestartEffect.StartLocal : RestartEffect.None),
                "Host launch did not wait for every client launch-permission receipt.");
            if (i == 0) Check(!host.Complete().Accepted, "Host completed with a missing client acknowledgement.");
            Rejected(host, ids[i], startAck, 12);
            Check(client.Complete().Accepted && client.Phase == RestartPhase.Completed, "Client did not complete.");
            Rejected(client, Peers[0], Packet(RestartMessageKind.Abort), 12);
        }
        Check(host.Complete().Accepted && host.IsTerminal, "Host did not complete.");
        Check(!host.Begin(Guid.NewGuid().ToString("N"), 13).Accepted, "Retired attempt reused.");
    }

    private static void Failures()
    {
        var host = New(Peers[0], 10);
        host.Begin(Op, 0);
        Check(host.Tick(9).Effects == RestartEffect.None, "Premature timeout.");
        var timedOut = host.Tick(10);
        Check(timedOut.Effects == RestartEffect.Cancelled && host.Phase == RestartPhase.Aborted && !host.IsCommitted, "Precommit timeout not abortable.");
        Only(timedOut, RestartMessageKind.Abort);
        Check(host.Tick(11).Outbound.Count == 0, "Terminal timeout retransmitted.");
        var client = New(Peers[1], 10);
        client.Receive(Peers[0], Packet(RestartMessageKind.Prepare), 0);
        client.Prepared(1);
        client.Receive(Peers[0], Packet(RestartMessageKind.Stop), 2);
        Check(client.Tick(12).Effects == RestartEffect.Cancelled && client.Phase == RestartPhase.Failed && client.IsCommitted, "Postcommit timeout promised rollback.");
        client = New(Peers[1]);
        client.Receive(Peers[0], Packet(RestartMessageKind.Prepare), 5);
        Rejected(client, Peers[2], Packet(RestartMessageKind.Abort), 5);
        Check(client.Receive(Peers[0], Packet(RestartMessageKind.Abort), 6).Effects == RestartEffect.Cancelled && client.Phase == RestartPhase.Aborted, "Host abort not followed.");
        host = New(Peers[0]); host.Begin(Op, 0);
        var aborted = host.Receive(Peers[1], Packet(RestartMessageKind.Abort), 1);
        Check(aborted.Effects == RestartEffect.Cancelled && host.Phase == RestartPhase.Aborted, "Participant veto not honored.");
        Only(aborted, RestartMessageKind.Abort);
        host = New(Peers[0]); host.Begin(Op, 5);
        Check(host.Tick(4).Effects == RestartEffect.Cancelled && host.Phase == RestartPhase.Aborted, "Clock regression hung attempt.");
        Check(!New(Peers[0]).Complete().Accepted, "Idle completed.");
        Check(!New(Peers[0]).Prepared(0).Accepted, "Idle acknowledged preparation.");
        Check(!New(Peers[0]).Stopped(0).Accepted, "Idle acknowledged stop.");
        Check(!New(Peers[0]).Verified(0).Accepted, "Idle acknowledged lobby verification.");
        host = new RestartProtocol(1001, 1001, new ulong[] { 1001, 1002 }, Session, Digest, 10);
        host.Begin(Op, 0); host.Prepared(0);
        host.Receive(1002, Packet(RestartMessageKind.Ready), 0); host.Stopped(0);
        host.Receive(1002, Packet(RestartMessageKind.Stopped), 0); host.PublishLobby(77, 0);
        Rejected(host, 1002, Packet(RestartMessageKind.StartAcknowledged), 0);
        host.Verified(0);
        Check(host.Receive(1002, Packet(RestartMessageKind.Joined), 0).Effects == RestartEffect.None, "Two-peer host skipped final start barrier.");
        Check(host.Tick(10).Effects == RestartEffect.Cancelled && host.Phase == RestartPhase.Failed, "Missing final start acknowledgement did not fail safely.");
        Rejected(host, 1002, Packet(RestartMessageKind.StartAcknowledged), 10);
        foreach (var invalidPeers in new[] { new ulong[] { 1001 }, new ulong[] { 1001, 1001 }, new ulong[] { 1001, 0 } })
        {
            bool rejected = false;
            try { new RestartProtocol(1001, 1001, invalidPeers, Session, Digest, 10); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid roster constructor accepted.");
        }
    }

    public static int Main()
    {
        try
        {
            AuthorityAndAdmission();
            foreach (bool hostFirst in new[] { true, false })
                foreach (bool reverse in new[] { true, false }) BarriersAndRoundTrip(hostFirst, reverse);
            Failures();
            Console.WriteLine("PASS: " + checks + " restart protocol behavior checks. No game/network acceptance is inferred.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
