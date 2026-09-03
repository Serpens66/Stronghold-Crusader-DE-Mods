using System;

namespace Shared
{
    [Flags]
    internal enum GameplayModAllowedContext
    {
        None = 0,
        CustomGame = 1 << 0,
        CustomizedVanillaTrail = 1 << 1,
        CustomizedCustomTrail = 1 << 2,
        CustomizedCoopTrail = 1 << 3,
        CustomizedSandsOfTime = 1 << 4,
        MapEditor = 1 << 5,
    }

    internal readonly struct GameplayModActivationProfile
    {
        internal GameplayModActivationProfile(
            string modGuid,
            string displayName,
            GameplayModAllowedContext allowedContexts)
        {
            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? modGuid : displayName;
            AllowedContexts = allowedContexts;
        }

        internal string ModGuid { get; }
        internal string DisplayName { get; }
        internal GameplayModAllowedContext AllowedContexts { get; }
    }

    /// <summary>Single typed source of truth for mode permissions of regular gameplay mods.</summary>
    internal static class GameplayModModePolicy
    {
        private const GameplayModAllowedContext RegularContexts =
            GameplayModAllowedContext.CustomGame |
            GameplayModAllowedContext.CustomizedVanillaTrail |
            GameplayModAllowedContext.CustomizedCustomTrail |
            GameplayModAllowedContext.CustomizedCoopTrail |
            GameplayModAllowedContext.CustomizedSandsOfTime |
            GameplayModAllowedContext.MapEditor;

        internal static GameplayModActivationProfile GetProfile(string modGuid, string displayName)
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

        internal static bool IsAllowed(
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

        private static GameplayModAllowedContext ResolveContext(GameModeSnapshot snapshot)
        {
            if (snapshot.Kind == GameModeKind.MapEditor)
                return GameplayModAllowedContext.MapEditor;
            if (snapshot.Kind == GameModeKind.CustomGame && !snapshot.IsCustomized)
                return GameplayModAllowedContext.CustomGame;
            if (!snapshot.IsCustomized)
                return GameplayModAllowedContext.None;

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
