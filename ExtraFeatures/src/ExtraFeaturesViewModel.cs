// Feature: Lobby settings model for all Extra Features options.
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExtraFeatures
{
    public sealed class ExtraFeaturesViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        public event Action<string> SettingChanged;

        private bool enableMod = true;
        private int woodRefundPercent = -1;
        private int stoneRefundPercent = -1;
        private int ironRefundPercent = -1;
        private int pitchRefundPercent = -1;
        private int goldRefundPercent = -1;
        private double multiplyGoodsGainAI = 1.0;
        private double multiplyGoodsGainHuman = 1.0;
        private double multiplyGoodsGainInMoneyAI;
        private double multiplyGoodsGainInMoneyHuman;
        private double marketBuyPriceMultiplier = 1.0;
        private double marketSellPriceMultiplier = 1.0;
        private bool marketPricesAlsoForAI;
        private double[] marketGoodBuyPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
        private double[] marketGoodSellPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
        private double plagueDurationMultiplier = 2.0;
        private int apothecaryPlagueSearchDistance = 50;
        private int campfirePeasantsLimit = -1;
        private int humanLordHealthPercent = LordHealthMultiplierPolicy.DefaultPercent;
        private int aiLordHealthPercent = LordHealthMultiplierPolicy.DefaultPercent;
        private bool keepStorageContent = true;
        private bool enableClientFeatures = true;
        private bool enableAllyGoodsAmountModifiers = true;
        private bool enableCtrlSingleMarketTrade = true;
        private bool enableSingleBuildingPause = true;
        private bool enableMultiplayerGameSpeedChanges = true;
        private bool enableShiftGameSpeedSteps = true;
        private bool enableFastRecruitRallyMovement = true;
        private bool enableMonksAlwaysRun;
        private bool enableKnightDismount = true;
        private bool instantHorse;
        private bool enableQuarryPileRelocation = true;
        private bool enableExtraChurchPriests = true;
        private bool preventAIPause = true;
        private bool preventEmergencyDemolition = true;
        private bool preventHovelDeletion = true;
        private double humanGateReopenDelaySeconds = GatehouseTimingPatch.VanillaHumanDelaySeconds;
        private double aiGateReopenDelaySeconds = GatehouseTimingPatch.VanillaAiDelaySeconds;
        private double humanGateClosingDistanceTiles = GatehouseTimingPatch.VanillaHumanDistanceTiles;
        private double aiGateClosingDistanceTiles = GatehouseTimingPatch.VanillaAiDistanceTiles;
        private bool requireReachableEnemyForAutomaticGateClosing = true;
        private bool marketGoodPriceVisualsResolved;

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public ExtraFeaturesViewModel(bool legacySomeSettingsLoaded)
        {
            LegacyModWarningVisibility = legacySomeSettingsLoaded ? Visibility.Visible : Visibility.Collapsed;
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public ObservableCollection<MarketGoodPriceItemViewModel> MarketGoodPriceItems { get; } =
            new ObservableCollection<MarketGoodPriceItemViewModel>();
        public Visibility LegacyModWarningVisibility { get; }
        public string LegacyModWarningText => SerpLocalization.Get(SerpLocalization.LegacySomeSettingsWarning);
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string EnableClientFeaturesText => SerpLocalization.Get(SerpLocalization.EnableClientFeatures);
        public string EnableClientFeaturesHelpText => SerpLocalization.Get(SerpLocalization.EnableClientFeaturesHelp);
        public ImageSource WoodRefundIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource StoneRefundIcon => GetGoodIconImage(eGoods.STORED_STONE_BLOCKS);
        public ImageSource IronRefundIcon => GetGoodIconImage(eGoods.STORED_IRON_INGOTS);
        public ImageSource PitchRefundIcon => GetGoodIconImage(eGoods.STORED_PITCH_RAW);
        public ImageSource GoldRefundIcon => GetGoodIconImage(eGoods.STORED_GOLD);
        public ImageSource KeepStorageFruitIcon => GetGoodIconImage(eGoods.STORED_FOOD_FRUIT);
        public ImageSource KeepStorageWoodIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource KeepStorageBowsIcon => GetGoodIconImage(eGoods.STORED_BOWS);
        public string EnableCtrlSingleMarketTradeText => SerpLocalization.Get(SerpLocalization.EnableCtrlSingleMarketTrade);
        public string EnableCtrlSingleMarketTradeHelpText => SerpLocalization.Get(SerpLocalization.EnableCtrlSingleMarketTradeHelp);
        public string EnableAllyGoodsAmountModifiersText => SerpLocalization.Get(SerpLocalization.EnableAllyGoodsAmountModifiers);
        public string EnableAllyGoodsAmountModifiersHelpText => SerpLocalization.Get(SerpLocalization.EnableAllyGoodsAmountModifiersHelp);
        public string EnableSingleBuildingPauseText => SerpLocalization.Get(SerpLocalization.EnableSingleBuildingPause);
        public string EnableSingleBuildingPauseHelpText => SerpLocalization.Get(SerpLocalization.EnableSingleBuildingPauseHelp);
        public string EnableMultiplayerGameSpeedChangesText => SerpLocalization.Get(SerpLocalization.EnableMultiplayerGameSpeedChanges);
        public string EnableMultiplayerGameSpeedChangesHelpText => SerpLocalization.Get(SerpLocalization.EnableMultiplayerGameSpeedChangesHelp);
        public string EnableShiftGameSpeedStepsText => SerpLocalization.Get(SerpLocalization.EnableShiftGameSpeedSteps);
        public string EnableShiftGameSpeedStepsHelpText => SerpLocalization.Get(SerpLocalization.EnableShiftGameSpeedStepsHelp);
        public string EnableFastRecruitRallyMovementText => SerpLocalization.Get(SerpLocalization.EnableFastRecruitRallyMovement);
        public string EnableFastRecruitRallyMovementHelpText => SerpLocalization.Get(SerpLocalization.EnableFastRecruitRallyMovementHelp);
        public string EnableMonksAlwaysRunText => SerpLocalization.Get(SerpLocalization.EnableMonksAlwaysRun);
        public string EnableMonksAlwaysRunHelpText => SerpLocalization.Get(SerpLocalization.EnableMonksAlwaysRunHelp);
        public string EnableKnightDismountText => SerpLocalization.Get(SerpLocalization.EnableKnightDismount);
        public string EnableKnightDismountHelpText => SerpLocalization.Get(SerpLocalization.EnableKnightDismountHelp);
        public string InstantHorseText => SerpLocalization.Get(SerpLocalization.InstantHorse);
        public string InstantHorseHelpText => SerpLocalization.Get(SerpLocalization.InstantHorseHelp);
        public string EnableQuarryPileRelocationText => SerpLocalization.Get(SerpLocalization.EnableQuarryPileRelocation);
        public string EnableQuarryPileRelocationHelpText => SerpLocalization.Get(SerpLocalization.EnableQuarryPileRelocationHelp);
        public string EnableExtraChurchPriestsText => SerpLocalization.Get(SerpLocalization.EnableExtraChurchPriests);
        public string EnableExtraChurchPriestsHelpText => SerpLocalization.Get(SerpLocalization.EnableExtraChurchPriestsHelp);
        public string CampfirePeasantsText => SerpLocalization.Get(SerpLocalization.CampfirePeasants);
        public string CampfirePeasantsHelpText => SerpLocalization.Get(SerpLocalization.CampfirePeasantsHelp);
        public string PlagueDurationMultiplierText => SerpLocalization.Get(SerpLocalization.PlagueDurationMultiplier);
        public string PlagueDurationMultiplierHelpText => SerpLocalization.Get(SerpLocalization.PlagueDurationMultiplierHelp);
        public string ApothecaryPlagueSearchDistanceText => SerpLocalization.Get(SerpLocalization.ApothecaryPlagueSearchDistance);
        public string ApothecaryPlagueSearchDistanceHelpText => SerpLocalization.Get(SerpLocalization.ApothecaryPlagueSearchDistanceHelp);
        public string BulldozeTitleText => SerpLocalization.Get(SerpLocalization.BulldozeTitle);
        public string ComfortTitleText => SerpLocalization.Get("SomeSettings.ComfortTitle");
        public string NewFeaturesTitleText => SerpLocalization.Get("SomeSettings.NewFeaturesTitle");
        public string LordHealthTitleText => SerpLocalization.Get(SerpLocalization.LordHealthTitle);
        public string LordHealthHelpText => SerpLocalization.Get(SerpLocalization.LordHealthHelp);
        public string BuildingsProductionTitleText => SerpLocalization.Get(SerpLocalization.BuildingsProductionTitle);
        public string PlagueTitleText => SerpLocalization.Get("SomeSettings.PlagueTitle");
        public string BulldozeHelpText => SerpLocalization.Get(SerpLocalization.BulldozeHelp);
        public string WoodRefundText => SerpLocalization.Get(SerpLocalization.WoodRefund);
        public string StoneRefundText => SerpLocalization.Get(SerpLocalization.StoneRefund);
        public string IronRefundText => SerpLocalization.Get(SerpLocalization.IronRefund);
        public string PitchRefundText => SerpLocalization.Get(SerpLocalization.PitchRefund);
        public string GoldRefundText => SerpLocalization.Get(SerpLocalization.GoldRefund);
        public string VanillaValue50Text => SerpLocalization.Get(SerpLocalization.VanillaValue50);
        public string KeepStorageContentText => SerpLocalization.Get(SerpLocalization.KeepStorageContent);
        public string KeepStorageContentHelpText => SerpLocalization.Get(SerpLocalization.KeepStorageContentHelp);
        public string EconomyBuffsTitleText => SerpLocalization.Get(SerpLocalization.EconomyBuffsTitle);
        public string AiText => SerpLocalization.Get(SerpLocalization.Ai);
        public string HumanText => SerpLocalization.Get(SerpLocalization.Human);
        public string MultiplyGoodsGainText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsGain);
        public string MultiplyGoodsGainHelpText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsGainHelp);
        public string MultiplyGoodsAsMoneyText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsAsMoney);
        public string MultiplyGoodsAsMoneyHelpText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsAsMoneyHelp);
        public string MarketPriceMultipliersTitleText => SerpLocalization.Get(SerpLocalization.MarketPriceMultipliersTitle);
        public string MarketBuyPriceMultiplierText => SerpLocalization.Get(SerpLocalization.MarketBuyPriceMultiplier);
        public string MarketBuyPriceMultiplierHelpText => SerpLocalization.Get(SerpLocalization.MarketBuyPriceMultiplierHelp);
        public string MarketSellPriceMultiplierText => SerpLocalization.Get(SerpLocalization.MarketSellPriceMultiplier);
        public string MarketSellPriceMultiplierHelpText => SerpLocalization.Get(SerpLocalization.MarketSellPriceMultiplierHelp);
        public string MarketPricesAlsoForAIText => SerpLocalization.Get(SerpLocalization.MarketPricesAlsoForAI);
        public string MarketPricesAlsoForAIHelpText => SerpLocalization.Get(SerpLocalization.MarketPricesAlsoForAIHelp);
        public string MarketGoodPriceMultipliersHelpText => SerpLocalization.Get(SerpLocalization.MarketGoodPriceMultipliersHelp);
        public string AiEconomyProtectionTitleText => SerpLocalization.Get(SerpLocalization.AiEconomyProtectionTitle);
        public string PreventAIPauseText => SerpLocalization.Get(SerpLocalization.PreventAIPause);
        public string PreventAIPauseHelpText => SerpLocalization.Get(SerpLocalization.PreventAIPauseHelp);
        public string PreventEmergencyDemolitionText => SerpLocalization.Get(SerpLocalization.PreventEmergencyDemolition);
        public string PreventEmergencyDemolitionHelpText => SerpLocalization.Get(SerpLocalization.PreventEmergencyDemolitionHelp);
        public string PreventHovelDeletionText => SerpLocalization.Get(SerpLocalization.PreventHovelDeletion);
        public string PreventHovelDeletionHelpText => SerpLocalization.Get(SerpLocalization.PreventHovelDeletionHelp);
        public string GatehousesTitleText => SerpLocalization.Get("SomeSettings.GatehousesTitle");
        public string HumanGateReopenDelayText => SerpLocalization.Get("SomeSettings.HumanGateReopenDelay");
        public string AIGateReopenDelayText => SerpLocalization.Get("SomeSettings.AIGateReopenDelay");
        public string GateReopenDelayHelpText => SerpLocalization.Get("SomeSettings.GateReopenDelayHelp");
        public string HumanGateClosingDistanceText => SerpLocalization.Get("SomeSettings.HumanGateClosingDistance");
        public string AIGateClosingDistanceText => SerpLocalization.Get("SomeSettings.AIGateClosingDistance");
        public string GateClosingDistanceHelpText => SerpLocalization.Get("SomeSettings.GateClosingDistanceHelp");
        public string RequireReachableEnemyForAutomaticGateClosingText => SerpLocalization.Get("SomeSettings.RequireReachableEnemyForAutomaticGateClosing");
        public string RequireReachableEnemyForAutomaticGateClosingHelpText => SerpLocalization.Get("SomeSettings.RequireReachableEnemyForAutomaticGateClosingHelp");

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => SetSetting(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public int WoodRefundPercent { get => woodRefundPercent; set => SetIntSetting(ref woodRefundPercent, value, -1, 100, nameof(WoodRefundPercent), nameof(WoodRefundPercentValueText)); }
        [SyncHostOnly] public int StoneRefundPercent { get => stoneRefundPercent; set => SetIntSetting(ref stoneRefundPercent, value, -1, 100, nameof(StoneRefundPercent), nameof(StoneRefundPercentValueText)); }
        [SyncHostOnly] public int IronRefundPercent { get => ironRefundPercent; set => SetIntSetting(ref ironRefundPercent, value, -1, 100, nameof(IronRefundPercent), nameof(IronRefundPercentValueText)); }
        [SyncHostOnly] public int PitchRefundPercent { get => pitchRefundPercent; set => SetIntSetting(ref pitchRefundPercent, value, -1, 100, nameof(PitchRefundPercent), nameof(PitchRefundPercentValueText)); }
        [SyncHostOnly] public int GoldRefundPercent { get => goldRefundPercent; set => SetIntSetting(ref goldRefundPercent, value, -1, 100, nameof(GoldRefundPercent), nameof(GoldRefundPercentValueText)); }
        [SyncHostOnly] public bool KeepStorageContent { get => keepStorageContent; set => SetSetting(ref keepStorageContent, value, nameof(KeepStorageContent)); }
        [Shared.PresetLocal] public bool EnableClientFeatures { get => enableClientFeatures; set => SetSetting(ref enableClientFeatures, value, nameof(EnableClientFeatures)); }
        [Shared.PresetLocal] public bool EnableAllyGoodsAmountModifiers { get => enableAllyGoodsAmountModifiers; set => SetSetting(ref enableAllyGoodsAmountModifiers, value, nameof(EnableAllyGoodsAmountModifiers)); }

        public string WoodRefundPercentValueText { get => FormatRefundPercent(WoodRefundPercent); set => SetIntValueText(value, parsed => WoodRefundPercent = parsed, nameof(WoodRefundPercentValueText)); }
        public string StoneRefundPercentValueText { get => FormatRefundPercent(StoneRefundPercent); set => SetIntValueText(value, parsed => StoneRefundPercent = parsed, nameof(StoneRefundPercentValueText)); }
        public string IronRefundPercentValueText { get => FormatRefundPercent(IronRefundPercent); set => SetIntValueText(value, parsed => IronRefundPercent = parsed, nameof(IronRefundPercentValueText)); }
        public string PitchRefundPercentValueText { get => FormatRefundPercent(PitchRefundPercent); set => SetIntValueText(value, parsed => PitchRefundPercent = parsed, nameof(PitchRefundPercentValueText)); }
        public string GoldRefundPercentValueText { get => FormatRefundPercent(GoldRefundPercent); set => SetIntValueText(value, parsed => GoldRefundPercent = parsed, nameof(GoldRefundPercentValueText)); }

        [SyncHostOnly] public double MultiplyGoodsGainAI { get => multiplyGoodsGainAI; set => SetDoubleSetting(ref multiplyGoodsGainAI, value, 0.0, 5.0, nameof(MultiplyGoodsGainAI), nameof(MultiplyGoodsGainAIText)); }
        [SyncHostOnly] public double MultiplyGoodsGainHuman { get => multiplyGoodsGainHuman; set => SetDoubleSetting(ref multiplyGoodsGainHuman, value, 0.0, 5.0, nameof(MultiplyGoodsGainHuman), nameof(MultiplyGoodsGainHumanText)); }
        [SyncHostOnly] public double MultiplyGoodsGainInMoneyAI { get => multiplyGoodsGainInMoneyAI; set => SetDoubleSetting(ref multiplyGoodsGainInMoneyAI, value, 0.0, 5.0, nameof(MultiplyGoodsGainInMoneyAI), nameof(MultiplyGoodsGainInMoneyAIText)); }
        [SyncHostOnly] public double MultiplyGoodsGainInMoneyHuman { get => multiplyGoodsGainInMoneyHuman; set => SetDoubleSetting(ref multiplyGoodsGainInMoneyHuman, value, 0.0, 5.0, nameof(MultiplyGoodsGainInMoneyHuman), nameof(MultiplyGoodsGainInMoneyHumanText)); }
        [SyncHostOnly] public double MarketBuyPriceMultiplier { get => marketBuyPriceMultiplier; set => SetDoubleSetting(ref marketBuyPriceMultiplier, value, 0.0, 5.0, nameof(MarketBuyPriceMultiplier), nameof(MarketBuyPriceMultiplierValueText)); }
        [SyncHostOnly] public double MarketSellPriceMultiplier { get => marketSellPriceMultiplier; set => SetDoubleSetting(ref marketSellPriceMultiplier, value, 0.0, 5.0, nameof(MarketSellPriceMultiplier), nameof(MarketSellPriceMultiplierValueText)); }
        [SyncHostOnly] public bool MarketPricesAlsoForAI { get => marketPricesAlsoForAI; set => SetSetting(ref marketPricesAlsoForAI, value, nameof(MarketPricesAlsoForAI)); }
        [SyncHostOnly]
        public double[] MarketGoodBuyPriceMultipliers
        {
            get => (double[])marketGoodBuyPriceMultipliers.Clone();
            set => SetMarketGoodMultiplierArray(
                ref marketGoodBuyPriceMultipliers,
                value,
                nameof(MarketGoodBuyPriceMultipliers));
        }
        [SyncHostOnly]
        public double[] MarketGoodSellPriceMultipliers
        {
            get => (double[])marketGoodSellPriceMultipliers.Clone();
            set => SetMarketGoodMultiplierArray(
                ref marketGoodSellPriceMultipliers,
                value,
                nameof(MarketGoodSellPriceMultipliers));
        }
        [SyncHostOnly] public double PlagueDurationMultiplier { get => plagueDurationMultiplier; set => SetDoubleSetting(ref plagueDurationMultiplier, value, PlagueDurationPatch.MinimumMultiplier, PlagueDurationPatch.MaximumMultiplier, nameof(PlagueDurationMultiplier), nameof(PlagueDurationMultiplierValueText)); }
        [SyncHostOnly] public int ApothecaryPlagueSearchDistance { get => apothecaryPlagueSearchDistance; set => SetIntSetting(ref apothecaryPlagueSearchDistance, value, PlagueApothecarySearchRangePatch.MinimumDistance, PlagueApothecarySearchRangePatch.MaximumDistance, nameof(ApothecaryPlagueSearchDistance), nameof(ApothecaryPlagueSearchDistanceValueText)); }
        [SyncHostOnly] public int CampfirePeasantsLimit { get => campfirePeasantsLimit; set => SetIntSetting(ref campfirePeasantsLimit, value, -1, 200, nameof(CampfirePeasantsLimit), nameof(CampfirePeasantsLimitText)); }
        [SyncHostOnly] public int HumanLordHealthPercent { get => humanLordHealthPercent; set => SetIntSetting(ref humanLordHealthPercent, value, LordHealthMultiplierPolicy.MinimumPercent, LordHealthMultiplierPolicy.MaximumPercent, nameof(HumanLordHealthPercent), nameof(HumanLordHealthPercentText)); }
        [SyncHostOnly] public int AILordHealthPercent { get => aiLordHealthPercent; set => SetIntSetting(ref aiLordHealthPercent, value, LordHealthMultiplierPolicy.MinimumPercent, LordHealthMultiplierPolicy.MaximumPercent, nameof(AILordHealthPercent), nameof(AILordHealthPercentText)); }
        [SyncHostOnly] public bool EnableCtrlSingleMarketTrade { get => enableCtrlSingleMarketTrade; set => SetSetting(ref enableCtrlSingleMarketTrade, value, nameof(EnableCtrlSingleMarketTrade)); }
        [SyncHostOnly] public bool EnableSingleBuildingPause { get => enableSingleBuildingPause; set => SetSetting(ref enableSingleBuildingPause, value, nameof(EnableSingleBuildingPause)); }
        [SyncHostOnly] public bool EnableMultiplayerGameSpeedChanges { get => enableMultiplayerGameSpeedChanges; set => SetSetting(ref enableMultiplayerGameSpeedChanges, value, nameof(EnableMultiplayerGameSpeedChanges)); }
        [SyncHostOnly] public bool EnableShiftGameSpeedSteps { get => enableShiftGameSpeedSteps; set => SetSetting(ref enableShiftGameSpeedSteps, value, nameof(EnableShiftGameSpeedSteps)); }
        [SyncHostOnly] public bool EnableFastRecruitRallyMovement { get => enableFastRecruitRallyMovement; set => SetSetting(ref enableFastRecruitRallyMovement, value, nameof(EnableFastRecruitRallyMovement)); }
        [SyncHostOnly] public bool EnableMonksAlwaysRun { get => enableMonksAlwaysRun; set => SetSetting(ref enableMonksAlwaysRun, value, nameof(EnableMonksAlwaysRun)); }
        [SyncHostOnly] public bool EnableKnightDismount { get => enableKnightDismount; set => SetSetting(ref enableKnightDismount, value, nameof(EnableKnightDismount)); }
        [SyncHostOnly] public bool InstantHorse { get => instantHorse; set => SetSetting(ref instantHorse, value, nameof(InstantHorse)); }
        [SyncHostOnly] public bool EnableQuarryPileRelocation { get => enableQuarryPileRelocation; set => SetSetting(ref enableQuarryPileRelocation, value, nameof(EnableQuarryPileRelocation)); }
        [SyncHostOnly] public bool EnableExtraChurchPriests { get => enableExtraChurchPriests; set => SetSetting(ref enableExtraChurchPriests, value, nameof(EnableExtraChurchPriests)); }
        [SyncHostOnly] public bool PreventAIPause { get => preventAIPause; set => SetSetting(ref preventAIPause, value, nameof(PreventAIPause)); }
        [SyncHostOnly] public bool PreventEmergencyDemolition { get => preventEmergencyDemolition; set => SetSetting(ref preventEmergencyDemolition, value, nameof(PreventEmergencyDemolition)); }
        [SyncHostOnly] public bool PreventHovelDeletion { get => preventHovelDeletion; set => SetSetting(ref preventHovelDeletion, value, nameof(PreventHovelDeletion)); }
        [SyncHostOnly] public double HumanGateReopenDelaySeconds { get => humanGateReopenDelaySeconds; set => SetDoubleSetting(ref humanGateReopenDelaySeconds, RoundToStep(value, 0.5), GatehouseTimingPatch.MinimumHumanDelaySeconds, GatehouseTimingPatch.MaximumHumanDelaySeconds, nameof(HumanGateReopenDelaySeconds), nameof(HumanGateReopenDelayValueText)); }
        [SyncHostOnly] public double AIGateReopenDelaySeconds { get => aiGateReopenDelaySeconds; set => SetDoubleSetting(ref aiGateReopenDelaySeconds, RoundToStep(value, 2.5), GatehouseTimingPatch.MinimumAiDelaySeconds, GatehouseTimingPatch.MaximumAiDelaySeconds, nameof(AIGateReopenDelaySeconds), nameof(AIGateReopenDelayValueText)); }
        [SyncHostOnly] public double HumanGateClosingDistanceTiles { get => humanGateClosingDistanceTiles; set => SetDoubleSetting(ref humanGateClosingDistanceTiles, RoundToStep(value, 0.5), GatehouseTimingPatch.MinimumDistanceTiles, GatehouseTimingPatch.MaximumDistanceTiles, nameof(HumanGateClosingDistanceTiles), nameof(HumanGateClosingDistanceValueText)); }
        [SyncHostOnly] public double AIGateClosingDistanceTiles { get => aiGateClosingDistanceTiles; set => SetDoubleSetting(ref aiGateClosingDistanceTiles, RoundToStep(value, 0.5), GatehouseTimingPatch.MinimumDistanceTiles, GatehouseTimingPatch.MaximumDistanceTiles, nameof(AIGateClosingDistanceTiles), nameof(AIGateClosingDistanceValueText)); }
        [SyncHostOnly] public bool RequireReachableEnemyForAutomaticGateClosing { get => requireReachableEnemyForAutomaticGateClosing; set => SetSetting(ref requireReachableEnemyForAutomaticGateClosing, value, nameof(RequireReachableEnemyForAutomaticGateClosing)); }

        public string MultiplyGoodsGainAIText { get => FormatDecimalMultiplier(MultiplyGoodsGainAI); set => SetDoubleValueText(value, parsed => MultiplyGoodsGainAI = parsed, nameof(MultiplyGoodsGainAIText)); }
        public string MultiplyGoodsGainHumanText { get => FormatDecimalMultiplier(MultiplyGoodsGainHuman); set => SetDoubleValueText(value, parsed => MultiplyGoodsGainHuman = parsed, nameof(MultiplyGoodsGainHumanText)); }
        public string MultiplyGoodsGainInMoneyAIText { get => FormatDecimalMultiplier(MultiplyGoodsGainInMoneyAI); set => SetDoubleValueText(value, parsed => MultiplyGoodsGainInMoneyAI = parsed, nameof(MultiplyGoodsGainInMoneyAIText)); }
        public string MultiplyGoodsGainInMoneyHumanText { get => FormatDecimalMultiplier(MultiplyGoodsGainInMoneyHuman); set => SetDoubleValueText(value, parsed => MultiplyGoodsGainInMoneyHuman = parsed, nameof(MultiplyGoodsGainInMoneyHumanText)); }
        public string MarketBuyPriceMultiplierValueText { get => MarketBuyPriceMultiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x"; set => SetDoubleValueText(value, parsed => MarketBuyPriceMultiplier = parsed, nameof(MarketBuyPriceMultiplierValueText)); }
        public string MarketSellPriceMultiplierValueText { get => MarketSellPriceMultiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x"; set => SetDoubleValueText(value, parsed => MarketSellPriceMultiplier = parsed, nameof(MarketSellPriceMultiplierValueText)); }
        public string PlagueDurationMultiplierValueText { get => PlagueDurationMultiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x"; set => SetDoubleValueText(value, parsed => PlagueDurationMultiplier = parsed, nameof(PlagueDurationMultiplierValueText)); }
        public string ApothecaryPlagueSearchDistanceValueText
        {
            get => string.Format(
                CultureInfo.CurrentCulture,
                SerpLocalization.Get("SomeSettings.TilesValueFormat"),
                ApothecaryPlagueSearchDistance);
            set => SetIntValueText(value, parsed => ApothecaryPlagueSearchDistance = parsed, nameof(ApothecaryPlagueSearchDistanceValueText));
        }
        public string CampfirePeasantsLimitText { get => CampfirePeasantsLimit.ToString(CultureInfo.InvariantCulture); set => SetIntValueText(value, parsed => CampfirePeasantsLimit = parsed, nameof(CampfirePeasantsLimitText)); }
        public string HumanGateReopenDelayValueText { get => FormatSeconds(HumanGateReopenDelaySeconds); set => SetDoubleValueText(value, parsed => HumanGateReopenDelaySeconds = parsed, nameof(HumanGateReopenDelayValueText)); }
        public string AIGateReopenDelayValueText { get => FormatSeconds(AIGateReopenDelaySeconds); set => SetDoubleValueText(value, parsed => AIGateReopenDelaySeconds = parsed, nameof(AIGateReopenDelayValueText)); }
        public string HumanGateClosingDistanceValueText { get => FormatTiles(HumanGateClosingDistanceTiles); set => SetDoubleValueText(value, parsed => HumanGateClosingDistanceTiles = parsed, nameof(HumanGateClosingDistanceValueText)); }
        public string AIGateClosingDistanceValueText { get => FormatTiles(AIGateClosingDistanceTiles); set => SetDoubleValueText(value, parsed => AIGateClosingDistanceTiles = parsed, nameof(AIGateClosingDistanceValueText)); }
        public string HumanLordHealthPercentText { get => FormatPercent(HumanLordHealthPercent); set => SetIntValueText(value, parsed => HumanLordHealthPercent = parsed, nameof(HumanLordHealthPercentText)); }
        public string AILordHealthPercentText { get => FormatPercent(AILordHealthPercent); set => SetIntValueText(value, parsed => AILordHealthPercent = parsed, nameof(AILordHealthPercentText)); }

        private void ResetToDefault()
        {
            if (CanEditHostSettings)
            {
                EnableMod = true;
                WoodRefundPercent = -1;
                StoneRefundPercent = -1;
                IronRefundPercent = -1;
                PitchRefundPercent = -1;
                GoldRefundPercent = -1;
                KeepStorageContent = true;
                MultiplyGoodsGainAI = 1;
                MultiplyGoodsGainHuman = 1;
                MultiplyGoodsGainInMoneyAI = 0;
                MultiplyGoodsGainInMoneyHuman = 0;
                MarketBuyPriceMultiplier = 1.0;
                MarketSellPriceMultiplier = 1.0;
                MarketPricesAlsoForAI = false;
                MarketGoodBuyPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
                MarketGoodSellPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
                PlagueDurationMultiplier = 2.0;
                ApothecaryPlagueSearchDistance = 50;
                CampfirePeasantsLimit = -1;
                HumanLordHealthPercent = LordHealthMultiplierPolicy.DefaultPercent;
                AILordHealthPercent = LordHealthMultiplierPolicy.DefaultPercent;
                EnableCtrlSingleMarketTrade = true;
                EnableSingleBuildingPause = true;
                EnableMultiplayerGameSpeedChanges = true;
                EnableShiftGameSpeedSteps = true;
                EnableFastRecruitRallyMovement = true;
                EnableMonksAlwaysRun = false;
                EnableKnightDismount = true;
                InstantHorse = false;
                EnableQuarryPileRelocation = true;
                EnableExtraChurchPriests = true;
                PreventAIPause = true;
                PreventEmergencyDemolition = true;
                PreventHovelDeletion = true;
                HumanGateReopenDelaySeconds = GatehouseTimingPatch.VanillaHumanDelaySeconds;
                AIGateReopenDelaySeconds = GatehouseTimingPatch.VanillaAiDelaySeconds;
                HumanGateClosingDistanceTiles = GatehouseTimingPatch.VanillaHumanDistanceTiles;
                AIGateClosingDistanceTiles = GatehouseTimingPatch.VanillaAiDistanceTiles;
                RequireReachableEnemyForAutomaticGateClosing = true;
            }

            EnableClientFeatures = true;
            EnableAllyGoodsAmountModifiers = true;
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            if (Equals(field, value))
                return;
            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetDoubleSetting(
            ref double field,
            double value,
            double minimum,
            double maximum,
            string propertyName,
            string textPropertyName)
        {
            if (!CanMutateSettingWithDependents(propertyName, textPropertyName))
                return;

            double clamped = ClampMultiplier(value, minimum, maximum);
            if (Math.Abs(field - clamped) < 0.0001)
                return;
            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetMarketGoodMultiplierArray(
            ref double[] field,
            double[] value,
            string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            double[] normalized = MarketGoodPriceDefinition.NormalizeMultipliers(value);
            if (AreMultiplierArraysEqual(field, normalized))
            {
                RefreshMarketGoodPriceItems();
                return;
            }

            field = normalized;
            RefreshMarketGoodPriceItems();
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void ChangeMarketGoodMultiplier(int goodId, bool buyPrice, double value)
        {
            int index = MarketGoodPriceDefinition.FindGoodIndex(goodId);
            if (index < 0)
                return;

            double[] updated = buyPrice
                ? (double[])marketGoodBuyPriceMultipliers.Clone()
                : (double[])marketGoodSellPriceMultipliers.Clone();
            updated[index] = MarketGoodPriceDefinition.NormalizeMultiplier(value);
            if (buyPrice)
                MarketGoodBuyPriceMultipliers = updated;
            else
                MarketGoodSellPriceMultipliers = updated;
        }

        internal double GetMarketGoodPriceMultiplier(eGoods good, bool buyPrice)
        {
            int index = MarketGoodPriceDefinition.FindGoodIndex((int)good);
            if (index < 0)
                return 1.0;
            return buyPrice
                ? marketGoodBuyPriceMultipliers[index]
                : marketGoodSellPriceMultipliers[index];
        }

        internal void InitializeMarketGoodPriceEditor(ManualLogSource log)
        {
            if (MarketGoodPriceItems.Count == 0)
            {
                for (int index = 0; index < MarketGoodPriceDefinition.Count; index++)
                {
                    int good = MarketGoodPriceDefinition.GetGood(index);
                    MarketGoodPriceItems.Add(new MarketGoodPriceItemViewModel(
                        good,
                        ((Enums.Goods)good).ToString(),
                        ChangeMarketGoodMultiplier));
                }
            }

            RefreshMarketGoodPriceItems();
            RefreshMarketGoodPriceVisuals();
        }

        internal void RefreshMarketGoodPriceVisuals()
        {
            if (marketGoodPriceVisualsResolved || !MainViewModel.viewModelLoaded)
                return;

            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel?.GameSprites == null)
                return;

            int resolvedIconCount = 0;
            int resolvedNameCount = 0;
            foreach (MarketGoodPriceItemViewModel item in MarketGoodPriceItems)
            {
                if (TryResolveMarketGoodName(item.GoodId, out string name))
                    resolvedNameCount++;
                if (TryResolveMarketGoodIcon(viewModel, item.GoodId, out ImageSource icon))
                    resolvedIconCount++;
                item.UpdateVisuals(name, icon);
            }

            marketGoodPriceVisualsResolved =
                resolvedIconCount == MarketGoodPriceDefinition.Count &&
                resolvedNameCount == MarketGoodPriceDefinition.Count;
        }

        private void RefreshMarketGoodPriceItems()
        {
            for (int index = 0;
                index < MarketGoodPriceItems.Count && index < MarketGoodPriceDefinition.Count;
                index++)
            {
                MarketGoodPriceItems[index].UpdateMultipliers(
                    marketGoodBuyPriceMultipliers[index],
                    marketGoodSellPriceMultipliers[index]);
            }
        }

        private static bool TryResolveMarketGoodName(int good, out string name)
        {
            try
            {
                name = Translate.Instance?.lookUpText(Enums.eTextSections.TEXT_GOODS, good);
                if (!string.IsNullOrWhiteSpace(name))
                    return true;
            }
            catch
            {
            }

            name = ((Enums.Goods)good).ToString();
            return false;
        }

        private static bool TryResolveMarketGoodIcon(
            MainViewModel viewModel,
            int good,
            out ImageSource icon)
        {
            icon = null;
            try
            {
                int spriteId = (int)viewModel.goodsSpriteEnumFromGoodsEnum((Enums.Goods)good);
                if (spriteId < 0 || spriteId >= viewModel.GameSprites.Count)
                    return false;

                icon = viewModel.GameSprites[spriteId];
                return (BaseComponent)(object)icon != (BaseComponent)null;
            }
            catch
            {
                icon = null;
                return false;
            }
        }

        private static bool AreMultiplierArraysEqual(double[] left, double[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (Math.Abs(left[index] - right[index]) >= 0.0001)
                    return false;
            }
            return true;
        }

        private void SetIntSetting(ref int field, int value, int minimum, int maximum, string propertyName, string textPropertyName = null)
        {
            if (!CanMutateSettingWithDependents(propertyName, textPropertyName))
                return;

            int clamped = Math.Max(minimum, Math.Min(maximum, value));
            if (field == clamped)
                return;
            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            if (!string.IsNullOrEmpty(textPropertyName))
                OnPropertyChanged(textPropertyName);
        }

        private void SetIntValueText(string text, Action<int> setValue, string textPropertyName)
        {
            if (Shared.NumericTextInput.TryParseInt(text, out int parsed))
                setValue(parsed);

            // Invalid and clamped input returns to the authoritative formatted value.
            OnPropertyChanged(textPropertyName);
        }

        private void SetDoubleValueText(string text, Action<double> setValue, string textPropertyName)
        {
            if (Shared.NumericTextInput.TryParseDouble(text, out double parsed))
                setValue(parsed);

            OnPropertyChanged(textPropertyName);
        }

        private static string FormatRefundPercent(int percent) =>
            percent < 0 ? "-1" : percent.ToString(CultureInfo.InvariantCulture) + "%";

        private static string FormatPercent(int percent) =>
            percent.ToString(CultureInfo.InvariantCulture) + "%";

        private static string FormatSeconds(double seconds) =>
            string.Format(
                CultureInfo.CurrentCulture,
                SerpLocalization.Get("SomeSettings.SecondsAtGamespeed40ValueFormat"),
                seconds);

        private static string FormatTiles(double tiles) =>
            string.Format(
                CultureInfo.CurrentCulture,
                SerpLocalization.Get("SomeSettings.DecimalTilesValueFormat"),
                tiles);

        private static double RoundToStep(double value, double step)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value;
            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }

        private static double ClampMultiplier(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;
            return Math.Max(minimum, Math.Min(maximum, Math.Round(value, 1, MidpointRounding.AwayFromZero)));
        }

        private static string FormatDecimalMultiplier(double value) =>
            ClampMultiplier(value, 0.0, 5.0).ToString("0.0", CultureInfo.InvariantCulture) + "x";

        private static ImageSource GetGoodIconImage(eGoods good)
        {
            try
            {
                return CrusaderDE.MainViewModel.Instance?.getSmallGoodsIcon((int)good);
            }
            catch
            {
                return null;
            }
        }
    }
}
