using BepInEx.Logging;
using HarmonyLib;
using R3;
using SHCDESE.EventAPI;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    // V5: EngineInterface.GetMeritData locks EngineInterface.threadLock, which EngineInterface.run holds
    // for the whole native tick; during long AI ticks the GUI checks froze the picture for 260-380 ms.
    // Its callers (allies/merit panels, spectator UI) only display rank and alliance values, which
    // change slowly. While another thread holds the lock, a copy of the last Vanilla result younger
    // than MaxAge is returned; otherwise Vanilla runs unchanged. Callers may modify the returned array
    // (HUD_MeritPanel does), so every result is a fresh clone. The cache is dropped on map unload.
    internal static class MeritDataLockOptimization
    {
        private static readonly long MaxAgeTicks = Stopwatch.Frequency * 3;
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.VanillaPerformance.Merit");
        private static ManualLogSource log;
        private static object threadLock;
        private static int[,] cache;
        private static long cacheStamp, served;
        private static int enabled, failed;

        internal static void Install(ManualLogSource logger)
        {
            log = logger;
            try
            {
                FieldInfo lockField = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
                if (lockField == null || lockField.FieldType != typeof(object))
                    throw new MissingFieldException("EngineInterface.threadLock differs from the audit.");
                threadLock = lockField.GetValue(null) ?? throw new InvalidOperationException("EngineInterface.threadLock is null.");
                MethodInfo target = typeof(EngineInterface).GetMethod(nameof(EngineInterface.GetMeritData),
                    BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (target == null || target.ReturnType != typeof(int[,]))
                    throw new MissingMethodException("EngineInterface.GetMeritData signature differs from the audit.");
                // Subscribed first (rooted by the Extender's static subject): a published patch must never
                // serve data across a map change.
                MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => Invalidate());
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(MeritDataLockOptimization), nameof(BeforeGetMeritData)),
                    postfix: new HarmonyMethod(typeof(MeritDataLockOptimization), nameof(AfterGetMeritData)));
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_MERIT_INSTALLED: permanent EngineInterface.GetMeritData prefix/postfix.");
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        internal static void SetEnabled(bool value)
        {
            bool active = value && Volatile.Read(ref failed) == 0;
            Volatile.Write(ref enabled, active ? 1 : 0);
            if (!active) Invalidate();
        }

        private static bool BeforeGetMeritData(ref int[,] __result, out bool __state)
        {
            __state = false;
            if (Volatile.Read(ref enabled) == 0) return true;
            int[,] cached = Volatile.Read(ref cache);
            if (cached == null || Stopwatch.GetTimestamp() - Volatile.Read(ref cacheStamp) > MaxAgeTicks) return true;
            // Free or owned by this thread (reentrant): Vanilla takes the lock itself. A race in which the
            // simulation acquires it right after this check only waits as Vanilla would.
            if (Monitor.TryEnter(threadLock))
            {
                Monitor.Exit(threadLock);
                return true;
            }
            __result = (int[,])cached.Clone();
            __state = true;
            if (Interlocked.Increment(ref served) == 1)
                Shared.DebugLogHelper.LogInfo(log,
                    "VANILLA_PERF_MERIT_CONFIRMED: served cached merit data while the simulation held the engine lock.");
            return false;
        }

        // Only fresh Vanilla results refresh the cache; served clones keep the original age.
        private static void AfterGetMeritData(int[,] __result, bool __state)
        {
            if (__state || __result == null || Volatile.Read(ref enabled) == 0) return;
            int[,] copy = (int[,])__result.Clone();
            Volatile.Write(ref cacheStamp, Stopwatch.GetTimestamp());
            Volatile.Write(ref cache, copy);
        }

        private static void Invalidate() => Volatile.Write(ref cache, null);

        private static void FailOpen(Exception exception)
        {
            Volatile.Write(ref enabled, 0);
            Invalidate();
            if (Interlocked.Exchange(ref failed, 1) == 0)
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/VanillaPerformance/MeritDataLockOptimization.cs:94",
                    "VANILLA_PERF_MERIT_DISABLED: Vanilla merit data access retained; " + exception);
        }
    }
}
