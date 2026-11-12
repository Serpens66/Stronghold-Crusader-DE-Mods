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

## Abschlussarbeiten und Gegenprüfung vom 12.11.2026

Der Arbeitsstand wurde erneut nach Funktionsbereichen und mit unabhängigen Gegenprüfungen
untersucht. Nachweise liegen unter `.work/a/pf-finish/001`; dies ist eine datierte
Prüfbeobachtung, keine Zusicherung allgemeiner Fehlerfreiheit. Während der Arbeit wurde
`develop` durch parallele Arbeit von `340846a58` auf `e529b355f` fortgeschrieben.
Die nachfolgenden Korrekturen wurden darauf aufgebaut; dieser Auftrag erzeugt keine Commits.

- **Bereinigung:** MoatMove ist nach Prüfung der übernommenen Quellen entfernt.
  `migration/moatmove-notes.md` bündelt das weiterhin benötigte Wissen. Historische
  Quellen bleiben in Git, das alte Log unter `_inspect/MoatMove`; 175 ignorierte
  Nachweisdateien sind hashgeprüft unter `.work/a/moat-retired/001` erhalten.
  Die aktiven Buildadapter sind entfernt. Zwölf tatsächlich unbenutzte Shared-Texte
  samt Rückfallwerten sind entfernt; noch verwendete Assassin-Texte bleiben bestehen.
- **Befehle und Kosten:** Einzelabfragen der wartenden Fast-Befehle allokieren keine
  temporären Sammlungen mehr. Der ungenutzte verzögerte Bewegungskontext und seine
  privaten Weiterleitungen sind entfernt; die weiterhin verwendete
  Gruppenroutenverteilung bleibt erhalten. Ohne aktivierte Diagnostik entfallen
  deren nicht ausgegebene Messungen/Kategorien für Vanilla-Fallbacks.
- **Formationen:** Mittige Rollenplatzierung gruppiert Reihen einmalig statt die
  gesamten Plätze für jede Reihe erneut zu durchsuchen. Referenzvergleiche decken
  Formen, Rollen, gleiche Sortierwerte, lückenhafte Reihen und maximale Tiefe ab.
  Isolierte Messungen mit Produktionscode ergaben bei 10.000 Reihen rund
  439–466 ms vorher und 32–36 ms nachher, bei identischer Ergebnisprüfsumme;
  dies ist kein Ingame-FPS-Nachweis.
- **Tore:** Der funktionale Topologie-Fingerprint wird auch im normalen Build
  gespeichert. Erreichbarkeitsantworten gelten nur innerhalb desselben
  auditierten Tor-Abfragedurchlaufs; ein gleicher Extender-Zeitwert nach
  Simulationsdurchlauf, Laden oder Editorarbeit legitimiert keine Wiederverwendung.
  Die Produktionsmethoden werden in normalen und Diagnose-Fixtures geprüft.
- **Import:** Vor sicherem Migrationsjournal darf die Settingsregistrierung keine
  Standardwerte materialisieren. Explizite Änderungen bleiben auch dann erhalten,
  wenn die alte BFQoL-Datei zunächst nicht lesbar ist. Die neuen Verhaltenstests
  verwenden den echten Importer und den tatsächlichen Bootstrap-Aufrufpfad.
- **Netzwerk:** Assassin-Klettern und Formationen nutzen APIShareds native
  Absenderprüfung. Der Empfänger prüft die Spieleridentität beziehungsweise den
  lebenden Tribe-Besitzer. Pakettypen, Format und Registrierungsreihenfolge bleiben
  erhalten. Formationen behalten ihren synchronen Versand im Simulationspfad;
  die nur auf Unity erlaubte Kanal-Sendemethode wird dort nicht verwendet.
- **Abhängigkeiten und Veröffentlichung:** APIShared 0.48.0 bezeichnet die
  SafeIlHook-/SafeTranspiler-Verträge eindeutig; BFQoL und Spectator verlangen
  diese Mindestversion. Pathfinding benötigt weiterhin 0.46.0. Release- und
  Workshop-Registrierung sind ergänzt; die Packprüfung lehnt BFQoL ohne
  Pathfinding ab und berücksichtigt Änderungen an nativen Quellen.
  Beide Spieler-READMEs und die modweiten Versionen/Changelogs sind angepasst
  (Pathfinding 0.1.2, BFQoL 1.0.221, Spectator 0.2.3).

Der historische Drift von `FeatureSaveStates.cs` gegenüber `source-map.json` ist
beabsichtigt: BFQoL behält nur SingleBuildingPause, Pathfinding besitzt AssassinClimb
und TroopMovementSync. Die ursprünglichen Blob-Nachweise bleiben unverändert.

Der vor der Testsperre abgeschlossene UI-Lauf `pf-ui-recheck` bestand 43/43 Schritte
mit rückgesicherten Einstellungen und Plugin-Auswahl. Die gespeicherten Screenshots
zeigen „Block“ und „Reset to default“, das Log enthält keine Error-/Fatal-Zeilen
und kein `PATHFINDING_OUTDATED_BUGFIXES`. Dieser Lauf liegt vor den letzten
Import-, Cache- und Netzwerkänderungen und ersetzt deren Spielabnahme nicht.

## Noch offen

- Auf Nutzerwunsch derzeit **keine weiteren Spieltests oder Installationsänderungen**.
  Die spätesten Quellkorrekturen sind deshalb noch nicht im Spiel installiert.
- Nach erneuter Freigabe: finale gemeinsame und Standalone-Abnahme mit
  `SP-pathfinding.json`, `SP-7b-vanilla-control.json` und die vergleichbaren
  Performance-Läufe (Extender, APIShared, Pathfinding, BFQoL zusammen).
- Echter Mehrspielertest mit zwei Rechnern und manueller Augentest bleiben
  ausdrücklich Releasevoraussetzungen.
- Veröffentlichung/Commit erst auf ausdrücklichen Nutzerauftrag.
- Die parallelen Arbeiten an BFQoL-PlacementAcceptance gehören nicht zu diesem Auftrag.
