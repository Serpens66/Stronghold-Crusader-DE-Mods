using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Globalization;

namespace SomeSettings
{
    public sealed class SomeSettingsViewModel : LobbyModSettingsBaseViewModel
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
        private bool keepStorageContent;
        private bool rememberAiAivSettings = true;
        private readonly bool[] allowMinimapWhilePlacingBuildingData = new bool[9];

        public SomeSettingsViewModel()
        {
            SetAllowMinimapDefaults();
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public ImageSource WoodRefundIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource StoneRefundIcon => GetGoodIconImage(eGoods.STORED_STONE_BLOCKS);
        public ImageSource IronRefundIcon => GetGoodIconImage(eGoods.STORED_IRON_INGOTS);
        public ImageSource PitchRefundIcon => GetGoodIconImage(eGoods.STORED_PITCH_RAW);
        public ImageSource GoldRefundIcon => GetGoodIconImage(eGoods.STORED_GOLD);
        public ImageSource KeepStorageFruitIcon => GetGoodIconImage(eGoods.STORED_FOOD_FRUIT);
        public ImageSource KeepStorageWoodIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource KeepStorageBowsIcon => GetGoodIconImage(eGoods.STORED_BOWS);
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string AlwaysActiveTitleText => SerpLocalization.Get(SerpLocalization.AlwaysActiveTitle);
        public string AlwaysActiveHelpText => SerpLocalization.Get(SerpLocalization.AlwaysActiveHelp);
        public string MarketKeyMainTradeMenuHelpText => SerpLocalization.Get(SerpLocalization.MarketKeyMainTradeMenuHelp);
        public string AllowMinimapWhilePlacingBuildingText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuilding);
        public string AllowMinimapWhilePlacingBuildingHelpText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuildingHelp);
        public string RememberAiAivSettingsText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettings);
        public string RememberAiAivSettingsHelpText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettingsHelp);
        public string BulldozeTitleText => SerpLocalization.Get(SerpLocalization.BulldozeTitle);
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

        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuildingData;

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set => SetSetting(ref enableMod, value, nameof(EnableMod));
        }

        [SyncHostOnly]
        public string WoodRefundPercentText
        {
            get => woodRefundPercentText;
            set => SetTextSetting(ref woodRefundPercentText, value, nameof(WoodRefundPercentText));
        }

        [SyncHostOnly]
        public string StoneRefundPercentText
        {
            get => stoneRefundPercentText;
            set => SetTextSetting(ref stoneRefundPercentText, value, nameof(StoneRefundPercentText));
        }

        [SyncHostOnly]
        public string IronRefundPercentText
        {
            get => ironRefundPercentText;
            set => SetTextSetting(ref ironRefundPercentText, value, nameof(IronRefundPercentText));
        }

        [SyncHostOnly]
        public string PitchRefundPercentText
        {
            get => pitchRefundPercentText;
            set => SetTextSetting(ref pitchRefundPercentText, value, nameof(PitchRefundPercentText));
        }

        [SyncHostOnly]
        public string GoldRefundPercentText
        {
            get => goldRefundPercentText;
            set => SetTextSetting(ref goldRefundPercentText, value, nameof(GoldRefundPercentText));
        }

        [SyncHostOnly]
        public bool KeepStorageContent
        {
            get => keepStorageContent;
            set
            {
                if (keepStorageContent == value)
                    return;

                keepStorageContent = value;
                SettingChanged?.Invoke(nameof(KeepStorageContent));
                OnPropertyChanged(nameof(KeepStorageContent));
            }
        }

        [SyncPerPlayer]
        public bool AllowMinimapWhilePlacingBuilding
        {
            get => allowMinimapWhilePlacingBuildingData[LocalPlayerIdOrOne];
            set
            {
                int playerId = LocalPlayerIdOrOne;
                if (allowMinimapWhilePlacingBuildingData[playerId] == value)
                    return;

                allowMinimapWhilePlacingBuildingData[playerId] = value;
                SettingChanged?.Invoke(nameof(AllowMinimapWhilePlacingBuilding));
                OnPropertyChanged(nameof(AllowMinimapWhilePlacingBuilding));
            }
        }

        public int WoodRefundPercent => ParsePercentOrUnchanged(WoodRefundPercentText);
        public int StoneRefundPercent => ParsePercentOrUnchanged(StoneRefundPercentText);
        public int IronRefundPercent => ParsePercentOrUnchanged(IronRefundPercentText);
        public int PitchRefundPercent => ParsePercentOrUnchanged(PitchRefundPercentText);
        public int GoldRefundPercent => ParsePercentOrUnchanged(GoldRefundPercentText);

        [SyncHostOnly] public double MultiplyGoodsGainAI { get => multiplyGoodsGainAI; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainAI, value, nameof(MultiplyGoodsGainAI), nameof(MultiplyGoodsGainAIText)); }
        [SyncHostOnly] public double MultiplyGoodsGainHuman { get => multiplyGoodsGainHuman; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainHuman, value, nameof(MultiplyGoodsGainHuman), nameof(MultiplyGoodsGainHumanText)); }
        [SyncHostOnly] public double MultiplyGoodsGainInMoneyAI { get => multiplyGoodsGainInMoneyAI; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainInMoneyAI, value, nameof(MultiplyGoodsGainInMoneyAI), nameof(MultiplyGoodsGainInMoneyAIText)); }
        [SyncHostOnly] public double MultiplyGoodsGainInMoneyHuman { get => multiplyGoodsGainInMoneyHuman; set => SetDecimalMultiplierSetting(ref multiplyGoodsGainInMoneyHuman, value, nameof(MultiplyGoodsGainInMoneyHuman), nameof(MultiplyGoodsGainInMoneyHumanText)); }
        [SyncHostOnly] public double MarketBuyPriceMultiplier { get => marketBuyPriceMultiplier; set => SetDoubleSetting(ref marketBuyPriceMultiplier, value, nameof(MarketBuyPriceMultiplier), nameof(MarketBuyPriceMultiplierValueText)); }
        [SyncHostOnly] public double MarketSellPriceMultiplier { get => marketSellPriceMultiplier; set => SetDoubleSetting(ref marketSellPriceMultiplier, value, nameof(MarketSellPriceMultiplier), nameof(MarketSellPriceMultiplierValueText)); }
        [SyncHostOnly] public bool RememberAiAivSettings { get => rememberAiAivSettings; set => SetSetting(ref rememberAiAivSettings, value, nameof(RememberAiAivSettings)); }

        public string MultiplyGoodsGainAIText { get => FormatDecimalMultiplier(MultiplyGoodsGainAI); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainAI = parsed, nameof(MultiplyGoodsGainAIText)); }
        public string MultiplyGoodsGainHumanText { get => FormatDecimalMultiplier(MultiplyGoodsGainHuman); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainHuman = parsed, nameof(MultiplyGoodsGainHumanText)); }
        public string MultiplyGoodsGainInMoneyAIText { get => FormatDecimalMultiplier(MultiplyGoodsGainInMoneyAI); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainInMoneyAI = parsed, nameof(MultiplyGoodsGainInMoneyAIText)); }
        public string MultiplyGoodsGainInMoneyHumanText { get => FormatDecimalMultiplier(MultiplyGoodsGainInMoneyHuman); set => SetDecimalMultiplierText(value, parsed => MultiplyGoodsGainInMoneyHuman = parsed, nameof(MultiplyGoodsGainInMoneyHumanText)); }
        public string MarketBuyPriceMultiplierValueText => MarketBuyPriceMultiplier.ToString("0.0");
        public string MarketSellPriceMultiplierValueText => MarketSellPriceMultiplier.ToString("0.0");

        private void ResetToDefault()
        {
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
            RememberAiAivSettings = true;
            SetAllowMinimapDefaults();
            SettingChanged?.Invoke(nameof(AllowMinimapWhilePlacingBuilding));
            OnPropertyChanged(nameof(AllowMinimapWhilePlacingBuilding));
        }

        private void SetTextSetting(ref string field, string value, string propertyName)
        {
            string normalized = NormalizePercentText(value);
            if (field == normalized)
                return;

            field = normalized;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetIntSetting(ref int field, int value, string propertyName, string textPropertyName)
        {
            if (field == value)
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetDoubleSetting(ref double field, double value, string propertyName, string textPropertyName)
        {
            double clamped = ClampMultiplier(value);
            if (Math.Abs(field - clamped) < 0.0001)
                return;

            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetDecimalMultiplierSetting(ref double field, double value, string propertyName, string textPropertyName)
        {
            double normalized = NormalizeDecimalMultiplier(value);
            if (Math.Abs(field - normalized) < 0.0001)
                return;

            field = normalized;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetIntText(string text, Action<int> setValue, string textPropertyName)
        {
            if (!int.TryParse(text, out int parsed))
            {
                OnPropertyChanged(textPropertyName);
                return;
            }

            setValue(parsed);
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

        private static string NormalizePercentText(string value)
        {
            int parsed = ParsePercentOrUnchanged(value);
            return parsed.ToString();
        }

        private static int ParsePercentOrUnchanged(string value)
        {
            if (!int.TryParse(value, out int parsed))
                return -1;

            if (parsed < -1)
                return -1;

            if (parsed > 100)
                return 100;

            return parsed;
        }

        private static double ClampMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;

            double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
            if (rounded < 0.0)
                return 0.0;

            if (rounded > 5.0)
                return 5.0;

            return rounded;
        }

        private static double NormalizeDecimalMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return Math.Max(0.0, rounded);
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

        private static string FormatDecimalMultiplier(double value)
        {
            return NormalizeDecimalMultiplier(value).ToString("0.00", CultureInfo.InvariantCulture);
        }

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

        private static int LocalPlayerIdOrOne => Math.Max(1, GameNetworkAPI.GetLocalPlayerId());

        private void SetAllowMinimapDefaults()
        {
            for (int i = 1; i < allowMinimapWhilePlacingBuildingData.Length; i++)
                allowMinimapWhilePlacingBuildingData[i] = true;
        }
    }
}
