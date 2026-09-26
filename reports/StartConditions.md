# StartConditions release status

**Status:** code newer

- Release: [v1.0.27](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.27)
- Release commit: [b30c092](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/b30c092997dfbda06efc55cf7fd8b331a5ab572a)
- Current main commit: [d43d3d8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d43d3d831cc5f89e83ff1abdddb547a7fa579867)

## Relevant changed files

- `Shared/DebugLogHelper.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplaySessionLifecycle.cs`
- `Shared/SerpLocalization.cs`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/info.json`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ar.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/cs-CZ.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/de-DE.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/el-GR.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/en-US.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/es-ES.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/fr-FR.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/hu-HU.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/it-IT.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ja-JP.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ko-KR.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/nl-NL.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pl-PL.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pt-BR.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ru-RU.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/sv-SE.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/th-TH.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/tr-TR.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/uk-UA.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/zh-CN.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/zh-HK.txt`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/Override/ScriptExtenderUI/StartConditionsSettings.xaml`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/StartConditions.dll`
- `StartConditions/BepInEx/plugins/StartConditions_Serp/StartConditions.pdb`
- `StartConditions/build.bat`
- `StartConditions/Locales/ar.txt`
- `StartConditions/Locales/cs-CZ.txt`
- `StartConditions/Locales/de-DE.txt`
- `StartConditions/Locales/el-GR.txt`
- `StartConditions/Locales/en-US.txt`
- `StartConditions/Locales/es-ES.txt`
- `StartConditions/Locales/fr-FR.txt`
- `StartConditions/Locales/hu-HU.txt`
- `StartConditions/Locales/it-IT.txt`
- `StartConditions/Locales/ja-JP.txt`
- `StartConditions/Locales/ko-KR.txt`
- `StartConditions/Locales/nl-NL.txt`
- `StartConditions/Locales/pl-PL.txt`
- `StartConditions/Locales/pt-BR.txt`
- `StartConditions/Locales/ru-RU.txt`
- `StartConditions/Locales/sv-SE.txt`
- `StartConditions/Locales/th-TH.txt`
- `StartConditions/Locales/tr-TR.txt`
- `StartConditions/Locales/uk-UA.txt`
- `StartConditions/Locales/zh-CN.txt`
- `StartConditions/Locales/zh-HK.txt`
- `StartConditions/src/AIStartTroopIsolationSaveState.cs`
- `StartConditions/src/RepairFailureLogState.cs`
- `StartConditions/src/StartConditionsBriefingGoldRegistration.cs`
- `StartConditions/src/StartConditionsMapSessionState.cs`
- `StartConditions/src/StartConditionsPlugin.cs`
- `StartConditions/src/StartConditionsRuntime.AIStartTroopIsolation.cs`
- `StartConditions/src/StartConditionsRuntime.cs`
- `StartConditions/src/StartConditionsRuntime.Helpers.cs`
- `StartConditions/src/StartConditionsRuntime.MapLifecycle.cs`
- `StartConditions/src/StartConditionsRuntime.StartResources.cs`
- `StartConditions/src/StartConditionsRuntime.StartTroops.cs`
- `StartConditions/src/StartGoldPolicy.cs`
- `StartConditions/src/StartTroopSpawnCompletionContract.cs`
- `StartConditions/src/VanillaPeaceTimeState.cs`
- `StartConditions/src/VanillaStartTroopSpawnState.cs`
- `StartConditions/StartConditions.csproj`

Relevant localization keys: `Common.Ai`, `Common.Human`, `Common.Preset`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/Shared/DebugLogHelper.cs b/Shared/DebugLogHelper.cs
index fbc4eef0..71f41dd1 100644
--- a/Shared/DebugLogHelper.cs
+++ b/Shared/DebugLogHelper.cs
@@ -27,6 +27,26 @@ namespace Shared
             return debugEnabledCache;
         }
 
+        public static bool IsDiskDebugEnabled()
+        {
+            try
+            {
+                foreach (ILogListener listener in Logger.Listeners)
+                {
+                    if (listener is DiskLogListener diskLogListener &&
+                        HasDebugFlag(diskLogListener.DisplayedLogLevel))
+                    {
+                        return true;
+                    }
+                }
+            }
+            catch
+            {
+            }
+
+            return false;
+        }
+
         public static void LogDebug(ManualLogSource log, params object[] parts)
         {
             if (log == null || !IsDebugEnabled())

diff --git a/Shared/GameModeHelper.cs b/Shared/GameModeHelper.cs
index e1e3dca3..24d03f31 100644
--- a/Shared/GameModeHelper.cs
+++ b/Shared/GameModeHelper.cs
@@ -5,7 +5,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Reflection;
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
 using Steamworks;
 #endif
 
@@ -134,7 +134,7 @@ namespace Shared
                 $"Steam identity {senderSteamId} belongs to final slot {resolution.PlayerId}.");
         }
 
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
         internal static PlayerIdentityResolution CaptureLocalPlayerId(
             bool preferInGameRoster) =>
             CaptureLocalPlayerId(

diff --git a/Shared/GameplayModActivationGate.cs b/Shared/GameplayModActivationGate.cs
index 7e0182e5..102d723c 100644
--- a/Shared/GameplayModActivationGate.cs
+++ b/Shared/GameplayModActivationGate.cs
@@ -1,6 +1,6 @@
 using BepInEx.Logging;
 using System;
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
 using R3;
 using SHCDESE.EventAPI;
 using SHCDESE.EventAPI.MapLoader;
@@ -20,6 +20,7 @@ namespace Shared
         private static GameModeSnapshot snapshot;
         private static volatile bool isAllowed;
         private static bool initialized;
+        private static bool routineLoggingEnabled = true;
         internal static event Action<bool> StateChanged;
 
         internal static bool IsAllowed => isAllowed;
@@ -30,7 +31,8 @@ namespace Shared
             ManualLogSource logger,
             string modGuid,
             string displayName,
-            Func<bool> isConfiguredEnabled)
+            Func<bool> isConfiguredEnabled,
+            bool logRoutineActivity = true)
         {
             if (initialized)
                 return;
@@ -38,6 +40,7 @@ namespace Shared
             log = logger;
             profile = GameplayModModePolicy.GetProfile(modGuid, displayName);
             configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
+            routineLoggingEnabled = logRoutineActivity;
 
             MissionEvents.SetOwner(modGuid);
             MissionEvents.SetGate(e =>
@@ -100,6 +103,9 @@ namespace Shared
         private static void LogTransition(string source, bool policyChanged)
         {
             bool configuredEnabled = ReadConfiguredEnabled();
+            if (!routineLoggingEnabled)
+                return;
+
             bool effectiveEnabled = configuredEnabled && IsAllowed;
             GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
             string action = effectiveEnabled
@@ -128,7 +134,7 @@ namespace Shared
             }
         }
 
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
         internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => Update(next, "test-load");
         internal static void SetStartSnapshotForTests(GameModeSnapshot next) => Update(next, "test-start");

diff --git a/Shared/GameplaySessionLifecycle.cs b/Shared/GameplaySessionLifecycle.cs
index c519e9a2..81aabbad 100644
--- a/Shared/GameplaySessionLifecycle.cs
+++ b/Shared/GameplaySessionLifecycle.cs
@@ -31,7 +31,7 @@ namespace Shared
         private static Action<MissionLifecycleNotification> priority;
         private static MissionLifecycleNotification latest;
         private static string ownerGuid;
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         private static readonly ManualLogSource log = null;
 #else
         private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Mission adapter");
@@ -46,7 +46,7 @@ namespace Shared
         }
         private static void EnsureConnected()
         {
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
             if (capability != null) return;
             string owner = ownerGuid ?? typeof(MissionEvents).Assembly.GetTypes()
                 .SelectMany(t => t.GetCustomAttributes(typeof(BepInPlugin), false).Cast<BepInPlugin>())
@@ -58,7 +58,7 @@ namespace Shared
             { capability = null; throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason); }
 #endif
         }
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         internal static void ResetForTests() { observers.Clear(); latest = null; priority = null; capability = null; }
         internal static void PublishForTests(MissionLifecycleNotification e) => Deliver(e);
 #endif

diff --git a/Shared/SerpLocalization.cs b/Shared/SerpLocalization.cs
index f1f972e6..611b2d4d 100644
--- a/Shared/SerpLocalization.cs
+++ b/Shared/SerpLocalization.cs
@@ -246,6 +279,12 @@ public static class SerpLocalization
     private static string cachedSteamLanguage;
     private static string cachedSteamLanguageSource;
     private static BepInEx.Logging.ManualLogSource localizationLog;
+    private static bool routineLoggingEnabled = true;
+
+    internal static void SetRoutineLoggingEnabled(bool enabled)
+    {
+        routineLoggingEnabled = enabled;
+    }
 
     private static readonly Dictionary<string, string> SteamLanguageLocales =
         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
@@ -331,8 +379,8 @@ public static class SerpLocalization
         { EnableTrailCustomizationButtons, "Customize buttons for Custom and Coop Trails" },
         { EnableTrailCustomizationButtonsHelp, "Shows Customize for Custom Trails and adds it to all four Coop Trails. The host can open the normal skirmish setup before starting the selected mission." },
         { "ExtendedData.HostOptions", "HOST OPTIONS" },
-        { "ExtendedData.SupportedTrailSettings", "MOD SETTINGS IN CUSTOM TRAILS" },
-        { "ExtendedData.SupportedTrailSettingsHelp", "Select which compatible mods are saved with newly created Custom Trail missions. Enabled by default. Unselected mods remain unchanged when the Trail is played." },
+        { "ExtendedData.SupportedTrailSettings", "MOD SETTINGS IN MAPS AND CUSTOM TRAILS" },
+        { "ExtendedData.SupportedTrailSettingsHelp", "Choose whether each host-controlled setting uses the mod default, the normal player/host preset, or a fixed creator value when Maps and Custom Trail missions are saved." },
         { "ExtendedData.IncompatibleTrailMods", "Installed mods with incompatible mod settings:" },
         { "ExtendedData.CompatibilityGuide", "How can mod authors add compatibility?" },
         { "ExtendedData.CompatibilityGuideHelp", "Opens the Extended Data compatibility guide in your browser." },
@@ -376,7 +428,10 @@ public static class SerpLocalization
         { "CastlePlanner.Blueprints", "Blueprints" },
         { "CastlePlanner.BlueprintsHelp", "Displays the selected castle as a local blueprint without changing the game simulation." },
         { "CastlePlanner.SpawnCastle", "Spawn Castle" },
-        { "CastlePlanner.SpawnCastleHelp", "Host setting: spawns the host's selected castle when a new supported singleplayer game starts. Enabling this restores the default castle-content choices." },
+        { "CastlePlanner.SpawnCastleHelp", "Host setting: pauses each new supported skirmish or Trail so every human player can choose Nothing, rotate only their Keep, or preview, rotate, and confirm one free castle. Enabling this restores the default castle-content choices." },
+        { "CastlePlanner.CastleSelectionTimeout", "Castle selection time" },
+        { "CastlePlanner.CastleSelectionTimeoutHelp", "Host setting: real-time limit for choosing a castle at game start, from 60 to 600 seconds." },
+        { "CastlePlanner.CastleSelectionTimeoutValue", "{0} s" },
         { "CastlePlanner.SpawnFortifications", "Also spawn fortifications" },
         { "CastlePlanner.SpawnFortificationsHelp", "Spawns walls, crenels, towers, gates, drawbridges, and stairs from the selected AIV." },
         { "CastlePlanner.SpawnBuildings", "Also spawn buildings" },
@@ -462,6 +517,9 @@ public static class SerpLocalization
         { "BugfixesAndQoL.EnableHostFeatures", "Enable host features" },
         { "BugfixesAndQoL.EnableHostFeaturesHelp", "Enables or disables the host-controlled fixes for the match." },
         { "SomeSettings.NewFeaturesTitle", "New Gameplay Features" },
+        { "SomeSettings.NoKillReward", "No Kill Reward" },
+        { "SomeSettings.NoKillRewardHumanHelp", "Human players receive no gold or goods for defeating an enemy Lord." },
+        { "SomeSettings.NoKillRewardAIHelp", "AI players receive no gold or goods for defeating an enemy Lord." },
         { "SomeSettings.TilesValueFormat", "{0} tiles" },
         { "SomeSettings.PlagueTitle", "Plague" },
         { "ImprovedHunters.BehaviorTitle", "Behavior" },
@@ -651,9 +713,29 @@ public static class SerpLocalization
         { AivPlacementPartial, "Partial fit: {FitPercentage}%, sequential score {SequentialBuildScore}" },
         { AivPlacementImpossible, "Does not fit" },
         { AivPlacementNotEvaluable, "Not evaluable: {Reason}" },
-        { AivPlacementPreBuildUnsupported, "Sequential pre-build placement is not supported yet." },
+        { AivPlacementPreBuildUnsupported, "A prior AI castle was pre-built; its live tile state is not available in the lobby." },
         { AivPlacementHostOnly, "Only the host evaluates AI castle placement." },
-        { AivPlacementChecking, "The best AI castle is still being checked." }
+        { AivPlacementChecking, "The best AI castle is still being checked." },
+        { AivPlacementRotationResults, "Rotations: {Results}" },
+        { AivPlacementAutoSelected, "Vanilla automatic choice: this AIV at {Rotation}" },
+        { AivPlacementAutoDifferent, "Vanilla automatic choice: another AIV" },
+        { AivPlacementAutoUnknown, "Vanilla automatic choice: AIV or rotation cannot be predicted uniquely" },
+        { AivPlacementAutoImpossible, "Vanilla automatic choice: no fitting AIV" },
+        { AivPlacementHighBuildUnknown, "Moat/drawbridge build rule on high ground is unknown." },
+        { AivPlacementHighMoatRisk, "Moat cannot be placed on high ground: {Rotations}." },
+        { AivPlacementHighDrawbridgeRisk, "Drawbridge cannot be placed on high ground: {Rotations}." },
+        { AivPlacementHighBothRisk, "Moat and drawbridge cannot be placed on high ground: {Rotations}." },
+        { AivPlacementPriorKeepRisk, "An earlier Keep may be removed when this AI starts." },
+        { AivPlacementPlannedOverlap, "Planned AIV building areas overlap an earlier AI plan." },
+        { AivPlacementVanillaFit, "Vanilla fit: {Result}" },
+        { AivPlacementPreBuildShort, "cannot be reconstructed exactly after completed castles" },
+        { AivPlacementPracticeRotations, "Practice: {Results}" },
+        { AivPlacementPracticeEstimateRotations, "Practice (geometric estimate): {Results}" },
+        { AivPlacementPracticeUnknown, "Practice: cannot be evaluated" },
+        { AivPlacementSoftOverlap, "Planned walls or moats overlap other building areas." },
+        { AivPlacementUnknownGeometry, "Castle geometry or starting fit is unknown." },
+        { AivPlacementOtherFootprintUnknown, "Another lord's building area is unknown." },
+        { AivPlacementMixedZeroAndPositive, "Some possible rotations have no fitting space." }
     };
 
     public static string Get(string key)
@@ -948,6 +1030,9 @@ public static class SerpLocalization
         string englishPath,
         string localePath)
     {
+        if (!routineLoggingEnabled)
+            return;
+
         try
         {
             if (localizationLog == null)

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/info.json b/StartConditions/BepInEx/plugins/StartConditions_Serp/info.json
index d2324117..104342eb 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/info.json
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/info.json
@@ -3,13 +3,19 @@
   "Author": "Serpens66",
   "Name": "Start Conditions",
   "Description": "Configures start resources and start troops in Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.27",
+  "Version": "1.0.28",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.3.0",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.28",
+      "Changes": [
+        "Delayed additional, removed, and multiplied start troops until 20 seconds after Vanilla Peace Time ends, including Extra Features singleplayer peace time."
+      ]
+    },
     {
       "Version": "1.0.27",
       "Changes": [

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ar.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ar.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ar.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ar.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/cs-CZ.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/cs-CZ.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/cs-CZ.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/cs-CZ.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/de-DE.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/de-DE.txt
index 4645e4c0..d2d70f39 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/de-DE.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/de-DE.txt
@@ -1,6 +1,49 @@
+Common.PresetLoad=Preset laden
+Common.PresetSave=Preset speichern
+Common.PresetBasedOn=Basiert auf
+Common.PresetModified=geändert
+Common.PresetLoadConfirm=Laden
+Common.PresetLoadSelectionHelp=Die Auswahl ändert noch nichts. Erst Laden wendet das Preset an.
+Common.PresetLoadCancel=Abbrechen
+Common.PresetDelete=Löschen
+Common.PresetDeleteTitle=Eigenes Preset löschen
+Common.PresetDeleteConfirm=Das eigene Preset wird endgültig gelöscht. Fortfahren?
+Common.PresetDeleteFailedTitle=Preset konnte nicht gelöscht werden
+Common.PresetSaveTarget=Speichern als
+Common.PresetSaveNew=Neues persönliches Preset
+Common.PresetSourcePersonal=Eigene Presets
+Common.PresetSourceBundled=Mit diesem Mod geliefert
+Common.PresetSourceExternal=Externe Presets
+Common.PresetSaveName=Presetname
+Common.PresetSaveDescription=Beschreibung (optional)
+Common.PresetSaveBulkMode=Alle Modi setzen
+Common.PresetSaveBulkModeHelp=Standard: Mod-Standard verwenden. Spieler: aktuellen Spielerwert behalten. Fest: gespeicherten Wert anwenden. Host fest: Hostwerte festlegen, Spieler-/lokale Werte behalten.
+Common.PresetSaveConfirm=Speichern
+Common.PresetSaveNameRequired=Vor dem Speichern einen Presetnamen eingeben.
+Common.PresetSaveCancel=Abbrechen
+Common.PresetSaveOverwriteTitle=Eigenes Preset überschreiben
+Common.PresetSaveOverwrite=Das gewählte eigene Preset wird vollständig ersetzt. Fortfahren?
+Common.PresetSaveFailedTitle=Preset konnte nicht gespeichert werden
+Common.SettingsSource=Einstellungen zurücksetzen auf
+Common.SettingsSourceLoad=Zurücksetzen
+Common.SettingsSourceDefaults=Mod-Standards
+Common.SettingsSourceTrail=Trail-Einstellungen
+Common.SettingsSourceMap=Map-Einstellungen
+Common.SettingsSourceLoadFailed=Einstellungen konnten nicht zurückgesetzt werden
+Common.PresetConfirm=Bestätigen
+Common.PresetStatusDismiss=Schließen
+Common.PresetModeDefault=Standard
+Common.PresetModePlayer=Spieler
+Common.PresetModeFixed=Fest
+Common.PresetModeHostFixed=Host fest
+Common.PresetModeMixed=Gemischt
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Spieler
+Common.PresetScopeLocal=Lokal
+
 # Serp mod localization
+
 # Format: key=valü
-Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client-Settings)
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unverändert. Erlaubter Bereich: -1 bis
 StartConditions.NormalCrusade=Normal/Kreuzzug
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Startgold setzen (-1 = unverändert)
-StartConditions.SetStartGoldHelp=Setzt die anfängliche Goldmenge für jeden Spieler. -1 bedeutet unverändert.
+StartConditions.SetStartGoldHelp=Ersetzt das effektive Vanilla-Startgold. Fuer Menschen setzt Vanillas Option "Kein Startgold" diese Basis zuvor auf 0. -1 behaelt die effektive Vanilla-Basis bei.
 StartConditions.AddStartGold=Startgold hinzufügen
-StartConditions.AddStartGoldHelp=Fügt jedem Spieler die angegebene Goldmenge zu seiner anfänglichen Goldmenge hinzu.
+StartConditions.AddStartGoldHelp=Eine vorzeichenbehaftete Anpassung nach der effektiven Vanilla-Basis beziehungsweise einem aktiven Set-Wert. Das Ergebnis wird mindestens auf 0 begrenzt.
 StartConditions.MultiplyStartTroops=Starttruppen-Armeen multiplizieren
-StartConditions.StartTroopsMultiplierHelp=Multiplikator: 0 = offizielle Starttruppen nach 20 Sekunden entfernen, 1 = unverändert, 2 = doppelt. Erlaubter Bereich: 0 bis 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplikator fuer Starttruppen-Armeen. Wird nach {DelayedStartTroopCountMilliseconds} ms angewendet, aktuell {DelayedStartTroopCountSeconds} Sekunden nach Kartenstart. 0 = entfernen, 1 = unveraendert, 2 = doppelt. Erlaubter Bereich: 0 bis 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplikator: 0 = offizielle Starttruppen nach der 20-Sekunden-Verzoegerung entfernen, 1 = unveraendert, 2 = doppelt. Eine aktive Vanilla-Waffenruhe verschiebt den Beginn dieser Verzoegerung bis zu ihrem Ende. Erlaubter Bereich: 0 bis 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplikator fuer Starttruppen-Armeen. Wird {DelayedStartTroopCountMilliseconds} ms (aktuell {DelayedStartTroopCountSeconds} Sekunden) nach Kartenstart oder nach dem Ende einer aktiven Vanilla-Waffenruhe angewendet. 0 = entfernen, 1 = unveraendert, 2 = doppelt. Erlaubter Bereich: 0 bis 100.
 StartConditions.ExtraStartUnitsHelp=Extra-Start-Einheiten: 0 = keine Extra-Einheiten. Erlaubter Bereich: 0 bis 1000.
 
 Common.HostOptions=HOST-OPTIONEN
 Common.ClientOptions=LOKALE CLIENT-OPTIONEN
 Common.HostReadOnly=Werte vom Host – schreibgeschützt
-Common.ResetToDefaultHelp=Setzt die Einstellungen zurück, die du im aktuellen Kontext ändern kannst.
 Common.EnableModHelp=Aktiviert oder deaktiviert diese Mod für die Partie.
 Common.PresetHelp=Wählt ein gespeichertes Preset. Clients ändern damit nur ihre persönlichen Einstellungen.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset und Zurücksetzen betreffen Host-Einstellungen und deine lokalen Client-Optionen.
-Common.ActionsScopeClient=Preset und Zurücksetzen betreffen nur deine lokalen Client-Optionen.
+Common.ActionsScopeHost=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft Host-Einstellungen und deine lokalen Client-Optionen.
+Common.ActionsScopeClient=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft nur deine lokalen Client-Optionen.
 StartConditions.StartTroopArmiesTitle=Armee-Multiplikator
 StartConditions.ExtraStartUnitsTitle=Zusätzliche Einheiten
 Common.ModSettingsSearchLabel=Suche
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Tooltips durchsuchen
 Common.ModSettingsSearchIncludeToolTipsHelp=Durchsucht zusätzlich die erklärenden Tooltips der Einstellungen.
 Common.ModSettingsSearchClearHelp=Leert den Einstellungsfilter.
 Common.ModSettingsSearchNoResults=Keine passenden Einstellungen gefunden.
+Common.SettingsSourceHelp=Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.
+Common.PresetLoadFailedTitle=Preset konnte nicht geladen werden

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/el-GR.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/el-GR.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/el-GR.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/el-GR.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/en-US.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/en-US.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/en-US.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/en-US.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/es-ES.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/es-ES.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/es-ES.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/es-ES.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/fr-FR.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/fr-FR.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/fr-FR.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/fr-FR.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/hu-HU.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/hu-HU.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/hu-HU.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/hu-HU.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/it-IT.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/it-IT.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/it-IT.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/it-IT.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ja-JP.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ja-JP.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ja-JP.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ja-JP.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ko-KR.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ko-KR.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ko-KR.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ko-KR.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/nl-NL.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/nl-NL.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/nl-NL.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/nl-NL.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pl-PL.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pl-PL.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pl-PL.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pl-PL.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pt-BR.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pt-BR.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pt-BR.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/pt-BR.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ru-RU.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ru-RU.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ru-RU.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/ru-RU.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/sv-SE.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/sv-SE.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/sv-SE.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/sv-SE.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/th-TH.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/th-TH.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/th-TH.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/th-TH.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/tr-TR.txt b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/tr-TR.txt
index 5dd7cab8..23765dd7 100644
--- a/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/tr-TR.txt
+++ b/StartConditions/BepInEx/plugins/StartConditions_Serp/Locales/tr-TR.txt
@@ -1,11 +1,54 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Ai=AI
 Common.Human=Human
 StartConditions.StartGoldTitle=Start Gold
@@ -15,23 +58,22 @@ StartConditions.UnchangedRangeHelp=-1 = unchanged. Allowed range: -1 to 10000.
 StartConditions.NormalCrusade=Normal/Crusade
 StartConditions.Deathmatch=Deathmatch
 StartConditions.SetStartGold=Set start gold (-1 = unchanged)
-StartConditions.SetStartGoldHelp=Sets the initial amount of gold for each player. -1 means unchanged.
+StartConditions.SetStartGoldHelp=Replaces the effective Vanilla starting gold. For humans, Vanilla's "No Starting Gold" option first makes this base 0. -1 keeps the effective Vanilla base.
 StartConditions.AddStartGold=Add start gold
-StartConditions.AddStartGoldHelp=Adds the specified amount of gold to the initial amount for each player.
+StartConditions.AddStartGoldHelp=Applies a signed adjustment after the effective Vanilla base or an active Set value. The result is clamped to at least 0.
 StartConditions.MultiplyStartTroops=Multiply Start Troop armies
-StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after 20 seconds, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
-StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms, currently {DelayedStartTroopCountSeconds} seconds after map start. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierHelp=Multiplier: 0 = remove official start troops after the 20-second processing delay, 1 = unchanged, 2 = double. Active Vanilla Peace Time postpones that delay until it ends. Allowed range: 0 to 100.
+StartConditions.StartTroopsMultiplierToolTip=Multiplier for Start Troop armies. Applied after {DelayedStartTroopCountMilliseconds} ms (currently {DelayedStartTroopCountSeconds} seconds) following map start, or following the end of an active Vanilla Peace Time. 0 = remove, 1 = unchanged, 2 = double. Allowed range: 0 to 100.
 StartConditions.ExtraStartUnitsHelp=Extra Start Units: 0 = no extra units. Allowed range: 0 to 1000.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 StartConditions.StartTroopArmiesTitle=Start-troop Armies
 StartConditions.ExtraStartUnitsTitle=Additional Start Units
 Common.ModSettingsSearchLabel=Search
@@ -41,3 +83,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/StartConditions.diff).
