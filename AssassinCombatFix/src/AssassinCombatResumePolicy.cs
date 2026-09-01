// Feature: Pure eligibility rules for passive Assassin combat-resume diagnostics.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AssassinCombatFix
{
    internal static class AssassinCombatResumePolicy
    {
        public static bool IsValidNativeUnitIndex(int nativeUnitIndex, int unitCount)
        {
            return nativeUnitIndex >= 0 && nativeUnitIndex < unitCount;
        }

        public static bool ShouldLogPassiveDiagnostic(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHooksInstalled,
            bool mapActive,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType)
        {
            return modEnabled &&
                improvedPathfindingEnabled &&
                nativeHooksInstalled &&
                mapActive &&
                unitResolved &&
                aliveState == AliveState.IsAlive &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN;
        }

        public static bool ShouldLogRawResumeDiagnostic(
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType)
        {
            return unitResolved &&
                aliveState == AliveState.IsAlive &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN;
        }

        public static bool ShouldTreatAsNewTrackedUnit(
            bool hasTrackedUnit,
            uint trackedGlobalId,
            uint currentGlobalId)
        {
            return !hasTrackedUnit || trackedGlobalId != currentGlobalId;
        }

        public static bool IsWithinDiagnosticLimit(int currentCount, int maximumCount)
        {
            return currentCount >= 0 && maximumCount > 0 && currentCount < maximumCount;
        }

        public static bool ShouldBeginEditorTrace(bool mapActive, bool isMapEditor)
        {
            return !mapActive && isMapEditor;
        }

        public static bool ShouldLogStateTrace(
            bool isNewUnit,
            bool aiStateChanged,
            bool signatureChanged,
            bool activeState,
            int ticksSinceLastLog)
        {
            return isNewUnit ||
                aiStateChanged ||
                (signatureChanged && ticksSinceLastLog >= 8) ||
                (!signatureChanged && activeState && ticksSinceLastLog >= 32);
        }
    }
}
