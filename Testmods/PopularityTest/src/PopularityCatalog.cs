using RedBird.Core.Memory.Managed;
using SHCDESE.GameGlobals;
using System;

namespace PopularityTest
{
    internal sealed class PopularityParameter
    {
        internal readonly string Name, Group;
        internal readonly int Vanilla, Limit;
        internal readonly Func<GameGlobalsManager, ManagedValue<int>> Resolve;
        internal readonly Func<PopularitySettings, bool> Enabled;
        internal readonly Func<PopularitySettings, int> Read;
        internal PopularityParameter(string name, string group, int vanilla, int limit,
            Func<GameGlobalsManager, ManagedValue<int>> resolve,
            Func<PopularitySettings, bool> enabled, Func<PopularitySettings, int> read)
        { Name = name; Group = group; Vanilla = vanilla; Limit = limit; Resolve = resolve; Enabled = enabled; Read = read; }
    }
    internal static class PopularityCatalog
    {
        // Component reports divide by 25; total popularity instead divides by 100.
        internal const int UnitsPerPoint = 25;
        internal static readonly PopularityParameter[] All = {
            new PopularityParameter("RationsPopModifierNone", "Rations", -8, 16, g => g.RationsPopModifierNone, s => s.RationsPopModifierNoneEnabled, s => s.RationsPopModifierNone),
            new PopularityParameter("RationsPopModifierHalf", "Rations", -4, 16, g => g.RationsPopModifierHalf, s => s.RationsPopModifierHalfEnabled, s => s.RationsPopModifierHalf),
            new PopularityParameter("RationsPopModifierFull", "Rations", 0, 16, g => g.RationsPopModifierFull, s => s.RationsPopModifierFullEnabled, s => s.RationsPopModifierFull),
            new PopularityParameter("RationsPopModifierExtra", "Rations", 4, 16, g => g.RationsPopModifierExtra, s => s.RationsPopModifierExtraEnabled, s => s.RationsPopModifierExtra),
            new PopularityParameter("RationsPopModifierDouble", "Rations", 8, 16, g => g.RationsPopModifierDouble, s => s.RationsPopModifierDoubleEnabled, s => s.RationsPopModifierDouble),
            new PopularityParameter("RationsPopModifierNoPreferredFood", "Rations", -8, 16, g => g.RationsPopModifierNoPreferredFood, s => s.RationsPopModifierNoPreferredFoodEnabled, s => s.RationsPopModifierNoPreferredFood),
            new PopularityParameter("RationsPopModifierNoPopulation", "Rations", 8, 16, g => g.RationsPopModifierNoPopulation, s => s.RationsPopModifierNoPopulationEnabled, s => s.RationsPopModifierNoPopulation),
            new PopularityParameter("FoodVarietyPopModifierTwoTypes", "Variety", 1, 6, g => g.FoodVarietyPopModifierTwoTypes, s => s.FoodVarietyPopModifierTwoTypesEnabled, s => s.FoodVarietyPopModifierTwoTypes),
            new PopularityParameter("FoodVarietyPopModifierThreeTypes", "Variety", 2, 6, g => g.FoodVarietyPopModifierThreeTypes, s => s.FoodVarietyPopModifierThreeTypesEnabled, s => s.FoodVarietyPopModifierThreeTypes),
            new PopularityParameter("FoodVarietyPopModifierFourTypes", "Variety", 3, 6, g => g.FoodVarietyPopModifierFourTypes, s => s.FoodVarietyPopModifierFourTypesEnabled, s => s.FoodVarietyPopModifierFourTypes),
            new PopularityParameter("OverpopulationPopModifierTierOne", "Crowding", -2, 20, g => g.OverpopulationPopModifierTierOne, s => s.OverpopulationPopModifierTierOneEnabled, s => s.OverpopulationPopModifierTierOne),
            new PopularityParameter("OverpopulationPopModifierTierTwo", "Crowding", -4, 20, g => g.OverpopulationPopModifierTierTwo, s => s.OverpopulationPopModifierTierTwoEnabled, s => s.OverpopulationPopModifierTierTwo),
            new PopularityParameter("OverpopulationPopModifierTierThree", "Crowding", -6, 20, g => g.OverpopulationPopModifierTierThree, s => s.OverpopulationPopModifierTierThreeEnabled, s => s.OverpopulationPopModifierTierThree),
            new PopularityParameter("OverpopulationPopModifierTierFour", "Crowding", -8, 20, g => g.OverpopulationPopModifierTierFour, s => s.OverpopulationPopModifierTierFourEnabled, s => s.OverpopulationPopModifierTierFour),
            new PopularityParameter("OverpopulationPopModifierTierFive", "Crowding", -10, 20, g => g.OverpopulationPopModifierTierFive, s => s.OverpopulationPopModifierTierFiveEnabled, s => s.OverpopulationPopModifierTierFive),
            new PopularityParameter("TaxPopModifierHugeDonation", "Taxes", 7, 48, g => g.TaxPopModifierHugeDonation, s => s.TaxPopModifierHugeDonationEnabled, s => s.TaxPopModifierHugeDonation),
            new PopularityParameter("TaxPopModifierBigDonation", "Taxes", 5, 48, g => g.TaxPopModifierBigDonation, s => s.TaxPopModifierBigDonationEnabled, s => s.TaxPopModifierBigDonation),
            new PopularityParameter("TaxPopModifierSmallDonation", "Taxes", 3, 48, g => g.TaxPopModifierSmallDonation, s => s.TaxPopModifierSmallDonationEnabled, s => s.TaxPopModifierSmallDonation),
            new PopularityParameter("TaxPopModifierNone", "Taxes", 1, 48, g => g.TaxPopModifierNone, s => s.TaxPopModifierNoneEnabled, s => s.TaxPopModifierNone),
            new PopularityParameter("TaxPopModifierLow", "Taxes", -2, 48, g => g.TaxPopModifierLow, s => s.TaxPopModifierLowEnabled, s => s.TaxPopModifierLow),
            new PopularityParameter("TaxPopModifierModerate", "Taxes", -4, 48, g => g.TaxPopModifierModerate, s => s.TaxPopModifierModerateEnabled, s => s.TaxPopModifierModerate),
            new PopularityParameter("TaxPopModifierHigh", "Taxes", -6, 48, g => g.TaxPopModifierHigh, s => s.TaxPopModifierHighEnabled, s => s.TaxPopModifierHigh),
            new PopularityParameter("TaxPopModifierMean", "Taxes", -8, 48, g => g.TaxPopModifierMean, s => s.TaxPopModifierMeanEnabled, s => s.TaxPopModifierMean),
            new PopularityParameter("TaxPopModifierUsurious", "Taxes", -12, 48, g => g.TaxPopModifierUsurious, s => s.TaxPopModifierUsuriousEnabled, s => s.TaxPopModifierUsurious),
            new PopularityParameter("TaxPopModifierCruel", "Taxes", -16, 48, g => g.TaxPopModifierCruel, s => s.TaxPopModifierCruelEnabled, s => s.TaxPopModifierCruel),
            new PopularityParameter("TaxPopModifierMostCruel", "Taxes", -20, 48, g => g.TaxPopModifierMostCruel, s => s.TaxPopModifierMostCruelEnabled, s => s.TaxPopModifierMostCruel),
            new PopularityParameter("TaxPopModifierCruellest", "Taxes", -24, 48, g => g.TaxPopModifierCruellest, s => s.TaxPopModifierCruellestEnabled, s => s.TaxPopModifierCruellest),
            new PopularityParameter("TaxPopModifierUnaffordableDonation", "Taxes", 1, 48, g => g.TaxPopModifierUnaffordableDonation, s => s.TaxPopModifierUnaffordableDonationEnabled, s => s.TaxPopModifierUnaffordableDonation),
            new PopularityParameter("TaxPopModifierUnknownMode", "Taxes", -24, 48, g => g.TaxPopModifierUnknownMode, s => s.TaxPopModifierUnknownModeEnabled, s => s.TaxPopModifierUnknownMode),
            new PopularityParameter("ReligionPopModifierTierOne", "Religion", 2, 16, g => g.ReligionPopModifierTierOne, s => s.ReligionPopModifierTierOneEnabled, s => s.ReligionPopModifierTierOne),
            new PopularityParameter("ReligionPopModifierTierTwo", "Religion", 4, 16, g => g.ReligionPopModifierTierTwo, s => s.ReligionPopModifierTierTwoEnabled, s => s.ReligionPopModifierTierTwo),
            new PopularityParameter("ReligionPopModifierTierThree", "Religion", 6, 16, g => g.ReligionPopModifierTierThree, s => s.ReligionPopModifierTierThreeEnabled, s => s.ReligionPopModifierTierThree),
            new PopularityParameter("ReligionPopModifierTierFour", "Religion", 8, 16, g => g.ReligionPopModifierTierFour, s => s.ReligionPopModifierTierFourEnabled, s => s.ReligionPopModifierTierFour),
            new PopularityParameter("ChurchPopModifier", "Religion", 1, 16, g => g.ChurchPopModifier, s => s.ChurchPopModifierEnabled, s => s.ChurchPopModifier),
            new PopularityParameter("CathedralPopModifier", "Religion", 2, 16, g => g.CathedralPopModifier, s => s.CathedralPopModifierEnabled, s => s.CathedralPopModifier),
            new PopularityParameter("AlePopModifierTierOne", "Ale", 2, 16, g => g.AlePopModifierTierOne, s => s.AlePopModifierTierOneEnabled, s => s.AlePopModifierTierOne),
            new PopularityParameter("AlePopModifierTierTwo", "Ale", 4, 16, g => g.AlePopModifierTierTwo, s => s.AlePopModifierTierTwoEnabled, s => s.AlePopModifierTierTwo),
            new PopularityParameter("AlePopModifierTierThree", "Ale", 6, 16, g => g.AlePopModifierTierThree, s => s.AlePopModifierTierThreeEnabled, s => s.AlePopModifierTierThree),
            new PopularityParameter("AlePopModifierTierFour", "Ale", 8, 16, g => g.AlePopModifierTierFour, s => s.AlePopModifierTierFourEnabled, s => s.AlePopModifierTierFour),
            new PopularityParameter("FairsPopularityEventModifier", "Fairs", 16, 32, g => g.FairsPopularityEventModifier, s => s.FairsPopularityEventModifierEnabled, s => s.FairsPopularityEventModifier),
        };
    }
}
