# Auslagerung aus BugfixesAndQoL und MoatMove

Momentaufnahme vom 07.11.2026 (Quellstand `develop`, Arbeitsstand). Prüfweg: `git status`, `tools/Test-MigrationDrift.ps1` und `Test-Modsettings.ps1` in BFQoL. Herkunft je Datei mit Git-Blob: `migration/source-map.json`.

Pathfinding besitzt jetzt Befehle, Routenkern, Tor-Routing, Assassinen-Wege, Formationen, Shift-Warteschlange und Truppenbewegung. BFQoL und Pathfinding laufen gemeinsam. Nur der alte Testmod MoatMove bleibt inkompatibel (`BepInIncompatibility`).

## Entfernt aus BugfixesAndQoL

- **Quellen:** `src/UnitCommands`, `src/Formations`, `src/CommandQueue`, `src/TroopMovement`, `native/`, `UnitCommandImports.cs`, `Core/Common/SimulationRunScope.cs`, `Core/Native/NativeHandlerSnapshot.cs`, `VanillaPerformance/GateAttackFreezeRun.DevDiag.cs`.
- **`src/GateRouting`:** bis auf `GatehouseDistanceOriginRegistration`, `GatehouseTargetMarkerHeightHook`, `GatehouseTargetMarkerHeightPolicy`.
- **`src/Assassins`:** bis auf `AssassinCapturedGateProtectionPolicy`, `AssassinCapturedGateProtectionRuntime`.
- **Einstellungen:** 16 Schlüssel. Sie stehen in `IsRetiredPresetProperty`, alte Presets bleiben ladbar. Dazu Zeilen in `BugfixesAndQoLSettings.xaml`, `ModSettingsPresentation`, Texte in 21 Sprachen, Hilfetext von `EnableVanillaPerformanceOptimizations`.
- **XAML-Patches:** Assassinen-Stil, Formationssymbole, Menü- und Rollover-Hosts, Vorschau-Canvas.
- **Tests und Build:** umgezogene Suiten, `Test-GateRouting.ps1`, zugehörige `build.bat`-Schritte, `/checks:gatehouse`.

## Gelöste Kopplungen

| Stelle | Lösung |
|---|---|
| `TribeMemberLookupOptimization` | Eigene `BodyMatches` |
| `ForeignTroopHudRuntime` | Meldet über `TroopActionHud.NotifyForeignPanelActivating()`, Pathfinding hört auf `ForeignPanelActivating` |
| `FeatureSaveStates` | Jeder Mod hält nur noch seine Datensätze; die IDs `BugfixesAndQoL-AssassinClimb` und `BugfixesAndQoL-TroopMovementSync` bleiben unverändert, damit alte Spielstände lesbar bleiben |
| `SimulationRunScope` | Pathfinding setzt den Marker auf jedem Pfad seines `EngineInterface.run`-Hooks, unabhängig von den Optionen |
| Helfer (`DemandGatedContextHook`, `HookInstallPhase`, `BugfixesHookInfrastructure`, `MultiplayerFeatureGate` u. a.) | Bleiben je Mod eine eigene Kopie; ein Umzug nach `Shared/Runtime` wäre nur Aufräumen |

## Umgezogene Prüfungen

- **Neu in Pathfinding:** `tests/Movement.Tests` (aus `ImprovedMoatFilling.Tests` und `Modsettings.Tests`), `tests/GatehouseDiagnostics`, Rally-Prüfung in `tests/Test-Preflight.ps1`.
- **Umgestellt:** `_inspect/FormationIntegration/Verify-Interop.ps1`, `_inspect/AssassinGateClimb/verify.ps1`, `Shared/Tools/Validation/{Test-UnitCommandSplit.ps1,UnitCommandSourceChecks,source-check-projects.txt,Test-PermanentNativeRuntimePatches.ps1}`, `_inspect/HostClientPresetTests`, `_inspect/BugfixesAndQoLNativeTests`, `Testmods/{BetterPathfindingTest,GroupPathProbe,EnemyBridgePathTest}`.
- **Bewusst geändert:** Die Klettern-Prüfung verlangt die Einordnung als „Fixes“ nicht mehr, weil Pathfinding keine Fix/QoL-Aufteilung hat. Die Host-Berechtigung bleibt Pflicht.
- **Entfallen:** `FreezeTests` in `EnginePresentation.Tests`, weil der Starter der Gate-Freeze-Diagnose wegfiel.

## Bereinigung vom 11.11.2026

- **Entfernt:** die DEVDIAG-Messungen `GateAttackFreezeCapture`, `GateAttackFreezeRuntime` und `ManualProbeCapture` samt Aufrufstellen und ihren Prüfungen in den Testgerüsten. Ihr einziger Starter lag in BFQoL und wurde beim Umzug gelöscht, sie liefen also nie mehr. Ebenfalls entfernt: die Startsperre gegen den alten Testmod `EnemyGatePathfindingTest`, BFQoLs Installationsschritt `Disable-LegacyGateTest.ps1` und der Abhängigkeitsbericht `CommandDependencyReport` (diente nur der Aufteilung zwischen BFQoL und MoatMove).
- **Bereinigt:** Messfelder der früheren exakten Pflichtroutensuche, die kein Code mehr beschreibt (`RequiredRouteMetrics`), und ihre immer leeren Logwerte. Compiler-Warnungen der Diagnoseschalter sind gezielt unterdrückt bzw. die Felder als DEVDIAG- oder Testschalter gekennzeichnet.
- **Ergänzt:** die beim Umzug fehlenden Formationsnamen (`Pathfinding.Lineup*`) und die Texte des Zurücksetzen-Knopfs in allen 21 Sprachen; `tests/Test-Preflight.ps1` prüft jetzt, dass jeder im Code verwendete Schlüssel `Pathfinding.Name` in jeder Sprachdatei steht.
- **Fehlermeldung:** Mit BugfixesAndQoL vor 1.0.215 meldet Pathfinding beim Start `PATHFINDING_OUTDATED_BUGFIXES`, weil beide Fassungen die Funktionen sonst doppelt ausführen.
- **Bleibt:** die übrigen DEVDIAG-Diagnosen der Torrouten (`TEMP_GATE_*`). Sie sind nur im Diagnose-Build enthalten und dienen weiter der Prüfung nach DLL-Updates.

## Gemeinsamer Betrieb

Quellaudit vom 07.11.2026: Keine Spanne wird von beiden Mods gehookt. Die Treffer sind Aufrufe, Validierungen gegen das Ladeabbild oder Kommentare.

| Adresse | BFQoL | Pathfinding |
|---|---|---|
| `0x119F90` | V14-Inline-Hook | Ruft die Funktion nur auf, liest das Ladeabbild |
| `0x11FFA7` / `0x123090` | Aufrufstelle | Funktionseintritt; der relozierte Aufruf läuft durch den Detour |
| `0x59210`, `0x639C0`, `0x69470` | MoatTerrain-Stellen im Funktionsinneren | Funktionseintritt |
| `EngineInterface.run` | Harmony-Transpiler | MonoMod-Hook; Reihenfolge wie zuvor, die Brücke zuerst |

Pathfinding lädt per Soft-Abhängigkeit nach BFQoL. `NativeSiteRegistry`-Einträge für Pathfinding sind nicht gesetzt; das wäre eine reine Diagnose-Ergänzung.

## Nachweise vom 07.11.2026

- **Builds:** `BugfixesAndQoL/build.bat` und `Pathfinding/build.bat` mit `/source:working` samt aller Tests grün und installiert.
- **Spieltest `SP-pathfinding`:** gemeinsam 39 von 39 Schritten, Pathfinding allein 37 von 37 (ohne die zwei Importschritte). Geprüft: Einstellungsseite, Formationsmenü, Befehle, Warteschlange, zwei Spielstände, Editor. BFQoL allein: Spielstart mit zwei Partien ohne Fehler.
- **Einstellungsimport:** Ein `false` aus der BFQoL-Datei kam in Pathfinding an, ein neues BFQoL lässt die alten Werte beim Start stehen.
- **Messung `test_stresstest5`, Tempo 250, 120 s, ohne DEVDIAG:**

| Variante | Ticks | Frames > 100 ms | Lag-Summe | Größter Frame |
|---|---|---|---|---|
| BFQoL + MoatMove (`pf-cmp-src-1`) | 7888 | 69 | 22,3 s | 2575 ms |
| BFQoL + Pathfinding (`pf-cmp-both-1`) | 8074 | 71 | 17,0 s | 622 ms |
| BFQoL + Pathfinding (`pf-cmp-both-2`, Routenstatistik an) | 8222 | 68 | 16,8 s | 540 ms |

Die Vergleichszeile lief mit Gräbenquerung an und einem älteren MoatMove-Paket, die neuen Zeilen mit den neuen Standardwerten. Protokolle: `.work/a/pathfinding-build/runs`. Größter verbleibender Einzelposten in den Lags ist die native Assassinensuche (`bfq_assassin_field_route`, rund 25 % der Stichproben).

Die laufenden Routenstatistiken (`stage=anchor-*`, `stage=fast-performance`, `anchor-move-unreached`, `anchor-unreachable-found`, `reachability-slow`) erscheinen nur mit `[Diagnostics] Enabled`. `tools/Run-PathfindingIteration.ps1 -RouteStats` schaltet das für einen Lauf ein.

## Noch offen

- Echter Mehrspielertest mit zwei Rechnern und Augentest.
- Veröffentlichung: `Shared/Tools/Release/release-projects.json`, `Shared/Tools/Steam/WorkshopConfiguration.ps1`, `Test-SteamPackPolicy.ps1`, `ModpackSource.Common.ps1`. BFQoL darf ohne Pathfinding nicht in den Pack, sonst fehlen die Funktionen.
- `BugfixesAndQoL/README.md` nennt die umgezogenen Funktionen noch (nur auf Nutzerauftrag ändern). BFQoL-Version und `SerpChangelog` werden erst zum Abschluss gesetzt.
- `Testmods/MoatMove` ist ohne BFQoL-Interna nicht mehr baubar; der Ordner und seine Installationsadapter in `Shared/Tools/BranchBuild` bleiben bis zur Entscheidung des Nutzers.
- Tote Einträge in `Shared/Runtime/Localization/SerpLocalization.cs` (Konstanten und englische Rückfalltexte der umgezogenen Schlüssel) sind harmlos. Das Entfernen würde alle verlinkenden Mods neu bauen und kann mit der nächsten Shared-Änderung erfolgen.
