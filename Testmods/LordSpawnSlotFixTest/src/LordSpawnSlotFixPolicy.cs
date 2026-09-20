using System;
using System.Collections.Generic;

namespace LordSpawnSlotFixTest
{
    internal enum LordSpawnSlotDecision
    {
        ClearStaleLordReference,
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

    internal readonly struct LordSpawnSlotGuardInput
    {
        internal LordSpawnSlotGuardInput(
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

    internal static class LordSpawnSlotFixPolicy
    {
        internal static LordSpawnSlotDecision Evaluate(in LordSpawnSlotGuardInput input)
        {
            if (!input.IsNewGameSession)
                return LordSpawnSlotDecision.RejectNotNewGameSession;
            if (!input.CorrectionWindowOpen)
                return LordSpawnSlotDecision.RejectOutsideCorrectionWindow;
            if (!input.HasPlayerRecord)
                return LordSpawnSlotDecision.RejectMissingPlayerRecord;
            if (input.Kicked)
                return LordSpawnSlotDecision.RejectKicked;
            if (input.AlreadyAttempted)
                return LordSpawnSlotDecision.RejectAlreadyAttempted;
            if (input.IsDefeated)
                return LordSpawnSlotDecision.RejectDefeated;
            if (!input.ValidOwnedKeep)
                return LordSpawnSlotDecision.RejectInvalidKeep;
            if (!input.ValidOwnedKeepDoorReference)
                return LordSpawnSlotDecision.RejectInvalidKeepDoorReference;
            if (input.LordUnitId <= 0)
                return LordSpawnSlotDecision.RejectMissingStoredLordUnitId;
            if (input.LordGlobalId <= 0)
                return LordSpawnSlotDecision.RejectMissingStoredLordGlobalId;
            if (!input.LordUnitResolved)
                return LordSpawnSlotDecision.RejectLordUnitUnresolved;
            if (input.LordOwnerPlayerId != 0)
                return LordSpawnSlotDecision.RejectLordOwnerNotZero;
            if (!input.LordTypeIsNull)
                return LordSpawnSlotDecision.RejectLordTypeNotNull;
            if (!input.LordAliveStateIsNone)
                return LordSpawnSlotDecision.RejectLordAliveStateNotNone;
            if (input.LordUnitGlobalId != 0)
                return LordSpawnSlotDecision.RejectLordUnitGlobalIdNotZero;
            if (input.LordCurrentHealth != 0)
                return LordSpawnSlotDecision.RejectLordHealthNotZero;

            // Do not broaden this into a generic "invalid Lord" repair. The complete zeroed
            // unit-slot identity is the evidence that distinguishes the remap tombstone from a
            // live, initializing, dying, restored, or otherwise meaningful Vanilla unit state.
            return LordSpawnSlotDecision.ClearStaleLordReference;
        }
    }

    internal sealed class LordSpawnSlotSessionState
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
        internal bool MarkAttempted(int playerId) => attemptedPlayers.Add(playerId);
        internal bool MarkConfirmed(int playerId) => confirmedPlayers.Add(playerId);
    }
}
