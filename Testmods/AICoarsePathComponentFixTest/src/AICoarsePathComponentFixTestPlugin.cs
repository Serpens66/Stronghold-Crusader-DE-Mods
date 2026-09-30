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
        private static WoodSiteGuardExperiment woodGuard;
        private static bool woodGuardRequested;
        private static string woodGuardScope;

        private void Awake()
        {
            log = Logger;
            if (subscribed) return;
            isolationMode = Config.Bind("Isolation", "Mode", 0,
                "0=safe generation-only tick observer; 1=original-only native detour; 2=detour+cache notification; " +
                "3=detour+cache+calculation; 4=detour+cache+calculation+diagnostic publisher. " +
                "5=tick-only calculation and publisher without native detour. " +
                "Modes 1-5 are for an expendable save copy only.").Value;
            woodGuardRequested = Config.Bind("WoodSiteGuard", "Enabled", false,
                "Opt-in native wood-only candidate guard. Disable AIBuildDiagnoseTest NearbyWoodTest first; " +
                "test on the disposable Canari save copy and the Rat control map before using an original save.").Value;
            woodGuardScope = Config.Bind("WoodSiteGuard", "Scope", "CopyOnly",
                "CopyOnly enables the guard only for test_canari_nowoodcutters_probe.sav; " +
                "RatControl enables it only for a new game on spezialist 3vs5.map.").Value;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version +
                $" loaded; isolationMode={isolationMode}; woodGuardRequested={woodGuardRequested}; " +
                $"woodGuardScope={woodGuardScope}; coarseWrites=0.");
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
            if (woodGuardRequested && woodGuard == null)
            {
                try
                {
                    if (isolationMode != 0)
                        throw new InvalidOperationException("Wood guard requires safe isolation mode 0.");
                    woodGuard = new WoodSiteGuardExperiment(log,
                        unchecked((ulong)context.ModuleHandle.ToInt64()), woodGuardScope);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(log, "AI_WOOD_SITE_GUARD_UNAVAILABLE: " + ex);
                }
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
            try { woodGuard?.OnTick(tick); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI_WOOD_SITE_GUARD_TICK_FAILED: " + ex);
            }
        }
    }
}
