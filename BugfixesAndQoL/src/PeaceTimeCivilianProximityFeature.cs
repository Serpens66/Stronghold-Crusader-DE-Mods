using System;
using System.Threading;
using APIShared.Buildings;
using APIShared.Simulation;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace BugfixesAndQoL
{
    // Process-lived via the static plugin field, settings publisher and central mission subscriptions.
    // No native hook, timer or executable-memory mutation belongs to this consumer.
    internal sealed class PeaceTimeCivilianProximityFeature
    {
        private const string ExtraFeaturesGuid = "ExtraFeatures_Serp";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly EnemyProximityRegistration registration;
        private readonly APIShared.IMissionLifecycleCapability missions;
        private readonly IDisposable missionSubscription;
        private Session session;
        private volatile bool compatible;
        private bool compatibilityWarningLogged;
        private bool availabilityWarningLogged;

        internal PeaceTimeCivilianProximityFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!APIShared.ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid).TryGetMissionLifecycle(
                out missions, out APIShared.NativeCapabilityDiagnostic missionDiagnostic))
                throw new InvalidOperationException("Peace-time civilian proximity needs mission lifecycle: " + missionDiagnostic?.Reason);
            if (!EnemyProximityPolicies.TryRegister(BugfixesAndQoLPlugin.PluginGuid,
                "peace-time-civilian-proximity", EnemyProximityPhase.Limit, Resolve,
                out registration, out string reason))
                throw new InvalidOperationException("Peace-time civilian proximity registration failed: " + reason);
            settings.SettingChanged += Reconcile;
            Reconcile(null);
            missionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnStarted, OnEnded);
        }

        // Host activation is independent of local client features and the SP peace-extension switch.
        private bool DesiredEnabled => settings.EnableMod && settings.EnablePeaceTimeCivilianProximityReduction;

        private void Reconcile(string property)
        {
            if (property != null && property != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                property != nameof(BugfixesAndQoLViewModel.EnablePeaceTimeCivilianProximityReduction)) return;
            compatible = PeaceTimeCivilianProximityPolicy.CanCoexist(
                Chainloader.PluginInfos.ContainsKey(ExtraFeaturesGuid),
                EnemyProximityPolicies.HasRegistration(ExtraFeaturesGuid, "enemy-proximity"));
            if (!compatible && DesiredEnabled && !compatibilityWarningLogged)
            {
                compatibilityWarningLogged = true;
                Shared.DebugLogHelper.LogWarning(log,
                    "PEACE_CIVILIAN_PROXIMITY_COMPATIBILITY: ExtraFeatures did not register its proximity rules with APIShared; " +
                    "only the peace-time civilian reduction is inactive. Update ExtraFeatures or resolve its registration failure.");
            }
            if (DesiredEnabled && !availabilityWarningLogged &&
                (EnemyProximityPolicies.QueryDiagnostic.State != APIShared.NativeCapabilityState.Available ||
                 PeaceTimeState.Diagnostic.State != APIShared.NativeCapabilityState.Available))
            {
                availabilityWarningLogged = true;
                Shared.DebugLogHelper.LogWarning(log,
                    "PEACE_CIVILIAN_PROXIMITY_UNAVAILABLE: " + EnemyProximityPolicies.QueryDiagnostic.Reason +
                    "; peace state: " + PeaceTimeState.Diagnostic.Reason + ". Existing building rules remain active.");
            }
            Session current = Volatile.Read(ref session);
            registration.SetEnabled(current != null && current.Enabled && compatible);
        }

        private void OnStarted(Shared.GameplaySessionStartedContext context)
        {
            // Settings have passed through the authoritative mission load before this completed-start notification.
            Volatile.Write(ref session, new Session(context.SessionId, DesiredEnabled));
            settings.SetPeaceTimeCivilianMissionLocked(true);
            Reconcile(null);
            Shared.DebugLogHelper.LogInfo(log,
                "PEACE_CIVILIAN_PROXIMITY_SESSION: session=" + context.SessionId +
                "; enabled=" + DesiredEnabled + "; compatible=" + compatible + ".");
        }

        private void OnEnded()
        {
            Volatile.Write(ref session, null);
            registration.SetEnabled(false);
            settings.SetPeaceTimeCivilianMissionLocked(false);
        }

        private EnemyProximityDecision Resolve(EnemyProximityContext context)
        {
            Session current = Volatile.Read(ref session);
            if (current == null || !current.Enabled || !compatible ||
                missions.Current?.SessionId != current.Id) return EnemyProximityDecision.None;
            bool available = PeaceTimeState.TryGetActive(out bool active);
            if (!ReferenceEquals(current, Volatile.Read(ref session)) ||
                missions.Current?.SessionId != current.Id) return EnemyProximityDecision.None;
            return PeaceTimeCivilianProximityPolicy.ShouldLimit(true, current.Enabled, true,
                compatible, available, active)
                ? new EnemyProximityDecision(civilianRadius: PeaceTimeCivilianProximityPolicy.MaximumCivilianRadius)
                : EnemyProximityDecision.None;
        }

        private sealed class Session
        {
            internal readonly long Id;
            internal readonly bool Enabled;
            internal Session(long id, bool enabled) { Id = id; Enabled = enabled; }
        }
    }
}
