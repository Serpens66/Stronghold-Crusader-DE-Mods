using System;
using System.Collections.Generic;

namespace UnitLimit
{
    public static class UnitLimitIntegration
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<Action> StateChangedCallbacks = new List<Action>();
        private static UnitLimitRuntime runtime;

        internal static void Attach(UnitLimitRuntime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (SyncRoot)
                runtime = value;
        }

        public static bool TryGetCapacity(int playerId, int unitType, out int count, out int limit)
        {
            count = 0;
            limit = -1;
            UnitLimitRuntime current;
            lock (SyncRoot)
                current = runtime;

            return current != null &&
                current.TryGetExternalCapacity(playerId, unitType, out count, out limit);
        }

        public static bool TryReserveOne(
            int playerId,
            int unitType,
            out long reservationId,
            out int count,
            out int limit)
        {
            UnitLimitRuntime current;
            lock (SyncRoot)
                current = runtime;

            if (current == null)
            {
                reservationId = 0;
                count = 0;
                limit = -1;
                return true;
            }

            return current.TryReserveExternalUnit(playerId, unitType, out reservationId, out count, out limit);
        }

        public static bool ArmReservation(long reservationId)
        {
            if (reservationId == 0)
                return true;

            UnitLimitRuntime current;
            lock (SyncRoot)
                current = runtime;

            return current != null && current.TryArmExternalReservation(reservationId);
        }

        public static void ReleaseReservation(long reservationId)
        {
            if (reservationId == 0)
                return;

            UnitLimitRuntime current;
            lock (SyncRoot)
                current = runtime;

            current?.ReleaseExternalReservation(reservationId);
        }

        public static void RegisterStateChanged(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (SyncRoot)
            {
                if (!StateChangedCallbacks.Contains(callback))
                    StateChangedCallbacks.Add(callback);
            }
        }

        internal static void NotifyStateChanged()
        {
            Action[] callbacks;
            lock (SyncRoot)
                callbacks = StateChangedCallbacks.ToArray();

            for (int index = 0; index < callbacks.Length; index++)
            {
                try { callbacks[index](); }
                catch { }
            }
        }
    }
}
