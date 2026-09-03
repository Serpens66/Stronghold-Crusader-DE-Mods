using OxTetherIdleFixTest;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

internal static class Program
{
    private const string ExpectedHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const string DllPath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

    private static int assertions;

    private static int Main()
    {
        try
        {
            CheckNativeHash();
            CheckLayout();
            CheckPolicy();
            CheckSourceContracts();
            Console.WriteLine($"PASS: OxTetherIdleFixTest tests ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void CheckNativeHash()
    {
        using (FileStream stream = File.OpenRead(DllPath))
        using (SHA256 sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            Check(actual == ExpectedHash, "canonical native hash");
            Check(actual != new string('0', 64), "unknown native hash is rejected");
        }
    }

    private static void CheckLayout()
    {
        CheckOffset(nameof(GameUnit.r_AliveState), 0x88);
        CheckOffset(nameof(GameUnit.r_UnitChimp), 0x8A);
        CheckOffset(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
        CheckOffset(nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
        CheckOffset(nameof(GameUnit.r_TargetTilePositionX2), 0xE8);
        CheckOffset(nameof(GameUnit.r_TargetTilePositionY2), 0xEA);
        CheckOffset(nameof(GameUnit.r_PathPlanStateBitFlags), 0xF2);
        CheckOffset(nameof(GameUnit.r_PathPlanRelated3), 0x290);
        CheckOffset(nameof(GameUnit.r_AIState), 0x2BC);
        CheckOffset(nameof(GameUnit.r_LinkedProductionBuildingId), 0x334);
    }

    private static void CheckPolicy()
    {
        OxIdleEpisodePolicy policy = new OxIdleEpisodePolicy();
        OxObservation stuck = Observation(10, 100, 1, 0, 7, 20, 20, 21, 20);
        for (int tick = 1; tick < OxIdleEpisodePolicy.RequiredConsecutiveTicks; tick++)
            Check(policy.Observe(stuck, tick) == OxEpisodeAction.None, "candidate remains pending " + tick);
        Check(policy.Observe(stuck, OxIdleEpisodePolicy.RequiredConsecutiveTicks) == OxEpisodeAction.ConfirmAndRepair,
            "stationary state-one candidate confirms");
        Check(policy.Observe(Observation(10, 100, 2, 0, 0, 20, 20, 21, 20), 51) == OxEpisodeAction.Verified,
            "state one verifies through state two");

        policy = new OxIdleEpisodePolicy();
        OxObservation returnStuck = Observation(11, 101, 3, 0, 9, 30, 31, 30, 32);
        for (int tick = 1; tick <= OxIdleEpisodePolicy.RequiredConsecutiveTicks; tick++)
        {
            OxEpisodeAction expected = tick == OxIdleEpisodePolicy.RequiredConsecutiveTicks
                ? OxEpisodeAction.ConfirmAndRepair
                : OxEpisodeAction.None;
            Check(policy.Observe(returnStuck, tick) == expected, "state-three sequence " + tick);
        }
        Check(policy.Observe(Observation(11, 101, 4, 0, 0, 30, 31, 30, 32), 51) == OxEpisodeAction.Verified,
            "state three verifies through state four");

        policy = new OxIdleEpisodePolicy();
        for (int tick = 1; tick <= 25; tick++)
            policy.Observe(stuck, tick);
        Check(policy.Observe(Observation(10, 100, 1, 0, 7, 21, 20, 21, 20), 26) == OxEpisodeAction.None,
            "movement resets candidate");
        for (int tick = 27; tick < 76; tick++)
            Check(policy.Observe(stuck, tick) == OxEpisodeAction.None, "restarted candidate remains pending " + tick);
        Check(policy.Observe(stuck, 76) == OxEpisodeAction.ConfirmAndRepair, "movement requires a fresh fifty ticks");

        policy = new OxIdleEpisodePolicy();
        for (int tick = 1; tick <= 20; tick++)
            policy.Observe(stuck, tick);
        Check(policy.Observe(Observation(10, 200, 1, 0, 7, 20, 20, 21, 20), 21) == OxEpisodeAction.None,
            "global-id reuse resets candidate");

        policy = new OxIdleEpisodePolicy();
        for (int tick = 1; tick <= OxIdleEpisodePolicy.RequiredConsecutiveTicks; tick++)
            policy.Observe(stuck, tick);
        for (int tick = 51; tick < 50 + OxIdleEpisodePolicy.VerificationTicks; tick++)
            Check(policy.Observe(Observation(10, 100, 1, 0, 0, 20, 20, 21, 20), tick) == OxEpisodeAction.None,
                "verification waits " + tick);
        Check(policy.Observe(Observation(10, 100, 1, 0, 0, 20, 20, 21, 20), 70) == OxEpisodeAction.Unverified,
            "verification times out");
        Check(policy.Observe(stuck, 71) == OxEpisodeAction.None, "failed episode is not repaired twice");

        OxObservation changedTarget = Observation(10, 100, 1, 0, 8, 20, 20, 22, 20);
        for (int tick = 72; tick < 121; tick++)
            Check(policy.Observe(changedTarget, tick) == OxEpisodeAction.None, "new episode remains pending " + tick);
        Check(policy.Observe(changedTarget, 121) == OxEpisodeAction.ConfirmAndRepair,
            "changed target starts a new repair episode");

        Check(!Observation(1, 1, 1, 2, 7, 1, 1, 2, 1).HasIdleBugSignature, "active path is not a candidate");
        Check(!Observation(1, 1, 2, 0, 7, 1, 1, 2, 1).HasIdleBugSignature, "unrelated state is not a candidate");
        Check(!Observation(1, 1, 1, 0, 0, 1, 1, 2, 1).HasIdleBugSignature, "zero marker is not a candidate");
        Check(!Observation(1, 1, 1, 0, 7, 1, 1, 1, 1).HasIdleBugSignature, "exact arrival is not a candidate");
    }

    private static void CheckSourceContracts()
    {
        string workspace = FindWorkspace();
        string plugin = File.ReadAllText(Path.Combine(workspace, "OxTetherIdleFixTest", "src", "OxTetherIdleFixTestPlugin.cs"));
        string runtime = File.ReadAllText(Path.Combine(workspace, "OxTetherIdleFixTest", "src", "OxTetherIdleFixTestRuntime.cs"));
        string helper = File.ReadAllText(Path.Combine(workspace, "Shared", "DebugLogHelper.cs"));
        Check(plugin.Contains("requireCurrentVersion: true"), "native hash mismatch fails closed");
        Check(helper.Contains(ExpectedHash), "shared hash matches test contract");
        Check(plugin.Contains("private static OxTetherIdleFixTestRuntime persistentRuntime;"),
            "runtime is rooted independently of the destroyed plugin component");
        Check(plugin.Contains("OX_IDLE_PLUGIN_COMPONENT_DESTROYED"),
            "startup component destruction is logged");
        string onDestroy = ExtractMethodBody(plugin, "OnDestroy");
        Check(!onDestroy.Contains("Dispose("), "OnDestroy does not dispose the process runtime");
        Check(!onDestroy.Contains("LibraryLoaded -="), "OnDestroy does not remove Script Extender registration");
        Check(!onDestroy.Contains("persistentRuntime = null"), "OnDestroy does not release the runtime root");
        Check(runtime.Contains("unit->r_PathPlanRelated3 = 0;"), "repair clears only the alternate-target marker");
        Check(!runtime.Contains("r_AIState ="), "repair does not force AI state");
        Check(runtime.Contains("OX_IDLE_BUG_CONFIRMED"), "confirmation marker is logged");
        Check(runtime.Contains("OX_IDLE_FIX_APPLIED"), "repair marker is logged");
        Check(runtime.Contains("OX_IDLE_FIX_VERIFIED"), "verification marker is logged");
    }

    private static OxObservation Observation(
        int id, uint global, ushort state, ushort flags, ushort marker,
        ushort x, ushort y, ushort requestedX, ushort requestedY) =>
        new OxObservation(id, global, state, flags, marker, x, y, requestedX, requestedY);

    private static void CheckOffset(string field, int expected) =>
        Check(Marshal.OffsetOf(typeof(GameUnit), field).ToInt32() == expected, field + " offset");

    private static string FindWorkspace()
    {
        DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "OxTetherIdleFixTest")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        string signature = "void " + methodName + "(";
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        if (signatureIndex < 0)
            throw new InvalidOperationException("Method not found: " + methodName);

        int bodyStart = source.IndexOf('{', signatureIndex);
        if (bodyStart < 0)
            throw new InvalidOperationException("Method body not found: " + methodName);

        int depth = 0;
        for (int index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source.Substring(bodyStart + 1, index - bodyStart - 1);
        }

        throw new InvalidOperationException("Unterminated method body: " + methodName);
    }

    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Assertion failed: " + name);
    }
}
