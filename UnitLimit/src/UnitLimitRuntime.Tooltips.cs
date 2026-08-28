using CrusaderDE;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        private const BindingFlags MainViewModelFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo LastTroopBuildChimpField = typeof(MainViewModel).GetField("lastTroopBuildChimp", MainViewModelFlags);
        private static readonly PropertyInfo LastTroopBuildChimpProperty = typeof(MainViewModel).GetProperty("lastTroopBuildChimp", MainViewModelFlags);
        private static readonly FieldInfo LastTroopsAmountToMakeField = typeof(MainViewModel).GetField("lastTroopsAmountToMake", MainViewModelFlags);
        private static readonly PropertyInfo LastTroopsAmountToMakeProperty = typeof(MainViewModel).GetProperty("lastTroopsAmountToMake", MainViewModelFlags);
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

            if (IsMapEditor())
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
            int count = CountAliveUnits(playerId, currentTooltipUnitType) +
                GetPendingRecruitmentCount(playerId, currentTooltipUnitType);
            UnitLimitTooltip.Show(count, limit);
        }

        private void ClearUnitLimitTooltip()
        {
            hasCurrentTooltipUnitType = false;
            currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
            UnitLimitTooltip.Clear();
        }

        private static eChimps GetLastTroopBuildChimp(MainViewModel mainViewModel)
        {
            object value = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopBuildChimpField,
                LastTroopBuildChimpProperty);
            if (value == null)
                return eChimps.CHIMP_TYPE_ARCHER;

            try
            {
                return (eChimps)Convert.ToInt32(value);
            }
            catch
            {
                return eChimps.CHIMP_TYPE_ARCHER;
            }
        }

        private static bool TryGetCurrentVanillaRecruitAmount(eChimps unitType, out int amount)
        {
            amount = 0;
            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel == null)
                return false;

            object unitValue = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopBuildChimpField,
                LastTroopBuildChimpProperty);
            object amountValue = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopsAmountToMakeField,
                LastTroopsAmountToMakeProperty);
            if (unitValue == null || amountValue == null)
                return false;

            try
            {
                if ((eChimps)Convert.ToInt32(unitValue) != unitType)
                    return false;

                amount = Math.Max(0, Convert.ToInt32(amountValue));
                return true;
            }
            catch
            {
                amount = 0;
                return false;
            }
        }

        private static object GetMainViewModelMemberValue(MainViewModel mainViewModel, FieldInfo field, PropertyInfo property)
        {
            if (mainViewModel == null)
                return null;

            if (field != null)
                return field.GetValue(mainViewModel);

            return property?.GetValue(mainViewModel);
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
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }
    }
}

