using System;
using System.Threading;

namespace APIShared
{
    /// <summary>Effective AI elevated-moat construction state published by ExtraFeatures.</summary>
    public enum ElevatedMoatAiState
    {
        /// <summary>No effective runtime state has been established.</summary>
        Unknown = 0,
        /// <summary>Elevated AI construction is not enabled by ExtraFeatures.</summary>
        Disabled = 1,
        /// <summary>The ExtraFeatures hook is installed and enabled for AI players.</summary>
        Enabled = 2
    }

    /// <summary>Process-wide publication of the effective elevated AI build state.</summary>
    public static class ElevatedMoatAiCapability
    {
        private static int current;

        /// <summary>Latest state; unknown until ExtraFeatures publishes a verified state.</summary>
        public static ElevatedMoatAiState Current =>
            (ElevatedMoatAiState)Volatile.Read(ref current);

        // Subscribers are process-lifetime consumers. The publisher never removes a runtime hook.
        /// <summary>Raised when the effective state changes.</summary>
        public static event Action<ElevatedMoatAiState> Changed;

        /// <summary>Publish the state after reconciling the native patch and AI setting.</summary>
        public static void Publish(ElevatedMoatAiState state)
        {
            if (state < ElevatedMoatAiState.Unknown || state > ElevatedMoatAiState.Enabled)
                throw new ArgumentOutOfRangeException(nameof(state));
            int previous = Interlocked.Exchange(ref current, (int)state);
            if (previous != (int)state)
            {
                Action<ElevatedMoatAiState> observers = Changed;
                if (observers == null)
                    return;
                foreach (Action<ElevatedMoatAiState> observer in observers.GetInvocationList())
                {
                    try { observer(state); }
                    catch { /* A diagnostic consumer cannot change the native feature state. */ }
                }
            }
        }
    }
}
