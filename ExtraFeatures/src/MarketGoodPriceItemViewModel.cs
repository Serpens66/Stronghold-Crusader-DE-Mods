// Feature: Presents one tradeable good and its individual buy/sell price multipliers.
using Noesis;
using SHCDESE.ViewModels;
using System;
using System.Globalization;

namespace ExtraFeatures
{
    public sealed class MarketGoodPriceItemViewModel : LobbyModSettingsBaseViewModel
    {
        private readonly Action<int, bool, double> multiplierChanged;
        private string goodName;
        private ImageSource icon;
        private double buyMultiplier;
        private double sellMultiplier;

        internal MarketGoodPriceItemViewModel(
            int goodId,
            string initialGoodName,
            Action<int, bool, double> multiplierChanged)
        {
            GoodId = goodId;
            goodName = initialGoodName ?? string.Empty;
            this.multiplierChanged = multiplierChanged ?? throw new ArgumentNullException(nameof(multiplierChanged));
        }

        public int GoodId { get; }
        public ImageSource Icon => icon;
        public string GoodToolTip => goodName;

        public double BuyMultiplier
        {
            get => buyMultiplier;
            set => multiplierChanged(GoodId, true, value);
        }

        public double SellMultiplier
        {
            get => sellMultiplier;
            set => multiplierChanged(GoodId, false, value);
        }

        public string BuyMultiplierValueText =>
            CreateCompactValueText(SerpLocalization.Get(SerpLocalization.MarketBuyPriceMultiplier), buyMultiplier);

        public string SellMultiplierValueText =>
            CreateCompactValueText(SerpLocalization.Get(SerpLocalization.MarketSellPriceMultiplier), sellMultiplier);

        public string BuyToolTip => CreateMultiplierToolTip(
            SerpLocalization.Get(SerpLocalization.MarketBuyPriceMultiplier),
            BuyMultiplierValueText);

        public string SellToolTip => CreateMultiplierToolTip(
            SerpLocalization.Get(SerpLocalization.MarketSellPriceMultiplier),
            SellMultiplierValueText);

        internal void UpdateVisuals(string newGoodName, ImageSource newIcon)
        {
            goodName = newGoodName ?? string.Empty;
            icon = newIcon;
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(GoodToolTip));
            OnPropertyChanged(nameof(BuyToolTip));
            OnPropertyChanged(nameof(SellToolTip));
        }

        internal void UpdateMultipliers(double newBuyMultiplier, double newSellMultiplier)
        {
            buyMultiplier = newBuyMultiplier;
            sellMultiplier = newSellMultiplier;
            OnPropertyChanged(nameof(BuyMultiplier));
            OnPropertyChanged(nameof(SellMultiplier));
            OnPropertyChanged(nameof(BuyMultiplierValueText));
            OnPropertyChanged(nameof(SellMultiplierValueText));
            OnPropertyChanged(nameof(BuyToolTip));
            OnPropertyChanged(nameof(SellToolTip));
        }

        private string CreateMultiplierToolTip(string direction, string valueText) =>
            SerpLocalization.Get(
                SerpLocalization.MarketGoodPriceMultiplierHelp,
                "Good", goodName,
                "Direction", direction,
                "Value", valueText);

        private static string CreateCompactValueText(string label, double value)
        {
            string compactLabel = string.IsNullOrWhiteSpace(label)
                ? string.Empty
                : label.Trim().Substring(0, 1).ToUpper(CultureInfo.CurrentCulture) + " ";
            return compactLabel + value.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }
    }
}
