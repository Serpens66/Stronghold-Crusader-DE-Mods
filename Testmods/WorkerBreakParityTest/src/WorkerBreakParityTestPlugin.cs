using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace WorkerBreakParityTest
{
    [BepInPlugin("WorkerBreakParityTest_Serp", "Worker Break Parity Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class WorkerBreakParityTestPlugin : BaseUnityPlugin
    {
        private static ManualLogSource persistentLog;
        private static WorkerBreakNativePatch persistentPatch;
        private static bool subscribed;
        private static bool tickLogged;

        private void Awake()
        {
            persistentLog = Logger;
            if (subscribed) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
            Shared.DebugLogHelper.LogInfo(persistentLog,
                "WORKER_BREAK_PARITY_TEST_LOADED: testMod=true; waiting for native library.");
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (persistentPatch != null) return;
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog, "WorkerBreakParityTest", requireCurrentVersion: true))
                    return;
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                persistentPatch = new WorkerBreakNativePatch(persistentLog, context);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(persistentLog,
                    "WORKER_BREAK_PARITY_TEST_INACTIVE: Vanilla remains active: " + exception);
            }
        }

        private static void OnTick(int tick)
        {
            if (tickLogged || persistentPatch == null) return;
            tickLogged = true;
            Shared.DebugLogHelper.LogInfo(persistentLog,
                "WORKER_BREAK_PARITY_TEST_TICK_AFTER_STARTUP: native patch active; tick=" + tick);
        }
    }
}
