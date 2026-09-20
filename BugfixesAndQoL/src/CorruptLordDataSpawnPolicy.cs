using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal enum CorruptLordDataSpawnDecision
    {
        ClearStaleLordReference,
        RejectDisabled,
        RejectNotNewGameSession,
        RejectOutsideCorrectionWindow,
        RejectMissingPlayerRecord,
        RejectKicked,
        RejectAlreadyAttempted,
        RejectDefeated,
        RejectMissingStoredLordUnitId,
        RejectMissingStoredLordGlobalId,
        RejectLordUnitUnresolved,
        RejectLordOwnerNotZero,
        RejectLordTypeNotNull,
        RejectLordAliveStateNotNone,
        RejectLordUnitGlobalIdNotZero,
        RejectLordHealthNotZero,
        RejectInvalidKeep,
        RejectInvalidKeepDoorReference
    }

    internal readonly struct CorruptLordDataSpawnGuardInput
    {
        internal CorruptLordDataSpawnGuardInput(
            bool enabled,
            bool isNewGameSession,
            bool correctionWindowOpen,
            bool hasPlayerRecord,
            bool kicked,
            bool alreadyAttempted,
            bool isDefeated,
            int lordUnitId,
            int lordGlobalId,
            bool lordUnitResolved,
            int lordOwnerPlayerId,
            bool lordTypeIsNull,
            bool lordAliveStateIsNone,
            int lordUnitGlobalId,
            int lordCurrentHealth,
            bool validOwnedKeep,
            bool validOwnedKeepDoorReference)
        {
            Enabled = enabled;
            IsNewGameSession = isNewGameSession;
            CorrectionWindowOpen = correctionWindowOpen;
            HasPlayerRecord = hasPlayerRecord;
            Kicked = kicked;
            AlreadyAttempted = alreadyAttempted;
            IsDefeated = isDefeated;
            LordUnitId = lordUnitId;
            LordGlobalId = lordGlobalId;
            LordUnitResolved = lordUnitResolved;
            LordOwnerPlayerId = lordOwnerPlayerId;
            LordTypeIsNull = lordTypeIsNull;
            LordAliveStateIsNone = lordAliveStateIsNone;
            LordUnitGlobalId = lordUnitGlobalId;
            LordCurrentHealth = lordCurrentHealth;
            ValidOwnedKeep = validOwnedKeep;
            ValidOwnedKeepDoorReference = validOwnedKeepDoorReference;
        }

        internal bool Enabled { get; }
        internal bool IsNewGameSession { get; }
        internal bool CorrectionWindowOpen { get; }
        internal bool HasPlayerRecord { get; }
        internal bool Kicked { get; }
        internal bool AlreadyAttempted { get; }
        internal bool IsDefeated { get; }
        internal int LordUnitId { get; }
        internal int LordGlobalId { get; }
        internal bool LordUnitResolved { get; }
        internal int LordOwnerPlayerId { get; }
        internal bool LordTypeIsNull { get; }
        internal bool LordAliveStateIsNone { get; }
        internal int LordUnitGlobalId { get; }
        internal int LordCurrentHealth { get; }
        internal bool ValidOwnedKeep { get; }
        internal bool ValidOwnedKeepDoorReference { get; }
    }

    internal static class CorruptLordDataSpawnPolicy
    {
        internal static CorruptLordDataSpawnDecision Evaluate(in CorruptLordDataSpawnGuardInput input)
        {
            if (!input.Enabled)
                return CorruptLordDataSpawnDecision.RejectDisabled;
            if (!input.IsNewGameSession)
                return CorruptLordDataSpawnDecision.RejectNotNewGameSession;
            if (!input.CorrectionWindowOpen)
                return CorruptLordDataSpawnDecision.RejectOutsideCorrectionWindow;
            if (!input.HasPlayerRecord)
                return CorruptLordDataSpawnDecision.RejectMissingPlayerRecord;
            if (input.Kicked)
                return CorruptLordDataSpawnDecision.RejectKicked;
            if (input.AlreadyAttempted)
                return CorruptLordDataSpawnDecision.RejectAlreadyAttempted;
            if (input.IsDefeated)
                return CorruptLordDataSpawnDecision.RejectDefeated;
            if (!input.ValidOwnedKeep)
                return CorruptLordDataSpawnDecision.RejectInvalidKeep;
            if (!input.ValidOwnedKeepDoorReference)
                return CorruptLordDataSpawnDecision.RejectInvalidKeepDoorReference;
            if (input.LordUnitId <= 0)
                return CorruptLordDataSpawnDecision.RejectMissingStoredLordUnitId;
            if (input.LordGlobalId <= 0)
                return CorruptLordDataSpawnDecision.RejectMissingStoredLordGlobalId;
            if (!input.LordUnitResolved)
                return CorruptLordDataSpawnDecision.RejectLordUnitUnresolved;
            if (input.LordOwnerPlayerId != 0)
                return CorruptLordDataSpawnDecision.RejectLordOwnerNotZero;
            if (!input.LordTypeIsNull)
                return CorruptLordDataSpawnDecision.RejectLordTypeNotNull;
            if (!input.LordAliveStateIsNone)
                return CorruptLordDataSpawnDecision.RejectLordAliveStateNotNone;
            if (input.LordUnitGlobalId != 0)
                return CorruptLordDataSpawnDecision.RejectLordUnitGlobalIdNotZero;
            if (input.LordCurrentHealth != 0)
                return CorruptLordDataSpawnDecision.RejectLordHealthNotZero;

            // Do not broaden this into a generic "invalid Lord" repair. The complete zeroed
            // unit-slot identity is the evidence that distinguishes the remap tombstone from a
            // live, initializing, dying, restored, or otherwise meaningful Vanilla unit state.
            return CorruptLordDataSpawnDecision.ClearStaleLordReference;
        }
    }

    internal static class CorruptLordDataSpawnObservationPolicy
    {
        internal const int CorrectionWindowEndTick = 3;
        internal const int ConfirmationTimeoutTick = 180;

        internal static bool IsCorrectionWindow(int observationTick) =>
            observationTick >= 1 && observationTick <= CorrectionWindowEndTick;

        internal static bool ShouldStopAfterTick(int observationTick, bool hasOutstandingConfirmations) =>
            observationTick >= CorrectionWindowEndTick && !hasOutstandingConfirmations;

        internal static bool ShouldTimeoutAfterConfirmation(
            int observationTick,
            bool hasOutstandingConfirmations) =>
            observationTick >= ConfirmationTimeoutTick && hasOutstandingConfirmations;
    }

    internal sealed class CorruptLordDataSpawnSessionState
    {
        private readonly HashSet<int> attemptedPlayers = new HashSet<int>();
        private readonly HashSet<int> confirmedPlayers = new HashSet<int>();

        internal long SessionId { get; private set; }

        internal void Reset(long sessionId)
        {
            SessionId = sessionId;
            attemptedPlayers.Clear();
            confirmedPlayers.Clear();
        }

        internal bool WasAttempted(int playerId) => attemptedPlayers.Contains(playerId);
        internal bool IsConfirmed(int playerId) => confirmedPlayers.Contains(playerId);
        internal bool IsOutstanding(int playerId) =>
            attemptedPlayers.Contains(playerId) && !confirmedPlayers.Contains(playerId);
        internal int AttemptedCount => attemptedPlayers.Count;
        internal int ConfirmedCount => confirmedPlayers.Count;
        internal bool HasOutstandingConfirmations => attemptedPlayers.Count > confirmedPlayers.Count;
        internal bool MarkAttempted(int playerId) => attemptedPlayers.Add(playerId);
        internal bool MarkConfirmed(int playerId) =>
            attemptedPlayers.Contains(playerId) && confirmedPlayers.Add(playerId);
    }
}
