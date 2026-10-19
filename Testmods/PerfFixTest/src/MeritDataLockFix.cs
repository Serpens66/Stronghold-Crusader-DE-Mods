using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace PerfFixTest
{
    // V5: EngineInterface.GetMeritData waits for EngineInterface.threadLock, which
    // EngineInterface.run holds for the whole native tick (observed 260-381 ms main-thread stalls
    // during ~450 ms ticks). All callers only read the fresh int[9,5] (rank/alliance per player,
    // slowly changing). While the lock is held, a clone of the last result younger than MaxAge is
    // returned; otherwise Vanilla runs unchanged and refreshes the cache.
    internal static class MeritDataLockFix
    {
        private static readonly long MaxAgeTicks = Stopwatch.Frequency * 3;
        private static object threadLock;
        private static int[,] cache;
        private static long cacheStamp, served;

        internal static void Install()
        {
            try
            {
                FieldInfo lockField = AccessTools.Field(typeof(EngineInterface), "threadLock");
                if (lockField == null || !lockField.IsStatic || lockField.FieldType != typeof(object))
                    throw new MissingFieldException("EngineInterface.threadLock differs from the audit.");
                threadLock = lockField.GetValue(null) ?? throw new InvalidOperationException("EngineInterface.threadLock is null.");
                MethodInfo target = AccessTools.Method(typeof(EngineInterface), nameof(EngineInterface.GetMeritData), Type.EmptyTypes);
                if (target == null || !target.IsStatic || target.ReturnType != typeof(int[,]))
                    throw new MissingMethodException("EngineInterface.GetMeritData signature differs from the audit.");
                PerfFixTestPlugin.Harmony.Patch(target, prefix: new HarmonyMethod(typeof(MeritDataLockFix), nameof(Prefix)),
                    postfix: new HarmonyMethod(typeof(MeritDataLockFix), nameof(Postfix)));
                PerfFixTestPlugin.Note("V5 MeritDataLockCache installed on EngineInterface.GetMeritData.");
            }
            catch (Exception exception) { PerfFixTestPlugin.Fail("V5 MeritDataLockCache disabled: " + exception.Message); }
        }

        private static bool Prefix(ref int[,] __result, out bool __state)
        {
            __state = false;
            int[,] cached = cache;
            if (cached == null || Stopwatch.GetTimestamp() - Volatile.Read(ref cacheStamp) > MaxAgeTicks) return true;
            if (Monitor.TryEnter(threadLock))
            {
                // Free: let Vanilla take the lock itself (a re-acquire race only waits as in Vanilla).
                Monitor.Exit(threadLock);
                return true;
            }
            __result = (int[,])cached.Clone();
            __state = true;
            long count = Interlocked.Increment(ref served);
            PerfFixTestPlugin.Note("V5 served cached merit data while the engine lock was held (#" + count + ").");
            return false;
        }

        private static void Postfix(int[,] __result, bool __state)
        {
            // Only fresh Vanilla results refresh the cache; served clones keep their original age.
            if (__state || __result == null) return;
            cache = (int[,])__result.Clone();
            Volatile.Write(ref cacheStamp, Stopwatch.GetTimestamp());
        }
    }
}
