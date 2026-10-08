using SHCDESE.Interop.Enums;
using System;

namespace APIShared
{
    internal readonly struct PlayerDefeatObservation
    {
        internal PlayerDefeatObservation(
            bool readable,
            WinLossState winLossState,
            bool hasLivingLord,
            int lordUnitId,
            int lordGlobalId)
        {
            Readable = readable;
            WinLossState = winLossState;
            HasLivingLord = hasLivingLord;
            LordUnitId = lordUnitId;
            LordGlobalId = lordGlobalId;
        }

        internal bool Readable { get; }
        internal WinLossState WinLossState { get; }
        internal bool HasLivingLord { get; }
        internal int LordUnitId { get; }
        internal int LordGlobalId { get; }
    }

    internal sealed class PlayerDefeatState
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private readonly Slot[] slots = new Slot[LastPlayerId];
        private long sessionId;
        private bool sessionActive;

        internal void SynchronizeSession(long? activeSessionId)
        {
            if (!activeSessionId.HasValue)
            {
                Reset();
                return;
            }

            if (sessionActive && sessionId == activeSessionId.Value)
                return;

            Reset();
            sessionActive = true;
            sessionId = activeSessionId.Value;
        }

        internal void Observe(
            int simulationTick,
            PlayerDefeatObservation[] observations,
            Action<PlayerLordDeathNotification> lordDied,
            Action<PlayerDefeatNotification> defeated)
        {
            if (!sessionActive || observations == null || observations.Length < LastPlayerId)
                return;

            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
            {
                PlayerDefeatObservation observation = observations[playerId - 1];
                if (!observation.Readable)
                    continue;

                Slot slot = slots[playerId - 1] ?? (slots[playerId - 1] = new Slot());
                if (!slot.Observed)
                {
                    slot.Observed = true;
                    slot.PreviousWinLossState = observation.WinLossState;
                    if (observation.WinLossState == WinLossState.Loss)
                        slot.LordDeathPublished = true;
                    else
                        ArmLivingLord(slot, observation);
                    continue;
                }

                if (!slot.DefeatPublished &&
                    slot.PreviousWinLossState != WinLossState.Loss &&
                    observation.WinLossState == WinLossState.Loss)
                {
                    slot.DefeatPublished = true;
                    defeated?.Invoke(new PlayerDefeatNotification(sessionId, playerId, simulationTick));
                }
                slot.PreviousWinLossState = observation.WinLossState;

                if (slot.LordDeathPublished)
                    continue;

                if (observation.HasLivingLord)
                {
                    ArmLivingLord(slot, observation);
                    continue;
                }

                if (slot.LordArmed)
                {
                    slot.LordDeathPublished = true;
                    slot.LordArmed = false;
                    lordDied?.Invoke(new PlayerLordDeathNotification(
                        sessionId,
                        playerId,
                        slot.LordUnitId,
                        slot.LordGlobalId,
                        simulationTick));
                }
            }
        }

        private static void ArmLivingLord(Slot slot, PlayerDefeatObservation observation)
        {
            if (!observation.HasLivingLord)
                return;
            slot.LordArmed = true;
            slot.LordUnitId = observation.LordUnitId;
            slot.LordGlobalId = observation.LordGlobalId;
        }

        private void Reset()
        {
            sessionActive = false;
            sessionId = 0;
            Array.Clear(slots, 0, slots.Length);
        }

        private sealed class Slot
        {
            internal bool Observed;
            internal WinLossState PreviousWinLossState;
            internal bool DefeatPublished;
            internal bool LordArmed;
            internal bool LordDeathPublished;
            internal int LordUnitId;
            internal int LordGlobalId;
        }
    }
}
