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
            => Apply(good, buying, multiplier, out _);

        internal bool Apply(eGoods good, bool buying, double multiplier, out MarketPriceApplication result)
        {
            result = default;
            var cells = buying ? buy : sell;
            if (Math.Abs(multiplier - 1.0) < 0.0001)
            {
                cells.Restore(good);
                return true;
            }
            PackedGoodPrice price = defaults(good);
            int baseValue = buying ? price.BuyPrice : price.SellPrice;
            int desired = (int)Math.Round(baseValue * multiplier, MidpointRounding.AwayFromZero);
            bool applied = cells.Apply(good, desired, out int current, out bool owned,
                value => value == baseValue);
            result = new MarketPriceApplication(baseValue, current, desired, owned);
            // An equal external/save-restored value already satisfies this setting. It is
            // deliberately not acquired: a later reset must not overwrite that value.
            return applied || current == desired;
        }

        internal void RestoreAll(Action<Exception> report, bool endSession = false)
        {
            buy.RestoreAll(report, endSession);
            sell.RestoreAll(report, endSession);
        }
    }

    internal readonly struct MarketPriceApplication
    {
        internal MarketPriceApplication(int baseline, int current, int desired, bool owned)
        { Baseline = baseline; Current = current; Desired = desired; Owned = owned; }
        internal int Baseline { get; }
        internal int Current { get; }
        internal int Desired { get; }
        internal bool Owned { get; }
    }
}
