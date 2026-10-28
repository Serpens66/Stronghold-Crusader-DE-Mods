using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace LordTeleportGuardTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.18.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class LordTeleportGuardPlugin : BaseUnityPlugin
    {
        public const string Guid = "LordTeleportGuardTest_Serp";
        public const string Name = "Lord Teleport Guard Test";
        public const string Version = "0.1.0";
        private static ManualLogSource log;
        private static LordTeleportGuardRuntime runtime;
        private static bool subscribed;
        private static bool observerFailureLogged;

        private void Awake()
        {
            if (subscribed) return;
            log = Logger;
            Shared.DevDiag.Attach(log);
            // Never unsubscribe on Unity cleanup: the native hooks and these publishers live to process exit.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; gameplaySynchronized=True; allModes=True.");
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true))
                    throw new InvalidOperationException("Unsupported native hash; human Lord teleport protection is unavailable.");
                runtime = new LordTeleportGuardRuntime(log, context);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                runtime.ReportInstalled();
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "LORD_TELEPORT_GUARD_UNAVAILABLE: " + error);
            }
        }

        private static void OnTick(int tick)
        {
            try { runtime?.OnTick(tick); }
            catch (Exception error)
            {
                if (!observerFailureLogged)
                {
                    observerFailureLogged = true;
                    Shared.DebugLogHelper.LogError(log, "LORD_TELEPORT_GUARD_OBSERVER_FAILED: " + error);
                }
            }
        }
    }
}
