using System;

namespace APIShared.GameModes
{
    /// <summary>Recognized contexts that an optional gameplay profile can permit; combine values with bitwise OR.</summary>
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

    /// <summary>Caller-defined optional permissions. Unknown or conflicting contexts fail closed when evaluated.</summary>
    public readonly struct GameplayModActivationProfile
    {
        /// <summary>Caller-defined optional permissions. Unknown or conflicting contexts fail closed when evaluated.</summary>
        public GameplayModActivationProfile(
            string modGuid,
            string displayName,
            GameplayModAllowedContext allowedContexts,
            bool allowRealMultiplayer = true)
        {
            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? modGuid : displayName;
            AllowedContexts = allowedContexts;
            AllowRealMultiplayer = allowRealMultiplayer;
        }

        /// <summary>The consumer BepInEx GUID; no Serps GUID whitelist is applied.</summary>
        public string ModGuid { get; }
        /// <summary>Human-readable name, falling back to the owner GUID.</summary>
        public string DisplayName { get; }
        /// <summary>Contexts explicitly permitted by this caller.</summary>
        public GameplayModAllowedContext AllowedContexts { get; }
        /// <summary>Whether this optional profile permits real multiplayer. Local skirmish is not real multiplayer.</summary>
        public bool AllowRealMultiplayer { get; }
    }

    /// <summary>Optional evaluator for caller-defined gameplay profiles. Construct a profile for any mod GUID; capture and lifecycle do not apply this policy automatically.</summary>
    public static class GameplayModModePolicy
    {
        /// <summary>Evaluates the supplied profile without changing simulation state and returns a diagnostic reason.</summary>
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

            if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
            {
                reason = "profile-does-not-allow-real-multiplayer";
                return false;
            }
            bool allowed = (profile.AllowedContexts & context) == context;
            reason = allowed ? ToReason(context) : "profile-does-not-allow-" + context;
            return allowed;
        }

        /// <summary>Maps an existing mode snapshot to a recognized permission context; unknown contexts return None.</summary>
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
