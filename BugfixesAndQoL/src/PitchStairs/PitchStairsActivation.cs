using System;

namespace BugfixesAndQoL.PitchStairs
{
    // Snapshot and native load share one continuous engine-lock scope. BeforeLoad can arrive
    // on either side of our managed loader wrapper, depending on hook order.
    internal sealed class PitchStairsActivation
    {
        private readonly object engineGate;
        private readonly Action<bool> publish;
        private long session;
        private bool missionLocked;
        private bool pending, nextEnabled;
        private int loadDepth;

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
                session = sessionId;
                missionLocked = true;
                nextEnabled = ready && modEnabled && featureEnabled;
                pending = true;
                if (loadDepth != 0) PublishPending();
                return true;
            }
        }

        internal bool End(long sessionId)
        {
            lock (engineGate)
            {
                if (sessionId != session || !missionLocked) return false;
                missionLocked = false;
                pending = false; // A failed attempt may never reach our loader wrapper.
                return true;
            }
        }

        internal T RunLoad<T>(Func<T> original)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            lock (engineGate)
            {
                loadDepth++;
                try
                {
                    // APIShared outside: the snapshot is queued. APIShared inside: its
                    // BeforeLoad publishes reentrantly while this same lock stays held.
                    PublishPending();
                    return original();
                }
                finally { loadDepth--; }
            }
        }

        private void PublishPending()
        {
            if (!pending) return;
            publish(nextEnabled);
            pending = false;
        }
    }
}
