namespace ExtraFeatures
{
    internal sealed class PopularityParameter
    {
        internal readonly string Name, Group;
        internal readonly int Vanilla, Limit;
        internal PopularityParameter(string name,string group,int vanilla,int limit)
        {Name=name;Group=group;Vanilla=vanilla;Limit=limit;}
    }
    internal static class PopularityCatalog
    {
        internal static readonly PopularityParameter[] All = {
            new PopularityParameter("RationsPopModifierNone","Rations",-8,16),
            new PopularityParameter("RationsPopModifierHalf","Rations",-4,16),
            new PopularityParameter("RationsPopModifierFull","Rations",0,16),
            new PopularityParameter("RationsPopModifierExtra","Rations",4,16),
            new PopularityParameter("RationsPopModifierDouble","Rations",8,16),
            new PopularityParameter("RationsPopModifierNoPreferredFood","Rations",-8,16),
            new PopularityParameter("RationsPopModifierNoPopulation","Rations",8,16),
            new PopularityParameter("FoodVarietyPopModifierTwoTypes","Variety",1,6),
            new PopularityParameter("FoodVarietyPopModifierThreeTypes","Variety",2,6),
            new PopularityParameter("FoodVarietyPopModifierFourTypes","Variety",3,6),
            new PopularityParameter("OverpopulationPopModifierTierOne","Crowding",-2,20),
            new PopularityParameter("OverpopulationPopModifierTierTwo","Crowding",-4,20),
            new PopularityParameter("OverpopulationPopModifierTierThree","Crowding",-6,20),
            new PopularityParameter("OverpopulationPopModifierTierFour","Crowding",-8,20),
            new PopularityParameter("OverpopulationPopModifierTierFive","Crowding",-10,20),
            new PopularityParameter("TaxPopModifierHugeDonation","Taxes",7,48),
            new PopularityParameter("TaxPopModifierBigDonation","Taxes",5,48),
            new PopularityParameter("TaxPopModifierSmallDonation","Taxes",3,48),
            new PopularityParameter("TaxPopModifierNone","Taxes",1,48),
            new PopularityParameter("TaxPopModifierLow","Taxes",-2,48),
            new PopularityParameter("TaxPopModifierModerate","Taxes",-4,48),
            new PopularityParameter("TaxPopModifierHigh","Taxes",-6,48),
            new PopularityParameter("TaxPopModifierMean","Taxes",-8,48),
            new PopularityParameter("TaxPopModifierUsurious","Taxes",-12,48),
            new PopularityParameter("TaxPopModifierCruel","Taxes",-16,48),
            new PopularityParameter("TaxPopModifierMostCruel","Taxes",-20,48),
            new PopularityParameter("TaxPopModifierCruellest","Taxes",-24,48),
            new PopularityParameter("TaxPopModifierUnaffordableDonation","Taxes",1,48),
            new PopularityParameter("TaxPopModifierUnknownMode","Taxes",-24,48),
            new PopularityParameter("ReligionPopModifierTierOne","Religion",2,16),
            new PopularityParameter("ReligionPopModifierTierTwo","Religion",4,16),
            new PopularityParameter("ReligionPopModifierTierThree","Religion",6,16),
            new PopularityParameter("ReligionPopModifierTierFour","Religion",8,16),
            new PopularityParameter("ChurchPopModifier","Religion",1,16),
            new PopularityParameter("CathedralPopModifier","Religion",2,16),
            new PopularityParameter("AlePopModifierTierOne","Ale",2,16),
            new PopularityParameter("AlePopModifierTierTwo","Ale",4,16),
            new PopularityParameter("AlePopModifierTierThree","Ale",6,16),
            new PopularityParameter("AlePopModifierTierFour","Ale",8,16),
            new PopularityParameter("FairsPopularityEventModifier","Fairs",16,32),
        };
    }
}
