using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using APIShared;
using APIShared.Networking;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MultiplayerRestartTest;

// Exercise the production ReceiveCore routing after the runtime adopted the target's
// new launch identity. Only packet-registration lookup and Apply's external effects
// are isolated; the real retained-receipt check and protocol Receive execute unchanged.
internal static class RuntimeContextTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string SourceSession = "original-lobby-session", SourceDigest = "original-launch-digest";
    private const string TargetSession = "replacement-lobby-session", TargetDigest = "replacement-launch-digest";
    private const short PacketId = 1234;
    private static readonly List<RestartStep> applied = new List<RestartStep>();
    private static readonly string operation = "11223344556677889900aabbccddeeff";
    private static int checks;

    private sealed class Lifecycle : IMissionLifecycleCapability
    {
        internal MissionContext Value;
        public MissionContext Current => Value;
        public bool TryRegisterObserver(string registrationId, Action<MissionLifecycleNotification> onStart,
            Action<MissionLifecycleNotification> onEnd, Action<MissionLifecycleNotification> onInitialization,
            out NativeCapabilityDiagnostic diagnostic)
        { diagnostic = null; throw new InvalidOperationException("This fixture never registers observers."); }
    }
    private static void Check(bool condition, string reason)
    { checks++; if (!condition) throw new Exception(reason); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
    private static short RegisteredPacket(object channel)
    {
        if (channel == null) throw new Exception("Packet registration fixture is missing.");
        return PacketId;
    }
    private static void ObserveApply(RestartRuntime runtime, RestartStep step)
    {
        if (runtime == null || step == null) throw new Exception("Unexpected Apply boundary.");
        applied.Add(step);
    }
    private static MissionContext Context(long session)
    {
        var result = (MissionContext)FormatterServices.GetUninitializedObject(typeof(MissionContext));
        Set(result, "<SessionId>k__BackingField", session);
        return result;
    }
    private static RestartRuntime Seed(RestartPhase phase, long currentMission = 22)
    {
        var runtime = (RestartRuntime)FormatterServices.GetUninitializedObject(typeof(RestartRuntime));
        var snapshot = (LaunchSnapshot)FormatterServices.GetUninitializedObject(typeof(LaunchSnapshot));
        Type type = typeof(LaunchSnapshot).GetNestedType("Member", BindingFlags.NonPublic);
        Array members = Array.CreateInstance(type, 3);
        for (int i = 0; i < members.Length; i++)
        {
            object record = Activator.CreateInstance(type, true);
            Set(record, "Id", 1001UL + (ulong)i); Set(record, "Ai", false);
            members.SetValue(record, i);
        }
        Set(snapshot, "members", members);
        // These stage inputs isolate runtime routing. Protocol state-machine transitions
        // that create the completed receipt are exercised separately by ProtocolTests.
        var protocol = new RestartProtocol(1002, 1001, new ulong[] { 1001, 1002, 1003 },
            SourceSession, SourceDigest, long.MaxValue);
        Set(protocol, "<Phase>k__BackingField", phase);
        Set(protocol, "<Operation>k__BackingField", operation);
        Set(protocol, "<Committed>k__BackingField", true);
        var target = new RestartTargetState(11);
        target.AuthorizeLaunch(); target.BindTarget(22);
        Set(runtime, "unityThread", Thread.CurrentThread.ManagedThreadId);
        Set(runtime, "snapshot", snapshot); Set(runtime, "protocol", protocol);
        Set(runtime, "targetState", target);
        Set(runtime, "lifecycle", new Lifecycle { Value = Context(currentMission) });
        Set(runtime, "messages", FormatterServices.GetUninitializedObject(typeof(MessageChannel<RestartWire>)));
        Set(runtime, "attemptPeers", new ulong[] { 1001, 1002, 1003 });
        Set(runtime, "session", TargetSession); Set(runtime, "digest", TargetDigest);
        Set(runtime, "self", 1002UL); Set(runtime, "host", 1001UL);
        Set(runtime, "retired", new HashSet<string>(StringComparer.Ordinal) { operation });
        return runtime;
    }
    private static NetworkMessage<RestartWire> Message(RestartMessageKind kind, ulong sender = 1001,
        string session = SourceSession, string digest = SourceDigest, string op = null)
    {
        var value = new RestartWire { PacketId = PacketId, Packet = new RestartPacket {
            Kind = kind, Session = session, Digest = digest, Operation = op ?? operation } };
        return (NetworkMessage<RestartWire>)Activator.CreateInstance(typeof(NetworkMessage<RestartWire>),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { value, sender }, null);
    }
    internal static void Run()
    {
        MethodInfo source = typeof(RestartRuntime).GetMethod("ReceiveCore", Any);
        using (var method = new DynamicMethodDefinition(source))
        using (var il = new ILContext(method.Definition))
        {
            int packetLookups = 0, effects = 0;
            foreach (var instruction in il.Body.Instructions)
            {
                if (!(instruction.Operand is MethodReference called)) continue;
                if (called.Name == "get_PacketId" && called.DeclaringType.FullName.StartsWith("APIShared.Networking.MessageChannel`1", StringComparison.Ordinal))
                {
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = il.Import(typeof(RuntimeContextTests).GetMethod(nameof(RegisteredPacket), Any));
                    packetLookups++;
                }
                else if (called.Name == "Apply" && called.DeclaringType.FullName == typeof(RestartRuntime).FullName)
                {
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = il.Import(typeof(RuntimeContextTests).GetMethod(nameof(ObserveApply), Any));
                    effects++;
                }
            }
            Check(packetLookups == 1 && effects == 1, "Actual runtime packet/effect boundaries changed.");
            var receive = (Action<RestartRuntime, NetworkMessage<RestartWire>>)method.Generate()
                .CreateDelegate(typeof(Action<RestartRuntime, NetworkMessage<RestartWire>>));

            var runtime = Seed(RestartPhase.Completed);
            applied.Clear(); receive(runtime, Message(RestartMessageKind.Complete));
            Check(applied.Count == 1 && applied[0].Accepted && applied[0].Outbound.Count == 1 &&
                applied[0].Outbound[0].Kind == RestartMessageKind.CompleteAck,
                "A delayed Complete was lost after promotion to the target launch identity.");
            Check((string)Get(runtime, "session") == TargetSession && (string)Get(runtime, "digest") == TargetDigest,
                "Receiving an old-operation completion rewrote the adopted target launch identity.");
            Check(((RestartProtocol)Get(runtime, "protocol")).Session == SourceSession,
                "The retained attempt identity was overwritten with the target identity.");

            applied.Clear(); receive(runtime, Message(RestartMessageKind.Abort));
            Check(applied.Count == 1 && applied[0].Accepted && (applied[0].Effects & RestartEffect.Cancelled) != 0,
                "A late authoritative Abort did not reach failure cleanup after target completion.");
            Check(((RestartProtocol)Get(runtime, "protocol")).Phase == RestartPhase.Failed,
                "The completed target remained successful after its host cancelled the retained attempt.");

            runtime = Seed(RestartPhase.Completed);
            applied.Clear(); receive(runtime, Message(RestartMessageKind.Abort, sender: 1003));
            Check(applied.Count == 1 && !applied[0].Accepted && ((RestartProtocol)Get(runtime, "protocol")).Phase == RestartPhase.Completed,
                "An ordinary client cancelled another client's completed target.");

            foreach (var bad in new[] {
                Message(RestartMessageKind.Abort, sender: 9999),
                Message(RestartMessageKind.Abort, session: "unrelated-session"),
                Message(RestartMessageKind.Abort, digest: "unrelated-digest"),
                Message(RestartMessageKind.Abort, op: "ffeeddccbbaa00998877665544332211") })
            {
                runtime = Seed(RestartPhase.Completed); applied.Clear(); receive(runtime, bad);
                Check(applied.Count == 0 && ((RestartProtocol)Get(runtime, "protocol")).Phase == RestartPhase.Completed,
                    "An unrelated peer/session/digest/operation reached the retained completion receipt.");
            }
            runtime = Seed(RestartPhase.Completed, currentMission: 33);
            applied.Clear(); receive(runtime, Message(RestartMessageKind.Abort));
            Check(applied.Count == 0, "An old completion receipt cancelled a newer unrelated mission.");

            runtime = Seed(RestartPhase.Starting);
            applied.Clear(); receive(runtime, Message(RestartMessageKind.Abort));
            Check(applied.Count == 1 && applied[0].Accepted && (applied[0].Effects & RestartEffect.Cancelled) != 0,
                "The active attempt lost its immutable identity while target capture was pending.");
        }
        Console.WriteLine("Actual runtime context routing passed: " + checks + " checks; registration/effect boundaries isolated.");
    }
}
