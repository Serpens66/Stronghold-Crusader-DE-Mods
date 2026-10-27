using System;

namespace BugfixesAndQoL.MoatTerrain
{
    // Save registrations and executable patches both publish references to our buffer.
    // Neither can be withdrawn safely; readiness is separate from resource retention.
    internal sealed class TerrainPublication
    {
        private bool retained, started, failed;
        internal bool IsReady { get; private set; }
        internal void RetainRegisteredResources() { retained = true; }

        internal void Enable(Action publish, Action validate)
        {
            if (started || failed) throw new InvalidOperationException("Moat terrain publication cannot be retried.");
            started = retained = true; // An Enable can write code before it throws.
            publish();
            validate();
            IsReady = true;
        }

        internal void Fail(Action disable, Action rollback)
        {
            if (failed) return;
            failed = true;
            IsReady = false;
            try { disable(); }
            finally { if (!retained) rollback(); }
        }
    }
}
