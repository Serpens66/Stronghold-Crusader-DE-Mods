using APIShared;
using BepInEx;
using BepInEx.Logging;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    internal enum GameplaySessionStartKind { NewMap, LoadedSave, EditorCreated, EditorLoaded }
    internal sealed class GameplaySessionStartedContext
    {
        internal GameplaySessionStartedContext(MissionLifecycleNotification notification) { Notification = notification; }
        internal MissionLifecycleNotification Notification { get; }
        internal GameplaySessionStartKind Kind => (GameplaySessionStartKind)Notification.Context.StartKind;
        internal GameModeSnapshot Mode => Notification.Context.Mode;
        internal bool IsLoadedSave => Notification.Context.IsSave;
        internal bool IsEditor => Notification.Context.IsEditor;
        internal bool LoadingEditorMap => Kind == GameplaySessionStartKind.EditorLoaded;
        internal long SessionId => Notification.Context.SessionId;
        internal bool IsReplay => Notification.IsReplay;
        internal string SaveFileName => Notification.Context.FilePath;
    }

    // Per-assembly delivery only. Detection and mode decisions live in APIShared.
    internal static class MissionEvents
    {
        private static readonly List<Action<MissionLifecycleNotification>> observers = new List<Action<MissionLifecycleNotification>>();
        private static IMissionLifecycleCapability capability;
        private static Action<MissionLifecycleNotification> priority;
        private static MissionLifecycleNotification latest;
        private static string ownerGuid;
#if API_SHARED_PRESET_TESTS
        private static readonly ManualLogSource log = null;
#else
        private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Mission adapter");
#endif
        internal static void SetOwner(string owner) { ownerGuid = owner; }
        internal static void SetGate(Action<MissionLifecycleNotification> gate)
        {
            bool connected = capability != null;
            priority = gate;
            EnsureConnected();
            if (connected && latest != null) gate(latest);
        }
        private static void EnsureConnected()
        {
#if !API_SHARED_PRESET_TESTS
            if (capability != null) return;
            string owner = ownerGuid ?? typeof(MissionEvents).Assembly.GetTypes()
                .SelectMany(t => t.GetCustomAttributes(typeof(BepInPlugin), false).Cast<BepInPlugin>())
                .Select(a => a.GUID).First();
            if (!ApiShared.Current.TryGetMissionLifecycle(owner, out var service, out var diagnostic))
                throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
            capability = service;
            if (!service.TryRegisterObserver("Shared.MissionEvents." + typeof(MissionEvents).Assembly.GetName().Name, Deliver, Deliver, Deliver, out diagnostic))
            { capability = null; throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason); }
#endif
        }
#if API_SHARED_PRESET_TESTS
        internal static void ResetForTests() { observers.Clear(); latest = null; priority = null; capability = null; }
        internal static void PublishForTests(MissionLifecycleNotification e) => Deliver(e);
#endif
        private static void Deliver(MissionLifecycleNotification notification)
        {
            latest = notification.Kind == MissionLifecycleKind.End ? null : notification;
            try { priority?.Invoke(notification); }
            catch (Exception ex) { Report("Mission gate callback failed: ", ex); }
            foreach (var callback in observers.ToArray())
            {
                try { callback(notification); }
                catch (Exception ex) { Report("Mission subscriber failed: ", ex); }
            }
        }
        private static void Report(string message, Exception exception)
        {
            try { DebugLogHelper.LogError(log, message + exception); } catch { }
        }
        private static Observable<MissionLifecycleNotification> Observe(Func<MissionLifecycleNotification, bool> predicate, bool replay = false) =>
            new RelayObservable(predicate, replay);
        private sealed class RelayObservable : Observable<MissionLifecycleNotification>
        {
            private readonly Func<MissionLifecycleNotification, bool> predicate;
            private readonly bool replay;
            internal RelayObservable(Func<MissionLifecycleNotification, bool> predicate, bool replay)
            { this.predicate = predicate; this.replay = replay; }
            protected override IDisposable SubscribeCore(Observer<MissionLifecycleNotification> observer)
            {
                long deliveredStart = 0;
                Action<MissionLifecycleNotification> callback = e =>
                {
                    if (!predicate(e)) return;
                    if (e.Kind == MissionLifecycleKind.Start)
                    {
                        if (deliveredStart == e.Context.SessionId) return;
                        deliveredStart = e.Context.SessionId;
                    }
                    observer.OnNext(e);
                };
                observers.Add(callback);
                try
                {
                    EnsureConnected();
                    if (replay && latest?.Kind == MissionLifecycleKind.Start)
                        callback(latest.AsReplay());
                    return new LocalSubscription(callback);
                }
                catch { observers.Remove(callback); throw; }
            }
        }
        internal static Observable<MissionLifecycleNotification> Started => Observe(e => e.Kind == MissionLifecycleKind.Start, true);
        internal static Observable<MissionLifecycleNotification> Ended => Observe(e => e.Kind == MissionLifecycleKind.End);
        internal static Observable<MissionLifecycleNotification> Initialization => Observe(e => e.Kind == MissionLifecycleKind.Initialization);
        internal static Observable<MissionLifecycleNotification> NativeStart => Observe(e => e.Kind == MissionLifecycleKind.Initialization &&
            (e.Phase == MissionInitializationPhase.BeforeNativeStart || e.Phase == MissionInitializationPhase.AfterNativeStart));
        internal static Observable<MissionLifecycleNotification> Loading => Observe(e => e.Kind == MissionLifecycleKind.Initialization &&
            (e.Phase == MissionInitializationPhase.BeforeLoad || e.Phase == MissionInitializationPhase.NativeLoaded));
        internal static Observable<MissionLifecycleNotification> SaveLoading => Loading.Where(e => e.Context.IsSave);
        private sealed class LocalSubscription : IDisposable
        {
            private Action<MissionLifecycleNotification> callback;
            internal LocalSubscription(Action<MissionLifecycleNotification> callback) { this.callback = callback; }
            public void Dispose() { observers.Remove(callback); callback = null; }
        }
    }

    internal static class GameplaySessionLifecycle
    {
        internal static IDisposable SubscribeStarted(ManualLogSource log, Action<GameplaySessionStartedContext> callback,
            Action onEnded = null)
        {
            var start = MissionEvents.Started.Subscribe(e =>
            {
                try { callback(new GameplaySessionStartedContext(e)); }
                catch (Exception ex) { DebugLogHelper.LogError(log, "Mission start subscriber failed: " + ex); }
            });
            if (onEnded == null) return start;
            var end = MissionEvents.Ended.Subscribe(_ =>
            {
                try { onEnded?.Invoke(); }
                catch (Exception ex) { DebugLogHelper.LogError(log, "Mission end subscriber failed: " + ex); }
            });
            return new Pair(start, end);
        }
        private sealed class Pair : IDisposable
        {
            private readonly IDisposable start, end;
            internal Pair(IDisposable start, IDisposable end) { this.start = start; this.end = end; }
            public void Dispose() { start.Dispose(); end.Dispose(); }
        }
    }
}
