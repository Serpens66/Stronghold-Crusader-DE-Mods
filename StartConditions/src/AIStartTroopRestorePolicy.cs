using SHCDESE.Interop.Enums;

namespace StartConditions
{
    internal static class AIStartTroopRestorePolicy
    {
        internal const ushort IsolationMarker = ushort.MaxValue;
        internal const ushort IsolationRelatedValue = 0;

        internal static bool ValidateUnit(AIStartTroopIsolationSaveRecord saved, uint globalId,
            AliveState aliveState, int ownerPlayerId, bool ownerIsAI, ushort marker, ushort related,
            out string reason)
        {
            if (globalId != saved.UnitGlobalId)
                reason = $"unit-identity-mismatch: expectedGlobalId={saved.UnitGlobalId}, actualGlobalId={globalId}";
            else if (aliveState != AliveState.NeedsInit && aliveState != AliveState.IsAlive)
                reason = $"unit-inactive: globalId={globalId}, actualAliveState={aliveState}";
            else if (ownerPlayerId != saved.OwnerPlayerId)
                reason = $"unit-owner-mismatch: globalId={globalId}, expectedOwner={saved.OwnerPlayerId}, actualOwner={ownerPlayerId}";
            else if (!ownerIsAI)
                reason = $"unit-owner-not-ai: globalId={globalId}, owner={ownerPlayerId}";
            else if (marker != IsolationMarker || related != IsolationRelatedValue)
                reason = $"unit-isolation-marker-mismatch: globalId={globalId}, expectedMarker={IsolationMarker}/{IsolationRelatedValue}, actualMarker={marker}/{related}";
            else
            {
                reason = null;
                return true;
            }
            return false;
        }

        internal static bool ValidatePrivateTribe(AIStartTroopIsolationSaveRecord saved, int tribeId,
            uint globalId, AliveState aliveState, int ownerPlayerId, int unitTribeId, out string reason)
        {
            if (globalId != saved.PrivateTribeGlobalId ||
                (aliveState != AliveState.NeedsInit && aliveState != AliveState.IsAlive) ||
                ownerPlayerId != saved.OwnerPlayerId || unitTribeId != tribeId)
            {
                reason = $"private-tribe-conflict: expectedGlobalId={saved.PrivateTribeGlobalId}, actualGlobalId={globalId}, " +
                    $"expectedOwner={saved.OwnerPlayerId}, actualOwner={ownerPlayerId}, actualAliveState={aliveState}, " +
                    $"expectedUnitTribeId={tribeId}, actualUnitTribeId={unitTribeId}";
                return false;
            }
            reason = null;
            return true;
        }
    }
}
