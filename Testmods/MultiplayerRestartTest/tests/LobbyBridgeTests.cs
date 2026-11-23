using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using MultiplayerSaveLoadTest;
using Steamworks;

// Executes the production framing and lobby-membership decisions without starting
// Unity/Steam. Metadata validation uses the actual selected game/Extender assemblies.
internal static class LobbyBridgeTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }

    internal static void Run()
    {
        LobbyBridge.Validate();
        Check(LobbyBridge.CreationResultMethod != null && !LobbyBridge.CreationResultMethod.IsStatic,
            "Native lobby completion observer contract is unavailable.");
        Framing();
        Membership();
        Console.WriteLine("LobbyBridge: " + checks + " checks passed.");
    }

    private static byte[] Frame(short id, byte[] body)
    {
        var bytes = new byte[6 + body.Length];
        Buffer.BlockCopy(BitConverter.GetBytes(id), 0, bytes, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, bytes, 2, 4);
        Buffer.BlockCopy(body, 0, bytes, 6, body.Length);
        return bytes;
    }

    private static void Framing()
    {
        const short id = 137;
        var frame = Frame(id, new byte[] { 5, 7, 9 });
        var body = LobbyBridge.ExtractControl(frame, id);
        Check(body != null && body.SequenceEqual(new byte[] { 5, 7, 9 }), "Valid control body was altered.");
        frame[6] = 88;
        Check(body[0] == 5, "Extracted payload aliases the Steam message buffer.");
        Check(LobbyBridge.ExtractControl(null, id) == null, "Null input accepted.");
        for (int length = 0; length < 7; length++)
            Check(LobbyBridge.ExtractControl(new byte[length], id) == null, "Truncated envelope accepted.");
        Check(LobbyBridge.ExtractControl(Frame(id, new byte[0]), id) == null, "Empty payload accepted.");
        Check(LobbyBridge.ExtractControl(Frame(id, new byte[1]), 0) == null, "Invalid packet registration accepted.");
        Check(LobbyBridge.ExtractControl(Frame(id, new byte[1]), -1) == null, "Negative packet registration accepted.");
        Check(LobbyBridge.ExtractControl(Frame((short)(id + 1), new byte[1]), id) == null, "Unrelated API packet forwarded.");
        foreach (int claimed in new[] { int.MinValue, -1, 0, 2, 4, int.MaxValue })
        {
            var mismatch = Frame(id, new byte[3]);
            Buffer.BlockCopy(BitConverter.GetBytes(claimed), 0, mismatch, 2, 4);
            Check(LobbyBridge.ExtractControl(mismatch, id) == null, "Inexact payload boundary accepted: " + claimed);
        }
        Check(LobbyBridge.ExtractControl(Frame(id, new byte[1018]), id)?.Length == 1018,
            "Maximum permitted control envelope rejected.");
        Check(LobbyBridge.ExtractControl(Frame(id, new byte[1019]), id) == null,
            "Oversized packet accepted.");
    }

    private static Platform_Multiplayer.MPLobbyMember Member(ulong id)
    {
        var member = (Platform_Multiplayer.MPLobbyMember)FormatterServices.GetUninitializedObject(
            typeof(Platform_Multiplayer.MPLobbyMember));
        member.id = new CSteamID(id);
        return member;
    }

    private static Platform_Multiplayer.MPLobby Lobby(params Platform_Multiplayer.MPLobbyMember[] members)
    {
        var lobby = (Platform_Multiplayer.MPLobby)FormatterServices.GetUninitializedObject(typeof(Platform_Multiplayer.MPLobby));
        lobby.members = members == null ? null : new List<Platform_Multiplayer.MPLobbyMember>(members);
        return lobby;
    }

    private static void Membership()
    {
        const ulong host = 76561198000000001, client = 76561198000000002, foreign = 76561198000000003;
        var expected = new[] { host, client };
        Check(LobbyBridge.MatchesHumans(Lobby(Member(client), Member(host)), expected, host),
            "Member order incorrectly affects the group handoff.");
        Check(LobbyBridge.MatchesHumans(Lobby(Member(host), Member(client)), expected, client),
            "Client cannot acknowledge the same group.");
        Check(!LobbyBridge.MatchesHumans(null, expected, host), "Missing lobby accepted.");
        Check(!LobbyBridge.MatchesHumans(Lobby(null), expected, host), "Missing member list accepted.");
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host)), expected, host), "Missing human accepted.");
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), Member(client), Member(foreign)), expected, host),
            "Foreign human accepted.");
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), Member(host)), expected, host), "Duplicate member accepted.");
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), null), expected, host), "Null member accepted.");
        var dummy = Member(client); dummy.dummyToBeKicked = true;
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), dummy), expected, host), "Departing dummy acknowledged.");
        var computer = Member(client); computer.SkirmishMember = true;
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), computer), expected, host), "Computer acknowledged as expected human.");
        foreach (var invalid in new[] { null, Array.Empty<ulong>(), new[] { host, host }, new[] { host, 0UL }, new[] { client } })
            Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), Member(client)), invalid, host),
                "Invalid expected transport roster accepted.");
        Check(!LobbyBridge.MatchesHumans(Lobby(Member(host), Member(client)), expected, foreign),
            "A process outside the requested group acknowledged it.");
    }
}
