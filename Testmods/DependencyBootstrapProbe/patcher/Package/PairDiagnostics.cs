using System;

namespace DependencyBootstrapProbe
{
    internal static class PairDiagnostics
    {
        internal static void Mark(string phase, string detail) { try { ProbeTrace.Mark(phase, detail); } catch { } }
        internal static void Reject(Exception error) => Mark("PAIR_REJECTED", error.GetType().Name + ": " + error.Message);
    }
}
