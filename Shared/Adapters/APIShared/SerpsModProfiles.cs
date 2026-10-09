using System;
using APIShared.GameModes;

namespace Shared
{
    /// <summary>Established permissions for Serps mods, separate from the optional general evaluator.</summary>
    internal static class SerpsModProfiles
    {
        private const GameplayModAllowedContext RegularContexts =
            GameplayModAllowedContext.CustomGame |
            GameplayModAllowedContext.CustomizedVanillaTrail |
            GameplayModAllowedContext.CustomizedCustomTrail |
            GameplayModAllowedContext.CustomizedCoopTrail |
            GameplayModAllowedContext.CustomizedSandsOfTime |
            GameplayModAllowedContext.MapEditor;

        /// <summary>Gets the established Serps gameplay profile. Unknown mod GUIDs are rejected; third-party mods construct their own profile.</summary>
        public static GameplayModActivationProfile GetProfile(string modGuid, string displayName)
        {
            switch (modGuid)
            {
                case "BuildingCosts_Serp":
                case "BuildingLimit_Serp":
                case "CastlePlanner_Serp":
                case "CheatMod_Serp":
                case "ExtraFeatures_Serp":
                case "ExtremePowers_Serp":
                case "ImprovedHunters_Serp":
                case "RandomEvents_Serp":
                case "StartConditions_Serp":
                case "UnitCosts_Serp":
                case "UnitLimit_Serp":
                    return new GameplayModActivationProfile(modGuid, displayName, RegularContexts);
                default:
                    throw new ArgumentOutOfRangeException(nameof(modGuid), modGuid, "Unknown gameplay mod GUID.");
            }
        }

    }
}
