using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MultiplayerRestartTest;

// Executes the production snapshot's validation/hash functions against the real game
// metadata, without constructing Unity views or invoking Steam. The capture/restore
// integration still requires game acceptance; no fake game implementation is substituted.
internal static class LaunchSnapshotTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string reason)
    {
        checks++;
        if (!value) throw new Exception(reason);
    }
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Any).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Any).Invoke(target, args);
    private static Platform_Multiplayer.MPLobbyMember Human(ulong id) =>
        new Platform_Multiplayer.MPLobbyMember { id = new Steamworks.CSteamID(id), colourID = 1 };
    private static LaunchSnapshot Seed()
    {
        var snapshot = (LaunchSnapshot)FormatterServices.GetUninitializedObject(typeof(LaunchSnapshot));
        Type member = typeof(LaunchSnapshot).GetNestedType("Member", BindingFlags.NonPublic);
        Array members = Array.CreateInstance(member, 3);
        for (int i = 0; i < 3; i++)
        {
            object record = Activator.CreateInstance(member, true);
            Set(record, "Id", i == 2 ? 9UL : 10001UL + (ulong)i);
            Set(record, "Ai", i == 2);
            Set(record, "Colour", i + 1);
            Set(record, "Lord", 0);
            Set(record, "Team", i == 2 ? 2 : 1);
            Set(record, "CustomName", string.Empty);
            Set(record, "HumanLordData", i == 2 ? string.Empty : "0");
            members.SetValue(record, i);
        }
        Set(snapshot, "members", members);
        Set(snapshot, "mapping", new ulong[] { 10001, 10002, 9, 0, 0, 0, 0, 0 });
        Set(snapshot, "aivs", Enumerable.Repeat("0:0:0:0", 7).ToArray());
        Set(snapshot, "mapHash", "original-hash");
        Set(snapshot, "<MapFileName>k__BackingField", "original.map");
        Set(snapshot, "<SetupText>k__BackingField", "original-settings");
        return snapshot;
    }
    public static int Main()
    {
        try
        {
            Run();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    internal static void Run()
    {
        RosterChecks(); SourceChecks(); DigestChecks(); CodecChecks();
        Console.WriteLine("PASS: " + checks + " production snapshot checks; no Unity/Steam calls.");
    }
    private static void RosterChecks()
    {
        LaunchSnapshot snapshot = Seed();
        var lobby = new Platform_Multiplayer.MPLobby();
        lobby.members.Add(Human(10002)); lobby.members.Add(Human(10001));
        var ai = new Platform_Multiplayer.MPLobbyMember(); ai.SetSkirmishPlayer(9UL); lobby.members.Add(ai);
        Check(snapshot.ValidateHumanRoster(lobby) == null, "Same humans in another enumeration order were rejected.");
        ulong[] view = snapshot.HumanIds; view[0] = 777;
        Check(snapshot.HumanIds[0] == 10001, "HumanIds exposed mutable stored identity.");
        lobby.members.RemoveAt(0);
        Check(snapshot.ValidateHumanRoster(lobby) != null, "Missing original human was accepted.");
        lobby.members.Add(Human(10003));
        Check(snapshot.ValidateHumanRoster(lobby) != null, "Replacement human was accepted.");
        lobby.members.Add(Human(10002));
        Check(snapshot.ValidateHumanRoster(lobby) != null, "Additional human was accepted.");
        lobby.members.RemoveAll(value => value.id.m_SteamID == 10003);
        lobby.members.Add(Human(10002));
        Check(snapshot.ValidateHumanRoster(lobby) != null, "Duplicate human identity was accepted.");
        Check(snapshot.ValidateHumanRoster(null) != null, "Missing lobby was accepted.");
    }
    private static void SourceChecks()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original map bytes");
            LaunchSnapshot snapshot = Seed();
            var map = (FileHeader)FormatterServices.GetUninitializedObject(typeof(FileHeader));
            map.filePath = path; map.fileName = "original.map"; map.crc = 42;
            Set(snapshot, "map", map); Set(snapshot, "mapPath", Path.GetFullPath(path));
            Set(snapshot, "<Crc>k__BackingField", 42);
            string hash = (string)typeof(LaunchSnapshot).GetMethod("HashFile", Any).Invoke(null, new object[] { path });
            Set(snapshot, "mapHash", hash);
            Check(snapshot.ValidateSource() == null, "Original map source was rejected.");
            File.WriteAllText(path, "changed map bytes");
            Check(snapshot.ValidateSource() != null, "Changed map bytes were accepted with the old CRC.");
            File.WriteAllText(path, "original map bytes");
            map.fileName = "other.map";
            Check(snapshot.ValidateSource() != null, "Changed source header identity was accepted.");
            map.fileName = "original.map"; map.crc++;
            Check(snapshot.ValidateSource() != null, "Changed map CRC was accepted.");
            map.crc = 42; File.Delete(path);
            Check(snapshot.ValidateSource() != null, "Missing original map was accepted.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private static void DigestChecks()
    {
        LaunchSnapshot first = Seed(), second = Seed();
        string original = (string)Call(first, "ComputeDigest");
        Set(second, "mapPath", "another-local-directory");
        Check(original == (string)Call(second, "ComputeDigest"), "Local directory leaked into common digest.");
        Set(second, "<SetupText>k__BackingField", "different-starting-rules");
        Check(original != (string)Call(second, "ComputeDigest"), "Changed rules retained the same digest.");
        second = Seed(); Set(second, "mapping", new ulong[] { 10002, 10001, 9, 0, 0, 0, 0, 0 });
        Check(original != (string)Call(second, "ComputeDigest"), "Swapped human slots retained the same digest.");
        second = Seed(); Set(second, "mapHash", "different-map-bytes");
        Check(original != (string)Call(second, "ComputeDigest"), "Different map content retained the same digest.");
        second = Seed(); Set(second, "aivs", Enumerable.Repeat("different-castle", 7).ToArray());
        Check(original != (string)Call(second, "ComputeDigest"), "Different AI castles retained the same digest.");
        second = Seed(); Set(second, "coopTrailProgress", new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        Check(original != (string)Call(second, "ComputeDigest"), "Different Coop progress retained the same digest.");
    }
    private static void CodecChecks()
    {
        var setup = new EngineInterface.MultiplayerSetupData();
        setup.starting_gamespeed = 42;
        setup.start_keep_location_order[0] = 3;
        setup.start_keep_location_order[1] = 0;
        setup.preferredAIVs[2] = -4;
        string encoded = setup.ToString();
        var decoded = new EngineInterface.MultiplayerSetupData();
        // Unity Mono implements String.Split(string, StringSplitOptions), while desktop
        // .NET Framework lacks this overload. Execute the real parser in an isolated
        // DMD clone, adapting only that BCL call to its equivalent array overload.
        // This neither patches the game nor substitutes the setup serialization logic.
        using (var parser = new DynamicMethodDefinition(typeof(EngineInterface.MultiplayerSetupData).GetMethod("FromString")))
        using (var il = new ILContext(parser.Definition))
        {
            int adapted = 0;
            foreach (var instruction in il.Body.Instructions)
            {
                if (!(instruction.Operand is MethodReference method) || method.DeclaringType.FullName != "System.String" ||
                    method.Name != "Split" || method.Parameters.Count != 2 ||
                    method.Parameters[0].ParameterType.FullName != "System.String" ||
                    method.Parameters[1].ParameterType.FullName != "System.StringSplitOptions") continue;
                instruction.OpCode = OpCodes.Call;
                instruction.Operand = il.Import(typeof(LaunchSnapshotTests).GetMethod(nameof(PortableSplit), Any));
                adapted++;
            }
            Check(adapted > 0, "The desktop BCL adapter did not match the actual Mono-specific parser call.");
            var parse = (Func<EngineInterface.MultiplayerSetupData, string, bool, bool>)parser.Generate()
                .CreateDelegate(typeof(Func<EngineInterface.MultiplayerSetupData, string, bool, bool>));
            parse(decoded, encoded, false);
        }
        Check(decoded.ToString() == encoded, "Actual Vanilla setup codec lost a launch setting.");
        Check(decoded.start_keep_location_order[0] == 3 && decoded.start_keep_location_order[1] == 0,
            "Actual Vanilla codec lost start-slot ordering.");
        Check(decoded.preferredAIVs[2] == -4, "Actual Vanilla codec lost castle rotation selection.");
    }
    private static string[] PortableSplit(string value, string separator, StringSplitOptions options) =>
        value.Split(new[] { separator }, options);
}
