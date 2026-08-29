// Feature: Pure eligibility rules for restoring Assassin path context after combat.
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

        public static bool ShouldInjectPostCombatPathContext(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHookInstalled,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            ushort aiState,
            int currentPathContext)
        {
            return ShouldUseAssassinPathContext(
                    modEnabled,
                    improvedPathfindingEnabled,
                    nativeHookInstalled,
                    unitResolved,
                    aliveState,
                    unitType) &&
                aiState == PostCombatRepathState &&
                currentPathContext == 0;
        }

        public static bool ShouldLogResumeDiagnostic(bool unitResolved, eChimps unitType)
        {
            return unitResolved && unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN;
        }

        public static bool ShouldLogDirectRepathDiagnostic(
            bool unitResolved,
            eChimps unitType,
            ushort aiState)
        {
            return ShouldLogResumeDiagnostic(unitResolved, unitType) &&
                aiState == PostCombatRepathState;
        }
    }
}
