using CrusaderDE;
using SHCDESE.Interop;
using System;
using System.Reflection;

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
            object value = GetMainViewModelMemberValue(mainViewModel, "lastTroopBuildChimp");
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

        private static object GetMainViewModelMemberValue(MainViewModel mainViewModel, string memberName)
        {
            if (mainViewModel == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = typeof(MainViewModel).GetField(memberName, flags);
            if (field != null)
                return field.GetValue(mainViewModel);

            PropertyInfo property = typeof(MainViewModel).GetProperty(memberName, flags);
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

