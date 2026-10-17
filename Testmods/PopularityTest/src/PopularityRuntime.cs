using APIShared;
using BepInEx.Logging;
using R3;
using RedBird.Core.Memory.Managed;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using SHCDESE.GameGlobals;
using System;
using System.Collections.Generic;

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
        private IDisposable calculationSubscription;
        private long session;
        private bool started, callbackLogged;
        private bool[] appliedParameters;
        private PopularityPresentation presentation;

        internal PopularityRuntime(ManualLogSource log, PopularitySettings settings)
        { this.log = log; this.settings = settings; overrides = new OverrideBatch(message => Error("POPULARITY_OWNERSHIP " + message)); }

        internal void Register()
        {
            var api = ApiShared.ForMod(Plugin.Guid);
            if (!api.TryGetMissionLifecycle(out var missions, out var diagnostic) ||
                !missions.TryRegisterObserver("popularity-session", Started, Ended, Initializing, out diagnostic))
                throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
            calculationSubscription = PlayerR3EventHooks.OnPlayerCalculatePopularity.Observable.Subscribe(Calculated);
            presentation = new PopularityPresentation(CapturePresentationValues, Info, Error);
            presentation.Register();
            Info("POPULARITY_READY parameters=41; durable publishers=APIShared missions,SE popularity; no native hooks.");
        }

        private void Initializing(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (session != note.Context.SessionId)
                {
                    Release(); session = note.Context.SessionId; started = false; callbackLogged = false;
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
                ApplySnapshot(); started = true; settings.SetMissionLocked(true);
                Info("POPULARITY_SESSION session=" + session + "; kind=" + note.Context.StartKind +
                    "; mode=" + note.Context.Mode.ToDiagnosticString() + "; overrides=" + overrides.Count);
            }
        }
        private void Ended(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (note.Context.SessionId != session) return;
                Release(); started = false; session = 0; settings.SetMissionLocked(false);
                Info("POPULARITY_END reason=" + note.EndReason + "; own overrides released.");
            }
        }
        private void ApplySnapshot()
        {
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
                appliedParameters = Array.ConvertAll(desired, request => request.Value.HasValue);
                foreach (var request in desired)
                {
                    if (request.Value.HasValue)
                        Info("POPULARITY_APPLY " + request.Name + "=" + request.Value.Value + " internal; points=" + request.Value.Value / PopularityCatalog.UnitsPerPoint);
                }
            }
            catch (Exception ex)
            {
                Release();
                Error("POPULARITY_APPLY_FAILED own overrides released: " + ex);
            }
        }
        private void Release() { appliedParameters = null; overrides.Release(); }
        private KeyValuePair<long, IReadOnlyDictionary<string, int>> CapturePresentationValues()
        {
            lock (sync)
            {
                if (!started || appliedParameters == null || overrides.Count == 0) return default;
                var values = new Dictionary<string, int>();
                for (int i = 0; i < appliedParameters.Length; i++)
                    if (appliedParameters[i])
                    {
                        var parameter = PopularityCatalog.All[i];
                        // Read the effective top of the SE stack, including a later foreign overlay.
                        values.Add(parameter.Name, parameter.Resolve(GameGlobalsManager.Instance).GetValue());
                    }
                return new KeyValuePair<long, IReadOnlyDictionary<string, int>>(session, values);
            }
        }
        private void Calculated(PlayerCalculatePopularityEventArgs args)
        {
            lock (sync)
            {
                if (!started || callbackLogged || args.Phase != EventHookPhase.Pre) return;
                callbackLogged = true;
                Info("POPULARITY_CALLBACK session=" + session + "; player=" + args.PlayerId +
                    "; accumulator=" + args.Popularity + "; activeOverrides=" + overrides.Count + "; post-start publisher confirmed.");
            }
        }
        private void Info(string text) => log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + text);
        private void Error(string text) => log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + text);
    }
}
