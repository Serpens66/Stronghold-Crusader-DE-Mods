# UnitLimit release status

**Status:** code newer

- Release: [v1.0.101](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.101)
- Release commit: [4800d86](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4800d86ec2a9003929a52704c8188a01bb0b2409)
- Current main commit: [2c95ad4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2c95ad4c3cefecb00bdadc503704e56a025a37f7)

## Relevant changed files

- `APIShared`
- `Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs`
- `Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs`
- `Shared/Adapters/APIShared/GameplayModActivationGate.cs`
- `Shared/Adapters/APIShared/MissionEventsAdapter.cs`
- `Shared/Adapters/APIShared/PlayerIdentityHelper.cs`
- `Shared/Adapters/APIShared/SerpsModProfiles.cs`
- `Shared/Runtime/Diagnostics/CrashBreadcrumbDiagnostics.cs`
- `Shared/Runtime/Diagnostics/CrashBreadcrumbRecorder.cs`
- `Shared/Runtime/Diagnostics/DebugLogHelper.cs`
- `Shared/Runtime/Localization/SerpLocalization.cs`
- `Shared/Runtime/UI/ToolTipPresentation.cs`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Override/ScriptExtenderUI/UnitLimitSettings.xaml`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/UnitLimit.dll`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/UnitLimit.pdb`
- `UnitLimit/build.bat`
- `UnitLimit/src/ActiveUnitCache.cs`
- `UnitLimit/src/CreateTroopHoverHook.cs`
- `UnitLimit/src/MakeTroopGameActionHook.cs`
- `UnitLimit/src/RecruitmentAvailabilityUiHook.cs`
- `UnitLimit/src/SiegeBuildHoverHook.cs`
- `UnitLimit/src/UnitLimitLobbyViewModel.cs`
- `UnitLimit/src/UnitLimitPlugin.cs`
- `UnitLimit/src/UnitLimitRuntime.Helpers.cs`
- `UnitLimit/src/UnitLimitRuntime.UnitLimits.cs`
- `UnitLimit/UnitLimit.csproj`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs b/Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs
new file mode 100644
index 000000000..1c9450b2a
--- /dev/null
+++ b/Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs
@@ -0,0 +1,71 @@
+using APIShared.ModSettings;
+using APIShared.GameModes;
+using System;
+
+namespace Shared
+{
+    /// <summary>Serps gameplay-policy notice; APIShared only supplies the reusable presentation contract.</summary>
+    internal static class DirectLaunchSettingsNotice
+    {
+        internal static void Configure(PresetLobbyModSettingsViewModel settings, string modGuid, bool blueprintsRemainAvailable = false)
+        {
+            if (settings == null) throw new ArgumentNullException(nameof(settings));
+            settings.System_ModeAvailability.ConfigureDefault(SerpsModProfiles.GetProfile(modGuid, modGuid));
+            ConfigureFeatureRules(settings.System_ModeAvailability, modGuid);
+            settings.System_ConfigureDirectLaunchNotice(() => ResolveText(blueprintsRemainAvailable) + " " + settings.System_ModeNoticeText);
+        }
+
+        private static void ConfigureFeatureRules(ModSettingsModeAvailability availability, string modGuid)
+        {
+            GameplayFeatureId? defaultFeature = null;
+            switch (modGuid)
+            {
+                case "BuildingLimit_Serp": defaultFeature = GameplayFeatureId.BuildingLimitEnforcement; break;
+                case "UnitCosts_Serp": defaultFeature = GameplayFeatureId.UnitCostEnforcement; break;
+                case "UnitLimit_Serp": defaultFeature = GameplayFeatureId.UnitLimitEnforcement; break;
+                case "CheatMod_Serp": defaultFeature = GameplayFeatureId.EndlessExtremePowersRecharge; break;
+                case "RandomEvents_Serp": defaultFeature = GameplayFeatureId.RandomEventsRuntime; break;
+            }
+            if (defaultFeature.HasValue) availability.ConfigureDefault(ToSharedProfile(modGuid, defaultFeature.Value));
+            if (modGuid == "ExtraFeatures_Serp")
+            {
+                availability.Configure("extra.lord-health", ToSharedProfile(modGuid, GameplayFeatureId.LordHealthMultipliers));
+                // These settings are intentionally separate values for the two network contexts.
+                availability.ConfigureNetworkVariant("extra.human-enemy-proximity-singleplayer", false);
+                availability.ConfigureNetworkVariant("extra.ai-enemy-proximity-singleplayer", false);
+                availability.ConfigureNetworkVariant("extra.human-enemy-proximity-multiplayer", true);
+                availability.ConfigureNetworkVariant("extra.ai-enemy-proximity-multiplayer", true);
+            }
+            if (modGuid == "ImprovedHunters_Serp")
+            {
+                availability.Configure("hunters.improved-target-selection", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection));
+                availability.Configure("hunters.improved-pathfinding", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding));
+                availability.Configure("hunters.allow-dead-targets", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection));
+            }
+            if (modGuid == "CastlePlanner_Serp")
+            {
+                availability.ConfigureDefault(ToSharedProfile(modGuid, GameplayFeatureId.CastleSpawning));
+                availability.Configure("castle-planner.blueprints", ToSharedProfile(modGuid, GameplayFeatureId.CastleBlueprints));
+                availability.Configure("castle-planner.hotkey", null);
+            }
+        }
+
+        private static GameplayModActivationProfile ToSharedProfile(string modGuid, GameplayFeatureId featureId)
+        {
+            var feature = GameplayFeatureModePolicy.GetProfile(modGuid, featureId);
+            return new GameplayModActivationProfile(modGuid, featureId.ToString(), feature.AllowedContexts, feature.AllowRealMultiplayer);
+        }
+
+        private static string ResolveText(bool blueprintsRemainAvailable)
+        {
+            bool german = SerpLocalization.GetActiveLocale().StartsWith("de", StringComparison.OrdinalIgnoreCase);
+            if (blueprintsRemainAvailable)
+                return german
+                    ? "Burgplatzierung und Spieländerungen sind für diesen direkten Start inaktiv; Blaupausen bleiben verfügbar. Über „Customize“ starten, um die Spieländerungen zu nutzen. Änderungen hier werden gespeichert."
+                    : "Castle spawning and gameplay changes are inactive for this direct start. Blueprints remain available. Use Customize to play with these changes; edits here are saved for later games.";
+            return german
+                ? "Die Spieländerungen dieser Mod sind für diesen direkten Start inaktiv. Über „Customize“ starten, um damit zu spielen. Änderungen hier werden für spätere Partien gespeichert."
+                : "This mod's gameplay changes are inactive for this direct start. Use Customize to play with them; edits here are saved for later games.";
+        }
+    }
+}

diff --git a/Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs b/Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs
new file mode 100644
index 000000000..2fcfbd9a8
--- /dev/null
+++ b/Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs
@@ -0,0 +1,288 @@
+using APIShared.GameModes;
+using BepInEx.Logging;
+using System;
+using System.Collections.Generic;
+
+namespace Shared
+{
+    /// <summary>GameplayFeatureId in the centralized mission policy contract.</summary>
+    internal enum GameplayFeatureId
+    {
+        /// <summary>BuildingCostTooltip.</summary>
+        BuildingCostTooltip,
+        /// <summary>BuildingLimitEnforcement.</summary>
+        BuildingLimitEnforcement,
+        /// <summary>UnitCostEnforcement.</summary>
+        UnitCostEnforcement,
+        /// <summary>UnitLimitEnforcement.</summary>
+        UnitLimitEnforcement,
+        /// <summary>LordHealthMultipliers.</summary>
+        LordHealthMultipliers,
+        /// <summary>EndlessExtremePowersRecharge.</summary>
+        EndlessExtremePowersRecharge,
+        /// <summary>RandomEventsRuntime.</summary>
+        RandomEventsRuntime,
+        /// <summary>ImprovedHunterTargetSelection.</summary>
+        ImprovedHunterTargetSelection,
+        /// <summary>ImprovedHunterPathfinding.</summary>
+        ImprovedHunterPathfinding,
+        /// <summary>CastleSpawning.</summary>
+        CastleSpawning,
+        /// <summary>FreeCastlePreview.</summary>
+        FreeCastlePreview,
+        /// <summary>CastleBlueprints.</summary>
+        CastleBlueprints,
+    }
+
+    /// <summary>GameplayFeatureActivationProfile in the centralized mission policy contract.</summary>
+    internal readonly struct GameplayFeatureActivationProfile
+    {
+        /// <summary>GameplayFeatureActivationProfile in the centralized mission policy contract.</summary>
+        public GameplayFeatureActivationProfile(
+            string modGuid,
+            GameplayFeatureId featureId,
+            GameplayModAllowedContext allowedContexts,
+            bool allowRealMultiplayer)
+        {
+            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
+            FeatureId = featureId;
+            AllowedContexts = allowedContexts;
+            AllowRealMultiplayer = allowRealMultiplayer;
+        }
+
+        /// <summary>ModGuid in the centralized mission policy contract.</summary>
+        public string ModGuid { get; }
+        /// <summary>FeatureId in the centralized mission policy contract.</summary>
+        public GameplayFeatureId FeatureId { get; }
+        /// <summary>AllowedContexts in the centralized mission policy contract.</summary>
+        public GameplayModAllowedContext AllowedContexts { get; }
+        /// <summary>AllowRealMultiplayer in the centralized mission policy contract.</summary>
+        public bool AllowRealMultiplayer { get; }
+    }
+
+    /// <summary>
+    /// Typed source of truth for features that intentionally have a narrower
+    /// mode contract than their owning gameplay mod.
+    /// </summary>
+    internal static class GameplayFeatureModePolicy
+    {
+        private const GameplayModAllowedContext NonEditorGameplayContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime;
+
+        private const GameplayModAllowedContext AllRecognizedContexts =
+            NonEditorGameplayContexts |
+            GameplayModAllowedContext.MapEditor |
+            GameplayModAllowedContext.Campaign |
+            GameplayModAllowedContext.StandaloneMission |
+            GameplayModAllowedContext.VanillaTrail |
+            GameplayModAllowedContext.CustomTrail |
+            GameplayModAllowedContext.CoopTrail |
+            GameplayModAllowedContext.SandsOfTime;
+
+        private static readonly object LogSync = new object();
+        private static readonly Dictionary<GameplayFeatureId, bool> LoggedDecisions =
+            new Dictionary<GameplayFeatureId, bool>();
+
+        /// <summary>GetProfile in the centralized mission policy contract.</summary>
+        public static GameplayFeatureActivationProfile GetProfile(
+            string modGuid,
+            GameplayFeatureId featureId)
+        {
+            string expectedGuid;
+            GameplayModAllowedContext contexts;
+            bool allowRealMultiplayer = true;
+
+            switch (featureId)
+            {
+                case GameplayFeatureId.BuildingCostTooltip:
+                    expectedGuid = "BuildingCosts_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.BuildingLimitEnforcement:
+                    expectedGuid = "BuildingLimit_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.UnitCostEnforcement:
+                    expectedGuid = "UnitCosts_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.UnitLimitEnforcement:
+                    expectedGuid = "UnitLimit_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.LordHealthMultipliers:
+                    expectedGuid = "ExtraFeatures_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.EndlessExtremePowersRecharge:
+                    expectedGuid = "CheatMod_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.RandomEventsRuntime:
+                    expectedGuid = "RandomEvents_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.ImprovedHunterTargetSelection:
+                case GameplayFeatureId.ImprovedHunterPathfinding:
+                    expectedGuid = "ImprovedHunters_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    allowRealMultiplayer = false;
+                    break;
+                case GameplayFeatureId.CastleSpawning:
+                case GameplayFeatureId.FreeCastlePreview:
+                    expectedGuid = "CastlePlanner_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.CastleBlueprints:
+                    expectedGuid = "CastlePlanner_Serp";
+                    contexts = AllRecognizedContexts;
+                    break;
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(featureId), featureId, "Unknown gameplay feature ID.");
+            }
+
+            if (!string.Equals(modGuid, expectedGuid, StringComparison.Ordinal))
+            {
+                throw new ArgumentOutOfRangeException(
+                    nameof(modGuid),
+                    modGuid,
+                    $"Feature {featureId} belongs to mod GUID {expectedGuid}.");
+            }
+
+            return new GameplayFeatureActivationProfile(
+                expectedGuid,
+                featureId,
+                contexts,
+                allowRealMultiplayer);
+        }
+
+        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
+        public static bool IsAllowed(
+            string modGuid,
+            GameplayFeatureId featureId,
+            GameModeSnapshot snapshot)
+        {
+            try
+            {
+                return IsAllowed(GetProfile(modGuid, featureId), snapshot, out _);
+            }
+            catch (ArgumentOutOfRangeException)
+            {
+                // A bad GUID/feature pair is a programming or versioning error;
+                // gameplay hooks must still leave Vanilla unchanged.
+                return false;
+            }
+        }
+
+        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
+        public static bool IsAllowed(
+            GameplayFeatureActivationProfile profile,
+            GameModeSnapshot snapshot,
+            out string reason)
+        {
+            var sharedProfile = new GameplayModActivationProfile(profile.ModGuid, profile.ModGuid,
+                profile.AllowedContexts, profile.AllowRealMultiplayer);
+            bool allowed = GameplayModModePolicy.IsAllowed(sharedProfile, snapshot, out _);
+            // Preserve the established consumer diagnostics; evaluation belongs to APIShared.
+            var context = GameplayModModePolicy.ResolveContext(snapshot);
+            if (snapshot.HasConflictingCustomizedOrigin)
+                reason = "conflicting-customize-origin";
+            else if (context == GameplayModAllowedContext.None)
+                reason = snapshot.Kind == GameModeKind.Unknown ? "unknown-fail-closed" : "owning-mod-context-not-allowed";
+            else if ((profile.AllowedContexts & context) != context)
+                reason = context == GameplayModAllowedContext.MapEditor ? "feature-not-supported-in-map-editor" : "feature-context-not-allowed";
+            else if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
+                reason = "feature-not-approved-for-real-multiplayer";
+            else
+                reason = "feature-context-allowed";
+            return allowed;
+        }
+
+        /// <summary>LogDecisions in the centralized mission policy contract.</summary>
+        public static void LogDecisions(
+            ManualLogSource log,
+            string modGuid,
+            GameModeSnapshot snapshot,
+            string source)
+        {
+            foreach (GameplayFeatureActivationProfile feature in GetProfiles(modGuid))
+            {
+                bool allowed = IsAllowed(feature, snapshot, out string reason);
+                if (!RecordDecision(feature.FeatureId, allowed))
+                    continue;
+
+                DebugLogHelper.LogDebug(
+                    log,
+                    $"[{modGuid}] gameplay-feature gate: feature={feature.FeatureId}, source={source}, " +
+                    $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                    $"realMultiplayer={snapshot.IsRealMultiplayer}, modeAllowed={allowed}, " +
+                    $"action={(allowed ? "enabled" : "disabled-by-feature-mode")}, reason={reason}.");
+            }
+        }
+
+        private static bool RecordDecision(GameplayFeatureId featureId, bool allowed)
+        {
+            lock (LogSync)
+            {
+                bool changed = !LoggedDecisions.TryGetValue(featureId, out bool previous) ||
+                    previous != allowed;
+                LoggedDecisions[featureId] = allowed;
+                return changed;
+            }
+        }
+
+#if API_SHARED_PRESET_TESTS
+        /// <summary>RecordDecisionForTests in the centralized mission policy contract.</summary>
+        public static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
+            RecordDecision(featureId, allowed);
+
+        /// <summary>ResetLoggedDecisionsForTests in the centralized mission policy contract.</summary>
+        public static void ResetLoggedDecisionsForTests()
+        {
+            lock (LogSync)
+                LoggedDecisions.Clear();
+        }
+#endif
+
+        private static IEnumerable<GameplayFeatureActivationProfile> GetProfiles(string modGuid)
+        {
+            switch (modGuid)
+            {
+                case "BuildingCosts_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingCostTooltip);
+                    break;
+                case "BuildingLimit_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingLimitEnforcement);
+                    break;
+                case "UnitCosts_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.UnitCostEnforcement);
+                    break;
+                case "UnitLimit_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.UnitLimitEnforcement);
+                    break;
+                case "ExtraFeatures_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.LordHealthMultipliers);
+                    break;
+                case "CheatMod_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.EndlessExtremePowersRecharge);
+                    break;
+                case "RandomEvents_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.RandomEventsRuntime);
+                    break;
+                case "ImprovedHunters_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection);
+                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding);
+                    break;
+                case "CastlePlanner_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.CastleSpawning);
+                    yield return GetProfile(modGuid, GameplayFeatureId.FreeCastlePreview);
+                    yield return GetProfile(modGuid, GameplayFeatureId.CastleBlueprints);
+                    break;
+            }
+        }
+    }
+}

diff --git a/Shared/Adapters/APIShared/GameplayModActivationGate.cs b/Shared/Adapters/APIShared/GameplayModActivationGate.cs
new file mode 100644
index 000000000..1960fc534
--- /dev/null
+++ b/Shared/Adapters/APIShared/GameplayModActivationGate.cs
@@ -0,0 +1,148 @@
+using APIShared.GameModes;
+using BepInEx.Logging;
+using System;
+#if !API_SHARED_PRESET_TESTS
+using R3;
+using SHCDESE.EventAPI;
+using SHCDESE.EventAPI.MapLoader;
+#endif
+
+namespace Shared
+{
+    /// <summary>
+    /// Caches the current map policy for one mod assembly. Shared sources are linked
+    /// into every mod, so one mod can never accidentally change another mod's state.
+    /// </summary>
+    internal static class GameplayModActivationGate
+    {
+        private static ManualLogSource log;
+        private static GameplayModActivationProfile profile;
+        private static Func<bool> configuredEnabledProvider;
+        private static GameplayModeGate gate;
+        private static GameModeSnapshot snapshot => gate?.Snapshot ?? default;
+        private static bool isAllowed => gate?.IsAllowed ?? false;
+        private static bool initialized;
+        private static bool routineLoggingEnabled = true;
+        internal static event Action<bool> StateChanged;
+
+        internal static bool IsAllowed => isAllowed;
+        internal static GameModeSnapshot Snapshot => snapshot;
+        internal static bool IsEnabled(bool configuredEnabled) => configuredEnabled && IsAllowed;
+
+        internal static void Initialize(
+            ManualLogSource logger,
+            string modGuid,
+            string displayName,
+            Func<bool> isConfiguredEnabled,
+            bool logRoutineActivity = true)
+        {
+            if (initialized)
+                return;
+
+            log = logger;
+            profile = SerpsModProfiles.GetProfile(modGuid, displayName);
+            gate = new GameplayModeGate(profile);
+            configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
+            routineLoggingEnabled = logRoutineActivity;
+
+            MissionEvents.SetOwner(modGuid);
+            MissionEvents.SetGate(e =>
+            {
+                if (e.Kind == APIShared.MissionLifecycleKind.End) Reset("MissionEnd");
+                else Update(e.Context.Mode, $"Mission{e.Kind}({e.Phase}, session={e.Context.SessionId})");
+            });
+            initialized = true;
+            LogTransition("initialization", policyChanged: false);
+        }
+
+        private static void Update(GameModeSnapshot next, string source)
+        {
+            bool previousAllowed = isAllowed;
+            bool changed = next.Kind != snapshot.Kind ||
+                next.LaunchVariant != snapshot.LaunchVariant ||
+                next.CustomizedTrailId != snapshot.CustomizedTrailId ||
+                next.CustomizedMissionId != snapshot.CustomizedMissionId ||
+                next.IsRealMultiplayer != snapshot.IsRealMultiplayer ||
+                next.HasConflictingCustomizedOrigin != snapshot.HasConflictingCustomizedOrigin;
+            gate.Update(next);
+            if (changed)
+                LogTransition(source, previousAllowed != isAllowed);
+            if (previousAllowed != isAllowed)
+                NotifyStateChanged(isAllowed);
+        }
+
+        private static void Reset(string source)
+        {
+            bool changed = snapshot.Kind != GameModeKind.Unknown ||
+                snapshot.LaunchVariant != GameModeLaunchVariant.Standard;
+            bool previousAllowed = isAllowed;
+            gate.Update(default(GameModeSnapshot));
+            if (changed)
+                LogTransition(source, previousAllowed != isAllowed);
+            if (previousAllowed)
+                NotifyStateChanged(false);
+        }
+
+        private static void NotifyStateChanged(bool allowed)
+        {
+            Delegate[] handlers = StateChanged?.GetInvocationList();
+            if (handlers == null)
+                return;
+
+            foreach (Delegate handler in handlers)
+            {
+                try { ((Action<bool>)handler)(allowed); }
+                catch (Exception ex)
+                {
+                    DebugLogHelper.LogError(
+                        log,
+                        $"[{profile.DisplayName}] gameplay-mod gate listener failed closed: {ex}");
+                }
+            }
+        }
+
+        private static void LogTransition(string source, bool policyChanged)
+        {
+            bool configuredEnabled = ReadConfiguredEnabled();
+            if (!routineLoggingEnabled)
+                return;
+
+            bool effectiveEnabled = configuredEnabled && IsAllowed;
+            GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
+            string action = effectiveEnabled
+                ? "enabled"
+                : !IsAllowed ? "disabled-by-mode" : "restriction-lifted-setting-disabled";
+            string message =
+                $"[{profile.DisplayName}] gameplay-mod gate: modGuid={profile.ModGuid}, source={source}, " +
+                $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                $"customized={snapshot.IsCustomized}, customizedOrigin={snapshot.CustomizedOriginKind}, " +
+                $"modeAllowed={IsAllowed}, configuredEnabled={configuredEnabled}, " +
+                $"effectiveEnabled={effectiveEnabled}, action={action}, reason={reason}.";
+            DebugLogHelper.LogDebug(log, message);
+            GameplayFeatureModePolicy.LogDecisions(log, profile.ModGuid, snapshot, source);
+        }
+
+        private static bool ReadConfiguredEnabled()
+        {
+            try { return configuredEnabledProvider?.Invoke() == true; }
+            catch (Exception ex)
+            {
+                DebugLogHelper.LogError(log, $"[{profile.DisplayName}] EnableMod provider failed closed: {ex}");
+                return false;
+            }
+        }
+
+#if API_SHARED_PRESET_TESTS
+        internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
+        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => Update(next, "test-load");
+        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => Update(next, "test-start");
+        internal static void ResetForTests()
+        {
+            profile = SerpsModProfiles.GetProfile("ExtraFeatures_Serp", "Extra Features");
+            if (gate == null) gate = new GameplayModeGate(profile);
+            configuredEnabledProvider = () => true;
+            Reset("test-reset");
+        }
+#endif
+    }
+}

diff --git a/Shared/Adapters/APIShared/MissionEventsAdapter.cs b/Shared/Adapters/APIShared/MissionEventsAdapter.cs
new file mode 100644
index 000000000..ab1eb5f82
--- /dev/null
+++ b/Shared/Adapters/APIShared/MissionEventsAdapter.cs
@@ -0,0 +1,154 @@
+using APIShared.GameModes;
+using APIShared;
+using BepInEx;
+using BepInEx.Logging;
+using R3;
+using System;
+using System.Collections.Generic;
+using System.Linq;
+
+namespace Shared
+{
+    internal enum GameplaySessionStartKind { NewMap, LoadedSave, EditorCreated, EditorLoaded }
+    internal sealed class GameplaySessionStartedContext
+    {
+        internal GameplaySessionStartedContext(MissionLifecycleNotification notification) { Notification = notification; }
+        internal MissionLifecycleNotification Notification { get; }
+        internal GameplaySessionStartKind Kind => (GameplaySessionStartKind)Notification.Context.StartKind;
+        internal GameModeSnapshot Mode => Notification.Context.Mode;
+        internal bool IsLoadedSave => Notification.Context.IsSave;
+        internal bool IsEditor => Notification.Context.IsEditor;
+        internal bool LoadingEditorMap => Kind == GameplaySessionStartKind.EditorLoaded;
+        internal long SessionId => Notification.Context.SessionId;
+        internal bool IsReplay => Notification.IsReplay;
+        internal string SaveFileName => Notification.Context.FilePath;
+    }
+
+    // Per-assembly delivery only. Detection and mode decisions live in APIShared.
+    internal static class MissionEvents
+    {
+        private static readonly List<Action<MissionLifecycleNotification>> observers = new List<Action<MissionLifecycleNotification>>();
+        private static IMissionLifecycleCapability capability;
+        private static Action<MissionLifecycleNotification> priority;
+        private static MissionLifecycleNotification latest;
+        private static string ownerGuid;
+#if API_SHARED_PRESET_TESTS
+        private static readonly ManualLogSource log = null;
+#else
+        private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Mission adapter");
+#endif
+        internal static void SetOwner(string owner) { ownerGuid = owner; }
+        internal static void SetGate(Action<MissionLifecycleNotification> gate)
+        {
+            bool connected = capability != null;
+            priority = gate;
+            EnsureConnected();
+            if (connected && latest != null) gate(latest);
+        }
+        private static void EnsureConnected()
+        {
+#if !API_SHARED_PRESET_TESTS
+            if (capability != null) return;
+            string owner = ownerGuid ?? typeof(MissionEvents).Assembly.GetTypes()
+                .SelectMany(t => t.GetCustomAttributes(typeof(BepInPlugin), false).Cast<BepInPlugin>())
+                .Select(a => a.GUID).First();
+            if (!ApiShared.Current.TryGetMissionLifecycle(owner, out var service, out var diagnostic))
+                throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
+            capability = service;
+            if (!service.TryRegisterObserver("Shared.MissionEvents." + typeof(MissionEvents).Assembly.GetName().Name, Deliver, Deliver, Deliver, out diagnostic))
+            { capability = null; throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason); }
+#endif
+        }
+#if API_SHARED_PRESET_TESTS
+        internal static void ResetForTests() { observers.Clear(); latest = null; priority = null; capability = null; }
+        internal static void PublishForTests(MissionLifecycleNotification e) => Deliver(e);
+#endif
+        private static void Deliver(MissionLifecycleNotification notification)
+        {
+            latest = notification.Kind == MissionLifecycleKind.End ? null : notification;
+            try { priority?.Invoke(notification); }
+            catch (Exception ex) { Report("Mission gate callback failed: ", ex); }
+            foreach (var callback in observers.ToArray())
+            {
+                try { callback(notification); }
+                catch (Exception ex) { Report("Mission subscriber failed: ", ex); }
+            }
+        }
+        private static void Report(string message, Exception exception)
+        {
+            try { DebugLogHelper.LogError(log, message + exception); } catch { }
+        }
+        private static Observable<MissionLifecycleNotification> Observe(Func<MissionLifecycleNotification, bool> predicate, bool replay = false) =>
+            new RelayObservable(predicate, replay);
+        private sealed class RelayObservable : Observable<MissionLifecycleNotification>
+        {
+            private readonly Func<MissionLifecycleNotification, bool> predicate;
+            private readonly bool replay;
+            internal RelayObservable(Func<MissionLifecycleNotification, bool> predicate, bool replay)
+            { this.predicate = predicate; this.replay = replay; }
+            protected override IDisposable SubscribeCore(Observer<MissionLifecycleNotification> observer)
+            {
+                long deliveredStart = 0;
+                Action<MissionLifecycleNotification> callback = e =>
+                {
+                    if (!predicate(e)) return;
+                    if (e.Kind == MissionLifecycleKind.Start)
+                    {
+                        if (deliveredStart == e.Context.SessionId) return;
+                        deliveredStart = e.Context.SessionId;
+                    }
+                    observer.OnNext(e);
+                };
+                observers.Add(callback);
+                try
+                {
+                    EnsureConnected();
+                    if (replay && latest?.Kind == MissionLifecycleKind.Start)
+                        callback(latest.AsReplay());
+                    return new LocalSubscription(callback);
+                }
+                catch { observers.Remove(callback); throw; }
+            }
+        }
+        internal static Observable<MissionLifecycleNotification> Started => Observe(e => e.Kind == MissionLifecycleKind.Start, true);
+        internal static Observable<MissionLifecycleNotification> Ended => Observe(e => e.Kind == MissionLifecycleKind.End);
+        internal static Observable<MissionLifecycleNotification> Initialization => Observe(e => e.Kind == MissionLifecycleKind.Initialization);
+        internal static Observable<MissionLifecycleNotification> NativeStart => Observe(e => e.Kind == MissionLifecycleKind.Initialization &&
+            (e.Phase == MissionInitializationPhase.BeforeNativeStart || e.Phase == MissionInitializationPhase.AfterNativeStart));
+        internal static Observable<MissionLifecycleNotification> Loading => Observe(e => e.Kind == MissionLifecycleKind.Initialization &&
+            (e.Phase == MissionInitializationPhase.BeforeLoad || e.Phase == MissionInitializationPhase.NativeLoaded));
+        internal static Observable<MissionLifecycleNotification> SaveLoading => Loading.Where(e => e.Context.IsSave);
+        private sealed class LocalSubscription : IDisposable
+        {
+            private Action<MissionLifecycleNotification> callback;
+            internal LocalSubscription(Action<MissionLifecycleNotification> callback) { this.callback = callback; }
+            public void Dispose() { observers.Remove(callback); callback = null; }
+        }
+    }
+
+    internal static class GameplaySessionLifecycle
+    {
+        internal static IDisposable SubscribeStarted(ManualLogSource log, Action<GameplaySessionStartedContext> callback,
+            Action onEnded = null)
+        {
+            var start = MissionEvents.Started.Subscribe(e =>
+            {
+                try { callback(new GameplaySessionStartedContext(e)); }
+                catch (Exception ex) { DebugLogHelper.LogError(log, "Mission start subscriber failed: " + ex); }
+            });
+            if (onEnded == null) return start;
+            var end = MissionEvents.Ended.Subscribe(_ =>
+            {
+                try { onEnded?.Invoke(); }
+                catch (Exception ex) { DebugLogHelper.LogError(log, "Mission end subscriber failed: " + ex); }
+            });
+            return new Pair(start, end);
+        }
+        private sealed class Pair : IDisposable
+        {
+            private readonly IDisposable start, end;
+            internal Pair(IDisposable start, IDisposable end) { this.start = start; this.end = end; }
+            public void Dispose() { start.Dispose(); end.Dispose(); }
+        }
+    }
+}

diff --git a/Shared/Adapters/APIShared/PlayerIdentityHelper.cs b/Shared/Adapters/APIShared/PlayerIdentityHelper.cs
new file mode 100644
index 000000000..cec12fa15
--- /dev/null
+++ b/Shared/Adapters/APIShared/PlayerIdentityHelper.cs
@@ -0,0 +1,431 @@
+using APIShared.GameModes;
+using SHCDESE.API;
+using SHCDESE.EventAPI.MapLoader;
+using CrusaderDE;
+using System;
+using System.Collections.Generic;
+using System.Linq;
+using System.Reflection;
+#if !API_SHARED_PRESET_TESTS
+using Steamworks;
+#endif
+
+namespace Shared
+{
+    internal readonly struct PlayerIdentityResolution
+    {
+        internal PlayerIdentityResolution(int playerId, bool isResolved, string error, string diagnostic)
+        {
+            PlayerId = playerId;
+            IsResolved = isResolved;
+            Error = error ?? string.Empty;
+            Diagnostic = diagnostic ?? string.Empty;
+        }
+
+        internal int PlayerId { get; }
+        internal bool IsResolved { get; }
+        internal string Error { get; }
+        internal string Diagnostic { get; }
+    }
+
+    internal static class PlayerIdentityHelper
+    {
+        // Resolve the local player only from sources whose slot semantics are known.
+        // The GameNetworkAPI local-player getter reads the same managed rosters and logs a
+        // warning whenever they are still transitional, so it must not be used as an
+        // additional fallback from persistent lobby observers.
+        private const int FirstPlayerId = 1;
+        private const int LastPlayerId = 8;
+
+        internal static PlayerIdentityResolution ResolveLocalPlayerId(
+            bool realMultiplayer,
+            bool hasInGameHumanRoster,
+            int nativePlayerId,
+            int gameMemberPlayerId,
+            int lobbyPlayerId)
+        {
+            bool nativeValid = IsValidPlayerId(nativePlayerId);
+            bool gameMemberValid = IsValidPlayerId(gameMemberPlayerId);
+            bool lobbyValid = IsValidPlayerId(lobbyPlayerId);
+
+            if (realMultiplayer && hasInGameHumanRoster)
+            {
+                if (nativeValid && gameMemberValid && nativePlayerId != gameMemberPlayerId)
+                {
+                    return Failure(
+                        $"Authoritative local player ID mismatch: native={nativePlayerId}, gameMember={gameMemberPlayerId}, " +
+                        $"lobby={lobbyPlayerId}.");
+                }
+
+                int authoritative = nativeValid ? nativePlayerId : gameMemberPlayerId;
+                if (!IsValidPlayerId(authoritative))
+                {
+                    return Failure(
+                        $"No authoritative local player ID is available in the active multiplayer roster: " +
+                        $"native={nativePlayerId}, gameMember={gameMemberPlayerId}, lobby={lobbyPlayerId}.");
+                }
+                if (lobbyValid && lobbyPlayerId != authoritative)
+                {
+                    return Failure(
+                        $"Final lobby mapping disagrees with the authoritative local player ID: " +
+                        $"authoritative={authoritative}, lobby={lobbyPlayerId}.");
+                }
+
+                return Success(authoritative, string.Empty);
+            }
+
+            if (realMultiplayer)
+            {
+                if (lobbyValid)
+                    return Success(lobbyPlayerId, string.Empty);
+                return Failure($"No local multiplayer player ID is available yet: lobby={lobbyPlayerId}.");
+            }
+
+            if (nativeValid)
+                return Success(nativePlayerId, string.Empty);
+            if (gameMemberValid)
+                return Success(gameMemberPlayerId, string.Empty);
+            if (lobbyValid)
+                return Success(lobbyPlayerId, string.Empty);
+            return Failure("No valid local player ID is available.");
+        }
+
+        internal static PlayerIdentityResolution ResolvePlayerIdForSteamId(
+            ulong steamId,
+            IReadOnlyDictionary<int, ulong> playersById)
+        {
+            if (steamId == 0)
+                return Failure("The requested Steam identity is invalid.");
+
+            var normalized = new Dictionary<int, ulong>();
+            foreach (KeyValuePair<int, ulong> player in
+                playersById ?? new Dictionary<int, ulong>())
+            {
+                if (!TryAddPlayer(normalized, player.Key, player.Value, out string error))
+                    return Failure(error);
+            }
+
+            int[] matches = normalized
+                .Where(player => player.Value == steamId)
+                .Select(player => player.Key)
+                .ToArray();
+            if (matches.Length != 1)
+            {
+                return Failure(
+                    matches.Length == 0
+                        ? $"Steam identity {steamId} is not part of the resolved human roster."
+                        : $"Steam identity {steamId} is assigned to multiple player slots.");
+            }
+            return Success(matches[0], string.Empty);
+        }
+
+        internal static PlayerIdentityResolution ResolveAuthenticatedPerPlayerTarget(
+            ulong senderSteamId,
+            int payloadPlayerId,
+            IReadOnlyDictionary<int, ulong> playersById)
+        {
+            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
+                senderSteamId,
+                playersById);
+            if (!resolution.IsResolved || resolution.PlayerId == payloadPlayerId)
+                return resolution;
+            return Success(
+                resolution.PlayerId,
+                $"The per-player payload claimed slot {payloadPlayerId}, but authenticated " +
+                $"Steam identity {senderSteamId} belongs to final slot {resolution.PlayerId}.");
+        }
+
+#if !API_SHARED_PRESET_TESTS
+        internal static PlayerIdentityResolution CaptureLocalPlayerId(
+            bool preferInGameRoster) =>
+            CaptureLocalPlayerId(
+                GameModeHelper.IsRealMultiplayer(),
+                preferInGameRoster);
+
+        internal static PlayerIdentityResolution CaptureLocalPlayerId(
+            bool realMultiplayer,
+            bool preferInGameRoster)
+        {
+            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
+            ulong localSteamId = 0;
+            try
+            {
+                localSteamId = SteamUser.GetSteamID().m_SteamID;
+            }
+            catch
+            {
+                // Steam can be unavailable during early singleplayer initialization.
+            }
+
+            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
+                .Where(member => member != null && !member.kicked && !member.skirmishAI)
+                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
+            int gameMemberPlayerId = humanGameMembers
+                .Where(member => localSteamId != 0 && member.steamID == localSteamId)
+                .Select(member => member.playerID)
+                .FirstOrDefault();
+
+            int nativePlayerId = 0;
+            try
+            {
+                nativePlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
+            }
+            catch
+            {
+                // Native player resources are not guaranteed to exist in the lobby.
+            }
+
+            int lobbyPlayerId = 0;
+            try
+            {
+                if (localSteamId != 0 && platform?.activeLobby != null)
+                    lobbyPlayerId = platform.activeLobby.getThisPlayerFromSteamID(localSteamId);
+            }
+            catch
+            {
+                // The final lobby mapping can still be under construction.
+            }
+
+            return ResolveLocalPlayerId(
+                realMultiplayer,
+                preferInGameRoster && humanGameMembers.Length > 0,
+                nativePlayerId,
+                gameMemberPlayerId,
+                lobbyPlayerId);
+        }
+
+        internal static PlayerIdentityResolution CapturePlayerIdForSteamId(
+            ulong steamId,
+            bool preferInGameRoster)
+        {
+            if (!TryCaptureHumanRoster(
+                    preferInGameRoster,
+                    out Dictionary<int, ulong> playersById,
+                    out string error,
+                    out string diagnostic))
+                return Failure(error);
+
+            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
+                steamId,
+                playersById);
+            if (!resolution.IsResolved)
+                return resolution;
+
+            if (preferInGameRoster)
+            {
+                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
+                int vanillaPlayerId = 0;
+                int networkLobbyPlayerId = 0;
+                try
+                {
+                    if (lobby != null)
+                        vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
+                }
+                catch
+                {
+                    // The lobby can disappear while the in-game roster remains authoritative.
+                }
+                try
+                {
+                    networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
+                        new CSteamID(steamId));
+                }
+                catch
+                {
+                    // Lobby order is diagnostic-only after the game roster exists.
+                }
+
+                if (IsValidPlayerId(vanillaPlayerId) &&
+                    vanillaPlayerId != resolution.PlayerId)
+                {
+                    return Failure(
+                        $"Final lobby mapping disagrees with the authoritative in-game player slot for " +
+                        $"Steam identity {steamId}: gameMember={resolution.PlayerId}, lobby={vanillaPlayerId}, " +
+                        $"networkLobby={networkLobbyPlayerId}.");
+                }
+                if (IsValidPlayerId(networkLobbyPlayerId) &&
+                    networkLobbyPlayerId != resolution.PlayerId)
+                {
+                    diagnostic =
+                        $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
+                        $"{steamId}: networkLobby={networkLobbyPlayerId}, final={resolution.PlayerId}.";
+                }
+            }
+            return Success(resolution.PlayerId, diagnostic);
+        }
+
+        internal static string CaptureProvisionalPlayerIdDiagnostic(
+            ulong steamId,
+            int finalPlayerId,
+            bool inGame)
+        {
+            if (steamId == 0 || !IsValidPlayerId(finalPlayerId))
+                return string.Empty;
+
+            try
+            {
+                int provisionalPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
+                    new CSteamID(steamId));
+                if (!IsValidPlayerId(provisionalPlayerId) ||
+                    provisionalPlayerId == finalPlayerId)
+                {
+                    return string.Empty;
+                }
+
+                return inGame
+                    ? $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
+                      $"{steamId}: networkLobby={provisionalPlayerId}, final={finalPlayerId}."
+                    : $"Script Extender lobby-order player ID differs from Vanilla's final lobby mapping " +
+                      $"for Steam identity {steamId}: networkLobby={provisionalPlayerId}, " +
+                      $"finalLobby={finalPlayerId}.";
+            }
+            catch
+            {
+                return string.Empty;
+            }
+        }
+
+        internal static bool TryCaptureHumanRoster(
+            bool preferInGameRoster,
+            out Dictionary<int, ulong> playersById,
+            out string error) =>
+            TryCaptureHumanRoster(
+                preferInGameRoster,
+                requireAuthoritativeLobbyRoster: false,
+                out playersById,
+                out error,
+                out _);
+
+        internal static bool TryCaptureHumanRoster(
+            bool preferInGameRoster,
+            out Dictionary<int, ulong> playersById,
+            out string error,
+            out string diagnostic) =>
+            TryCaptureHumanRoster(
+                preferInGameRoster,
+                requireAuthoritativeLobbyRoster: false,
+                out playersById,
+                out error,
+                out diagnostic);
+
+        internal static bool TryCaptureHumanRoster(
+            bool preferInGameRoster,
+            bool requireAuthoritativeLobbyRoster,
+            out Dictionary<int, ulong> playersById,
+            out string error,
+            out string diagnostic)
+        {
+            playersById = new Dictionary<int, ulong>();
+            diagnostic = string.Empty;
+            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
+            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
+                .Where(member => member != null && !member.kicked && !member.skirmishAI)
+                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
+            if (preferInGameRoster)
+            {
+                if (humanGameMembers.Length == 0)
+                {
+                    error = "The active in-game human roster is unavailable.";
+                    return false;
+                }
+                foreach (Platform_Multiplayer.MPGameMember member in humanGameMembers)
+                {
+                    if (!TryAddPlayer(playersById, member.playerID, member.steamID, out error))
+                        return false;
+                }
+                error = string.Empty;
+                return true;
+            }
+
+            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
+            if (lobby?.members == null)
+            {
+                error = "The active human lobby roster is unavailable.";
+                return false;
+            }
+
+            var diagnostics = new List<string>();
+            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
+            {
+                if (member == null || member.dummyToBeKicked ||
+                    (member.SkirmishMember && !member.SkirmishHumanMember))
+                    continue;
+                ulong steamId = member.id.m_SteamID;
+                int vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
+                int networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
+                if (requireAuthoritativeLobbyRoster && !IsValidPlayerId(vanillaPlayerId))
+                {
+                    error =
+                        $"Vanilla has not assigned a final player slot to lobby member {steamId} yet.";
+                    return false;
+                }
+                int playerId = IsValidPlayerId(vanillaPlayerId)
+                    ? vanillaPlayerId
+                    : networkLobbyPlayerId;
+                if (IsValidPlayerId(vanillaPlayerId) &&
+                    IsValidPlayerId(networkLobbyPlayerId) &&
+                    vanillaPlayerId != networkLobbyPlayerId)
+                {
+                    diagnostics.Add(
+                        $"steamId={steamId}: networkLobby={networkLobbyPlayerId}, finalLobby={vanillaPlayerId}");
+                }
+                else if (!IsValidPlayerId(vanillaPlayerId) &&
+                         IsValidPlayerId(networkLobbyPlayerId))
+                {
+                    diagnostics.Add(
+                        $"steamId={steamId}: only provisional networkLobby={networkLobbyPlayerId} is available");
+                }
+                if (!TryAddPlayer(playersById, playerId, steamId, out error))
+                    return false;
+            }
+
+            if (playersById.Count == 0)
+            {
+                error = "The active lobby contains no resolved human players.";
+                return false;
+            }
+            diagnostic = diagnostics.Count == 0
+                ? string.Empty
+                : "Lobby player-ID source differences: " + string.Join("; ", diagnostics) + ".";
+            error = string.Empty;
+            return true;
+        }
+#endif
+
+        private static bool TryAddPlayer(
+            IDictionary<int, ulong> playersById,
+            int playerId,
+            ulong steamId,
+            out string error)
+        {
+            if (!IsValidPlayerId(playerId) || steamId == 0)
+            {
+                error = $"A human player has an invalid final identity: playerId={playerId}, steamId={steamId}.";
+                return false;
+            }
+            if (playersById.TryGetValue(playerId, out ulong existingSteamId) && existingSteamId != steamId)
+            {
+                error = $"Final player slot {playerId} is assigned to multiple Steam identities.";
+                return false;
+            }
+            if (playersById.Any(player => player.Key != playerId && player.Value == steamId))
+            {
+                error = $"Steam identity {steamId} is assigned to multiple final player slots.";
+                return false;
+            }
+            playersById[playerId] = steamId;
+            error = string.Empty;
+            return true;
+        }
+
+        private static PlayerIdentityResolution Success(int playerId, string diagnostic) =>
+            new PlayerIdentityResolution(playerId, true, string.Empty, diagnostic);
+
+        private static PlayerIdentityResolution Failure(string error) =>
+            new PlayerIdentityResolution(0, false, error, string.Empty);
+
+        private static bool IsValidPlayerId(int playerId) =>
+            playerId >= FirstPlayerId && playerId <= LastPlayerId;
+    }
+
+}

diff --git a/Shared/Adapters/APIShared/SerpsModProfiles.cs b/Shared/Adapters/APIShared/SerpsModProfiles.cs
new file mode 100644
index 000000000..ac13dd358
--- /dev/null
+++ b/Shared/Adapters/APIShared/SerpsModProfiles.cs
@@ -0,0 +1,40 @@
+using System;
+using APIShared.GameModes;
+
+namespace Shared
+{
+    /// <summary>Established permissions for Serps mods, separate from the optional general evaluator.</summary>
+    internal static class SerpsModProfiles
+    {
+        private const GameplayModAllowedContext RegularContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime |
+            GameplayModAllowedContext.MapEditor;
+
+        /// <summary>Gets the established Serps gameplay profile. Unknown mod GUIDs are rejected; third-party mods construct their own profile.</summary>
+        public static GameplayModActivationProfile GetProfile(string modGuid, string displayName)
+        {
+            switch (modGuid)
+            {
+                case "BuildingCosts_Serp":
+                case "BuildingLimit_Serp":
+                case "CastlePlanner_Serp":
+                case "CheatMod_Serp":
+                case "ExtraFeatures_Serp":
+                case "ExtremePowers_Serp":
+                case "ImprovedHunters_Serp":
+                case "RandomEvents_Serp":
+                case "StartConditions_Serp":
+                case "UnitCosts_Serp":
+                case "UnitLimit_Serp":
+                    return new GameplayModActivationProfile(modGuid, displayName, RegularContexts);
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(modGuid), modGuid, "Unknown gameplay mod GUID.");
+            }
+        }
+
+    }
+}

diff --git a/Shared/Runtime/Diagnostics/CrashBreadcrumbDiagnostics.cs b/Shared/Runtime/Diagnostics/CrashBreadcrumbDiagnostics.cs
new file mode 100644
index 000000000..8f5ff9b49
--- /dev/null
+++ b/Shared/Runtime/Diagnostics/CrashBreadcrumbDiagnostics.cs
@@ -0,0 +1,65 @@
+using BepInEx.Logging;
+using System;
+using UnityEngine;
+
+namespace Shared
+{
+    internal static class CrashBreadcrumbDiagnostics
+    {
+        private static CrashBreadcrumbRecorder recorder;
+        private static bool initialized;
+
+        internal static bool IsEnabled => recorder?.IsEnabled == true;
+
+        internal static void Initialize(
+            ManualLogSource log,
+            string pluginGuid,
+            string pluginName,
+            string pluginVersion)
+        {
+            if (initialized)
+                return;
+
+            initialized = true;
+            bool enabled = DebugLogHelper.IsDiskDebugEnabled();
+            recorder = new CrashBreadcrumbRecorder(
+                enabled,
+                BepInEx.Paths.BepInExRootPath,
+                pluginGuid,
+                pluginName,
+                pluginVersion,
+                message => DebugLogHelper.LogDebug(log, message));
+            if (!enabled)
+                return;
+
+            Application.quitting += MarkCleanShutdown;
+            DebugLogHelper.LogDebug(
+                log,
+                $"Crash breadcrumb diagnostics enabled: plugin={pluginGuid}, " +
+                "ringCapacity=256, snapshotIntervalSeconds=1, retainedGameStarts=5.");
+        }
+
+        internal static CrashBreadcrumbScope Enter(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0) =>
+            recorder?.Enter(operation, value1, value2, value3, value4) ??
+            default(CrashBreadcrumbScope);
+
+        internal static void Record(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0,
+            int outcome = 0) =>
+            recorder?.Record(operation, value1, value2, value3, value4, outcome);
+
+        internal static bool ShouldLogUnexpected(string signature) =>
+            recorder?.TryRegisterUnexpected(signature) ?? true;
+
+        private static void MarkCleanShutdown() => recorder?.MarkCleanShutdown();
+    }
+}

diff --git a/Shared/Runtime/Diagnostics/CrashBreadcrumbRecorder.cs b/Shared/Runtime/Diagnostics/CrashBreadcrumbRecorder.cs
new file mode 100644
index 000000000..1bb3486ef
--- /dev/null
+++ b/Shared/Runtime/Diagnostics/CrashBreadcrumbRecorder.cs
@@ -0,0 +1,742 @@
+using System;
+using System.Collections.Generic;
+using System.Diagnostics;
+using System.Globalization;
+using System.IO;
+using System.Linq;
+using System.Runtime.InteropServices;
+using System.Text;
+using System.Threading;
+
+namespace Shared
+{
+    internal sealed class CrashBreadcrumbRecorder : IDisposable
+    {
+        private const int RingCapacity = 256;
+        private const int RetainedGameStarts = 5;
+        private const string PersistenceMutexName = @"Local\SerpsModsDiagnostics.Persistence";
+        private readonly object syncRoot = new object();
+        private readonly object snapshotWriteRoot = new object();
+        private readonly BreadcrumbRecord[] ring = new BreadcrumbRecord[RingCapacity];
+        private readonly Dictionary<int, BreadcrumbRecord> activeByThread =
+            new Dictionary<int, BreadcrumbRecord>();
+        private readonly Dictionary<string, CounterState> counters =
+            new Dictionary<string, CounterState>(StringComparer.Ordinal);
+        private readonly HashSet<string> unexpectedSignatures =
+            new HashSet<string>(StringComparer.Ordinal);
+        private readonly Action<string> statusLogger;
+        private readonly string pluginGuid;
+        private readonly string pluginName;
+        private readonly string pluginVersion;
+        private readonly string directory;
+        private readonly string filePrefix;
+        private readonly int processId;
+        private readonly long processStartedUtcTicks;
+        private readonly DateTime startedUtc;
+        private readonly long startedTimestamp;
+        private readonly long summaryIntervalTicks;
+        private readonly Timer timer;
+        private long sequence;
+        private long snapshotSequence;
+        private long lastPersistedBreadcrumbSequence = -1;
+        private long nextSummaryTimestamp;
+        private bool cleanShutdown;
+        private bool persistenceDisabled;
+        private bool persistenceFailureReported;
+        private bool disposed;
+
+        internal CrashBreadcrumbRecorder(
+            bool enabled,
+            string rootDirectory,
+            string pluginGuid,
+            string pluginName,
+            string pluginVersion,
+            Action<string> statusLogger,
+            TimeSpan? snapshotInterval = null,
+            TimeSpan? summaryInterval = null)
+        {
+            IsEnabled = enabled;
+            this.pluginGuid = pluginGuid ?? string.Empty;
+            this.pluginName = pluginName ?? string.Empty;
+            this.pluginVersion = pluginVersion ?? string.Empty;
+            this.statusLogger = statusLogger;
+            using (Process process = Process.GetCurrentProcess())
+            {
+                processId = process.Id;
+                processStartedUtcTicks = process.StartTime.ToUniversalTime().Ticks;
+            }
+            startedUtc = DateTime.UtcNow;
+            startedTimestamp = Stopwatch.GetTimestamp();
+            summaryIntervalTicks = Math.Max(
+                1L,
+                (long)((summaryInterval ?? TimeSpan.FromMinutes(1)).TotalSeconds * Stopwatch.Frequency));
+            nextSummaryTimestamp = startedTimestamp + summaryIntervalTicks;
+
+            if (!enabled)
+                return;
+
+            directory = Path.Combine(rootDirectory ?? string.Empty, "SerpsModsDiagnostics");
+            filePrefix = SanitizeFileName(this.pluginGuid) + "-pid" +
+                processId.ToString(CultureInfo.InvariantCulture) + "-start" +
+                processStartedUtcTicks.ToString(CultureInfo.InvariantCulture);
+            TryPrepareDirectory();
+
+            TimeSpan interval = snapshotInterval ?? TimeSpan.FromSeconds(1);
+            if (interval > TimeSpan.Zero)
+            {
+                timer = new Timer(
+                    _ => TryWriteSnapshot(finalSnapshot: false),
+                    null,
+                    interval,
+                    interval);
+            }
+        }
+
+        internal bool IsEnabled { get; }
+
+        internal CrashBreadcrumbScope Enter(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0)
+        {
+            if (!IsEnabled || disposed)
+                return default(CrashBreadcrumbScope);
+
+            try
+            {
+                int threadId = GetCurrentThreadId();
+                BreadcrumbRecord previous;
+                bool hadPrevious;
+                long entrySequence;
+                lock (syncRoot)
+                {
+                    hadPrevious = activeByThread.TryGetValue(threadId, out previous);
+                    entrySequence = AddRecordLocked(
+                        BreadcrumbKind.Enter,
+                        operation,
+                        threadId,
+                        value1,
+                        value2,
+                        value3,
+                        value4,
+                        outcome: 0);
+                    activeByThread[threadId] = ring[(int)((entrySequence - 1) % RingCapacity)];
+                }
+
+                return new CrashBreadcrumbScope(this, entrySequence, threadId, hadPrevious, previous);
+            }
+            catch
+            {
+                return default(CrashBreadcrumbScope);
+            }
+        }
+
+        internal void Record(
+            string operation,
+            long value1 = 0,
+            long value2 = 0,
+            long value3 = 0,
+            long value4 = 0,
+            int outcome = 0)
+        {
+            if (!IsEnabled || disposed)
+                return;
+
+            try
+            {
+                lock (syncRoot)
+                {
+                    AddRecordLocked(
+                        BreadcrumbKind.Point,
+                        operation,
+                        GetCurrentThreadId(),
+                        value1,
+                        value2,
+                        value3,
+                        value4,
+                        outcome);
+                }
+            }
+            catch
+            {
+            }
+        }
+
+        internal void CompleteScope(
+            long entrySequence,
+            int threadId,
+            bool hadPrevious,
+            BreadcrumbRecord previous,
+            int outcome)
+        {
+            if (!IsEnabled || disposed || entrySequence <= 0)
+                return;
+
+            try
+            {
+                lock (syncRoot)
+                {
+                    if (!activeByThread.TryGetValue(threadId, out BreadcrumbRecord current) ||
+                        current.Sequence != entrySequence)
+                    {
+                        return;
+                    }
+
+                    AddRecordLocked(
+                        BreadcrumbKind.Exit,
+                        current.Operation,
+                        threadId,
+                        current.Value1,
+                        current.Value2,
+                        current.Value3,
+                        current.Value4,
+                        outcome);
+                    if (hadPrevious)
+                        activeByThread[threadId] = previous;
+                    else
+                        activeByThread.Remove(threadId);
+                }
+            }
+            catch
+            {
+            }
+        }
+
+        internal void MarkCleanShutdown()
+        {
+            if (!IsEnabled || disposed)
+                return;
+
+            cleanShutdown = true;
+            TryWriteSnapshot(finalSnapshot: true);
+        }
+
+        internal bool TryRegisterUnexpected(string signature)
+        {
+            if (!IsEnabled || disposed)
+                return true;
+
+            try
+            {
+                lock (syncRoot)
+                {
+                    string normalized = signature ?? string.Empty;
+                    if (!unexpectedSignatures.Add(normalized))
+                        return false;
+
+                    AddRecordLocked(
+                        BreadcrumbKind.Point,
+                        "UnexpectedState",
+                        GetCurrentThreadId(),
+                        normalized.GetHashCode(),
+                        0,
+                        0,
+                        0,
+                        outcome: -1);
+                    return true;
+                }
+            }
+            catch
+            {
+                return true;
+            }
+        }
+
+        internal void WriteSnapshotForTests() => TryWriteSnapshot(finalSnapshot: false);
+
+        internal long SequenceForTests
+        {
+            get
+            {
+                lock (syncRoot)
+                    return sequence;
+            }
+        }
+
+        internal long SnapshotSequenceForTests => Interlocked.Read(ref snapshotSequence);
+
+        internal string DirectoryForTests => directory;
+
+        public void Dispose()
+        {
+            if (disposed)
+                return;
+
+            disposed = true;
+            timer?.Dispose();
+        }
+
+        private long AddRecordLocked(
+            BreadcrumbKind kind,
+            string operation,
+            int threadId,
+            long value1,
+            long value2,
+            long value3,
+            long value4,
+            int outcome)
+        {
+            long next = ++sequence;
+            BreadcrumbRecord record = new BreadcrumbRecord(
+                next,
+                Stopwatch.GetTimestamp(),
+                threadId,
+                kind,
+                operation ?? string.Empty,
+                value1,
+                value2,
+                value3,
+                value4,
+                outcome);
+            ring[(int)((next - 1) % RingCapacity)] = record;
+
+            if (!counters.TryGetValue(record.Operation, out CounterState counter))
+            {
+                counter = new CounterState();
+                counters.Add(record.Operation, counter);
+            }
+            counter.Total++;
+            counter.Interval++;
+            if (outcome < 0)
+            {
+                counter.Failures++;
+                counter.IntervalFailures++;
+            }
+            return next;
+        }
+
+        private void TryPrepareDirectory()
+        {
+            try
+            {
+                Directory.CreateDirectory(directory);
+            }
+            catch (Exception exception)
+            {
+                DisablePersistence(exception);
+            }
+        }
+
+        private void TrimOldGameStarts()
+        {
+            FileInfo[] files = new DirectoryInfo(directory)
+                .GetFiles("*.txt", SearchOption.TopDirectoryOnly);
+            var sessions = files
+                .Select(file => TryGetGameStart(file, out string key, out DateTime startedUtc)
+                    ? new { File = file, Key = key, StartedUtc = startedUtc }
+                    : null)
+                .Where(item => item != null)
+                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
+                .OrderByDescending(group => group.Max(item => item.StartedUtc))
+                .ThenByDescending(group => group.Key, StringComparer.Ordinal)
+                .Skip(RetainedGameStarts)
+                .ToArray();
+
+            foreach (var session in sessions)
+            {
+                foreach (var item in session)
+                    item.File.Delete();
+            }
+        }
+
+        private static bool TryGetGameStart(FileInfo file, out string key, out DateTime startedUtc)
+        {
+            key = null;
+            startedUtc = default(DateTime);
+            string name = Path.GetFileNameWithoutExtension(file.Name);
+            int slotSeparator = name.LastIndexOf('-');
+            if (slotSeparator < 1 || slotSeparator != name.Length - 2 ||
+                (name[name.Length - 1] != '0' && name[name.Length - 1] != '1'))
+            {
+                return false;
+            }
+
+            string prefix = name.Substring(0, slotSeparator);
+            int pidMarker = prefix.LastIndexOf("-pid", StringComparison.OrdinalIgnoreCase);
+            if (pidMarker < 1)
+                return false;
+
+            string identity = prefix.Substring(pidMarker + 4);
+            int startMarker = identity.IndexOf("-start", StringComparison.OrdinalIgnoreCase);
+            string pidText = startMarker < 0 ? identity : identity.Substring(0, startMarker);
+            if (!int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
+                return false;
+
+            if (startMarker < 0)
+            {
+                key = "legacy-pid" + pid.ToString(CultureInfo.InvariantCulture);
+                startedUtc = file.LastWriteTimeUtc;
+                return true;
+            }
+
+            string ticksText = identity.Substring(startMarker + 6);
+            if (!long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) ||
+                ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
+            {
+                return false;
+            }
+
+            key = "pid" + pid.ToString(CultureInfo.InvariantCulture) + "-start" + ticksText;
+            startedUtc = new DateTime(ticks, DateTimeKind.Utc);
+            return true;
+        }
+
+        private void TryWriteSnapshot(bool finalSnapshot)
+        {
+            if (!IsEnabled || persistenceDisabled || disposed && !finalSnapshot)
+                return;
+            if (!finalSnapshot &&
+                Interlocked.Read(ref sequence) == Interlocked.Read(ref lastPersistedBreadcrumbSequence))
+            {
+                return;
+            }
+
+            lock (snapshotWriteRoot)
+            {
+                try
+                {
+                    if (!finalSnapshot &&
+                        Interlocked.Read(ref sequence) == Interlocked.Read(ref lastPersistedBreadcrumbSequence))
+                    {
+                        return;
+                    }
+
+                    Snapshot snapshot = CaptureSnapshot();
+                    string text = FormatSnapshot(snapshot, finalSnapshot);
+                    int slot = (int)(snapshot.SnapshotSequence & 1L);
+                    string path = Path.Combine(directory, filePrefix + "-" + slot + ".txt");
+                    using (Mutex mutex = new Mutex(false, PersistenceMutexName))
+                    {
+                        bool acquired = false;
+                        try
+                        {
+                            try
+                            {
+                                acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
+                            }
+                            catch (AbandonedMutexException)
+                            {
+                                acquired = true;
+                            }
+
+                            if (!acquired)
+                                throw new TimeoutException("Crash diagnostics persistence lock timed out.");
+
+                            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
+                            TrimOldGameStarts();
+                        }
+                        finally
+                        {
+                            if (acquired)
+                                mutex.ReleaseMutex();
+                        }
+                    }
+                    // Publish only the sequence captured in this file. A concurrent writer that
+                    // advances the ring remains dirty and is persisted by the next timer pass.
+                    Interlocked.Exchange(
+                        ref lastPersistedBreadcrumbSequence,
+                        snapshot.BreadcrumbSequence);
+                    TryLogMinuteSummary(snapshot);
+                }
+                catch (Exception exception)
+                {
+                    DisablePersistence(exception);
+                }
+            }
+        }
+
+        private Snapshot CaptureSnapshot()
+        {
+            lock (syncRoot)
+            {
+                long currentSequence = sequence;
+                long first = Math.Max(1, currentSequence - RingCapacity + 1);
+                List<BreadcrumbRecord> records = new List<BreadcrumbRecord>((int)(currentSequence - first + 1));
+                for (long itemSequence = first; itemSequence <= currentSequence; itemSequence++)
+                {
+                    BreadcrumbRecord record = ring[(int)((itemSequence - 1) % RingCapacity)];
+                    if (record.Sequence == itemSequence)
+                        records.Add(record);
+                }
+
+                List<BreadcrumbRecord> active = activeByThread.Values
+                    .OrderBy(record => record.ThreadId)
+                    .ToList();
+                Dictionary<string, CounterSnapshot> counterCopy = counters.ToDictionary(
+                    pair => pair.Key,
+                    pair => new CounterSnapshot(
+                        pair.Value.Total,
+                        pair.Value.Failures,
+                        pair.Value.Interval,
+                        pair.Value.IntervalFailures),
+                    StringComparer.Ordinal);
+                return new Snapshot(
+                    ++snapshotSequence,
+                    currentSequence,
+                    records,
+                    active,
+                    counterCopy,
+                    cleanShutdown,
+                    Stopwatch.GetTimestamp());
+            }
+        }
+
+        private string FormatSnapshot(Snapshot snapshot, bool finalSnapshot)
+        {
+            StringBuilder builder = new StringBuilder(32768);
+            AppendLine(builder, "SERPS_MOD_CRASH_BREADCRUMBS_V1");
+            AppendLine(builder, "pluginGuid=" + Escape(pluginGuid));
+            AppendLine(builder, "pluginName=" + Escape(pluginName));
+            AppendLine(builder, "pluginVersion=" + Escape(pluginVersion));
+            AppendLine(builder, "processId=" + processId.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "sessionStartedUtc=" + startedUtc.ToString("O", CultureInfo.InvariantCulture));
+            AppendLine(builder, "snapshotUtc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
+            AppendLine(builder, "snapshotSequence=" + snapshot.SnapshotSequence.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "breadcrumbSequence=" + snapshot.BreadcrumbSequence.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "state=" + ((snapshot.CleanShutdown || finalSnapshot) ? "clean-shutdown" : "running"));
+            AppendLine(builder, "ringCapacity=" + RingCapacity.ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, "overwritten=" + Math.Max(0, snapshot.BreadcrumbSequence - RingCapacity).ToString(CultureInfo.InvariantCulture));
+            AppendLine(builder, string.Empty);
+            AppendLine(builder, "[active-scopes]");
+            if (snapshot.Active.Count == 0)
+                AppendLine(builder, "none");
+            foreach (BreadcrumbRecord record in snapshot.Active)
+                AppendRecord(builder, record);
+
+            AppendLine(builder, string.Empty);
+            AppendLine(builder, "[counters]");
+            foreach (KeyValuePair<string, CounterSnapshot> pair in snapshot.Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
+            {
+                AppendLine(
+                    builder,
+                    Escape(pair.Key) +
+                    " total=" + pair.Value.Total.ToString(CultureInfo.InvariantCulture) +
+                    " failures=" + pair.Value.Failures.ToString(CultureInfo.InvariantCulture));
+            }
+
+            AppendLine(builder, string.Empty);
+            AppendLine(builder, "[breadcrumbs-oldest-to-newest]");
+            foreach (BreadcrumbRecord record in snapshot.Records)
+                AppendRecord(builder, record);
+            return builder.ToString();
+        }
+
+        private void TryLogMinuteSummary(Snapshot snapshot)
+        {
+            if (snapshot.CapturedTimestamp < nextSummaryTimestamp)
+                return;
+
+            nextSummaryTimestamp = snapshot.CapturedTimestamp + summaryIntervalTicks;
+            string[] parts = snapshot.Counters
+                .Where(pair => pair.Value.Interval > 0)
+                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
+                .Select(pair => Escape(pair.Key) + "=" +
+                    pair.Value.Interval.ToString(CultureInfo.InvariantCulture) +
+                    (pair.Value.IntervalFailures > 0
+                        ? "/fail:" + pair.Value.IntervalFailures.ToString(CultureInfo.InvariantCulture)
+                        : string.Empty))
+                .ToArray();
+            if (parts.Length > 0)
+                statusLogger?.Invoke("Crash diagnostics 60s summary: " + string.Join(", ", parts) + ".");
+
+            lock (syncRoot)
+            {
+                foreach (KeyValuePair<string, CounterSnapshot> pair in snapshot.Counters)
+                {
+                    if (!counters.TryGetValue(pair.Key, out CounterState counter))
+                        continue;
+
+                    counter.Interval = Math.Max(0, counter.Interval - pair.Value.Interval);
+                    counter.IntervalFailures = Math.Max(
+                        0,
+                        counter.IntervalFailures - pair.Value.IntervalFailures);
+                }
+            }
+        }
+
+        private void DisablePersistence(Exception exception)
+        {
+            persistenceDisabled = true;
+            if (persistenceFailureReported)
+                return;
+
+            persistenceFailureReported = true;
+            try
+            {
+                statusLogger?.Invoke("Crash diagnostics persistence disabled for this process: " + exception.Message);
+            }
+            catch
+            {
+            }
+        }
+
+        private void AppendRecord(StringBuilder builder, BreadcrumbRecord record)
+        {
+            double milliseconds = (record.Timestamp - startedTimestamp) * 1000.0 / Stopwatch.Frequency;
+            AppendLine(
+                builder,
+                "seq=" + record.Sequence.ToString(CultureInfo.InvariantCulture) +
+                " ms=" + milliseconds.ToString("F3", CultureInfo.InvariantCulture) +
+                " thread=" + record.ThreadId.ToString(CultureInfo.InvariantCulture) +
+                " kind=" + record.Kind.ToString().ToLowerInvariant() +
+                " operation=" + Escape(record.Operation) +
+                " v1=" + record.Value1.ToString(CultureInfo.InvariantCulture) +
+                " v2=" + record.Value2.ToString(CultureInfo.InvariantCulture) +
+                " v3=" + record.Value3.ToString(CultureInfo.InvariantCulture) +
+                " v4=" + record.Value4.ToString(CultureInfo.InvariantCulture) +
+                " outcome=" + record.Outcome.ToString(CultureInfo.InvariantCulture));
+        }
+
+        private static void AppendLine(StringBuilder builder, string value)
+        {
+            builder.Append(value);
+            builder.Append((char)13);
+            builder.Append((char)10);
+        }
+
+        private static string Escape(string value) =>
+            (value ?? string.Empty)
+                .Replace(((char)13).ToString(), " ")
+                .Replace(((char)10).ToString(), " ")
+                .Replace("=", ":");
+
+        private static string SanitizeFileName(string value)
+        {
+            string result = value ?? string.Empty;
+            foreach (char invalid in Path.GetInvalidFileNameChars())
+                result = result.Replace(invalid, '_');
+            return string.IsNullOrWhiteSpace(result) ? "unknown-mod" : result;
+        }
+
+        [DllImport("kernel32.dll")]
+        private static extern int GetCurrentThreadId();
+
+        internal enum BreadcrumbKind : byte
+        {
+            Enter,
+            Exit,
+            Point
+        }
+
+        internal readonly struct BreadcrumbRecord
+        {
+            internal BreadcrumbRecord(
+                long sequence,
+                long timestamp,
+                int threadId,
+                BreadcrumbKind kind,
+                string operation,
+                long value1,
+                long value2,
+                long value3,
+                long value4,
+                int outcome)
+            {
+                Sequence = sequence;
+                Timestamp = timestamp;
+                ThreadId = threadId;
+                Kind = kind;
+                Operation = operation;
+                Value1 = value1;
+                Value2 = value2;
+                Value3 = value3;
+                Value4 = value4;
+                Outcome = outcome;
+            }
+
+            internal long Sequence { get; }
+            internal long Timestamp { get; }
+            internal int ThreadId { get; }
+            internal BreadcrumbKind Kind { get; }
+            internal string Operation { get; }
+            internal long Value1 { get; }
+            internal long Value2 { get; }
+            internal long Value3 { get; }
+            internal long Value4 { get; }
+            internal int Outcome { get; }
+        }
+
+        private sealed class CounterState
+        {
+            internal long Total;
+            internal long Failures;
+            internal long Interval;
+            internal long IntervalFailures;
+        }
+
+        private readonly struct CounterSnapshot
+        {
+            internal CounterSnapshot(long total, long failures, long interval, long intervalFailures)
+            {
+                Total = total;
+                Failures = failures;
+                Interval = interval;
+                IntervalFailures = intervalFailures;
+            }
+
+            internal long Total { get; }
+            internal long Failures { get; }
+            internal long Interval { get; }
+            internal long IntervalFailures { get; }
+        }
+
+        private sealed class Snapshot
+        {
+            internal Snapshot(
+                long snapshotSequence,
+                long breadcrumbSequence,
+                List<BreadcrumbRecord> records,
+                List<BreadcrumbRecord> active,
+                Dictionary<string, CounterSnapshot> counters,
+                bool cleanShutdown,
+                long capturedTimestamp)
+            {
+                SnapshotSequence = snapshotSequence;
+                BreadcrumbSequence = breadcrumbSequence;
+                Records = records;
+                Active = active;
+                Counters = counters;
+                CleanShutdown = cleanShutdown;
+                CapturedTimestamp = capturedTimestamp;
+            }
+
+            internal long SnapshotSequence { get; }
+            internal long BreadcrumbSequence { get; }
+            internal List<BreadcrumbRecord> Records { get; }
+            internal List<BreadcrumbRecord> Active { get; }
+            internal Dictionary<string, CounterSnapshot> Counters { get; }
+            internal bool CleanShutdown { get; }
+            internal long CapturedTimestamp { get; }
+        }
+    }
+
+    internal readonly struct CrashBreadcrumbScope : IDisposable
+    {
+        private readonly CrashBreadcrumbRecorder recorder;
+        private readonly long entrySequence;
+        private readonly int threadId;
+        private readonly bool hadPrevious;
+        private readonly CrashBreadcrumbRecorder.BreadcrumbRecord previous;
+
+        internal CrashBreadcrumbScope(
+            CrashBreadcrumbRecorder recorder,
+            long entrySequence,
+            int threadId,
+            bool hadPrevious,
+            CrashBreadcrumbRecorder.BreadcrumbRecord previous)
+        {
+            this.recorder = recorder;
+            this.entrySequence = entrySequence;
+            this.threadId = threadId;
+            this.hadPrevious = hadPrevious;
+            this.previous = previous;
+        }
+
+        public void Complete(int outcome = 0) =>
+            recorder?.CompleteScope(entrySequence, threadId, hadPrevious, previous, outcome);
+
+        public void Dispose() => Complete();
+    }
+}

diff --git a/Shared/Runtime/Diagnostics/DebugLogHelper.cs b/Shared/Runtime/Diagnostics/DebugLogHelper.cs
new file mode 100644
index 000000000..0b144f4bf
--- /dev/null
+++ b/Shared/Runtime/Diagnostics/DebugLogHelper.cs
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/UnitLimit.diff).
