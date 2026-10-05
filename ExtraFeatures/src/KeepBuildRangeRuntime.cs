using System;
using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;

namespace ExtraFeatures
{
    // Process-rooted by ExtraFeaturesRuntime; subscriptions survive startup and map changes.
    internal sealed class KeepBuildRangeRuntime
    {
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly KeepBuildRangeOverride range = new KeepBuildRangeOverride();
        private IDisposable initializationSubscription;
        private IDisposable endSubscription;
        private bool callbackLogged;
        private bool errorLogged;

        internal KeepBuildRangeRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        internal void Initialize()
        {
            Shared.MissionEvents.SetOwner(ExtraFeaturesPlugin.PluginGuid);
            if (initializationSubscription == null)
                initializationSubscription = Shared.MissionEvents.Initialization.Subscribe(OnInitialization);
            if (endSubscription == null)
                endSubscription = Shared.MissionEvents.Ended.Subscribe(_ => Refresh());
            Refresh();
        }

        private void OnInitialization(MissionLifecycleNotification notification)
        {
            // The publisher updates the mode gate first. BeforeNativeStart precedes prebuilt AIVs;
            // NativeLoaded also covers saves/editor routes which omit the new-game checkpoint.
            Refresh();
            if (!callbackLogged)
            {
                callbackLogged = true;
                try
                {
                    Shared.DebugLogHelper.LogDebug(log,
                        "Keep build range lifecycle active after startup: phase=" + notification.Phase + ".");
                }
                catch { /* Diagnostics must not interrupt mission callbacks. */ }
            }
        }

        internal int PreviewLobby(int currentOverride)
        {
            return range.Preview(settings.EnableMod ? settings.KeepBuildRange : -1, currentOverride);
        }

        internal void Refresh()
        {
            try
            {
                int requested = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod)
                    ? settings.KeepBuildRange : -1;
                var value = GameBuildingManagerAPI.Instance.KeepProximityOverride;
                range.Reconcile(requested, value.GetValue, value.SetValue);
                errorLogged = false;
            }
            catch (Exception ex)
            {
                // Retry application/restoration, but report only the first failure
                // until reconciliation succeeds again.
                if (!errorLogged)
                {
                    errorLogged = true;
                    try { Shared.DebugLogHelper.LogError(log, "Keep build range update failed: " + ex); }
                    catch { /* Logging must not escape the settings/event path. */ }
                }
            }
        }
    }
}
