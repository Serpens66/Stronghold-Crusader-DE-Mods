using APIShared.ModSettings;
using APIShared.GameModes;
using System;

namespace Shared
{
    /// <summary>Serps gameplay-policy notice; APIShared only supplies the reusable presentation contract.</summary>
    internal static class DirectLaunchSettingsNotice
    {
        internal static void Configure(PresetLobbyModSettingsViewModel settings, string modGuid, bool blueprintsRemainAvailable = false)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.System_ModeAvailability.ConfigureDefault(SerpsModProfiles.GetProfile(modGuid, modGuid));
            ConfigureFeatureRules(settings.System_ModeAvailability, modGuid);
            settings.System_ConfigureDirectLaunchNotice(() => ResolveText(blueprintsRemainAvailable) + " " + settings.System_ModeNoticeText);
        }

        private static void ConfigureFeatureRules(ModSettingsModeAvailability availability, string modGuid)
        {
            GameplayFeatureId? defaultFeature = null;
            switch (modGuid)
            {
                case "BuildingLimit_Serp": defaultFeature = GameplayFeatureId.BuildingLimitEnforcement; break;
                case "UnitCosts_Serp": defaultFeature = GameplayFeatureId.UnitCostEnforcement; break;
                case "UnitLimit_Serp": defaultFeature = GameplayFeatureId.UnitLimitEnforcement; break;
                case "CheatMod_Serp": defaultFeature = GameplayFeatureId.EndlessExtremePowersRecharge; break;
                case "RandomEvents_Serp": defaultFeature = GameplayFeatureId.RandomEventsRuntime; break;
            }
            if (defaultFeature.HasValue) availability.ConfigureDefault(ToSharedProfile(modGuid, defaultFeature.Value));
            if (modGuid == "ExtraFeatures_Serp")
            {
                availability.Configure("extra.lord-health", ToSharedProfile(modGuid, GameplayFeatureId.LordHealthMultipliers));
                // These settings are intentionally separate values for the two network contexts.
                availability.ConfigureNetworkVariant("extra.human-enemy-proximity-singleplayer", false);
                availability.ConfigureNetworkVariant("extra.ai-enemy-proximity-singleplayer", false);
                availability.ConfigureNetworkVariant("extra.human-enemy-proximity-multiplayer", true);
                availability.ConfigureNetworkVariant("extra.ai-enemy-proximity-multiplayer", true);
            }
            if (modGuid == "ImprovedHunters_Serp")
            {
                availability.Configure("hunters.improved-target-selection", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection));
                availability.Configure("hunters.improved-pathfinding", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding));
                availability.Configure("hunters.allow-dead-targets", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection));
            }
            if (modGuid == "CastlePlanner_Serp")
            {
                availability.ConfigureDefault(ToSharedProfile(modGuid, GameplayFeatureId.CastleSpawning));
                availability.Configure("castle-planner.blueprints", ToSharedProfile(modGuid, GameplayFeatureId.CastleBlueprints));
                availability.Configure("castle-planner.hotkey", null);
            }
        }

        private static GameplayModActivationProfile ToSharedProfile(string modGuid, GameplayFeatureId featureId)
        {
            var feature = GameplayFeatureModePolicy.GetProfile(modGuid, featureId);
            return new GameplayModActivationProfile(modGuid, featureId.ToString(), feature.AllowedContexts, feature.AllowRealMultiplayer);
        }

        private static string ResolveText(bool blueprintsRemainAvailable)
        {
            bool german = SerpLocalization.GetActiveLocale().StartsWith("de", StringComparison.OrdinalIgnoreCase);
            if (blueprintsRemainAvailable)
                return german
                    ? "Burgplatzierung und Spieländerungen sind für diesen direkten Start inaktiv; Blaupausen bleiben verfügbar. Über „Customize“ starten, um die Spieländerungen zu nutzen. Änderungen hier werden gespeichert."
                    : "Castle spawning and gameplay changes are inactive for this direct start. Blueprints remain available. Use Customize to play with these changes; edits here are saved for later games.";
            return german
                ? "Die Spieländerungen dieser Mod sind für diesen direkten Start inaktiv. Über „Customize“ starten, um damit zu spielen. Änderungen hier werden für spätere Partien gespeichert."
                : "This mod's gameplay changes are inactive for this direct start. Use Customize to play with them; edits here are saved for later games.";
        }
    }
}
