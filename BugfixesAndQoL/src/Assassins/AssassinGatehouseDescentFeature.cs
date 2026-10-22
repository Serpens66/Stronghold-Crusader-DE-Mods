using System;
using System.Collections.Generic;
using APIShared;
using APIShared.GateRouting.Native;
using BepInEx.Logging;
using R3;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.GateRouting
{
    // Owned by the direct gatehouse-climbing setting, not the enemy routing policy.
    // Static session subscriptions and the permanent native callback survive startup cleanup.
    internal static class AssassinGatehouseDescentFeature
    {
        private static readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private static AssassinGateDescentRuntime runtime;
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static bool map, initialized;

        internal static void Initialize(CrusaderLibraryLoadContext context, ManualLogSource logger,
            BugfixesAndQoLViewModel options, bool compatible)
        {
            if (initialized || !compatible) return;
            // Same owner/registration as routing, but requesting this backend installs no
            // routing hooks. Only InstallDescent is requested by this feature.
            if (!GateRoutingNativeHooks.TryRequest(BugfixesAndQoLPlugin.PluginGuid, "gate-routing",
                context, logger, out var backend, out string reason))
                throw new InvalidOperationException(reason);
            var candidate = new AssassinGateDescentRuntime(context, backend);
            runtime = candidate; settings = options; log = logger; initialized = true;
            subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log, c =>
            {
                map = true; runtime.ResetCounters(); Refresh();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebug(log, "Assassin gatehouse descent ready: owner=EnableAssassinGatehouseClimbFix; gatePolicyIndependent=True; enabled=" + Enabled);
            }));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_ =>
            {
                bool wasMap = map; map = false; Refresh();
                if (wasMap) Shared.DebugLogHelper.LogDebug(log, runtime.Summary);
            }));
            Refresh();
        }
        private static bool Enabled => map && settings != null && settings.EnableMod &&
            settings.EnableAssassinGatehouseClimbFix && AssassinPathAPI.DirectGatehouseClimbingEnabled;
        internal static void Refresh() { runtime?.SetActive(Enabled); }
        // TEMP_GATE_MIGRATION_DIAGNOSTICS: optional observers may be detached without
        // disabling the function. Native owners and executable memory never change.
        internal static void SetObservers(Action<IntPtr, bool> clear, Action<int> exception)
        { runtime?.SetObservers(clear, exception); }
    }
}
