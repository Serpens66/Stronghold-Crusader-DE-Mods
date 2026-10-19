using APIShared.Recruitment;
using CrusaderDE;
using SHCDESE.Interop;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        // Registered on GuiChecks Post before APIShared's recruitment-availability pass (lower order):
        // expiry can notify capacity observers (knight button) and, as before, precedes the limit check.
        internal void ExpirePendingRecruitmentsForGuiCheck()
        {
            // Per GUI check: the pure count test comes before the mode-policy evaluation.
            if (activeUnitLimits.Count == 0 || !IsEffectsActive)
                return;

            if (GetLocalHumanPlayerId() <= 0)
                return;

            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel?.HUDBuildingPanel == null)
                return;

            RemoveExpiredPendingRecruitments();
        }

        internal bool IsRecruitmentAvailabilityActive() => activeUnitLimits.Count != 0 && IsEffectsActive;

        // Queried by APIShared for each still-enabled Vanilla recruit button; side-effect free.
        internal bool IsRecruitmentBlockedByLimit(RecruitmentAvailabilityContext context, eChimps unitType)
        {
            if (!context.IsLocalPlayerHuman || context.LocalPlayerId <= 0)
                return false;

            if (!activeUnitLimits.TryGetValue(unitType, out int limit) || limit < 0)
                return false;

            return GetEffectiveUnitCount(context.LocalPlayerId, unitType) >= limit;
        }
    }
}