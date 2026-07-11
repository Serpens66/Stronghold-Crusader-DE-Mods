using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public static class SerpLocalization
{
    public const string ResetToDefault = "Common.ResetToDefault";
    public const string EnableMod = "Common.EnableMod";
    public const string Ai = "Common.Ai";
    public const string Human = "Common.Human";
    public const string Limit = "Common.Limit";
    public const string Max = "Common.Max";
    public const string UnitLimitsTitle = "UnitLimit.Title";
    public const string UnitLimitsHelp = "UnitLimit.Help";
    public const string UnitLimitsCampfirePeasants = "UnitLimit.CampfirePeasants";
    public const string UnitLimitsCampfirePeasantsHelp = "UnitLimit.CampfirePeasantsHelp";
    public const string BuildingLimitsTitle = "BuildingLimit.Title";
    public const string BuildingLimitsHelp = "BuildingLimit.Help";
    public const string UnitCostsTitle = "UnitCosts.Title";
    public const string UnitCostsHelp = "UnitCosts.Help";
    public const string UnitCostsExtraTitle = "UnitCosts.ExtraTitle";
    public const string UnitCostsExtraHelp = "UnitCosts.ExtraHelp";
    public const string UnitHeader = "UnitCosts.UnitHeader";
    public const string Slot1Header = "UnitCosts.Slot1Header";
    public const string Slot2Header = "UnitCosts.Slot2Header";
    public const string Slot3Header = "UnitCosts.Slot3Header";
    public const string Slot4HorseHeader = "UnitCosts.Slot4HorseHeader";
    public const string None = "UnitCosts.None";
    public const string Horse = "UnitCosts.Horse";
    public const string ResourcesMissing = "UnitCosts.ResourcesMissing";
    public const string BuildingCostsTitle = "BuildingCosts.Title";
    public const string BuildingCostsHelp = "BuildingCosts.Help";
    public const string BuildingHeader = "BuildingCosts.BuildingHeader";
    public const string Vanilla = "BuildingCosts.Vanilla";
    public const string VanillaNoCosts = "BuildingCosts.VanillaNoCosts";
    public const string StartGoldTitle = "StartConditions.StartGoldTitle";
    public const string StartGoodsTitle = "StartConditions.StartGoodsTitle";
    public const string StartTroopsTitle = "StartConditions.StartTroopsTitle";
    public const string UnchangedRangeHelp = "StartConditions.UnchangedRangeHelp";
    public const string NormalCrusade = "StartConditions.NormalCrusade";
    public const string Deathmatch = "StartConditions.Deathmatch";
    public const string SetStartGold = "StartConditions.SetStartGold";
    public const string SetStartGoldHelp = "StartConditions.SetStartGoldHelp";
    public const string AddStartGold = "StartConditions.AddStartGold";
    public const string AddStartGoldHelp = "StartConditions.AddStartGoldHelp";
    public const string MultiplyStartTroops = "StartConditions.MultiplyStartTroops";
    public const string StartTroopsMultiplierHelp = "StartConditions.StartTroopsMultiplierHelp";
    public const string StartTroopsMultiplierToolTip = "StartConditions.StartTroopsMultiplierToolTip";
    public const string ExtraStartUnitsHelp = "StartConditions.ExtraStartUnitsHelp";
    public const string AlwaysActiveTitle = "SomeSettings.AlwaysActiveTitle";
    public const string AlwaysActiveHelp = "SomeSettings.AlwaysActiveHelp";
    public const string MarketKeyMainTradeMenuHelp = "SomeSettings.MarketKeyMainTradeMenuHelp";
    public const string AllowMinimapWhilePlacingBuilding = "SomeSettings.AllowMinimapWhilePlacingBuilding";
    public const string AllowMinimapWhilePlacingBuildingHelp = "SomeSettings.AllowMinimapWhilePlacingBuildingHelp";
    public const string BulldozeTitle = "SomeSettings.BulldozeTitle";
    public const string BulldozeHelp = "SomeSettings.BulldozeHelp";
    public const string WoodRefund = "SomeSettings.WoodRefund";
    public const string StoneRefund = "SomeSettings.StoneRefund";
    public const string IronRefund = "SomeSettings.IronRefund";
    public const string PitchRefund = "SomeSettings.PitchRefund";
    public const string GoldRefund = "SomeSettings.GoldRefund";
    public const string VanillaValue50 = "SomeSettings.VanillaValue50";
    public const string KeepStorageContent = "SomeSettings.KeepStorageContent";
    public const string KeepStorageContentHelp = "SomeSettings.KeepStorageContentHelp";
    public const string EconomyBuffsTitle = "SomeSettings.EconomyBuffsTitle";
    public const string MultiplyGoodsGain = "SomeSettings.MultiplyGoodsGain";
    public const string MultiplyGoodsGainHelp = "SomeSettings.MultiplyGoodsGainHelp";
    public const string MultiplyGoodsAsMoney = "SomeSettings.MultiplyGoodsAsMoney";
    public const string MultiplyGoodsAsMoneyHelp = "SomeSettings.MultiplyGoodsAsMoneyHelp";
    public const string MarketPriceMultipliersTitle = "SomeSettings.MarketPriceMultipliersTitle";
    public const string MarketBuyPriceMultiplier = "SomeSettings.MarketBuyPriceMultiplier";
    public const string MarketBuyPriceMultiplierHelp = "SomeSettings.MarketBuyPriceMultiplierHelp";
    public const string MarketSellPriceMultiplier = "SomeSettings.MarketSellPriceMultiplier";
    public const string MarketSellPriceMultiplierHelp = "SomeSettings.MarketSellPriceMultiplierHelp";
    public const string RememberAiAivSettings = "SomeSettings.RememberAiAivSettings";
    public const string RememberAiAivSettingsHelp = "SomeSettings.RememberAiAivSettingsHelp";
    public const string EnableKnightDismount = "SomeSettings.EnableKnightDismount";
    public const string EnableKnightDismountHelp = "SomeSettings.EnableKnightDismountHelp";
    public const string KnightDismountTooltip = "SomeSettings.KnightDismountTooltip";
    public const string KnightDismountTooltipBody = "SomeSettings.KnightDismountTooltipBody";
    public const string KnightMountTooltip = "SomeSettings.KnightMountTooltip";
    public const string KnightMountTooltipBody = "SomeSettings.KnightMountTooltipBody";
    public const string AiEconomyProtectionTitle = "SomeSettings.AIEconomyProtectionTitle";
    public const string PreventAIPause = "SomeSettings.PreventAIPause";
    public const string PreventAIPauseHelp = "SomeSettings.PreventAIPauseHelp";
    public const string PreventEmergencyDemolition = "SomeSettings.PreventEmergencyDemolition";
    public const string PreventEmergencyDemolitionHelp = "SomeSettings.PreventEmergencyDemolitionHelp";
    public const string PreventHovelDeletion = "SomeSettings.PreventHovelDeletion";
    public const string PreventHovelDeletionHelp = "SomeSettings.PreventHovelDeletionHelp";

    private const string DefaultLocale = "en-US";
    private static Dictionary<string, string> loadedTexts;
    private static string loadedLocale;

    private static readonly Dictionary<string, string> EnglishFallbacks = new Dictionary<string, string>
    {
        { ResetToDefault, "Reset to Default" },
        { EnableMod, "Enable Mod" },
        { Ai, "AI" },
        { Human, "Human" },
        { Limit, "Limit" },
        { Max, "Max" },
        { UnitLimitsTitle, "UNIT LIMITS (Human)" },
        { UnitLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Existing living units count against the limit." },
        { UnitLimitsCampfirePeasants, "Campfire Peasants" },
        { UnitLimitsCampfirePeasantsHelp, "-1 = unchanged. Allowed range: -1 to 500. Sets the maximum peasants waiting at the campfire." },
        { BuildingLimitsTitle, "BUILDING LIMITS (Human)" },
        { BuildingLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together." },
        { UnitCostsTitle, "UNIT COSTS (Human and AI)" },
        { UnitCostsHelp, "Good slots apply to European units. unchanged keeps the vanilla slot; gold -1 stays unchanged." },
        { UnitCostsExtraTitle, "EXTRA COSTS (only Human)" },
        { UnitCostsExtraHelp, "0 = no extra cost. Positive values are charged in addition; negative gold refunds up to the current gold cost. AI players ignore this table." },
        { UnitHeader, "Unit" },
        { Slot1Header, "Slot 1" },
        { Slot2Header, "Slot 2" },
        { Slot3Header, "Slot 3" },
        { Slot4HorseHeader, "Slot 4 / Horse" },
        { None, "none" },
        { Horse, "Horse" },
        { ResourcesMissing, "Resources missing" },
        { BuildingCostsTitle, "BUILDING COSTS" },
        { BuildingCostsHelp, "-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI)." },
        { BuildingHeader, "Building" },
        { Vanilla, "Vanilla" },
        { VanillaNoCosts, "no costs" },
        { StartGoldTitle, "START GOLD" },
        { StartGoodsTitle, "START GOODS" },
        { StartTroopsTitle, "START TROOPS" },
        { UnchangedRangeHelp, "-1 = unchanged. Allowed range: -1 to 100000." },
        { NormalCrusade, "Normal/Crusade" },
        { Deathmatch, "Deathmatch" },
        { SetStartGold, "Set start gold (-1 = unchanged)" },
        { SetStartGoldHelp, "Sets the initial amount of gold for each player. -1 means unchanged." },
        { AddStartGold, "Add start gold" },
        { AddStartGoldHelp, "Adds the specified amount of gold to the initial amount for each player." },
        { MultiplyStartTroops, "Multiply Start Troop armies" },
        { StartTroopsMultiplierHelp, "Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100." },
        { StartTroopsMultiplierToolTip, "Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100." },
        { ExtraStartUnitsHelp, "Extra Start Units: -1 or 0 = no extra units. Allowed range: -1 to 100000." },
        { AlwaysActiveTitle, "Always active (if mod enabled)" },
        { AlwaysActiveHelp, "Minimap dragging follows the mouse cursor directly. Pressing the market keybind while the market is selected returns the menu to the main trade menu. The bulldoze icon changes when the cursor is near enemies." },
        { MarketKeyMainTradeMenuHelp, "Pressing the market keybind while the market is already selected returns the menu to the main trade menu." },
        { AllowMinimapWhilePlacingBuilding, "Allow minimap while placing buildings" },
        { AllowMinimapWhilePlacingBuildingHelp, "When enabled, left-clicking the minimap moves the camera even while a building is selected for placement. This is a per-player setting." },
        { BulldozeTitle, "BULLDOZE" },
        { BulldozeHelp, "-1 = unchanged. Refund values are percentages from 0 to 100." },
        { WoodRefund, "Wood refund %" },
        { StoneRefund, "Stone refund %" },
        { IronRefund, "Iron refund %" },
        { PitchRefund, "Pitch refund %" },
        { GoldRefund, "Gold refund %" },
        { VanillaValue50, "Vanilla value: 50%." },
        { KeepStorageContent, "Keep Storage Content" },
        { KeepStorageContentHelp, "When enabled, bulldozing a granary, armory, or stockpile keeps the goods stored inside by adding them back as incoming goods.\nIf Granary was built for free can't credit goods back (happens when building it while no wood is on stock)" },
        { EconomyBuffsTitle, "Economy Buffs" },
        { MultiplyGoodsGain, "Multiply goods gain" },
        { MultiplyGoodsGainHelp, "Multiplier for gained goods. 1 = unchanged, 2 = double, 3 = triple. Values 1 or lower add nothing." },
        { MultiplyGoodsAsMoney, "Multiply goods as money" },
        { MultiplyGoodsAsMoneyHelp, "Extra gold payouts based on sell value of gained goods. 0 = unchanged, 1 = one sell-value payout, 2 = two payouts." },
        { MarketPriceMultipliersTitle, "Market Price Multipliers" },
        { MarketBuyPriceMultiplier, "Buy prices" },
        { MarketBuyPriceMultiplierHelp, "Multiplier for all market buy prices. 1.0 = unchanged, 0.0 = free, 5.0 = five times the vanilla price." },
        { MarketSellPriceMultiplier, "Sell prices" },
        { MarketSellPriceMultiplierHelp, "Multiplier for all market sell prices. 1.0 = unchanged, 0.0 = no gold from selling, 5.0 = five times the vanilla price." },
        { RememberAiAivSettings, "Remember AI castle/settings selection" },
        { RememberAiAivSettingsHelp, "When enabled, the last AIV, rotation, and custom lord settings selected for each AI lord are applied automatically when that AI is added to a skirmish lobby." },
        { EnableKnightDismount, "Enable knight mount/dismount buttons" },
        { EnableKnightDismountHelp, "Adds command buttons to mount swordsmen and dismount mounted knights." },
        { KnightDismountTooltip, "Dismount" },
        { KnightDismountTooltipBody, "Turns selected mounted knights into swordsmen at the same position." },
        { KnightMountTooltip, "Mount" },
        { KnightMountTooltipBody, "Turns selected swordsmen into mounted knights. Requires available horses in a stable." },
        { AiEconomyProtectionTitle, "AI Economy Protection" },
        { PreventAIPause, "Prevent AI building pauses" },
        { PreventAIPauseHelp, "Prevents AI-controlled players from putting their own production buildings to sleep." },
        { PreventEmergencyDemolition, "Prevent AI panic demolition" },
        { PreventEmergencyDemolitionHelp, "Skips the AI emergency resource-recovery demolition block, which can otherwise remove useful buildings under pressure." },
        { PreventHovelDeletion, "Prevent AI hovel deletion" },
        { PreventHovelDeletionHelp, "Blocks direct deletes of living AI-owned hovels while still allowing normal destruction by damage." }
    };

    public static string Get(string key)
    {
        EnsureLoaded();

        if (loadedTexts.TryGetValue(key, out string localized))
            return localized;

        return EnglishFallbacks.TryGetValue(key, out string fallback) ? fallback : key;
    }

    public static string Get(string key, params object[] replacements)
    {
        string text = Get(key);
        if (replacements == null)
            return text;

        for (int i = 0; i + 1 < replacements.Length; i += 2)
        {
            string name = Convert.ToString(replacements[i]);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string value = Convert.ToString(replacements[i + 1]);
            text = text.Replace("{" + name + "}", value ?? string.Empty);
        }

        return text;
    }

    private static void EnsureLoaded()
    {
        string locale = GetCurrentLocale();
        if (loadedTexts != null && string.Equals(loadedLocale, locale, StringComparison.OrdinalIgnoreCase))
            return;

        Dictionary<string, string> texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string pluginDirectory = GetPluginDirectory();
        LoadLocaleFile(texts, Path.Combine(pluginDirectory, "Locales", DefaultLocale + ".txt"));

        if (!string.Equals(locale, DefaultLocale, StringComparison.OrdinalIgnoreCase))
            LoadLocaleFile(texts, Path.Combine(pluginDirectory, "Locales", locale + ".txt"));

        loadedTexts = texts;
        loadedLocale = locale;
    }

    private static void LoadLocaleFile(Dictionary<string, string> target, string path)
    {
        if (!File.Exists(path))
            return;

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            int separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            if (key.Length > 0)
                target[key] = value.Replace("\\n", Environment.NewLine);
        }
    }

    private static string GetPluginDirectory()
    {
        try
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
                return Path.GetDirectoryName(assemblyLocation);
        }
        catch
        {
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string GetCurrentLocale()
    {
        try
        {
            string language = GameAssetManagerAPI.Instance.CurrentLanguage;
            if (!string.IsNullOrWhiteSpace(language))
                return NormalizeLocale(language);
        }
        catch
        {
        }

        return DefaultLocale;
    }

    private static string NormalizeLocale(string locale)
    {
        locale = locale.Trim().Replace('_', '-');
        if (locale.Length == 4 && locale.IndexOf('-') < 0)
            return locale.Substring(0, 2).ToLowerInvariant() + "-" + locale.Substring(2, 2).ToUpperInvariant();

        return locale;
    }
}
