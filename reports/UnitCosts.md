# UnitCosts release status

**Status:** code newer

- Release: [v1.0.29](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.29)
- Release commit: [597b3db](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/597b3dbd1b9e87466ea96ba04aa0485d048c9555)
- Current main commit: [d43d3d8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d43d3d831cc5f89e83ff1abdddb547a7fa579867)

## Relevant changed files

- `Shared/DebugLogHelper.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplaySessionLifecycle.cs`
- `Shared/SerpLocalization.cs`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/UnitCosts.dll`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/UnitCosts.pdb`
- `UnitCosts/Locales/ar.txt`
- `UnitCosts/Locales/cs-CZ.txt`
- `UnitCosts/Locales/de-DE.txt`
- `UnitCosts/Locales/el-GR.txt`
- `UnitCosts/Locales/en-US.txt`
- `UnitCosts/Locales/es-ES.txt`
- `UnitCosts/Locales/fr-FR.txt`
- `UnitCosts/Locales/hu-HU.txt`
- `UnitCosts/Locales/it-IT.txt`
- `UnitCosts/Locales/ja-JP.txt`
- `UnitCosts/Locales/ko-KR.txt`
- `UnitCosts/Locales/nl-NL.txt`
- `UnitCosts/Locales/pl-PL.txt`
- `UnitCosts/Locales/pt-BR.txt`
- `UnitCosts/Locales/ru-RU.txt`
- `UnitCosts/Locales/sv-SE.txt`
- `UnitCosts/Locales/th-TH.txt`
- `UnitCosts/Locales/tr-TR.txt`
- `UnitCosts/Locales/uk-UA.txt`
- `UnitCosts/Locales/zh-CN.txt`
- `UnitCosts/Locales/zh-HK.txt`
- `UnitCosts/src/UnitCostsPlugin.cs`
- `UnitCosts/UnitCosts.csproj`

Relevant localization keys: `Common.Preset`

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

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json
index 9707278b..3c921bd6 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json
@@ -3,13 +3,19 @@
   "Author": "Serpens66",
   "Name": "Unit Costs",
   "Description": "Configure recruit costs in Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.29",
+  "Version": "1.0.30",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.3.0",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.30",
+      "Changes": [
+        "Loads after Crusader DE Tweaker when both mods are installed so Unit Costs settings have defined precedence."
+      ]
+    },
     {
       "Version": "1.0.29",
       "Changes": [

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt
index 08b9bc40..8cb4c82b 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt
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
 # Format: key=value
-Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client-Settings)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Material fehlt
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
 Common.ModSettingsSearchLabel=Suche
 Common.ModSettingsSearchHelp=Durchsucht die Titel der Einstellungen. Optional können Tooltips einbezogen werden.
 Common.ModSettingsSearchToggleHelp=Blendet die Einstellungssuche ein oder aus.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Tooltips durchsuchen
 Common.ModSettingsSearchIncludeToolTipsHelp=Durchsucht zusätzlich die erklärenden Tooltips der Einstellungen.
 Common.ModSettingsSearchClearHelp=Leert den Einstellungsfilter.
 Common.ModSettingsSearchNoResults=Keine passenden Einstellungen gefunden.
+Common.SettingsSourceHelp=Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.
+Common.PresetLoadFailedTitle=Preset konnte nicht geladen werden

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt
index 1ce461ac..9f4467cf 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt
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
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.
 UnitCosts.ExtraTitle=Additional Costs (Human only)
@@ -21,12 +64,11 @@ UnitCosts.ResourcesMissing=Resources missing
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
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -34,3 +76,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml
index 9a328bf6..1e16c237 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml
@@ -1,7 +1,8 @@
 <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
-      xmlns:shared="clr-namespace:Shared;assembly=UnitCosts"
+      xmlns:sys="clr-namespace:System;assembly=mscorlib"
+      xmlns:shared="clr-namespace:Shared;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"
@@ -29,10 +30,76 @@
         <Border Style="{StaticResource ClientActivationBorder}" Visibility="{Binding ClientSettingsActivationVisibility}" Margin="8,0,0,0">
           <CheckBox IsEnabled="{Binding CanToggleClientSettings}" IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}" Content="{Binding ClientActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ClientSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
         </Border>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="145" VerticalAlignment="Center" Margin="14,0,0,0"/>
+        <Button IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" Content="{Binding System_PresetLoadText}" Command="{Binding System_OpenPresetLoadCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Padding="10,3" Margin="14,0,0,0"/>
+        <Button IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" Content="{Binding System_PresetSaveText}" Command="{Binding System_OpenPresetSaveCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveText}" Padding="10,3" Margin="8,0,0,0"/>
         <Button Command="{Binding System_ToggleModSettingsSearchCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_ModSettingsSearchToggleHelpText}" Width="30" Height="28" Padding="5" Margin="8,0,0,0"><Viewbox Width="16" Height="16"><Path Style="{StaticResource ModSettingsSearchIcon}" StrokeThickness="2" Data="M 7,1 A 6,6 0 1 1 6.99,1 M 11.5,11.5 L 16,16"/></Viewbox></Button>
-        <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" HorizontalAlignment="Left" Padding="10,3" Margin="14,0,0,0"/>
       </StackPanel>
+      <TextBlock Text="{Binding System_PresetStatusText}" Visibility="{Binding System_PresetStatusVisibility}" Foreground="#FFF2D48A" FontStyle="Italic" Margin="0,0,0,8" shared:ModSettingsSearch.Exclude="True"/>
+      <Border Visibility="{Binding System_PresetInlineConfirmationVisibility}" BorderBrush="#FFFFCC66" BorderThickness="1" CornerRadius="3" Padding="8" Margin="0,0,0,8" shared:ModSettingsSearch.Exclude="True">
+        <StackPanel><TextBlock Text="{Binding System_PresetInlineConfirmationTitle}" Foreground="#FFFFCC66" FontWeight="Bold"/><TextBlock Text="{Binding System_PresetInlineConfirmationMessage}" Foreground="White" TextWrapping="Wrap" MaxWidth="700" Margin="0,4,0,0"/><StackPanel Orientation="Horizontal" Margin="0,7,0,0"><Button Content="{Binding System_PresetInlineConfirmText}" Command="{Binding System_ConfirmPresetInlineActionCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetInlineConfirmText}" Padding="10,3"/><Button Content="{Binding System_PresetInlineCancelText}" Command="{Binding System_CancelPresetInlineActionCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetInlineCancelText}" Padding="10,3" Margin="8,0,0,0"/></StackPanel></StackPanel>
+      </Border>
+      <Border Visibility="{Binding System_PresetOperationErrorVisibility}" BorderBrush="#FFFF7777" BorderThickness="1" CornerRadius="3" Padding="8" Margin="0,0,0,8" shared:ModSettingsSearch.Exclude="True">
+        <StackPanel Orientation="Horizontal"><TextBlock Text="{Binding System_PresetOperationStatusText}" Foreground="#FFFFAAAA" TextWrapping="Wrap" MaxWidth="650"/><Button Content="{Binding System_PresetStatusDismissText}" Command="{Binding System_DismissPresetStatusCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetStatusDismissText}" Padding="8,2" Margin="8,0,0,0" VerticalAlignment="Top"/></StackPanel>
+      </Border>
+      <StackPanel Orientation="Horizontal" Visibility="{Binding System_SettingsSourceVisibility}" Margin="0,0,0,8" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_SettingsSourceHelpText}" shared:ModSettingsSearch.Exclude="True">
+        <TextBlock Text="{Binding System_SettingsSourceText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        <ComboBox ItemsSource="{Binding System_SettingsSources}" SelectedItem="{Binding System_SelectedSettingsSource, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_SettingsSourceHelpText}" Width="260" Margin="8,0,0,0"/>
+        <Button IsEnabled="{Binding System_CanLoadSettingsSource}" Content="{Binding System_SettingsSourceLoadText}" Command="{Binding System_LoadSettingsSourceCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_SettingsSourceHelpText}" Padding="10,3" Margin="8,0,0,0"/>
+      </StackPanel>
+      <Border Visibility="{Binding System_PresetLoadPanelVisibility}" BorderBrush="#FF77AAFF" BorderThickness="1" CornerRadius="3" Padding="8" Margin="0,0,0,8" shared:ModSettingsSearch.Exclude="True">
+        <StackPanel>
+          <TextBlock Text="{Binding System_PresetLoadText}" Foreground="White" FontWeight="Bold" Margin="0,0,0,6"/>
+          <ComboBox ItemsSource="{Binding System_PresetLoadEntries}" SelectedItem="{Binding System_SelectedPresetLoadEntry, Mode=TwoWay}" Width="530" HorizontalAlignment="Left" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_SelectedPresetLoadEntry.Description}"/>
+          <TextBlock Text="{Binding System_SelectedPresetLoadEntry.Description}" Foreground="#CCCCCC" TextWrapping="Wrap" MaxWidth="530" HorizontalAlignment="Left" Margin="0,5,0,0"/>
+          <TextBlock Text="{Binding System_PresetLoadSelectionHelpText}" Foreground="#FFBFCFE8" TextWrapping="Wrap" MaxWidth="530" HorizontalAlignment="Left" Margin="0,5,0,0"/>
+          <StackPanel Orientation="Horizontal" Margin="0,7,0,0">
+            <Button Content="{Binding System_PresetLoadConfirmText}" Command="{Binding System_ConfirmPresetLoadCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetLoadConfirmText}" Padding="10,3"/>
+            <Button IsEnabled="{Binding System_CanDeleteSelectedPreset}" Visibility="{Binding System_PresetDeleteVisibility}" Content="{Binding System_PresetDeleteText}" Command="{Binding System_DeletePresetCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetDeleteText}" Padding="10,3" Margin="8,0,0,0"/>
+            <Button Content="{Binding System_PresetLoadCancelText}" Command="{Binding System_CancelPresetLoadCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetLoadCancelText}" Padding="10,3" Margin="8,0,0,0"/>
+          </StackPanel>
+        </StackPanel>
+      </Border>
+      <Border Visibility="{Binding System_PresetSavePanelVisibility}" BorderBrush="#FFF2D48A" BorderThickness="1" CornerRadius="3" Padding="8" Margin="0,0,0,8" shared:ModSettingsSearch.Exclude="True">
+        <StackPanel>
+          <TextBlock Text="{Binding System_PresetSaveText}" Foreground="White" FontWeight="Bold" Margin="0,0,0,6"/>
+          <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
+            <TextBlock Text="{Binding System_PresetSaveTargetText}" Foreground="White" VerticalAlignment="Center" Width="140"/>
+            <ComboBox ItemsSource="{Binding System_PresetSaveTargets}" SelectedItem="{Binding System_SelectedPresetSaveTarget, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveTargetText}" Width="260"/>
+          </StackPanel>
+          <StackPanel Orientation="Horizontal">
+            <TextBlock Text="{Binding System_PresetSaveNameText}" Foreground="White" VerticalAlignment="Center" Width="140"/>
+            <TextBox ui:KeyboardCaptureBinding.Enabled="True" Text="{Binding System_PresetSaveName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveNameText}" Width="260" Foreground="White" Background="#CC1A1A1A" BorderBrush="#FFB08A4A" BorderThickness="1" Padding="5,2"/>
+          </StackPanel>
+          <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
+            <TextBlock Text="{Binding System_PresetSaveDescriptionText}" Foreground="White" VerticalAlignment="Center" Width="140"/>
+            <TextBox ui:KeyboardCaptureBinding.Enabled="True" Text="{Binding System_PresetSaveDescription, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveDescriptionText}" Width="420" Height="120" MaxLength="8192" AcceptsReturn="True" TextWrapping="Wrap" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto" Foreground="White" Background="#CC1A1A1A" BorderBrush="#FFB08A4A" BorderThickness="1" Padding="5,2"/>
+          </StackPanel>
+          <StackPanel Orientation="Horizontal" Margin="0,6,0,4">
+            <TextBlock Text="{Binding System_PresetSaveBulkModeText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveBulkModeHelpText}" Foreground="White" VerticalAlignment="Center" Margin="0,0,6,0"/>
+            <ComboBox ItemsSource="{Binding System_PresetSaveBulkModeOptions}" SelectedIndex="{Binding System_PresetSaveBulkModeIndex, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveBulkModeHelpText}" Width="150"/>
+          </StackPanel>
+          <ScrollViewer HorizontalAlignment="Left" Width="530" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto" MaxHeight="220">
+            <ScrollViewer.Resources>
+              <sys:Double x:Key="Size.ScrollBar">8</sys:Double>
+            </ScrollViewer.Resources>
+          <ItemsControl ItemsSource="{Binding System_PresetSaveSettings}">
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/UnitCosts.diff).
