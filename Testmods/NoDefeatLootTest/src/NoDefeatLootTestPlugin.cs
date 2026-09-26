using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace NoDefeatLootTest
{
    [BepInDependency("000shcdese", "2.10.1")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, "No Defeat Loot Test", Version)]
    public sealed class NoDefeatLootTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "NoDefeatLootTest_Serp";
        public const string Version = "0.1.0";
        private static ManualLogSource persistentLog;
        private static DefeatLootHook hook;
        private static bool tickSubscribed;
        private static int tickMarkerLogged;
        private static int lastReportedSuppressed;
        private static int lastReportedErrors;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(persistentLog, "NO_DEFEAT_LOOT_TEST awake version=" + Version);
            // The Script Extender publisher survives SHCDE's plugin-component cleanup.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (hook != null)
                return;
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        persistentLog, "No Defeat Loot Test", requireCurrentVersion: true))
                    return;

                if (!tickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick += OnTick;
                    tickSubscribed = true;
                }
                DefeatLootHook candidate = new DefeatLootHook(persistentLog);
                candidate.Install(context);
                hook = candidate;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(persistentLog,
                    "NO_DEFEAT_LOOT_TEST install failed; Vanilla remains active: " + exception);
            }
        }

        private static void OnTick(int tick)
        {
            DefeatLootHook active = hook;
            if (active == null)
                return;
            if (Interlocked.Exchange(ref tickMarkerLogged, 1) == 0)
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    "NO_DEFEAT_LOOT_TEST post-cleanup tick=" + tick + " nativeHook=active");

            int suppressed = active.SuppressedCount;
            int errors = active.CallbackErrorCount;
            if (suppressed != lastReportedSuppressed || errors != lastReportedErrors)
            {
                lastReportedSuppressed = suppressed;
                lastReportedErrors = errors;
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    "NO_DEFEAT_LOOT_TEST tick=" + tick + " suppressed=" + suppressed +
                    " callbackErrors=" + errors);
            }
        }
    }
}
