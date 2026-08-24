using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public static class SerpLocalization
{
    public const string ResetToDefault = "Common.ResetToDefault";
    public const string EnableMod = "Common.EnableMod";
    public const string HostActivationLabel = "Common.HostActivationLabel";
    public const string ClientActivationLabel = "Common.ClientActivationLabel";
    public const string HostSettingsActivationHelp = "Common.HostSettingsActivationHelp";
    public const string ClientSettingsActivationHelp = "Common.ClientSettingsActivationHelp";
    public const string HostOptions = "Common.HostOptions";
    public const string ClientOptions = "Common.ClientOptions";
    public const string HostReadOnly = "Common.HostReadOnly";
    public const string ResetToDefaultHelp = "Common.ResetToDefaultHelp";
    public const string EnableModHelp = "Common.EnableModHelp";
    public const string PresetHelp = "Common.PresetHelp";
    public const string Preset = "Common.Preset";
    public const string ActionsScopeHost = "Common.ActionsScopeHost";
    public const string ActionsScopeClient = "Common.ActionsScopeClient";
    public const string Ai = "Common.Ai";
    public const string Human = "Common.Human";
    public const string LegacySomeSettingsWarning = "Common.LegacySomeSettingsWarning";
    public const string Limit = "Common.Limit";
    public const string Max = "Common.Max";
    public const string UnitLimitsTitle = "UnitLimit.Title";
    public const string UnitLimitsHelp = "UnitLimit.Help";
    public const string BuildingsProductionTitle = "SomeSettings.BuildingsProductionTitle";
    public const string CampfirePeasants = "SomeSettings.CampfirePeasants";
    public const string CampfirePeasantsHelp = "SomeSettings.CampfirePeasantsHelp";
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
    public const string AllowCameraMovementWithModifiers = "SomeSettings.AllowCameraMovementWithModifiers";
    public const string AllowCameraMovementWithModifiersHelp = "SomeSettings.AllowCameraMovementWithModifiersHelp";
    public const string HdMarketView = "SomeSettings.HdMarketView";
    public const string HdMarketViewHelp = "SomeSettings.HdMarketViewHelp";
    public const string MarketGoodsOrderTitle = "BugfixesAndQoL.MarketGoodsOrderTitle";
    public const string MarketGoodsOrderHelp = "BugfixesAndQoL.MarketGoodsOrderHelp";
    public const string MarketGoodsOrderRestoreHd = "BugfixesAndQoL.MarketGoodsOrderRestoreHd";
    public const string MarketGoodsOrderRestoreHdHelp = "BugfixesAndQoL.MarketGoodsOrderRestoreHdHelp";
    public const string MarketGoodsOrderMovePreviousHelp = "BugfixesAndQoL.MarketGoodsOrderMovePreviousHelp";
    public const string MarketGoodsOrderMoveNextHelp = "BugfixesAndQoL.MarketGoodsOrderMoveNextHelp";
    public const string MarketGoodsOrderPositionHelp = "BugfixesAndQoL.MarketGoodsOrderPositionHelp";
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
    public const string LordHealthTitle = "SomeSettings.LordHealthTitle";
    public const string LordHealthHelp = "SomeSettings.LordHealthHelp";
    public const string MultiplyGoodsGain = "SomeSettings.MultiplyGoodsGain";
    public const string MultiplyGoodsGainHelp = "SomeSettings.MultiplyGoodsGainHelp";
    public const string MultiplyGoodsAsMoney = "SomeSettings.MultiplyGoodsAsMoney";
    public const string MultiplyGoodsAsMoneyHelp = "SomeSettings.MultiplyGoodsAsMoneyHelp";
    public const string MarketPriceMultipliersTitle = "SomeSettings.MarketPriceMultipliersTitle";
    public const string MarketGoodPriceMultipliersHelp = "SomeSettings.MarketGoodPriceMultipliersHelp";
    public const string MarketGoodPriceMultiplierHelp = "SomeSettings.MarketGoodPriceMultiplierHelp";
    public const string MarketBuyPriceMultiplier = "SomeSettings.MarketBuyPriceMultiplier";
    public const string MarketBuyPriceMultiplierHelp = "SomeSettings.MarketBuyPriceMultiplierHelp";
    public const string MarketSellPriceMultiplier = "SomeSettings.MarketSellPriceMultiplier";
    public const string MarketSellPriceMultiplierHelp = "SomeSettings.MarketSellPriceMultiplierHelp";
    public const string MarketPricesAlsoForAI = "SomeSettings.MarketPricesAlsoForAI";
    public const string MarketPricesAlsoForAIHelp = "SomeSettings.MarketPricesAlsoForAIHelp";
    public const string RememberAiAivSettings = "SomeSettings.RememberAiAivSettings";
    public const string RememberAiAivSettingsHelp = "SomeSettings.RememberAiAivSettingsHelp";
    public const string EnableCtrlSingleMarketTrade = "SomeSettings.EnableCtrlSingleMarketTrade";
    public const string EnableCtrlSingleMarketTradeHelp = "SomeSettings.EnableCtrlSingleMarketTradeHelp";
    public const string EnableClientFeatures = "SomeSettings.EnableClientFeatures";
    public const string EnableClientFeaturesHelp = "SomeSettings.EnableClientFeaturesHelp";
    public const string EnableAllyGoodsAmountModifiers = "SomeSettings.EnableAllyGoodsAmountModifiers";
    public const string EnableAllyGoodsAmountModifiersHelp = "SomeSettings.EnableAllyGoodsAmountModifiersHelp";
    public const string EnableSingleBuildingPause = "SomeSettings.EnableSingleBuildingPause";
    public const string EnableSingleBuildingPauseHelp = "SomeSettings.EnableSingleBuildingPauseHelp";
    public const string EnableMultiplayerGameSpeedChanges = "SomeSettings.EnableMultiplayerGameSpeedChanges";
    public const string EnableMultiplayerGameSpeedChangesHelp = "SomeSettings.EnableMultiplayerGameSpeedChangesHelp";
    public const string EnableShiftGameSpeedSteps = "SomeSettings.EnableShiftGameSpeedSteps";
    public const string EnableShiftGameSpeedStepsHelp = "SomeSettings.EnableShiftGameSpeedStepsHelp";
    public const string EnableTroopMovementFix = "SomeSettings.EnableTroopMovementFix";
    public const string EnableTroopMovementFixHelp = "SomeSettings.EnableTroopMovementFixHelp";
    public const string EnableAiFixes = "BugfixesAndQoL.EnableAiFixes";
    public const string EnableAiFixesHelp = "BugfixesAndQoL.EnableAiFixesHelp";
    public const string EnablePlaguePopularityFix = "SomeSettings.EnablePlaguePopularityFix";
    public const string EnablePlaguePopularityFixHelp = "SomeSettings.EnablePlaguePopularityFixHelp";
    public const string EnablePlagueCloudRemovalFix = "SomeSettings.EnablePlagueCloudRemovalFix";
    public const string EnablePlagueCloudRemovalFixHelp = "SomeSettings.EnablePlagueCloudRemovalFixHelp";
    public const string EnableStuckApothecaryFix = "SomeSettings.EnableStuckApothecaryFix";
    public const string EnableStuckApothecaryFixHelp = "SomeSettings.EnableStuckApothecaryFixHelp";
    public const string EnablePlagueTargetReservationFix = "SomeSettings.EnablePlagueTargetReservationFix";
    public const string EnablePlagueTargetReservationFixHelp = "SomeSettings.EnablePlagueTargetReservationFixHelp";
    public const string EnableFastRecruitRallyMovement = "SomeSettings.EnableFastRecruitRallyMovement";
    public const string EnableFastRecruitRallyMovementHelp = "SomeSettings.EnableFastRecruitRallyMovementHelp";
    public const string EnableMonksAlwaysRun = "SomeSettings.EnableMonksAlwaysRun";
    public const string EnableMonksAlwaysRunHelp = "SomeSettings.EnableMonksAlwaysRunHelp";
    public const string EnablePortableShieldsOnWalls = "SomeSettings.EnablePortableShieldsOnWalls";
    public const string EnablePortableShieldsOnWallsHelp = "SomeSettings.EnablePortableShieldsOnWallsHelp";
    public const string EnableKnightDismount = "SomeSettings.EnableKnightDismount";
    public const string EnableKnightDismountHelp = "SomeSettings.EnableKnightDismountHelp";
    public const string InstantHorse = "SomeSettings.InstantHorse";
    public const string InstantHorseHelp = "SomeSettings.InstantHorseHelp";
    public const string KnightDismountTooltip = "SomeSettings.KnightDismountTooltip";
    public const string KnightDismountTooltipBody = "SomeSettings.KnightDismountTooltipBody";
    public const string KnightMountTooltip = "SomeSettings.KnightMountTooltip";
    public const string KnightMountTooltipBody = "SomeSettings.KnightMountTooltipBody";
    public const string EnableQuarryPileRelocation = "SomeSettings.EnableQuarryPileRelocation";
    public const string EnableQuarryPileRelocationHelp = "SomeSettings.EnableQuarryPileRelocationHelp";
    public const string EnableAIQuarryPileTowardsKeep = "SomeSettings.EnableAIQuarryPileTowardsKeep";
    public const string EnableAIQuarryPileTowardsKeepHelp = "SomeSettings.EnableAIQuarryPileTowardsKeepHelp";
    public const string EnableExtraChurchPriests = "SomeSettings.EnableExtraChurchPriests";
    public const string EnableExtraChurchPriestsHelp = "SomeSettings.EnableExtraChurchPriestsHelp";
    public const string PlagueDurationMultiplier = "SomeSettings.PlagueDurationMultiplier";
    public const string PlagueDurationMultiplierHelp = "SomeSettings.PlagueDurationMultiplierHelp";
    public const string ApothecaryPlagueSearchDistance = "SomeSettings.ApothecaryPlagueSearchDistance";
    public const string ApothecaryPlagueSearchDistanceHelp = "SomeSettings.ApothecaryPlagueSearchDistanceHelp";
    public const string QuarryPileRelocationTooltip = "SomeSettings.QuarryPileRelocationTooltip";
    public const string QuarryPileRelocationTooltipBody = "SomeSettings.QuarryPileRelocationTooltipBody";
    public const string AiEconomyProtectionTitle = "SomeSettings.AIEconomyProtectionTitle";
    public const string PreventAIPause = "SomeSettings.PreventAIPause";
    public const string PreventAIPauseHelp = "SomeSettings.PreventAIPauseHelp";
    public const string PreventEmergencyDemolition = "SomeSettings.PreventEmergencyDemolition";
    public const string PreventEmergencyDemolitionHelp = "SomeSettings.PreventEmergencyDemolitionHelp";
    public const string PreventHovelDeletion = "SomeSettings.PreventHovelDeletion";
    public const string InaccessibleAIBuildingDemolitionProtection = "SomeSettings.InaccessibleAIBuildingDemolitionProtection";
    public const string InaccessibleAIBuildingDemolitionProtectionHelp = "SomeSettings.InaccessibleAIBuildingDemolitionProtectionHelp";
    public const string InaccessibleAIBuildingDemolitionModeVanilla = "SomeSettings.InaccessibleAIBuildingDemolitionModeVanilla";
    public const string InaccessibleAIBuildingDemolitionModeTemporary = "SomeSettings.InaccessibleAIBuildingDemolitionModeTemporary";
    public const string InaccessibleAIBuildingDemolitionModeAlways = "SomeSettings.InaccessibleAIBuildingDemolitionModeAlways";
    public const string SerpsModsStatusTitle = "SerpsModsHost.StatusTitle";
    public const string SerpsModsSummaryTitle = "SerpsModsHost.SummaryTitle";
    public const string SerpsModsErrorsTitle = "SerpsModsHost.ErrorsTitle";
    public const string SerpsModsRefresh = "SerpsModsHost.Refresh";
    public const string SerpsModsRefreshHelp = "SerpsModsHost.RefreshHelp";
    public const string SerpsModsClearErrors = "SerpsModsHost.ClearErrors";
    public const string SerpsModsClearErrorsHelp = "SerpsModsHost.ClearErrorsHelp";
    public const string SerpsModsNoErrors = "SerpsModsHost.NoErrors";
    public const string SerpsModsSummaryFormat = "SerpsModsHost.SummaryFormat";
    public const string SerpsModsLobbyHashMismatch = "SerpsModsHost.LobbyHashMismatch";
    public const string PreventHovelDeletionHelp = "SomeSettings.PreventHovelDeletionHelp";
    public const string AivPlacementComplete = "CastlePlanner.AIVPlacement.Complete";
    public const string AivPlacementPartial = "CastlePlanner.AIVPlacement.Partial";
    public const string AivPlacementImpossible = "CastlePlanner.AIVPlacement.Impossible";
    public const string AivPlacementNotEvaluable = "CastlePlanner.AIVPlacement.NotEvaluable";
    public const string AivPlacementPreBuildUnsupported = "CastlePlanner.AIVPlacement.PreBuildUnsupported";
    public const string AivPlacementHostOnly = "CastlePlanner.AIVPlacement.HostOnly";
    public const string AivPlacementChecking = "CastlePlanner.AIVPlacement.Checking";

    private const string DefaultLocale = "en-US";
    private static Dictionary<string, string> loadedTexts;
    private static string loadedLocale;

    private static readonly Dictionary<string, string> EnglishFallbacks = new Dictionary<string, string>
    {
        { SerpsModsStatusTitle, "Serps Mods diagnostics" },
        { SerpsModsSummaryTitle, "PACK STATUS" },
        { SerpsModsErrorsTitle, "ERRORS" },
        { SerpsModsRefresh, "Refresh" },
        { SerpsModsRefreshHelp, "Checks whether all expected child plugins are loaded with the packaged versions." },
        { SerpsModsClearErrors, "Clear displayed errors" },
        { SerpsModsClearErrorsHelp, "Clears only this diagnostic display. The BepInEx log remains unchanged." },
        { SerpsModsNoErrors, "No errors recorded." },
        { SerpsModsSummaryFormat, "Pack {Version}: expected {Expected}, validated {Validated}, registered {Registered}." },
        { SerpsModsLobbyHashMismatch, "ERROR: The installed mods of {Player} differ from lobby host {Host} ({Player}: {PlayerHash}, {Host}: {HostHash})." },
        { ResetToDefault, "Reset to Default" },
        { EnableMod, "Enable Mod" },
        { HostActivationLabel, "(Host-)" },
        { ClientActivationLabel, "(Client settings)" },
        { HostSettingsActivationHelp, "Enables or disables all host-controlled settings of this mod." },
        { ClientSettingsActivationHelp, "Enables or disables all local and personal client settings of this mod." },
        { HostOptions, "HOST OPTIONS" },
        { ClientOptions, "LOCAL CLIENT OPTIONS" },
        { HostReadOnly, "Values from host - read-only" },
        { ResetToDefaultHelp, "Resets the settings you can control in the current context." },
        { EnableModHelp, "Enables or disables this mod for the match." },
        { "CustomCustomTrail.HostOptions", "HOST OPTIONS" },
        { "CustomCustomTrail.SupportedTrailSettings", "MOD SETTINGS IN CUSTOM TRAILS" },
        { "CustomCustomTrail.SupportedTrailSettingsHelp", "Select which compatible mods are saved with newly created Custom Trail missions. Enabled by default. Unselected mods remain unchanged when the Trail is played." },
        { "CustomCustomTrail.IncompatibleTrailMods", "Installed mods with incompatible mod settings:" },
        { "CustomCustomTrail.CompatibilityGuide", "How can mod authors add compatibility?" },
        { "CustomCustomTrail.CompatibilityGuideHelp", "Opens the Custom Custom Trail compatibility guide in your browser." },
        { "CustomCustomTrail.CoopPackage", "Active Coop Trail package" },
        { "CustomCustomTrail.CoopPackageHelp", "Selects the installed Coop Trail package used by the host. Missions 1-10 replace Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Missing slots remain Vanilla." },
        { "CustomCustomTrail.CoopPackageStatusLabel", "Local package status:" },
        { "CustomCustomTrail.HostReadOnlyNotice", "The host controls the active Coop Trail package." },
        { "CustomCustomTrail.StatusVanilla", "Vanilla Coop Trails are active." },
        { "CustomCustomTrail.StatusReady", "Package validated and ready." },
        { "CustomCustomTrail.StatusChecking", "Waiting for the host package information." },
        { "CustomCustomTrail.VanillaPackage", "Vanilla - no custom package" },
        { "CustomCustomTrail.ErrorModDisabled", "Custom Custom Trail is disabled locally." },
        { "CustomCustomTrail.ErrorPackageMissing", "The selected Coop Trail package is not installed:" },
        { "CustomCustomTrail.ErrorFingerprintMismatch", "The local package contents differ from the host package." },
        { "CustomCustomTrail.ErrorPackageInvalid", "The selected Coop Trail package is invalid:" },
        { "CustomCustomTrail.ErrorPackageNotReady", "The selected Coop Trail package is not ready." },
        { "CustomCustomTrail.ErrorParticipantNotReady", "At least one participant is missing the selected package or has different contents." },
        { "CustomCustomTrail.ErrorParticipantsMissing", "Package missing for:" },
        { "CustomCustomTrail.ErrorParticipantsMismatch", "Package contents differ for:" },
        { "CustomCustomTrail.ErrorParticipantsNotReady", "Package status not ready for:" },
        { "CustomCustomTrail.StartBlockedTitle", "Coop Trail cannot start" },
        { "CustomCustomTrail.TrailMakerCoop", "Coop Trail" },
        { "CustomCustomTrail.TrailMakerCoopHelp", "Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest." },
        { "CustomCustomTrail.ExportFailedTitle", "Coop Trail export failed" },
        { "CustomCustomTrail.ExportFailed", "The normal Trail was not exported because the Coop package could not be created." },
        { PresetHelp, "Selects a saved preset. Clients change only their personal settings." },
        { Preset, "Preset" },
        { ActionsScopeHost, "Preset and reset affect host settings and your local client settings." },
        { ActionsScopeClient, "Preset and reset affect only your local client settings." },
        { "Common.Clear", "Clear" },
        { "CastlePlanner.Title", "HUMAN CASTLE" },
        { "CastlePlanner.Help", "Displays the selected AIVJSON as a local blueprint and/or spawns it in a new singleplayer game. Blueprints are local; Spawn Castle is controlled by the host." },
        { "CastlePlanner.LocalOptions", "LOCAL CLIENT OPTIONS" },
        { "CastlePlanner.Castle", "AIVJSON castle" },
        { "CastlePlanner.CastleHelp", "Selects the AIVJSON castle used for the local blueprint or singleplayer spawn." },
        { "CastlePlanner.Inventory", "{0} local AIVJSON files found. The selection remains local in multiplayer." },
        { "CastlePlanner.Mode", "Mode" },
        { "CastlePlanner.ModeHelp", "Shows the selected castle as a local blueprint or spawns it in a new singleplayer game." },
        { "CastlePlanner.Mode.Blueprint", "Blueprint" },
        { "CastlePlanner.Mode.Spawn", "Spawn castle" },
        { "CastlePlanner.Blueprints", "Blueprints" },
        { "CastlePlanner.BlueprintsHelp", "Displays the selected castle as a local blueprint without changing the game simulation." },
        { "CastlePlanner.SpawnCastle", "Spawn Castle" },
        { "CastlePlanner.SpawnCastleHelp", "Host setting: spawns the host's selected castle when a new supported singleplayer game starts." },
        { "CastlePlanner.Hotkey", "Blueprint toggle key" },
        { "CastlePlanner.HotkeyHelp", "Assigns the key or mouse button that toggles the blueprint display." },
        { "CastlePlanner.ClearHelp", "Removes the assigned blueprint toggle key." },
        { "CastlePlanner.NotAssigned", "Not assigned" },
        { "CastlePlanner.PressAnyKey", "Press any key..." },
        { "CastlePlanner.AssignKey", "Assign key" },
        { "CastlePlanner.Hud.Settings", "Blueprint settings" },
        { "CastlePlanner.Hud.IconScale", "Icon scale" },
        { "CastlePlanner.Hud.IconAlpha", "Icon opacity" },
        { "CastlePlanner.Hud.Unavailable", "Blueprint: unavailable" },
        { "CastlePlanner.Hud.Loading", "Blueprint: loading {0}/{1}" },
        { "CastlePlanner.Hud.On", "Blueprint: on" },
        { "CastlePlanner.Hud.Off", "Blueprint: off" },
        { "CastlePlanner.CastleSectionTitle", "Castle Blueprint" },
        { "CastlePlanner.PlacementControlsTitle", "Placement and Controls" },
        { "RandomEvents.Interval", "Interval (Vanilla months)" },
        { "RandomEvents.MonthsValueFormat", "{0} months" },
        { "RandomEvents.GroupsValueFormat", "{0} groups" },
        { "RandomEvents.ScheduleTitle", "SCHEDULE" },
        { "RandomEvents.MultiplayerTitle", "MULTIPLAYER" },
        { "RandomEvents.IntervalHelp", "The first roll happens after one complete interval. Every event rolls independently." },
        { "RandomEvents.Cooldown", "Cooldown of an event (months)" },
        { "RandomEvents.CooldownHelp", "After a specific event has triggered, it will not trigger again for this many months." },
        { "RandomEvents.ChancesTitle", "EVENT CHANCES (%)" },
        { "RandomEvents.PositiveEventsTitle", "Positive Events" },
        { "RandomEvents.NegativeEventsTitle", "Negative Events" },
        { "RandomEvents.ChanceHelp", "Each event rolls independently whenever an interval completes: a random value from 0 to 99 must be lower than this percentage, and the event must be off cooldown. Multiple events can trigger in the same interval." },
        { "RandomEvents.StrengthTitle", "EVENT STRENGTH" },
        { "RandomEvents.ScaledStrengthHelp", "When bandits or archers trigger, a factor between the selected minimum and maximum is rolled in increments of 0.1. Units = floor(elapsed game months since map start × factor ÷ 3). A result of 0 spawns no units." },
        { "RandomEvents.PlagueStrengthHelp", "Sets the minimum and maximum plague strength. A value in this range is rolled when the event triggers." },
        { "RandomEvents.LionStrengthHelp", "Sets the minimum and maximum number of lion groups. A value in this range is rolled when the event triggers." },
        { "RandomEvents.TheftStrengthHelp", "Sets the minimum and maximum percentage of granary food stolen. A value in this range is rolled when the event triggers." },
        { "RandomEvents.FireStrengthHelp", "Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers." },
        { "RandomEvents.Minimum", "Min" },
        { "RandomEvents.Maximum", "Max" },
        { "RandomEvents.MultiplayerMode", "Multiplayer event distribution" },
        { "RandomEvents.MultiplayerModeHelp", "Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized." },
        { "RandomEvents.MultiplayerShared", "Shared events" },
        { "RandomEvents.MultiplayerIndividual", "Individual rolls" },
        { "RandomEvents.Event.Fair", "Fair" },
        { "RandomEvents.Event.Plague", "Plague" },
        { "RandomEvents.Event.WheatInfestation", "Wheat infestation" },
        { "RandomEvents.Event.HopsBeetles", "Hops beetles" },
        { "RandomEvents.Event.AppleBlight", "Apple blight" },
        { "RandomEvents.Event.TreeBlight", "Tree blight" },
        { "RandomEvents.Event.Rabbits", "Rabbit infestation" },
        { "RandomEvents.Event.LionAttack", "Lion attack" },
        { "RandomEvents.Event.Bandits", "Bandits" },
        { "RandomEvents.Event.MadCows", "Mad cows" },
        { "RandomEvents.Event.Archers", "Archers" },
        { "RandomEvents.Event.Marriage", "Marriage" },
        { "RandomEvents.Event.Bard", "Bard" },
        { "RandomEvents.Event.GranaryTheft", "Granary theft" },
        { "RandomEvents.Event.Fire", "Fire" },
        { "BugfixesAndQoL.ClientInterfaceTitle", "Interface and Controls" },
        { "BugfixesAndQoL.AiAivTitle", "AI and AIV" },
        { "BugfixesAndQoL.TroopMovementTitle", "Troop Movement" },
        { "BugfixesAndQoL.PlagueTitle", "Plague" },
        { "BugfixesAndQoL.EnableClientFeatures", "Enable local client features" },
        { "BugfixesAndQoL.EnableClientFeaturesHelp", "Enables or disables this mod's local interface and control features for you." },
        { "BugfixesAndQoL.EnableHostFeatures", "Enable host features" },
        { "BugfixesAndQoL.EnableHostFeaturesHelp", "Enables or disables the host-controlled fixes for the match." },
        { "SomeSettings.ComfortTitle", "Convenience Features" },
        { "SomeSettings.NewFeaturesTitle", "New Gameplay Features" },
        { "SomeSettings.TilesValueFormat", "{0} tiles" },
        { "SomeSettings.PlagueTitle", "Plague" },
        { "ImprovedHunters.BehaviorTitle", "Behavior" },
        { "ImprovedHunters.MeatHelp", "Final meat amount from one collected animal. -1 keeps Vanilla yield for deer and goats. Allowed range: -1 to 100 for deer and goats, otherwise 0 to 100." },
        { "ImprovedHunters.ImprovedTargetSelection", "Improved Target Selection" },
        { "ImprovedHunters.ImprovedTargetSelectionHelp", "Ranks reachable prey by expected meat output and estimated hunting-cycle cost." },
        { "ImprovedHunters.ImprovedPathfinding", "Improved Pathfinding" },
        { "ImprovedHunters.ImprovedPathfindingHelp", "Improves obstacle visibility handling, pursuit continuation, moving-target replanning and Hunter speed selection from the actual remaining path." },
        { "ImprovedHunters.AllowDeadTargets", "Allow Dead Targets" },
        { "ImprovedHunters.AllowDeadTargetsHelp", "Allows unreserved usable animal corpses to participate in Hunter target selection." },
        { "ImprovedHunters.ReliableHunterProjectiles", "Reliable Hunter Projectiles" },
        { "ImprovedHunters.ReliableHunterProjectilesHelp", "Retries Vanilla ranged damage for a validated matching Hunter arrow when it stalls, reaches its target or is deleted without resolving the hit." },
        { "ImprovedHunters.ChickenPopulationTitle", "Granary Chickens" },
        { "ImprovedHunters.MaxNeutralChickensPerPlayer", "Neutral chicken limit" },
        { "ImprovedHunters.MaxNeutralChickensPerPlayerHelp", "Maximum number of living neutral granary chickens assigned to each player. Zero prevents new granary chicken spawns without deleting existing chickens." },
        { "ImprovedHunters.MaxNeutralChickensPerPlayerValueFormat", "{0} chickens per player" },
        { "ImprovedHunters.TargetsYieldTitle", "Targets and Meat Yield" },
        { "StartConditions.StartTroopArmiesTitle", "Start-troop armies" },
        { "StartConditions.ExtraStartUnitsTitle", "Additional start units" },
        { Ai, "AI" },
        { Human, "Human" },
        { LegacySomeSettingsWarning, "ERROR: The obsolete mod SomeSettings_Serp is also loaded. Please uninstall SomeSettings_Serp to avoid duplicate or conflicting features." },
        { Limit, "Limit" },
        { Max, "Max" },
        { UnitLimitsTitle, "Unit Limits (Human)" },
        { UnitLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Existing living units count against the limit." },
        { BuildingsProductionTitle, "Buildings and Production" },
        { CampfirePeasants, "Peasants waiting at the campfire" },
        { CampfirePeasantsHelp, "-1 = unchanged. Allowed range: -1 to 500. Sets the maximum peasants waiting at the campfire." },
        { BuildingLimitsTitle, "Building Limits (Human)" },
        { BuildingLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together." },
        { UnitCostsTitle, "Base Costs (Human and AI)" },
        { UnitCostsHelp, "Good slots apply to European units. unchanged keeps the vanilla slot; gold -1 stays unchanged." },
        { UnitCostsExtraTitle, "Additional Costs (Human only)" },
        { UnitCostsExtraHelp, "0 = no extra cost. Positive values are charged in addition; negative gold refunds up to the current gold cost. AI players ignore this table." },
        { UnitHeader, "Unit" },
        { Slot1Header, "Slot 1" },
        { Slot2Header, "Slot 2" },
        { Slot3Header, "Slot 3" },
        { Slot4HorseHeader, "Slot 4 / Horse" },
        { None, "none" },
        { Horse, "Horse" },
        { ResourcesMissing, "Resources missing" },
        { BuildingCostsTitle, "Building Costs" },
        { BuildingCostsHelp, "-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI)." },
        { BuildingHeader, "Building" },
        { Vanilla, "Vanilla" },
        { VanillaNoCosts, "no costs" },
        { StartGoldTitle, "Start Gold" },
        { StartGoodsTitle, "Start Goods" },
        { StartTroopsTitle, "Start Troops" },
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
        { AllowCameraMovementWithModifiers, "Allow camera movement while holding Ctrl or Alt" },
        { AllowCameraMovementWithModifiersHelp, "When enabled, keyboard and edge scrolling continue to move the camera while Ctrl or Alt is held. This is a per-player setting." },
        { HdMarketView, "Use configured market-goods order" },
        { HdMarketViewHelp, "Uses the configured order in the detailed market view. Disable this option to restore the Definitive Edition order. This is a per-player setting." },
        { MarketGoodsOrderTitle, "Market-goods order" },
        { MarketGoodsOrderHelp, "The numbered icons show the circular market order. Use the arrow buttons to swap a good with its previous or next neighbor." },
        { MarketGoodsOrderRestoreHd, "Restore HD order" },
        { MarketGoodsOrderRestoreHdHelp, "Restores only the market-goods order used by Stronghold Crusader HD." },
        { MarketGoodsOrderMovePreviousHelp, "Move {Good} one position earlier in the circular market order." },
        { MarketGoodsOrderMoveNextHelp, "Move {Good} one position later in the circular market order." },
        { MarketGoodsOrderPositionHelp, "Position {Position}: {Good}" },
        { BulldozeTitle, "Building Demolition" },
        { BulldozeHelp, "-1 = unchanged. Refund values are percentages from 0 to 100." },
        { WoodRefund, "Wood refund %" },
        { StoneRefund, "Stone refund %" },
        { IronRefund, "Iron refund %" },
        { PitchRefund, "Pitch refund %" },
        { GoldRefund, "Gold refund %" },
        { VanillaValue50, "Vanilla value: 50%." },
        { KeepStorageContent, "Keep Storage Content" },
        { KeepStorageContentHelp, "When enabled, bulldozing a granary, armory, or stockpile keeps the goods stored inside by adding them back as incoming goods.\nIf Granary was built for free can't credit goods back (happens when building it while no wood is on stock)" },
        { EconomyBuffsTitle, "Economy Bonuses" },
        { LordHealthTitle, "Lord Health" },
        { LordHealthHelp, "Multiplies the maximum and current health of all Lords when the match starts. Human and AI Lords can be configured separately; individual AI Lord health differences remain intact. 100% is unchanged." },
        { MultiplyGoodsGain, "Multiply goods gain" },
        { MultiplyGoodsGainHelp, "Multiplier for gained goods. 1 = unchanged, 2 = double, 3 = triple. Values 1 or lower add nothing." },
        { MultiplyGoodsAsMoney, "Multiply goods as money" },
        { MultiplyGoodsAsMoneyHelp, "Extra gold payouts based on sell value of gained goods. 0 = unchanged, 1 = one sell-value payout, 2 = two payouts." },
        { MarketPriceMultipliersTitle, "Market Prices" },
        { MarketGoodPriceMultipliersHelp, "For each good, the upper slider controls buying and the lower slider controls selling. Each individual factor is multiplied by the corresponding general factor; 1.0x leaves that good unchanged." },
        { MarketGoodPriceMultiplierHelp, "{Good} - {Direction}: {Value}. This individual factor is multiplied by the corresponding general market factor." },
        { MarketBuyPriceMultiplier, "Buy prices" },
        { MarketBuyPriceMultiplierHelp, "Multiplier for all market buy prices. 1.0 = unchanged, 0.0 = free, 5.0 = five times the vanilla price." },
        { MarketSellPriceMultiplier, "Sell prices" },
        { MarketSellPriceMultiplierHelp, "Multiplier for all market sell prices. 1.0 = unchanged, 0.0 = no gold from selling, 5.0 = five times the vanilla price." },
        { MarketPricesAlsoForAI, "Also apply to AI" },
        { MarketPricesAlsoForAIHelp, "When enabled, AI players use the configured market prices for their decisions, purchases, and sales. When disabled, AI players use Vanilla prices. The configured market prices affect the Economy Bonuses setting independently of this option." },
        { RememberAiAivSettings, "Remember AI castle/settings selection" },
        { RememberAiAivSettingsHelp, "When enabled, the last AIV, rotation, and custom lord settings selected for each AI lord are applied automatically when that AI is added to a skirmish lobby." },
        { EnableCtrlSingleMarketTrade, "Ctrl trades one market unit" },
        { EnableCtrlSingleMarketTradeHelp, "Hold Ctrl while buying or selling to trade exactly one unit. Ctrl+Shift uses the normal five-unit amount." },
        { EnableClientFeatures, "Enable local client features" },
        { EnableClientFeaturesHelp, "Enables or disables this mod's local interface and control features only for you." },
        { EnableAllyGoodsAmountModifiers, "Modify ally goods-transfer amounts" },
        { EnableAllyGoodsAmountModifiersHelp, "In the ally goods-transfer panel, hold Shift for 5x or Ctrl for 0.2x the clicked amount. Holding both uses the normal amount." },
        { EnableSingleBuildingPause, "Enable single-building pause" },
        { EnableSingleBuildingPauseHelp, "Hold Ctrl while toggling a production building's pause state to affect only the selected building. Multiplayer actions use Script Extender's tick-aligned Chore transport." },
        { EnableMultiplayerGameSpeedChanges, "Change Gamespeed in Multiplayer" },
        { EnableMultiplayerGameSpeedChangesHelp, "Lets every human player change the running multiplayer game's speed with the normal increase/decrease keybinds or the in-game options slider. Changes are executed for all players through Script Extender's tick-aligned Chore transport. Multiplayer speed uses Script Extender's configured maximum and is not saved as the local singleplayer default." },
        { EnableShiftGameSpeedSteps, "Enable 25-step game-speed keybinds" },
        { EnableShiftGameSpeedStepsHelp, "Press or hold the normal increase/decrease game-speed keybind. The first change is immediate and repeats every 0.25 seconds while held; hold Shift for 25 instead of 5 per step, up to Script Extender's configured maximum. The options slider keeps its 5-step increments. Multiplayer changes use Script Extender's tick-aligned Chore transport." },
        { EnableTroopMovementFix, "Troop Speed Fix" },
        { EnableTroopMovementFixHelp, "Fixes synchronized movement for mixed troop groups: with a normal movement command, all units move at the speed of the slowest unit in the group." },
        { EnableAiFixes, "AI Fixes" },
        { EnableAiFixesHelp, "Fixes Vanilla AI behavior. Prevents failed knight recruitment without an available horse from reusing an old missing weapon or armour result and repeatedly buying that equipment, and refreshes the stone reserve from open castle buildings before the AI sells stone." },
        { EnablePlaguePopularityFix, "Plague Popularity Fix" },
        { EnablePlaguePopularityFixHelp, "Applies exactly -1 popularity for each active plague outbreak and removes it once all associated plague clouds have expired naturally or been removed by an apothecary." },
        { EnablePlagueCloudRemovalFix, "Plague Cloud Removal Fix" },
        { EnablePlagueCloudRemovalFixHelp, "Ensures every plague cloud affected by an apothecary treatment enters Vanilla's fade-out phase instead of becoming active again." },
        { EnableStuckApothecaryFix, "Stuck Apothecary Fix" },
        { EnableStuckApothecaryFixHelp, "Completes Vanilla's building-exit transition when an apothecary finds a plague target, preventing it from remaining stuck at the building." },
        { EnablePlagueTargetReservationFix, "Apothecary Target Reservation Fix" },
        { EnablePlagueTargetReservationFixHelp, "Prevents different apothecaries from selecting plague clouds covered by the same expected area treatment." },
        { EnableFastRecruitRallyMovement, "Recruits Run to Rally Points" },
        { EnableFastRecruitRallyMovementHelp, "Newly recruited player and AI units move to their rally points using their own Vanilla Fast pace and animation while keeping terrain and state modifiers." },
        { EnableMonksAlwaysRun, "Monks Always Run" },
        { EnableMonksAlwaysRunHelp, "Lets Monks use the normal troop running decision and running animation instead of their special walking restriction. Applies to both the Fighting Monk and Temple Guard skins." },
        { EnablePortableShieldsOnWalls, "Allow Portable Shields on Walls" },
        { EnablePortableShieldsOnWallsHelp, "Lets portable shields use the normal troop pathing flag so they can enter connected walls, gatehouses, and towers via stairs. It does not let them climb enemy walls or create new routes." },
        { EnableKnightDismount, "Enable knight mount/dismount buttons" },
        { EnableKnightDismountHelp, "Adds command buttons to mount swordsmen and dismount mounted knights. Multiplayer actions use Script Extender's tick-aligned Chore transport." },
        { InstantHorse, "Instant Horse" },
        { InstantHorseHelp, "Makes a horse immediately available again after a mounted knight dismounts, instead of requiring the stable to replenish it first." },
        { KnightDismountTooltip, "Dismount" },
        { KnightDismountTooltipBody, "Turns selected mounted knights into swordsmen at the same position." },
        { KnightMountTooltip, "Mount" },
        { KnightMountTooltipBody, "Turns selected swordsmen into mounted knights. Requires available horses in a stable." },
        { EnableQuarryPileRelocation, "Enable quarry pile rotation button" },
        { EnableQuarryPileRelocationHelp, "Adds a button to selected quarries that rotates their linked stone pile clockwise to the next valid position. The existing pile is kept if no valid replacement can be created. Multiplayer actions use Script Extender's tick-aligned Chore transport." },
        { EnableAIQuarryPileTowardsKeep, "Place AI quarry piles towards their Keep" },
        { EnableAIQuarryPileTowardsKeepHelp, "Moves each newly built AI quarry's linked stone pile to the valid Vanilla position nearest to that AI player's Keep. Existing quarries are not changed. Multiplayer actions use Script Extender's tick-aligned Chore transport." },
        { EnableExtraChurchPriests, "Enable extra priests for churches" },
        { EnableExtraChurchPriestsHelp, "When enabled, churches receive two priests and cathedrals receive three priests." },
        { PlagueDurationMultiplier, "Plague cloud duration" },
        { PlagueDurationMultiplierHelp, "Multiplier for the active lifetime of all plague clouds. 1.0x keeps the Vanilla duration; higher values also extend how long clouds can cause damage." },
        { ApothecaryPlagueSearchDistance, "Apothecary plague-search range" },
        { ApothecaryPlagueSearchDistanceHelp, "Maximum Manhattan distance from an apothecary's assigned building to a plague cloud. 30 keeps the Vanilla range." },
        { QuarryPileRelocationTooltip, "Move stone pile" },
        { QuarryPileRelocationTooltipBody, "Moves the linked stone pile clockwise to the next valid position around this quarry." },
        { AiEconomyProtectionTitle, "AI Economy Protection" },
        { PreventAIPause, "Prevent AI building pauses" },
        { PreventAIPauseHelp, "Prevents AI-controlled players from putting their own production buildings to sleep." },
        { PreventEmergencyDemolition, "Prevent AI panic demolition" },
        { PreventEmergencyDemolitionHelp, "Skips the AI emergency resource-recovery demolition block, which can otherwise remove useful buildings under pressure." },
        { PreventHovelDeletion, "Prevent AI hovel deletion" },
        { PreventHovelDeletionHelp, "Blocks direct deletes of living AI-owned hovels while still allowing normal destruction by damage." },
        { InaccessibleAIBuildingDemolitionProtection, "Demolition of unreachable AI buildings" },
        { InaccessibleAIBuildingDemolitionProtectionHelp, "Controls only demolitions caused by Vanilla classifying an AI building as unreachable. Vanilla does not intervene. Improved reachability check treats all living own and allied gatehouses and their associated drawbridges as passable regardless of their current state; Vanilla may demolish only if the building is still unreachable. Always prevent blocks every demolition for unreachability. Other demolition reasons remain unchanged." },
        { InaccessibleAIBuildingDemolitionModeVanilla, "Vanilla" },
        { InaccessibleAIBuildingDemolitionModeTemporary, "Improved reachability check" },
        { InaccessibleAIBuildingDemolitionModeAlways, "Always prevent" },
        { AivPlacementComplete, "Complete fit" },
        { AivPlacementPartial, "Partial fit: {FitPercentage}%, sequential score {SequentialBuildScore}" },
        { AivPlacementImpossible, "Does not fit" },
        { AivPlacementNotEvaluable, "Not evaluable: {Reason}" },
        { AivPlacementPreBuildUnsupported, "Sequential pre-build placement is not supported yet." },
        { AivPlacementHostOnly, "Only the host evaluates AI castle placement." },
        { AivPlacementChecking, "The best AI castle is still being checked." }
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
