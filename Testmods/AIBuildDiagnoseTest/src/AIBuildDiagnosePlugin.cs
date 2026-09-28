using APIShared;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AIBuildDiagnoseTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIBuildDiagnosePlugin : BaseUnityPlugin
    {
        public const string Guid = "AIBuildDiagnoseTest_Serp";
        public const string Name = "AI Build Diagnose Test";
        public const string Version = "0.1.0";
        private static ManualLogSource log;
        private static AIBuildDiagnoseRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool registered;

        private void Awake()
        {
            log = Logger;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; readOnly=true.");
            if (registered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                var candidate = new AIBuildDiagnoseRuntime(log,
                    Chainloader.PluginInfos.ContainsKey("fixes"));
                // Publisher subscriptions and static fields survive SHCDE startup cleanup.
                IDisposable candidateSession = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                if (!AiBuildDiagnostic.TryRegister(Guid, candidate.OnNativeRecord, out string error))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_SCHEDULER_HOOK_UNAVAILABLE: " + error);
                sessionSubscription = candidateSession;
                runtime = candidate;
                Shared.DebugLogHelper.LogInfo(log,
                    "AI_BUILD_DIAGNOSTIC_READY: publisher=GameTimeManagerAPI.OnTick; nativeObserver=" +
                    AiBuildDiagnostic.HasObserver + ".");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI build diagnostic initialization failed: " + ex);
            }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
