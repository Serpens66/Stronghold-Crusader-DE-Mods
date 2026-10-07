// TEMP_GATE_ROUTE_ACCEPTANCE: remove this file and its explicit project include after acceptance.
using System;
using System.Threading;
namespace APIShared
{
    /// <summary>Read-only TEMP_GATE_ROUTE_ACCEPTANCE observer; callbacks must never alter native decisions.</summary>
    public interface ITemporaryGateRouteAcceptanceObserver
    {
        /// <summary>Begins a short-lived published-route inspection, or returns null to decline observation.</summary>
        object BeginRoute(int player, int tribe, uint tribeGlobal, int unit, uint unitGlobal,
            int unitType, int x, int y, int targetX, int targetY, string orderContext);
        /// <summary>Observes one decoded directed edge belonging to the entry token.</summary>
        void RouteEdge(object token, int from, int to, int direction);
        /// <summary>Completes an inspection; incomplete or ambiguous output must remain unverified.</summary>
        void EndRoute(object token, string status, int result);
        /// <summary>Observes existing identity-checked Raid classification or retarget completion.</summary>
        void Raid(int player, int role, int tribe, uint tribeGlobal, int building, uint buildingGlobal,
            string stage, string result, string detail);
    }
    /// <summary>Passive optional TEMP_GATE_ROUTE_ACCEPTANCE registration with isolated observer failures.</summary>
    public static class TemporaryGateRouteAcceptanceBridge
    {
        private static ITemporaryGateRouteAcceptanceObserver observer;
        private static long failures;
        private static string lastFailureCause = "none";
        /// <summary>Gets the permanently rooted optional observer; null requires no game inspection.</summary>
        public static ITemporaryGateRouteAcceptanceObserver Current => Volatile.Read(ref observer);
        /// <summary>Gets the full process-wide diagnostic failure count.</summary>
        public static long Failures => Interlocked.Read(ref failures);
        /// <summary>Gets the most recently observed diagnostic source and exception type.</summary>
        public static string LastFailureCause => Volatile.Read(ref lastFailureCause);
        /// <summary>Registers one process-wide observer; map lifecycle changes only its logical state.</summary>
        public static void Register(ITemporaryGateRouteAcceptanceObserver value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (Interlocked.CompareExchange(ref observer, value, null) != null)
                throw new InvalidOperationException("TEMP_GATE_ROUTE_ACCEPTANCE observer already registered");
        }
        /// <summary>Reports existing Raid evidence when registered; observer exceptions cannot change its result.</summary>
        public static void ReportRaid(int player, int role, int tribe, uint tribeGlobal, int building,
            uint buildingGlobal, string stage, string result, string detail)
        {
            var target = Current;
            if (target == null) return;
            try { target.Raid(player, role, tribe, tribeGlobal, building, buildingGlobal, stage, result, detail); }
            catch (Exception error) { ReportFailure("raid-observer", error); }
        }
        /// <summary>Counts a diagnostic failure without retaining exceptions or an event history.</summary>
        public static void ReportFailure(string source = "adapter", Exception error = null)
        {
            Volatile.Write(ref lastFailureCause, source + ":" + (error == null ? "unspecified" : error.GetType().Name));
            Interlocked.Increment(ref failures);
        }
    }
}
