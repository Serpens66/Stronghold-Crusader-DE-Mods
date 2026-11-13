// Consumer fixture boundary: the real game-independent APIShared scheduler drives production
// consumers; only owner binding, lifecycle delivery and the Unity clock are simulated here.
using System;
using System.Collections.Generic;
using APIShared.Presentation;

namespace APIShared
{
    internal static class UiWorkBoundary
    {
        private static long milliseconds;
        private static readonly List<Exception> callbackErrors = new List<Exception>();
        private static UiWorkScheduler scheduler = CreateScheduler();
        private static readonly List<Tracked> registrations = new List<Tracked>();
        internal static readonly TestMissionLifecycle Missions = new TestMissionLifecycle();
        private static UiWorkScheduler CreateScheduler() => new UiWorkScheduler(() => milliseconds, 1000, (_, error) => callbackErrors.Add(error));
        internal static int ActiveCount { get { int count = 0; foreach (Tracked work in registrations) if (work.Continuous || work.Pending) count++; return count; } }
        internal static void Pump(int frame, int elapsedMilliseconds = 16)
        {
            milliseconds += elapsedMilliseconds; scheduler.Pump(frame);
            if (callbackErrors.Count != 0) throw new AggregateException("Unexpected consumer callback failure.", callbackErrors);
        }
        internal static void Reset() { milliseconds = 0; scheduler = CreateScheduler(); registrations.Clear(); callbackErrors.Clear(); Missions.Reset(); }
        internal static bool Register(string owner, string id, Action callback, out IUiFrameWorkRegistration registration, out string reason, int order)
        {
            var tracked = new Tracked();
            bool accepted = scheduler.TryRegister(owner, id, () => { tracked.Pending = false; callback(); }, order, out tracked.Inner, out reason);
            registration = accepted ? tracked : null;
            if (accepted) registrations.Add(tracked);
            return accepted;
        }
        private sealed class Tracked : IUiFrameWorkRegistration
        {
            internal IUiFrameWorkRegistration Inner;
            internal bool Continuous, Pending;
            public void Request() { Pending = true; Inner.Request(); }
            public void RequestAfter(TimeSpan delay) { Pending = true; Inner.RequestAfter(delay); }
            public void CancelPending() { Pending = false; Inner.CancelPending(); }
            public void SetContinuous(bool enabled) { Continuous = enabled; Inner.SetContinuous(enabled); }
        }
    }
    internal static class ApiShared { internal static TestModApiClient ForMod(string owner) => new TestModApiClient(owner); }
    internal sealed class TestModApiClient
    {
        internal TestModApiClient(string owner) { Ui = new TestUiWorkClient(owner); }
        internal TestUiWorkClient Ui { get; }
        internal bool TryGetMissionLifecycle(out IMissionLifecycleCapability lifecycle, out NativeCapabilityDiagnostic diagnostic)
        { lifecycle = UiWorkBoundary.Missions; diagnostic = null; return true; }
    }
    internal sealed class TestUiWorkClient
    {
        private readonly string owner;
        internal TestUiWorkClient(string owner) { this.owner = owner; }
        internal bool TryRegisterBeforeRender(string id, Action callback, out IUiFrameWorkRegistration registration, out string reason, int order = 0)
            => UiWorkBoundary.Register(owner, id, callback, out registration, out reason, order);
    }
    internal sealed class NativeCapabilityDiagnostic { internal string Reason => string.Empty; }
    internal interface IMissionLifecycleCapability
    {
        bool TryRegisterObserver(string id, Action<object> started, Action<object> ended, Action<object> initialization, out NativeCapabilityDiagnostic diagnostic);
    }
    internal sealed class TestMissionLifecycle : IMissionLifecycleCapability
    {
        private Action<object> onStart, onEnd;
        public bool TryRegisterObserver(string id, Action<object> started, Action<object> ended, Action<object> initialization, out NativeCapabilityDiagnostic diagnostic)
        { onStart += started; onEnd += ended; diagnostic = null; return true; }
        internal void Start() => onStart?.Invoke(null);
        internal void End() => onEnd?.Invoke(null);
        internal void Reset() { onStart = onEnd = null; }
    }
}
