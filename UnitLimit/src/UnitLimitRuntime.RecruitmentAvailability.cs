using CrusaderDE;
using SHCDESE.Interop;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        private readonly System.Collections.Generic.List<RecruitmentButtonRule> configuredRecruitmentButtons =
            new System.Collections.Generic.List<RecruitmentButtonRule>();
        private static readonly RecruitmentButtonRule[] RecruitmentButtonRules =
        {
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARCHER, panel => panel.RefRecruitArcherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_SPEARMAN, panel => panel.RefRecruitSpearmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_MACEMAN, panel => panel.RefRecruitMacemanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_XBOWMAN, panel => panel.RefRecruitXBowmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_PIKEMAN, panel => panel.RefRecruitPikemanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_SWORDSMAN, panel => panel.RefRecruitSwordsmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_KNIGHT, panel => panel.RefRecruitKnightButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ENGINEER, panel => panel.RefRecruitEngineerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_LADDERMAN, panel => panel.RefRecruitLaddermanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_TUNNELER, panel => panel.RefRecruitTunellerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_MONK, panel => panel.RefRecruitMonkButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_BOW, panel => panel.RefRecruitArabBowButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_SLAVE, panel => panel.RefRecruitArabSlaveButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_SLINGER, panel => panel.RefRecruitArabSlingerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_ASSASIN, panel => panel.RefRecruitArabAssassinButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_HORSEMAN, panel => panel.RefRecruitArabHorseArcherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, panel => panel.RefRecruitArabSwordsmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_GRENADIER, panel => panel.RefRecruitArabGrenadierButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, panel => panel.RefRecruitBedouinCamelLancerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, panel => panel.RefRecruitBedouinHealerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, panel => panel.RefRecruitBedouinEunuchButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, panel => panel.RefRecruitBedouinAmbusherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, panel => panel.RefRecruitBedouinSkirmisherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, panel => panel.RefRecruitBedouinHeavyCamelButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, panel => panel.RefRecruitBedouinSapperButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, panel => panel.RefRecruitBedouinDemolisherButton),
        };

        private readonly struct RecruitmentButtonRule
        {
            internal RecruitmentButtonRule(eChimps unitType, System.Func<HUD_Buildings, Noesis.UIElement> getButton)
            {
                UnitType = unitType;
                GetButton = getButton;
            }
            internal eChimps UnitType { get; }
            internal System.Func<HUD_Buildings, Noesis.UIElement> GetButton { get; }
        }

        private void RebuildConfiguredRecruitmentButtons()
        {
            configuredRecruitmentButtons.Clear();
            foreach (RecruitmentButtonRule rule in RecruitmentButtonRules)
                if (activeUnitLimits.TryGetValue(rule.UnitType, out int limit) && limit >= 0)
                    configuredRecruitmentButtons.Add(rule);
        }

        internal void RefreshRecruitmentButtonAvailability()
        {
            if (!IsUnitLimitModeAllowed() || !EffectsEnabled || activeUnitLimits.Count == 0)
                return;

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return;

            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel?.HUDBuildingPanel == null)
                return;

            RemoveExpiredPendingRecruitments();
            HUD_Buildings panel = mainViewModel.HUDBuildingPanel;

            // Resolve from the current panel; retain Vanilla/other-mod disabled states.
            foreach (RecruitmentButtonRule rule in configuredRecruitmentButtons)
                DisableRecruitmentButtonIfLimitReached(playerId, rule.UnitType, rule.GetButton(panel));
        }

        private void DisableRecruitmentButtonIfLimitReached(int playerId, eChimps unitType, Noesis.UIElement button)
        {
            if (button == null || !button.IsEnabled)
                return;

            if (!activeUnitLimits.TryGetValue(unitType, out int limit) || limit < 0)
                return;

            int count = CountAliveUnits(playerId, unitType) + GetPendingRecruitmentCount(playerId, unitType);
            if (count >= limit)
                button.IsEnabled = false;
        }
    }
}
