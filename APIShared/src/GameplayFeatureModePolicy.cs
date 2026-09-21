using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace Shared
{
    /// <summary>GameplayFeatureId in the centralized mission policy contract.</summary>
    public enum GameplayFeatureId
    {
        /// <summary>BuildingCostTooltip.</summary>
        BuildingCostTooltip,
        /// <summary>BuildingLimitEnforcement.</summary>
        BuildingLimitEnforcement,
        /// <summary>UnitCostEnforcement.</summary>
        UnitCostEnforcement,
        /// <summary>UnitLimitEnforcement.</summary>
        UnitLimitEnforcement,
        /// <summary>LordHealthMultipliers.</summary>
        LordHealthMultipliers,
        /// <summary>EndlessExtremePowersRecharge.</summary>
        EndlessExtremePowersRecharge,
        /// <summary>RandomEventsRuntime.</summary>
        RandomEventsRuntime,
        /// <summary>ImprovedHunterTargetSelection.</summary>
        ImprovedHunterTargetSelection,
        /// <summary>ImprovedHunterPathfinding.</summary>
        ImprovedHunterPathfinding,
        /// <summary>CastleSpawning.</summary>
        CastleSpawning,
        /// <summary>FreeCastlePreview.</summary>
        FreeCastlePreview,
        /// <summary>CastleBlueprints.</summary>
        CastleBlueprints,
    }

    /// <summary>GameplayFeatureActivationProfile in the centralized mission policy contract.</summary>
    public readonly struct GameplayFeatureActivationProfile
    {
        /// <summary>GameplayFeatureActivationProfile in the centralized mission policy contract.</summary>
        public GameplayFeatureActivationProfile(
            string modGuid,
            GameplayFeatureId featureId,
            GameplayModAllowedContext allowedContexts,
            bool allowRealMultiplayer)
        {
            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
            FeatureId = featureId;
            AllowedContexts = allowedContexts;
            AllowRealMultiplayer = allowRealMultiplayer;
        }

        /// <summary>ModGuid in the centralized mission policy contract.</summary>
        public string ModGuid { get; }
        /// <summary>FeatureId in the centralized mission policy contract.</summary>
        public GameplayFeatureId FeatureId { get; }
        /// <summary>AllowedContexts in the centralized mission policy contract.</summary>
        public GameplayModAllowedContext AllowedContexts { get; }
        /// <summary>AllowRealMultiplayer in the centralized mission policy contract.</summary>
        public bool AllowRealMultiplayer { get; }
    }

    /// <summary>
    /// Typed source of truth for features that intentionally have a narrower
    /// mode contract than their owning gameplay mod.
    /// </summary>
    public static class GameplayFeatureModePolicy
    {
        private const GameplayModAllowedContext NonEditorGameplayContexts =
            GameplayModAllowedContext.CustomGame |
            GameplayModAllowedContext.CustomizedVanillaTrail |
            GameplayModAllowedContext.CustomizedCustomTrail |
            GameplayModAllowedContext.CustomizedCoopTrail |
            GameplayModAllowedContext.CustomizedSandsOfTime;

        private const GameplayModAllowedContext AllRecognizedContexts =
            NonEditorGameplayContexts |
            GameplayModAllowedContext.MapEditor |
            GameplayModAllowedContext.Campaign |
            GameplayModAllowedContext.StandaloneMission |
            GameplayModAllowedContext.VanillaTrail |
            GameplayModAllowedContext.CustomTrail |
            GameplayModAllowedContext.CoopTrail |
            GameplayModAllowedContext.SandsOfTime;

        private static readonly object LogSync = new object();
        private static readonly Dictionary<GameplayFeatureId, bool> LoggedDecisions =
            new Dictionary<GameplayFeatureId, bool>();

        /// <summary>GetProfile in the centralized mission policy contract.</summary>
        public static GameplayFeatureActivationProfile GetProfile(
            string modGuid,
            GameplayFeatureId featureId)
        {
            string expectedGuid;
            GameplayModAllowedContext contexts;
            bool allowRealMultiplayer = true;

            switch (featureId)
            {
                case GameplayFeatureId.BuildingCostTooltip:
                    expectedGuid = "BuildingCosts_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.BuildingLimitEnforcement:
                    expectedGuid = "BuildingLimit_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.UnitCostEnforcement:
                    expectedGuid = "UnitCosts_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.UnitLimitEnforcement:
                    expectedGuid = "UnitLimit_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.LordHealthMultipliers:
                    expectedGuid = "ExtraFeatures_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.EndlessExtremePowersRecharge:
                    expectedGuid = "CheatMod_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.RandomEventsRuntime:
                    expectedGuid = "RandomEvents_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.ImprovedHunterTargetSelection:
                case GameplayFeatureId.ImprovedHunterPathfinding:
                    expectedGuid = "ImprovedHunters_Serp";
                    contexts = NonEditorGameplayContexts;
                    allowRealMultiplayer = false;
                    break;
                case GameplayFeatureId.CastleSpawning:
                case GameplayFeatureId.FreeCastlePreview:
                    expectedGuid = "CastlePlanner_Serp";
                    contexts = NonEditorGameplayContexts;
                    break;
                case GameplayFeatureId.CastleBlueprints:
                    expectedGuid = "CastlePlanner_Serp";
                    contexts = AllRecognizedContexts;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(featureId), featureId, "Unknown gameplay feature ID.");
            }

            if (!string.Equals(modGuid, expectedGuid, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modGuid),
                    modGuid,
                    $"Feature {featureId} belongs to mod GUID {expectedGuid}.");
            }

            return new GameplayFeatureActivationProfile(
                expectedGuid,
                featureId,
                contexts,
                allowRealMultiplayer);
        }

        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
        public static bool IsAllowed(
            string modGuid,
            GameplayFeatureId featureId,
            GameModeSnapshot snapshot)
        {
            try
            {
                return IsAllowed(GetProfile(modGuid, featureId), snapshot, out _);
            }
            catch (ArgumentOutOfRangeException)
            {
                // A bad GUID/feature pair is a programming or versioning error;
                // gameplay hooks must still leave Vanilla unchanged.
                return false;
            }
        }

        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
        public static bool IsAllowed(
            GameplayFeatureActivationProfile profile,
            GameModeSnapshot snapshot,
            out string reason)
        {
            if (snapshot.HasConflictingCustomizedOrigin)
            {
                reason = "conflicting-customize-origin";
                return false;
            }

            GameplayModAllowedContext context = GameplayModModePolicy.ResolveContext(snapshot);
            if (context == GameplayModAllowedContext.None)
            {
                reason = snapshot.Kind == GameModeKind.Unknown
                    ? "unknown-fail-closed"
                    : "owning-mod-context-not-allowed";
                return false;
            }

            if ((profile.AllowedContexts & context) != context)
            {
                reason = context == GameplayModAllowedContext.MapEditor
                    ? "feature-not-supported-in-map-editor"
                    : "feature-context-not-allowed";
                return false;
            }

            if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
            {
                reason = "feature-not-approved-for-real-multiplayer";
                return false;
            }

            reason = "feature-context-allowed";
            return true;
        }

        /// <summary>LogDecisions in the centralized mission policy contract.</summary>
        public static void LogDecisions(
            ManualLogSource log,
            string modGuid,
            GameModeSnapshot snapshot,
            string source)
        {
            foreach (GameplayFeatureActivationProfile feature in GetProfiles(modGuid))
            {
                bool allowed = IsAllowed(feature, snapshot, out string reason);
                if (!RecordDecision(feature.FeatureId, allowed))
                    continue;

                DebugLogHelper.LogInfo(
                    log,
                    $"[{modGuid}] gameplay-feature gate: feature={feature.FeatureId}, source={source}, " +
                    $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
                    $"realMultiplayer={snapshot.IsRealMultiplayer}, modeAllowed={allowed}, " +
                    $"action={(allowed ? "enabled" : "disabled-by-feature-mode")}, reason={reason}.");
            }
        }

        private static bool RecordDecision(GameplayFeatureId featureId, bool allowed)
        {
            lock (LogSync)
            {
                bool changed = !LoggedDecisions.TryGetValue(featureId, out bool previous) ||
                    previous != allowed;
                LoggedDecisions[featureId] = allowed;
                return changed;
            }
        }

#if API_SHARED_PRESET_TESTS
        /// <summary>RecordDecisionForTests in the centralized mission policy contract.</summary>
        public static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
            RecordDecision(featureId, allowed);

        /// <summary>ResetLoggedDecisionsForTests in the centralized mission policy contract.</summary>
        public static void ResetLoggedDecisionsForTests()
        {
            lock (LogSync)
                LoggedDecisions.Clear();
        }
#endif

        private static IEnumerable<GameplayFeatureActivationProfile> GetProfiles(string modGuid)
        {
            switch (modGuid)
            {
                case "BuildingCosts_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingCostTooltip);
                    break;
                case "BuildingLimit_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingLimitEnforcement);
                    break;
                case "UnitCosts_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.UnitCostEnforcement);
                    break;
                case "UnitLimit_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.UnitLimitEnforcement);
                    break;
                case "ExtraFeatures_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.LordHealthMultipliers);
                    break;
                case "CheatMod_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.EndlessExtremePowersRecharge);
                    break;
                case "RandomEvents_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.RandomEventsRuntime);
                    break;
                case "ImprovedHunters_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection);
                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding);
                    break;
                case "CastlePlanner_Serp":
                    yield return GetProfile(modGuid, GameplayFeatureId.CastleSpawning);
                    yield return GetProfile(modGuid, GameplayFeatureId.FreeCastlePreview);
                    yield return GetProfile(modGuid, GameplayFeatureId.CastleBlueprints);
                    break;
            }
        }
    }
}
