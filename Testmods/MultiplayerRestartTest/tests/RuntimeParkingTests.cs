using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using APIShared.Presentation;
using HarmonyLib;
using MultiplayerRestartTest;

// Run before RuntimeFailureTests, which intentionally intercepts Cancelled itself.
// These fixtures execute the real Cancelled and Tick; only logging and the final
// Unity recovery-popup boundary are suppressed. No game singleton is constructed.
internal static class RuntimeParkingTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks, popups, invalidations;
    private static Harmony patches;
    private sealed class Frames : IUiFrameWorkRegistration
    {
        internal bool Continuous;
        public void SetContinuous(bool enabled) { Continuous = enabled; }
        public void Request() { }
        public void RequestAfter(TimeSpan delay) { }
        public void CancelPending() { }
    }
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Any);
    private static FieldInfo Barrier(string name) => typeof(StopBarrier).GetField(name, Any);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Any).GetValue(target);
    private static void Call(object target, string name)
    {
        try { Method(target.GetType(), name).Invoke(target, null); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private static bool NoLog() => false;
    private static bool RecoveryBoundary() { popups++; return false; }
    private static RestartRuntime Seed(RestartProtocol protocol, Frames frames)
    {
        var runtime = (RestartRuntime)FormatterServices.GetUninitializedObject(typeof(RestartRuntime));
        Set(runtime, "unityThread", Thread.CurrentThread.ManagedThreadId);
        Set(runtime, "retired", new HashSet<string>(StringComparer.Ordinal));
        Set(runtime, "protocol", protocol);
        Set(runtime, "frame", frames);
        Set(runtime, "ready", true);
        Set(runtime, "allowStart", true);
        var constructor = typeof(IngameMenuActionRegistration).GetConstructor(Any, null, new[] { typeof(Action) }, null);
        Set(runtime, "menu", constructor.Invoke(new object[] { (Action)(() => invalidations++) }));
        return runtime;
    }
    private static RestartProtocol Aborted(bool committed)
    {
        var protocol = new RestartProtocol(1001, 1001, new ulong[] { 1001, 1002 }, "parking-source", "parking-digest", 100);
        protocol.Begin(Guid.NewGuid().ToString("N"), 0);
        if (committed)
        {
            protocol.Prepared(0);
            protocol.Receive(1002, new RestartPacket { Kind = RestartMessageKind.Ready,
                Session = protocol.Session, Digest = protocol.Digest, Operation = protocol.Operation }, 0);
        }
        protocol.Abort("fixture-abort-after-start");
        Check(protocol.IsCommitted == committed && protocol.IsTerminal, "Fixture failed to reach its real abort state.");
        return protocol;
    }
    internal static void Run()
    {
        Check(StopBarrier.Installed, "Parking fixture requires the bootstrap's real installed barrier.");
        patches = new Harmony("MultiplayerRestartTest.RuntimeParkingFixture");
        patches.Patch(Method(typeof(RestartRuntime), "Report"), prefix: new HarmonyMethod(Method(typeof(RuntimeParkingTests), nameof(NoLog))));
        patches.Patch(Method(typeof(RestartRuntime), "ShowFailureRecovery"), prefix: new HarmonyMethod(Method(typeof(RuntimeParkingTests), nameof(RecoveryBoundary))));
        string[] names = { "requested", "parked", "entries", "hits", "networkAllowed", "launchAllowed" };
        var saved = new Dictionary<string, object>();
        foreach (string name in names) saved.Add(name, Barrier(name).GetValue(null));
        object entry = Barrier("Entry").GetValue(null);
        try
        {
            StopBarrier.Release();
            lock (entry)
            {
                Barrier("hits").SetValue(null, 1);
                Barrier("parked").SetValue(null, 0);
                Barrier("entries").SetValue(null, 0);
            }
            var frames = new Frames();
            var runtime = Seed(Aborted(true), frames);
            Call(runtime, "Cancelled");
            Check(StopBarrier.Requested && !StopBarrier.Quiescent && Get<bool>(runtime, "failureParkPending"),
                "Committed late Abort did not request a fresh park.");
            Check(frames.Continuous && popups == 0 && !Get<bool>(runtime, "recoveryPending"),
                "Recovery UI or cleanup ran before the fresh park receipt.");
            Check(!Get<bool>(runtime, "ready") && !Get<bool>(runtime, "allowStart") && invalidations == 1,
                "Failed target retained launch/menu permission.");
            Call(runtime, "Tick");
            Check(popups == 0 && Get<bool>(runtime, "failureParkPending") && frames.Continuous,
                "Tick crossed the UI boundary while the worker was still running.");

            lock (entry) { Barrier("parked").SetValue(null, 1); Barrier("entries").SetValue(null, 1); }
            Call(runtime, "Tick");
            Check(popups == 0 && Get<bool>(runtime, "failureParkPending"),
                "Worker-only receipt bypassed an outstanding timer/network callback.");
            lock (entry) Barrier("entries").SetValue(null, 0);
            Call(runtime, "Tick");
            Check(popups == 1 && !Get<bool>(runtime, "failureParkPending") && StopBarrier.Requested,
                "Drained worker/callback receipt did not expose recovery exactly once while staying parked.");
            Call(runtime, "Tick"); Call(runtime, "Cancelled");
            Check(popups == 1, "Repeated failure processing displayed recovery twice.");

            StopBarrier.Release();
            lock (entry) Barrier("parked").SetValue(null, 0);
            var untouched = new Frames { Continuous = true };
            var uncommitted = Seed(Aborted(false), untouched);
            Call(uncommitted, "Cancelled");
            Check(!StopBarrier.Requested && !Get<bool>(uncommitted, "failureParkPending") &&
                Get<bool>(uncommitted, "ready") && !untouched.Continuous && popups == 1,
                "Pre-commit Abort parked the source world or opened target recovery.");
        }
        finally
        {
            StopBarrier.Release();
            lock (entry) foreach (string name in names) Barrier(name).SetValue(null, saved[name]);
        }
        Console.WriteLine("Runtime park-before-recovery fixtures passed: " + checks + " checks.");
    }
}
