using System;
using Shared;
using SHCDESE.Interop;

namespace ExtraFeatures
{
    internal sealed class MarketPriceOverrides
    {
        private readonly OwnedValueOverrides<eGoods, int> buy, sell;
        private readonly Func<eGoods, PackedGoodPrice> defaults;

        internal MarketPriceOverrides(Func<eGoods, PackedGoodPrice> read,
            Action<eGoods, PackedGoodPrice> write, Func<eGoods, PackedGoodPrice> defaults)
        {
            this.defaults = defaults;
            buy = new OwnedValueOverrides<eGoods, int>(good => read(good).BuyPrice,
                (good, value) => write(good, new PackedGoodPrice(value, read(good).SellPrice)));
            sell = new OwnedValueOverrides<eGoods, int>(good => read(good).SellPrice,
                (good, value) => write(good, new PackedGoodPrice(read(good).BuyPrice, value)));
        }

        // A foreign direct price is an absolute override, not a new multiplier base.
        internal bool Apply(eGoods good, bool buying, double multiplier)
        {
            var cells = buying ? buy : sell;
            if (Math.Abs(multiplier - 1.0) < 0.0001)
            {
                cells.Restore(good);
                return true;
            }
            PackedGoodPrice price = defaults(good);
            int baseValue = buying ? price.BuyPrice : price.SellPrice;
            int desired = (int)Math.Round(baseValue * multiplier, MidpointRounding.AwayFromZero);
            return cells.Apply(good, desired, current => current == baseValue);
        }

        internal void RestoreAll(Action<Exception> report, bool endSession = false)
        {
            buy.RestoreAll(report, endSession);
            sell.RestoreAll(report, endSession);
        }
    }
}
