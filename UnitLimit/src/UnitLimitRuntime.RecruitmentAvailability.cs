using CrusaderDE;
using SHCDESE.Interop;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        internal void RefreshRecruitmentButtonAvailability()
        {
            if (!settings.EnableMod || activeUnitLimits.Count == 0)
                return;

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return;

            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel?.HUDBuildingPanel == null)
                return;

            RemoveExpiredPendingRecruitments();
            HUD_Buildings panel = mainViewModel.HUDBuildingPanel;

            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARCHER, panel.RefRecruitArcherButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_SPEARMAN, panel.RefRecruitSpearmanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_MACEMAN, panel.RefRecruitMacemanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_XBOWMAN, panel.RefRecruitXBowmanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_PIKEMAN, panel.RefRecruitPikemanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_SWORDSMAN, panel.RefRecruitSwordsmanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_KNIGHT, panel.RefRecruitKnightButton);

            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ENGINEER, panel.RefRecruitEngineerButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_LADDERMAN, panel.RefRecruitLaddermanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_TUNNELER, panel.RefRecruitTunellerButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_MONK, panel.RefRecruitMonkButton);

            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_BOW, panel.RefRecruitArabBowButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_SLAVE, panel.RefRecruitArabSlaveButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_SLINGER, panel.RefRecruitArabSlingerButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_ASSASIN, panel.RefRecruitArabAssassinButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_HORSEMAN, panel.RefRecruitArabHorseArcherButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, panel.RefRecruitArabSwordsmanButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_ARAB_GRENADIER, panel.RefRecruitArabGrenadierButton);

            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, panel.RefRecruitBedouinCamelLancerButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_HEALER, panel.RefRecruitBedouinHealerButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, panel.RefRecruitBedouinEunuchButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, panel.RefRecruitBedouinAmbusherButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, panel.RefRecruitBedouinSkirmisherButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, panel.RefRecruitBedouinHeavyCamelButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, panel.RefRecruitBedouinSapperButton);
            DisableRecruitmentButtonIfLimitReached(playerId, eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, panel.RefRecruitBedouinDemolisherButton);
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
