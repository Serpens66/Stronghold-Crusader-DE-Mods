using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace Shared
{
    internal enum GameplayFeatureId
    {
        BuildingCostTooltip,
        BuildingLimitEnforcement,
        UnitCostEnforcement,
        UnitLimitEnforcement,
        LordHealthMultipliers,
        AIQuarryPileTowardsKeep,
        EndlessExtremePowersRecharge,
        RandomEventsRuntime,
        ImprovedHunterTargetSelection,
        ImprovedHunterPathfinding,
        CastleSpawning,
        FreeCastlePreview,
        CastleBlueprints,
    }

    internal readonly struct GameplayFeatureActivationProfile
    {
        internal GameplayFeatureActivationProfile(
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

        internal string ModGuid { get; }
        internal GameplayFeatureId FeatureId { get; }
        internal GameplayModAllowedContext AllowedContexts { get; }
        internal bool AllowRealMultiplayer { get; }
    }

    /// <summary>
    /// Typed source of truth for features that intentionally have a narrower
    /// mode contract than their owning gameplay mod.
    /// </summary>
    internal static class GameplayFeatureModePolicy
    {
        private const GameplayModAllowedContext NonEditorGameplayContexts =
            GameplayModAllowedContext.CustomGame |
            GameplayModAllowedContext.CustomizedVanillaTrail |
            GameplayModAllowedContext.CustomizedCustomTrail |
            GameplayModAllowedContext.CustomizedCoopTrail |
            GameplayModAllowedContext.CustomizedSandsOfTime;

        private const GameplayModAllowedContext AllRegularContexts =
            NonEditorGameplayContexts |
            GameplayModAllowedContext.MapEditor;

        private static readonly object LogSync = new object();
        private static readonly Dictionary<GameplayFeatureId, bool> LoggedDecisions =
            new Dictionary<GameplayFeatureId, bool>();

        internal static GameplayFeatureActivationProfile GetProfile(
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
                case GameplayFeatureId.AIQuarryPileTowardsKeep:
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
                    contexts = AllRegularContexts;
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

        internal static bool IsAllowed(
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

        internal static bool IsAllowed(
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

        internal static void LogDecisions(
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

#if SHARED_PRESET_TESTS
        internal static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
            RecordDecision(featureId, allowed);

        internal static void ResetLoggedDecisionsForTests()
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
                    yield return GetProfile(modGuid, GameplayFeatureId.AIQuarryPileTowardsKeep);
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
