// Feature: Pure eligibility rules for restoring Assassin path context after combat.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL
{
    internal static class AssassinCombatResumePolicy
    {
        public static bool IsValidNativeUnitIndex(int nativeUnitIndex, int unitCount)
        {
            return nativeUnitIndex >= 0 && nativeUnitIndex < unitCount;
        }

        public static bool ShouldUseAssassinPathContext(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHookInstalled,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType)
        {
            return modEnabled &&
                improvedPathfindingEnabled &&
                nativeHookInstalled &&
                unitResolved &&
                aliveState == AliveState.IsAlive &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN;
        }
    }
}
