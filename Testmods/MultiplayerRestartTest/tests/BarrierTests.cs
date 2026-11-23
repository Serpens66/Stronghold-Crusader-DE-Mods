using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MultiplayerSaveLoadTest;

// Separate fixture executable: compile production LoadBarrier and this file against
// the selected runtime references. Pass the installed Assembly-CSharp.dll as argv[0].
// This reads its actual method body; it does not install a hook into the game.
internal static class BarrierTests
{
    private static int checks, calls;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static MethodInfo Private(string name) => typeof(LoadBarrier).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
    private static FieldInfo Field(string name) => typeof(LoadBarrier).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
    private static Instruction Target(object operand) => operand is ILLabel label ? label.Target : operand as Instruction;

    private static void InstalledLoopFixture(string gameAssembly)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(gameAssembly))
        {
            var method = assembly.MainModule.Types.Single(t => t.FullName == "Director").Methods.Single(m => m.Name == "runSimTickTest");
            int originalCount = method.Body.Instructions.Count;
            using (var il = new ILContext(method)) il.Invoke(ctx => {
                // Discover the runtime backedge independently by its loop-body destination.
                var edge = ctx.Body.Instructions.Single(i =>
                    (i.OpCode == OpCodes.Brtrue || i.OpCode == OpCodes.Brtrue_S) &&
                    Target(i.Operand)?.Next?.Operand is FieldReference f && f.Name == "FramesToProcess");
                var originalEntry = Target(edge.Operand);
                Check(edge.Operand is ILLabel, "Fixture did not exercise MonoMod label normalization.");
                LoadBarrier.Inject(ctx);
                var gate = Target(edge.Operand);
                Check(gate.OpCode == OpCodes.Call && gate.Operand is MethodReference called && called.Name == "Checkpoint",
                    "Backedge bypasses the production checkpoint.");
                Check(ReferenceEquals(gate.Next, originalEntry) && originalEntry.OpCode == OpCodes.Ldarg_0,
                    "Checkpoint does not run before the original frame read.");
                Check(ctx.Body.Instructions.Count == originalCount + 1, "Fixture changed more than one instruction.");
                Check(ctx.Body.Instructions.Count(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference r && r.Name == "Checkpoint") == 1,
                    "Multiple checkpoints inserted.");
                Check(ctx.Body.Instructions.All(i => Target(i.Operand) == null || !ReferenceEquals(Target(i.Operand), originalEntry)),
                    "An original branch still skips the checkpoint.");
            });
        }
        using (var assembly = AssemblyDefinition.ReadAssembly(gameAssembly))
        {
            var method = assembly.MainModule.Types.Single(t => t.FullName == "Director").Methods.Single(m => m.Name == "runSimTickTest");
            using (var il = new ILContext(method)) il.Invoke(ctx => {
                var frame = ctx.Body.Instructions.First(i => i.Operand is FieldReference f && f.Name == "FramesToProcess");
                frame.Next.OpCode = OpCodes.Ldc_I4_1; // An incompatible admission predicate must fail before any edit.
                int count = ctx.Body.Instructions.Count;
                bool rejected = false;
                try { LoadBarrier.Inject(ctx); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && ctx.Body.Instructions.Count == count, "Unknown loop contract was mutated.");
            });
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MyFixedUpdate(bool fail)
    { calls++; if (fail) throw new InvalidOperationException("fixture-original"); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UpdateMultiplayer() { calls++; }

    private static void HarmonyReceipts()
    {
        var patcher = new Harmony("MultiplayerSaveLoadTest.BarrierFixture");
        foreach (string name in new[] { nameof(MyFixedUpdate), nameof(UpdateMultiplayer) })
            patcher.Patch(typeof(BarrierTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static),
                prefix: new HarmonyMethod(Private("Enter")), finalizer: new HarmonyMethod(Private("Leave")), ilmanipulator: null);
        LoadBarrier.Release();
        MyFixedUpdate(false);
        Check(calls == 1 && (int)Field("entries").GetValue(null) == 0, "Normal prefix/finalizer receipt differs.");
        bool threw = false;
        try { MyFixedUpdate(true); } catch (InvalidOperationException e) { threw = e.Message == "fixture-original"; }
        Check(threw && (int)Field("entries").GetValue(null) == 0, "Original exception or finalizer receipt lost.");
        Field("requested").SetValue(null, 1);
        MyFixedUpdate(false); UpdateMultiplayer();
        Check(calls == 2 && (int)Field("entries").GetValue(null) == 0, "Blocked calls ran or leaked active counts.");
        LoadBarrier.AllowNetwork();
        UpdateMultiplayer(); MyFixedUpdate(false);
        Check(calls == 3 && (int)Field("entries").GetValue(null) == 0, "Network-only reopening admitted timer work.");
        LoadBarrier.Release();
    }

    private static void ParkReceipt()
    {
        object entry = Field("Entry").GetValue(null);
        Action checkpoint = (Action)Delegate.CreateDelegate(typeof(Action), Private("Checkpoint"));
        lock (entry) Field("requested").SetValue(null, 1);
        var worker = new Thread(() => checkpoint());
        worker.Start();
        Check(SpinWait.SpinUntil(() => LoadBarrier.Quiescent, 3000), "Checkpoint did not park.");
        LoadBarrier.Release();
        Check(worker.Join(3000), "Release failed to wake worker.");
        Check((int)Field("parked").GetValue(null) == 0 && !LoadBarrier.Quiescent, "Park receipt survived worker release.");
    }

    public static int Main(string[] args)
    {
        try
        {
            InstalledLoopFixture(args[0]);
            HarmonyReceipts();
            ParkReceipt();
            Console.WriteLine("Barrier fixture passed: " + checks + " checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { LoadBarrier.Release(); }
    }
}
