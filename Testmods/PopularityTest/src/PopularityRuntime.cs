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

        internal PopularityRuntime(ManualLogSource log, PopularitySettings settings)
        { this.log = log; this.settings = settings; overrides = new OverrideBatch(message => Error("POPULARITY_OWNERSHIP " + message)); }

        internal void Register()
        {
            if (!FearPopularity.TryRegister(Plugin.Guid, "fear-multiplier", out fear, out var reason))
                throw new InvalidOperationException(reason);
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
                Info("POPULARITY_SESSION session=" + session + "; kind=" + note.Context.StartKind +
                    "; overrides=" + overrides.Count);
            }
        }
        private void Ended(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (note.Context.SessionId != session) return;
                Release(); session = 0; settings.SetMissionLocked(false);
                Info("POPULARITY_END reason=" + note.EndReason + "; own overrides released.");
            }
        }
        private void ApplySnapshot()
        {
            fear.SetMultiplier(settings.EnableMod && settings.GoodBadThingPopModifierMultiplierEnabled
                ? (decimal?)settings.GoodBadThingPopModifierMultiplier : null);
            var desired = new OverrideRequest[PopularityCatalog.All.Length];
            for (int i = 0; i < desired.Length; i++)
            {
                var parameter = PopularityCatalog.All[i];
                int? value = settings.EnableMod && parameter.Enabled(settings)
                    ? (int?)checked(parameter.Read(settings) * PopularityCatalog.UnitsPerPoint) : null;
                desired[i] = new OverrideRequest(parameter.Name, value,
                    () => new ValueAdapter(parameter.Resolve(GameGlobalsManager.Instance)));
            }
            try
            {
                if (!overrides.Apply(desired)) return;
            }
            catch (Exception ex)
            {
                Release();
                Error("POPULARITY_APPLY_FAILED own overrides released: " + ex);
            }
        }
        private void Release() { fear?.SetMultiplier(null); overrides.Release(); }
        private void Info(string text) => log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + text);
        private void Error(string text) => log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + text);
    }
}
