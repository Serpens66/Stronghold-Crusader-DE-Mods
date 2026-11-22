using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MultiplayerRestartTest;

// The actual runtime decides when to issue Ready. Only the clock and final Vanilla
// UI command are replaced; these checks do not pretend to accept a guarded launch.
internal static class RuntimeReadyTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static long now;
    private static int clicks, checks;
    private static long Clock(RestartRuntime runtime) => now;
    private static void Click() { clicks++; }
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    internal static void Run()
    {
        using (var method = new DynamicMethodDefinition(typeof(RestartRuntime).GetMethod("TryIssueReady", Any)))
        using (var il = new ILContext(method.Definition))
        {
            int clocks = 0, commands = 0;
            foreach (var instruction in il.Body.Instructions)
            {
                if (!(instruction.Operand is MethodReference called) || called.DeclaringType.FullName != typeof(RestartRuntime).FullName) continue;
                if (called.Name == "get_Now")
                {
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = il.Import(typeof(RuntimeReadyTests).GetMethod(nameof(Clock), Any)); clocks++;
                }
                else if (called.Name == "ClickReady")
                {
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = il.Import(typeof(RuntimeReadyTests).GetMethod(nameof(Click), Any)); commands++;
                }
            }
            Check(clocks > 0 && commands > 0, "The real Ready clock/UI boundaries were not isolated.");
            var attempt = (Func<RestartRuntime, bool>)method.Generate().CreateDelegate(typeof(Func<RestartRuntime, bool>));
            var runtime = (RestartRuntime)FormatterServices.GetUninitializedObject(typeof(RestartRuntime));
            var ready = typeof(RestartRuntime).GetField("productionReady", Any);
            var due = typeof(RestartRuntime).GetField("nextReadyAttempt", Any);
            now = 0; clicks = 0;
            Check(!attempt(runtime) && clicks == 0 && (long)due.GetValue(runtime) == 0,
                "Pending production agreement issued Ready or consumed a retry slot.");
            ready.SetValue(runtime, true);
            Check(attempt(runtime) && clicks == 1, "Completed production agreement did not permit a guarded Ready attempt.");
            long deadline = (long)due.GetValue(runtime);
            for (int frame = 0; frame < 100; frame++) Check(!attempt(runtime), "Ready was repeatedly clicked within one retry interval.");
            Check(clicks == 1 && (long)due.GetValue(runtime) == deadline, "Suppressed frames changed the retry deadline.");
            now = Stopwatch.Frequency - 1;
            Check(!attempt(runtime) && clicks == 1, "Ready retried before one second elapsed.");
            now = Stopwatch.Frequency;
            Check(attempt(runtime) && clicks == 2, "A previously rejected Ready could not retry after yielding.");
            ready.SetValue(runtime, false); now = Stopwatch.Frequency * 2;
            Check(!attempt(runtime) && clicks == 2, "Loss of production readiness retained launch permission.");
            ready.SetValue(runtime, true);
            Check(attempt(runtime) && clicks == 3, "Renewed readiness did not resume the ordinary Ready path.");
        }
        Console.WriteLine("Actual runtime Ready scheduling passed: " + checks + " checks.");
    }
}
