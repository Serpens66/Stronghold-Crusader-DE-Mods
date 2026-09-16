using System;

namespace Shared
{
    /// <summary>GameplayModAllowedContext in the centralized mission policy contract.</summary>
    [Flags]
    public enum GameplayModAllowedContext
    {
        /// <summary>None.</summary>
        None = 0,
        /// <summary>CustomGame.</summary>
        CustomGame = 1 << 0,
        /// <summary>CustomizedVanillaTrail.</summary>
        CustomizedVanillaTrail = 1 << 1,
        /// <summary>CustomizedCustomTrail.</summary>
        CustomizedCustomTrail = 1 << 2,
        /// <summary>CustomizedCoopTrail.</summary>
        CustomizedCoopTrail = 1 << 3,
        /// <summary>CustomizedSandsOfTime.</summary>
        CustomizedSandsOfTime = 1 << 4,
        /// <summary>MapEditor.</summary>
        MapEditor = 1 << 5,
        /// <summary>Campaign.</summary>
        Campaign = 1 << 6,
        /// <summary>StandaloneMission.</summary>
        StandaloneMission = 1 << 7,
        /// <summary>VanillaTrail.</summary>
        VanillaTrail = 1 << 8,
        /// <summary>CustomTrail.</summary>
        CustomTrail = 1 << 9,
        /// <summary>CoopTrail.</summary>
        CoopTrail = 1 << 10,
        /// <summary>SandsOfTime.</summary>
        SandsOfTime = 1 << 11,
    }

    /// <summary>GameplayModActivationProfile in the centralized mission policy contract.</summary>
    public readonly struct GameplayModActivationProfile
    {
        /// <summary>GameplayModActivationProfile in the centralized mission policy contract.</summary>
        public GameplayModActivationProfile(
            string modGuid,
            string displayName,
            GameplayModAllowedContext allowedContexts)
        {
            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? modGuid : displayName;
            AllowedContexts = allowedContexts;
        }

        /// <summary>ModGuid in the centralized mission policy contract.</summary>
        public string ModGuid { get; }
        /// <summary>DisplayName in the centralized mission policy contract.</summary>
        public string DisplayName { get; }
        /// <summary>AllowedContexts in the centralized mission policy contract.</summary>
        public GameplayModAllowedContext AllowedContexts { get; }
    }

    /// <summary>Single typed source of truth for mode permissions of regular gameplay mods.</summary>
    public static class GameplayModModePolicy
    {
        private const GameplayModAllowedContext RegularContexts =
            GameplayModAllowedContext.CustomGame |
            GameplayModAllowedContext.CustomizedVanillaTrail |
            GameplayModAllowedContext.CustomizedCustomTrail |
            GameplayModAllowedContext.CustomizedCoopTrail |
            GameplayModAllowedContext.CustomizedSandsOfTime |
            GameplayModAllowedContext.MapEditor;

        /// <summary>GetProfile in the centralized mission policy contract.</summary>
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
                    return Create(modGuid, displayName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(modGuid), modGuid, "Unknown gameplay mod GUID.");
            }
        }

        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
        public static bool IsAllowed(
            GameplayModActivationProfile profile,
            GameModeSnapshot snapshot,
            out string reason)
        {
            if (snapshot.HasConflictingCustomizedOrigin)
            {
                reason = "conflicting-customize-origin";
                return false;
            }

            GameplayModAllowedContext context = ResolveContext(snapshot);
            if (context == GameplayModAllowedContext.None)
            {
                reason = snapshot.Kind == GameModeKind.Unknown
                    ? "unknown-fail-closed"
                    : snapshot.IsMissionContent ? "direct-mission-content" : "mode-not-allowed";
                return false;
            }

            bool allowed = (profile.AllowedContexts & context) == context;
            reason = allowed ? ToReason(context) : "profile-does-not-allow-" + context;
            return allowed;
        }

        private static GameplayModActivationProfile Create(string modGuid, string displayName) =>
            new GameplayModActivationProfile(modGuid, displayName, RegularContexts);

        /// <summary>ResolveContext in the centralized mission policy contract.</summary>
        public static GameplayModAllowedContext ResolveContext(GameModeSnapshot snapshot)
        {
            if (snapshot.Kind == GameModeKind.MapEditor)
                return GameplayModAllowedContext.MapEditor;
            if (snapshot.Kind == GameModeKind.CustomGame && !snapshot.IsCustomized)
                return GameplayModAllowedContext.CustomGame;
            if (!snapshot.IsCustomized)
            {
                switch (snapshot.Kind)
                {
                    case GameModeKind.Campaign: return GameplayModAllowedContext.Campaign;
                    case GameModeKind.StandaloneMission: return GameplayModAllowedContext.StandaloneMission;
                    case GameModeKind.VanillaTrail: return GameplayModAllowedContext.VanillaTrail;
                    case GameModeKind.CustomTrail: return GameplayModAllowedContext.CustomTrail;
                    case GameModeKind.CoopTrail: return GameplayModAllowedContext.CoopTrail;
                    case GameModeKind.SandsOfTime: return GameplayModAllowedContext.SandsOfTime;
                    default: return GameplayModAllowedContext.None;
                }
            }

            switch (snapshot.Kind)
            {
                case GameModeKind.VanillaTrail: return GameplayModAllowedContext.CustomizedVanillaTrail;
                case GameModeKind.CustomTrail: return GameplayModAllowedContext.CustomizedCustomTrail;
                case GameModeKind.CoopTrail: return GameplayModAllowedContext.CustomizedCoopTrail;
                case GameModeKind.SandsOfTime: return GameplayModAllowedContext.CustomizedSandsOfTime;
                default: return GameplayModAllowedContext.None;
            }
        }

        private static string ToReason(GameplayModAllowedContext context)
        {
            if (context == GameplayModAllowedContext.CustomGame)
                return "custom-game";
            if (context == GameplayModAllowedContext.MapEditor)
                return "map-editor";
            return "verified-customize-origin";
        }
    }
}
