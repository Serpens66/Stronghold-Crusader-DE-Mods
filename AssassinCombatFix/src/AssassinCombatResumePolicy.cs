// Feature: Pure eligibility rules for the audited Assassin state-106 resume path.
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

        public static bool IsConfirmedCombatFinishCallerRva(long returnRva)
        {
            return returnRva == AssassinCombatResumeNativeDefinition.CombatFinishResumeReturnRva;
        }

        public static bool IsEligiblePostCombatPathRequest(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHooksInstalled,
            bool confirmedCombatFinishCaller,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType)
        {
            return modEnabled &&
                improvedPathfindingEnabled &&
                nativeHooksInstalled &&
                confirmedCombatFinishCaller &&
                unitResolved &&
                aliveState == AliveState.IsAlive &&
                unitType == eChimps.CHIMP_TYPE_ARAB_ASSASIN;
        }

        public static bool ShouldSetAssassinPathContext(bool eligible, int currentContextFlag)
        {
            return eligible && currentContextFlag == 0;
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
