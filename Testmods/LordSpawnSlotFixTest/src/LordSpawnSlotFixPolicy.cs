using System;
using System.Collections.Generic;

namespace LordSpawnSlotFixTest
{
    internal enum LordSpawnSlotDecision
    {
        Correct,
        RejectIneligibleSession,
        RejectMissingPlayerRecord,
        RejectNotInRoster,
        RejectKicked,
        RejectAlreadyAttempted,
        RejectNotDefeated,
        RejectLordPresent,
        RejectInvalidKeep,
        RejectInvalidKeepDoor
    }

    internal readonly struct LordSpawnSlotGuardInput
    {
        internal LordSpawnSlotGuardInput(
            bool sessionEligible,
            bool hasPlayerRecord,
            bool inRoster,
            bool kicked,
            bool alreadyAttempted,
            bool isDefeated,
            int lordUnitId,
            bool validOwnedKeep,
            bool validOwnedKeepDoor)
        {
            SessionEligible = sessionEligible;
            HasPlayerRecord = hasPlayerRecord;
            InRoster = inRoster;
            Kicked = kicked;
            AlreadyAttempted = alreadyAttempted;
            IsDefeated = isDefeated;
            LordUnitId = lordUnitId;
            ValidOwnedKeep = validOwnedKeep;
            ValidOwnedKeepDoor = validOwnedKeepDoor;
        }

        internal bool SessionEligible { get; }
        internal bool HasPlayerRecord { get; }
        internal bool InRoster { get; }
        internal bool Kicked { get; }
        internal bool AlreadyAttempted { get; }
        internal bool IsDefeated { get; }
        internal int LordUnitId { get; }
        internal bool ValidOwnedKeep { get; }
        internal bool ValidOwnedKeepDoor { get; }
    }

    internal static class LordSpawnSlotFixPolicy
    {
        internal static LordSpawnSlotDecision Evaluate(in LordSpawnSlotGuardInput input)
        {
            if (!input.SessionEligible)
                return LordSpawnSlotDecision.RejectIneligibleSession;
            if (!input.HasPlayerRecord)
                return LordSpawnSlotDecision.RejectMissingPlayerRecord;
            if (!input.InRoster)
                return LordSpawnSlotDecision.RejectNotInRoster;
            if (input.Kicked)
                return LordSpawnSlotDecision.RejectKicked;
            if (input.AlreadyAttempted)
                return LordSpawnSlotDecision.RejectAlreadyAttempted;
            if (!input.IsDefeated)
                return LordSpawnSlotDecision.RejectNotDefeated;
            if (input.LordUnitId != 0)
                return LordSpawnSlotDecision.RejectLordPresent;
            if (!input.ValidOwnedKeep)
                return LordSpawnSlotDecision.RejectInvalidKeep;
            if (!input.ValidOwnedKeepDoor)
                return LordSpawnSlotDecision.RejectInvalidKeepDoor;
            return LordSpawnSlotDecision.Correct;
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
