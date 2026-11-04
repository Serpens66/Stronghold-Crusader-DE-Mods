using System;

namespace BugfixesAndQoL.PitchStairs
{
    // Stored heights and all fourteen readers/writers must use one rule for an entire native
    // world. Settings and End never publish a new rule; only the next first BeforeLoad does.
    internal sealed class PitchStairsActivation
    {
        private readonly object engineGate;
        private readonly Action<bool> publish;
        private long session;
        private bool missionLocked;

        internal PitchStairsActivation(object engineGate, Action<bool> publish)
        {
            this.engineGate = engineGate ?? throw new ArgumentNullException(nameof(engineGate));
            this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
        }

        internal bool BeginLoad(long sessionId, bool ready, bool modEnabled, bool featureEnabled)
        {
            lock (engineGate)
            {
                if (sessionId <= session) return false; // Duplicate/nested load or stale notification.
                publish(ready && modEnabled && featureEnabled);
                session = sessionId;
                missionLocked = true;
                return true;
            }
        }

        internal bool End(long sessionId)
        {
            lock (engineGate)
            {
                if (sessionId != session || !missionLocked) return false;
                missionLocked = false;
                return true;
            }
        }
    }
}
