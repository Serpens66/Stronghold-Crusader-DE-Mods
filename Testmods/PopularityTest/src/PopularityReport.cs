using System.Collections.Generic;
using SHCDESE.Interop.Enums;

namespace PopularityTest
{
    // A detached presentation snapshot. Never carries native pointers or writes PlayState.
    internal sealed class PopularityReport
    {
        internal int Population, FoodAvailable, FoodTypes, RationMode, TaxMode, Gold;
        internal int BlessedPercent, AlePercent, FearLevel;
        internal int Rations, Variety, Food, Tax, Crowding, Religion, Buildings, Ale, Fear, Fairs, Events, Total;

        internal PopularityReport Correct(IReadOnlyDictionary<string, int> active)
        {
            var result = (PopularityReport)MemberwiseClone();
            string ration = Population <= 0 ? "NoPopulation" : FoodAvailable <= 0 ? "NoPreferredFood" :
                RationName((RationsMode)RationMode);
            result.Rations = Value(active, "RationsPopModifier" + ration, Rations);
            string variety = FoodTypes == 2 ? "TwoTypes" : FoodTypes == 3 ? "ThreeTypes" : FoodTypes == 4 ? "FourTypes" : null;
            result.Variety = Value(active, variety == null ? null : "FoodVarietyPopModifier" + variety, Variety);
            result.Food = Food + result.Rations - Rations + result.Variety - Variety;
            string tax = TaxMode < (int)TaxesMode.None && Gold <= 0 ? "UnaffordableDonation" : TaxName((TaxesMode)TaxMode);
            result.Tax = Value(active, "TaxPopModifier" + tax, Tax);
            // Native report encodes the selected crowding tier, including the zero-population gate.
            string crowd = Crowding == -50 ? "One" : Crowding == -100 ? "Two" : Crowding == -150 ? "Three" :
                Crowding == -200 ? "Four" : Crowding == -250 ? "Five" : null;
            result.Crowding = Value(active, crowd == null ? null : "OverpopulationPopModifierTier" + crowd, Crowding);
            string religion = Tier(BlessedPercent, 95);
            result.Religion = Value(active, religion == null ? null : "ReligionPopModifierTier" + religion, Religion);
            // Vanilla's 25/50/75 is also its church/cathedral identity marker. Do not overwrite that sbyte.
            if (Buildings == 25 || Buildings == 75) result.Buildings += Value(active, "ChurchPopModifier", 25) - 25;
            if (Buildings == 50 || Buildings == 75) result.Buildings += Value(active, "CathedralPopModifier", 50) - 50;
            string ale = Tier(AlePercent, 100);
            result.Ale = Value(active, ale == null ? null : "AlePopModifierTier" + ale, Ale);
            if (active.TryGetValue("GoodBadThingPopModifierMultiplier", out int fear)) result.Fear = checked(fear * FearLevel);
            if (Fairs != 0) result.Fairs = Value(active, "FairsPopularityEventModifier", Fairs);
            result.Events = Events + result.Fairs - Fairs;
            result.Total = checked(Total + result.Food - Food + result.Tax - Tax + result.Crowding - Crowding +
                result.Religion - Religion + result.Buildings - Buildings + result.Ale - Ale + result.Fear - Fear + result.Fairs - Fairs);
            return result;
        }
        private static int Value(IReadOnlyDictionary<string, int> values, string key, int original)
            => key != null && values.TryGetValue(key, out int value) ? value : original;
        private static string Tier(int percent, int top) => percent < 25 ? null : percent < 50 ? "One" : percent < 75 ? "Two" : percent < top ? "Three" : "Four";
        private static string RationName(RationsMode mode)
        {
            switch (mode)
            {
                case RationsMode.None: return "None";
                case RationsMode.Half: return "Half";
                case RationsMode.Full: return "Full";
                case RationsMode.Extra: return "Extra";
                case RationsMode.Double: return "Double";
                default: return null;
            }
        }
        private static string TaxName(TaxesMode mode)
        {
            switch (mode)
            {
                case TaxesMode.HugeDonation: return "HugeDonation";
                case TaxesMode.BigDonation: return "BigDonation";
                case TaxesMode.SmallDonation: return "SmallDonation";
                case TaxesMode.None: return "None";
                case TaxesMode.Low: return "Low";
                case TaxesMode.Moderate: return "Moderate";
                case TaxesMode.High: return "High";
                case TaxesMode.Mean: return "Mean";
                case TaxesMode.Usurious: return "Usurious";
                case TaxesMode.Cruel: return "Cruel";
                case TaxesMode.MostCruel: return "MostCruel";
                case TaxesMode.Cruellest: return "Cruellest";
                default: return "UnknownMode";
            }
        }
    }
}
