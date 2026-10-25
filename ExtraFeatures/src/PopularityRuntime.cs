using APIShared;
using APIShared.Economy;
using BepInEx.Logging;
using System;
using R3;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed class PopularityRuntime
    {
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly object sync = new object();
        private PopularityOverrideRegistration ordinary;
        private string ordinaryDiagnostic;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private long session;
        private FearPopularityRegistration fear;
        private string fearDiagnostic;

        internal PopularityRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        { this.log = log; this.settings = settings; }

        internal void Register()
        {
            // Each adapter is optional; one failure must not disable independent features.
            IndependentFeatureRegistration.Register(
                () => PopularityOverrides.TryRegister(ExtraFeaturesPlugin.PluginGuid, "popularity-sources", out ordinary, out ordinaryDiagnostic),
                () => IndependentFeatureRegistration.Register(
                    () => FearPopularity.TryRegister(ExtraFeaturesPlugin.PluginGuid, "popularity-fear-levels", out fear, out fearDiagnostic),
                    RegisterMissions, ex => Error("POPULARITY_FEAR_REGISTRATION_FAILED: " + ex)),
                ex => Error("POPULARITY_SOURCE_REGISTRATION_FAILED: " + ex));
        }
        private void RegisterMissions()
        {
            // The shared adapter updates ExtraFeatures' mode gate before these callbacks.
            subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(Initializing));
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(Started));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(Ended));
        }

        private void Initializing(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (session != note.Context.SessionId)
                {
                    Release(); session = note.Context.SessionId;
                }
                // BeforeLoad covers editor and save routes as well as ordinary starts.
                // NativeLoaded refreshes values restored by synchronous save settings handlers.
                if (note.IsBeforeInitialization || note.Phase == MissionInitializationPhase.NativeLoaded)
                    ApplySnapshot();
            }
        }
        private void Started(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (session != note.Context.SessionId) { Release(); session = note.Context.SessionId; }
                ApplySnapshot(); settings.SetPopularityMissionLocked(true);
            }
        }
        private void Ended(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (note.Context.SessionId != session) return;
                Release(); session = 0; settings.SetPopularityMissionLocked(false);
            }
        }
        private void ApplySnapshot()
        {
            bool active = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            try {
                var values = active ? settings.CaptureFearContributions() : null;
                if (fear != null) fear.SetTargetedLevelContributions(values);
                else if (values?.Count > 0) Error("POPULARITY_FEAR_UNAVAILABLE: " + fearDiagnostic);
            } catch (Exception ex) { Error("POPULARITY_FEAR_FAILED: " + ex); }
            try {
                var values = active ? settings.CaptureOrdinaryContributions() : null;
                if (ordinary != null) ordinary.SetContributions(values);
                else if (values?.Count > 0) Error("POPULARITY_SOURCES_UNAVAILABLE: " + ordinaryDiagnostic);
            } catch (Exception ex) { Error("POPULARITY_SOURCES_FAILED: " + ex); }
        }
        internal void RefreshActivation()
        {
            lock (sync)
            {
                if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod)) Release();
            }
        }
        private void Release()
        {
            try { fear?.SetTargetedLevelContributions(null); }
            catch (Exception ex) { Error("POPULARITY_FEAR_RELEASE_FAILED: " + ex); }
            try { ordinary?.SetContributions(null); }
            catch (Exception ex) { Error("POPULARITY_SOURCE_RELEASE_FAILED: " + ex); }
        }
        private void Error(string text) => Shared.DebugLogHelper.LogError(log, text);
    }
}
