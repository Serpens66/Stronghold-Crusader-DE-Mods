using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared.Patching;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MultiplayerSaveLoadTest
{
    // A process-lived checkpoint before EACH sim iteration, never inside native execution.
    // Unity requests parking and observes receipts; it never waits for worker threads.
    internal static class LoadBarrier
    {
        private static readonly object Entry = new object();
        private static int requested, parked, entries, hits, networkAllowed, launchAllowed, installed;
        private static SafeIlHook hook;
        private static Harmony harmony;
        internal static bool Installed => Volatile.Read(ref installed) != 0 && hook?.IsApplied == true;
        internal static bool Observed => Volatile.Read(ref hits) != 0;
        internal static bool Requested => Volatile.Read(ref requested) != 0;
        internal static bool Quiescent { get { lock (Entry) return Requested && Volatile.Read(ref parked) != 0 && entries == 0; } }
        internal static void InstallEarly(ManualLogSource log)
        {
            var instance = typeof(Director).GetField("instance", BindingFlags.Public | BindingFlags.Static);
            var director = instance?.GetValue(null);
            var threadField = typeof(Director).GetField("simThread", BindingFlags.NonPublic | BindingFlags.Instance);
            if (threadField?.FieldType != typeof(Thread)) throw new MissingFieldException("Director.simThread");
            if (director != null && threadField.GetValue(director) is Thread thread && thread.IsAlive)
                throw new InvalidOperationException("The sim thread already runs; its active invocation cannot be repatched.");
            if (!SafeIlHook.TryInstall(SaveLoadPlugin.Guid, SaveLoadPlugin.Guid + ".sim-checkpoint",
                AccessTools.Method(typeof(Director), "runSimTickTest"), Inject, log, out hook, out string reason))
                throw new InvalidOperationException(reason);
            harmony = new Harmony(SaveLoadPlugin.Guid + ".barrier");
            foreach (string method in new[] { "MyFixedUpdate", "forceEarlyEngine", "UpdateMultiplayer" })
                harmony.Patch(AccessTools.Method(typeof(Director), method),
                    prefix: new HarmonyMethod(typeof(LoadBarrier), nameof(Enter)),
                    finalizer: new HarmonyMethod(typeof(LoadBarrier), nameof(Leave)), ilmanipulator: null);
            harmony.Patch(AccessTools.Method(typeof(Director), "startSimThread"),
                new HarmonyMethod(typeof(LoadBarrier), nameof(Start)));
            harmony.Patch(AccessTools.Method(typeof(Director), "OnDestroy"),
                new HarmonyMethod(typeof(LoadBarrier), nameof(Shutdown)));
            Volatile.Write(ref installed, 1);
        }
        internal static void Inject(ILContext il)
        {
            var candidates = il.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldfld &&
                i.Operand is FieldReference f && f.DeclaringType.FullName == "Director" && f.Name == "FramesToProcess" &&
                i.Previous?.OpCode == OpCodes.Ldarg_0 && i.Next?.OpCode == OpCodes.Ldc_I4_0 &&
                (i.Next.Next?.OpCode == OpCodes.Ble || i.Next.Next?.OpCode == OpCodes.Ble_S)).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Unique sim-loop admission contract missing.");
            var target = candidates[0].Previous;
            var branches = il.Body.Instructions.Where(i => ReferenceEquals(BranchTarget(i.Operand), target)).ToArray();
            if (branches.Length != 1 || (branches[0].OpCode != OpCodes.Brtrue && branches[0].OpCode != OpCodes.Brtrue_S))
                throw new InvalidOperationException("Unique sim-loop backedge missing.");
            var cursor = new ILCursor(il); cursor.Goto(target, MoveType.Before);
            cursor.Emit(OpCodes.Call, typeof(LoadBarrier).GetMethod(nameof(Checkpoint), BindingFlags.Static | BindingFlags.NonPublic));
            // Explicitly redirect the loop backedge. Existing active method invocations cannot use this edit.
            foreach (var branch in branches)
            {
                if (branch.Operand is ILLabel label) label.Target = target.Previous;
                else branch.Operand = target.Previous;
            }
        }
        private static Instruction BranchTarget(object operand) => operand is ILLabel label ? label.Target : operand as Instruction;
        private static void Checkpoint()
        {
            if (Volatile.Read(ref hits) == 0) Volatile.Write(ref hits, 1);
            if (!Requested) return;
            // Publish and clear the receipt under the same lock as Request/Release.
            // Otherwise a rapid second request could observe the previous park receipt
            // after its worker had already decided to resume.
            lock (Entry)
            {
                if (!Requested) return;
                Volatile.Write(ref parked, 1);
                while (Requested) Monitor.Wait(Entry, 1);
                Volatile.Write(ref parked, 0);
            }
        }
        private static bool Enter(MethodBase __originalMethod, out bool __state)
        {
            lock (Entry)
            {
                bool network = __originalMethod.Name == "UpdateMultiplayer";
                __state = !Requested || (network && Volatile.Read(ref networkAllowed) != 0);
                if (__state) entries++;
                return __state;
            }
        }
        private static Exception Leave(Exception __exception, bool __state)
        { if (__state) { lock (Entry) entries--; } return __exception; }
        internal static void Request()
        {
            if (!Installed || !Observed) throw new InvalidOperationException("Worker checkpoint has not been observed.");
            lock (Entry) { Volatile.Write(ref launchAllowed, 0); Volatile.Write(ref networkAllowed, 0); Volatile.Write(ref requested, 1); }
        }
        internal static void AllowNetwork() => Volatile.Write(ref networkAllowed, 1);
        internal static void AllowLaunch() { AllowNetwork(); Volatile.Write(ref launchAllowed, 1); }
        private static bool Start()
        {
            if (!Requested) return true;
            if (Volatile.Read(ref launchAllowed) == 0) return false;
            Release(); return true;
        }
        internal static void Release()
        { lock (Entry) { Volatile.Write(ref requested, 0); Volatile.Write(ref networkAllowed, 0); Volatile.Write(ref launchAllowed, 0); Monitor.PulseAll(Entry); } }
        private static void Shutdown() => Release(); // Let Vanilla's simThread.Join finish on real Director destruction.
    }
}
