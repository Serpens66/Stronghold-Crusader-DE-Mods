using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using MultiplayerRestartTest;

internal static class RestartTestBootstrap
{
    public static int Main(string[] args)
    {
        string game = Path.GetFullPath(args[0]);
        string api = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(game,"BepInEx/plugins/APIShared_Serp");
        string[] roots = { Path.Combine(game,"BepInEx/core"), api,
            Path.Combine(game,"BepInEx/plugins/000shcdese"), Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed") };
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            string file = new AssemblyName(e.Name).Name + ".dll";
            foreach (string root in roots) { string path = Path.Combine(root,file); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        try { return Run(game); } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Run(string game)
    {
        int result = BarrierTests.Main(new[] { Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll") });
        if (result != 0) return result;
        WireTests.Run(); LaunchSnapshotTests.Run(); SettingsFingerprintTests.Run(); LocaleTests.Run(); TransportTests.Run(); TargetReadinessTests.Run(); VanillaBridge.Validate();
        Console.WriteLine("Actual runtime private-member contracts passed.");
        StopBarrier.InstallEarly(null);
        if (!StopBarrier.Installed) throw new Exception("Actual early loop/Director hooks failed to publish.");
        Console.WriteLine("Actual early hook installation passed in desktop fixture (no game thread executed).");
        RuntimeReadyTests.Run();
        RuntimeContextTests.Run();
        RuntimeParkingTests.Run();
        RuntimeFailureTests.Run();
        return 0;
    }
}
internal static class WireTests
{
    private static int checks;
    private static void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
    internal static void Run()
    {
        foreach (RestartMessageKind kind in Enum.GetValues(typeof(RestartMessageKind)))
        {
            var packet = new RestartWire { PacketId = 1234, Packet = new RestartPacket { Kind = kind,
                Session = "123:" + new string('A',64), Operation = Guid.NewGuid().ToString("N"), Digest = new string('B',64),
                LobbyId = kind == RestartMessageKind.Lobby ? 12345UL : 0 } };
            var bytes = MessagePackSerializer.Serialize(packet);
            Check(bytes[0] == 0x98 && bytes[1] == RestartWire.WireVersion, "Unexpected wire version framing.");
            var oldVersion = (byte[])bytes.Clone(); oldVersion[1] = 1;
            bool oldRejected = false;
            try { MessagePackSerializer.Deserialize<RestartWire>(oldVersion); }
            catch (MessagePackSerializationException) { oldRejected = true; }
            Check(oldRejected, "Protocol v1 accepted by v2 formatter.");
            var copy = MessagePackSerializer.Deserialize<RestartWire>(bytes);
            Check(copy.Packet.Kind == kind && copy.Packet.Session == packet.Packet.Session && copy.Packet.Operation == packet.Packet.Operation &&
                copy.Packet.Digest == packet.Packet.Digest && copy.Packet.LobbyId == packet.Packet.LobbyId && copy.PacketId == packet.PacketId,
                "Actual MessagePack roundtrip lost restart fields.");
            Check(!ReferenceEquals(copy, packet) && !ReferenceEquals(copy.Packet,packet.Packet), "Mutable wire alias.");
            bool rejected = false;
            try { MessagePackSerializer.Deserialize<RestartWire>(bytes.Concat(new byte[] { 0 }).ToArray()); }
            catch (MessagePackSerializationException) { rejected = true; }
            Check(rejected, "Trailing bytes accepted.");
            packet.Packet.Operation = "stale";
            rejected = false; try { MessagePackSerializer.Serialize(packet); } catch (MessagePackSerializationException) { rejected = true; }
            Check(rejected,"Invalid operation serialized.");
        }
        var hello = new RestartWire { Control = 1, PacketId = 1234, Packet = new RestartPacket {
            Kind = RestartMessageKind.Prepare, Session = "old-lobby", Operation = "", Digest = new string('C',64) } };
        Check(MessagePackSerializer.Deserialize<RestartWire>(MessagePackSerializer.Serialize(hello)).Control == 1, "Capability hello failed.");
        var detached = hello.Copy(); hello.Packet.Digest = new string('D',64);
        Check(detached.Packet.Digest == new string('C',64), "Raw receive snapshot is not detached.");
        for (int length = 0; length < 6; length++) Check(VanillaBridge.ExtractRestart(new byte[length], 1234) == null, "Short transport frame accepted.");
        byte[] framed = new byte[9]; Buffer.BlockCopy(BitConverter.GetBytes((short)1234),0,framed,0,2);
        Buffer.BlockCopy(BitConverter.GetBytes(3),0,framed,2,4); framed[6] = 1;
        Check(VanillaBridge.ExtractRestart(framed,1234).SequenceEqual(new byte[] {1,0,0}), "Valid transport framing lost.");
        Check(VanillaBridge.ExtractRestart(framed,1235) == null, "Foreign packet forwarded.");
        foreach (int length in new[] {-1,0,2,4,int.MaxValue}) {
            Buffer.BlockCopy(BitConverter.GetBytes(length),0,framed,2,4);
            Check(VanillaBridge.ExtractRestart(framed,1234) == null, "Invalid declared transport length accepted.");
        }
        Console.WriteLine("Actual MessagePack fixture passed: " + checks + " checks.");
    }
}
