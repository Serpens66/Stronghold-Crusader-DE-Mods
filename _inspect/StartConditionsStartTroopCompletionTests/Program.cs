using StartConditions;
using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string workspaceRoot = args != null && args.Length == 1
                ? Path.GetFullPath(args[0])
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

            TestCompletionValues();
            TestNativeReaderBoundsAndReads();
            TestNativeReaderFailure();
            TestHashGatedCompletionReader();
            TestCompletionWaitState();
            TestSourceContracts(workspaceRoot);
            Console.WriteLine("PASS: StartConditions Vanilla start-troop completion contracts.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void TestCompletionValues()
    {
        Require(StartTroopSpawnCompletionContract.TryInterpret(0, out bool zeroComplete) && !zeroComplete,
            "zero must be a valid incomplete progress value");
        Require(StartTroopSpawnCompletionContract.TryInterpret(200, out bool progressComplete) && !progressComplete,
            "positive progress must be valid and incomplete");
        Require(StartTroopSpawnCompletionContract.TryInterpret(-1, out bool complete) && complete,
            "only -1 must indicate completion");
        Require(!StartTroopSpawnCompletionContract.TryInterpret(-2, out bool invalidComplete) && !invalidComplete,
            "unexpected negative values must invalidate the native contract");
    }

    private static void TestNativeReaderBoundsAndReads()
    {
        IntPtr memory = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.WriteInt32(memory, 4, 1234);
            var reader = new NativeInt32StateReader();
            Require(!reader.TryInitialize(IntPtr.Zero, 16, 4), "zero module handle must fail");
            Require(!reader.TryInitialize(memory, 7, 4), "three remaining bytes must fail");
            Require(reader.TryInitialize(memory, 8, 4), "exactly four remaining bytes must succeed");
            Require(reader.TryRead(out int value, out Exception failure), "initialized reader did not read");
            Require(value == 1234 && failure == null, "reader returned the wrong int32 value");
            Require(!reader.TryInitialize(memory, 16, -1), "negative RVA must fail");
            Require(!reader.TryInitialize(memory, -1, 0), "negative module length must fail");
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void TestNativeReaderFailure()
    {
        var reader = new NativeInt32StateReader(_ => throw new InvalidOperationException("synthetic read failure"));
        Require(reader.TryInitialize(new IntPtr(1), 4, 0), "synthetic reader did not initialize");
        Require(!reader.TryRead(out _, out Exception failure), "synthetic failure unexpectedly succeeded");
        Require(failure is InvalidOperationException, "synthetic failure was not returned");
        Require(!reader.IsAvailable, "reader must disable itself after a read failure");
        Require(!reader.TryRead(out _, out Exception secondFailure) && secondFailure == null,
            "disabled reader must remain unavailable without repeating the exception");
    }

    private static void TestHashGatedCompletionReader()
    {
        IntPtr memory = Marshal.AllocHGlobal(8);
        try
        {
            Marshal.WriteInt32(memory, 0, -1);
            var wrongHashReader = new VanillaStartTroopCompletionReader(new NativeInt32StateReader());
            Require(!wrongHashReader.TryInitialize(memory, int.MaxValue, false),
                "an unrecognized native hash must keep the completion reader unavailable");
            Require(!wrongHashReader.TryGetIsComplete(out _, out _, out _),
                "a hash-rejected completion reader unexpectedly read native memory");

            var currentHashReader = new VanillaStartTroopCompletionReader(
                new NativeInt32StateReader(address => Marshal.ReadInt32(memory)));
            Require(currentHashReader.TryInitialize(memory, int.MaxValue, true),
                "the verified native hash did not initialize the completion reader");
            Require(currentHashReader.TryGetIsComplete(out bool complete, out int rawValue, out Exception failure),
                "the verified completion reader failed");
            Require(complete && rawValue == -1 && failure == null,
                "the verified completion reader did not report Vanilla's terminal value");

            Marshal.WriteInt32(memory, 0, -2);
            Require(!currentHashReader.TryGetIsComplete(out bool invalidComplete, out int invalidRaw, out failure),
                "an unexpected negative completion value must fail closed");
            Require(!invalidComplete && invalidRaw == -2 && failure == null,
                "the unexpected completion value was not preserved for diagnostics");
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void TestCompletionWaitState()
    {
        var state = new StartTroopCompletionWaitState();
        Require(state.Observe(false, 10) == StartTroopCompletionWaitResult.Waiting,
            "incomplete Vanilla state must keep waiting");
        Require(state.Observe(true, 11) == StartTroopCompletionWaitResult.Settling,
            "first completion observation must begin the settling tick");
        Require(state.Observe(true, 11) == StartTroopCompletionWaitResult.Settling,
            "the same tick must not become ready");
        Require(state.Observe(true, 12) == StartTroopCompletionWaitResult.Ready,
            "the following simulation tick must become ready");

        state.Reset();
        Require(state.Observe(true, 20) == StartTroopCompletionWaitResult.Settling,
            "reset must clear the prior observation");
        Require(state.Observe(false, 21) == StartTroopCompletionWaitResult.Waiting,
            "a reverted completion signal must clear settling state");
        Require(state.Observe(true, 22) == StartTroopCompletionWaitResult.Settling,
            "completion after a revert must start settling again");
    }

    private static void TestSourceContracts(string workspaceRoot)
    {
        string runtime = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "StartConditionsRuntime.StartTroops.cs"));
        string reader = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "VanillaStartTroopSpawnState.cs"));

        RequireContains(reader, "referenceHashMatches");
        RequireContains(reader, "StartTroopSpawnCompletionContract.CompletionStateRva");
        RequireContains(runtime, "GameTimeManagerAPI.Instance.OnTick += OnVanillaStartTroopCompletionTick;");
        RequireContains(runtime, "GameTimeManagerAPI.Instance.OnTick -= OnVanillaStartTroopCompletionTick;");
        RequireContains(runtime, "StartLegacyStartTroopTiming(plan");
        RequireContains(runtime, "StopWaitingForVanillaStartTroopCompletion();");

        int executeIndex = runtime.IndexOf("private void ExecuteStartTroopPlan", StringComparison.Ordinal);
        int countIndex = runtime.IndexOf("CountSoldiersForPlayers()", executeIndex, StringComparison.Ordinal);
        int configuredSpawnIndex = runtime.IndexOf(
            "SpawnConfiguredStartTroops(plan.AiTroops, plan.HumanTroops);",
            executeIndex,
            StringComparison.Ordinal);
        Require(countIndex >= 0 && configuredSpawnIndex > countIndex,
            "configured mod troops must spawn only after Vanilla troops are counted");
    }

    private static void RequireContains(string source, string expected)
    {
        Require(source.IndexOf(expected, StringComparison.Ordinal) >= 0, "missing source contract: " + expected);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
