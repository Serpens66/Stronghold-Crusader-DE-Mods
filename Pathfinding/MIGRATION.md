# Entfernungsliste: Code aus BugfixesAndQoL und MoatMove

Momentaufnahme vom 07.11.2026, Quellstand `develop` (HEAD beim Kopieren siehe `originHead` in `migration/source-map.json`). Prüfweg: `tools/Test-MigrationDrift.ps1` zeigt je Datei, ob sich die Quelle seit dem Kopieren geändert hat. Verweise nutzen Symbolnamen, weil Zeilennummern veralten.

Dieser Schritt läuft erst auf ausdrücklichen Nutzerauftrag nach der Abnahme des neuen Mods.

## Vorher erledigen

1. `tools/Test-MigrationDrift.ps1` ausführen und jede Änderung in `Pathfinding/src` nachziehen.
2. Audit der Hook-Reihenfolge von BFQoL und Pathfinding im gemeinsamen Betrieb (`0x196483` Mounted-Hook zieht mit um; `0x59359` MoatTerrain liegt in `0x59210` Recovery; `0x119F90` V14 neben `0x124740`).
3. Echten Mehrspielertest und Augentest abschließen (bisher nicht gelaufen).

## Dateien

Vollständige Liste mit Blobs: `migration/source-map.json` (`origin`, `target`, `action`).

| `action` | Bedeutung |
|---|---|
| `remove` | Datei in der Quelle löschen, sie lebt nur noch hier |
| `keep` | Hilfsquelle, die auch BFQoL weiter braucht; Kopie bleibt oder wandert nach `Shared/Runtime` |
| `move-to-shared` | Muss nach `Shared/Runtime`, weil beide Mods sie brauchen |

Ordner, die in BFQoL komplett entfallen: `src/UnitCommands`, `src/Formations`, `src/CommandQueue`, `src/TroopMovement`, `native/` und `Build-AssassinSearch.ps1`, dazu `UnitCommandImports.cs`. `Testmods/MoatMove` entfällt ganz.

Bleiben in BFQoL:
- `src/GateRouting`: nur `GatehouseDistanceOriginRegistration`, `GatehouseTargetMarkerHeightHook`, `GatehouseTargetMarkerHeightPolicy`
- `src/Assassins`: nur `AssassinCapturedGateProtectionPolicy`, `AssassinCapturedGateProtectionRuntime`

## Offene Kopplungen in BFQoL

| Stelle | Lösung beim Entfernen |
|---|---|
| `VanillaPerformance/TribeMemberLookupOptimization` nutzt `TribeFloodMembership.BodyMatches` | `BodyMatches` nach `Shared/Runtime/Native` |
| `Hud/ForeignTroopHudRuntime` ruft `FormationHudButton.HideForForeignHud` | Hinweis „fremde Truppen-HUD aktiv“ über APIShared `TroopActionHud`, Pathfinding reagiert darauf |
| `VanillaPerformance/GateAttackFreezeRun.DevDiag` nutzt `GateAttackFreezeCapture` | Datei und Aufruf `DiagInstallFreeze` aus BFQoL entfernen. In Pathfinding fehlt der Starter noch: die Freeze-Diagnose bleibt im DEVDIAG-Build inaktiv, bis jemand sie wieder braucht |
| `Core/Common/FeatureSaveStates`: `AssassinClimbSaveState`, `TroopMovementSyncSaveState` | Aus BFQoL entfernen; IDs `BugfixesAndQoL-AssassinClimb` und `BugfixesAndQoL-TroopMovementSync` bleiben im neuen Mod unverändert, damit alte Spielstände lesbar bleiben |
| `Core/Common/SimulationRunScope` | Wird in BFQoL dann nicht mehr gelesen; Setzer in `EnginePresentationBridge` entfernen, wenn kein Leser bleibt. Pathfinding setzt den Marker im `EngineInterface.run`-Hook (`FormationRuntime.RunOriginalOnce`) |
| `Core/Native/DemandGatedContextHook`, `HookInstallPhase`, `PermanentInstructionSkipPatch`, `NativeInstructionReplayEmitter`, `NativeHandlerSnapshot`, `BugfixesHookInfrastructure`, `BfqolNativeCache`, `MultiplayerFeatureGate`, `SessionSaveRecordSlot`, `LocalPerPlayerSetting`, `LocalSelectionSnapshot` | Bleiben in BFQoL; bei Bedarf nach `Shared/Runtime` zusammenführen |
| `InternalsVisibleTo("MoatMove")` in `UnitCommandContracts` | Entfällt mit der Datei |

## BFQoL-Bootstrap

- `BugfixesAndQoLPlugin`: `FormationFeature.Configure`, `GateRoutingFeature.Configure`, `FormationFeature.Initialize`.
- `BugfixesAndQoLRuntime`: Felder und Aufrufe von `troopMovementFixRuntime`, `assassinClimbRuntime`, `assassinClimbCancellationRuntime`, `assassinPathfindingRuntime`, `assassinCombatResumeRuntime`, `friendlyMoatMovementRuntime`, `processUnitCommandPathRuntime`, `extendedShiftCommandQueueRuntime`, `assemblyPointPlacementPatch`, `mountedStockpileMovementPatch`; Methoden `InitializeFriendlyMoatMovement`, `InitializeExtendedShiftCommandQueue`, `ApplyAssassinGatehouseClimbFix`, `Apply/Install/Disable…AssemblyPoint…`, `…MountedStockpile…`.
- `BugfixesAndQoLRuntime.BuildingAndEconomy`: `reachableEnemyGatehouseRuntime`, `fastRecruitMovementBridge`, `ApplyFastRecruitRallyMovementSetting`. Die Paketreihenfolge in `InitializeBuildingFeatureNetwork` ist eine Mehrspieler-Protokollgrenze: Paket-IDs von `ReachableEnemyGatehouseRuntime` beim Entfernen prüfen.

## Einstellungen

`Settings/BugfixesAndQoLViewModel`: Felder, Properties, `…Text`/`…HelpText`, Reset-Einträge, `IUnitCommandSettings`. Die Properties in `IsRetiredPresetProperty` aufnehmen.

`EnableImprovedManualUnitCommands`, `EnableImprovedMoatFilling`, `EnableLadderAttackPathfindingFix`, `EnableEnemyGatePathfindingFix`, `RequireReachableEnemyForAutomaticGateClosing`, `EnableImprovedAssassinPathfinding`, `EnableImprovedAssassinPathfindingAI`, `EnableAssassinGatehouseClimbFix`, `EnableAssassinCombatResumeFix`, `EnableMoveFormationEnhancements`, `EnableExtendedShiftCommandQueue`, `EnableTroopMovementFix`, `EnableRallyTerrainSlowdownFix`, `EnableFastRecruitRallyMovement`, `EnableMountedStockpileMovementFix`, `EnableAssemblyPointPlacementFix`

- `EnableVanillaPerformanceOptimizations` und `EnableClientFeatures` bleiben auch in BFQoL. Der Hilfetext der BFQoL-Option nennt die Gruppenmitgliedsprüfung und muss angepasst werden.
- `Settings/ModSettingsPresentation.Topics` und `.Recommendations`: Einträge obiger Schlüssel.
- `Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml`: die Zeilen obiger Schlüssel.
- `Locales/*.txt` (21 Sprachen): `BugfixesAndQoL.<Key>`, `BugfixesAndQoL.<Key>Help`, `BugfixesAndQoL.SettingsRework.Reason.<Key>`, `SomeSettings.EnableTroopMovementFix…`, `SomeSettings.EnableImprovedAssassinPathfinding…`, `BugfixesAndQoL.Lineup*`, `BugfixesAndQoL.ArrangementTooltip`, `BugfixesAndQoL.RememberRows*`.
- `[Formation]` und DEVDIAG-`[GateMigrationDiagnostics]` in der BFQoL-BepInEx-Config entfallen; `LegacyBfqolSettings` übernimmt die Werte.

## Tests und Prüfungen

- BFQoL-Tests: `FriendlyMoatMovement.Tests`, `GateRouting.Tests`, `AssassinPathfinding.Tests`, `Formations.Core.Tests`, `Formations.Tests`, `FormationSpeech.Tests`, `ExtendedShiftCommandQueue.Tests`, `Issue6.Tests`, `ImprovedMoatFilling.Tests.csproj`, die zugehörigen Teile von `Program.cs` und `MovementFastPathParityHarness.cs`, `TribeMemberLookup.Tests` (nur `BodyMatches`), `GatehouseDiagnostics`. Laufen bisher nur gegen die BFQoL-Quellen; beim Entfernen nach `Pathfinding/tests` umziehen und Pfade umstellen.
- `BugfixesAndQoL/build.bat`: Schritte dieser Suiten, `_inspect/AssassinGateClimb/verify.ps1`, `_inspect/AssassinAttackControlAudit/verify-integrated.ps1`, `_inspect/FormationIntegration/Verify-Interop.ps1`, `_inspect/HostClientPresetTests` (`EnemyGateRoutingPresetTests`), `Test-RuntimePreflight.ps1`, Build der nativen Assassinen-DLL. Modpfad parametrisieren, Garantien behalten.
- `Testmods/GroupPathProbe` (`AnchorCoreInterop`, `AnchorCoreGate`), `Testmods/BetterPathfindingTest`, `Testmods/EnemyBridgePathTest`, `Testmods/UiAcceptance` (Probe an `0x196100`), `Testmods/PerfProbe/DIAGNOSTICS.md`.

## Workspace-Werkzeuge

- `Shared/Tools/Validation/Test-UnitCommandSplit.ps1`, `UnitCommandSourceChecks/Program.cs`, `CommandDependencyReport.cs`, `Test-PermanentNativeRuntimePatches.ps1`: BFQoL/MoatMove durch Pathfinding ersetzen; die Grenze Besitzer/Provider liegt dann innerhalb eines Mods.
- `Shared/Tools/Validation/source-check-projects.txt`: `Testmods/MoatMove/MoatMove.csproj` und die umgezogenen Testprojekte.
- `Shared/Tools/BranchBuild/BranchBuild.Common.ps1`, `Test-BranchInstallAdapters.ps1`, `APIShared/tools/BranchBuild/BranchBuild.Common.ps1`: MoatMove-Installationsadapter.
- `Shared/Tools/ScriptExtenderUpdate/mods.json`: MoatMove-Eintrag; alte Kompatibilitätsdateien bleiben als Historie.

## Veröffentlichung

- `Shared/Tools/Release/release-projects.json`, `Shared/Tools/Steam/WorkshopConfiguration.ps1`, `Test-SteamPackPolicy.ps1`, `ModpackSource.Common.ps1`, `Shared/Docs/runtime-health-coverage.json`, `README.md` (Modtabelle), `SerpsModsHost/info.json`.
- `[BepInIncompatibility("BugfixesAndQoL_Serp")]` und `("MoatMove_Serp")` in `PathfindingPlugin` entfernen; Abschnitt „Good to know“ der README anpassen.
- Pack-Regel: Soft-Abhängigkeit `SerpsMods_Serp` ist gesetzt, `EnableMod` ist der Aktivierungsschalter.

## Dokumentation

`BugfixesAndQoL/UpdateToNewDLL.md` (die nach `Pathfinding/UpdateToNewDLL.md` übernommenen Abschnitte), `BugfixesAndQoL/README.md` (nur auf Nutzerauftrag), `AGENTS/apishared.md`, `AGENTS/performance.md`, `Shared/Docs/SOURCE_OWNERSHIP.md`, `APIShared/docs/API_CATALOG.md`, `APIShared/docs/PATHFINDING_RULES.md`, `APIShared/ARCHITECTURE.md`.
