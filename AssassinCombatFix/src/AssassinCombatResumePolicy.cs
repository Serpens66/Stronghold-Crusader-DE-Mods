// Feature: Pure eligibility rules for the audited Assassin state-122 callsite.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AssassinCombatFix
{
    internal static class AssassinCombatResumePolicy
    {
        public const ushort PostCombatRepathState = 122;

        public static bool IsValidNativeUnitIndex(int nativeUnitIndex, int unitCount)
        {
            return nativeUnitIndex >= 0 && nativeUnitIndex < unitCount;
        }

        public static bool ShouldInjectPostCombatPathContext(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHooksInstalled,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            ushort aiState,
            int currentPathContext)
        {
            return modEnabled &&
                improvedPathfindingEnabled &&
                nativeHooksInstalled &&
                unitResolved &&
                aliveState == AliveState.IsAlive &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN &&
                aiState == PostCombatRepathState &&
                currentPathContext == 0;
        }

        public static bool ShouldLogCallsiteDiagnostic(
            bool unitResolved,
            eChimps unitType,
            ushort aiState)
        {
            return unitResolved &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN &&
                aiState == PostCombatRepathState;
        }
    }
}
