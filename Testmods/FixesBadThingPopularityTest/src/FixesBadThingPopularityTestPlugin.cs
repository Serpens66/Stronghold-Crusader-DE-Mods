using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace FixesBadThingPopularityTest
{
    [BepInDependency("000shcdese", "2.13.0")]
    [BepInDependency("APIShared_Serp", "0.4.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class FixesBadThingPopularityTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "FixesBadThingPopularityTest_Serp";
        public const string Name = "FixesBadThingPopularityTest";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static FixesDiagnostics.ObserverRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool libraryRegistered;

        private void Awake()
        {
            log = Logger;
            if (!Config.Bind("Diagnostic", "Enabled", false, "Enable the passive observer for an isolated Fixes 1.24 evidence run; restart required.").Value)
            {
                Shared.DebugLogHelper.LogInfo(log, Name + " disabled; enable Diagnostic.Enabled in the plugin config for an evidence run.");
                return;
            }
            Shared.DebugLogHelper.LogInfo(log, $"{Name} {Version} loaded; waiting for native library.");
            if (libraryRegistered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            libraryRegistered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true))
                return;

            IDisposable candidateSubscription = null;
            bool candidateTickRegistered = false;
            FixesDiagnostics.ObserverRuntime candidate = null;
            try
            {
                candidate = new FixesDiagnostics.ObserverRuntime(log, Guid, false, context.ModuleHandle);
                candidateSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                candidateTickRegistered = true;

                // The extender's publisher and these static fields outlive startup cleanup.
                Shared.DebugLogHelper.LogInfo(log,
                    "FIXES_EVIDENCE_DIAGNOSTIC_READY: readOnly=true, publisher=GameTimeManagerAPI.OnTick.");
                sessionSubscription = candidateSubscription;
                candidate.MarkPublished();
                runtime = candidate;
            }
            catch (Exception ex)
            {
                if (candidateTickRegistered)
                    GameTimeManagerAPI.Instance.OnTick -= OnTick;
                candidate?.RollbackUnpublished();
                candidateSubscription?.Dispose(); // Only an unpublished initialization candidate.
                Shared.DebugLogHelper.LogError(log, "Fixes evidence initialization failed: " + ex);
            }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
