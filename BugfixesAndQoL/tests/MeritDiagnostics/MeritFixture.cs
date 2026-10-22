using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
#if !SERP_DEVDIAG
using MeritReadState = System.Boolean;
#endif

// External boundaries only. The cache prefix/postfix under test are the production files.
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace R3
{
    public sealed class FixtureSubject { }
    public static class FixtureExtensions
    {
        public static IDisposable Subscribe(this FixtureSubject subject, Action<object> callback) => null;
    }
}
namespace SHCDESE.EventAPI
{
    public static class MapLoaderR3EventHooks
    {
        public sealed class Publisher { public R3.FixtureSubject Observable = new R3.FixtureSubject(); }
        public static readonly Publisher OnUnloadMap = new Publisher();
    }
}
public static class EngineInterface
{
    public static int[,] GetMeritData() => new int[1, 1];
}
namespace Shared
{
    internal static class DevDiag { internal static void Count(string name) { } }
    internal static class DebugLogHelper
    {
        internal static void LogInfo(BepInEx.Logging.ManualLogSource log, string message) { }
        internal static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string key, string message) { }
    }
    internal static class DevDiagRuntime
    {
        internal static int Calls;
        internal static string Detail;
        internal static void Add(string name, long count, long ticks) { Calls++; }
        internal static void Spike(string name, long elapsed, string detail) { Detail = detail; }
    }
}
namespace BugfixesAndQoL.VanillaPerformance
{
    internal static partial class MeritDataLockOptimization
    {
        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        internal static string RunFixture()
        {
            threadLock = new object(); failed = 0; enabled = 0;
            cache = new int[1, 1]; cache[0, 0] = 11; cacheStamp = Stopwatch.GetTimestamp();
            int[,] output = null;
            Check(BeforeGetMeritData(ref output, out MeritReadState state), "Disabled cache skipped Vanilla");
            int[,] disabledCache = cache;
            AfterGetMeritData(new int[1, 1], state);
            Check(ReferenceEquals(disabledCache, cache), "Disabled cache was changed");

            enabled = 1; cache = null;
            Check(BeforeGetMeritData(ref output, out state), "Missing cache skipped Vanilla");
            int[,] fresh = new int[1, 1]; fresh[0, 0] = 17;
            AfterGetMeritData(fresh, state); fresh[0, 0] = 99;
            Check(cache[0, 0] == 17 && !ReferenceEquals(cache, fresh), "Fresh result not cloned");

            cacheStamp = Stopwatch.GetTimestamp() - MaxAgeTicks - Stopwatch.Frequency;
            Check(BeforeGetMeritData(ref output, out state), "Expired cache skipped Vanilla");
#if SERP_DEVDIAG
            Check(state.Decision == "cache-expired", "Wrong expired decision");
#endif
            AfterGetMeritData(new int[1, 1], state);
            Check(BeforeGetMeritData(ref output, out state), "Free lock skipped Vanilla");
#if SERP_DEVDIAG
            Check(state.Decision == "lock-free-at-probe", "Wrong free-lock decision");
#endif
            Check(!Monitor.IsEntered(threadLock), "Existing probe leaked the lock");
            AfterGetMeritData(new int[1, 1], state);

            cache[0, 0] = 23;
            long oldStamp = cacheStamp;
            using (var ready = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var holder = new Thread(() => { lock (threadLock) { ready.Set(); release.Wait(); } });
                holder.Start();
                try
                {
                    Check(ready.Wait(10000), "Holder did not acquire");
                    Check(!BeforeGetMeritData(ref output, out state) && state && output[0, 0] == 23,
                        "Busy lock did not serve cache");
                    output[0, 0] = 29;
                    AfterGetMeritData(output, state);
                    Check(cache[0, 0] == 23 && cacheStamp == oldStamp, "Cached clone altered data or age");
                }
                finally { release.Set(); Check(holder.Join(10000), "Holder did not exit"); }
            }
#if SERP_DEVDIAG
            state.Started = Stopwatch.GetTimestamp() - Stopwatch.Frequency / 5;
            DiagEnd(ref state);
            Check(Shared.DevDiagRuntime.Detail != null && Shared.DevDiagRuntime.Detail.Contains("decision=cache-served-lock-busy") &&
                Shared.DevDiagRuntime.Detail.Contains("servedCache=True"), "Slow decision context absent");
            int calls = Shared.DevDiagRuntime.Calls;
            state = default;
            DiagEnd(ref state);
            Check(Shared.DevDiagRuntime.Calls == calls, "Skipped Harmony prefix produced a false spike");
#else
            Check(Shared.DevDiagRuntime.Calls == 0 && typeof(MeritReadState) == typeof(bool) &&
                typeof(MeritDataLockOptimization).GetMethod("DiagBegin", BindingFlags.Static | BindingFlags.NonPublic) == null,
                "Ordinary-build cache retains diagnostic work/state");
#endif
            return "PASS: production merit cache disabled/missing/expired/free/busy paths; fresh and served clone isolation; unchanged cache age; diagnostic context and ordinary-build boundary.";
        }
    }
    public static class MeritFixtureRunner
    {
        public static string Run() => MeritDataLockOptimization.RunFixture();
    }
}
