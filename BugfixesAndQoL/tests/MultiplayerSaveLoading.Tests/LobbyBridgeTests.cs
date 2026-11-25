using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using BugfixesAndQoL.Multiplayer.SaveLoading;
using Steamworks;

// Executes the production framing and lobby-membership decisions without starting
// Unity/Steam. Metadata validation uses the actual selected game/Extender assemblies.
internal static class LobbyBridgeTests
{
    private static int checks;
    private static bool fixtureCustomCoop, fixtureSkirmish;
    private static int publishedType, publishedPlayers, publications;
    private static Platform_Multiplayer fixturePlatform;
    private static FileHeader fixtureHeader;
    private static EngineInterface.MultiplayerSetupData fixtureSetup;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }

    internal static void Run()
    {
        LobbyBridge.Validate();
        Check(LobbyBridge.CreationResultMethod != null && !LobbyBridge.CreationResultMethod.IsStatic,
            "Native lobby completion observer contract is unavailable.");
        Framing();
        Membership();
        ReadyBeforeUiLoaded();
        GenericHostMode();
        AllSaveChecks();
        Console.WriteLine("LobbyBridge: " + checks + " checks passed.");
    }

    private static List<FileHeader> saveHeaders;
    private static MapFileManager SaveManager() => null;
    private static List<FileHeader> AllHeaders(MapFileManager manager, int sort, bool ascending, bool coopOnly)
    {
        Check(sort == 0 && ascending && !coopOnly, "Combined request filtered out a save category.");
        return saveHeaders;
    }
    private static void AllSaveChecks()
    {
        var sender = typeof(LobbyBridge).GetField("sendSaveCrc", PrivateStatic);
        object previous = sender.GetValue(null);
        var platform = (Platform_Multiplayer)FormatterServices.GetUninitializedObject(typeof(Platform_Multiplayer));
        try
        {
            var method = typeof(LobbyBridge).GetMethod("RequestAllSaveChecks", PrivateStatic);
            using (var fixture = new DynamicMethodDefinition(method))
            {
                // Replace filesystem discovery and transport only. Execute production
                // reset/sending order, category coverage and checksum construction.
                foreach (var instruction in fixture.Definition.Body.Instructions)
                {
                    if (!(instruction.Operand is MethodReference called) || called.DeclaringType.Name != "MapFileManager") continue;
                    string replacement = called.Name == "get_Instance" ? nameof(SaveManager) : called.Name == "GetMPSaves" ? nameof(AllHeaders) : null;
                    Check(replacement != null, "Unexpected save discovery boundary.");
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = fixture.Definition.Module.ImportReference(typeof(LobbyBridgeTests).GetMethod(replacement, PrivateStatic));
                }
                MethodInfo run = fixture.Generate();
                foreach (bool reverse in new[] { false, true })
                {
                    var normal = new FileHeader { fileName = "ordinary.msv", coopTrailID = 0, xPlaySaveChecksum = 123, xPlaySaveTime = 97, retrieveCRCChecks = 7 };
                    var coop = new FileHeader { fileName = "coop.msv", coopTrailID = 1, xPlaySaveChecksum = 456, xPlaySaveTime = 82, retrieveCRCChecks = 8 };
                    saveHeaders = reverse ? new List<FileHeader> { coop, normal } : new List<FileHeader> { normal, coop };
                    int sent = 0;
                    sender.SetValue(null, (Action<Platform_Multiplayer, string, int>)((target, name, checksum) =>
                    {
                        Check(ReferenceEquals(target, platform), "Save check changed its transport owner.");
                        Check(saveHeaders.All(header => header.retrieveCRCChecks == (saveHeaders.IndexOf(header) < sent ? 1 : 0)),
                            "Reset order erased a reply or retained stale acknowledgements.");
                        FileHeader expected = saveHeaders[sent++];
                        Check(name == expected.fileName && checksum == (expected.xPlaySaveChecksum ^ expected.xPlaySaveTime), "Vanilla checksum/name changed.");
                        expected.retrieveCRCChecks = 1; // Synchronous reply must survive all later sends.
                    }));
                    run.Invoke(null, new object[] { platform });
                    Check(sent == 2 && saveHeaders.All(header => header.retrieveCRCChecks == 1), "Both save kinds were not checked exactly once.");
                }
                saveHeaders.Clear();
                run.Invoke(null, new object[] { platform });
            }
        }
        finally { sender.SetValue(null, previous); saveHeaders = null; }
    }

    private static void GenericHostMode()
    {
        var target = typeof(LobbyBridge).GetMethod("OpenHost", PrivateStatic);
        using (var prefix = new DynamicMethodDefinition(target))
        {
            var body = prefix.Definition.Body;
            var reset = body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Stsfld &&
                instruction.Operand is FieldReference field && field.DeclaringType.Name == "FRONT_Multiplayer" && field.Name == "customCoopGame");
            // Execute the actual production prefix including all host/lobby validation.
            // Only the external parked-worker receipt is bypassed. Stop before UI/Steam
            // startup and redirect the static field to avoid FRONT's Noesis type initializer.
            var receipt = body.Instructions.Single(instruction => instruction.Operand is MethodReference method &&
                method.DeclaringType.Name == nameof(LobbyBridge) && method.Name == "RequireParked");
            receipt.OpCode = OpCodes.Nop; receipt.Operand = null;
            reset.Operand = prefix.Definition.Module.ImportReference(typeof(LobbyBridgeTests).GetField(nameof(fixtureCustomCoop), PrivateStatic));
            int end = body.Instructions.IndexOf(reset) + 1;
            while (body.Instructions.Count > end) body.Instructions.RemoveAt(end);
            body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            MethodInfo run = prefix.Generate();
            var host = Lobby(Member(76561198000000001));
            host.id = new CSteamID(77); host.isHost = true;
            fixtureCustomCoop = true;
            PublishGenericMetadata(1, 2); // Negative control: reproduce Vanilla's stale-Coop metadata.
            foreach (bool priorCoop in new[] { true, false })
            {
                fixtureCustomCoop = priorCoop;
                run.Invoke(null, new object[] { host });
                Check(!fixtureCustomCoop, "Generic host retained the previous Custom-Coop mode.");
                PublishGenericMetadata(0, 8);
            }
            var client = Lobby(Member(76561198000000001)); client.id = new CSteamID(77);
            var missingId = Lobby(Member(76561198000000001)); missingId.isHost = true;
            foreach (var invalid in new[] { null, client, missingId })
            {
                fixtureCustomCoop = true; bool rejected = false;
                try { run.Invoke(null, new object[] { invalid }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                Check(rejected && fixtureCustomCoop, "Invalid host input changed the previous lobby mode.");
            }
        }
    }

    private static Platform_Multiplayer MetadataPlatform() => fixturePlatform;
    private static FileHeader MetadataHeader(object front) => fixtureHeader;
    private static EngineInterface.MultiplayerSetupData MetadataSetup(object front) => fixtureSetup;
    private static int MetadataPlayerCap(object front) => 8;
    private static string MetadataHostName(object front) => "fixture";
    private static CrusaderDE.FRONT_Multiplayer.MPAIVInfo[] MetadataAivs(object front) => null;
    private static void MetadataDelay(object front, DateTime value) { }
    private static void CapturePublication(Platform_Multiplayer platform, string gameName, string mapName,
        string fileName, int maxPlayers, int type, string settings, int crc, CrusaderDE.FRONT_Multiplayer.MPAIVInfo[] aivs)
    { publications++; publishedPlayers = maxPlayers; publishedType = type; }

    private static void PublishGenericMetadata(int expectedType, int expectedPlayers)
    {
        var frontType = typeof(CrusaderDE.FRONT_Multiplayer);
        fixturePlatform = (Platform_Multiplayer)FormatterServices.GetUninitializedObject(typeof(Platform_Multiplayer));
        fixtureHeader = new FileHeader { maxPlayers = 8, fileName = "fixture", display_filename = "fixture" };
        fixtureSetup = new EngineInterface.MultiplayerSetupData();
        var instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var target = frontType.GetMethod("UpdateHostInfo", instance);
        using (var publication = new DynamicMethodDefinition(target))
        {
            var body = publication.Definition.Body;
            // This closed managed segment is the real Vanilla publication logic.
            // Remove only native starting-data/keep-order setup before the mode branch;
            // replace the Steam sink and neutral metadata inputs, never the type/max
            // algorithm. No FRONT instance is constructed (its cctor creates Noesis brushes).
            var entry = body.Instructions.First(instruction => instruction.OpCode == OpCodes.Ldsfld &&
                instruction.Operand is FieldReference field && field.DeclaringType.Name == "FRONT_Multiplayer" && field.Name == "skirmishGame");
            while (!ReferenceEquals(body.Instructions[0], entry)) body.Instructions.RemoveAt(0);
            foreach (var instruction in body.Instructions)
            {
                if (instruction.Operand is FieldReference field && field.DeclaringType.Name == "FRONT_Multiplayer" &&
                    (field.Name == "customCoopGame" || field.Name == "skirmishGame"))
                    instruction.Operand = publication.Definition.Module.ImportReference(typeof(LobbyBridgeTests).GetField(
                        field.Name == "customCoopGame" ? nameof(fixtureCustomCoop) : nameof(fixtureSkirmish), PrivateStatic));
                if (instruction.Operand is FieldReference input && input.DeclaringType.Name == "FRONT_Multiplayer")
                {
                    string adapter = input.Name == "selectedMPHeader" ? nameof(MetadataHeader) :
                        input.Name == "MPsetupData" ? nameof(MetadataSetup) :
                        input.Name == "PlayerCap" ? nameof(MetadataPlayerCap) :
                        input.Name == "MPHostLobbyname" ? nameof(MetadataHostName) :
                        input.Name == "AIVs" ? nameof(MetadataAivs) :
                        input.Name == "delayedSendDataToLobby" ? nameof(MetadataDelay) : null;
                    if (adapter != null)
                    {
                        Check(instruction.OpCode == (adapter == nameof(MetadataDelay) ? OpCodes.Stfld : OpCodes.Ldfld),
                            "Vanilla metadata input access changed: " + input.Name);
                        instruction.OpCode = OpCodes.Call;
                        instruction.Operand = publication.Definition.Module.ImportReference(typeof(LobbyBridgeTests).GetMethod(adapter, PrivateStatic));
                    }
                }
                if (!(instruction.Operand is MethodReference method) || method.DeclaringType.Name != "Platform_Multiplayer") continue;
                if (method.Name == "get_Instance" || method.Name == "UpdateHostLobbyInfo")
                {
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = publication.Definition.Module.ImportReference(typeof(LobbyBridgeTests).GetMethod(
                        method.Name == "get_Instance" ? nameof(MetadataPlatform) : nameof(CapturePublication), PrivateStatic));
                }
            }
            fixtureSkirmish = false; publications = 0;
            publication.Generate().Invoke(null, new object[] { null, false });
            Check(publications == 1 && publishedType == expectedType && publishedPlayers == expectedPlayers,
                "Vanilla published unexpected type/max-player metadata for the current lobby mode.");
        }
        fixturePlatform = null; fixtureHeader = null; fixtureSetup = null;
    }

    private static void ReadyBeforeUiLoaded()
    {
        foreach (string name in new[] { "TryReady", "HasModal", "HasSaveRequester" })
        {
            var target = typeof(LobbyBridge).GetMethod(name,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            using (var method = new DynamicMethodDefinition(target))
            {
                // Replace only the external load-state read. Execute the production
                // early return: any premature singleton/Steam/UI access still fails here.
                var reads = method.Definition.Body.Instructions.Where(instruction =>
                    instruction.OpCode == OpCodes.Ldsfld && instruction.Operand is FieldReference field &&
                    field.DeclaringType.Name == "MainViewModel" && field.Name == "viewModelLoaded").ToArray();
                Check(reads.Length == 1, name + " no longer has its explicit UI-load guard.");
                reads[0].OpCode = OpCodes.Ldc_I4_0;
                reads[0].Operand = null;
                object result = method.Generate().Invoke(null,
                    name == "TryReady" ? new object[] { 0UL, 0UL, null, 0UL } : Array.Empty<object>());
                Check(name == "TryReady" ? (LocalReadyResult)result == LocalReadyResult.NotCurrent : !(bool)result,
                    name + " accessed or reported unloaded UI.");
            }
        }
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
