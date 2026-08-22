# BuildingCosts release status

**Status:** code newer

- Release: [v1.0.93](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.93)
- Release commit: [7dd3c98](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/7dd3c9843abf577fcf0e19956f837a30d5c280a1)
- Current main commit: [052884c](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/052884c545ea5a7388b629bab9add42d8bc7c4d0)

## Relevant changed files

- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/BuildingCosts.dll`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/BuildingCosts.pdb`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/info.json`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ar.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/cs-CZ.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/de-DE.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/el-GR.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/en-US.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/es-ES.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/fr-FR.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/hu-HU.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/it-IT.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ja-JP.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ko-KR.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/nl-NL.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pl-PL.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pt-BR.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ru-RU.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/sv-SE.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/th-TH.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/tr-TR.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/uk-UA.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-CN.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-HK.txt`
- `BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml`
- `BuildingCosts/Locales/ar.txt`
- `BuildingCosts/Locales/cs-CZ.txt`
- `BuildingCosts/Locales/de-DE.txt`
- `BuildingCosts/Locales/el-GR.txt`
- `BuildingCosts/Locales/en-US.txt`
- `BuildingCosts/Locales/es-ES.txt`
- `BuildingCosts/Locales/fr-FR.txt`
- `BuildingCosts/Locales/hu-HU.txt`
- `BuildingCosts/Locales/it-IT.txt`
- `BuildingCosts/Locales/ja-JP.txt`
- `BuildingCosts/Locales/ko-KR.txt`
- `BuildingCosts/Locales/nl-NL.txt`
- `BuildingCosts/Locales/pl-PL.txt`
- `BuildingCosts/Locales/pt-BR.txt`
- `BuildingCosts/Locales/ru-RU.txt`
- `BuildingCosts/Locales/sv-SE.txt`
- `BuildingCosts/Locales/th-TH.txt`
- `BuildingCosts/Locales/tr-TR.txt`
- `BuildingCosts/Locales/uk-UA.txt`
- `BuildingCosts/Locales/zh-CN.txt`
- `BuildingCosts/Locales/zh-HK.txt`
- `BuildingCosts/src/BuildingCostsLobbyViewModel.cs`
- `BuildingCosts/src/BuildingCostsPlugin.cs`
- `BuildingCosts/src/BuildingCostsRuntime.cs`
- `Shared/GameModeHelper.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`
- `Shared/SerpLocalization.cs`

Relevant localization keys: `Common.ClientActivationLabel`, `Common.ClientSettingsActivationHelp`, `Common.HostActivationLabel`, `Common.HostSettingsActivationHelp`

## Diff

```diff
diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/info.json b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/info.json
index 02d54737..46e7c197 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/info.json
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/info.json
@@ -3,10 +3,22 @@
   "Author": "Serpens66",
   "Name": "Building Costs",
   "Description": "Lets you change construction costs for buildings in Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.93",
+  "Version": "1.0.95",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.95",
+      "Changes": [
+        "Apply and restore building costs per structure, keep failed restorations retryable, and always remove optional hooks when the mod is disabled."
+      ]
+    },
+    {
+      "Version": "1.0.94",
+      "Changes": [
+        "Disabled custom building-cost tooltip additions in the map editor."
+      ]
+    },
     {
       "Version": "1.0.93",
       "Changes": [

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ar.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ar.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ar.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ar.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/cs-CZ.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/cs-CZ.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/cs-CZ.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/cs-CZ.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/de-DE.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/de-DE.txt
index b42159bc..f40be027 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/de-DE.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/de-DE.txt
@@ -2,6 +2,10 @@
 # Format: key=value
 Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 BuildingCosts.Title=Baukosten
 BuildingCosts.Help=-1 = unverändert. Werte von 0 bis 1000 setzen die nativen Baukosten für dieses Material (Mensch und KI).
 BuildingCosts.BuildingHeader=Gebaeude

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/el-GR.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/el-GR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/el-GR.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/el-GR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/en-US.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/en-US.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/en-US.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/en-US.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/es-ES.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/es-ES.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/es-ES.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/es-ES.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/fr-FR.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/fr-FR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/fr-FR.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/fr-FR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/hu-HU.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/hu-HU.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/hu-HU.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/hu-HU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/it-IT.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/it-IT.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/it-IT.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/it-IT.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ja-JP.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ja-JP.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ja-JP.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ja-JP.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ko-KR.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ko-KR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ko-KR.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ko-KR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/nl-NL.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/nl-NL.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/nl-NL.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/nl-NL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pl-PL.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pl-PL.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pl-PL.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pl-PL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pt-BR.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pt-BR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pt-BR.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/pt-BR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ru-RU.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ru-RU.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ru-RU.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/ru-RU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/sv-SE.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/sv-SE.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/sv-SE.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/sv-SE.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/th-TH.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/th-TH.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/th-TH.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/th-TH.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/tr-TR.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/tr-TR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/tr-TR.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/tr-TR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/uk-UA.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/uk-UA.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/uk-UA.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/uk-UA.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-CN.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-CN.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-CN.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-CN.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-HK.txt b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-HK.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-HK.txt
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Locales/zh-HK.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml
index a6577381..ff476fa4 100644
--- a/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml
+++ b/BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml
@@ -14,17 +14,14 @@
                 HorizontalScrollBarVisibility="Auto">
     <StackPanel Margin="10">
       <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
-        <Border Style="{StaticResource HostActivationBorder}"><CheckBox IsEnabled="{Binding CanEditHostSettings}"
-                  IsChecked="{Binding EnableMod, Mode=TwoWay}"
-                  Content="{Binding EnableModText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding EnableModHelpText}"
-                  Foreground="White"
-                  FontWeight="Bold"
-                  VerticalAlignment="Center"/></Border>
-        <TextBlock Text="{Binding PresetText}" Visibility="{Binding PresetVisibility}" Foreground="#CCCCCC" VerticalAlignment="Center" Margin="14,0,6,0"/>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}"
-                  SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}"
-                  Width="170"
-                  VerticalAlignment="Center"/>
+        <TextBlock Text="{Binding ModEnabledText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        <Border Style="{StaticResource HostActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleHostSettings}" IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}" Content="{Binding HostActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding HostSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        </Border>
+        <Border Style="{StaticResource ClientActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleClientSettings}" IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}" Content="{Binding ClientActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ClientSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        </Border>
+        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="170" VerticalAlignment="Center" Margin="14,0,0,0"/>
         <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" HorizontalAlignment="Left" Padding="10,3" Margin="14,0,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>

diff --git a/BuildingCosts/Locales/ar.txt b/BuildingCosts/Locales/ar.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/ar.txt
+++ b/BuildingCosts/Locales/ar.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/cs-CZ.txt b/BuildingCosts/Locales/cs-CZ.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/cs-CZ.txt
+++ b/BuildingCosts/Locales/cs-CZ.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/de-DE.txt b/BuildingCosts/Locales/de-DE.txt
index b42159bc..f40be027 100644
--- a/BuildingCosts/Locales/de-DE.txt
+++ b/BuildingCosts/Locales/de-DE.txt
@@ -2,6 +2,10 @@
 # Format: key=value
 Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 BuildingCosts.Title=Baukosten
 BuildingCosts.Help=-1 = unverändert. Werte von 0 bis 1000 setzen die nativen Baukosten für dieses Material (Mensch und KI).
 BuildingCosts.BuildingHeader=Gebaeude

diff --git a/BuildingCosts/Locales/el-GR.txt b/BuildingCosts/Locales/el-GR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/el-GR.txt
+++ b/BuildingCosts/Locales/el-GR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/en-US.txt b/BuildingCosts/Locales/en-US.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/en-US.txt
+++ b/BuildingCosts/Locales/en-US.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/es-ES.txt b/BuildingCosts/Locales/es-ES.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/es-ES.txt
+++ b/BuildingCosts/Locales/es-ES.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/fr-FR.txt b/BuildingCosts/Locales/fr-FR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/fr-FR.txt
+++ b/BuildingCosts/Locales/fr-FR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/hu-HU.txt b/BuildingCosts/Locales/hu-HU.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/hu-HU.txt
+++ b/BuildingCosts/Locales/hu-HU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/it-IT.txt b/BuildingCosts/Locales/it-IT.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/it-IT.txt
+++ b/BuildingCosts/Locales/it-IT.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/ja-JP.txt b/BuildingCosts/Locales/ja-JP.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/ja-JP.txt
+++ b/BuildingCosts/Locales/ja-JP.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/ko-KR.txt b/BuildingCosts/Locales/ko-KR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/ko-KR.txt
+++ b/BuildingCosts/Locales/ko-KR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/nl-NL.txt b/BuildingCosts/Locales/nl-NL.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/nl-NL.txt
+++ b/BuildingCosts/Locales/nl-NL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/pl-PL.txt b/BuildingCosts/Locales/pl-PL.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/pl-PL.txt
+++ b/BuildingCosts/Locales/pl-PL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/pt-BR.txt b/BuildingCosts/Locales/pt-BR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/pt-BR.txt
+++ b/BuildingCosts/Locales/pt-BR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/ru-RU.txt b/BuildingCosts/Locales/ru-RU.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/ru-RU.txt
+++ b/BuildingCosts/Locales/ru-RU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/sv-SE.txt b/BuildingCosts/Locales/sv-SE.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/sv-SE.txt
+++ b/BuildingCosts/Locales/sv-SE.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/th-TH.txt b/BuildingCosts/Locales/th-TH.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/th-TH.txt
+++ b/BuildingCosts/Locales/th-TH.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/tr-TR.txt b/BuildingCosts/Locales/tr-TR.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/tr-TR.txt
+++ b/BuildingCosts/Locales/tr-TR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/uk-UA.txt b/BuildingCosts/Locales/uk-UA.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/uk-UA.txt
+++ b/BuildingCosts/Locales/uk-UA.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/zh-CN.txt b/BuildingCosts/Locales/zh-CN.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/zh-CN.txt
+++ b/BuildingCosts/Locales/zh-CN.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/Locales/zh-HK.txt b/BuildingCosts/Locales/zh-HK.txt
index 263bb423..697285d5 100644
--- a/BuildingCosts/Locales/zh-HK.txt
+++ b/BuildingCosts/Locales/zh-HK.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 BuildingCosts.Title=Building Costs
 BuildingCosts.Help=-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).

diff --git a/BuildingCosts/src/BuildingCostsLobbyViewModel.cs b/BuildingCosts/src/BuildingCostsLobbyViewModel.cs
index 15142e90..2ad32fbd 100644
--- a/BuildingCosts/src/BuildingCostsLobbyViewModel.cs
+++ b/BuildingCosts/src/BuildingCostsLobbyViewModel.cs
@@ -426,6 +426,9 @@ namespace BuildingCosts
         {
             try
             {
+                if (!CrusaderDE.MainViewModel.viewModelLoaded)
+                    return null;
+
                 return CrusaderDE.MainViewModel.Instance?.getSmallGoodsIcon((int)good);
             }
             catch

diff --git a/BuildingCosts/src/BuildingCostsPlugin.cs b/BuildingCosts/src/BuildingCostsPlugin.cs
index de3b5ac3..11cc2bf4 100644
--- a/BuildingCosts/src/BuildingCostsPlugin.cs
+++ b/BuildingCosts/src/BuildingCostsPlugin.cs
@@ -15,7 +15,7 @@ namespace BuildingCosts
 
         public const string PluginGuid = "BuildingCosts_Serp";
         public const string PluginName = "Building Costs";
-        public const string PluginVersion = "1.0.93";
+        public const string PluginVersion = "1.0.95";
 
         internal static readonly BuildingCostTooltipViewModel BuildingCostTooltipViewModel = new BuildingCostTooltipViewModel();
 
@@ -41,30 +41,48 @@ namespace BuildingCosts
 
             CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
 
+            TryInitializeStage("native version diagnostics", () => Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName));
+            TryInitializeStage("localized names", Settings.RefreshLocalizedNames);
             try
             {
-                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
-                Settings.RefreshLocalizedNames();
                 Shared.LobbyModSettingsPresetRegistration.Register(
                     this,
                     Logger,
                     "BuildingCosts_Serp",
                     Settings,
                     "ScriptExtenderUI/BuildingCostsSettings.xaml");
-                GameXAMLManagerAPI.Instance.RegisterBinding(
+            }
+            catch (Exception ex)
+            {
+                Shared.DebugLogHelper.LogError(Logger, $"BuildingCosts settings registration failed; gameplay runtime stopped fail-closed: {ex}");
+                return;
+            }
+
+            TryInitializeStage("detailed tooltip binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                     "BuildingCostsTooltipHost",
-                    BuildingCostTooltipViewModel);
-                GameXAMLManagerAPI.Instance.RegisterBinding(
+                    BuildingCostTooltipViewModel));
+            TryInitializeStage("compact tooltip binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                     "BuildingCostsTooltipHostCompact",
-                    BuildingCostTooltipViewModel);
+                    BuildingCostTooltipViewModel));
 
-                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingCosts UI registered.");
+            try
+            {
                 runtime.InitializeAfterLibraryLoaded();
+                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingCosts runtime initialized.");
             }
             catch (Exception ex)
             {
                 Shared.DebugLogHelper.LogError(Logger, $"Error while initializing BuildingCosts after library load: {ex}");
             }
         }
+
+        private void TryInitializeStage(string stageName, Action initialize)
+        {
+            try { initialize(); }
+            catch (Exception ex)
+            {
+                Shared.DebugLogHelper.LogError(Logger, $"BuildingCosts {stageName} failed; independent stages continue: {ex}");
+            }
+        }
     }
 }

diff --git a/BuildingCosts/src/BuildingCostsRuntime.cs b/BuildingCosts/src/BuildingCostsRuntime.cs
index 61e511f4..77298e7f 100644
--- a/BuildingCosts/src/BuildingCostsRuntime.cs
+++ b/BuildingCosts/src/BuildingCostsRuntime.cs
@@ -63,9 +63,16 @@ namespace BuildingCosts
             if (hooksSubscribed)
                 return;
 
-            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
-                .Where(args => args.Phase == EventHookPhase.Post)
-                .Subscribe(OnStartMap));
+            try
+            {
+                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
+                    .Where(args => args.Phase == EventHookPhase.Post)
+                    .Subscribe(OnStartMap));
+            }
+            catch (Exception ex)
+            {
+                LogDebug("Building cost map-start subscription failed; direct settings changes continue:", ex);
+            }
 
             try
             {
@@ -94,8 +101,8 @@ namespace BuildingCosts
             }
 
             SubscribeHooks();
-            InitializeVanillaCostTooltips();
-            ApplyBuildingCosts();
+            TryRunFeature("Vanilla tooltip costs", InitializeVanillaCostTooltips);
+            TryRunFeature("building costs", ApplyBuildingCosts);
             libraryInitialized = true;
             LogDebug("Applied initial building cost settings");
         }
@@ -113,11 +120,15 @@ namespace BuildingCosts
         private void UnsubscribeHooks()
         {
             foreach (IDisposable subscription in subscriptions)
-                subscription.Dispose();
+            {
+                try { subscription.Dispose(); }
+                catch (Exception ex) { LogDebug("Building cost subscription cleanup failed:", ex); }
+            }
 
             subscriptions.Clear();
             hooksSubscribed = false;
-            updateRolloverHook?.Dispose();
+            try { updateRolloverHook?.Dispose(); }
+            catch (Exception ex) { LogDebug("Building cost tooltip hook cleanup failed:", ex); }
             updateRolloverHook = null;
             updateRolloverTrampoline = null;
             ClearBuildingCostTooltip();
@@ -161,13 +172,19 @@ namespace BuildingCosts
                 if (settings.EnableMod)
                 {
                     SubscribeHooks();
-                    InitializeVanillaCostTooltips();
-                    ApplyBuildingCosts();
+                    TryRunFeature("Vanilla tooltip costs", InitializeVanillaCostTooltips);
+                    TryRunFeature("building costs", ApplyBuildingCosts);
                 }
                 else
                 {
-                    RestoreDefaultBuildingCosts();
-                    UnsubscribeHooks();
+                    try
+                    {
+                        RestoreDefaultBuildingCosts();
+                    }
+                    finally
+                    {
+                        UnsubscribeHooks();
+                    }
                 }
 
                 return;
@@ -177,7 +194,7 @@ namespace BuildingCosts
                 return;
 
             if (propertyName == nameof(BuildingCostsLobbyViewModel.BuildingCosts))
-                ApplyBuildingCosts();
+                TryRunFeature("building costs", ApplyBuildingCosts);
         }
 
         private void OnStartMap(MapStartEventArgs args)
@@ -245,7 +262,16 @@ namespace BuildingCosts
                 }
 
                 foreach (eStructs structure in definition.Structures)
-                    changedMaterials += ApplyStructureCosts(structure, entry.Value);
+                {
+                    try
+                    {
+                        changedMaterials += ApplyStructureCosts(structure, entry.Value);
+                    }
+                    catch (Exception ex)
+                    {
+                        LogDebug("Could not apply building costs; remaining structures continue:", structure, ex);
+                    }
+                }
             }
 
             LogDebug("Applied building cost materials:", changedMaterials);
@@ -255,44 +281,55 @@ namespace BuildingCosts
         private void RestoreDefaultBuildingCosts()
         {
             int restoredMaterials = 0;
+            var failedRestores = new Dictionary<eStructs, CostMaterialMask>();
             foreach (KeyValuePair<eStructs, CostMaterialMask> entry in overriddenCostMaterials)
             {
-                eStructs structure = entry.Key;
-                CostMaterialMask materials = entry.Value;
-                BuildingCost cost = GameBuildingManagerAPI.Instance.GetDefaultCost(structure);
-
-                if ((materials & CostMaterialMask.Wood) != 0)
+                try
                 {
-                    GameBuildingManagerAPI.Instance.SetWoodCost(structure, cost.Wood);
-                    restoredMaterials++;
-                }
+                    eStructs structure = entry.Key;
+                    CostMaterialMask materials = entry.Value;
+                    BuildingCost cost = GameBuildingManagerAPI.Instance.GetDefaultCost(structure);
 
-                if ((materials & CostMaterialMask.Stone) != 0)
-                {
-                    GameBuildingManagerAPI.Instance.SetStoneCost(structure, cost.Stone);
-                    restoredMaterials++;
-                }
+                    if ((materials & CostMaterialMask.Wood) != 0)
+                    {
+                        GameBuildingManagerAPI.Instance.SetWoodCost(structure, cost.Wood);
+                        restoredMaterials++;
+                    }
 
-                if ((materials & CostMaterialMask.Iron) != 0)
-                {
-                    GameBuildingManagerAPI.Instance.SetIronIngotCost(structure, cost.Iron);
-                    restoredMaterials++;
-                }
+                    if ((materials & CostMaterialMask.Stone) != 0)
+                    {
+                        GameBuildingManagerAPI.Instance.SetStoneCost(structure, cost.Stone);
+                        restoredMaterials++;
+                    }
 
-                if ((materials & CostMaterialMask.Pitch) != 0)
-                {
-                    GameBuildingManagerAPI.Instance.SetRawPitchCost(structure, cost.Pitch);
-                    restoredMaterials++;
-                }
+                    if ((materials & CostMaterialMask.Iron) != 0)
+                    {
+                        GameBuildingManagerAPI.Instance.SetIronIngotCost(structure, cost.Iron);
+                        restoredMaterials++;
+                    }
+
+                    if ((materials & CostMaterialMask.Pitch) != 0)
+                    {
+                        GameBuildingManagerAPI.Instance.SetRawPitchCost(structure, cost.Pitch);
+                        restoredMaterials++;
+                    }
 
-                if ((materials & CostMaterialMask.Gold) != 0)
+                    if ((materials & CostMaterialMask.Gold) != 0)
+                    {
+                        GameBuildingManagerAPI.Instance.SetGoldCost(structure, cost.Gold);
+                        restoredMaterials++;
+                    }
+                }
+                catch (Exception ex)
                 {
-                    GameBuildingManagerAPI.Instance.SetGoldCost(structure, cost.Gold);
-                    restoredMaterials++;
+                    failedRestores[entry.Key] = entry.Value;
+                    LogDebug("Could not restore building costs; remaining structures continue:", entry.Key, ex);
                 }
             }
 
             overriddenCostMaterials.Clear();
+            foreach (KeyValuePair<eStructs, CostMaterialMask> failed in failedRestores)
+                overriddenCostMaterials[failed.Key] = failed.Value;
             LogDebug("Restored default building cost materials:", restoredMaterials);
             ResetTooltipCache();
         }
@@ -413,6 +450,13 @@ namespace BuildingCosts
         {
             try
             {
+                if (IsMapEditor())
+                {
+                    BuildingCostsPlugin.BuildingCostTooltipViewModel.SetPlacement(false, false);
+                    ClearBuildingCostTooltip();
+                    return;
+                }
+
                 bool detailedTooltipVisible = MainViewModel.Instance.RolloverBuilding_TooltipVis;
                 bool compactTooltipVisible = MainViewModel.Instance.RolloverBuilding_TooltipVisNot;
                 int hoverStruct = (int)hoverStructField.GetValue(hud);
@@ -483,6 +527,8 @@ namespace BuildingCosts
             tooltipIsClear = true;
         }
 
+        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();
+
         private void ResetTooltipCache()
         {
             lastTooltipStruct = int.MinValue;
@@ -981,5 +1027,14 @@ namespace BuildingCosts
         {
             Shared.DebugLogHelper.LogDebug(log, parts);
         }
+
+        private void TryRunFeature(string featureName, Action action)
+        {
+            try { action(); }
+            catch (Exception ex)
+            {
+                LogDebug("Building costs feature failed; independent features continue:", featureName, ex);
+            }
+        }
     }
 }

diff --git a/Shared/GameModeHelper.cs b/Shared/GameModeHelper.cs
index f5be1a9f..be7eee52 100644
--- a/Shared/GameModeHelper.cs
+++ b/Shared/GameModeHelper.cs
@@ -1,4 +1,5 @@
 using SHCDESE.API;
+using CrusaderDE;
 
 namespace Shared
 {
@@ -17,7 +18,7 @@ namespace Shared
             {
                 foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                 {
-                    if (!member.SkirmishMember)
+                    if (member != null && !member.SkirmishMember)
                         realLobbyMembers++;
                 }
             }
@@ -28,7 +29,7 @@ namespace Shared
             {
                 foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                 {
-                    if (!member.skirmishAI && member.steamID > 1000)
+                    if (member != null && !member.skirmishAI && member.steamID > 1000)
                         realNetworkGameMembers++;
                 }
             }
@@ -44,7 +45,7 @@ namespace Shared
 
             int gameType = gameData != null ? gameData.game_type : -1;
             int skirmishGameType = gameData != null ? gameData.SkirmishGameType : -1;
-            bool mapEditor = GamePlayerManagerAPI.Instance.IsInMapEditor();
+            bool mapEditor = IsMapEditor();
             // game_type 3 is Vanilla's skirmish family; non-negative subtypes are initialized via StartSkirmishModeGame.
             bool singleplayerSkirmishMode =
                 !realMultiplayer &&
@@ -86,6 +87,33 @@ namespace Shared
         // This broader check also covers future/utility subtypes initialized by Vanilla as skirmish mode.
         public static bool IsSingleplayerSkirmishMode(bool multiplayerSave = false) =>
             Capture(multiplayerSave).IsSingleplayerSkirmishMode;
+
+        public static bool IsMapEditor()
+        {
+            try
+            {
+                if (GamePlayerManagerAPI.Instance?.IsInMapEditor() == true)
+                    return true;
+            }
+            catch
+            {
+                // The Script Extender singleton can be unavailable during early plugin startup.
+            }
+
+            // MainViewModel.Instance constructs the ViewModel. Reading it before the
+            // game's own loaded marker is set can therefore fail inside Vanilla code.
+            if (!MainViewModel.viewModelLoaded)
+                return false;
+
+            try
+            {
+                return MainViewModel.Instance?.IsMapEditorMode ?? false;
+            }
+            catch
+            {
+                return false;
+            }
+        }
     }
 
     internal readonly struct GameModeSnapshot

diff --git a/Shared/PresetLobbyModSettingsViewModel.cs b/Shared/PresetLobbyModSettingsViewModel.cs
index 5ef91a1c..5924b0b8 100644
--- a/Shared/PresetLobbyModSettingsViewModel.cs
+++ b/Shared/PresetLobbyModSettingsViewModel.cs
@@ -8,14 +8,605 @@ using SHCDESE.BepInEx.Bootstrap;
 using SHCDESE.ViewModels;
 using System;
 using System.Collections.Generic;
+using System.Collections.ObjectModel;
 using System.Diagnostics;
 using System.IO;
 using System.Linq;
 using System.Reflection;
+using System.Runtime.InteropServices;
 using System.Runtime.CompilerServices;
+#if !SHARED_PRESET_TESTS
+using R3;
+using SHCDESE.EventAPI;
+using UnityEngine;
+#endif
 using ComboBoxItem = Noesis.ComboBoxItem;
 using Visibility = Noesis.Visibility;
 
+namespace Shared
+{
+    internal sealed class PerPlayerLobbySettingsCoordinator
+    {
+        private const int FirstPlayerId = 1;
+        private const int LastPlayerId = 8;
+#if !SHARED_PRESET_TESTS
+        private static readonly FieldInfo LobbyIdField = typeof(Platform_Multiplayer.MPLobby)
+            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly FieldInfo LobbyMemberIdField = typeof(Platform_Multiplayer.MPLobbyMember)
+            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly FieldInfo SteamIdValueField = LobbyMemberIdField?.FieldType
+            .GetField("m_SteamID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly MethodInfo GetPlayerIdForSteamIdMethod = typeof(GameNetworkAPI)
+            .GetMethods(BindingFlags.Static | BindingFlags.Public)
+            .Single(method =>
+                method.Name == "GetPlayerIdForSteamId" &&
+                method.GetParameters().Length == 1);
+#endif
+        private readonly PresetLobbyModSettingsViewModel owner;
+        private readonly ManualLogSource log;
+        private readonly string modName;
+        private readonly PerPlayerLobbySettingsContract contract;
+        private readonly Dictionary<int, ulong> playersById = new Dictionary<int, ulong>();
+        private ulong lobbyId;
+        private bool hasLobby;
+        private bool publishPending;
+        private bool rosterHasUnresolvedPlayers;
+        private int resolvedLocalPlayerId;
+        private bool isResettingSlots;
+        private bool isMirroringLocalSetting;
+        private bool isReady = true;
+        private string readinessError = string.Empty;
+        private bool active;
+#if !SHARED_PRESET_TESTS
+        private int lastObservedFrame = -1;
+        private float nextErrorLogTime;
+        private IDisposable mapStartSubscription;
+        private IDisposable mapUnloadSubscription;
+        private bool mapStarted;
+#endif
+
+        internal PerPlayerLobbySettingsCoordinator(
+            PresetLobbyModSettingsViewModel owner,
+            ManualLogSource log,
+            string modName,
+            PerPlayerLobbySettingsContract contract)
+        {
+            this.owner = owner;
+            this.log = log;
+            this.modName = modName;
+            this.contract = contract;
+        }
+
+        internal bool IsReady => isReady;
+        internal string ReadinessError => readinessError;
+
+        internal void Activate()
+        {
+            if (contract.Settings.Count == 0)
+                return;
+            if (active)
+                return;
+
+            try
+            {
+                owner.PropertyChanged += OnOwnerPropertyChanged;
+#if !SHARED_PRESET_TESTS
+                Application.onBeforeRender += OnBeforeRender;
+                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(args =>
+                {
+                    if (args.Phase == EventHookPhase.Pre)
+                        mapStarted = true;
+                });
+                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(args =>
+                {
+                    if (args.Phase == EventHookPhase.Post)
+                        mapStarted = false;
+                });
+                if (mapStartSubscription == null || mapUnloadSubscription == null)
+                    throw new InvalidOperationException("The persistent map lifecycle subscriptions could not be created.");
+#endif
+                active = true;
+                RequestPublish();
+            }
+            catch
+            {
+                Deactivate();
+                throw;
+            }
+            DebugLogHelper.LogInfo(
+                log,
+                $"[{modName}] Shared per-player lobby convergence activated: " +
+                $"settings=[{string.Join(",", contract.Settings.Select(item => item.Property.Name))}], " +
+                $"required=[{string.Join(",", contract.Settings.Where(item => item.IsReportRequired).Select(item => item.Property.Name))}].");
+        }
+
+        internal void Deactivate()
+        {
+            owner.PropertyChanged -= OnOwnerPropertyChanged;
+#if !SHARED_PRESET_TESTS
+            Application.onBeforeRender -= OnBeforeRender;
+            mapStartSubscription?.Dispose();
+            mapUnloadSubscription?.Dispose();
+            mapStartSubscription = null;
+            mapUnloadSubscription = null;
+            mapStarted = false;
+#endif
+            active = false;
+            publishPending = false;
+        }
+
+        internal void RequestPublish()
+        {
+            publishPending = true;
+        }
+
+        internal bool ArePlayersReady(IEnumerable<int> playerIds, out string error)
+        {
+            int[] supplied = (playerIds ?? Enumerable.Empty<int>()).ToArray();
+            if (supplied.Any(id => !IsValidPlayerId(id)))
+            {
+                error = "At least one supplied human player ID is invalid.";
+                return false;
+            }
+            int[] expected = supplied
+                .Distinct()
+                .OrderBy(id => id)
+                .ToArray();
+            if (expected.Length == 0)
+            {
+                error = "No valid human player IDs were supplied.";
+                return false;
+            }
+            if (rosterHasUnresolvedPlayers)
+            {
+                error = "At least one human lobby member has no stable player ID yet.";
+                return false;
+            }
+            if (hasLobby && !expected.SequenceEqual(playersById.Keys.OrderBy(id => id)))
+            {
+                error = $"The requested human players [{string.Join(",", expected)}] do not match the converged lobby roster [{string.Join(",", playersById.Keys.OrderBy(id => id))}].";
+                return false;
+            }
+            if (hasLobby && (!IsValidPlayerId(resolvedLocalPlayerId) || !playersById.ContainsKey(resolvedLocalPlayerId)))
+            {
+                error = "The local human player ID is not part of the converged lobby roster.";
+                return false;
+            }
+
+            foreach (PerPlayerLobbySettingContract setting in contract.Settings.Where(item => item.IsReportRequired))
+            {
+                Array data = setting.GetData();
+                foreach (int playerId in expected)
+                {
+                    object value = data.GetValue(playerId);
+                    if (!setting.HasReport(value))
+                    {
+                        error = $"Player {playerId} has not reported [{setting.Property.Name}].";
+                        return false;
+                    }
+                }
+            }
+
+            error = string.Empty;
+            return true;
+        }
+
+        internal void Observe(
+            ulong? currentLobbyId,
+            IReadOnlyDictionary<int, ulong> currentPlayers,
+            bool hasUnresolvedPlayers,
+            int localPlayerId,
+            bool preserveForMapTransition)
+        {
+            if (!currentLobbyId.HasValue)
+            {
+                if (preserveForMapTransition)
+                    return;
+
+                if (hasLobby || playersById.Count != 0)
+                {
+                    ResetSlots(Enumerable.Range(FirstPlayerId, LastPlayerId));
+                    hasLobby = false;
+                    lobbyId = 0;
+                    playersById.Clear();
+                    rosterHasUnresolvedPlayers = false;
+                    resolvedLocalPlayerId = 0;
+                    publishPending = false;
+                    contract.LobbyChanged?.Invoke(PerPlayerLobbySnapshot.Empty);
+                }
+                SetReadiness(true, string.Empty);
+                return;
+            }
+
+            // Domain observers run in the lobby only. Settings are immutable once
+            // the map starts, so no file/status refresh may publish into a match.
+            contract.Observe?.Invoke();
+
+            var normalized = new Dictionary<int, ulong>();
+            foreach (KeyValuePair<int, ulong> player in currentPlayers ?? new Dictionary<int, ulong>())
+            {
+                if (IsValidPlayerId(player.Key) && player.Value != 0)
+                    normalized[player.Key] = player.Value;
+            }
+
+            bool sessionChanged = !hasLobby || lobbyId != currentLobbyId.Value;
+            bool membershipChanged = sessionChanged ||
+                normalized.Count != playersById.Count ||
+                normalized.Any(player =>
+                    !playersById.TryGetValue(player.Key, out ulong previousSteamId) ||
+                    previousSteamId != player.Value);
+            bool resolutionChanged = rosterHasUnresolvedPlayers != hasUnresolvedPlayers ||
+                resolvedLocalPlayerId != localPlayerId;
+            if (membershipChanged)
+            {
+                int[] slotsToReset = sessionChanged
+                    ? Enumerable.Range(FirstPlayerId, LastPlayerId).ToArray()
+                    : Enumerable.Range(FirstPlayerId, LastPlayerId)
+                        .Where(id =>
+                            playersById.TryGetValue(id, out ulong previousSteamId) &&
+                            (!normalized.TryGetValue(id, out ulong currentSteamId) ||
+                             currentSteamId != previousSteamId))
+                        .ToArray();
+                ResetSlots(slotsToReset);
+                hasLobby = true;
+                lobbyId = currentLobbyId.Value;
+                playersById.Clear();
+                foreach (KeyValuePair<int, ulong> player in normalized)
+                    playersById[player.Key] = player.Value;
+                publishPending = true;
+                DebugLogHelper.LogInfo(
+                    log,
+                    $"[{modName}] Shared per-player lobby roster changed: lobby={currentLobbyId.Value}, " +
+                    $"sessionChanged={sessionChanged}, players=[{string.Join(",", normalized.Keys.OrderBy(id => id))}], " +
+                    $"unresolved={hasUnresolvedPlayers}, resetSlots=[{string.Join(",", slotsToReset)}].");
+            }
+
+            bool localResolved = IsValidPlayerId(localPlayerId) && normalized.ContainsKey(localPlayerId);
+            rosterHasUnresolvedPlayers = hasUnresolvedPlayers;
+            resolvedLocalPlayerId = localPlayerId;
+            if (membershipChanged || resolutionChanged)
+            {
+                contract.LobbyChanged?.Invoke(new PerPlayerLobbySnapshot(
+                    currentLobbyId,
+                    new Dictionary<int, ulong>(normalized),
+                    hasUnresolvedPlayers,
+                    localPlayerId));
+            }
+            if (publishPending && localResolved && !hasUnresolvedPlayers)
+                PublishLocalSettings(localPlayerId);
+
+            if (hasUnresolvedPlayers)
+                SetReadiness(false, "At least one human lobby member has no stable player ID yet.");
+            else if (!localResolved)
+                SetReadiness(false, "The local human player ID is not part of the resolved lobby roster yet.");
+            else if (!ArePlayersReady(normalized.Keys, out string error))
+                SetReadiness(false, error);
+            else
+                SetReadiness(true, string.Empty);
+        }
+
+        private void PublishLocalSettings(int localPlayerId)
+        {
+            contract.BeforePublish?.Invoke();
+            contract.LocalPlayerResolved?.Invoke(localPlayerId);
+            foreach (PerPlayerLobbySettingContract setting in contract.Settings)
+            {
+                Array data = setting.GetData();
+                data.SetValue(CloneValue(setting.Property.GetValue(owner)), localPlayerId);
+                owner.System_TriggerUpdate(setting.Property.Name);
+            }
+            publishPending = false;
+            contract.Published?.Invoke();
+            DebugLogHelper.LogInfo(
+                log,
+                $"[{modName}] Shared personal settings advertised for playerId={localPlayerId}, " +
+                $"properties={contract.Settings.Count}.");
+        }
+
+        private void ResetSlots(IEnumerable<int> playerIds)
+        {
+            int[] slots = (playerIds ?? Enumerable.Empty<int>())
+                .Where(IsValidPlayerId)
+                .Distinct()
+                .ToArray();
+            if (slots.Length == 0)
+                return;
+
+            foreach (PerPlayerLobbySettingContract setting in contract.Settings)
+            {
+                Array data = setting.GetData();
+                foreach (int playerId in slots)
+                    data.SetValue(CloneValue(setting.CreateResetValue()), playerId);
+                isResettingSlots = true;
+                try
+                {
+                    owner.System_TriggerUpdate(setting.DataProperty.Name);
+                }
+                finally
+                {
+                    isResettingSlots = false;
+                }
+            }
+        }
+
+        private void SetReadiness(bool value, string error)
+        {
+            error = error ?? string.Empty;
+            if (isReady == value && string.Equals(readinessError, error, StringComparison.Ordinal))
+                return;
+            isReady = value;
+            readinessError = error;
+            owner.System_TriggerUpdate(nameof(PresetLobbyModSettingsViewModel.IsPerPlayerLobbySettingsReady));
+            owner.System_TriggerUpdate(nameof(PresetLobbyModSettingsViewModel.PerPlayerLobbySettingsReadinessError));
+        }
+
+        private void OnOwnerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
+        {
+            if (string.IsNullOrEmpty(args?.PropertyName))
+                return;
+            if (contract.Settings.Any(item => item.DataProperty.Name == args.PropertyName))
+            {
+                if (isResettingSlots || isMirroringLocalSetting)
+                    return;
+                contract.RemoteDataChanged?.Invoke(args.PropertyName);
+                RequestReadinessRefresh();
+                return;
+            }
+
+            PerPlayerLobbySettingContract localSetting = contract.Settings.FirstOrDefault(
+                item => item.Property.Name == args.PropertyName);
+            if (localSetting != null && hasLobby &&
+                IsValidPlayerId(resolvedLocalPlayerId) &&
+                playersById.ContainsKey(resolvedLocalPlayerId))
+            {
+                // The transport does not echo a sender's packet back to itself. Keep
+                // the local companion slot authoritative in Shared so individual mods
+                // never need to resolve or guess their own player ID in a setter.
+                localSetting.GetData().SetValue(
+                    CloneValue(localSetting.Property.GetValue(owner)),
+                    resolvedLocalPlayerId);
+                isMirroringLocalSetting = true;
+                try
+                {
+                    owner.System_TriggerUpdate(localSetting.DataProperty.Name);
+                }
+                finally
+                {
+                    isMirroringLocalSetting = false;
+                }
+                RequestReadinessRefresh();
+            }
+        }
+
+        private void RequestReadinessRefresh()
+        {
+            if (!hasLobby)
+                return;
+            if (!ArePlayersReady(playersById.Keys, out string error))
+                SetReadiness(false, error);
+            else
+                SetReadiness(true, string.Empty);
+        }
+
+#if !SHARED_PRESET_TESTS
+        private void OnBeforeRender()
+        {
+            int frame = Time.frameCount;
+            if (lastObservedFrame >= 0 && frame - lastObservedFrame < 15)
+                return;
+            lastObservedFrame = frame;
+
+            try
+            {
+                if (mapStarted)
+                {
+                    // Lobby settings are immutable during a match. OnUnloadMap is
+                    // the authoritative point at which observation may resume.
+                    return;
+                }
+                ObserveCurrentGameLobby();
+            }
+            catch (Exception exception)
+            {
+                SetReadiness(
+                    false,
+                    "The lobby roster could not be observed; waiting for a successful retry.");
+                if (Time.unscaledTime < nextErrorLogTime)
+                    return;
+                nextErrorLogTime = Time.unscaledTime + 5f;
+                DebugLogHelper.LogError(
+                    log,
+                    $"[{modName}] Shared per-player lobby observer recovered from an error: {exception}");
+            }
+        }
+
+        private void ObserveCurrentGameLobby()
+        {
+            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
+            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
+            if (lobby == null)
+            {
+                bool mapTransition = platform?.gameMembers != null &&
+                    platform.gameMembers.Any(member =>
+                        member != null && !member.skirmishAI && !member.kicked);
+                // There is no stable local player slot outside a lobby. Querying the
+                // Extender here only emits warnings and the value is discarded anyway.
+                Observe(null, null, false, 0, mapTransition);
+                return;
+            }
+
+            var players = new Dictionary<int, ulong>();
+            bool unresolved = false;
+            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members ?? Enumerable.Empty<Platform_Multiplayer.MPLobbyMember>())
+            {
+                if (member == null || member.dummyToBeKicked ||
+                    (member.SkirmishMember && !member.SkirmishHumanMember))
+                    continue;
+                object memberSteamId = LobbyMemberIdField?.GetValue(member);
+                int playerId = memberSteamId == null
+                    ? 0
+                    : (int)GetPlayerIdForSteamIdMethod.Invoke(null, new[] { memberSteamId });
+                ulong steamId = ReadSteamId(memberSteamId);
+                if (!IsValidPlayerId(playerId) || steamId == 0 ||
+                    (players.TryGetValue(playerId, out ulong previous) && previous != steamId))
+                {
+                    unresolved = true;
+                    players.Remove(playerId);
+                    continue;
+                }
+                players[playerId] = steamId;
+            }
+
+            ulong currentLobbyId = ReadSteamId(LobbyIdField?.GetValue(lobby));
+            if (currentLobbyId == 0)
+                unresolved = true;
+            Observe(currentLobbyId, players, unresolved, GetLocalPlayerId(), false);
+        }
+
+        private static ulong ReadSteamId(object steamId)
+        {
+            object value = steamId == null ? null : SteamIdValueField?.GetValue(steamId);
+            return value == null ? 0UL : Convert.ToUInt64(value);
+        }
+
+        private static int GetLocalPlayerId()
+        {
+            int playerId = GameNetworkAPI.GetLocalPlayerId();
+            return IsValidPlayerId(playerId) ? playerId : 0;
+        }
+#endif
+
+        private static bool IsValidPlayerId(int playerId) =>
+            playerId >= FirstPlayerId && playerId <= LastPlayerId;
+
+        internal static object CloneValue(object value)
+        {
+            if (!(value is Array source))
+                return value;
+            Array clone = (Array)source.Clone();
+            for (int index = 0; index < clone.Length; index++)
+            {
+                if (clone.GetValue(index) is Array nested)
+                    clone.SetValue(CloneValue(nested), index);
+            }
+            return clone;
+        }
+    }
+
+    public sealed class PerPlayerLobbySettingsBuilder
+    {
+        private readonly PresetLobbyModSettingsViewModel owner;
+        private readonly Dictionary<string, PerPlayerLobbySettingOptions> options = new Dictionary<string, PerPlayerLobbySettingOptions>(StringComparer.Ordinal);
+        private Action beforePublish;
+        private Action<int> localPlayerResolved;
+        private Action<PerPlayerLobbySnapshot> lobbyChanged;
+        private Action<string> remoteDataChanged;
+        private Action published;
+        private Action observe;
+
+        internal PerPlayerLobbySettingsBuilder(PresetLobbyModSettingsViewModel owner) { this.owner = owner; }
+
+        public PerPlayerLobbySettingsBuilder ResetSlotsWith(string propertyName, Func<object> resetValueFactory) { Get(propertyName).ResetValueFactory = resetValueFactory ?? throw new ArgumentNullException(nameof(resetValueFactory)); return this; }
+        public PerPlayerLobbySettingsBuilder RequireReport(string propertyName, Func<object, bool> hasReport = null) { PerPlayerLobbySettingOptions item = Get(propertyName); item.IsReportRequired = true; item.HasReport = hasReport ?? (value => value != null); return this; }
+        public PerPlayerLobbySettingsBuilder BeforePublish(Action callback) { beforePublish += callback; return this; }
+        public PerPlayerLobbySettingsBuilder WhenLocalPlayerResolved(Action<int> callback) { localPlayerResolved += callback; return this; }
+        public PerPlayerLobbySettingsBuilder WhenLobbyChanged(Action<PerPlayerLobbySnapshot> callback) { lobbyChanged += callback; return this; }
+        public PerPlayerLobbySettingsBuilder WhenRemoteDataChanged(Action<string> callback) { remoteDataChanged += callback; return this; }
+        public PerPlayerLobbySettingsBuilder AfterPublish(Action callback) { published += callback; return this; }
+        public PerPlayerLobbySettingsBuilder OnObservation(Action callback) { observe += callback; return this; }
+
+        internal PerPlayerLobbySettingsContract Build()
+        {
+            PropertyInfo[] properties = owner.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
+            foreach (PropertyInfo property in properties)
+            {
+                bool host = property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;
+                bool player = property.GetCustomAttribute<SyncPerPlayerAttribute>() != null;
+                bool local = property.GetCustomAttribute<PresetLocalAttribute>() != null;
+                int classifications = (host ? 1 : 0) + (player ? 1 : 0) + (local ? 1 : 0);
+                if (classifications > 1)
+                    throw new InvalidOperationException($"Setting [{owner.GetType().Name}.{property.Name}] has conflicting sync/preset classifications.");
+            }
+
+            var settings = new List<PerPlayerLobbySettingContract>();
+            foreach (PropertyInfo property in properties.Where(item => item.GetCustomAttribute<SyncPerPlayerAttribute>() != null))
+            {
+                if (!property.CanRead)
+                    throw new InvalidOperationException($"Per-player setting [{owner.GetType().Name}.{property.Name}] is not readable.");
+                PropertyInfo dataProperty = owner.GetType().GetProperty(property.Name + "Data", BindingFlags.Instance | BindingFlags.Public);
+                if (dataProperty == null || !dataProperty.CanRead || !dataProperty.PropertyType.IsArray)
+                    throw new InvalidOperationException($"Per-player setting [{owner.GetType().Name}.{property.Name}] requires a readable [{property.Name}Data] array.");
+                Type elementType = dataProperty.PropertyType.GetElementType();
+                if (!elementType.IsAssignableFrom(property.PropertyType))
+                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] has element type [{elementType}], expected [{property.PropertyType}].");
+                Array data = dataProperty.GetValue(owner) as Array;
+                if (data == null || data.Rank != 1 || data.Length < 9)
+                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] must be a one-dimensional array containing slots 0 through 8.");
+                if (!ReferenceEquals(data, dataProperty.GetValue(owner)))
+                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] must return one stable array instance.");
+
+                options.TryGetValue(property.Name, out PerPlayerLobbySettingOptions configured);
+                configured = configured ?? new PerPlayerLobbySettingOptions();
+                settings.Add(new PerPlayerLobbySettingContract(property, dataProperty, data, configured));
+            }
+            foreach (string configuredName in options.Keys)
+                if (!settings.Any(item => item.Property.Name == configuredName))
+                    throw new InvalidOperationException($"Per-player policy references non-[SyncPerPlayer] property [{owner.GetType().Name}.{configuredName}].");
+            return new PerPlayerLobbySettingsContract(settings, beforePublish, localPlayerResolved, lobbyChanged, remoteDataChanged, published, observe);
+        }
+
+        private PerPlayerLobbySettingOptions Get(string propertyName)
+        {
+            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("A property name is required.", nameof(propertyName));
+            if (!options.TryGetValue(propertyName, out PerPlayerLobbySettingOptions value)) options[propertyName] = value = new PerPlayerLobbySettingOptions();
+            return value;
+        }
+    }
+
+    public sealed class PerPlayerLobbySnapshot
+    {
+        internal static readonly PerPlayerLobbySnapshot Empty = new PerPlayerLobbySnapshot(null, new Dictionary<int, ulong>(), false, 0);
+        internal PerPlayerLobbySnapshot(ulong? lobbyId, IReadOnlyDictionary<int, ulong> players, bool unresolved, int localPlayerId)
+        {
+            LobbyId = lobbyId;
+            Players = new ReadOnlyDictionary<int, ulong>(
+                (players ?? new Dictionary<int, ulong>())
+                    .ToDictionary(item => item.Key, item => item.Value));
+            HasUnresolvedPlayers = unresolved;
+            LocalPlayerId = localPlayerId;
+        }
+        public ulong? LobbyId { get; }
+        public IReadOnlyDictionary<int, ulong> Players { get; }
+        public bool HasUnresolvedPlayers { get; }
+        public int LocalPlayerId { get; }
+    }
+
+    internal sealed class PerPlayerLobbySettingOptions { internal Func<object> ResetValueFactory; internal bool IsReportRequired; internal Func<object, bool> HasReport; }
+    internal sealed class PerPlayerLobbySettingContract
+    {
+        private readonly Array data;
+        private readonly PerPlayerLobbySettingOptions options;
+        internal PerPlayerLobbySettingContract(PropertyInfo property, PropertyInfo dataProperty, Array data, PerPlayerLobbySettingOptions options) { Property = property; DataProperty = dataProperty; this.data = data; this.options = options; }
+        internal PropertyInfo Property { get; }
+        internal PropertyInfo DataProperty { get; }
+        internal bool IsReportRequired => options.IsReportRequired;
+        internal Array GetData() => data;
+        internal object CreateResetValue() => options.ResetValueFactory != null ? options.ResetValueFactory() : (Property.PropertyType.IsValueType ? Activator.CreateInstance(Property.PropertyType) : null);
+        internal bool HasReport(object value) => !IsReportRequired || (options.HasReport ?? (item => item != null))(value);
+    }
+    internal sealed class PerPlayerLobbySettingsContract
+    {
+        internal PerPlayerLobbySettingsContract(IReadOnlyList<PerPlayerLobbySettingContract> settings, Action beforePublish, Action<int> localPlayerResolved, Action<PerPlayerLobbySnapshot> lobbyChanged, Action<string> remoteDataChanged, Action published, Action observe) { Settings = settings; BeforePublish = beforePublish; LocalPlayerResolved = localPlayerResolved; LobbyChanged = lobbyChanged; RemoteDataChanged = remoteDataChanged; Published = published; Observe = observe; }
+        internal IReadOnlyList<PerPlayerLobbySettingContract> Settings { get; }
+        internal Action BeforePublish { get; }
+        internal Action<int> LocalPlayerResolved { get; }
+        internal Action<PerPlayerLobbySnapshot> LobbyChanged { get; }
+        internal Action<string> RemoteDataChanged { get; }
+        internal Action Published { get; }
+        internal Action Observe { get; }
+    }
+}
+
 namespace Shared
 {
     /// <summary>
@@ -50,6 +641,7 @@ namespace Shared
         private bool missionPresetEditable;
         private bool isRealMultiplayer;
         private bool isLocalHost = true;
+        private PerPlayerLobbySettingsCoordinator perPlayerSettingsCoordinator;
 
         public ComboBoxItem[] PresetOptions => presetOptions;
 
@@ -57,6 +649,22 @@ namespace Shared
 
         public bool HasClientSettings => presetController?.HasClientSettings ?? false;
 
+        public bool HasHostSettingsActivation => presetController?.HasHostSettingsActivation ?? false;
+
+        public bool HasClientSettingsActivation => presetController?.HasClientSettingsActivation ?? false;
+
+        public bool HostSettingsEnabled
+        {
+            get => presetController?.HostSettingsEnabled ?? false;
+            set => presetController?.SetHostSettingsEnabled(value);
+        }
+
+        public bool ClientSettingsEnabled
+        {
+            get => presetController?.ClientSettingsEnabled ?? false;
+            set => presetController?.SetClientSettingsEnabled(value);
+        }
+
         public bool IsLocalSettingsHost => isLocalHost;
 
         public bool IsRealMultiplayerContext => isRealMultiplayer;
@@ -70,6 +678,12 @@ namespace Shared
 
         public bool CanEditClientSettings => true;
 
+        public bool CanToggleHostSettings =>
+            HasHostSettings && HasHostSettingsActivation && CanEditHostSettings;
+
+        public bool CanToggleClientSettings =>
+            HasClientSettings && HasClientSettingsActivation && CanEditClientSettings;
+
         public bool CanChangePreset => isLocalHost || HasClientSettings;
 
         public bool CanResetSettings => CanEditHostSettings || HasClientSettings;
@@ -93,6 +707,15 @@ namespace Shared
         public string PresetText =>
             ResolveSettingsUiText("Common.Preset", "Preset");
 
+        public string ModEnabledText =>
+            ResolveSettingsUiText("Common.EnableMod", "Enable Mod");
+
+        public string HostActivationLabelText =>
+            ResolveSettingsUiText("Common.HostActivationLabel", "(Host-)");
+
+        public string ClientActivationLabelText =>
+            ResolveSettingsUiText("Common.ClientActivationLabel", "(Client settings)");
+
         public Visibility ActionsScopeNoticeVisibility =>
             isRealMultiplayer && HasClientSettings
                 ? Visibility.Visible
@@ -116,6 +739,12 @@ namespace Shared
         public string EnableModHelpText =>
             ResolveSettingsUiText("Common.EnableModHelp", "Enables or disables this mod for the match.");
 
+        public string HostSettingsActivationHelpText =>
+            ResolveSettingsUiText("Common.HostSettingsActivationHelp", "Enables or disables all host-controlled settings of this mod.");
+
+        public string ClientSettingsActivationHelpText =>
+            ResolveSettingsUiText("Common.ClientSettingsActivationHelp", "Enables or disables all local and personal client settings of this mod.");
+
         public string PresetHelpText =>
             ResolveSettingsUiText("Common.PresetHelp", "Selects a saved preset. Clients change only their personal settings.");
 
@@ -127,6 +756,56 @@ namespace Shared
 
         protected virtual string ResolveSettingsUiText(string key, string fallback) => fallback;
 
+        /// <summary>
+        /// Declares the few domain-specific parts of personal settings. Transport,
+        /// player-slot ownership, lobby convergence and readiness stay in Shared.
+        /// </summary>
+        protected virtual void ConfigurePerPlayerLobbySettings(
+            PerPlayerLobbySettingsBuilder settings)
+        {
+        }
+
+        public bool IsPerPlayerLobbySettingsReady =>
+            perPlayerSettingsCoordinator?.IsReady ?? true;
+
+        public string PerPlayerLobbySettingsReadinessError =>
+            perPlayerSettingsCoordinator?.ReadinessError ?? string.Empty;
+
+        public void System_RequestPerPlayerSettingsPublish()
+        {
+            perPlayerSettingsCoordinator?.RequestPublish();
+        }
+
+        public bool System_ArePerPlayerSettingsReady(
+            IEnumerable<int> playerIds,
+            out string error)
+        {
+            if (perPlayerSettingsCoordinator == null)
+            {
+                error = string.Empty;
+                return true;
+            }
+
+            return perPlayerSettingsCoordinator.ArePlayersReady(playerIds, out error);
+        }
+
+#if SHARED_PRESET_TESTS
+        internal void System_TestObservePerPlayerLobby(
+            ulong? lobbyId,
+            IReadOnlyDictionary<int, ulong> players,
+            bool hasUnresolvedPlayers,
+            int localPlayerId,
+            bool preserveForMapTransition = false)
+        {
+            perPlayerSettingsCoordinator?.Observe(
+                lobbyId,
+                players,
+                hasUnresolvedPlayers,
+                localPlayerId,
+                preserveForMapTransition);
+        }
+#endif
+
         /// <summary>
         /// Authorizes a settings mutation before any backing state is changed.
         /// Preset and Trail snapshots are trusted internal applications; all other
@@ -244,6 +923,27 @@ namespace Shared
             presetController.Activate();
         }
 
+        internal void ActivatePerPlayerLobbySettings(ManualLogSource log, string modName)
+        {
+            if (perPlayerSettingsCoordinator != null)
+                throw new InvalidOperationException($"Per-player lobby settings for [{modName}] were already activated.");
+
+            var builder = new PerPlayerLobbySettingsBuilder(this);
+            ConfigurePerPlayerLobbySettings(builder);
+            perPlayerSettingsCoordinator = new PerPlayerLobbySettingsCoordinator(
+                this,
+                log,
+                modName,
+                builder.Build());
+            perPlayerSettingsCoordinator.Activate();
+        }
+
+        internal void DeactivatePerPlayerLobbySettings()
+        {
+            perPlayerSettingsCoordinator?.Deactivate();
+            perPlayerSettingsCoordinator = null;
+        }
+
         // Neutral reflection boundary used by optional mission coordinators.
         public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
             presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
@@ -309,6 +1009,11 @@ namespace Shared
             try
             {
                 base.OnPropertyChanged(name);
+
+                if (presetController?.IsHostSettingsActivationProperty(name) == true)
+                    base.OnPropertyChanged(nameof(HostSettingsEnabled));
+                if (presetController?.IsClientSettingsActivationProperty(name) == true)
+                    base.OnPropertyChanged(nameof(ClientSettingsEnabled));
             }
             finally
             {
@@ -333,10 +1038,16 @@ namespace Shared
             base.OnPropertyChanged(nameof(IsRealMultiplayerContext));
             base.OnPropertyChanged(nameof(HasHostSettings));
             base.OnPropertyChanged(nameof(HasClientSettings));
+            base.OnPropertyChanged(nameof(HasHostSettingsActivation));
+            base.OnPropertyChanged(nameof(HasClientSettingsActivation));
+            base.OnPropertyChanged(nameof(HostSettingsEnabled));
+            base.OnPropertyChanged(nameof(ClientSettingsEnabled));
             base.OnPropertyChanged(nameof(MissionPresetEditable));
             base.OnPropertyChanged(nameof(IsMissionPresetSelected));
             base.OnPropertyChanged(nameof(CanEditHostSettings));
             base.OnPropertyChanged(nameof(CanEditClientSettings));
+            base.OnPropertyChanged(nameof(CanToggleHostSettings));
+            base.OnPropertyChanged(nameof(CanToggleClientSettings));
             base.OnPropertyChanged(nameof(CanChangePreset));
             base.OnPropertyChanged(nameof(CanResetSettings));
             base.OnPropertyChanged(nameof(PresetVisibility));
@@ -389,6 +1100,8 @@ namespace Shared
             private readonly PropertyInfo[] persistedProperties;
             private readonly PropertyInfo[] hostProperties;
             private readonly PropertyInfo[] clientProperties;
+            private readonly PropertyInfo hostSettingsActivationProperty;
+            private readonly PropertyInfo clientSettingsActivationProperty;
             private readonly Dictionary<string, PropertyInfo> persistedPropertiesByName;
 
             private Dictionary<string, byte[]> defaults;
@@ -427,12 +1140,34 @@ namespace Shared
                     .ToDictionary(property => property.Name, StringComparer.Ordinal);
                 hostProperties = persistedProperties.Where(IsHostProperty).ToArray();
                 clientProperties = persistedProperties.Where(IsClientProperty).ToArray();
+                hostSettingsActivationProperty = FindSettingsActivationProperty(hostProperties, "EnableMod");
+                clientSettingsActivationProperty = FindSettingsActivationProperty(clientProperties, "EnableClientFeatures", "EnableMod");
             }
 
             public bool HasHostSettings => hostProperties.Length != 0;
 
             public bool HasClientSettings => clientProperties.Length != 0;
 
+            public bool HasHostSettingsActivation => hostSettingsActivationProperty != null;
+
+            public bool HasClientSettingsActivation => clientSettingsActivationProperty != null;
+
+            public bool HostSettingsEnabled => ReadSettingsActivation(hostSettingsActivationProperty);
+
+            public bool ClientSettingsEnabled => ReadSettingsActivation(clientSettingsActivationProperty);
+
+            public void SetHostSettingsEnabled(bool value) =>
+                WriteSettingsActivation(hostSettingsActivationProperty, value);
+
+            public void SetClientSettingsEnabled(bool value) =>
+                WriteSettingsActivation(clientSettingsActivationProperty, value);
+
+            public bool IsHostSettingsActivationProperty(string propertyName) =>
+                IsSettingsActivationProperty(hostSettingsActivationProperty, propertyName);
+
+            public bool IsClientSettingsActivationProperty(string propertyName) =>
+                IsSettingsActivationProperty(clientSettingsActivationProperty, propertyName);
+
             public bool IsApplyingSnapshot => applying;
 
             public bool IsHostPropertyName(string propertyName) =>
@@ -891,6 +1626,40 @@ namespace Shared
                 (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                     property.GetCustomAttribute<PresetLocalAttribute>() != null);
 
+            private static PropertyInfo FindSettingsActivationProperty(
+                IEnumerable<PropertyInfo> properties,
+                params string[] preferredNames)
+            {
+                foreach (string name in preferredNames)
+                {
+                    PropertyInfo property = properties.FirstOrDefault(item =>
+                        item.Name == name &&
+                        item.PropertyType == typeof(bool) &&
+                        item.CanRead &&
+                        item.CanWrite);
+                    if (property != null)
+                        return property;
+                }
+
+                return null;
+            }
+
+            private bool ReadSettingsActivation(PropertyInfo property) =>
+                property != null && (bool)property.GetValue(owner);
+
+            private void WriteSettingsActivation(PropertyInfo property, bool value)
+            {
+                if (property == null || ReadSettingsActivation(property) == value)
+                    return;
+
+                property.SetValue(owner, value);
+            }
+
+            private static bool IsSettingsActivationProperty(
+                PropertyInfo property,
+                string propertyName) =>
+                property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal);
+
             public static bool IsNetworkSyncInProgress()
             {
                 try
@@ -982,10 +1751,15 @@ namespace Shared
                 catch (Exception ex)
                 {
                     anchor.RollBack();
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/BuildingCosts.diff).
