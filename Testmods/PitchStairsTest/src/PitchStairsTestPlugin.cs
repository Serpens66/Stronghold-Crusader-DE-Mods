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
    // Loads after the Fixes mod when present. Fixes patches none of these sites; loading later means
    // that any future overlap shows up as a live-byte mismatch and leaves Vanilla in place.
    // info.json uses NetworkMode 1 because the stored heights change pathing and fire, so every
    // player in a lobby needs the mod.
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class PitchStairsTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "PitchStairsTest_Serp";
        public const string Name = "Pitch Stairs Test";
        public const string Version = "0.1.1";

        // Rooted for process lifetime: the plugin component is destroyed by the SHCDE startup cleanup.
        private static ManualLogSource log;
        private static PitchStairsRuntime runtime;
        private static IDisposable started, ended;
        private static bool subscribed, nativeAttempted;

        private void Awake()
        {
            log = Logger;
            if (subscribed) return;
            subscribed = true;
            Shared.DevDiag.Attach(log);
            // Logged first: LibraryLoaded can fire synchronously inside the subscription.
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; waiting for CrusaderLibrary.LibraryLoaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (nativeAttempted) return;
            nativeAttempted = true;
            var candidate = new PitchStairsRuntime(log);
            try
            {
                candidate.Install(context, Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true));
            }
            catch (Exception ex)
            {
                LogErrorSafely("PITCH_STAIRS_UNAVAILABLE: Vanilla pitch heights remain active: " + ex);
            }
            finally
            {
                // Root even if installation fails its post-commit check or logging throws.
                if (candidate.Published) runtime = candidate;
            }
            // A published candidate stays rooted even when its post-commit validation disabled it.
            if (!candidate.Published) return;
            try
            {
                started = Shared.MissionEvents.Started.Subscribe(OnStarted);
                ended = Shared.MissionEvents.Ended.Subscribe(OnEnded);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "PITCH_STAIRS_SESSION_LOG_UNAVAILABLE: " + ex);
            }
        }

        private static void LogErrorSafely(string message)
        {
            try { Shared.DebugLogHelper.LogError(log, message); }
            catch { /* Logging must not retire a published native runtime or escape into the loader. */ }
        }

        private static void OnStarted(MissionLifecycleNotification notification)
        {
            try { runtime?.OnMissionStarted(notification); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PITCH_STAIRS_SESSION_FAILED: " + ex); }
        }

        private static void OnEnded(MissionLifecycleNotification notification)
        {
            try { runtime?.OnMissionEnded(notification); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PITCH_STAIRS_SESSION_END_FAILED: " + ex); }
        }
    }
}
