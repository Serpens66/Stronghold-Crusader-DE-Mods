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
        private string woodRefundPercentText = "-1";
        private string stoneRefundPercentText = "-1";
        private string ironRefundPercentText = "-1";
        private string pitchRefundPercentText = "-1";
        private string goldRefundPercentText = "-1";
        private double multiplyGoodsGainAI = 1.0;
        private double multiplyGoodsGainHuman = 1.0;
        private double multiplyGoodsGainInMoneyAI;
        private double multiplyGoodsGainInMoneyHuman;
        private double marketBuyPriceMultiplier = 1.0;
        private double marketSellPriceMultiplier = 1.0;
        private double[] marketGoodBuyPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
        private double[] marketGoodSellPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
        private double plagueDurationMultiplier = 2.0;
        private int apothecaryPlagueSearchDistance = 50;
        private int campfirePeasantsLimit = -1;
        private bool keepStorageContent;
        private bool enableCtrlSingleMarketTrade = true;
        private bool enableSingleBuildingPause = true;
        private bool enableFastRecruitRallyMovement = true;
        private bool enableKnightDismount = true;
        private bool instantHorse;
        private bool enableQuarryPileRelocation = true;
        private bool enableExtraChurchPriests = true;
        private bool preventAIPause = true;
        private bool preventEmergencyDemolition = true;
        private bool preventHovelDeletion = true;
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
        public string EnableSingleBuildingPauseText => SerpLocalization.Get(SerpLocalization.EnableSingleBuildingPause);
        public string EnableSingleBuildingPauseHelpText => SerpLocalization.Get(SerpLocalization.EnableSingleBuildingPauseHelp);
        public string EnableFastRecruitRallyMovementText => SerpLocalization.Get(SerpLocalization.EnableFastRecruitRallyMovement);
        public string EnableFastRecruitRallyMovementHelpText => SerpLocalization.Get(SerpLocalization.EnableFastRecruitRallyMovementHelp);
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
        public string MarketGoodPriceMultipliersHelpText => SerpLocalization.Get(SerpLocalization.MarketGoodPriceMultipliersHelp);
        public string AiEconomyProtectionTitleText => SerpLocalization.Get(SerpLocalization.AiEconomyProtectionTitle);
        public string PreventAIPauseText => SerpLocalization.Get(SerpLocalization.PreventAIPause);
        public string PreventAIPauseHelpText => SerpLocalization.Get(SerpLocalization.PreventAIPauseHelp);
        public string PreventEmergencyDemolitionText => SerpLocalization.Get(SerpLocalization.PreventEmergencyDemolition);
        public string PreventEmergencyDemolitionHelpText => SerpLocalization.Get(SerpLocalization.PreventEmergencyDemolitionHelp);
        public string PreventHovelDeletionText => SerpLocalization.Get(SerpLocalization.PreventHovelDeletion);
        public string PreventHovelDeletionHelpText => SerpLocalization.Get(SerpLocalization.PreventHovelDeletionHelp);

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => SetSetting(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public string WoodRefundPercentText { get => woodRefundPercentText; set => SetTextSetting(ref woodRefundPercentText, value, nameof(WoodRefundPercentText)); }
        [SyncHostOnly] public string StoneRefundPercentText { get => stoneRefundPercentText; set => SetTextSetting(ref stoneRefundPercentText, value, nameof(StoneRefundPercentText)); }
        [SyncHostOnly] public string IronRefundPercentText { get => ironRefundPercentText; set => SetTextSetting(ref ironRefundPercentText, value, nameof(IronRefundPercentText)); }
        [SyncHostOnly] public string PitchRefundPercentText { get => pitchRefundPercentText; set => SetTextSetting(ref pitchRefundPercentText, value, nameof(PitchRefundPercentText)); }
        [SyncHostOnly] public string GoldRefundPercentText { get => goldRefundPercentText; set => SetTextSetting(ref goldRefundPercentText, value, nameof(GoldRefundPercentText)); }
        [SyncHostOnly] public bool KeepStorageContent { get => keepStorageContent; set => SetSetting(ref keepStorageContent, value, nameof(KeepStorageContent)); }

        public int WoodRefundPercent => ParsePercentOrUnchanged(WoodRefundPercentText);
        public int StoneRefundPercent => ParsePercentOrUnchanged(StoneRefundPercentText);
        public int IronRefundPercent => ParsePercentOrUnchanged(IronRefundPercentText);
        public int PitchRefundPercent => ParsePercentOrUnchanged(PitchRefundPercentText);
        public int GoldRefundPercent => ParsePercentOrUnchanged(GoldRefundPercentText);

        [SyncHostOnly] public double MultiplyGoodsGainAI { get => multiplyGoodsGainAI; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainAI, value, nameof(MultiplyGoodsGainAI), nameof(MultiplyGoodsGainAIText)); }
        [SyncHostOnly] public double MultiplyGoodsGainHuman { get => multiplyGoodsGainHuman; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainHuman, value, nameof(MultiplyGoodsGainHuman), nameof(MultiplyGoodsGainHumanText)); }
        [SyncHostOnly] public double MultiplyGoodsGainInMoneyAI { get => multiplyGoodsGainInMoneyAI; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainInMoneyAI, value, nameof(MultiplyGoodsGainInMoneyAI), nameof(MultiplyGoodsGainInMoneyAIText)); }
        [SyncHostOnly] public double MultiplyGoodsGainInMoneyHuman { get => multiplyGoodsGainInMoneyHuman; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainInMoneyHuman, value, nameof(MultiplyGoodsGainInMoneyHuman), nameof(MultiplyGoodsGainInMoneyHumanText)); }
        [SyncHostOnly] public double MarketBuyPriceMultiplier { get => marketBuyPriceMultiplier; set => SetDoubleSetting(ref marketBuyPriceMultiplier, value, 0.0, 5.0, nameof(MarketBuyPriceMultiplier), nameof(MarketBuyPriceMultiplierValueText)); }
        [SyncHostOnly] public double MarketSellPriceMultiplier { get => marketSellPriceMultiplier; set => SetDoubleSetting(ref marketSellPriceMultiplier, value, 0.0, 5.0, nameof(MarketSellPriceMultiplier), nameof(MarketSellPriceMultiplierValueText)); }
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
        [SyncHostOnly] public int CampfirePeasantsLimit { get => campfirePeasantsLimit; set => SetIntSetting(ref campfirePeasantsLimit, value, -1, 500, nameof(CampfirePeasantsLimit), nameof(CampfirePeasantsLimitText)); }
        [SyncHostOnly] public bool EnableCtrlSingleMarketTrade { get => enableCtrlSingleMarketTrade; set => SetSetting(ref enableCtrlSingleMarketTrade, value, nameof(EnableCtrlSingleMarketTrade)); }
        [SyncHostOnly] public bool EnableSingleBuildingPause { get => enableSingleBuildingPause; set => SetSetting(ref enableSingleBuildingPause, value, nameof(EnableSingleBuildingPause)); }
        [SyncHostOnly] public bool EnableFastRecruitRallyMovement { get => enableFastRecruitRallyMovement; set => SetSetting(ref enableFastRecruitRallyMovement, value, nameof(EnableFastRecruitRallyMovement)); }
        [SyncHostOnly] public bool EnableKnightDismount { get => enableKnightDismount; set => SetSetting(ref enableKnightDismount, value, nameof(EnableKnightDismount)); }
        [SyncHostOnly] public bool InstantHorse { get => instantHorse; set => SetSetting(ref instantHorse, value, nameof(InstantHorse)); }
        [SyncHostOnly] public bool EnableQuarryPileRelocation { get => enableQuarryPileRelocation; set => SetSetting(ref enableQuarryPileRelocation, value, nameof(EnableQuarryPileRelocation)); }
        [SyncHostOnly] public bool EnableExtraChurchPriests { get => enableExtraChurchPriests; set => SetSetting(ref enableExtraChurchPriests, value, nameof(EnableExtraChurchPriests)); }
        [SyncHostOnly] public bool PreventAIPause { get => preventAIPause; set => SetSetting(ref preventAIPause, value, nameof(PreventAIPause)); }
        [SyncHostOnly] public bool PreventEmergencyDemolition { get => preventEmergencyDemolition; set => SetSetting(ref preventEmergencyDemolition, value, nameof(PreventEmergencyDemolition)); }
        [SyncHostOnly] public bool PreventHovelDeletion { get => preventHovelDeletion; set => SetSetting(ref preventHovelDeletion, value, nameof(PreventHovelDeletion)); }

        public string MultiplyGoodsGainAIText { get => FormatDecimalMultiplier(MultiplyGoodsGainAI); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainAI = parsed, nameof(MultiplyGoodsGainAIText)); }
        public string MultiplyGoodsGainHumanText { get => FormatDecimalMultiplier(MultiplyGoodsGainHuman); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainHuman = parsed, nameof(MultiplyGoodsGainHumanText)); }
        public string MultiplyGoodsGainInMoneyAIText { get => FormatDecimalMultiplier(MultiplyGoodsGainInMoneyAI); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainInMoneyAI = parsed, nameof(MultiplyGoodsGainInMoneyAIText)); }
        public string MultiplyGoodsGainInMoneyHumanText { get => FormatDecimalMultiplier(MultiplyGoodsGainInMoneyHuman); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainInMoneyHuman = parsed, nameof(MultiplyGoodsGainInMoneyHumanText)); }
        public string MarketBuyPriceMultiplierValueText => MarketBuyPriceMultiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        public string MarketSellPriceMultiplierValueText => MarketSellPriceMultiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        public string PlagueDurationMultiplierValueText => PlagueDurationMultiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        public string ApothecaryPlagueSearchDistanceValueText => string.Format(
            CultureInfo.CurrentCulture,
            SerpLocalization.Get("SomeSettings.TilesValueFormat"),
            ApothecaryPlagueSearchDistance);
        public string CampfirePeasantsLimitText
        {
            get => CampfirePeasantsLimit.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    CampfirePeasantsLimit = parsed;
                else
                    OnPropertyChanged(nameof(CampfirePeasantsLimitText));
            }
        }

        private void ResetToDefault()
        {
            if (!CanEditHostSettings)
                return;

            EnableMod = true;
            WoodRefundPercentText = "-1";
            StoneRefundPercentText = "-1";
            IronRefundPercentText = "-1";
            PitchRefundPercentText = "-1";
            GoldRefundPercentText = "-1";
            KeepStorageContent = false;
            MultiplyGoodsGainAI = 1;
            MultiplyGoodsGainHuman = 1;
            MultiplyGoodsGainInMoneyAI = 0;
            MultiplyGoodsGainInMoneyHuman = 0;
            MarketBuyPriceMultiplier = 1.0;
            MarketSellPriceMultiplier = 1.0;
            MarketGoodBuyPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
            MarketGoodSellPriceMultipliers = MarketGoodPriceDefinition.CreateDefaultMultipliers();
            PlagueDurationMultiplier = 2.0;
            ApothecaryPlagueSearchDistance = 50;
            CampfirePeasantsLimit = -1;
            EnableCtrlSingleMarketTrade = true;
            EnableSingleBuildingPause = true;
            EnableFastRecruitRallyMovement = true;
            EnableKnightDismount = true;
            InstantHorse = false;
            EnableQuarryPileRelocation = true;
            EnableExtraChurchPriests = true;
            PreventAIPause = true;
            PreventEmergencyDemolition = true;
            PreventHovelDeletion = true;
        }

        private void SetTextSetting(ref string field, string value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            string normalized = NormalizePercentText(value);
            if (field == normalized)
                return;
            field = normalized;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
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

        private void SetDecimalMultiplierSetting(ref double field, double value, string propertyName, string textPropertyName)
        {
            if (!CanMutateSettingWithDependents(propertyName, textPropertyName))
                return;

            double normalized = NormalizeDecimalMultiplier(value);
            if (Math.Abs(field - normalized) < 0.0001)
                return;
            field = normalized;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetDecimalMultiplierText(string text, Action<double> setValue, string textPropertyName)
        {
            if (!TryParseDecimalMultiplier(text, out double parsed))
            {
                OnPropertyChanged(textPropertyName);
                return;
            }
            setValue(parsed);
        }

        private static string NormalizePercentText(string value) => ParsePercentOrUnchanged(value).ToString();

        private static int ParsePercentOrUnchanged(string value)
        {
            if (!int.TryParse(value, out int parsed) || parsed < -1)
                return -1;
            return Math.Min(100, parsed);
        }

        private static double ClampMultiplier(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;
            return Math.Max(minimum, Math.Min(maximum, Math.Round(value, 1, MidpointRounding.AwayFromZero)));
        }

        private static double NormalizeDecimalMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;
            return Math.Max(0.0, Math.Round(value, 2, MidpointRounding.AwayFromZero));
        }

        private static bool TryParseDecimalMultiplier(string text, out double value)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            bool parsed = double.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
            if (parsed)
                value = NormalizeDecimalMultiplier(value);
            return parsed;
        }

        private static string FormatDecimalMultiplier(double value) =>
            NormalizeDecimalMultiplier(value).ToString("0.00", CultureInfo.InvariantCulture);

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
