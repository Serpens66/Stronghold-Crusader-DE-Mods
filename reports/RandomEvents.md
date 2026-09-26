# RandomEvents release status

**Status:** code newer

- Release: [v1.0.42](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.42)
- Release commit: [3729ba2](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/3729ba2988d6a63e4c7aeeddfa0fc7f550c501a2)
- Current main commit: [d43d3d8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d43d3d831cc5f89e83ff1abdddb547a7fa579867)

## Relevant changed files

- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/RandomEvents.dll`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/RandomEvents.pdb`
- `RandomEvents/Locales/ar.txt`
- `RandomEvents/Locales/cs-CZ.txt`
- `RandomEvents/Locales/de-DE.txt`
- `RandomEvents/Locales/el-GR.txt`
- `RandomEvents/Locales/en-US.txt`
- `RandomEvents/Locales/es-ES.txt`
- `RandomEvents/Locales/fr-FR.txt`
- `RandomEvents/Locales/hu-HU.txt`
- `RandomEvents/Locales/it-IT.txt`
- `RandomEvents/Locales/ja-JP.txt`
- `RandomEvents/Locales/ko-KR.txt`
- `RandomEvents/Locales/nl-NL.txt`
- `RandomEvents/Locales/pl-PL.txt`
- `RandomEvents/Locales/pt-BR.txt`
- `RandomEvents/Locales/ru-RU.txt`
- `RandomEvents/Locales/sv-SE.txt`
- `RandomEvents/Locales/th-TH.txt`
- `RandomEvents/Locales/tr-TR.txt`
- `RandomEvents/Locales/uk-UA.txt`
- `RandomEvents/Locales/zh-CN.txt`
- `RandomEvents/Locales/zh-HK.txt`
- `RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml`
- `RandomEvents/RandomEvents.csproj`
- `RandomEvents/src/KeepAnchorResolver.cs`
- `RandomEvents/src/RandomEventsPlugin.cs`
- `RandomEvents/src/RandomEventsRuntime.cs`
- `RandomEvents/src/ScenarioSignpostRegistry.cs`
- `RandomEvents/src/SignpostPlacementService.cs`
- `RandomEvents/src/SignpostTargeting.cs`
- `Shared/DebugLogHelper.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplaySessionLifecycle.cs`
- `Shared/SerpLocalization.cs`

Relevant localization keys: `Common.Preset`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt
index 55ca037f..6163b207 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt
@@ -1,5 +1,48 @@
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
 # Random Events localization
-Common.ResetToDefault=Zurücksetzen
+
 Common.EnableMod=Mod aktivieren
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client-Settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Feuer
 Common.HostOptions=HOST-OPTIONEN
 Common.ClientOptions=LOKALE CLIENT-OPTIONEN
 Common.HostReadOnly=Werte vom Host – schreibgeschützt
-Common.ResetToDefaultHelp=Setzt die Einstellungen zurück, die du im aktuellen Kontext ändern kannst.
 Common.EnableModHelp=Aktiviert oder deaktiviert diese Mod für die Partie.
 Common.PresetHelp=Wählt ein gespeichertes Preset. Clients ändern damit nur ihre persönlichen Einstellungen.
 
 RandomEvents.ScheduleTitle=Zeitplan
 RandomEvents.MultiplayerTitle=Mehrspieler
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset und Zurücksetzen betreffen Host-Einstellungen und deine lokalen Client-Optionen.
-Common.ActionsScopeClient=Preset und Zurücksetzen betreffen nur deine lokalen Client-Optionen.
+Common.ActionsScopeHost=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft Host-Einstellungen und deine lokalen Client-Optionen.
+Common.ActionsScopeClient=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft nur deine lokalen Client-Optionen.
 RandomEvents.PositiveEventsTitle=Positive Ereignisse
 RandomEvents.NegativeEventsTitle=Negative Ereignisse
 RandomEvents.MonthsValueFormat={0} Monate
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Tooltips durchsuchen
 Common.ModSettingsSearchIncludeToolTipsHelp=Durchsucht zusätzlich die erklärenden Tooltips der Einstellungen.
 Common.ModSettingsSearchClearHelp=Leert den Einstellungsfilter.
 Common.ModSettingsSearchNoResults=Keine passenden Einstellungen gefunden.
+Common.SettingsSourceHelp=Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.
+Common.PresetLoadFailedTitle=Preset konnte nicht geladen werden

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt
index 50d3cc8e..30d350a5 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt
@@ -1,5 +1,48 @@
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
 # Random Events localization
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
index 3248cd2d..d30a2316 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
@@ -1,7 +1,8 @@
 <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
-      xmlns:shared="clr-namespace:Shared;assembly=RandomEvents"
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
@@ -43,6 +110,8 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>
       <StackPanel IsEnabled="{Binding CanEditHostSettings}">

diff --git a/RandomEvents/Locales/ar.txt b/RandomEvents/Locales/ar.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/Locales/ar.txt
+++ b/RandomEvents/Locales/ar.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/Locales/cs-CZ.txt b/RandomEvents/Locales/cs-CZ.txt
index b1076020..bc64a64b 100644
--- a/RandomEvents/Locales/cs-CZ.txt
+++ b/RandomEvents/Locales/cs-CZ.txt
@@ -1,5 +1,48 @@
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
 # English fallback translation
-Common.ResetToDefault=Reset to Default
+
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Fire
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 
 RandomEvents.ScheduleTitle=Schedule
 RandomEvents.MultiplayerTitle=Multiplayer
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 RandomEvents.PositiveEventsTitle=Positive Events
 RandomEvents.NegativeEventsTitle=Negative Events
 RandomEvents.MonthsValueFormat={0} months
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/RandomEvents/Locales/de-DE.txt b/RandomEvents/Locales/de-DE.txt
index 55ca037f..6163b207 100644
--- a/RandomEvents/Locales/de-DE.txt
+++ b/RandomEvents/Locales/de-DE.txt
@@ -1,5 +1,48 @@
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
 # Random Events localization
-Common.ResetToDefault=Zurücksetzen
+
 Common.EnableMod=Mod aktivieren
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client-Settings)
@@ -44,15 +87,14 @@ RandomEvents.Event.Fire=Feuer
 Common.HostOptions=HOST-OPTIONEN
 Common.ClientOptions=LOKALE CLIENT-OPTIONEN
 Common.HostReadOnly=Werte vom Host – schreibgeschützt
-Common.ResetToDefaultHelp=Setzt die Einstellungen zurück, die du im aktuellen Kontext ändern kannst.
 Common.EnableModHelp=Aktiviert oder deaktiviert diese Mod für die Partie.
 Common.PresetHelp=Wählt ein gespeichertes Preset. Clients ändern damit nur ihre persönlichen Einstellungen.
 
 RandomEvents.ScheduleTitle=Zeitplan
 RandomEvents.MultiplayerTitle=Mehrspieler
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset und Zurücksetzen betreffen Host-Einstellungen und deine lokalen Client-Optionen.
-Common.ActionsScopeClient=Preset und Zurücksetzen betreffen nur deine lokalen Client-Optionen.
+Common.ActionsScopeHost=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft Host-Einstellungen und deine lokalen Client-Optionen.
+Common.ActionsScopeClient=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft nur deine lokalen Client-Optionen.
 RandomEvents.PositiveEventsTitle=Positive Ereignisse
 RandomEvents.NegativeEventsTitle=Negative Ereignisse
 RandomEvents.MonthsValueFormat={0} Monate
@@ -64,3 +106,5 @@ Common.ModSettingsSearchIncludeToolTips=Tooltips durchsuchen
 Common.ModSettingsSearchIncludeToolTipsHelp=Durchsucht zusätzlich die erklärenden Tooltips der Einstellungen.
 Common.ModSettingsSearchClearHelp=Leert den Einstellungsfilter.
 Common.ModSettingsSearchNoResults=Keine passenden Einstellungen gefunden.
+Common.SettingsSourceHelp=Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.
+Common.PresetLoadFailedTitle=Preset konnte nicht geladen werden

diff --git a/RandomEvents/Locales/el-GR.txt b/RandomEvents/Locales/el-GR.txt
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/RandomEvents.diff).
