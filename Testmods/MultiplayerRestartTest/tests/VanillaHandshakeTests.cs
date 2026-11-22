using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Threading;
using CrusaderDE;
using SHCDESE.API;
using Steamworks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MultiplayerRestartTest;

// Actual Vanilla UpdateMultiplayer, processMessage, monitorHostGameStart and
// startSimThread bodies. Steam delivery, native calls and presentation are the
// external boundaries. A parked worker is real; no native/game thread is started.
internal static class VanillaHandshakeTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private sealed class Peer
    {
        internal string Name;
        internal Director Director;
        internal Platform_Multiplayer Multiplayer;
        internal Platform_Multiplayer.MPGameMember Remote;
        internal readonly Queue<Platform_Multiplayer.MPData> Inbox = new Queue<Platform_Multiplayer.MPData>();
        internal readonly List<short> Sent = new List<short>();
        internal int Seed, PostLoads, Starts, Synced, Receives;
    }
    private static Peer current;
    private static Director currentDirector;
    private static Platform_Multiplayer currentMultiplayer;
    private static EditorDirector currentEditor;
    private static MainViewModel currentView;
    private static AchievementsCommon currentAchievements;
    private static int[] colours = new int[9];
    private static readonly List<DynamicMethodDefinition> definitions = new List<DynamicMethodDefinition>();
    private static Action<Director, bool> update;
    private static Action<Director> originalStart;
    private static Action<Platform_Multiplayer> monitor;
    private static Func<Platform_Multiplayer, Platform_Multiplayer.MPData, Platform_Multiplayer.MPGameMember, bool, bool> process;
    private static int checks;
    private static IntPtr lobbyMessage;
    private static bool lobbyPending;
    private static int customPackets;
    private static Func<Platform_Multiplayer.MPData, bool, bool> prefix;
    private static int failures, finalSends;
    private static bool senderThrows;
    private static Platform_Multiplayer.MPData finalPacket;
    private static bool finalInstant;
    private static ulong visibleHost = 1001;

    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    private static void Rejects(Action action, string reason)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, reason);
    }
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Any).SetValue(instance, value);
    private static object Get(object instance, string name) => instance.GetType().GetField(name, Any).GetValue(instance);
    private static MethodInfo Method(Type type, string name) => type.GetMethods(Any).Single(m => m.Name == name);
    private static MethodInfo Own(string name) => Method(typeof(VanillaHandshakeTests), name);
    private static int ReadInt32(byte[] value) => BitConverter.ToInt32(value, 0);
    private static ulong ReadUInt64(byte[] value) => BitConverter.ToUInt64(value, 0);
    private static FieldInfo Barrier(string name) => typeof(StopBarrier).GetField(name, Any);
    private static object Empty(Type type) => FormatterServices.GetUninitializedObject(type);
    private static void Select(Peer peer)
    {
        current = peer; currentDirector = peer.Director; currentMultiplayer = peer.Multiplayer;
    }
    private static Peer Make(string name, bool host)
    {
        var peer = new Peer { Name = name, Director = (Director)Empty(typeof(Director)),
            Multiplayer = (Platform_Multiplayer)Empty(typeof(Platform_Multiplayer)),
            Remote = new Platform_Multiplayer.MPGameMember { steamID = 1002, isHost = !host } };
        Set(peer.Director, "multiplayerGame", true);
        Set(peer.Multiplayer, "IsHost", host);
        Set(peer.Multiplayer, "newGameNotLoad", true);
        Set(peer.Multiplayer, "gameMembers", new List<Platform_Multiplayer.MPGameMember> { peer.Remote });
        Set(peer.Multiplayer, "UIColourRemap", new int[9]);
        Set(peer.Multiplayer, "mapLoaded", true);
        Set(peer.Multiplayer, "monitoringForGameStart", host);
        Set(peer.Multiplayer, "monitoringForGameStartTime", DateTime.UtcNow);
        return peer;
    }
    private static Platform_Multiplayer.MPData Packet(short type, byte[] body = null) =>
        new Platform_Multiplayer.MPData { packetType = type, data = body ?? new byte[0], dataLength = body?.Length ?? 0 };

    private static void Receive(Platform_Multiplayer multiplayer, bool fromThread)
    {
        Check(ReferenceEquals(multiplayer, current.Multiplayer), "Receive crossed peer contexts.");
        current.Receives++;
        while (current.Inbox.Count != 0)
            Check(process(multiplayer, current.Inbox.Dequeue(), current.Remote, fromThread), "Main-thread handshake packet was deferred.");
    }
    private static void Monitor(Platform_Multiplayer multiplayer) => monitor(multiplayer);
    private static void Boundary(string name, object[] args)
    {
        switch (name)
        {
            case "SetMPRandSeed": current.Seed = (int)args[0]; break;
            case "postLoading": current.PostLoads++; break;
            case "SendEmptyPacketTypeToAll": current.Sent.Add(Convert.ToInt16(args[1])); break;
            case "startSimThread":
                // Execute the production admission prefix and actual Vanilla body.
                if ((bool)Method(typeof(StopBarrier), "Start").Invoke(null, null))
                { originalStart((Director)args[0]); current.Starts++; }
                break;
            case "StartMultiplayerGameSynced": current.Synced++; break;
        }
    }
    private static FieldInfo InstanceField(Type type)
    {
        if (type == typeof(Director)) return typeof(VanillaHandshakeTests).GetField(nameof(currentDirector), Any);
        if (type == typeof(Platform_Multiplayer)) return typeof(VanillaHandshakeTests).GetField(nameof(currentMultiplayer), Any);
        if (type == typeof(EditorDirector)) return typeof(VanillaHandshakeTests).GetField(nameof(currentEditor), Any);
        if (type == typeof(MainViewModel)) return typeof(VanillaHandshakeTests).GetField(nameof(currentView), Any);
        if (type == typeof(AchievementsCommon)) return typeof(VanillaHandshakeTests).GetField(nameof(currentAchievements), Any);
        return null;
    }
    private static MethodInfo Resolve(MethodReference reference)
    {
        string name = reference.DeclaringType.FullName.Replace('/', '+');
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
        if (type == null && reference.DeclaringType.Scope is AssemblyNameReference scope)
            type = Assembly.Load(scope.Name).GetType(name, true);
        if (type == null) throw new TypeLoadException(name);
        return type.GetMethods(Any).Single(m => m.Name == reference.Name && m.GetParameters().Length == reference.Parameters.Count &&
            m.GetParameters().Select(p => p.ParameterType.FullName.Replace('+', '/')).SequenceEqual(reference.Parameters.Select(p => p.ParameterType.FullName)));
    }
    private static MethodInfo Stub(MethodInfo method)
    {
        Type[] parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        if (!method.IsStatic) parameters = new[] { method.DeclaringType }.Concat(parameters).ToArray();
        var definition = new DynamicMethodDefinition("HandshakeBoundary_" + definitions.Count, method.ReturnType, parameters);
        definitions.Add(definition);
        using (var il = new ILContext(definition.Definition))
        {
            var cursor = new ILCursor(il);
            FieldInfo instance = method.Name == "get_Instance" ? InstanceField(method.ReturnType) : null;
            if (instance != null) cursor.Emit(OpCodes.Ldsfld, instance);
            else
            {
                cursor.Emit(OpCodes.Ldstr, method.Name);
                cursor.Emit(OpCodes.Ldc_I4, parameters.Length); cursor.Emit(OpCodes.Newarr, typeof(object));
                for (int i = 0; i < parameters.Length; i++)
                {
                    cursor.Emit(OpCodes.Dup); cursor.Emit(OpCodes.Ldc_I4, i);
                    if (parameters[i].IsByRef || parameters[i].IsPointer) cursor.Emit(OpCodes.Ldnull);
                    else
                    {
                        cursor.Emit(OpCodes.Ldarg, i);
                        if (parameters[i].IsValueType) cursor.Emit(OpCodes.Box, parameters[i]);
                    }
                    cursor.Emit(OpCodes.Stelem_Ref);
                }
                cursor.Emit(OpCodes.Call, Own(nameof(Boundary)));
                if (method.ReturnType != typeof(void))
                {
                    if (!method.ReturnType.IsValueType) cursor.Emit(OpCodes.Ldnull);
                    else
                    {
                        var local = new VariableDefinition(il.Import(method.ReturnType));
                        il.Body.Variables.Add(local); il.Body.InitLocals = true;
                        cursor.Emit(OpCodes.Ldloca, local); cursor.Emit(OpCodes.Initobj, method.ReturnType); cursor.Emit(OpCodes.Ldloc, local);
                    }
                }
            }
            cursor.Emit(OpCodes.Ret);
        }
        return definition.Generate();
    }
    private static MethodInfo Clone(Type type, string name, bool isolate)
    {
        MethodInfo original = Method(type, name);
        Type[] parameters = original.GetParameters().Select(p => p.ParameterType).ToArray();
        if (!original.IsStatic) parameters = new[] { type }.Concat(parameters).ToArray();
        var definition = new DynamicMethodDefinition("HandshakeActual_" + name, original.ReturnType, parameters);
        definitions.Add(definition);
        // Read the canonical IL without reflection resolving Mono's ReadOnlySpan
        // overloads against desktop .NET Framework's older mscorlib.
        using (var source = AssemblyDefinition.ReadAssembly(type.Assembly.Location))
        {
            MethodDefinition method = source.MainModule.GetType(type.FullName.Replace('+', '/')).Methods.Single(m => m.MetadataToken.ToInt32() == original.MetadataToken);
            definition.Definition.Body = method.Body.Clone(definition.Definition);
            foreach (var instruction in definition.Definition.Body.Instructions)
            {
                if (instruction.Operand is ParameterDefinition parameter)
                    instruction.Operand = definition.Definition.Parameters[parameter.Index + 1];
                if (!(instruction.Operand is MethodReference called)) continue;
                if (called.DeclaringType.FullName.StartsWith("System.ReadOnlySpan`1", StringComparison.Ordinal) && called.Name == "op_Implicit")
                { instruction.OpCode = OpCodes.Nop; instruction.Operand = null; }
                else if (called.DeclaringType.FullName == "System.BitConverter" && called.Name == "ToInt32" &&
                    called.Parameters.Count == 1 && called.Parameters[0].ParameterType.FullName.StartsWith("System.ReadOnlySpan`1", StringComparison.Ordinal))
                    instruction.Operand = definition.Definition.Module.ImportReference(Own(nameof(ReadInt32)));
                else if (called.DeclaringType.FullName == "System.BitConverter" && called.Name == "ToUInt64" &&
                    called.Parameters.Count == 1 && called.Parameters[0].ParameterType.FullName.StartsWith("System.ReadOnlySpan`1", StringComparison.Ordinal))
                    instruction.Operand = definition.Definition.Module.ImportReference(Own(nameof(ReadUInt64)));
            }
        }
        if (!isolate) return definition.Generate();
        using (var il = new ILContext(definition.Definition))
        {
            foreach (var instruction in il.Body.Instructions.ToArray())
            {
                if (instruction.OpCode == OpCodes.Ldsfld && instruction.Operand is FieldReference field)
                {
                    Type owner = typeof(Director).Assembly.GetType(field.DeclaringType.FullName.Replace('/', '+'));
                    FieldInfo replacement = owner == null ? null : InstanceField(owner);
                    if (field.Name == "instance" && replacement != null) instruction.Operand = il.Import(replacement);
                    else if (field.Name == "UIColourRemap" || field.Name == "remapColours")
                        instruction.Operand = il.Import(typeof(VanillaHandshakeTests).GetField(nameof(colours), Any));
                }
                if ((instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt) || !(instruction.Operand is MethodReference called)) continue;
                if (called.DeclaringType.FullName.StartsWith("System.", StringComparison.Ordinal)) continue;
                if (called.DeclaringType.FullName == typeof(VanillaHandshakeTests).FullName) continue;
                // Actual simple admission getters stay intact; all native/UI/Steam effects are isolated.
                if (called.DeclaringType.FullName == "Director" && called.Name == "get_SimRunning") continue;
                if (called.DeclaringType.FullName == "Platform_Multiplayer" && called.Name == "get_IsHost") continue;
                MethodInfo target;
                if (called.Name == "ReceiveGameMessages") target = Own(nameof(Receive));
                else if (called.Name == "monitorHostGameStart") target = Own(nameof(Monitor));
                else target = Stub(Resolve(called));
                instruction.OpCode = OpCodes.Call; instruction.Operand = il.Import(target);
            }
        }
        return definition.Generate();
    }
    private static void Pump(Peer peer)
    {
        Select(peer);
        object[] args = { Method(typeof(Director), "UpdateMultiplayer"), false };
        bool entered = (bool)Method(typeof(StopBarrier), "Enter").Invoke(null, args);
        try { if (entered) update(peer.Director, true); }
        finally { Method(typeof(StopBarrier), "Leave").Invoke(null, new object[] { null, args[1] }); }
    }
    private static Thread Park()
    {
        StopBarrier.Release(); Barrier("requested").SetValue(null, 1);
        var worker = new Thread(() => Method(typeof(StopBarrier), "Checkpoint").Invoke(null, null));
        worker.IsBackground = true; worker.Start();
        Check(SpinWait.SpinUntil(() => StopBarrier.Quiescent, 3000), "Handshake worker did not park.");
        return worker;
    }
    private static int ReceiveLobbyChannel(int channel, IntPtr[] messages, int capacity)
    {
        Check(channel == 2 && capacity > 0, "Actual SE lobby channel changed.");
        if (!lobbyPending) return 0;
        lobbyPending = false; messages[0] = lobbyMessage; return 1;
    }
    private static void ReleaseLobbyMessage(IntPtr value) { Check(value == lobbyMessage, "SE released an unknown message."); }
    private static CSteamID LobbySender(ref SteamNetworkingIdentity identity) => new CSteamID(1002);
    private static GameNetworkAPI NetworkInstance() => null;
    private static GameXAMLManagerAPI XamlInstance() => null;
    private static bool CustomPacket(GameNetworkAPI instance, short id, byte[] body, CSteamID? sender)
    { customPackets++; return true; }
    private static void FlushXaml(GameXAMLManagerAPI instance) { }
    private static object OriginalLobby(object detour) => null;
    private static void OriginalLobbyCall(object callback, Platform_Multiplayer multiplayer) { }
    private static void LobbyError(Exception error, string message, string method, string file)
    { throw new InvalidOperationException("Actual SE lobby receiver fixture failed: " + message, error); }
    private static void LobbyReceiverDrop()
    {
        Type manager = typeof(GameNetworkAPI).Assembly.GetType("SHCDESE.ManagedHooks.ManagedHookManager", true);
        MethodInfo source = Method(manager, "Platform_Multiplayer_ReceiveLobbyMessages_Hook");
        MethodInfo parser = Clone(typeof(Platform_Multiplayer.MPData), "FromBytes", false);
        using (var definition = new DynamicMethodDefinition(source))
        using (var il = new ILContext(definition.Definition))
        {
            int transportBoundaries = 0, parserBoundaries = 0;
            foreach (var instruction in il.Body.Instructions.ToArray())
            {
                if (instruction.OpCode == OpCodes.Ldsfld && instruction.Operand is FieldReference field &&
                    field.DeclaringType.FullName == "Platform_Multiplayer" && field.Name == "instance")
                    instruction.Operand = il.Import(typeof(VanillaHandshakeTests).GetField(nameof(currentMultiplayer), Any));
                if (!(instruction.Operand is MethodReference call) ||
                    (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)) continue;
                MethodInfo replacement = null;
                if (call.DeclaringType.FullName == "Steamworks.SteamNetworkingMessages" && call.Name == "ReceiveMessagesOnChannel")
                { replacement = Own(nameof(ReceiveLobbyChannel)); transportBoundaries++; }
                else if (call.DeclaringType.FullName == "Steamworks.SteamNetworkingMessage_t" && call.Name == "Release")
                    replacement = Own(nameof(ReleaseLobbyMessage));
                else if (call.DeclaringType.FullName == "Steamworks.SteamNetworkingIdentity" && call.Name == "GetSteamID")
                    replacement = Own(nameof(LobbySender));
                else if (call.DeclaringType.FullName == "Platform_Multiplayer/MPData" && call.Name == "FromBytes")
                { replacement = parser; parserBoundaries++; }
                else if (call.DeclaringType.FullName == typeof(GameNetworkAPI).FullName && call.Name == "get_Instance")
                    replacement = Own(nameof(NetworkInstance));
                else if (call.DeclaringType.FullName == typeof(GameNetworkAPI).FullName && call.Name == "HandleRawPacket")
                    replacement = Own(nameof(CustomPacket));
                else if (call.DeclaringType.FullName == typeof(GameXAMLManagerAPI).FullName && call.Name == "get_Instance")
                    replacement = Own(nameof(XamlInstance));
                else if (call.DeclaringType.FullName == typeof(GameXAMLManagerAPI).FullName && call.Name == "FlushDeferredPerPlayerUpdates")
                    replacement = Own(nameof(FlushXaml));
                else if (call.Name == "get_Trampoline") replacement = Own(nameof(OriginalLobby));
                else if (call.DeclaringType.FullName == "SHCDESE.Logging.LogHelper" && call.Name == "Error")
                    replacement = Own(nameof(LobbyError));
                else if (call.DeclaringType.FullName.Contains("platform_Multiplayer_ReceiveLobbyMessagesDelegate") && call.Name == "Invoke")
                    replacement = Own(nameof(OriginalLobbyCall));
                if (replacement == null) continue;
                instruction.OpCode = OpCodes.Call; instruction.Operand = il.Import(replacement);
            }
            Check(transportBoundaries == 1 && parserBoundaries == 1, "Actual SE receiver/parser boundaries changed.");
            MethodInfo receiver = definition.Generate();
            Select(Make("lobby-client", false));
            Set(currentMultiplayer, "activeLobby", Empty(typeof(Platform_Multiplayer.MPLobby)));
            object hookOwner = Empty(manager);
            foreach (short packetType in new[] { (short)Enums.MPFlags.InitialSeedPacket, (short)30000 })
            {
                byte[] raw = new byte[10];
                Buffer.BlockCopy(BitConverter.GetBytes(packetType), 0, raw, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(4), 0, raw, 2, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(123456), 0, raw, 6, 4);
                IntPtr payload = Marshal.AllocHGlobal(raw.Length);
                lobbyMessage = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SteamNetworkingMessage_t)));
                try
                {
                    Marshal.Copy(raw, 0, payload, raw.Length);
                    var message = new SteamNetworkingMessage_t { m_pData = payload, m_cbSize = raw.Length };
                    Marshal.StructureToPtr(message, lobbyMessage, false);
                    customPackets = 0; lobbyPending = true;
                    receiver.Invoke(null, new[] { hookOwner, currentMultiplayer });
                    Check(!lobbyPending && customPackets == (packetType == 30000 ? 1 : 0),
                        "Actual SE lobby receiver did not distinguish discarded Vanilla seed from custom control packet.");
                    Check(ReceiveLobbyChannel(2, new IntPtr[1], 1) == 0, "Discarded seed remained available to gameplay receiver.");
                }
                finally { Marshal.FreeHGlobal(lobbyMessage); Marshal.FreeHGlobal(payload); lobbyMessage = IntPtr.Zero; }
            }
        }
    }
    private static void IgnoreReport(RestartRuntime runtime, string value) { }
    private static void CaptureFailure(RestartRuntime runtime, string value) { failures++; }
    private static void IgnoreApply(RestartRuntime runtime, RestartStep step) { }
    private static ulong RuntimeHost(RestartRuntime runtime) => visibleHost;
    private static ulong[] RuntimeRoster(RestartRuntime runtime) => new ulong[] { 1001, 1002 };
    private static Platform_Multiplayer RuntimeMultiplayer() => currentMultiplayer;
    private static void FinalSend(Platform_Multiplayer multiplayer, Platform_Multiplayer.MPData packet, bool instantMessage)
    {
        Check(prefix(packet, instantMessage), "Released seed was held a second time by the actual prefix.");
        finalSends++; finalPacket = packet; finalInstant = instantMessage;
        if (senderThrows) throw new InvalidOperationException("fixture-send-failure");
    }
    private static RestartRuntime Runtime(bool host = true)
    {
        var runtime = (RestartRuntime)Empty(typeof(RestartRuntime));
        var protocol = new RestartProtocol(host ? 1001UL : 1002UL, 1001, new ulong[] { 1001, 1002 }, "source", "digest", long.MaxValue);
        Set(protocol, "<Phase>k__BackingField", RestartPhase.Starting);
        Set(protocol, "<Committed>k__BackingField", true);
        Set(runtime, "protocol", protocol); Set(runtime, "host", 1001UL);
        Set(runtime, "unityThread", Thread.CurrentThread.ManagedThreadId);
        Set(runtime, "targetLoadEntered", true);
        Set(runtime, "loadReported", true);
        Set(runtime, "seedHandoff", new RestartSeedHandoff());
        Set(runtime, "targetState", new RestartTargetState(1));
        return runtime;
    }
    private static MethodInfo RuntimeClone(string name)
    {
        var definition = new DynamicMethodDefinition(Method(typeof(RestartRuntime), name)); definitions.Add(definition);
        using (var il = new ILContext(definition.Definition))
        {
            foreach (var instruction in il.Body.Instructions)
            {
                if (!(instruction.Operand is MethodReference call)) continue;
                MethodInfo replacement = null;
                if (call.DeclaringType.FullName == typeof(RestartRuntime).FullName)
                {
                    if (call.Name == "Report") replacement = Own(nameof(IgnoreReport));
                    else if (call.Name == "Fail") replacement = Own(nameof(CaptureFailure));
                    else if (call.Name == "Apply") replacement = Own(nameof(IgnoreApply));
                    else if (call.Name == "CurrentHost") replacement = Own(nameof(RuntimeHost));
                    else if (call.Name == "Roster") replacement = Own(nameof(RuntimeRoster));
                }
                else if (call.DeclaringType.FullName == typeof(Platform_Multiplayer).FullName)
                {
                    if (call.Name == "get_Instance") replacement = Own(nameof(RuntimeMultiplayer));
                    else if (call.Name == "SendPacketToAll") replacement = Own(nameof(FinalSend));
                }
                if (replacement == null) continue;
                instruction.OpCode = OpCodes.Call; instruction.Operand = il.Import(replacement);
            }
        }
        return definition.Generate();
    }
    private static void SeedHandoffAndSender()
    {
        var missing = new RestartSeedHandoff();
        Rejects(() => missing.Take(), "Missing seed was released.");
        foreach (var invalid in new[] { null, Packet((short)Enums.MPFlags.StartGamePacket),
            Packet((short)Enums.MPFlags.InitialSeedPacket, new byte[3]),
            Packet((short)Enums.MPFlags.InitialSeedPacket, new byte[5]) })
            Rejects(() => new RestartSeedHandoff().Hold(invalid, false), "Invalid seed shape was held.");
        var shifted = Packet((short)Enums.MPFlags.InitialSeedPacket, new byte[4]); shifted.dataOffset = 1;
        Rejects(() => new RestartSeedHandoff().Hold(shifted, false), "Shifted seed payload was held.");
        var source = Packet((short)Enums.MPFlags.InitialSeedPacket, BitConverter.GetBytes(123456));
        var handoff = new RestartSeedHandoff(); handoff.Hold(source, true); source.data[0]++;
        Rejects(() => handoff.Hold(source, false), "A second seed replaced the held packet.");
        var taken = handoff.Take();
        Check(BitConverter.ToInt32(taken.data, 0) == 123456 && !ReferenceEquals(taken.data, source.data) &&
            taken.dataLength == 4 && taken.dataOffset == 0 && handoff.InstantMessage,
            "Held packet aliased mutable sender storage or lost framing/delivery mode.");
        Rejects(() => handoff.Take(), "The seed was released twice.");
        Rejects(() => handoff.Hold(source, false), "A consumed handoff accepted another seed.");
        var abandoned = new RestartSeedHandoff(); abandoned.Hold(source, false); abandoned.Discard();
        Rejects(() => abandoned.Take(), "Abandoned target retained its seed.");
        Rejects(() => abandoned.Hold(source, false), "Abandoned handoff was reopened.");

        var previous = RestartPlugin.Runtime;
        try
        {
            prefix = (Func<Platform_Multiplayer.MPData, bool, bool>)RuntimeClone("BeforePacketSend")
                .CreateDelegate(typeof(Func<Platform_Multiplayer.MPData, bool, bool>));
            var tick = (Action<RestartRuntime>)RuntimeClone("Tick").CreateDelegate(typeof(Action<RestartRuntime>));
            var seed = Packet((short)Enums.MPFlags.InitialSeedPacket, BitConverter.GetBytes(123456));
            RestartPlugin.Runtime = null; Check(prefix(seed, false), "Uninstalled runtime intercepted Vanilla seed.");
            var runtime = Runtime(); RestartPlugin.Runtime = runtime;
            Set(runtime, "targetLoadEntered", false);
            Check(prefix(seed, false), "Ordinary pre-target multiplayer was intercepted.");
            Set(runtime, "targetLoadEntered", true);
            Check(prefix(Packet((short)Enums.MPFlags.StartGamePacket), false), "Synchronized-start packet was intercepted.");
            Check(prefix(null, false), "Unrelated null-packet path changed.");
            RestartPlugin.Runtime = Runtime(false); Check(prefix(seed, false), "Client seed path was intercepted.");
            RestartPlugin.Runtime = runtime;
            Check(!prefix(seed, true), "Authorized host seed escaped before target-loaded receipts.");
            failures = 0;
            Rejects(() => prefix(seed, true), "Duplicate host seed was silently forwarded.");
            Check(failures == 1, "Duplicate seed did not notify actual runtime failure boundary.");

            // Execute the real Tick sender block, preserving Take/once/finally and
            // the real prefix. Only protocol effects and final Steam/UI boundaries
            // are isolated; no synthetic packet send stands in for the Tick body.
            runtime = Runtime(); RestartPlugin.Runtime = runtime;
            ((RestartSeedHandoff)Get(runtime, "seedHandoff")).Hold(seed, true);
            Set(runtime, "seedReleasePending", true);
            Select(Make("sender-host", true)); finalSends = failures = 0; senderThrows = false; visibleHost = 1001;
            tick(runtime);
            Check(finalSends == 1 && failures == 0 && finalInstant && BitConverter.ToInt32(finalPacket.data, 0) == 123456,
                "Actual runtime Tick did not release the original seed exactly once.");
            Check(!(bool)Get(runtime, "sendingHeldSeed") && !(bool)Get(runtime, "seedReleasePending"),
                "Successful seed release left a recursion/retry flag set.");
            tick(runtime); Check(finalSends == 1, "Later frame sent the seed again.");
            runtime = Runtime(); RestartPlugin.Runtime = runtime;
            ((RestartSeedHandoff)Get(runtime, "seedHandoff")).Hold(seed, false); Set(runtime, "seedReleasePending", true);
            senderThrows = true; failures = finalSends = 0; tick(runtime);
            Check(finalSends == 1 && failures == 1 && !(bool)Get(runtime, "sendingHeldSeed"),
                "Sender exception bypassed failure handling or retained prefix bypass.");
            runtime = Runtime(); RestartPlugin.Runtime = runtime;
            ((RestartSeedHandoff)Get(runtime, "seedHandoff")).Hold(seed, false); Set(runtime, "seedReleasePending", true);
            senderThrows = false; visibleHost = 1002; finalSends = failures = 0; tick(runtime);
            Check(finalSends == 0 && failures == 1, "Host change still released the deferred seed.");
        }
        finally { RestartPlugin.Runtime = previous; senderThrows = false; visibleHost = 1001; }
    }
    internal static void Run()
    {
        Thread worker = null;
        try
        {
            LobbyReceiverDrop();
            currentEditor = (EditorDirector)Empty(typeof(EditorDirector));
            currentView = (MainViewModel)Empty(typeof(MainViewModel));
            currentAchievements = (AchievementsCommon)Empty(typeof(AchievementsCommon));
            originalStart = (Action<Director>)Clone(typeof(Director), "startSimThread", false).CreateDelegate(typeof(Action<Director>));
            process = (Func<Platform_Multiplayer, Platform_Multiplayer.MPData, Platform_Multiplayer.MPGameMember, bool, bool>)
                Clone(typeof(Platform_Multiplayer), "processMessage", true).CreateDelegate(typeof(Func<Platform_Multiplayer, Platform_Multiplayer.MPData, Platform_Multiplayer.MPGameMember, bool, bool>));
            monitor = (Action<Platform_Multiplayer>)Clone(typeof(Platform_Multiplayer), "monitorHostGameStart", true).CreateDelegate(typeof(Action<Platform_Multiplayer>));
            update = (Action<Director, bool>)Clone(typeof(Director), "UpdateMultiplayer", true).CreateDelegate(typeof(Action<Director, bool>));
            var host = Make("host", true); var client = Make("client", false);
            worker = Park();
            client.Inbox.Enqueue(Packet((short)Enums.MPFlags.InitialSeedPacket, BitConverter.GetBytes(123456)));
            Pump(client);
            Check(client.Seed == 0 && client.Receives == 0 && StopBarrier.Quiescent, "Closed transition admitted the seed.");
            StopBarrier.AllowNetwork(); Pump(client);
            Check(client.Seed == 123456 && client.PostLoads == 1 && client.Sent.SequenceEqual(new[] { (short)Enums.MPFlags.InitialAcknowledgePacket }),
                "Reopened Unity network path did not finish seed/postLoading/ack.");
            Check(StopBarrier.Quiescent && client.Starts == 0, "Seed receipt released simulation before synchronized start.");
            Pump(host);
            Check(host.Starts == 0 && host.Sent.Count == 0, "Host started without client acknowledgement.");
            host.Inbox.Enqueue(Packet((short)Enums.MPFlags.InitialAcknowledgePacket));
            StopBarrier.AllowLaunch(); Pump(host);
            Check(host.Starts == 1 && host.Synced == 1 && host.Sent.SequenceEqual(new[] { (short)Enums.MPFlags.StartGamePacket }),
                "Actual host monitor did not send synchronized start after acknowledgement.");
            Check(!StopBarrier.Requested && worker.Join(3000) && host.Director.SimRunning, "Host start failed to release the parked worker.");
            worker = Park(); StopBarrier.AllowLaunch();
            client.Inbox.Enqueue(Packet((short)Enums.MPFlags.StartGamePacket)); Pump(client);
            Check(client.Starts == 1 && client.Synced == 1 && client.Director.SimRunning && !StopBarrier.Requested && worker.Join(3000),
                "Actual client packet-4 path did not release and start the worker.");
            SeedHandoffAndSender();
            Console.WriteLine("Actual Vanilla parked-worker startup handshake passed: " + checks + " checks; Steam/native/UI boundaries isolated.");
        }
        finally
        {
            StopBarrier.Release(); if (worker != null) worker.Join(3000);
            foreach (var definition in definitions) definition.Dispose(); definitions.Clear();
        }
    }
}
