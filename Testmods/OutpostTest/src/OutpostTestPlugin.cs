using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace OutpostTest
{
    [BepInPlugin(Guid, "Outpost Test", Version)]
    [BepInDependency("000shcdese", "2.6.0")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    public sealed class OutpostTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "OutpostTest_Serp", Version = "0.1.0";
        private static ManualLogSource persistentLog;
        private static VanillaObserver runtime;
        private static IMissionLifecycleCapability lifecycle;
        private static bool subscribed;

        private void Awake()
        {
            persistentLog = Logger;
            if (subscribed) return;
            subscribed = true;
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
            Shared.DebugLogHelper.LogInfo(persistentLog, "OutpostTest 0.1.0: Vanilla observation; custom spawning and suppression disabled; no native hook installed.");
        }

        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            VanillaObserver candidate = null;
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(persistentLog, "OutpostTest", true)) return;
                if (!ApiShared.Current.TryGetMissionLifecycle(Guid, out lifecycle, out var diagnostic))
                    throw new InvalidOperationException("APIShared mission lifecycle: " + diagnostic?.Reason);
                candidate = new VanillaObserver(persistentLog);
                // Root before publishing callbacks. No normal Unity teardown owns it.
                runtime = candidate;
                candidate = null;
                if (!lifecycle.TryRegisterObserver("OutpostTest.Runtime", runtime.Begin, runtime.End, null, out diagnostic))
                    throw new InvalidOperationException("APIShared observer: " + diagnostic?.Reason);
                GameTimeManagerAPI.Instance.OnTick += Tick;
            }
            catch (Exception ex)
            {
                runtime?.Disable("initialization failure");
                Shared.DebugLogHelper.LogError(persistentLog, "OutpostTest initialization failed; Vanilla active: " + ex);
            }
        }
        private static void Tick(int tick) => runtime?.Tick(tick);
    }
}
