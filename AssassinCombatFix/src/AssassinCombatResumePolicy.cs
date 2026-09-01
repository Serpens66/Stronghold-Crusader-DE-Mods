// Feature: Pure eligibility rules for Assassin post-combat path resumption.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AssassinCombatFix
{
    internal static class AssassinCombatResumePolicy
    {
        public static bool TryConvertUnitIdToSpanIndex(
            int unitId,
            int unitCount,
            out int spanIndex)
        {
            spanIndex = unitId - 1;
            return unitId > 0 && spanIndex < unitCount;
        }

        public static bool ShouldProcessPostCombatPathRequest(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHooksInstalled,
            bool expectedCaller)
        {
            return modEnabled &&
                improvedPathfindingEnabled &&
                nativeHooksInstalled &&
                expectedCaller;
        }

        public static bool IsEligibleAssassin(
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            ushort aiState)
        {
            return unitResolved &&
                aliveState == AliveState.IsAlive &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN &&
                aiState == 106;
        }
    }
}
