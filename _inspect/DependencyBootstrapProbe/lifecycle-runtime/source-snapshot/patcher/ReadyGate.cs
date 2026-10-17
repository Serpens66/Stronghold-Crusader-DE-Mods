using System;
using System.Threading;

namespace DependencyBootstrapProbe
{
    internal sealed class ReadyGate
    {
        private readonly Action ready;
        private readonly Action complete;
        private readonly Action<string, string> mark;
        private int readySeen, completeSeen, failureSeen;
        internal ReadyGate(Action ready, Action complete, Action<string, string> mark)
        { this.ready = ready; this.complete = complete; this.mark = mark; }
        internal void Process(string source, string message)
        {
            try
            {
                if (source != "BepInEx") return;
                if (message == "Chainloader ready" && Interlocked.Exchange(ref readySeen, 1) == 0)
                    ready();
                else if (message == "Chainloader startup complete" && Interlocked.Exchange(ref completeSeen, 1) == 0)
                {
                    if (Volatile.Read(ref readySeen) == 0) throw new InvalidOperationException("Completion without readiness");
                    complete();
                }
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref failureSeen, 1) == 0)
                    try { mark("ERROR", "readiness callback " + ex); } catch { }
            }
        }
    }
}