using APIShared;
using APIShared.Economy;
using BepInEx.Logging;
using RedBird.Core.Memory.Managed;
using SHCDESE.GameGlobals;
using System;

namespace PopularityTest
{
    internal sealed class PopularityRuntime
    {
        private sealed class ValueAdapter : IOverrideValue
        {
            private readonly ManagedValue<int> source;
            internal ValueAdapter(ManagedValue<int> source) { this.source = source; }
            public int Value => source.GetValue();
            public int Depth => source.StackDepth;
            public void Push(int value) => source.Push(value);
            public void Pop() => source.Pop();
        }
        private readonly ManualLogSource log;
        private readonly PopularitySettings settings;
        private readonly object sync = new object();
        private readonly OverrideBatch overrides;
        private long session;
        private FearPopularityRegistration fear;
        private string fearDiagnostic;

        internal PopularityRuntime(ManualLogSource log, PopularitySettings settings)
        { this.log = log; this.settings = settings; overrides = new OverrideBatch(message => Error("POPULARITY_OWNERSHIP " + message)); }

        internal void Register()
        {
            // An optional fear adapter must not disable independent SE parameters.
            IndependentFeatureRegistration.Register(
                () => FearPopularity.TryRegister(Plugin.Guid, "fear-multiplier", out fear, out fearDiagnostic),
                RegisterMissions,
                ex => { fearDiagnostic = ex.ToString(); Error("POPULARITY_FEAR_REGISTRATION_FAILED: " + ex); });
        }
        private void RegisterMissions()
        {
            var api = ApiShared.ForMod(Plugin.Guid);
            if (!api.TryGetMissionLifecycle(out var missions, out var diagnostic) ||
                !missions.TryRegisterObserver("popularity-session", Started, Ended, Initializing, out diagnostic))
                throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
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
                ApplySnapshot(); settings.SetMissionLocked(true);
            }
        }
        private void Ended(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (note.Context.SessionId != session) return;
                Release(); session = 0; settings.SetMissionLocked(false);
            }
        }
        private void ApplySnapshot()
        {
            bool requested = settings.EnableMod && (settings.NegativeFearEnabled || settings.PositiveFearEnabled);
            try {
                if (fear != null) {
                    fear.SetLevelContributions(requested ? settings.CaptureFearContributions() : null);
                } else if (requested) Shared.RuntimeHealth.Report("fear-registration", false,
                    fearDiagnostic + "; independent popularity overrides continue.");
            } catch (Exception ex) { Error("POPULARITY_FEAR_FAILED: " + ex); }
            try {
            var desired = new OverrideRequest[PopularityCatalog.All.Length];
            for (int i = 0; i < desired.Length; i++)
            {
                var parameter = PopularityCatalog.All[i];
                int? value = settings.EnableMod && parameter.Enabled(settings)
                    ? (int?)checked(parameter.Read(settings) * PopularityCatalog.UnitsPerPoint) : null;
                desired[i] = new OverrideRequest(parameter.Name, value,
                    () => new ValueAdapter(parameter.Resolve(GameGlobalsManager.Instance)));
            }
                if (!overrides.Apply(desired)) return;
            }
            catch (Exception ex)
            {
                overrides.Release();
                Error("POPULARITY_APPLY_FAILED own overrides released: " + ex);
            }
        }
        private void Release()
        {
            fear?.SetLevelContributions(null);
            bool released = overrides.Release();
            if (!released) Shared.RuntimeHealth.Report("popularity-release", true,
                "Some owned layers could not be released; see ownership errors. session=" + session);
        }
        private void Error(string text) => Shared.DebugLogHelper.LogError(log, text);
    }
}
