using APIShared;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API.LowLevel;
using System;

namespace PitchStairsTest
{
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.23.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class PitchStairsTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "PitchStairsTest_Serp";
        public const string Name = "Pitch Stairs Test";
        public const string Version = "0.1.0";

        // Rooted for process lifetime: the plugin component is destroyed by the SHCDE startup cleanup.
        private static ManualLogSource log;
        private static PitchStairsRuntime runtime;
        private static IDisposable started;
        private static bool subscribed;

        private void Awake()
        {
            log = Logger;
            if (subscribed) return;
            Shared.DevDiag.Attach(log);
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; waiting for CrusaderLibrary.LibraryLoaded.");
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            var candidate = new PitchStairsRuntime(log);
            try
            {
                candidate.Install(context, Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "PITCH_STAIRS_UNAVAILABLE: Vanilla pitch heights remain active: " + ex);
            }
            // A published candidate stays rooted even when its post-commit validation disabled it.
            if (!candidate.Published) return;
            runtime = candidate;
            try
            {
                started = Shared.MissionEvents.Started.Subscribe(OnStarted);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "PITCH_STAIRS_SESSION_LOG_UNAVAILABLE: " + ex);
            }
        }

        private static void OnStarted(MissionLifecycleNotification notification)
        {
            try { runtime?.OnMissionStarted(notification); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PITCH_STAIRS_SESSION_FAILED: " + ex); }
        }
    }
}
