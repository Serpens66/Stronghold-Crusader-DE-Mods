using APIShared.Presentation;
using System.Collections.Generic;
using CrusaderDE;
using SHCDESE.Interop;
using System;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {

        private eChimps currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
        private bool hasCurrentTooltipUnitType;

        private IReadOnlyList<TooltipLine> unitLimitLines = Array.Empty<TooltipLine>();
        private string lastUnitLimitText;
        private TooltipTarget? currentLimitTooltipTarget;

        private void RegisterUnitLimitTooltip(TooltipTarget target, string id)
        {
            if (!TooltipPresentation.TryRegisterContent(UnitLimitPlugin.PluginGuid, id, target,
                TooltipPlacement.BeforeMainLine, GetUnitLimitTooltip, out string reason)) throw new InvalidOperationException(reason);
            if (target == TooltipTarget.SiegeConstruction && !TooltipPresentation.TryRegisterText(UnitLimitPlugin.PluginGuid,
                "SiegeNotification", target, TooltipPlacement.AfterMainLine,
                context => context.IsActive && IsEffectsActive && SiegeLimitNotification.IsVisible ? SiegeLimitNotification.Message : null,
                out reason, order: 1)) throw new InvalidOperationException(reason);
        }

        private IReadOnlyList<TooltipLine> GetUnitLimitTooltip(TooltipContext context)
        {
            if (!context.IsActive || !context.UnitType.HasValue)
            {
                if (currentLimitTooltipTarget == context.Target) { ClearUnitLimitTooltip(); currentLimitTooltipTarget = null; }
                return Array.Empty<TooltipLine>();
            }
            currentLimitTooltipTarget = context.Target;
            UpdateUnitLimitTooltip((eChimps)context.UnitType.Value);
            return unitLimitLines;
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
                unitLimitLines = Array.Empty<TooltipLine>();
                lastUnitLimitText = null;
                return;
            }

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0 ||
                !SoldierChimps.Contains(currentTooltipUnitType) ||
                !activeUnitLimits.TryGetValue(currentTooltipUnitType, out int limit) ||
                limit < 0)
            {
                unitLimitLines = Array.Empty<TooltipLine>();
                lastUnitLimitText = null;
                return;
            }

            RemoveExpiredPendingRecruitments();
            int count = GetEffectiveUnitCount(playerId, currentTooltipUnitType);
            string text = SerpLocalization.Get(SerpLocalization.Limit) + ": " + count + "/" + limit;
            if (text != lastUnitLimitText)
            { lastUnitLimitText = text; unitLimitLines = new[] { new TooltipLine(new TooltipText(text, 0xFFEFF3C6)) }; }
        }

        private void ClearUnitLimitTooltip()
        {
            hasCurrentTooltipUnitType = false;
            currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
            unitLimitLines = Array.Empty<TooltipLine>();
            lastUnitLimitText = null;
        }

        // Both fields are public in the shipped Assembly-CSharp; typed reads avoid reflection and boxing.

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

    }
}

