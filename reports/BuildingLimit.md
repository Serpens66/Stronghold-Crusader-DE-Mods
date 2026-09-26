# BuildingLimit release status

**Status:** code newer

- Release: [v1.0.24](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.24)
- Release commit: [9f580bb](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/9f580bbc7e32072f9ef32e380997af25767d09f8)
- Current main commit: [d43d3d8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d43d3d831cc5f89e83ff1abdddb547a7fa579867)

## Relevant changed files

- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/BuildingLimit.dll`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/BuildingLimit.pdb`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/info.json`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ar.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/cs-CZ.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/de-DE.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/el-GR.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/en-US.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/es-ES.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/fr-FR.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/hu-HU.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/it-IT.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ja-JP.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ko-KR.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/nl-NL.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pl-PL.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pt-BR.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ru-RU.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/sv-SE.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/th-TH.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/tr-TR.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/uk-UA.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-CN.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-HK.txt`
- `BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml`
- `BuildingLimit/BuildingLimit.csproj`
- `BuildingLimit/Locales/ar.txt`
- `BuildingLimit/Locales/cs-CZ.txt`
- `BuildingLimit/Locales/de-DE.txt`
- `BuildingLimit/Locales/el-GR.txt`
- `BuildingLimit/Locales/en-US.txt`
- `BuildingLimit/Locales/es-ES.txt`
- `BuildingLimit/Locales/fr-FR.txt`
- `BuildingLimit/Locales/hu-HU.txt`
- `BuildingLimit/Locales/it-IT.txt`
- `BuildingLimit/Locales/ja-JP.txt`
- `BuildingLimit/Locales/ko-KR.txt`
- `BuildingLimit/Locales/nl-NL.txt`
- `BuildingLimit/Locales/pl-PL.txt`
- `BuildingLimit/Locales/pt-BR.txt`
- `BuildingLimit/Locales/ru-RU.txt`
- `BuildingLimit/Locales/sv-SE.txt`
- `BuildingLimit/Locales/th-TH.txt`
- `BuildingLimit/Locales/tr-TR.txt`
- `BuildingLimit/Locales/uk-UA.txt`
- `BuildingLimit/Locales/zh-CN.txt`
- `BuildingLimit/Locales/zh-HK.txt`
- `BuildingLimit/src/BuildingLimitLobbyViewModel.cs`
- `BuildingLimit/src/BuildingLimitPlugin.cs`
- `BuildingLimit/src/BuildingLimitRuntime.cs`
- `Shared/DebugLogHelper.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplaySessionLifecycle.cs`
- `Shared/SerpLocalization.cs`

Relevant localization keys: `BuildingLimit.CrusaderDeTweakerWarning`, `Common.Limit`, `Common.Preset`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/info.json b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/info.json
index 7c4ab1be..6ae3fd87 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/info.json
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/info.json
@@ -3,13 +3,19 @@
   "Author": "Serpens66",
   "Name": "Building Limit",
   "Description": "Limits the number of active buildings per kind for human players in Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.24",
+  "Version": "1.0.25",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.3.0",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.25",
+      "Changes": [
+        "Shows a settings warning when Crusader DE Tweaker is loaded because building limits should be configured in only one mod."
+      ]
+    },
     {
       "Version": "1.0.24",
       "Changes": [

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ar.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ar.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ar.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ar.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/cs-CZ.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/cs-CZ.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/cs-CZ.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/cs-CZ.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/de-DE.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/de-DE.txt
index 3bc0ae03..90d6dc53 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/de-DE.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/de-DE.txt
@@ -1,6 +1,6 @@
 # Serp mod localization
+
 # Format: key=value
-Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client-Settings)
@@ -10,16 +10,16 @@ Common.Limit=Begrenzung
 Common.Max=Maximum
 BuildingLimit.Title=Gebäudelimits (Mensch)
 BuildingLimit.Help=Nur für Menschen! -1 = unbegrenzt. Erlaubter Bereich: -1 bis 10000. Varianten wie Gaerten, Statuen, Schreine und Teiche werden zusammengezaehlt.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker ist geladen. Konfiguriere Gebäudelimits nur in einem der beiden Mods; wenn beide Limits festlegen, gilt das strengere Limit.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Tooltips durchsuchen
 Common.ModSettingsSearchIncludeToolTipsHelp=Durchsucht zusätzlich die erklärenden Tooltips der Einstellungen.
 Common.ModSettingsSearchClearHelp=Leert den Einstellungsfilter.
 Common.ModSettingsSearchNoResults=Keine passenden Einstellungen gefunden.
+
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
+Common.SettingsSourceHelp=Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.
+Common.PresetLoadFailedTitle=Preset konnte nicht geladen werden

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/el-GR.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/el-GR.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/el-GR.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/el-GR.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/en-US.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/en-US.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/en-US.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/en-US.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/es-ES.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/es-ES.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/es-ES.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/es-ES.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/fr-FR.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/fr-FR.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/fr-FR.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/fr-FR.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/hu-HU.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/hu-HU.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/hu-HU.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/hu-HU.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/it-IT.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/it-IT.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/it-IT.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/it-IT.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ja-JP.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ja-JP.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ja-JP.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ja-JP.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ko-KR.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ko-KR.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ko-KR.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ko-KR.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/nl-NL.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/nl-NL.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/nl-NL.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/nl-NL.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pl-PL.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pl-PL.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pl-PL.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pl-PL.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pt-BR.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pt-BR.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pt-BR.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/pt-BR.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ru-RU.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ru-RU.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ru-RU.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/ru-RU.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/sv-SE.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/sv-SE.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/sv-SE.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/sv-SE.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/th-TH.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/th-TH.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/th-TH.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/th-TH.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/tr-TR.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/tr-TR.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/tr-TR.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/tr-TR.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/uk-UA.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/uk-UA.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/uk-UA.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/uk-UA.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-CN.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-CN.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-CN.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-CN.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-HK.txt b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-HK.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-HK.txt
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Locales/zh-HK.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml
index e2fb2e4a..fd5554dc 100644
--- a/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml
+++ b/BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml
@@ -1,7 +1,8 @@
 <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
-      xmlns:shared="clr-namespace:Shared;assembly=BuildingLimit"
+      xmlns:sys="clr-namespace:System;assembly=mscorlib"
+      xmlns:shared="clr-namespace:Shared;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"
@@ -29,10 +30,79 @@
         <Border Style="{StaticResource ClientActivationBorder}" Visibility="{Binding ClientSettingsActivationVisibility}" Margin="8,0,0,0">
           <CheckBox IsEnabled="{Binding CanToggleClientSettings}" IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}" Content="{Binding ClientActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ClientSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
         </Border>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="145" VerticalAlignment="Center" Margin="14,0,0,0"/>
+        <Button IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" Content="{Binding System_PresetLoadText}" Command="{Binding System_OpenPresetLoadCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Padding="10,3" Margin="14,0,0,0"/>
+        <Button IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" Content="{Binding System_PresetSaveText}" Command="{Binding System_OpenPresetSaveCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveText}" Padding="10,3" Margin="8,0,0,0"/>
         <Button Command="{Binding System_ToggleModSettingsSearchCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_ModSettingsSearchToggleHelpText}" Width="30" Height="28" Padding="5" Margin="8,0,0,0"><Viewbox Width="16" Height="16"><Path Style="{StaticResource ModSettingsSearchIcon}" StrokeThickness="2" Data="M 7,1 A 6,6 0 1 1 6.99,1 M 11.5,11.5 L 16,16"/></Viewbox></Button>
-        <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" HorizontalAlignment="Left" Padding="10,3" Margin="14,0,0,0"/>
       </StackPanel>
+      <Border Visibility="{Binding CrusaderDeTweakerWarningVisibility}" Background="#442D2100" BorderBrush="#FFFFA726" BorderThickness="1" CornerRadius="3" Padding="8,5" Margin="0,0,0,8" shared:ModSettingsSearch.Exclude="True">
+        <TextBlock Text="{Binding CrusaderDeTweakerWarningText}" Foreground="#FFFFCC66" FontWeight="Bold" TextWrapping="Wrap" MaxWidth="700"/>
+      </Border>
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
+            <ItemsControl.ItemTemplate>
+              <DataTemplate>
+                <StackPanel Orientation="Horizontal" Margin="0,1">
+                  <TextBlock Text="{Binding PropertyName}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PropertyName}" Foreground="White" Width="300" VerticalAlignment="Center"/>
+                  <TextBlock Text="{Binding ScopeText}" Foreground="#BBBBBB" Width="70" VerticalAlignment="Center"/>
+                  <ComboBox ItemsSource="{Binding ModeOptions}" SelectedIndex="{Binding SelectedModeIndex, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PropertyName}" Width="130"/>
+                </StackPanel>
+              </DataTemplate>
+            </ItemsControl.ItemTemplate>
+          </ItemsControl>
+          </ScrollViewer>
+          <StackPanel Orientation="Horizontal" Margin="0,7,0,0">
+            <Button IsEnabled="{Binding System_CanConfirmPresetSave}" Content="{Binding System_PresetSaveConfirmText}" Command="{Binding System_ConfirmPresetSaveCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveConfirmHelpText}" Padding="10,3"/>
+            <Button Content="{Binding System_PresetSaveCancelText}" Command="{Binding System_CancelPresetSaveCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding System_PresetSaveCancelText}" Padding="10,3" Margin="8,0,0,0"/>
+          </StackPanel>
+        </StackPanel>
+      </Border>
       <StackPanel shared:ModSettingsSearch.Exclude="True" Visibility="{Binding System_ModSettingsSearchPanelVisibility}" HorizontalAlignment="Left" Margin="0,0,0,8">
         <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
           <TextBlock Text="{Binding System_ModSettingsSearchLabelText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center" Margin="0,0,8,0"/>
@@ -43,6 +113,8 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>
       <StackPanel IsEnabled="{Binding CanEditHostSettings}">

diff --git a/BuildingLimit/BuildingLimit.csproj b/BuildingLimit/BuildingLimit.csproj
index 160e4857..09ab8d3e 100644
--- a/BuildingLimit/BuildingLimit.csproj
+++ b/BuildingLimit/BuildingLimit.csproj
@@ -132,8 +132,6 @@
     <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
     <Compile Include="..\Shared\GameplaySessionLifecycle.cs"><Link>Shared\GameplaySessionLifecycle.cs</Link></Compile>
     <Compile Include="..\Shared\GameplayModActivationGate.cs"><Link>Shared\GameplayModActivationGate.cs</Link></Compile>
-    <Compile Include="..\Shared\PresetLobbyModSettingsViewModel.cs"><Link>Shared\PresetLobbyModSettingsViewModel.cs</Link></Compile>
-    <Compile Include="..\Shared\ModSettingsSearch.cs"><Link>Shared\ModSettingsSearch.cs</Link></Compile>
     <Compile Include="..\Shared\ToolTipPresentation.cs"><Link>Shared\ToolTipPresentation.cs</Link></Compile>
   </ItemGroup>
   <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />

diff --git a/BuildingLimit/Locales/ar.txt b/BuildingLimit/Locales/ar.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/Locales/ar.txt
+++ b/BuildingLimit/Locales/ar.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 BuildingLimit.Title=Building Limits (Human)
 BuildingLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together.
+BuildingLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies.
 
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
@@ -27,3 +27,48 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+
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
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/BuildingLimit/Locales/cs-CZ.txt b/BuildingLimit/Locales/cs-CZ.txt
index e2547a69..8813451b 100644
--- a/BuildingLimit/Locales/cs-CZ.txt
+++ b/BuildingLimit/Locales/cs-CZ.txt
@@ -1,25 +1,25 @@
 # Serp mod localization
+
 # Format: key=value
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/BuildingLimit.diff).
