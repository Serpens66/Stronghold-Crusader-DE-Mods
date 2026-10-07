using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace GatehouseLivingCaptureTest
{
    [BepInPlugin("GatehouseLivingCaptureTest_Serp", "Gatehouse Living Capture Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("APIShared_Serp", "0.4.10")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class GatehouseLivingCaptureTestPlugin : BaseUnityPlugin
    {
        // LibraryLoaded roots initialization; the static runtime roots the native callback forever.
        private static CaptureRuntime runtime;
        private static ManualLogSource persistentLog;
        private static bool subscribed;

        private void Awake()
        {
            persistentLog = Logger;
            if (subscribed) return;
            subscribed = true;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            runtime = new CaptureRuntime(persistentLog);
            try { runtime.Install(context); }
            catch (Exception ex)
            {
                persistentLog.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] GATEHOUSE_LIVING_CAPTURE_DISABLED: {ex}");
            }
        }
    }
}
