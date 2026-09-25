using System;

namespace BugfixesAndQoL
{
    // Observation only. Subscribers belong to optional diagnostic mods and may never affect gameplay.
    public static class SurrenderDiagnosticBridge
    {
        public static event Action<int, int, int, int> SurrenderExecuted;
        public static event Action<long, int, int, int, int> SpectatorExecuted;
        public static event Action<byte[]> ChoresSent;
        public static event Action<bool, bool, int, int, int> ResyncStateChanged;

        internal static void PublishSurrender(int playerId, int unitId, int globalId, int tick) =>
            Publish(SurrenderExecuted, callback => callback(playerId, unitId, globalId, tick));

        internal static void PublishSpectator(long sessionId, int playerId, int deathTick, int executionTick, int localPlayerId) =>
            Publish(SpectatorExecuted, callback => callback(sessionId, playerId, deathTick, executionTick, localPlayerId));

        internal static void PublishChores(byte[] buffer)
        {
            try
            {
                Action<byte[]> callbacks = ChoresSent;
                if (callbacks == null)
                    return;
                byte[] snapshot = buffer == null ? null : (byte[])buffer.Clone();
                Publish(callbacks, callback => callback(snapshot));
            }
            catch { /* Buffer observation must never suppress the existing recovery path. */ }
        }

        internal static void PublishResync(bool previous, bool current, int tick, int section, int layer) =>
            Publish(ResyncStateChanged, callback => callback(previous, current, tick, section, layer));

        private static void Publish<T>(T callbacks, Action<T> invoke) where T : Delegate
        {
            if (callbacks == null)
                return;
            try
            {
                foreach (T callback in callbacks.GetInvocationList())
                {
                    try { invoke(callback); }
                    catch { /* A diagnostic observer must not interrupt a Vanilla or mod callback. */ }
                }
            }
            catch { /* Subscription races must not affect simulation. */ }
        }
    }
}
