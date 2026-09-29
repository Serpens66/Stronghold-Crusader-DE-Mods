using APIShared;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BugfixesAndQoL;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AICoarsePathComponentFixTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AICoarsePathComponentFixTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "AICoarsePathComponentFixTest_Serp";
        public const string Name = "AI Coarse Path Component Fix Test";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static AiCoarsePathComponentFix runtime;
        private static bool subscribed;
        private static int isolationMode;
        private static bool tickSubscribed;

        private void Awake()
        {
            log = Logger;
            if (subscribed) return;
            isolationMode = Config.Bind("Isolation", "Mode", 0,
                "0=safe generation-only tick observer; 1=original-only native detour; 2=detour+cache notification; " +
                "3=detour+cache+calculation; 4=detour+cache+calculation+diagnostic publisher. " +
                "5=tick-only calculation and publisher without native detour. " +
                "Modes 1-5 are for an expendable save copy only.").Value;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version +
                $" loaded; isolationMode={isolationMode}; observationOnly=True; writes=0.");
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                var candidate = new AiCoarsePathComponentFix(log, () => true, context,
                    Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name,
                        requireCurrentVersion: true), isolationMode);
                BugfixesAndQoLRuntime.AiEconomyOverlayRestored += OnOverlayRestored;
                runtime = candidate;
                if (!tickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick += OnTick;
                    tickSubscribed = true;
                }
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_COARSE_PCL_TEST_READY: publisher=GameTimeManagerAPI.OnTick; " +
                    $"isolationMode={isolationMode}; nativeHook={isolationMode >= 1 && isolationMode <= 4}; observationOnly=True; writes=0.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_TEST_UNAVAILABLE: " + ex);
            }
        }

        private static void OnOverlayRestored()
        {
            try { runtime?.FlushDeferred(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_TEST_OVERLAY_CALLBACK_FAILED: " + ex);
            }
        }

        private static void OnTick(int tick)
        {
            try { runtime?.OnTick(tick); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_TEST_TICK_FAILED: " + ex);
            }
        }
    }
}
