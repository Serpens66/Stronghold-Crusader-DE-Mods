# RandomEvents release status

**Status:** code newer

- Release: [v1.0.11](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.11)
- Release commit: [a6e23e7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/a6e23e7301ef87224e10b78041d6e0f2e0b227ce)
- Current main commit: [052884c](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/052884c545ea5a7388b629bab9add42d8bc7c4d0)

## Relevant changed files

- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json`
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
- `RandomEvents/info.json`
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
- `RandomEvents/src/NativeVanillaEventDispatcher.cs`
- `RandomEvents/src/NativeWildlifeEventDispatcher.cs`
- `RandomEvents/src/RandomEventsChorePacket.cs`
- `RandomEvents/src/RandomEventsCooldownCodec.cs`
- `RandomEvents/src/RandomEventsDiagnostics.cs`
- `RandomEvents/src/RandomEventsInitializationAckPacket.cs`
- `RandomEvents/src/RandomEventsPlugin.cs`
- `RandomEvents/src/RandomEventsPresentationScope.cs`
- `RandomEvents/src/RandomEventsRuntime.cs`
- `RandomEvents/src/RandomEventsSaveState.cs`
- `RandomEvents/src/RandomEventsSaveStateV2.cs`
- `RandomEvents/src/RandomEventsState.cs`
- `RandomEvents/src/ScenarioSignpostRegistry.cs`
- `RandomEvents/src/SignpostPlacementService.cs`
- `Shared/ActivePlayerHelper.cs`
- `Shared/ActivePlayerKeepReadiness.cs`
- `Shared/GameModeHelper.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`
- `Shared/SerpLocalization.cs`

Relevant localization keys: `Common.ClientActivationLabel`, `Common.ClientSettingsActivationHelp`, `Common.HostActivationLabel`, `Common.HostSettingsActivationHelp`, `RandomEvents.MultiplayerMode`, `RandomEvents.MultiplayerModeHelp`

## Diff

```diff
diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
index afac1c41..f6d0bf02 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
@@ -2,11 +2,85 @@
   "GUID": "RandomEvents_Serp",
   "Author": "Serpens66",
   "Name": "Random Events",
-  "Description": "Runs configurable Vanilla scenario events in singleplayer skirmishes and Trail missions.",
-  "Version": "1.0.11",
+  "Description": "Runs configurable Vanilla scenario events in skirmishes, Trail missions, and multiplayer games.",
+  "Version": "1.0.22",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.22",
+      "Changes": [
+        "Isolated native event backends and lifecycle stages, and continue later due actions after one action fails without repeating the batch."
+      ]
+    },
+    {
+      "Version": "1.0.21",
+      "Changes": [
+        "Resolve active players exclusively from synchronized in-game members while excluding kicked and defeated slots.",
+        "Use the shared active-player Keep-readiness check for retried deterministic signpost initialization."
+      ]
+    },
+    {
+      "Version": "1.0.20",
+      "Changes": [
+        "Fixed archer events spawning every player's units at Vanilla's shared fallback location by injecting the affected player's nearest signpost into the first native attack scenario point.",
+        "Added exact original and injected attack-point diagnostics for multiplayer verification."
+      ]
+    },
+    {
+      "Version": "1.0.19",
+      "Changes": [
+        "Made signpost-based Vanilla events expose only the signpost nearest to the affected player while their synchronized Chore executes, preventing peer-local source selection for archer spawns.",
+        "Added exact archer unit ID, owner, type, state, spawn tile, and target-tile diagnostics for multiplayer comparison."
+      ]
+    },
+    {
+      "Version": "1.0.18",
+      "Changes": [
+        "Limited event notifications and minimap action points to the affected local player while preserving synchronized simulation on every peer in both multiplayer event modes."
+      ]
+    },
+    {
+      "Version": "1.0.17",
+      "Changes": [
+        "Fixed multiplayer desyncs from FreeBuild events by executing their immediate native GameAction on every peer inside the synchronized Random Events batch Chore."
+      ]
+    },
+    {
+      "Version": "1.0.16",
+      "Changes": [
+        "Added event-scoped diagnostics for Vanilla FreeBuild event Chores, including target players, command IDs, ticks, hashes, and exact payload bytes, to isolate the remaining multiplayer desync."
+      ]
+    },
+    {
+      "Version": "1.0.15",
+      "Changes": [
+        "Reduced multiplayer initialization traffic with command-specific Chore packets, compact cooldown encoding, stable retries, and idempotent duplicate handling.",
+        "Replaced the legacy save payload with a dynamic-only schema; current synchronized lobby settings are authoritative when a save is loaded."
+      ]
+    },
+    {
+      "Version": "1.0.14",
+      "Changes": [
+        "Delayed multiplayer initialization until the map startup is stable and prevented multiple Random Events Chores in one simulation tick.",
+        "Added a fail-closed peer acknowledgement handshake before signposts or event batches can change the synchronized simulation."
+      ]
+    },
+    {
+      "Version": "1.0.13",
+      "Changes": [
+        "Added fail-closed Chore serializer self-tests and packet hashes to diagnose multiplayer transport corruption.",
+        "Added matching PRNG, action-order, and state digests for precise host/client desync comparison."
+      ]
+    },
+    {
+      "Version": "1.0.12",
+      "Changes": [
+        "Enabled Random Events in multiplayer through Script Extender's tick-aligned Chore transport.",
+        "Implemented shared rolls that affect every living human equally and independent per-player rolls that can produce different events.",
+        "Made the host authoritative for the private event PRNG while preserving identical Vanilla RNG call order on every peer, and replaced the bandit real-time delay with a deterministic simulation-tick delay."
+      ]
+    },
     {
       "Version": "1.0.11",
       "Changes": [

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ar.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/cs-CZ.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt
index 39884cb4..6831936b 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/de-DE.txt
@@ -1,6 +1,10 @@
 # Random Events localization
 Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 RandomEvents.Interval=Intervall (Vanilla-Monate)
 RandomEvents.IntervalHelp=Der erste Wurf erfolgt nach einem vollständigen Intervall. Jedes Ereignis würfelt unabhängig.
 RandomEvents.Cooldown=Abklingzeit eines Ereignisses (Monate)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Legt den minimalen und maximalen Prozentsatz der
 RandomEvents.FireStrengthHelp=Legt die minimale und maximale Stärke des Feuerereignisses fest. Beim Auslösen wird ein Wert aus diesem Bereich ausgewürfelt.
 RandomEvents.Minimum=Minimum
 RandomEvents.Maximum=Maximum
-RandomEvents.MultiplayerMode=Reservierter Mehrspielermodus
-RandomEvents.MultiplayerModeHelp=Für eine spätere Version reserviert. Random Events ist in Netzwerkspielen vollständig deaktiviert.
+RandomEvents.MultiplayerMode=Verteilung der Mehrspielerereignisse
+RandomEvents.MultiplayerModeHelp=Gemeinsame Ereignisse verwenden denselben Wurf und dieselbe Stärke für alle lebenden menschlichen Spieler. Individuelle Würfe geben jedem Menschen eigene Chancen- und Stärkewürfe. Beide Modi führen die aufgelösten Aktionen über dieselbe tick-synchrone Chore-Folge aus, damit die Simulation synchron bleibt.
 RandomEvents.MultiplayerShared=Gemeinsame Ereignisse
 RandomEvents.MultiplayerIndividual=Individuelle Würfe
 RandomEvents.Event.Fair=Jahrmarkt

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/el-GR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt
index 592304f6..60c507b5 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/en-US.txt
@@ -1,6 +1,10 @@
 # Random Events localization
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/es-ES.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/fr-FR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/hu-HU.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/it-IT.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ja-JP.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ko-KR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/nl-NL.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pl-PL.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/pt-BR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/ru-RU.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/sv-SE.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/th-TH.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/tr-TR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/uk-UA.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-CN.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Locales/zh-HK.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
index db8459c7..9b56b16f 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
@@ -14,9 +14,14 @@
                 HorizontalScrollBarVisibility="Auto">
     <StackPanel Margin="10">
       <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
-        <Border Style="{StaticResource HostActivationBorder}"><CheckBox IsEnabled="{Binding CanEditHostSettings}" IsChecked="{Binding EnableMod, Mode=TwoWay}" Content="{Binding EnableModText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding EnableModHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/></Border>
-        <TextBlock Text="{Binding PresetText}" Visibility="{Binding PresetVisibility}" Foreground="#CCCCCC" VerticalAlignment="Center" Margin="14,0,6,0"/>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" Width="170" VerticalAlignment="Center"/>
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

diff --git a/RandomEvents/info.json b/RandomEvents/info.json
index afac1c41..f6d0bf02 100644
--- a/RandomEvents/info.json
+++ b/RandomEvents/info.json
@@ -2,11 +2,85 @@
   "GUID": "RandomEvents_Serp",
   "Author": "Serpens66",
   "Name": "Random Events",
-  "Description": "Runs configurable Vanilla scenario events in singleplayer skirmishes and Trail missions.",
-  "Version": "1.0.11",
+  "Description": "Runs configurable Vanilla scenario events in skirmishes, Trail missions, and multiplayer games.",
+  "Version": "1.0.22",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.22",
+      "Changes": [
+        "Isolated native event backends and lifecycle stages, and continue later due actions after one action fails without repeating the batch."
+      ]
+    },
+    {
+      "Version": "1.0.21",
+      "Changes": [
+        "Resolve active players exclusively from synchronized in-game members while excluding kicked and defeated slots.",
+        "Use the shared active-player Keep-readiness check for retried deterministic signpost initialization."
+      ]
+    },
+    {
+      "Version": "1.0.20",
+      "Changes": [
+        "Fixed archer events spawning every player's units at Vanilla's shared fallback location by injecting the affected player's nearest signpost into the first native attack scenario point.",
+        "Added exact original and injected attack-point diagnostics for multiplayer verification."
+      ]
+    },
+    {
+      "Version": "1.0.19",
+      "Changes": [
+        "Made signpost-based Vanilla events expose only the signpost nearest to the affected player while their synchronized Chore executes, preventing peer-local source selection for archer spawns.",
+        "Added exact archer unit ID, owner, type, state, spawn tile, and target-tile diagnostics for multiplayer comparison."
+      ]
+    },
+    {
+      "Version": "1.0.18",
+      "Changes": [
+        "Limited event notifications and minimap action points to the affected local player while preserving synchronized simulation on every peer in both multiplayer event modes."
+      ]
+    },
+    {
+      "Version": "1.0.17",
+      "Changes": [
+        "Fixed multiplayer desyncs from FreeBuild events by executing their immediate native GameAction on every peer inside the synchronized Random Events batch Chore."
+      ]
+    },
+    {
+      "Version": "1.0.16",
+      "Changes": [
+        "Added event-scoped diagnostics for Vanilla FreeBuild event Chores, including target players, command IDs, ticks, hashes, and exact payload bytes, to isolate the remaining multiplayer desync."
+      ]
+    },
+    {
+      "Version": "1.0.15",
+      "Changes": [
+        "Reduced multiplayer initialization traffic with command-specific Chore packets, compact cooldown encoding, stable retries, and idempotent duplicate handling.",
+        "Replaced the legacy save payload with a dynamic-only schema; current synchronized lobby settings are authoritative when a save is loaded."
+      ]
+    },
+    {
+      "Version": "1.0.14",
+      "Changes": [
+        "Delayed multiplayer initialization until the map startup is stable and prevented multiple Random Events Chores in one simulation tick.",
+        "Added a fail-closed peer acknowledgement handshake before signposts or event batches can change the synchronized simulation."
+      ]
+    },
+    {
+      "Version": "1.0.13",
+      "Changes": [
+        "Added fail-closed Chore serializer self-tests and packet hashes to diagnose multiplayer transport corruption.",
+        "Added matching PRNG, action-order, and state digests for precise host/client desync comparison."
+      ]
+    },
+    {
+      "Version": "1.0.12",
+      "Changes": [
+        "Enabled Random Events in multiplayer through Script Extender's tick-aligned Chore transport.",
+        "Implemented shared rolls that affect every living human equally and independent per-player rolls that can produce different events.",
+        "Made the host authoritative for the private event PRNG while preserving identical Vanilla RNG call order on every peer, and replaced the bandit real-time delay with a deterministic simulation-tick delay."
+      ]
+    },
     {
       "Version": "1.0.11",
       "Changes": [

diff --git a/RandomEvents/Locales/ar.txt b/RandomEvents/Locales/ar.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/ar.txt
+++ b/RandomEvents/Locales/ar.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/cs-CZ.txt b/RandomEvents/Locales/cs-CZ.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/cs-CZ.txt
+++ b/RandomEvents/Locales/cs-CZ.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/de-DE.txt b/RandomEvents/Locales/de-DE.txt
index 39884cb4..6831936b 100644
--- a/RandomEvents/Locales/de-DE.txt
+++ b/RandomEvents/Locales/de-DE.txt
@@ -1,6 +1,10 @@
 # Random Events localization
 Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 RandomEvents.Interval=Intervall (Vanilla-Monate)
 RandomEvents.IntervalHelp=Der erste Wurf erfolgt nach einem vollständigen Intervall. Jedes Ereignis würfelt unabhängig.
 RandomEvents.Cooldown=Abklingzeit eines Ereignisses (Monate)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Legt den minimalen und maximalen Prozentsatz der
 RandomEvents.FireStrengthHelp=Legt die minimale und maximale Stärke des Feuerereignisses fest. Beim Auslösen wird ein Wert aus diesem Bereich ausgewürfelt.
 RandomEvents.Minimum=Minimum
 RandomEvents.Maximum=Maximum
-RandomEvents.MultiplayerMode=Reservierter Mehrspielermodus
-RandomEvents.MultiplayerModeHelp=Für eine spätere Version reserviert. Random Events ist in Netzwerkspielen vollständig deaktiviert.
+RandomEvents.MultiplayerMode=Verteilung der Mehrspielerereignisse
+RandomEvents.MultiplayerModeHelp=Gemeinsame Ereignisse verwenden denselben Wurf und dieselbe Stärke für alle lebenden menschlichen Spieler. Individuelle Würfe geben jedem Menschen eigene Chancen- und Stärkewürfe. Beide Modi führen die aufgelösten Aktionen über dieselbe tick-synchrone Chore-Folge aus, damit die Simulation synchron bleibt.
 RandomEvents.MultiplayerShared=Gemeinsame Ereignisse
 RandomEvents.MultiplayerIndividual=Individuelle Würfe
 RandomEvents.Event.Fair=Jahrmarkt

diff --git a/RandomEvents/Locales/el-GR.txt b/RandomEvents/Locales/el-GR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/el-GR.txt
+++ b/RandomEvents/Locales/el-GR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/en-US.txt b/RandomEvents/Locales/en-US.txt
index 592304f6..60c507b5 100644
--- a/RandomEvents/Locales/en-US.txt
+++ b/RandomEvents/Locales/en-US.txt
@@ -1,6 +1,10 @@
 # Random Events localization
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/es-ES.txt b/RandomEvents/Locales/es-ES.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/es-ES.txt
+++ b/RandomEvents/Locales/es-ES.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/fr-FR.txt b/RandomEvents/Locales/fr-FR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/fr-FR.txt
+++ b/RandomEvents/Locales/fr-FR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/hu-HU.txt b/RandomEvents/Locales/hu-HU.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/hu-HU.txt
+++ b/RandomEvents/Locales/hu-HU.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/it-IT.txt b/RandomEvents/Locales/it-IT.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/it-IT.txt
+++ b/RandomEvents/Locales/it-IT.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/ja-JP.txt b/RandomEvents/Locales/ja-JP.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/ja-JP.txt
+++ b/RandomEvents/Locales/ja-JP.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/ko-KR.txt b/RandomEvents/Locales/ko-KR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/ko-KR.txt
+++ b/RandomEvents/Locales/ko-KR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/nl-NL.txt b/RandomEvents/Locales/nl-NL.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/nl-NL.txt
+++ b/RandomEvents/Locales/nl-NL.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/pl-PL.txt b/RandomEvents/Locales/pl-PL.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/pl-PL.txt
+++ b/RandomEvents/Locales/pl-PL.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/pt-BR.txt b/RandomEvents/Locales/pt-BR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/pt-BR.txt
+++ b/RandomEvents/Locales/pt-BR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/ru-RU.txt b/RandomEvents/Locales/ru-RU.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/ru-RU.txt
+++ b/RandomEvents/Locales/ru-RU.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/sv-SE.txt b/RandomEvents/Locales/sv-SE.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/sv-SE.txt
+++ b/RandomEvents/Locales/sv-SE.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/th-TH.txt b/RandomEvents/Locales/th-TH.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/th-TH.txt
+++ b/RandomEvents/Locales/th-TH.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/tr-TR.txt b/RandomEvents/Locales/tr-TR.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/tr-TR.txt
+++ b/RandomEvents/Locales/tr-TR.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/uk-UA.txt b/RandomEvents/Locales/uk-UA.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/uk-UA.txt
+++ b/RandomEvents/Locales/uk-UA.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/zh-CN.txt b/RandomEvents/Locales/zh-CN.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/zh-CN.txt
+++ b/RandomEvents/Locales/zh-CN.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Locales/zh-HK.txt b/RandomEvents/Locales/zh-HK.txt
index f9bd3d56..afdce384 100644
--- a/RandomEvents/Locales/zh-HK.txt
+++ b/RandomEvents/Locales/zh-HK.txt
@@ -1,6 +1,10 @@
 # English fallback translation
 Common.ResetToDefault=Reset to Default
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 RandomEvents.Interval=Interval (Vanilla months)
 RandomEvents.IntervalHelp=The first roll happens after one complete interval. Every event rolls independently.
 RandomEvents.Cooldown=Cooldown of an event (months)
@@ -15,8 +19,8 @@ RandomEvents.TheftStrengthHelp=Sets the minimum and maximum percentage of granar
 RandomEvents.FireStrengthHelp=Sets the minimum and maximum fire-event strength. A value in this range is rolled when the event triggers.
 RandomEvents.Minimum=Min
 RandomEvents.Maximum=Max
-RandomEvents.MultiplayerMode=Reserved multiplayer mode
-RandomEvents.MultiplayerModeHelp=Reserved for a future version. Random Events is fully disabled in network games.
+RandomEvents.MultiplayerMode=Multiplayer event distribution
+RandomEvents.MultiplayerModeHelp=Shared events use one roll and strength for every living human player. Individual rolls give each human separate chance and strength rolls. Both modes execute the resolved actions through the same tick-aligned Chore sequence to keep the simulation synchronized.
 RandomEvents.MultiplayerShared=Shared events
 RandomEvents.MultiplayerIndividual=Individual rolls
 RandomEvents.Event.Fair=Fair

diff --git a/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml b/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml
index db8459c7..9b56b16f 100644
--- a/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml
+++ b/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml
@@ -14,9 +14,14 @@
                 HorizontalScrollBarVisibility="Auto">
     <StackPanel Margin="10">
       <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
-        <Border Style="{StaticResource HostActivationBorder}"><CheckBox IsEnabled="{Binding CanEditHostSettings}" IsChecked="{Binding EnableMod, Mode=TwoWay}" Content="{Binding EnableModText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding EnableModHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/></Border>
-        <TextBlock Text="{Binding PresetText}" Visibility="{Binding PresetVisibility}" Foreground="#CCCCCC" VerticalAlignment="Center" Margin="14,0,6,0"/>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" Width="170" VerticalAlignment="Center"/>
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

diff --git a/RandomEvents/RandomEvents.csproj b/RandomEvents/RandomEvents.csproj
index cfb37ee8..98417fe8 100644
--- a/RandomEvents/RandomEvents.csproj
+++ b/RandomEvents/RandomEvents.csproj
@@ -29,6 +29,7 @@
   </ItemGroup>
   <ItemGroup>
     <Compile Include="..\Shared\ActivePlayerHelper.cs"><Link>Shared\ActivePlayerHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\ActivePlayerKeepReadiness.cs"><Link>Shared\ActivePlayerKeepReadiness.cs</Link></Compile>
     <Compile Include="..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
     <Compile Include="..\Shared\NativePatternResolver.cs"><Link>Shared\NativePatternResolver.cs</Link></Compile>
     <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
@@ -38,8 +39,14 @@
     <Compile Include="src\NativeVanillaEventDispatcher.cs" />
     <Compile Include="src\NativeWildlifeEventDispatcher.cs" />
     <Compile Include="src\RandomEventsPlugin.cs" />
+    <Compile Include="src\RandomEventsChorePacket.cs" />
+    <Compile Include="src\RandomEventsCooldownCodec.cs" />
+    <Compile Include="src\RandomEventsDiagnostics.cs" />
+    <Compile Include="src\RandomEventsInitializationAckPacket.cs" />
+    <Compile Include="src\RandomEventsPresentationScope.cs" />
     <Compile Include="src\RandomEventsRuntime.cs" />
-    <Compile Include="src\RandomEventsSaveStateV2.cs" />
+    <Compile Include="src\RandomEventsSaveState.cs" />
+    <Compile Include="src\RandomEventsState.cs" />
     <Compile Include="src\RandomEventsSettingsViewModel.cs" />
     <Compile Include="src\ScenarioSignpostRegistry.cs" />
     <Compile Include="src\SignpostPlacementService.cs" />

diff --git a/RandomEvents/src/NativeVanillaEventDispatcher.cs b/RandomEvents/src/NativeVanillaEventDispatcher.cs
index d7e9c629..33071f27 100644
--- a/RandomEvents/src/NativeVanillaEventDispatcher.cs
+++ b/RandomEvents/src/NativeVanillaEventDispatcher.cs
@@ -1,4 +1,5 @@
 using BepInEx.Logging;
+using MonoMod.RuntimeDetour;
 using SHCDESE.API;
 using SHCDESE.GameGlobals;
 using SHCDESE.Interop;
@@ -62,6 +63,10 @@ namespace RandomEvents
         private BuildingEventDelegate madCowBuildingHandler;
         private GranaryTheftDelegate granaryTheftHandler;
         private PresentationDelegate presentationHandler;
+        private PresentationDelegate presentationOriginal;
+        private PresentationDelegate rootedPresentationDetour;
+        private NativeDetour presentationDetour;
+        private IntPtr presentationHandlerAddress;
 
         public NativeVanillaEventDispatcher(ManualLogSource log)
         {
@@ -320,7 +325,8 @@ namespace RandomEvents
                         $"reference presentation targets differ: manager=0x{managerRva:X}, handler=0x{handlerRva:X}.");
                 }
 
-                presentationHandler = Marshal.GetDelegateForFunctionPointer<PresentationDelegate>(AtRva(libraryHandle, handlerRva));
+                IntPtr resolvedHandlerAddress = AtRva(libraryHandle, handlerRva);
+                InstallPresentationFilter(resolvedHandlerAddress);
                 presentationManager = AtRva(libraryHandle, managerRva);
             }
             catch (Exception ex)
@@ -331,6 +337,65 @@ namespace RandomEvents
             }
         }
 
+        private void InstallPresentationFilter(IntPtr resolvedHandlerAddress)
+        {
+            if (presentationDetour != null)
+            {
+                if (resolvedHandlerAddress != presentationHandlerAddress)
+                {
+                    throw new InvalidOperationException(
+                        $"presentation handler changed after detour installation: " +
+                        $"installed=0x{presentationHandlerAddress.ToInt64():X}, resolved=0x{resolvedHandlerAddress.ToInt64():X}.");
+                }
+
+                presentationHandler = FilterPresentation;
+                return;
+            }
+
+            rootedPresentationDetour = FilterPresentation;
+            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedPresentationDetour);
+            NativeDetour installedDetour = null;
+            try
+            {
+                var config = new NativeDetourConfig { ManualApply = true };
+                installedDetour = new NativeDetour(resolvedHandlerAddress, detourAddress, config);
+                PresentationDelegate installedOriginal = installedDetour.GenerateTrampoline<PresentationDelegate>();
+                presentationHandlerAddress = resolvedHandlerAddress;
+                presentationOriginal = installedOriginal;
+                presentationHandler = FilterPresentation;
+                installedDetour.Apply();
+                presentationDetour = installedDetour;
+                LogDebug($"Native event-presentation target filter installed: address=0x{resolvedHandlerAddress.ToInt64():X}.");
+            }
+            catch
+            {
+                installedDetour?.Dispose();
+                presentationHandlerAddress = IntPtr.Zero;
+                presentationOriginal = null;
+                presentationHandler = null;
+                rootedPresentationDetour = null;
+                throw;
+            }
+        }
+
+        private void FilterPresentation(
+            IntPtr messageManager,
+            int messageId,
+            int presentationId,
+            IntPtr video,
+            IntPtr audio)
+        {
+            // Simulation remains replicated; only this peer's transient event UI is target-filtered.
+            PresentationDelegate original = presentationOriginal;
+            if (RandomEventsPresentationScope.IsSuppressed)
+            {
+                RandomEventsPresentationScope.RecordSuppressedPresentation();
+                return;
+            }
+            if (original != null)
+                original(messageManager, messageId, presentationId, video, audio);
+        }
+
         private bool TryGetEventAvailability(RandomEventKind kind, out string reason)
         {
             if (presentationHandler == null || presentationManager == IntPtr.Zero)
@@ -385,6 +450,7 @@ namespace RandomEvents
             new IntPtr(checked(libraryHandle.ToInt64() + rva));
 
         private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
+        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
 
         [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
         private delegate int HasBuildingDelegate(IntPtr buildingManager, int playerId, int buildingType);

diff --git a/RandomEvents/src/NativeWildlifeEventDispatcher.cs b/RandomEvents/src/NativeWildlifeEventDispatcher.cs
index 8fdfe106..e9c8540d 100644
--- a/RandomEvents/src/NativeWildlifeEventDispatcher.cs
+++ b/RandomEvents/src/NativeWildlifeEventDispatcher.cs
@@ -1,4 +1,5 @@
 using BepInEx.Logging;
+using MonoMod.RuntimeDetour;
 using SHCDESE.GameGlobals;
 using Shared;
 using System;
@@ -67,6 +68,10 @@ namespace RandomEvents
         private int tribeStride;
         private int lionActivationOffset;
         private ActionPointHandlerDelegate actionPointHandler;
+        private ActionPointHandlerDelegate actionPointOriginal;
+        private ActionPointHandlerDelegate rootedActionPointDetour;
+        private NativeDetour actionPointDetour;
+        private IntPtr actionPointHandlerAddress;
         private IntPtr actionPointManager;
         private string rabbitUnavailableReason = "native wildlife resolution has not run.";
         private string lionUnavailableReason = "native wildlife resolution has not run.";
@@ -423,8 +428,7 @@ namespace RandomEvents
                     throw new InvalidOperationException("lion wrapper does not call the validated action-point handler.");
                 actionPointManager = ResolveRipRelativeAddress(
                     libraryHandle, memory, wrapper.Rva + 8, 3, 7);
-                actionPointHandler = Marshal.GetDelegateForFunctionPointer<ActionPointHandlerDelegate>(
-                    AtRva(libraryHandle, handler.Rva));
+                InstallActionPointFilter(AtRva(libraryHandle, handler.Rva));
             }
             catch (Exception ex)
             {
@@ -436,6 +440,60 @@ namespace RandomEvents
             }
         }
 
+        private void InstallActionPointFilter(IntPtr resolvedHandlerAddress)
+        {
+            if (actionPointDetour != null)
+            {
+                if (resolvedHandlerAddress != actionPointHandlerAddress)
+                {
+                    throw new InvalidOperationException(
+                        $"action-point handler changed after detour installation: " +
+                        $"installed=0x{actionPointHandlerAddress.ToInt64():X}, resolved=0x{resolvedHandlerAddress.ToInt64():X}.");
+                }
+
+                actionPointHandler = FilterActionPoint;
+                return;
+            }
+
+            rootedActionPointDetour = FilterActionPoint;
+            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedActionPointDetour);
+            NativeDetour installedDetour = null;
+            try
+            {
+                var config = new NativeDetourConfig { ManualApply = true };
+                installedDetour = new NativeDetour(resolvedHandlerAddress, detourAddress, config);
+                ActionPointHandlerDelegate installedOriginal = installedDetour.GenerateTrampoline<ActionPointHandlerDelegate>();
+                actionPointHandlerAddress = resolvedHandlerAddress;
+                actionPointOriginal = installedOriginal;
+                actionPointHandler = FilterActionPoint;
+                installedDetour.Apply();
+                actionPointDetour = installedDetour;
+                LogDebug($"Native minimap action-point target filter installed: address=0x{resolvedHandlerAddress.ToInt64():X}.");
+            }
+            catch
+            {
+                installedDetour?.Dispose();
+                actionPointHandlerAddress = IntPtr.Zero;
+                actionPointOriginal = null;
+                actionPointHandler = null;
+                rootedActionPointDetour = null;
+                throw;
+            }
+        }
+
+        private void FilterActionPoint(IntPtr manager, int tileX, int tileY)
+        {
+            // Action points are minimap UI and must not leak events targeted at another human.
+            ActionPointHandlerDelegate original = actionPointOriginal;
+            if (RandomEventsPresentationScope.IsSuppressed)
+            {
+                RandomEventsPresentationScope.RecordSuppressedActionPoint();
+                return;
+            }
+            if (original != null)
+                original(manager, tileX, tileY);
+        }
+
         private static IntPtr ResolveRipRelativeAddress(
             IntPtr libraryHandle,
             ReadOnlySpan<byte> memory,
@@ -495,6 +553,7 @@ namespace RandomEvents
             new IntPtr(checked(libraryHandle.ToInt64() + rva));
 
         private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
+        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
 
         [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
         private delegate int WildlifeHandlerDelegate(

diff --git a/RandomEvents/src/RandomEventsChorePacket.cs b/RandomEvents/src/RandomEventsChorePacket.cs
new file mode 100644
index 00000000..2b90480f
--- /dev/null
+++ b/RandomEvents/src/RandomEventsChorePacket.cs
@@ -0,0 +1,155 @@
+using MessagePack;
+using MessagePack.Formatters;
+using System;
+using System.Buffers;
+
+namespace RandomEvents
+{
+    internal enum RandomEventsCooldownEncoding { None, SharedDense, IndividualSparse, IndividualDense }
+
+    [MessagePackObject, MessagePackFormatter(typeof(RandomEventsInitializationChorePacketFormatter))]
+    public sealed class RandomEventsInitializationChorePacket
+    {
+        [Key(0)] public int ProtocolVersion;
+        [Key(1)] public int OperationId;
+        [Key(2)] public byte[] ConfigurationDigest = Array.Empty<byte>();
+        [Key(3)] public ulong PrngState0;
+        [Key(4)] public ulong PrngState1;
+        [Key(5)] public int NextDueAbsoluteMonth;
+        [Key(6)] public int StartAbsoluteMonth;
+        [Key(7)] public int CooldownEncoding;
+        [Key(8)] public int[] CooldownData = Array.Empty<int>();
+    }
+
+    [MessagePackObject, MessagePackFormatter(typeof(RandomEventsBatchChorePacketFormatter))]
+    public sealed class RandomEventsBatchChorePacket
+    {
+        [Key(0)] public int ProtocolVersion;
+        [Key(1)] public int OperationId;
+        [Key(2)] public ulong PrngState0;
+        [Key(3)] public ulong PrngState1;
+        [Key(4)] public int DueAbsoluteMonth;
+        [Key(5)] public int[] EventKinds = Array.Empty<int>();
+        [Key(6)] public int[] EventStrengths = Array.Empty<int>();
+        [Key(7)] public int[] TargetPlayerIds = Array.Empty<int>();
+    }
+
+    [MessagePackObject, MessagePackFormatter(typeof(RandomEventsSignpostChorePacketFormatter))]
+    public sealed class RandomEventsSignpostChorePacket
+    {
+        [Key(0)] public int ProtocolVersion;
+        [Key(1)] public int OperationId;
+    }
+
+    public sealed class RandomEventsInitializationChorePacketFormatter : IMessagePackFormatter<RandomEventsInitializationChorePacket>
+    {
+        private const int FieldCount = 9;
+        public void Serialize(ref MessagePackWriter writer, RandomEventsInitializationChorePacket value, MessagePackSerializerOptions options)
+        {
+            if (value == null) { writer.WriteNil(); return; }
+            writer.WriteArrayHeader(FieldCount);
+            writer.Write(value.ProtocolVersion); writer.Write(value.OperationId);
+            RandomEventsMessagePack.WriteByteArray(ref writer, value.ConfigurationDigest);
+            writer.Write(value.PrngState0); writer.Write(value.PrngState1);
+            writer.Write(value.NextDueAbsoluteMonth); writer.Write(value.StartAbsoluteMonth);
+            writer.Write(value.CooldownEncoding);
+            RandomEventsMessagePack.WriteIntArray(ref writer, value.CooldownData);
+        }
+
+        public RandomEventsInitializationChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
+        {
+            if (reader.TryReadNil()) return null;
+            RandomEventsMessagePack.RequireFieldCount(ref reader, FieldCount, "initialization Chore");
+            return new RandomEventsInitializationChorePacket
+            {
+                ProtocolVersion = reader.ReadInt32(), OperationId = reader.ReadInt32(),
+                ConfigurationDigest = RandomEventsMessagePack.ReadByteArray(ref reader, 32),
+                PrngState0 = reader.ReadUInt64(), PrngState1 = reader.ReadUInt64(),
+                NextDueAbsoluteMonth = reader.ReadInt32(), StartAbsoluteMonth = reader.ReadInt32(),
+                CooldownEncoding = reader.ReadInt32(), CooldownData = RandomEventsMessagePack.ReadIntArray(ref reader, 240)
+            };
+        }
+    }
+
+    public sealed class RandomEventsBatchChorePacketFormatter : IMessagePackFormatter<RandomEventsBatchChorePacket>
+    {
+        private const int FieldCount = 8;
+        public void Serialize(ref MessagePackWriter writer, RandomEventsBatchChorePacket value, MessagePackSerializerOptions options)
+        {
+            if (value == null) { writer.WriteNil(); return; }
+            writer.WriteArrayHeader(FieldCount);
+            writer.Write(value.ProtocolVersion); writer.Write(value.OperationId);
+            writer.Write(value.PrngState0); writer.Write(value.PrngState1); writer.Write(value.DueAbsoluteMonth);
+            RandomEventsMessagePack.WriteIntArray(ref writer, value.EventKinds);
+            RandomEventsMessagePack.WriteIntArray(ref writer, value.EventStrengths);
+            RandomEventsMessagePack.WriteIntArray(ref writer, value.TargetPlayerIds);
+        }
+
+        public RandomEventsBatchChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
+        {
+            if (reader.TryReadNil()) return null;
+            RandomEventsMessagePack.RequireFieldCount(ref reader, FieldCount, "batch Chore");
+            return new RandomEventsBatchChorePacket
+            {
+                ProtocolVersion = reader.ReadInt32(), OperationId = reader.ReadInt32(),
+                PrngState0 = reader.ReadUInt64(), PrngState1 = reader.ReadUInt64(), DueAbsoluteMonth = reader.ReadInt32(),
+                EventKinds = RandomEventsMessagePack.ReadIntArray(ref reader, 135),
+                EventStrengths = RandomEventsMessagePack.ReadIntArray(ref reader, 135),
+                TargetPlayerIds = RandomEventsMessagePack.ReadIntArray(ref reader, 135)
+            };
+        }
+    }
+
+    public sealed class RandomEventsSignpostChorePacketFormatter : IMessagePackFormatter<RandomEventsSignpostChorePacket>
+    {
+        public void Serialize(ref MessagePackWriter writer, RandomEventsSignpostChorePacket value, MessagePackSerializerOptions options)
+        {
+            if (value == null) { writer.WriteNil(); return; }
+            writer.WriteArrayHeader(2); writer.Write(value.ProtocolVersion); writer.Write(value.OperationId);
+        }
+
+        public RandomEventsSignpostChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
+        {
+            if (reader.TryReadNil()) return null;
+            RandomEventsMessagePack.RequireFieldCount(ref reader, 2, "signpost Chore");
+            return new RandomEventsSignpostChorePacket { ProtocolVersion = reader.ReadInt32(), OperationId = reader.ReadInt32() };
+        }
+    }
+
+    internal static class RandomEventsMessagePack
+    {
+        public static void RequireFieldCount(ref MessagePackReader reader, int expected, string label)
+        {
+            int count = reader.ReadArrayHeader();
+            if (count != expected)
+                throw new MessagePackSerializationException($"RandomEvents {label} has {count} fields; expected exactly {expected}.");
+        }
+
+        public static void WriteIntArray(ref MessagePackWriter writer, int[] values)
+        {
+            if (values == null) { writer.WriteNil(); return; }
+            writer.WriteArrayHeader(values.Length);
+            for (int index = 0; index < values.Length; index++) writer.Write(values[index]);
+        }
+
+        public static int[] ReadIntArray(ref MessagePackReader reader, int maximumLength)
+        {
+            if (reader.TryReadNil()) return Array.Empty<int>();
+            int length = reader.ReadArrayHeader();
+            if (length < 0 || length > maximumLength)
+                throw new MessagePackSerializationException($"RandomEvents integer-array length {length} exceeds {maximumLength}.");
+            int[] values = new int[length];
+            for (int index = 0; index < length; index++) values[index] = reader.ReadInt32();
+            return values;
+        }
+
+        public static void WriteByteArray(ref MessagePackWriter writer, byte[] values) => writer.Write(values ?? Array.Empty<byte>());
+        public static byte[] ReadByteArray(ref MessagePackReader reader, int expectedLength)
+        {
+            byte[] values = reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
+            if (values.Length != expectedLength)
+                throw new MessagePackSerializationException($"RandomEvents byte-array length {values.Length}; expected {expectedLength}.");
+            return values;
+        }
+    }
+}

diff --git a/RandomEvents/src/RandomEventsCooldownCodec.cs b/RandomEvents/src/RandomEventsCooldownCodec.cs
new file mode 100644
index 00000000..bd8269d7
--- /dev/null
+++ b/RandomEvents/src/RandomEventsCooldownCodec.cs
@@ -0,0 +1,90 @@
+using System;
+using System.Collections.Generic;
+
+namespace RandomEvents
+{
+    internal sealed class RandomEventsCooldownPayload
+    {
+        public RandomEventsCooldownEncoding Encoding;
+        public int[] Data = Array.Empty<int>();
+    }
+
+    internal static class RandomEventsCooldownCodec
+    {
+        internal const int EventCount = 15;
+        internal const int MaximumPlayers = 8;
+        internal const int FullIndividualLength = (MaximumPlayers + 1) * EventCount;
+
+        public static RandomEventsCooldownPayload[] CreateCandidates(RandomEventsRuntimeState state)
+        {
+            if (state.MultiplayerMode == 0)
+            {
+                if (AllZero(state.SharedCooldownUntilAbsoluteMonths)) return None();
+                return new[] { new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.SharedDense, Data = (int[])state.SharedCooldownUntilAbsoluteMonths.Clone() } };
+            }
+            if (AllZero(state.IndividualCooldownUntilAbsoluteMonths)) return None();
+            var sparse = new List<int>();
+            for (int index = EventCount; index < FullIndividualLength; index++)
+            {
+                int value = state.IndividualCooldownUntilAbsoluteMonths[index];
+                if (value == 0) continue;
+                sparse.Add(index); sparse.Add(value);
+            }
+            int[] dense = new int[MaximumPlayers * EventCount];
+            Array.Copy(state.IndividualCooldownUntilAbsoluteMonths, EventCount, dense, 0, dense.Length);
+            return new[]
+            {
+                new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.IndividualSparse, Data = sparse.ToArray() },
+                new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.IndividualDense, Data = dense }
+            };
+        }
+
+        public static void Decode(int multiplayerMode, int encodingValue, int[] data, out int[] shared, out int[] individual)
+        {
+            shared = new int[EventCount]; individual = new int[FullIndividualLength];
+            int[] values = data ?? Array.Empty<int>();
+            var encoding = (RandomEventsCooldownEncoding)encodingValue;
+            if (encoding == RandomEventsCooldownEncoding.None)
+            {
+                if (values.Length != 0) throw new InvalidOperationException("None cooldown encoding contains data.");
+                return;
+            }
+            if (multiplayerMode == 0)
+            {
+                if (encoding != RandomEventsCooldownEncoding.SharedDense || values.Length != EventCount)
+                    throw new InvalidOperationException("Shared mode requires exactly 15 dense cooldown values.");
+                ValidateNonNegative(values); Array.Copy(values, shared, EventCount); return;
+            }
+            if (multiplayerMode != 1)
+                throw new InvalidOperationException("Unknown multiplayer mode.");
+            if (encoding == RandomEventsCooldownEncoding.IndividualDense)
+            {
+                if (values.Length != MaximumPlayers * EventCount)
+                    throw new InvalidOperationException("Individual dense cooldown data has the wrong length.");
+                ValidateNonNegative(values); Array.Copy(values, 0, individual, EventCount, values.Length); return;
+            }
+            if (encoding != RandomEventsCooldownEncoding.IndividualSparse || (values.Length & 1) != 0)
+                throw new InvalidOperationException("Individual sparse cooldown data is malformed.");
+            var seen = new HashSet<int>();
+            for (int offset = 0; offset < values.Length; offset += 2)
+            {
+                int index = values[offset]; int month = values[offset + 1];
+                if (index < EventCount || index >= FullIndividualLength || !seen.Add(index) || month <= 0)
+                    throw new InvalidOperationException("Individual sparse cooldown entry is invalid or duplicated.");
+                individual[index] = month;
+            }
+        }
+
+        private static RandomEventsCooldownPayload[] None() => new[] { new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.None } };
+        private static bool AllZero(int[] values)
+        {
+            if (values == null) return true;
+            for (int index = 0; index < values.Length; index++) if (values[index] != 0) return false;
+            return true;
+        }
+        private static void ValidateNonNegative(int[] values)
+        {
+            for (int index = 0; index < values.Length; index++) if (values[index] < 0) throw new InvalidOperationException("Cooldown values cannot be negative.");
+        }
+    }
+}

diff --git a/RandomEvents/src/RandomEventsDiagnostics.cs b/RandomEvents/src/RandomEventsDiagnostics.cs
new file mode 100644
index 00000000..32c2fc00
--- /dev/null
+++ b/RandomEvents/src/RandomEventsDiagnostics.cs
@@ -0,0 +1,178 @@
+using MessagePack;
+using SHCDESE.API;
+using SHCDESE.API.Components.Network;
+using System;
+using System.Collections.Generic;
+using System.Diagnostics;
+using System.IO;
+using System.Linq;
+using System.Security.Cryptography;
+using System.Text;
+
+namespace RandomEvents
+{
+    internal static class RandomEventsDiagnostics
+    {
+        public static byte[] SerializeAndVerify(RandomEventsInitializationChorePacket packet) =>
+            Verify(packet, SameInitialization, "initialization Chore");
+        public static byte[] SerializeAndVerify(RandomEventsBatchChorePacket packet) =>
+            Verify(packet, SameBatch, "batch Chore");
+        public static byte[] SerializeAndVerify(RandomEventsSignpostChorePacket packet) =>
+            Verify(packet, (a, b) => a != null && b != null && a.ProtocolVersion == b.ProtocolVersion && a.OperationId == b.OperationId, "signpost Chore");
+        public static byte[] SerializeAndVerify(RandomEventsInitializationAckPacket packet) =>
+            Verify(packet, (a, b) => a != null && b != null && a.ProtocolVersion == b.ProtocolVersion && a.OperationId == b.OperationId &&
+                a.PlayerId == b.PlayerId && BytesEqual(a.StateDigest, b.StateDigest), "initialization ACK");
+
+        public static string RunSerializerSelfTests(int protocolVersion)
+        {
+            byte[] digest = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
+            var initialization = new RandomEventsInitializationChorePacket
+            {
+                ProtocolVersion = protocolVersion, OperationId = 1, ConfigurationDigest = digest,
+                PrngState0 = 0x0123456789ABCDEFUL, PrngState1 = 0xFEDCBA9876543210UL,
+                NextDueAbsoluteMonth = 12346, StartAbsoluteMonth = 12345,
+                CooldownEncoding = (int)RandomEventsCooldownEncoding.None
+            };
+            var shared = CloneInitialization(initialization);
+            shared.CooldownEncoding = (int)RandomEventsCooldownEncoding.SharedDense;
+            shared.CooldownData = Enumerable.Range(0, 15).ToArray();
+            var dense = CloneInitialization(initialization);
+            dense.CooldownEncoding = (int)RandomEventsCooldownEncoding.IndividualDense;
+            dense.CooldownData = Enumerable.Range(0, 120).ToArray();
+            var emptyBatch = new RandomEventsBatchChorePacket
+            {
+                ProtocolVersion = protocolVersion, OperationId = 2,
+                PrngState0 = initialization.PrngState0, PrngState1 = initialization.PrngState1,
+                DueAbsoluteMonth = initialization.NextDueAbsoluteMonth
+            };
+            var maximumBatch = new RandomEventsBatchChorePacket
+            {
+                ProtocolVersion = protocolVersion, OperationId = 3,
+                PrngState0 = initialization.PrngState1, PrngState1 = initialization.PrngState0,
+                DueAbsoluteMonth = initialization.NextDueAbsoluteMonth,
+                EventKinds = Enumerable.Range(0, 135).Select(index => index % 15).ToArray(),
+                EventStrengths = Enumerable.Range(0, 135).Select(index => index * 1000 - 50000).ToArray(),
+                TargetPlayerIds = Enumerable.Range(0, 135).Select(index => index % 8 + 1).ToArray()
+            };
+            var results = new List<string>();
+            AddResult("initialization-none", SerializeAndVerify(initialization), results);
+            AddResult("initialization-shared", SerializeAndVerify(shared), results);
+            AddResult("initialization-individual-dense", SerializeAndVerify(dense), results);
+            AddResult("empty-batch", SerializeAndVerify(emptyBatch), results);
+            AddResult("maximum-batch", SerializeAndVerify(maximumBatch), results);
+            AddResult("signpost", SerializeAndVerify(new RandomEventsSignpostChorePacket { ProtocolVersion = protocolVersion, OperationId = 4 }), results);
+            AddResult("initialization-ack", SerializeAndVerify(new RandomEventsInitializationAckPacket
+            {
+                ProtocolVersion = protocolVersion, OperationId = 1, PlayerId = 2, StateDigest = digest
+            }), results);
+            return string.Join(", ", results);
+        }
+
+        public static byte[] GetStateDigestBytes(RandomEventsRuntimeState state)
+        {
+            if (state == null) return Array.Empty<byte>();
+            using (var stream = new MemoryStream())
+            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
+            using (SHA256 sha256 = SHA256.Create())
+            {
+                WriteByteArray(writer, state.ConfigurationDigest);
+                writer.Write(state.PrngState0); writer.Write(state.PrngState1);
+                writer.Write(state.NextDueAbsoluteMonth); writer.Write(state.StartAbsoluteMonth);
+                WriteIntArray(writer, state.SharedCooldownUntilAbsoluteMonths);
+                WriteIntArray(writer, state.IndividualCooldownUntilAbsoluteMonths);
+                writer.Write(state.BatchPrepared);
+                WriteIntArray(writer, state.PreparedDirectKinds);
+                WriteIntArray(writer, state.PreparedDirectStrengths);
+                WriteIntArray(writer, state.PreparedDirectTargetPlayerIds);
+                writer.Write(state.SignpostsInitialized);
+                WriteIntArray(writer, state.SignpostBuildingIds);
+                writer.Flush();
+                return sha256.ComputeHash(stream.ToArray());
+            }
+        }
+
+        public static string GetStateDigest(RandomEventsRuntimeState state) => ToHex(GetStateDigestBytes(state));
+        public static string HashBytes(byte[] bytes)
+        {
+            using (SHA256 sha256 = SHA256.Create()) return ToHex(sha256.ComputeHash(bytes ?? Array.Empty<byte>()));
+        }
+        public static string ToHex(byte[] bytes)
+        {
+            if (bytes == null) return "null";
+            var builder = new StringBuilder(bytes.Length * 2);
+            for (int index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("X2"));
+            return builder.ToString();
+        }
+        public static bool BytesEqual(byte[] left, byte[] right) =>
+            ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right));
+
+        public static string GetActionDigest(int[] kinds, int[] strengths, int[] targetPlayerIds)
+        {
+            using (var stream = new MemoryStream())
+            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
+            {
+                WriteIntArray(writer, kinds); WriteIntArray(writer, strengths); WriteIntArray(writer, targetPlayerIds);
+                writer.Flush(); return HashBytes(stream.ToArray());
+            }
+        }
+
+        public static string DescribeActions(int[] kinds, int[] strengths, int[] targetPlayerIds)
+        {
+            int count = kinds?.Length ?? 0;
+            if (count == 0) return "[]";
+            var entries = new string[count];
+            for (int index = 0; index < count; index++)
+                entries[index] = $"{index}:{kinds[index]}@P{targetPlayerIds[index]}={strengths[index]}";
+            return "[" + string.Join(",", entries) + "]";
+        }
+
+        public static string FormatPrng(ulong state0, ulong state1) => $"{state0:X16}:{state1:X16}";
+        public static string DescribeScriptExtenderBinary()
+        {
+            string path = typeof(GameNetworkAPI).Assembly.Location;
+            var file = new FileInfo(path); FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/RandomEvents.diff).
