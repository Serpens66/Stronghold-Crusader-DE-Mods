using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    internal struct MeritReadState
    {
        internal bool Served, OwnsLock;
        internal EnginePresentationBridge.Session Session;
#if SERP_DEVDIAG
        internal long Started, CacheAge;
        internal int Gc;
        internal string Decision;
#endif
    }
    internal static partial class MeritDataLockOptimization
    {
        private static readonly long MaxAgeTicks = Stopwatch.Frequency * 3;
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.VanillaPerformance.Merit");
        private static ManualLogSource log;
        private static object threadLock;
        private static int enabled, failed;
        [ThreadStatic] private static bool refreshing;
        internal sealed class Snapshot
        {
            internal readonly EnginePresentationBridge.Session Session;
            internal readonly int[,] Data;
            internal readonly long Stamp;
            internal Snapshot(EnginePresentationBridge.Session session, int[,] data, long stamp)
            { Session = session; Data = data; Stamp = stamp; }
        }
        internal static void Install(ManualLogSource logger)
        {
            log = logger;
            try
            {
                threadLock = EnginePresentationBridge.Gate ?? throw new InvalidOperationException("Engine bridge unavailable");
                MethodInfo target = typeof(EngineInterface).GetMethod(nameof(EngineInterface.GetMeritData),
                    BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (target == null || target.ReturnType != typeof(int[,])) throw new MissingMethodException("GetMeritData signature");
                EnginePresentationBridge.ValidateLockedCall(target, nameof(EngineInterface.DLL_GetMeritData));
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(MeritDataLockOptimization), nameof(BeforeGetMeritData)),
                    postfix: new HarmonyMethod(typeof(MeritDataLockOptimization), nameof(AfterGetMeritData)),
                    finalizer: new HarmonyMethod(typeof(MeritDataLockOptimization), nameof(FinishGetMeritData)));
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_MERIT_INSTALLED: nonblocking session snapshots, held TryEnter and finalizer.");
            }
            catch (Exception exception) { FailOpen(exception); }
        }
        internal static void SetEnabled(bool value)
        {
            Volatile.Write(ref enabled, value && Volatile.Read(ref failed) == 0 ? 1 : 0);
            if (!value) Invalidate();
        }
        static partial void DiagBegin(ref MeritReadState state);
        static partial void DiagDecision(ref MeritReadState state, string reason, long age);
        static partial void DiagEnd(ref MeritReadState state);
        private static bool BeforeGetMeritData(ref int[,] __result, out MeritReadState __state)
        {
            __state = default; DiagBegin(ref __state);
            var session = EnginePresentationBridge.Current;
            if (Volatile.Read(ref enabled) == 0 || !EnginePresentationBridge.Active || session == null ||
                (!refreshing && !EnginePresentationBridge.IsMainThread))
            { DiagDecision(ref __state, "disabled", -1); return true; }
            __state.Session = session;
            long now = Stopwatch.GetTimestamp();
            if (!refreshing) Volatile.Write(ref session.MeritDemandUntil, now + MaxAgeTicks);
            var cached = Volatile.Read(ref session.MeritCache);
            bool valid = cached != null && ReferenceEquals(cached.Session, session);
            long age = valid ? now - cached.Stamp : -1;
            bool taken = false;
            Monitor.TryEnter(threadLock, ref taken);
            __state.OwnsLock = taken;
            if (taken)
            { DiagDecision(ref __state, refreshing ? "simulation-refresh" : "lock-held-through-original", age); return true; }
            if (!valid)
            {
                // Initial seed only. Never fabricate a rank/alliance array.
                Interlocked.Exchange(ref session.MeritRequested, 1);
                DiagDecision(ref __state, "cache-missing", -1); return true;
            }
            Interlocked.Exchange(ref session.MeritRequested, 1);
            __result = (int[,])cached.Data.Clone(); __state.Served = true;
            DiagDecision(ref __state, age > MaxAgeTicks ? "stale-served-refresh-requested" : "cache-served-lock-busy", age);
            return false;
        }
        private static void AfterGetMeritData(int[,] __result, MeritReadState __state, bool __runOriginal)
        {
            if (!__runOriginal || __state.Served || __result == null || Volatile.Read(ref enabled) == 0 ||
                __state.Session == null || !ReferenceEquals(__state.Session, EnginePresentationBridge.Current)) return;
            var next = new Snapshot(__state.Session, (int[,])__result.Clone(), Stopwatch.GetTimestamp());
            // Recheck after the clone: an ended session must never replace a new snapshot.
            if (ReferenceEquals(next.Session, EnginePresentationBridge.Current)) Volatile.Write(ref next.Session.MeritCache, next);
        }
        private static Exception FinishGetMeritData(ref MeritReadState __state, Exception __exception)
        {
            if (__state.OwnsLock) { __state.OwnsLock = false; Monitor.Exit(threadLock); }
            try { DiagEnd(ref __state); }
            catch (Exception) { /* Diagnostic failure must not replace Vanilla's result/exception. */ }
            return __exception;
        }
        internal static void RefreshRequested()
        {
            var session = EnginePresentationBridge.Current;
            if (session == null || Volatile.Read(ref enabled) == 0 || refreshing) return;
            var snapshot = Volatile.Read(ref session.MeritCache);
            long now = Stopwatch.GetTimestamp();
            if (now - Volatile.Read(ref session.MeritLastAttempt) < Stopwatch.Frequency) return;
            bool due = Volatile.Read(ref session.MeritDemandUntil) > now &&
                (snapshot == null || !ReferenceEquals(snapshot.Session, session) || now - snapshot.Stamp >= Stopwatch.Frequency);
            if (Interlocked.Exchange(ref session.MeritRequested, 0) == 0 && !due) return;
            Volatile.Write(ref session.MeritLastAttempt, now);
            try
            {
                refreshing = true;
                // The run bridge already owns the engine lock; the regular patched
                // entry retains foreign patches and publishes an isolated result.
                EngineInterface.GetMeritData();
            }
            catch (Exception error) { Interlocked.Exchange(ref session.MeritRequested, 1); FailOpen(error); }
            finally { refreshing = false; }
        }
        internal static void Invalidate()
        {
            var session = EnginePresentationBridge.Current;
            if (session != null) Volatile.Write(ref session.MeritCache, null);
        }
        private static void FailOpen(Exception exception)
        {
            Volatile.Write(ref enabled, 0); Invalidate();
            if (Interlocked.Exchange(ref failed, 1) == 0)
                Shared.DebugLogHelper.LogErrorLimited(log, "vanilla-performance-merit",
                    "VANILLA_PERF_MERIT_DISABLED: original merit access retained; " + exception);
        }
    }
}
