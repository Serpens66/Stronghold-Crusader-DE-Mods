using System;
using System.Threading;

namespace APIShared
{
    /// <summary>Read-only bridge diagnostics, independent of the gate policy registration.</summary>
    public interface IEnemyBridgePathObserver : IEnemyGateAssassinObserver
    {
        /// <summary>Begins an existing synchronous search; no search is requested by this API.</summary>
        object BeginSearch(string source, int rawPlayer);
        /// <summary>Reports actual native/effective returns. Null native means not observed or void.</summary>
        void EndSearch(object token, bool completed, int? nativeResult, int effectiveResult, long nativeCalls);
        /// <summary>Reports an already executed region query.</summary>
        void ObserveRegion(int rawPlayer, int sourcePcl, int targetPcl, int mode, int nativeResult, int effectiveResult);
    }

    /// <summary>Passive single observer registration; never owns hooks, policies or a scheduler.</summary>
    public static class EnemyBridgeDiagnosticBridge
    {
        private static IEnemyBridgePathObserver observer;
        private static long failures;
        private static string lastFailure;
        [ThreadStatic] private static Search active;
        private sealed class Search
        {
            internal IEnemyBridgePathObserver Observer;
            internal object Token;
            internal Search Parent;
            internal int? Native;
            internal long NativeCalls;
        }
        /// <summary>The independently registered diagnostic observer.</summary>
        public static IEnemyBridgePathObserver Current => Volatile.Read(ref observer);
        /// <summary>Exact callback exception count; failures never affect game results.</summary>
        public static long FailureCount => Interlocked.Read(ref failures);
        /// <summary>Last callback error source/type; counts retain every occurrence.</summary>
        public static string LastFailure => Volatile.Read(ref lastFailure);
        private static void Failed(string source, Exception error)
        {
            Interlocked.Increment(ref failures);
            Volatile.Write(ref lastFailure, source + ":" + error.GetType().Name);
        }
        /// <summary>Registers once for process lifetime. Map end changes observer state only.</summary>
        public static bool TryRegister(IEnemyBridgePathObserver candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            var previous = Interlocked.CompareExchange(ref observer, candidate, null);
            return previous == null || ReferenceEquals(previous, candidate);
        }
        /// <summary>Creates no context when no observer is registered.</summary>
        public static object BeginSearch(string source, int rawPlayer)
        {
            var current = Current;
            if (current == null) return null;
            var search = new Search { Observer = current, Parent = active };
            // Preserve nesting even if the observer rejects or throws on Begin.
            try { search.Token = current.BeginSearch(source, rawPlayer); }
            catch (Exception ex) { Failed("begin:" + source, ex); }
            active = search;
            return search;
        }
        /// <summary>Records a result from an existing native call and returns it unchanged.</summary>
        public static int NativeResult(int value)
        {
            if (active != null) { active.Native = value; active.NativeCalls++; }
            return value;
        }
        /// <summary>Restores nesting before invoking the observer.</summary>
        public static void EndSearch(object token, bool completed, int effectiveResult)
        {
            if (!(token is Search search)) return;
            active = search.Parent;
            try { search.Observer.EndSearch(search.Token, completed, search.Native, effectiveResult, search.NativeCalls); }
            catch (Exception ex) { Failed("end", ex); }
        }
        /// <summary>Forwards an existing query only when subscribed.</summary>
        public static void ObserveRegion(int player, int source, int target, int mode, int nativeResult, int effectiveResult)
        {
            var current = Current;
            if (current == null) return;
            try { current.ObserveRegion(player, source, target, mode, nativeResult, effectiveResult); }
            catch (Exception ex) { Failed("region", ex); }
        }
    }
}
