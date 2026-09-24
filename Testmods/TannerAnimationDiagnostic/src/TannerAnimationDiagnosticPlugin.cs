using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace TannerAnimationDiagnostic
{
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SkinTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("TannerAnimationDiagnostic_Serp", "Tanner Animation Diagnostic", "0.1.0")]
    public sealed class TannerAnimationDiagnosticPlugin : BaseUnityPlugin
    {
        private static ManualLogSource persistentLog;
        private static TannerAnimationDiagnosticRuntime runtime;
        private static bool subscribed;

        private void Awake()
        {
            persistentLog = Logger;
            persistentLog.LogInfo($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_DIAG_LOADED version=0.1.0");
            if (subscribed)
                return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;
            try
            {
                // A static root survives SHCDE's normal startup destruction of the plugin component.
                var candidate = new TannerAnimationDiagnosticRuntime(persistentLog);
                candidate.Initialize(context);
                runtime = candidate;
                persistentLog.LogInfo($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_DIAG_READY waiting_for_post_cleanup_render=true");
            }
            catch (Exception ex)
            {
                persistentLog.LogError($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_DIAG_INIT_FAILED {ex}");
            }
        }
    }
}
