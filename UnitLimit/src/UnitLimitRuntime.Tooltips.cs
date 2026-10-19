using CrusaderDE;
using SHCDESE.Interop;
using System;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {

        private eChimps currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
        private bool hasCurrentTooltipUnitType;

        private void UpdateRecruitmentLimitTooltip(MainViewModel mainViewModel)
        {
            if (mainViewModel == null)
            {
                ClearUnitLimitTooltip();
                return;
            }

            UpdateUnitLimitTooltip(GetLastTroopBuildChimp(mainViewModel));
        }

        private void UpdateSiegeBuildLimitTooltip(object parameter)
        {
            if (!TryGetSiegeBuildHoverUnit(parameter, out eChimps unitType))
            {
                ClearUnitLimitTooltip();
                return;
            }

            UpdateUnitLimitTooltip(unitType);
        }

        private void UpdateUnitLimitTooltip(eChimps unitType)
        {
            hasCurrentTooltipUnitType = true;
            currentTooltipUnitType = unitType;
            RefreshCurrentUnitLimitTooltip();
        }

        private void RefreshCurrentUnitLimitTooltip()
        {
            if (!hasCurrentTooltipUnitType)
                return;

            if (!IsEffectsActive)
            {
                UnitLimitTooltip.Clear();
                return;
            }

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0 ||
                !SoldierChimps.Contains(currentTooltipUnitType) ||
                !activeUnitLimits.TryGetValue(currentTooltipUnitType, out int limit) ||
                limit < 0)
            {
                UnitLimitTooltip.Clear();
                return;
            }

            RemoveExpiredPendingRecruitments();
            int count = GetEffectiveUnitCount(playerId, currentTooltipUnitType);
            UnitLimitTooltip.Show(count, limit);
        }

        private void ClearUnitLimitTooltip()
        {
            hasCurrentTooltipUnitType = false;
            currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
            UnitLimitTooltip.Clear();
        }

        // Both fields are public in the shipped Assembly-CSharp; typed reads avoid reflection and boxing.
        private static eChimps GetLastTroopBuildChimp(MainViewModel mainViewModel)
        {
            if (mainViewModel == null)
                return eChimps.CHIMP_TYPE_ARCHER;

            return (eChimps)(int)mainViewModel.lastTroopBuildChimp;
        }

        private static bool TryGetCurrentVanillaRecruitAmount(eChimps unitType, out int amount)
        {
            amount = 0;
            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel == null)
                return false;

            if ((eChimps)(int)mainViewModel.lastTroopBuildChimp != unitType)
                return false;

            amount = Math.Max(0, mainViewModel.lastTroopsAmountToMake);
            return true;
        }

        private static bool TryGetSiegeBuildHoverUnit(object parameter, out eChimps unitType)
        {
            switch (parameter as string)
            {
                case "UnitBuildCat":
                    unitType = eChimps.CHIMP_TYPE_CATAPULT;
                    return true;
                case "UnitBuildTreb":
                    unitType = eChimps.CHIMP_TYPE_TREBUCHET;
                    return true;
                case "UnitBuildRam":
                    unitType = eChimps.CHIMP_TYPE_BATTERING_RAM;
                    return true;
                case "UnitBuildTower":
                    unitType = eChimps.CHIMP_TYPE_SIEGE_TOWER;
                    return true;
                case "UnitbuildMantlet":
                    unitType = eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
                    return true;
                case "UnitbuildArabBallista":
                    unitType = eChimps.CHIMP_TYPE_ARAB_BALLISTA;
                    return true;
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }
    }
}

