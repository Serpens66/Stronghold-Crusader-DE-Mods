using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MultiplayerRestartTest;

// Real managed Vanilla objects, never a constructed Unity view or Steam session.
// Keeping the member's connection flag proves that detachment did not run Vanilla's
// session-closing loop; executing that loop would also require unavailable Steam here.
internal static class TransportTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static Action<Platform_Multiplayer> adaptedClear;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }

    internal static void Run()
    {
        // Unity Mono supplies ConcurrentQueue.Clear; desktop .NET Framework does not.
        // Clone rather than detour: publishing a detour would JIT the unadapted method
        // first and fail on this absent BCL member. Both production bodies still run.
        using (var vanilla = new DynamicMethodDefinition(typeof(Platform_Multiplayer).GetMethod("clearMPMessages")))
        using (var il = new ILContext(vanilla.Definition))
        {
            AdaptClear(il);
            adaptedClear = (Action<Platform_Multiplayer>)vanilla.Generate().CreateDelegate(typeof(Action<Platform_Multiplayer>));
        }
        Action<Platform_Multiplayer> clear;
        using (var bridge = new DynamicMethodDefinition(typeof(VanillaBridge).GetMethod("ClearOldManagedMessages",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Platform_Multiplayer) }, null)))
        using (var il = new ILContext(bridge.Definition))
        {
            int replaced = 0;
            foreach (var instruction in il.Body.Instructions)
                if (instruction.Operand is MethodReference called && called.Name == "clearMPMessages" &&
                    called.DeclaringType.FullName == typeof(Platform_Multiplayer).FullName)
                {
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = il.Import(typeof(TransportTests).GetMethod(nameof(InvokeAdaptedClear), BindingFlags.NonPublic | BindingFlags.Static));
                    replaced++;
                }
            Check(replaced == 1, "Production bridge no longer calls the expected Vanilla queue cleanup.");
            clear = (Action<Platform_Multiplayer>)bridge.Generate().CreateDelegate(typeof(Action<Platform_Multiplayer>));
        }
        bool priorActive = Platform_Multiplayer.MPGameActive;
        try
        {
            var multiplayer = (Platform_Multiplayer)FormatterServices.GetUninitializedObject(typeof(Platform_Multiplayer));
            var lobby = new Platform_Multiplayer.MPLobby();
            var member = new Platform_Multiplayer.MPGameMember { steamID = 10001, stillWithSteamConnection = true };
            var roster = new List<Platform_Multiplayer.MPGameMember> { member };
            multiplayer.gameMembers = roster;
            multiplayer.activeLobby = lobby;
            multiplayer.IsHost = true;
            Platform_Multiplayer.MPGameActive = true;

            var queueField = typeof(Platform_Multiplayer).GetField("threadedMessages", PrivateInstance);
            object queue = Activator.CreateInstance(queueField.FieldType);
            queueField.SetValue(multiplayer, queue);
            Type itemType = queueField.FieldType.GetGenericArguments().Single();
            object queued = FormatterServices.GetUninitializedObject(itemType);
            queueField.FieldType.GetMethod("Enqueue").Invoke(queue, new[] { queued });
            Func<int> count = () => (int)queueField.FieldType.GetProperty("Count").GetValue(queue, null);

            VanillaBridge.DetachTransport(multiplayer);
            Check(!Platform_Multiplayer.MPGameActive && multiplayer.gameMembers == null,
                "Old gameplay send/receive paths remain attached.");
            Check(member.stillWithSteamConnection && roster.Count == 1 && ReferenceEquals(roster[0], member),
                "Detachment modified or closed a retained peer.");
            Check(ReferenceEquals(multiplayer.activeLobby, lobby) && multiplayer.IsHost,
                "Detachment replaced lobby identity or role.");
            Check(count() == 1, "Detachment prematurely erased a managed old-stream message.");
            VanillaBridge.DetachTransport(multiplayer);
            Check(member.stillWithSteamConnection && multiplayer.gameMembers == null,
                "Repeated detachment is not idempotent.");

            clear(multiplayer);
            Check(count() == 0 && member.stillWithSteamConnection && ReferenceEquals(multiplayer.activeLobby, lobby),
                "Explicit post-fence queue cleanup affected retained transport or lobby.");

            var closed = new List<ulong>();
            VanillaBridge.CleanupRetainedPeers(new ulong[] { 0, 8, 10001, 10002, 10001 }, peer => closed.Add(peer));
            Check(closed.SequenceEqual(new ulong[] { 10001, 10002 }),
                "Terminal cleanup closes AI/sentinel identities or repeats a peer.");
            closed.Clear();
            VanillaBridge.CleanupRetainedPeers(Array.Empty<ulong>(), peer => closed.Add(peer));
            Check(closed.Count == 0, "Empty retained-peer cleanup performed work.");
        }
        finally { Platform_Multiplayer.MPGameActive = priorActive; }
        Console.WriteLine("Transport detachment fixtures passed: " + checks + " checks.");
    }
    private static void InvokeAdaptedClear(Platform_Multiplayer multiplayer) => adaptedClear(multiplayer);
    private static void AdaptClear(ILContext il)
    {
        var calls = il.Body.Instructions.Where(i => i.Operand is MethodReference method &&
            method.Name == "Clear" && method.Parameters.Count == 0 &&
            method.DeclaringType.FullName.StartsWith("System.Collections.Concurrent.ConcurrentQueue`1", StringComparison.Ordinal)).ToArray();
        if (calls.Length != 1) throw new InvalidOperationException("Unique Vanilla queue Clear call missing.");
        calls[0].OpCode = OpCodes.Call;
        calls[0].Operand = il.Import(typeof(TransportTests).GetMethod(nameof(PortableClear), BindingFlags.NonPublic | BindingFlags.Static));
    }
    private static void PortableClear(object queue)
    {
        var dequeue = queue.GetType().GetMethod("TryDequeue");
        var arguments = new object[] { null };
        while ((bool)dequeue.Invoke(queue, arguments)) arguments[0] = null;
    }
}
