using PopularityTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class ReportTests
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    private static Dictionary<string, int> One(string key, int value) => new Dictionary<string, int> { [key] = value };
    private static PopularityReport Base() => new PopularityReport { Population=20, FoodAvailable=1, Gold=100, RationMode=2, TaxMode=3 };
    private static readonly string[] Rations = { "None", "Half", "Full", "Extra", "Double" };
    private static readonly string[] Taxes = { "HugeDonation", "BigDonation", "SmallDonation", "None", "Low", "Moderate", "High", "Mean", "Usurious", "Cruel", "MostCruel", "Cruellest" };
    private static readonly string[] Tiers = { "One", "Two", "Three", "Four", "Five" };
    private static PopularityReport Fixture(string name, int vanilla)
    {
        var r = Base();
        if (name.StartsWith("Rations"))
        {
            r.RationMode=Array.IndexOf(Rations, name.Substring("RationsPopModifier".Length));
            if (name.EndsWith("NoPopulation")) r.Population=0;
            if (name.EndsWith("NoPreferredFood")) r.FoodAvailable=0;
            r.Rations=r.Food=vanilla;
        }
        else if (name.StartsWith("FoodVariety")) { r.FoodTypes=name.EndsWith("TwoTypes") ? 2 : name.EndsWith("ThreeTypes") ? 3 : 4; r.Variety=r.Food=vanilla; }
        else if (name.StartsWith("Overpopulation")) r.Crowding=vanilla;
        else if (name.StartsWith("Tax"))
        {
            r.TaxMode=Array.IndexOf(Taxes, name.Substring("TaxPopModifier".Length));
            if (name.EndsWith("UnaffordableDonation")) { r.TaxMode=0; r.Gold=0; }
            r.Tax=vanilla;
        }
        else if (name.StartsWith("Religion")) { r.BlessedPercent=new[]{25,50,75,95}[Array.IndexOf(Tiers, name.Substring("ReligionPopModifierTier".Length))]; r.Religion=vanilla; }
        else if (name == "ChurchPopModifier" || name == "CathedralPopModifier") r.Buildings=vanilla;
        else if (name.StartsWith("Ale")) { r.AlePercent=new[]{25,50,75,100}[Array.IndexOf(Tiers, name.Substring("AlePopModifierTier".Length))]; r.Ale=vanilla; }
        else if (name.StartsWith("Fairs")) r.Fairs=r.Events=vanilla;
        else { r.FearLevel=1; r.Fear=vanilla; }
        // Independent unrelated event remainder must survive all corrections.
        r.Events-=125; r.Total=vanilla-125;
        return r;
    }
    private static int Main(string[] args)
    {
        var rows=Regex.Matches(File.ReadAllText(Path.Combine(args[0], "src/PopularityCatalog.cs")), "new PopularityParameter\\(\"(\\w+)\", \"\\w+\", (-?\\d+), (\\d+),");
        Check(rows.Count==41,"All parameters covered");
        foreach (Match row in rows)
        {
            string key=row.Groups[1].Value; int vanilla=int.Parse(row.Groups[2].Value)*25, limit=int.Parse(row.Groups[3].Value)*25;
            var input=Fixture(key,vanilla);
            foreach (int chosen in new[]{vanilla,-limit,limit,0,-vanilla,25,-25}.Distinct())
            {
                var output=input.Correct(One(key,chosen));
                Check(output.Total==input.Total+chosen-vanilla,key+" delta "+chosen);
                Check(input.Correct(One(key,chosen)).Total==output.Total,key+" repeat no accumulation");
                Check(input.Total==vanilla-125,key+" input immutable");
            }
            var disabled=input.Correct(new Dictionary<string,int>());
            foreach (var f in typeof(PopularityReport).GetFields(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                Check(Equals(f.GetValue(input),f.GetValue(disabled)),key+" disabled identity "+f.Name);
        }
        foreach (bool ale in new[]{false,true})
        foreach (int percent in new[]{0,24,25,49,50,74,75,94,95,99,100})
        {
            var r=Base(); r.BlessedPercent=r.AlePercent=percent;
            var v=new Dictionary<string,int>(); for(int i=0;i<4;i++) v[(ale?"Ale":"Religion")+"PopModifierTier"+Tiers[i]]=(i+1)*100;
            int expected=percent<25 ? 0 : percent<50 ? 100 : percent<75 ? 200 : percent<(ale?100:95) ? 300 : 400;
            Check(r.Correct(v).Total==expected,"coverage boundary "+ale+" "+percent);
        }
        var both=Base(); both.Buildings=75; both.Total=75;
        Check(both.Correct(new Dictionary<string,int>{{"ChurchPopModifier",325},{"CathedralPopModifier",350}}).Total==675,"Church 13 plus cathedral 14 without byte overflow");
        Check(both.Correct(new Dictionary<string,int>{{"ChurchPopModifier",-400},{"CathedralPopModifier",-400}}).Buildings==-800,"negative combined buildings");
        var food=Base(); food.FoodAvailable=0; food.RationMode=0; food.Rations=food.Food=food.Total=-200;
        Check(food.Correct(One("RationsPopModifierNone",400)).Total==-200,"no food precedes selected ration mode");
        food.Population=0;
        Check(food.Correct(One("RationsPopModifierNoPreferredFood",400)).Total==-200,"no population precedes no food");
        // The vanilla report uses zero here while the SE simulation uses +200. Active fallback must show its absolute value.
        food.Rations=food.Food=food.Total=0;
        Check(food.Correct(One("RationsPopModifierNoPopulation",200)).Total==200,"zero population report discrepancy explicitly corrected");
        var preview=Base(); preview.Rations=preview.Food=preview.Total=100; preview.RationMode=3;
        Check(preview.Correct(One("RationsPopModifierExtra",-50)).Total==-50,"ration preview");
        preview.TaxMode=0; preview.Gold=0;
        Check(preview.Correct(One("TaxPopModifierHugeDonation",1200)).Total==100,"unaffordable donation excludes selected tier");
        var fear=Base(); fear.FearLevel=-5; fear.Fear=fear.Total=-125;
        Check(fear.Correct(One("GoodBadThingPopModifierMultiplier",-50)).Total==250,"signed fear multiplication");
        var fair=Base(); fair.Fairs=400; fair.Events=fair.Total=0;
        Check(fair.Correct(One("FairsPopularityEventModifier",0)).Events==-400,"event cancellation preserves other events");
        Console.WriteLine("PASS: "+checks+" presentation assertions; all 41 sources, boundaries, signed values, preview, identity and no accumulation.");
        PresentationFixture.Run(Check);
        Console.WriteLine("PASS: production presentation callback fixture; total assertions="+checks);
        return 0;
    }
}
