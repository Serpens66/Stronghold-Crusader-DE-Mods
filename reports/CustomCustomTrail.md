# CustomCustomTrail release status

**Status:** code newer

- Release: [v1.3.31](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CustomCustomTrail/v1.3.31)
- Release commit: [97ad5db](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/97ad5db622ea02559c072dab6a50226eab93213d)
- Current main commit: [052884c](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/052884c545ea5a7388b629bab9add42d8bc7c4d0)

## Relevant changed files

- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/CustomCustomTrail.Core.dll`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/CustomCustomTrail.Core.pdb`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/CustomCustomTrail.dll`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/CustomCustomTrail.pdb`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/info.json`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ar.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/cs-CZ.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/de-DE.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/el-GR.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/en-US.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/es-ES.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/fr-FR.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/hu-HU.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/it-IT.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ja-JP.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ko-KR.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/nl-NL.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pl-PL.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pt-BR.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ru-RU.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/sv-SE.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/th-TH.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/tr-TR.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/uk-UA.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-CN.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-HK.txt`
- `CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml`
- `CustomCustomTrail/CustomCustomTrail.csproj`
- `CustomCustomTrail/info.json`
- `CustomCustomTrail/Locales/ar.txt`
- `CustomCustomTrail/Locales/cs-CZ.txt`
- `CustomCustomTrail/Locales/de-DE.txt`
- `CustomCustomTrail/Locales/el-GR.txt`
- `CustomCustomTrail/Locales/en-US.txt`
- `CustomCustomTrail/Locales/es-ES.txt`
- `CustomCustomTrail/Locales/fr-FR.txt`
- `CustomCustomTrail/Locales/hu-HU.txt`
- `CustomCustomTrail/Locales/it-IT.txt`
- `CustomCustomTrail/Locales/ja-JP.txt`
- `CustomCustomTrail/Locales/ko-KR.txt`
- `CustomCustomTrail/Locales/nl-NL.txt`
- `CustomCustomTrail/Locales/pl-PL.txt`
- `CustomCustomTrail/Locales/pt-BR.txt`
- `CustomCustomTrail/Locales/ru-RU.txt`
- `CustomCustomTrail/Locales/sv-SE.txt`
- `CustomCustomTrail/Locales/th-TH.txt`
- `CustomCustomTrail/Locales/tr-TR.txt`
- `CustomCustomTrail/Locales/uk-UA.txt`
- `CustomCustomTrail/Locales/zh-CN.txt`
- `CustomCustomTrail/Locales/zh-HK.txt`
- `CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml`
- `CustomCustomTrail/README.md`
- `CustomCustomTrail/src/CoopCustomizePacket.cs`
- `CustomCustomTrail/src/CustomCustomTrailPlugin.cs`
- `CustomCustomTrail/src/CustomCustomTrailRuntime.cs`
- `CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs`
- `CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs`
- `Shared/GameModeHelper.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`
- `Shared/SerpLocalization.cs`

Relevant localization keys: `Common.ClientActivationLabel`, `Common.ClientSettingsActivationHelp`, `Common.HostActivationLabel`, `Common.HostSettingsActivationHelp`, `CustomCustomTrail.ErrorParticipantsMismatch`, `CustomCustomTrail.ErrorParticipantsMissing`, `CustomCustomTrail.ErrorParticipantsNotReady`

## Diff

```diff
diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/info.json b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/info.json
index ac6f96e0..e5e64d07 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/info.json
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/info.json
@@ -3,10 +3,43 @@
   "Author": "Serpens66",
   "Name": "Custom Custom Trail",
   "Description": "Creates portable Coop Trails in the Trail Maker, synchronizes the selected package, and coordinates Custom Trail mod settings.",
-  "Version": "1.3.31",
+  "Version": "1.3.36",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.3.36",
+      "Changes": [
+        "Places the separate host and local-client activation switches visibly beside their corresponding settings headings."
+      ]
+    },
+    {
+      "Version": "1.3.35",
+      "Changes": [
+        "Keeps the selected Trail mod-settings preset active on clients across a host-triggered Coop map launch by synchronizing an authenticated launch transition."
+      ]
+    },
+    {
+      "Version": "1.3.34",
+      "Changes": [
+        "Recognizes normal Coop lobby members as human participants so matching ready package states no longer produce an empty participant audit.",
+        "Synchronizes the Coop Trail Customize transition from the host to clients through an authenticated, explicitly formatted lobby packet."
+      ]
+    },
+    {
+      "Version": "1.3.33",
+      "Changes": [
+        "Avoids constructing Vanilla's MainViewModel during early package initialization, preventing a selected Coop package from aborting Custom Custom Trail startup before its mission hooks are installed."
+      ]
+    },
+    {
+      "Version": "1.3.32",
+      "Changes": [
+        "Refreshes the visible Coop mission as soon as synchronized host package settings arrive, so clients no longer retain the previously selected Vanilla map.",
+        "Publishes the client's validated package status after synchronized host settings have finished applying, preventing a matching installed package from remaining falsely unreported.",
+        "Resolves participant status through the same Steam-ID-to-player-slot mapping as SyncPerPlayer and reports missing, differing, and still-unreported packages separately to the host."
+      ]
+    },
     {
       "Version": "1.3.31",
       "Changes": [

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ar.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ar.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ar.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ar.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/cs-CZ.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/cs-CZ.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/cs-CZ.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/cs-CZ.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/de-DE.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/de-DE.txt
index 8f6c2822..b327eb3b 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/de-DE.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/de-DE.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 Common.EnableModHelp=Aktiviert oder deaktiviert diese Mod lokal. Wenn sie deaktiviert ist, werden Custom-Trail-Sidecars und Coop-Ersatzmissionen nicht angewendet.
 CustomCustomTrail.PracticalEffects=Diese Mod speichert die Einstellungen unterstützter Mods zusammen mit Custom-Trail-Missionen und stellt sie beim Spielen wieder her. Im Traileditor exportiert die Option „Koop-Trail“ bis zu 40 Missionen als ein portables Paket. Der Host wählt ein installiertes Paket aus; jeder Teilnehmer benötigt identische Inhalte, bevor eine ersetzte Mission gestartet werden kann. Durch lokales Deaktivieren werden die Vanilla-Koop-Missionen wiederhergestellt und Trail-Modsettings ignoriert.
 CustomCustomTrail.HostOptions=HOST-OPTIONEN
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=Der lokale Paketinhalt unterscheidet
 CustomCustomTrail.ErrorPackageInvalid=Das ausgewählte Koop-Trail-Paket ist ungültig:
 CustomCustomTrail.ErrorPackageNotReady=Das ausgewählte Koop-Trail-Paket ist nicht bereit.
 CustomCustomTrail.ErrorParticipantNotReady=Mindestens einem Teilnehmer fehlt das ausgewählte Paket oder sein Inhalt weicht ab.
+CustomCustomTrail.ErrorParticipantsMissing=Paket fehlt bei:
+CustomCustomTrail.ErrorParticipantsMismatch=Paketinhalt weicht ab bei:
+CustomCustomTrail.ErrorParticipantsNotReady=Paketstatus noch nicht bereit bei:
 CustomCustomTrail.StartBlockedTitle=Koop-Trail kann nicht gestartet werden
 CustomCustomTrail.TrailMakerCoop=Koop-Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exportiert die ersten 40 vorhandenen Missionen als portables Koop-Paket. Missionen 1–10 werden Koop-Trail 1, 11–20 Trail 2, 21–30 Trail 3 und 31–40 Trail 4. Spätere Missionen bleiben normale Custom-Trail-Missionen. Die ersten beiden belegten Spielerslots werden Host und Gast.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/el-GR.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/el-GR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/el-GR.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/el-GR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/en-US.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/en-US.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/en-US.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/en-US.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/es-ES.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/es-ES.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/es-ES.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/es-ES.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/fr-FR.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/fr-FR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/fr-FR.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/fr-FR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/hu-HU.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/hu-HU.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/hu-HU.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/hu-HU.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/it-IT.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/it-IT.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/it-IT.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/it-IT.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ja-JP.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ja-JP.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ja-JP.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ja-JP.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ko-KR.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ko-KR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ko-KR.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ko-KR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/nl-NL.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/nl-NL.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/nl-NL.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/nl-NL.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pl-PL.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pl-PL.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pl-PL.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pl-PL.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pt-BR.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pt-BR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pt-BR.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/pt-BR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ru-RU.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ru-RU.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ru-RU.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/ru-RU.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/sv-SE.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/sv-SE.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/sv-SE.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/sv-SE.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/th-TH.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/th-TH.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/th-TH.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/th-TH.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/tr-TR.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/tr-TR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/tr-TR.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/tr-TR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/uk-UA.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/uk-UA.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/uk-UA.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/uk-UA.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-CN.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-CN.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-CN.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-CN.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-HK.txt b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-HK.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-HK.txt
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Locales/zh-HK.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml
index de9200bb..28e66764 100644
--- a/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml
+++ b/CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml
@@ -30,18 +30,15 @@
                 VerticalScrollBarVisibility="Auto">
     <StackPanel Margin="10">
       <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
-        <Border Style="{StaticResource ClientActivationBorder}">
-          <CheckBox IsEnabled="{Binding CanEditClientSettings}"
-                    IsChecked="{Binding EnableClientFeatures, Mode=TwoWay}"
-                    Content="{Binding EnableClientFeaturesText}"
-                    ToolTipService.ShowDuration="60000"
-                    ToolTip="{Binding EnableClientFeaturesHelpText}"
-                    Foreground="White"
-                    FontWeight="Bold"/>
+        <TextBlock Text="{Binding ModEnabledText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        <Border Style="{StaticResource HostActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleHostSettings}" IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}" Content="{Binding HostActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding HostSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
         </Border>
-        <TextBlock Text="{Binding PresetText}" Visibility="{Binding PresetVisibility}" Foreground="#CCCCCC" VerticalAlignment="Center" Margin="14,0,6,0"/>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="170"/>
-        <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" Padding="10,3" Margin="14,0,0,0"/>
+        <Border Style="{StaticResource ClientActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleClientSettings}" IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}" Content="{Binding ClientActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ClientSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        </Border>
+        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="170" VerticalAlignment="Center" Margin="14,0,0,0"/>
+        <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" HorizontalAlignment="Left" Padding="10,3" Margin="14,0,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFFFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
@@ -92,13 +89,7 @@
       </TextBlock>
       </StackPanel>
       <Separator Background="#444466" Margin="0,12"/>
-      <Grid Margin="0,0,0,6">
-        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
-        <TextBlock Grid.Column="0" Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" VerticalAlignment="Center"/>
-        <Border Grid.Column="1" Style="{StaticResource HostActivationBorder}" Margin="8,0,0,0">
-          <CheckBox IsEnabled="{Binding CanEditHostSettings}" IsChecked="{Binding EnableMod, Mode=TwoWay}" Content="{Binding EnableHostFeaturesText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding EnableHostFeaturesHelpText}" Foreground="White" FontWeight="Bold"/>
-        </Border>
-      </Grid>
+      <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>
       <Border Style="{StaticResource HostOptionsBorder}" HorizontalAlignment="Left">
         <StackPanel IsEnabled="{Binding CanEditHostSettings}">
           <TextBlock Text="{Binding CoopPackageText}" Foreground="White" Margin="0,0,0,4"/>

diff --git a/CustomCustomTrail/CustomCustomTrail.csproj b/CustomCustomTrail/CustomCustomTrail.csproj
index 5932124f..c08122f6 100644
--- a/CustomCustomTrail/CustomCustomTrail.csproj
+++ b/CustomCustomTrail/CustomCustomTrail.csproj
@@ -39,6 +39,7 @@
     <Reference Include="UnityEngine"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="UnityEngine.CoreModule"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="Assembly-CSharp"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
+    <Reference Include="com.rlabrecque.steamworks.net"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\com.rlabrecque.steamworks.net.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="Noesis.NoesisGUI"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\Noesis.NoesisGUI.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="SHCDESE"><HintPath>$(ExtenderDir)\SHCDESE.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="R3"><HintPath>$(ExtenderDir)\R3.dll</HintPath><Private>false</Private></Reference>
@@ -57,6 +58,7 @@
     <Compile Include="src\CustomCustomTrailPlugin.cs" />
     <Compile Include="src\CustomCustomTrailSettingsViewModel.cs" />
     <Compile Include="src\CustomCustomTrailRuntime.cs" />
+    <Compile Include="src\CoopCustomizePacket.cs" />
     <Compile Include="src\TrailMissionSettingsCoordinator.cs" />
     <Compile Include="src\MissionAssetResolver.cs" />
     <Compile Include="src\CoopTrailPackageExporter.cs" />

diff --git a/CustomCustomTrail/info.json b/CustomCustomTrail/info.json
index ac6f96e0..e5e64d07 100644
--- a/CustomCustomTrail/info.json
+++ b/CustomCustomTrail/info.json
@@ -3,10 +3,43 @@
   "Author": "Serpens66",
   "Name": "Custom Custom Trail",
   "Description": "Creates portable Coop Trails in the Trail Maker, synchronizes the selected package, and coordinates Custom Trail mod settings.",
-  "Version": "1.3.31",
+  "Version": "1.3.36",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.3.36",
+      "Changes": [
+        "Places the separate host and local-client activation switches visibly beside their corresponding settings headings."
+      ]
+    },
+    {
+      "Version": "1.3.35",
+      "Changes": [
+        "Keeps the selected Trail mod-settings preset active on clients across a host-triggered Coop map launch by synchronizing an authenticated launch transition."
+      ]
+    },
+    {
+      "Version": "1.3.34",
+      "Changes": [
+        "Recognizes normal Coop lobby members as human participants so matching ready package states no longer produce an empty participant audit.",
+        "Synchronizes the Coop Trail Customize transition from the host to clients through an authenticated, explicitly formatted lobby packet."
+      ]
+    },
+    {
+      "Version": "1.3.33",
+      "Changes": [
+        "Avoids constructing Vanilla's MainViewModel during early package initialization, preventing a selected Coop package from aborting Custom Custom Trail startup before its mission hooks are installed."
+      ]
+    },
+    {
+      "Version": "1.3.32",
+      "Changes": [
+        "Refreshes the visible Coop mission as soon as synchronized host package settings arrive, so clients no longer retain the previously selected Vanilla map.",
+        "Publishes the client's validated package status after synchronized host settings have finished applying, preventing a matching installed package from remaining falsely unreported.",
+        "Resolves participant status through the same Steam-ID-to-player-slot mapping as SyncPerPlayer and reports missing, differing, and still-unreported packages separately to the host."
+      ]
+    },
     {
       "Version": "1.3.31",
       "Changes": [

diff --git a/CustomCustomTrail/Locales/ar.txt b/CustomCustomTrail/Locales/ar.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/ar.txt
+++ b/CustomCustomTrail/Locales/ar.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/cs-CZ.txt b/CustomCustomTrail/Locales/cs-CZ.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/cs-CZ.txt
+++ b/CustomCustomTrail/Locales/cs-CZ.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/de-DE.txt b/CustomCustomTrail/Locales/de-DE.txt
index 8f6c2822..b327eb3b 100644
--- a/CustomCustomTrail/Locales/de-DE.txt
+++ b/CustomCustomTrail/Locales/de-DE.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 Common.EnableModHelp=Aktiviert oder deaktiviert diese Mod lokal. Wenn sie deaktiviert ist, werden Custom-Trail-Sidecars und Coop-Ersatzmissionen nicht angewendet.
 CustomCustomTrail.PracticalEffects=Diese Mod speichert die Einstellungen unterstützter Mods zusammen mit Custom-Trail-Missionen und stellt sie beim Spielen wieder her. Im Traileditor exportiert die Option „Koop-Trail“ bis zu 40 Missionen als ein portables Paket. Der Host wählt ein installiertes Paket aus; jeder Teilnehmer benötigt identische Inhalte, bevor eine ersetzte Mission gestartet werden kann. Durch lokales Deaktivieren werden die Vanilla-Koop-Missionen wiederhergestellt und Trail-Modsettings ignoriert.
 CustomCustomTrail.HostOptions=HOST-OPTIONEN
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=Der lokale Paketinhalt unterscheidet
 CustomCustomTrail.ErrorPackageInvalid=Das ausgewählte Koop-Trail-Paket ist ungültig:
 CustomCustomTrail.ErrorPackageNotReady=Das ausgewählte Koop-Trail-Paket ist nicht bereit.
 CustomCustomTrail.ErrorParticipantNotReady=Mindestens einem Teilnehmer fehlt das ausgewählte Paket oder sein Inhalt weicht ab.
+CustomCustomTrail.ErrorParticipantsMissing=Paket fehlt bei:
+CustomCustomTrail.ErrorParticipantsMismatch=Paketinhalt weicht ab bei:
+CustomCustomTrail.ErrorParticipantsNotReady=Paketstatus noch nicht bereit bei:
 CustomCustomTrail.StartBlockedTitle=Koop-Trail kann nicht gestartet werden
 CustomCustomTrail.TrailMakerCoop=Koop-Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exportiert die ersten 40 vorhandenen Missionen als portables Koop-Paket. Missionen 1–10 werden Koop-Trail 1, 11–20 Trail 2, 21–30 Trail 3 und 31–40 Trail 4. Spätere Missionen bleiben normale Custom-Trail-Missionen. Die ersten beiden belegten Spielerslots werden Host und Gast.

diff --git a/CustomCustomTrail/Locales/el-GR.txt b/CustomCustomTrail/Locales/el-GR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/el-GR.txt
+++ b/CustomCustomTrail/Locales/el-GR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/en-US.txt b/CustomCustomTrail/Locales/en-US.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/en-US.txt
+++ b/CustomCustomTrail/Locales/en-US.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/es-ES.txt b/CustomCustomTrail/Locales/es-ES.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/es-ES.txt
+++ b/CustomCustomTrail/Locales/es-ES.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/fr-FR.txt b/CustomCustomTrail/Locales/fr-FR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/fr-FR.txt
+++ b/CustomCustomTrail/Locales/fr-FR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/hu-HU.txt b/CustomCustomTrail/Locales/hu-HU.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/hu-HU.txt
+++ b/CustomCustomTrail/Locales/hu-HU.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/it-IT.txt b/CustomCustomTrail/Locales/it-IT.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/it-IT.txt
+++ b/CustomCustomTrail/Locales/it-IT.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/ja-JP.txt b/CustomCustomTrail/Locales/ja-JP.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/ja-JP.txt
+++ b/CustomCustomTrail/Locales/ja-JP.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/ko-KR.txt b/CustomCustomTrail/Locales/ko-KR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/ko-KR.txt
+++ b/CustomCustomTrail/Locales/ko-KR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/nl-NL.txt b/CustomCustomTrail/Locales/nl-NL.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/nl-NL.txt
+++ b/CustomCustomTrail/Locales/nl-NL.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/pl-PL.txt b/CustomCustomTrail/Locales/pl-PL.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/pl-PL.txt
+++ b/CustomCustomTrail/Locales/pl-PL.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/pt-BR.txt b/CustomCustomTrail/Locales/pt-BR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/pt-BR.txt
+++ b/CustomCustomTrail/Locales/pt-BR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/ru-RU.txt b/CustomCustomTrail/Locales/ru-RU.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/ru-RU.txt
+++ b/CustomCustomTrail/Locales/ru-RU.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/sv-SE.txt b/CustomCustomTrail/Locales/sv-SE.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/sv-SE.txt
+++ b/CustomCustomTrail/Locales/sv-SE.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/th-TH.txt b/CustomCustomTrail/Locales/th-TH.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/th-TH.txt
+++ b/CustomCustomTrail/Locales/th-TH.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/tr-TR.txt b/CustomCustomTrail/Locales/tr-TR.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/tr-TR.txt
+++ b/CustomCustomTrail/Locales/tr-TR.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/uk-UA.txt b/CustomCustomTrail/Locales/uk-UA.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/uk-UA.txt
+++ b/CustomCustomTrail/Locales/uk-UA.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/zh-CN.txt b/CustomCustomTrail/Locales/zh-CN.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/zh-CN.txt
+++ b/CustomCustomTrail/Locales/zh-CN.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Locales/zh-HK.txt b/CustomCustomTrail/Locales/zh-HK.txt
index c1da30b1..96fec5ca 100644
--- a/CustomCustomTrail/Locales/zh-HK.txt
+++ b/CustomCustomTrail/Locales/zh-HK.txt
@@ -1,4 +1,8 @@
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.EnableModHelp=Enables or disables this mod locally. When disabled, Custom Trail sidecars and Coop mission replacements are not applied.
 CustomCustomTrail.PracticalEffects=This mod saves supported mod settings with Custom Trail missions and restores them when played. In the Trail Maker, the Coop Trail option exports up to 40 missions as one portable package. The host selects an installed package; every participant must have identical contents before a replaced mission can start. Disabling the mod locally restores Vanilla Coop missions and ignores Trail mod settings.
 CustomCustomTrail.HostOptions=HOST OPTIONS
@@ -21,6 +25,9 @@ CustomCustomTrail.ErrorFingerprintMismatch=The local package contents differ fro
 CustomCustomTrail.ErrorPackageInvalid=The selected Coop Trail package is invalid:
 CustomCustomTrail.ErrorPackageNotReady=The selected Coop Trail package is not ready.
 CustomCustomTrail.ErrorParticipantNotReady=At least one participant is missing the selected package or has different contents.
+CustomCustomTrail.ErrorParticipantsMissing=Package missing for:
+CustomCustomTrail.ErrorParticipantsMismatch=Package contents differ for:
+CustomCustomTrail.ErrorParticipantsNotReady=Package status not ready for:
 CustomCustomTrail.StartBlockedTitle=Coop Trail cannot start
 CustomCustomTrail.TrailMakerCoop=Coop Trail
 CustomCustomTrail.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.

diff --git a/CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml b/CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml
index de9200bb..28e66764 100644
--- a/CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml
+++ b/CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml
@@ -30,18 +30,15 @@
                 VerticalScrollBarVisibility="Auto">
     <StackPanel Margin="10">
       <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
-        <Border Style="{StaticResource ClientActivationBorder}">
-          <CheckBox IsEnabled="{Binding CanEditClientSettings}"
-                    IsChecked="{Binding EnableClientFeatures, Mode=TwoWay}"
-                    Content="{Binding EnableClientFeaturesText}"
-                    ToolTipService.ShowDuration="60000"
-                    ToolTip="{Binding EnableClientFeaturesHelpText}"
-                    Foreground="White"
-                    FontWeight="Bold"/>
+        <TextBlock Text="{Binding ModEnabledText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        <Border Style="{StaticResource HostActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleHostSettings}" IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}" Content="{Binding HostActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding HostSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
         </Border>
-        <TextBlock Text="{Binding PresetText}" Visibility="{Binding PresetVisibility}" Foreground="#CCCCCC" VerticalAlignment="Center" Margin="14,0,6,0"/>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="170"/>
-        <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" Padding="10,3" Margin="14,0,0,0"/>
+        <Border Style="{StaticResource ClientActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleClientSettings}" IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}" Content="{Binding ClientActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ClientSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        </Border>
+        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="170" VerticalAlignment="Center" Margin="14,0,0,0"/>
+        <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" HorizontalAlignment="Left" Padding="10,3" Margin="14,0,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFFFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
@@ -92,13 +89,7 @@
       </TextBlock>
       </StackPanel>
       <Separator Background="#444466" Margin="0,12"/>
-      <Grid Margin="0,0,0,6">
-        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
-        <TextBlock Grid.Column="0" Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" VerticalAlignment="Center"/>
-        <Border Grid.Column="1" Style="{StaticResource HostActivationBorder}" Margin="8,0,0,0">
-          <CheckBox IsEnabled="{Binding CanEditHostSettings}" IsChecked="{Binding EnableMod, Mode=TwoWay}" Content="{Binding EnableHostFeaturesText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding EnableHostFeaturesHelpText}" Foreground="White" FontWeight="Bold"/>
-        </Border>
-      </Grid>
+      <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>
       <Border Style="{StaticResource HostOptionsBorder}" HorizontalAlignment="Left">
         <StackPanel IsEnabled="{Binding CanEditHostSettings}">
           <TextBlock Text="{Binding CoopPackageText}" Foreground="White" Margin="0,0,0,4"/>

diff --git a/CustomCustomTrail/README.md b/CustomCustomTrail/README.md
index a6521b18..995e2231 100644
--- a/CustomCustomTrail/README.md
+++ b/CustomCustomTrail/README.md
@@ -36,7 +36,7 @@ Unter den Modsettings wählt der Host „Vanilla – kein eigenes Paket“ oder
 
 Im Koop-Pfadmenü wird der beim Export gespeicherte Kartenname als Missionsname angezeigt. Für jeden durch das Paket belegten Koop-Trail ersetzt der Paketname außerdem die jeweilige Vanilla-Trailüberschrift; unbelegte Trails behalten ihren Vanilla-Namen.
 
-Paket-ID und SHA-256-Inhaltsfingerprint werden synchronisiert. Jeder Teilnehmer prüft sein lokales Paket. Bei einem ersetzten Missionsplatz blockiert der Mod `Ready`, `ReadyLock` und beim Host `Play`, solange einem Teilnehmer das Paket fehlt, es beschädigt ist oder vom Hostinhalt abweicht. Nicht belegte Paketplätze bleiben Vanilla und benötigen das Paket für ihren Start nicht.
+Paket-ID und SHA-256-Inhaltsfingerprint werden synchronisiert. Jeder Teilnehmer prüft sein lokales Paket. Nach dem Beitritt aktualisiert der Client die bereits sichtbare Koop-Mission nochmals mit den empfangenen Hostdaten und meldet ein fehlendes oder abweichendes Paket sofort. Bei einem ersetzten Missionsplatz blockiert der Mod `Ready`, `ReadyLock` und beim Host `Play`, solange einem Teilnehmer das Paket fehlt, es beschädigt ist oder vom Hostinhalt abweicht. Die Hostmeldung unterscheidet fehlende und inhaltlich abweichende Pakete samt Spielernamen. Nicht belegte Paketplätze bleiben Vanilla und benötigen das Paket für ihren Start nicht.
 
 Das Paket wird nicht über das Spielnetz übertragen. Zum Verteilen muss der vollständige Custom-Trail-Ordner kopiert werden.
 

diff --git a/CustomCustomTrail/src/CoopCustomizePacket.cs b/CustomCustomTrail/src/CoopCustomizePacket.cs
new file mode 100644
index 00000000..a5b43d3a
--- /dev/null
+++ b/CustomCustomTrail/src/CoopCustomizePacket.cs
@@ -0,0 +1,61 @@
+using MessagePack;
+using MessagePack.Formatters;
+
+namespace CustomCustomTrail
+{
+    [MessagePackObject]
+    [MessagePackFormatter(typeof(CoopCustomizePacketFormatter))]
+    public sealed class CoopCustomizePacket
+    {
+        [Key(0)] public int ProtocolVersion;
+        [Key(1)] public int TrailId;
+        [Key(2)] public int MissionId;
+        [Key(3)] public bool Launch;
+    }
+
+    public sealed class CoopCustomizePacketFormatter : IMessagePackFormatter<CoopCustomizePacket>
+    {
+        private const int FieldCount = 4;
+
+        public void Serialize(
+            ref MessagePackWriter writer,
+            CoopCustomizePacket value,
+            MessagePackSerializerOptions options)
+        {
+            if (value == null)
+            {
+                writer.WriteNil();
+                return;
+            }
+
+            writer.WriteArrayHeader(FieldCount);
+            writer.Write(value.ProtocolVersion);
+            writer.Write(value.TrailId);
+            writer.Write(value.MissionId);
+            writer.Write(value.Launch);
+        }
+
+        public CoopCustomizePacket Deserialize(
+            ref MessagePackReader reader,
+            MessagePackSerializerOptions options)
+        {
+            if (reader.TryReadNil())
+                return null;
+
+            int fieldCount = reader.ReadArrayHeader();
+            var packet = new CoopCustomizePacket();
+            for (int index = 0; index < fieldCount; index++)
+            {
+                switch (index)
+                {
+                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
+                    case 1: packet.TrailId = reader.ReadInt32(); break;
+                    case 2: packet.MissionId = reader.ReadInt32(); break;
+                    case 3: packet.Launch = reader.ReadBoolean(); break;
+                    default: reader.Skip(); break;
+                }
+            }
+            return packet;
+        }
+    }
+}

diff --git a/CustomCustomTrail/src/CustomCustomTrailPlugin.cs b/CustomCustomTrail/src/CustomCustomTrailPlugin.cs
index 30940481..5f8cb6e8 100644
--- a/CustomCustomTrail/src/CustomCustomTrailPlugin.cs
+++ b/CustomCustomTrail/src/CustomCustomTrailPlugin.cs
@@ -14,7 +14,7 @@ namespace CustomCustomTrail
     {
         public const string PluginGuid = "CustomCustomTrail_Serp";
         public const string PluginName = "Custom Custom Trail";
-        public const string PluginVersion = "1.3.31";
+        public const string PluginVersion = "1.3.36";
         public const bool CustomCustomTrailModSettingsOptOut = true;
 
         private static CustomCustomTrailRuntime runtime;

diff --git a/CustomCustomTrail/src/CustomCustomTrailRuntime.cs b/CustomCustomTrail/src/CustomCustomTrailRuntime.cs
index 5a8b384b..8a4d74c0 100644
--- a/CustomCustomTrail/src/CustomCustomTrailRuntime.cs
+++ b/CustomCustomTrail/src/CustomCustomTrailRuntime.cs
@@ -20,9 +20,34 @@ namespace CustomCustomTrail
         private delegate void CoopMissionChangedDelegate(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
         private delegate void ButtonClickedDelegate(FRONT_Multiplayer self, string command);
 
+        private sealed class HumanPackageState
+        {
+            public HumanPackageState(
+                string name,
+                int playerId,
+                string status,
+                bool skirmishMember,
+                bool skirmishHumanMember)
+            {
+                Name = name;
+                PlayerId = playerId;
+                Status = status ?? string.Empty;
+                SkirmishMember = skirmishMember;
+                SkirmishHumanMember = skirmishHumanMember;
+            }
+
+            public string Name { get; }
+            public int PlayerId { get; }
+            public string Status { get; }
+            public bool SkirmishMember { get; }
+            public bool SkirmishHumanMember { get; }
+        }
+
         private static readonly FieldInfo[] CoopTrailFields = Enumerable.Range(1, 4)
             .Select(index => typeof(FRONT_Multiplayer).GetField("CoopTrail" + index, BindingFlags.Static | BindingFlags.NonPublic))
             .ToArray();
+        private static readonly FieldInfo MainViewModelInstanceField = typeof(MainViewModel)
+            .GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
         private static readonly MethodInfo UpdateHostInfoMethod = typeof(FRONT_Multiplayer).GetMethod(
             "UpdateHostInfo",
             BindingFlags.Instance | BindingFlags.NonPublic,
@@ -55,6 +80,7 @@ namespace CustomCustomTrail
         private bool refreshingCatalog;
         private bool coopLaunchPending;
         private bool coopMapActive;
+        private string lastShownLocalBlockSignature = string.Empty;
         private bool enabled;
 
         public CustomCustomTrailRuntime(
@@ -73,6 +99,7 @@ namespace CustomCustomTrail
             missionSettingsCoordinator = new TrailMissionSettingsCoordinator(log, enabled, settings.IsTrailModEnabled);
             missionSettingsCoordinator.CoopPackagesChanged += OnActiveCoopPackageChanged;
             missionSettingsCoordinator.CoopSetupOpened += OnCoopSetupOpened;
+            missionSettingsCoordinator.CoopLaunchReceived += OnCoopLaunchReceived;
             missionSettingsCoordinator.Initialize();
             RefreshModCompatibility();
             settings.ActiveCoopPackageChanged += OnActiveCoopPackageChanged;
@@ -110,7 +137,9 @@ namespace CustomCustomTrail
             if (!value)
             {
                 RestoreVanillaMissions();
-                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled"));
+                SetLocalPackageError(
+                    CustomCustomTrailSettingsViewModel.DisabledStatus,
+                    SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled"));
             }
             else
             {
@@ -140,6 +169,7 @@ namespace CustomCustomTrail
             {
                 missionSettingsCoordinator.CoopPackagesChanged -= OnActiveCoopPackageChanged;
                 missionSettingsCoordinator.CoopSetupOpened -= OnCoopSetupOpened;
+                missionSettingsCoordinator.CoopLaunchReceived -= OnCoopLaunchReceived;
             }
             missionSettingsCoordinator?.ExitContext(force: true);
             missionSettingsCoordinator?.Dispose();
@@ -212,11 +242,24 @@ namespace CustomCustomTrail
                     BlockLaunch(command, SerpLocalization.Get("CustomCustomTrail.ErrorPackageNotReady"));
                     return;
                 }
-                if (IsStartCommand(command) && !self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost &&
-                    !AreAllHumanPlayersPackageReady(self))
+                if (IsStartCommand(command) && !self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost)
                 {
-                    BlockLaunch(command, SerpLocalization.Get("CustomCustomTrail.ErrorParticipantNotReady"));
-                    return;
+                    List<HumanPackageState> participantStates = GetHumanPackageStates(self);
+                    LogInfo("Custom Coop package participant audit: " +
+                        DescribeHumanPackageStates(self, participantStates));
+                    if (!settings.System_ArePerPlayerSettingsReady(
+                            participantStates.Select(state => state.PlayerId),
+                            out string syncError))
+                    {
+                        LogError("Blocked custom Coop package launch because Shared personal settings are incomplete: " + syncError);
+                        BlockLaunch(command, SerpLocalization.Get("CustomCustomTrail.ErrorPackageNotReady"));
+                        return;
+                    }
+                    if (!AreAllHumanPlayersPackageReady(participantStates))
+                    {
+                        BlockLaunch(command, GetParticipantPackageBlockReason(participantStates));
+                        return;
+                    }
                 }
             }
             if (enabled && selected != null && IsLaunchCommand(command))
@@ -230,6 +273,12 @@ namespace CustomCustomTrail
                     {
                         coopLaunchPending = true;
                         coopMapActive = false;
+                        if (!self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost)
+                        {
+                            missionSettingsCoordinator.BroadcastCoopLaunch(
+                                selected.Loaded.TrailNumber - 1,
+                                selected.Loaded.MissionNumber);
+                        }
                     }
                 }
                 catch (Exception exception)
@@ -278,11 +327,13 @@ namespace CustomCustomTrail
                     {
                         settings.ActiveCoopPackageFingerprint = hostPackage.Manifest.ContentFingerprint;
                         settings.ActiveCoopPackageMissionCount = hostPackage.Manifest.MissionCount;
+                        settings.ActiveCoopPackageDescriptor = ExpectedPackageDescriptor();
                     }
                     else
                     {
                         settings.ActiveCoopPackageFingerprint = string.Empty;
                         settings.ActiveCoopPackageMissionCount = 0;
+                        settings.ActiveCoopPackageDescriptor = ExpectedPackageDescriptor();
                     }
                 }
                 ApplyActivePackage();
@@ -320,28 +371,42 @@ namespace CustomCustomTrail
 
             if (!enabled)
             {
-                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled"));
+                SetLocalPackageError(CustomCustomTrailSettingsViewModel.DisabledStatus, SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled"));
                 return;
             }
             if (string.IsNullOrEmpty(settings.ActiveCoopPackageId))
             {
                 localPackageError = string.Empty;
-                settings.SetLocalPackageStatus("OK|VANILLA");
+                SetLocalPackageStatus("OK|VANILLA");
+                return;
+            }
+            if (!GameNetworkAPI.IsLocalHost() &&
+                !string.Equals(settings.ActiveCoopPackageDescriptor, ExpectedPackageDescriptor(), StringComparison.Ordinal))
+            {
+                SetLocalPackageError(CustomCustomTrailSettingsViewModel.WaitingStatus, SerpLocalization.Get("CustomCustomTrail.StatusChecking"));
                 return;
             }
             if (string.IsNullOrEmpty(settings.ActiveCoopPackageFingerprint))
             {
-                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.StatusChecking"));
+                SetLocalPackageError(CustomCustomTrailSettingsViewModel.WaitingStatus, SerpLocalization.Get("CustomCustomTrail.StatusChecking"));
                 return;
             }
             if (!packageCatalog.Packages.TryGetValue(settings.ActiveCoopPackageId, out activePackage))
             {
-                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorPackageMissing") + " " + settings.ActiveCoopPackageId);
+                SetLocalPackageError(
+                    CustomCustomTrailSettingsViewModel.MissingStatus,
+                    SerpLocalization.Get("CustomCustomTrail.ErrorPackageMissing") + " " + settings.ActiveCoopPackageId);
+                RefreshVisibleCoopMissionAfterPackageChange();
+                ShowLocalPackageBlockAfterSync();
                 return;
             }
             if (!string.Equals(activePackage.Manifest.ContentFingerprint, settings.ActiveCoopPackageFingerprint, StringComparison.OrdinalIgnoreCase))
             {
-                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorFingerprintMismatch"));
+                SetLocalPackageError(
+                    CustomCustomTrailSettingsViewModel.MismatchStatus,
+                    SerpLocalization.Get("CustomCustomTrail.ErrorFingerprintMismatch"));
+                RefreshVisibleCoopMissionAfterPackageChange();
+                ShowLocalPackageBlockAfterSync();
                 return;
             }
 
@@ -378,12 +443,18 @@ namespace CustomCustomTrail
             catch (Exception exception)
             {
                 RestoreVanillaMissions();
-                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorPackageInvalid") + " " + exception.Message);
+                SetLocalPackageError(
+                    CustomCustomTrailSettingsViewModel.InvalidStatusPrefix + exception.Message,
+                    SerpLocalization.Get("CustomCustomTrail.ErrorPackageInvalid") + " " + exception.Message);
                 LogError("Selected Coop Trail package is unusable: " + exception);
+                RefreshVisibleCoopMissionAfterPackageChange();
+                ShowLocalPackageBlockAfterSync();
                 return;
             }
             localPackageError = string.Empty;
-            settings.SetLocalPackageStatus(ExpectedReadyStatus());
+            SetLocalPackageStatus(ExpectedReadyStatus());
+            RefreshVisibleCoopMissionAfterPackageChange();
+            ShowLocalPackageBlockAfterSync();
         }
 
         private void RestoreVanillaMissions()
@@ -462,27 +533,109 @@ namespace CustomCustomTrail
         private string ExpectedReadyStatus() =>
             "OK|" + settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint;
 
-        private bool AreAllHumanPlayersPackageReady(FRONT_Multiplayer self)
+        private string ExpectedPackageDescriptor() =>
+            settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint + "|" + settings.ActiveCoopPackageMissionCount;
+
+        private List<HumanPackageState> GetHumanPackageStates(FRONT_Multiplayer self)
         {
-            string expected = ExpectedReadyStatus();
-            int expectedHumanPlayers = 0;
+            var result = new List<HumanPackageState>();
+            if (self?.currentLobby?.members == null)
+                return result;
+
             foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
             {
-                if (member != null && member.SkirmishHumanMember)
-                    expectedHumanPlayers++;
+                // Vanilla treats every non-Skirmish lobby member as human. The separate
+                // SkirmishHumanMember flag only distinguishes humans from Skirmish AIs.
+                if (member == null || member.dummyToBeKicked ||
+                    (!member.SkirmishHumanMember && member.SkirmishMember))
+                    continue;
+
+                // The Vanilla this_player_to_SteamID_mapping is not populated reliably in the
+                // Coop lobby. Use the same Steam-ID mapping as SyncPerPlayer packet handling.
+                int playerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
+                string status = playerId > 0 && playerId < settings.CoopPackageStatusData.Length
+                    ? settings.CoopPackageStatusData[playerId] ?? string.Empty
+                    : string.Empty;
+                string name = string.IsNullOrWhiteSpace(member.name)
+                    ? "Player " + (playerId > 0 ? playerId.ToString() : "?")
+                    : member.name;
+                result.Add(new HumanPackageState(
+                    name,
+                    playerId,
+                    status,
+                    member.SkirmishMember,
+                    member.SkirmishHumanMember));
             }
+            return result;
+        }
 
-            int checkedHumanPlayers = 0;
-            for (int playerId = 1; playerId < settings.CoopPackageStatusData.Length; playerId++)
+        private bool AreAllHumanPlayersPackageReady(IReadOnlyCollection<HumanPackageState> states) =>
+            states.Count > 0 && states.All(state =>
+                state.PlayerId > 0 &&
+                string.Equals(state.Status, ExpectedReadyStatus(), StringComparison.Ordinal));
+
+        private string GetParticipantPackageBlockReason(IReadOnlyCollection<HumanPackageState> states)
+        {
+            string expected = ExpectedReadyStatus();
+            var missing = new List<string>();
+            var mismatched = new List<string>();
+            var notReady = new List<string>();
+            foreach (HumanPackageState state in states)
             {
-                Platform_Multiplayer.MPLobbyMember member = self.currentLobby.GetLobbyMemberFromThis_PlayerID(playerId);
-                if (member == null || !member.SkirmishHumanMember)
+                if (state.PlayerId > 0 && string.Equals(state.Status, expected, StringComparison.Ordinal))
                     continue;
-                checkedHumanPlayers++;
-                if (!string.Equals(settings.CoopPackageStatusData[playerId], expected, StringComparison.Ordinal))
-                    return false;
+                if (string.Equals(state.Status, CustomCustomTrailSettingsViewModel.MissingStatus, StringComparison.Ordinal))
+                    missing.Add(state.Name);
+                else if (string.Equals(state.Status, CustomCustomTrailSettingsViewModel.MismatchStatus, StringComparison.Ordinal) ||
+                    state.Status.StartsWith("OK|", StringComparison.Ordinal))
+                    mismatched.Add(state.Name);
+                else
+                    notReady.Add(state.Name);
+            }
+
+            var reasons = new List<string>();
+            if (missing.Count != 0)
+                reasons.Add(SerpLocalization.Get("CustomCustomTrail.ErrorParticipantsMissing") + " " + string.Join(", ", missing));
+            if (mismatched.Count != 0)
+                reasons.Add(SerpLocalization.Get("CustomCustomTrail.ErrorParticipantsMismatch") + " " + string.Join(", ", mismatched));
+            if (notReady.Count != 0)
+                reasons.Add(SerpLocalization.Get("CustomCustomTrail.ErrorParticipantsNotReady") + " " + string.Join(", ", notReady));
+            return reasons.Count == 0
+                ? SerpLocalization.Get("CustomCustomTrail.ErrorPackageNotReady")
+                : string.Join("\r\n", reasons);
+        }
+
+        private string DescribeHumanPackageStates(
+            FRONT_Multiplayer self,
+            IReadOnlyCollection<HumanPackageState> states)
+        {
+            int lobbyMemberCount = self?.currentLobby?.members?.Count ?? -1;
+            if (states.Count == 0)
+                return "lobbyMembers=" + lobbyMemberCount + ", humans=none";
+            string expected = ExpectedReadyStatus();
+            return "lobbyMembers=" + lobbyMemberCount + ", humans=" + string.Join("; ", states.Select(state =>
+                state.Name + "[playerId=" + state.PlayerId +
+                ", kind=" + (state.SkirmishMember ? "skirmish-human" : "coop-human") +
+                ", skirmishHuman=" + state.SkirmishHumanMember +
+                ", status=" + DescribePackageStatus(state.Status, expected) + "]"));
+        }
+
+        private static string DescribePackageStatus(string status, string expected)
+        {
+            if (string.Equals(status, expected, StringComparison.Ordinal))
+                return "ready";
+            if (string.Equals(status, CustomCustomTrailSettingsViewModel.MissingStatus, StringComparison.Ordinal))
+                return "missing";
+            if (string.Equals(status, CustomCustomTrailSettingsViewModel.MismatchStatus, StringComparison.Ordinal) ||
+                (status ?? string.Empty).StartsWith("OK|", StringComparison.Ordinal))
+            {
+                return "mismatch";
             }
-            return expectedHumanPlayers > 0 && checkedHumanPlayers == expectedHumanPlayers;
+            if ((status ?? string.Empty).StartsWith(CustomCustomTrailSettingsViewModel.InvalidStatusPrefix, StringComparison.Ordinal))
+                return "invalid";
+            if (string.Equals(status, CustomCustomTrailSettingsViewModel.DisabledStatus, StringComparison.Ordinal))
+                return "disabled";
+            return string.IsNullOrEmpty(status) ? "unreported" : "waiting";
         }
 
         private void AppendPackageErrorToDescription(int zeroBasedTrailId, int oneBasedMissionId)
@@ -499,10 +652,57 @@ namespace CustomCustomTrail
             ? SerpLocalization.Get("CustomCustomTrail.ErrorPackageNotReady")
             : localPackageError;
 
-        private void SetLocalPackageError(string error)
+        private void SetLocalPackageError(string status, string error)
         {
             localPackageError = error ?? string.Empty;
-            settings.SetLocalPackageStatus("ERROR|" + localPackageError);
+            SetLocalPackageStatus(status);
+        }
+
+        private void SetLocalPackageStatus(string status)
+        {
+            settings.SetLocalPackageStatus(status);
+            // A derived status can remain textually identical after host settings
+            // arrive. Requesting Shared publication still advertises it for the
+            // current player slot after lobby convergence.
+            settings.System_RequestPerPlayerSettingsPublish();
+        }
+
+        private void RefreshVisibleCoopMissionAfterPackageChange()
+        {
+            // Do not use Instance here: its getter constructs Vanilla's view model before the UI is ready.
+            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
+            if (self?.currentLobby == null || !self.currentLobby.coopTrailGame)
+                return;
+            int trailId = self.currentLobby.coopTrailID;
+            int missionId = self.currentLobby.coopSelectedMission;
+            if (trailId < 0 || trailId >= CoopTrailFields.Length || missionId < 1 || missionId > 10)
+                return;
+
+            // Host package settings can arrive after AutoJoinLobby selected Vanilla data.
+            // Re-run the same Vanilla selection path so map, AIs, title and Trail preset agree.
+            self.CoopMissionChanged(trailId, missionId, false);
+            LogInfo("Refreshed visible Coop mission after package settings changed: Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ".");
+        }
+
+        private void ShowLocalPackageBlockAfterSync()
+        {
+            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
+            if (GameNetworkAPI.IsLocalHost() || !CurrentSlotRequiresPackage(self) || IsLocalPackageReady() ||
+                string.IsNullOrEmpty(settings.ActiveCoopPackageFingerprint))
+            {
+                lastShownLocalBlockSignature = string.Empty;
+                return;
+            }
+            string status = settings.CoopPackageStatus ?? string.Empty;
+            if (!status.StartsWith(CustomCustomTrailSettingsViewModel.ErrorStatusPrefix, StringComparison.Ordinal))
+                return;
+            string signature = settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint + "|" +
+                self.currentLobby.coopTrailID + "|" + self.currentLobby.coopSelectedMission + "|" + status;
+            if (string.Equals(lastShownLocalBlockSignature, signature, StringComparison.Ordinal))
+                return;
+            lastShownLocalBlockSignature = signature;
+            LogError("Showing immediate custom Coop package validation failure after host settings sync: " + GetLocalBlockReason());
+            ShowBlockedMessage(GetLocalBlockReason());
         }
 
         private static void ShowBlockedMessage(string message)
@@ -513,6 +713,9 @@ namespace CustomCustomTrail
                 message);
         }
 
+        private static MainViewModel GetExistingMainViewModel() =>
+            MainViewModelInstanceField?.GetValue(null) as MainViewModel;
+
         private void BlockLaunch(string command, string reason)
         {
             LogError("Blocked custom Coop mission " + command + ": " + reason);
@@ -547,6 +750,28 @@ namespace CustomCustomTrail
             LogInfo("Custom Coop mission map started; retaining its Trail mod-settings preset.");
         }
 
+        private void OnCoopLaunchReceived(int trailId, int missionId)
+        {
+            if (!enabled || !resolved.TryGetValue(MissionCatalog.ToKey(trailId + 1, missionId), out ResolvedMission mission))
+            {
+                LogError($"Ignored authenticated Coop Trail launch for unavailable Trail{trailId + 1}/{missionId:00}.");
+                return;
+            }
+            if (!IsLocalPackageReady())
+            {
+                LogError($"Ignored authenticated Coop Trail launch for Trail{trailId + 1}/{missionId:00} because the local package is not ready.");
+                return;
+            }
+
+            // Clients do not execute the host's COOP_START button handler. The authenticated
+            // transition supplies the missing launch boundary before OnUnloadMap clears presets.
+            selected = mission;
+            ActivateSelectedMissionSettings(editable: false, source: "authenticated host Coop launch");
+            coopLaunchPending = true;
+            coopMapActive = false;
+            LogInfo($"Prepared authenticated Coop Trail launch trail={trailId + 1}, mission={missionId}; retaining its Trail preset across map unload.");
+        }
+
         private void OnMapUnloaded()
         {
             if (coopLaunchPending && !coopMapActive)

diff --git a/CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs b/CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs
index 862ab44e..eef42e34 100644
--- a/CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs
+++ b/CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs
@@ -16,16 +16,25 @@ namespace CustomCustomTrail
 {
     public sealed class CustomCustomTrailSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
     {
+        internal const string ErrorStatusPrefix = "ERROR|";
+        internal const string MissingStatus = "ERROR|MISSING";
+        internal const string MismatchStatus = "ERROR|MISMATCH";
+        internal const string InvalidStatusPrefix = "ERROR|INVALID|";
+        internal const string DisabledStatus = "ERROR|DISABLED";
+        internal const string WaitingStatus = "WAITING";
+
         private bool enableClientFeatures = true;
         private bool enableMod = true;
         private string activeCoopPackageId = string.Empty;
         private string activeCoopPackageFingerprint = string.Empty;
         private int activeCoopPackageMissionCount;
+        private string activeCoopPackageDescriptor = string.Empty;
         private ComboBoxItem[] coopPackageOptions = Array.Empty<ComboBoxItem>();
         private string[] coopPackageIds = Array.Empty<string>();
         private string[] disabledTrailModIds = Array.Empty<string>();
         private TrailModSelectionItem[] compatibleTrailMods = Array.Empty<TrailModSelectionItem>();
         private string incompatibleTrailModsText = string.Empty;
+        private string coopPackageStatus = string.Empty;
 
         public CustomCustomTrailSettingsViewModel()
         {
@@ -41,6 +50,16 @@ namespace CustomCustomTrail
         protected override string ResolveSettingsUiText(string key, string fallback) =>
             SerpLocalization.Get(key);
 
+        protected override void ConfigurePerPlayerLobbySettings(
+            Shared.PerPlayerLobbySettingsBuilder settings)
+        {
+            settings
+                .ResetSlotsWith(nameof(CoopPackageStatus), () => null)
+                .RequireReport(
+                    nameof(CoopPackageStatus),
+                    value => !string.IsNullOrEmpty(value as string));
+        }
+
         public event Action<bool> RuntimeActivationChanged;
         public event Action ActiveCoopPackageChanged;
 
@@ -78,8 +97,14 @@ namespace CustomCustomTrail
                     return SerpLocalization.Get("CustomCustomTrail.StatusVanilla");
                 if (status.StartsWith("OK|", StringComparison.Ordinal))
                     return SerpLocalization.Get("CustomCustomTrail.StatusReady");
-                if (status.StartsWith("ERROR|", StringComparison.Ordinal))
-                    return status.Substring("ERROR|".Length);
+                if (string.Equals(status, MissingStatus, StringComparison.Ordinal))
+                    return SerpLocalization.Get("CustomCustomTrail.ErrorPackageMissing") + " " + ActiveCoopPackageId;
+                if (string.Equals(status, MismatchStatus, StringComparison.Ordinal))
+                    return SerpLocalization.Get("CustomCustomTrail.ErrorFingerprintMismatch");
+                if (status.StartsWith(InvalidStatusPrefix, StringComparison.Ordinal))
+                    return SerpLocalization.Get("CustomCustomTrail.ErrorPackageInvalid") + " " + status.Substring(InvalidStatusPrefix.Length);
+                if (string.Equals(status, DisabledStatus, StringComparison.Ordinal))
+                    return SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled");
                 return SerpLocalization.Get("CustomCustomTrail.StatusChecking");
             }
         }
@@ -177,17 +202,31 @@ namespace CustomCustomTrail
             }
         }
 
+        [SyncHostOnly, DoNotPersist]
+        public string ActiveCoopPackageDescriptor
+        {
+            get => activeCoopPackageDescriptor;
+            set
+            {
+                value = value ?? string.Empty;
+                if (!CanMutateSetting(nameof(ActiveCoopPackageDescriptor)) || string.Equals(activeCoopPackageDescriptor, value, StringComparison.Ordinal))
+                    return;
+                activeCoopPackageDescriptor = value;
+                OnPropertyChanged(nameof(ActiveCoopPackageDescriptor));
+                ActiveCoopPackageChanged?.Invoke();
+            }
+        }
+
         [SyncPerPlayer, DoNotPersist]
         public string CoopPackageStatus
         {
-            get => GetLocalStatus();
+            get => coopPackageStatus;
             set
             {
-                int playerId = Math.Max(1, GameNetworkAPI.GetLocalPlayerId());
                 value = value ?? string.Empty;
-                if (string.Equals(CoopPackageStatusData[playerId], value, StringComparison.Ordinal))
+                if (string.Equals(coopPackageStatus, value, StringComparison.Ordinal))
                     return;
-                CoopPackageStatusData[playerId] = value;
+                coopPackageStatus = value;
                 OnPropertyChanged(nameof(CoopPackageStatus));
                 OnPropertyChanged(nameof(CoopPackageStatusText));
             }
@@ -311,11 +350,7 @@ namespace CustomCustomTrail
             }
         }
 
-        private string GetLocalStatus()
-        {
-            int playerId = Math.Max(1, GameNetworkAPI.GetLocalPlayerId());
-            return CoopPackageStatusData[playerId] ?? string.Empty;
-        }
+        private string GetLocalStatus() => coopPackageStatus;
     }
 
     public sealed class TrailModSelectionItem : INotifyPropertyChanged

diff --git a/CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs b/CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs
index da6f454c..d674212e 100644
--- a/CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs
+++ b/CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs
@@ -5,10 +5,14 @@ using CrusaderDE;
 using MessagePack;
 using MonoMod.RuntimeDetour;
 using Noesis;
+using R3;
 using Shared;
 using SHCDESE.API;
 using SHCDESE.API.Components.ModManager;
 using SHCDESE.API.Components.Network;
+using SHCDESE.EventAPI;
+using SHCDESE.EventAPI.Network;
+using Steamworks;
 using System;
 using System.Collections;
 using System.Collections.Generic;
@@ -41,6 +45,7 @@ namespace CustomCustomTrail
         {
             private const string CoopTrailMakerSourceDirectory = "TrailMakerSource";
             private const string EncodedSettingPrefix = "messagepack-base64:";
+            private const int CoopCustomizeProtocolVersion = 2;
 
             private static readonly FieldInfo MpLocalReadyField = typeof(FRONT_Multiplayer).GetField(
                 "MPLocalReady", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
@@ -131,9 +136,11 @@ namespace CustomCustomTrail
             private CheckBox coopTrailExportCheckbox;
             private string coopPackageDisplayName = string.Empty;
             private int coopPackageMissionCount;
+            private short coopCustomizePacketId;
 
             public event Action CoopPackagesChanged;
             public event Action CoopSetupOpened;
+            public event Action<int, int> CoopLaunchReceived;
 
             public IReadOnlyList<TrailModCompatibilityInfo> DiscoverModCompatibility()
             {
@@ -240,6 +247,10 @@ namespace CustomCustomTrail
             public void Initialize()
             {
                 CaptureVanillaCoopTrailTitles();
+                R3PacketEventHook<CoopCustomizePacket> packetHook =
+                    GameNetworkAPI.Instance.GetPacketEventFor<CoopCustomizePacket>();
+                coopCustomizePacketId = packetHook.GetPacketId();
+                hooks.Add(packetHook.GetBaseHook().Observable.Subscribe(OnCoopCustomizePacket));
                 saveCustomTrailMapOriginal = InstallHook(
                     typeof(EditorDirector).GetMethod(nameof(EditorDirector.SaveCustomTrailMap)),
                     (SaveCustomTrailMapDelegate)SaveCustomTrailMapHook);
@@ -1267,23 +1278,46 @@ namespace CustomCustomTrail
 
             private void InitializeCoopPage(UserControl page, int zeroBasedTrail)
             {
-                // The Vanilla constructor has now loaded the XAML and assigned all named controls.
-                // This is the first deterministic point at which the first-visit title exists.
                 InjectCoopCustomizeButton(page);
                 if (UpdateCoopTrailTitle(page, zeroBasedTrail))
                 {
-                    DebugLogHelper.LogDebug(
-                        log,
-                        "Initialized custom presentation for Coop Trail " +
-                        (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
+                    LogCoopPresentationInitialized(zeroBasedTrail, "constructor");
+                    return;
                 }
-                else
+
+                // Noesis can finish the managed constructor before the logical and visual
+                // trees are materialized. Retry once at the framework's deterministic Loaded
+                // event instead of treating this normal first phase as a feature failure.
+                RoutedEventHandler loaded = null;
+                loaded = (_, __) =>
                 {
+                    page.Loaded -= loaded;
+                    InjectCoopCustomizeButton(page);
+                    if (UpdateCoopTrailTitle(page, zeroBasedTrail))
+                    {
+                        LogCoopPresentationInitialized(zeroBasedTrail, "loaded");
+                        return;
+                    }
+
                     DebugLogHelper.LogWarning(
                         log,
-                        "Could not find the logical title element for Coop Trail " +
+                        "Could not find the logical title element after Loaded for Coop Trail " +
                         (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
-                }
+                };
+                page.Loaded += loaded;
+                DebugLogHelper.LogDebug(
+                    log,
+                    "Deferred custom presentation until Loaded for Coop Trail " +
+                    (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
+            }
+
+            private void LogCoopPresentationInitialized(int zeroBasedTrail, string phase)
+            {
+                DebugLogHelper.LogDebug(
+                    log,
+                    "Initialized custom presentation for Coop Trail " +
+                    (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) +
+                    "; phase=" + phase + ".");
             }
 
             private void EnterSelectedCustomTrail(FrontendMenus menus)
@@ -1553,7 +1587,30 @@ namespace CustomCustomTrail
                 if (mission <= 0 || trailId < 0 || trailId > 3 || self.currentLobby == null)
                     return;
 
+                if (!self.singlePlayerCoop && !self.currentLobby.isHost)
+                {
+                    DebugLogHelper.LogWarning(log, "Ignored Coop Trail Customize click from a non-host client.");
+                    return;
+                }
+
+                OpenCoopTrailSetup(self, trailId, mission, notifyClients: !self.singlePlayerCoop, source: "local host");
+            }
+
+            private void OpenCoopTrailSetup(
+                FRONT_Multiplayer self,
+                int trailId,
+                int mission,
+                bool notifyClients,
+                string source)
+            {
+                if (self?.currentLobby == null || trailId < 0 || trailId > 3 || mission < 1 || mission > 10)
+                    throw new InvalidDataException("The Coop Trail setup transition is invalid.");
+
+                SetSelectedCoopMission(trailId, mission);
+
                 self.CoopMissionChanged(trailId, mission);
+                if (notifyClients)
+                    BroadcastCoopCustomize(trailId, mission);
                 MethodInfo showSetup = typeof(FRONT_Multiplayer).GetMethod("ShowSetupScreen", BindingFlags.Instance | BindingFlags.NonPublic);
                 if (self.singlePlayerCoop)
                 {
@@ -1593,14 +1650,104 @@ namespace CustomCustomTrail
                 MainViewModel.Instance.Show_CoopWaiting = false;
                 MainViewModel.Instance.Show_MPSharing = false;
                 MainViewModel.Instance.Show_MultiplayerSetup = true;
-                if (currentTrail == 21) MainViewModel.Instance.Show_CoopTrail1 = false;
-                if (currentTrail == 22) MainViewModel.Instance.Show_CoopTrail2 = false;
-                if (currentTrail == 23) MainViewModel.Instance.Show_CoopTrail3 = false;
-                if (currentTrail == 24) MainViewModel.Instance.Show_CoopTrail4 = false;
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/CustomCustomTrail.diff).
