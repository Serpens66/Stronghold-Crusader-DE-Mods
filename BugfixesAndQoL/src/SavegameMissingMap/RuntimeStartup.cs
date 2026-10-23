using System;
using System.Threading;

namespace BugfixesAndQoL.SavegameMissingMap
{
    // Independent of Unity component lifetime; also exercised by the desktop tests.
    internal sealed class RuntimeStartup
    {
        private int attempted;
        internal static void Report<T>(Action<T> sink, T message)
        {
            // Diagnostics must never change save data, activation or menu behavior.
            try { sink(message); }
            catch (Exception) { /* A failing logger cannot safely report its own failure. */ }
        }
        internal void Subscribe(Action<Action> subscribe, Action initialize, Action<bool> activate,
            Action<string> log, Action<Exception> error)
        {
            Report(log, "WAITING: awaiting game library.");
            subscribe(() =>
            {
                if (Interlocked.Exchange(ref attempted, 1) != 0) return;
                activate(false);
                try
                {
                    initialize();
                    activate(true);
                    Report(log, "READY: decoder, encoder, restart menu and isolated confirmation callback validated; permanent hooks active.");
                }
                catch (Exception ex)
                {
                    activate(false);
                    Report(error, ex);
                }
            });
        }
    }
}
