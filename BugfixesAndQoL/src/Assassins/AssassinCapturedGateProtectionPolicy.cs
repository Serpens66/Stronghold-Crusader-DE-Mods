using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
namespace BugfixesAndQoL
{
    internal static class AssassinCapturedGateProtectionPolicy
    {
        internal static bool ShouldProtect(in GameUnit unit, in GameBuilding building,
            bool enabled, bool protect, bool isAI, bool alliedCapturer)
        {
            return enabled && protect && isAI && IsEligibleUnit(in unit) && IsProtectedGate(in building, alliedCapturer);
        }

        // Cheap unit-only filter runs before any player or building lookup in the live callback.
        internal static bool IsEligibleUnit(in GameUnit unit)
        {
            if (unit.r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                !APIShared.UnitAccess.IsReallyAlive(in unit) || unit.r_ControllableForPlayerId < 1 ||
                unit.r_ControllableForPlayerId > 8 ||
                (unit.r_AIState != 101 && unit.r_AIState != 107) || unit.r_AI_ContextTargetBuildingTileId == 0 ||
                unit.r_AI_ContextTargetBuildingTileId >= 320800) return false;
            // Transit and climbing remain entirely under Vanilla's movement state machine.
            if (unit.r_AIState == 101 && (unit.r_PathPlanStateBitFlags != 0 ||
                unit.r_CurrentTilePositionX != unit.r_ContextTargetTileX ||
                unit.r_CurrentTilePositionY != unit.r_ContextTargetTileY)) return false;
            return true;
        }

        internal static bool IsProtectedGate(in GameBuilding building, bool alliedCapturer)
        {
            return building.r_AliveState != AliveState.None && building.r_AliveState != AliveState.MarkedForDeletion &&
                (building.r_BuildingType == eStructs.STRUCT_GATE_MAIN || building.r_BuildingType == eStructs.STRUCT_GATE_INNER) &&
                building.r_CapturedByPlayerId >= 1 && building.r_CapturedByPlayerId <= 8 && alliedCapturer;
        }
    }
}
