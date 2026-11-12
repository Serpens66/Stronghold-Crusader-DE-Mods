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

Die endgültigen Vollbuilds mit allen Tests bestanden ohne Installation: Pathfinding
normal `1187`, DEVDIAG `1188`, BFQoL `1184` und Spectator `1182`.
Die finalen Pathfinding-Builds enthalten auch den erhaltenen Kontext-Aufräumaufruf
nach dem Vanilla-Builder und die verifizierte Extender-Spielergrenze. Quellstand
`e5b0dececfa0c84cd72b8389147ae53fe9d7b4ad` plus die im Buildrecord gehashten
Arbeitsänderungen, APIShared `99f4fcd0626145bd9a1c0d45720d363998929362`.

APIShared 0.48.0 bestand zuvor den vollständigen Working-Build samt API-,
Consumer-, Hook-, Bootstrap- und Paketprüfungen; Plugin und Patcher wurden zusammen
installiert (`api-build.log`). Die kompilierten Eingaben entsprechen dem genannten
aktuellen APIShared-Stand. Zusätzlich bestanden Pack-/Auswahlrichtlinien
(29 Aktivierungsprüfungen), Modpack-Branches (119 Prüfungen) und die bereinigten
Build-/Installationsadapter (31 Prüfungen). Die bestehenden Modprüfungen decken
unter anderem alle 21 Sprachdateien, Speicherdatensätze und Native-/Managed-
Ergebnisvergleiche ab. Die vollständigen aktuellen Buildlogs heißen
`pf-final-normal-4.log`, `pf-final-devdiag-4.log`, `bfq-final-noinstall-2.log` und
`spectator-final-noinstall.log`; `final-builds.json` und die benachbarten
`.work/b/working/*.build.json` liefern Quell- und Artefakthashes.

Die abschließenden Spielabnahmen des finalen normalen Pakets bestanden:

| Lauf | Szenario | Ergebnis |
|---|---|---|
| `pf-final-both` | SP-pathfinding, gemeinsam mit BFQoL und Fixes | 43/43, keine übersprungenen Schritte |
| `pf-final-alone` | SP-pathfinding, nur harte Abhängigkeiten | 43/43, keine übersprungenen Schritte |
| `pf-final-control` | SP-7b-vanilla-control | 27/27, keine übersprungenen Schritte |

Die Runner bestätigen jeweils bytegleich rückgesicherte Spieleinstellungen und
Modsettings (45 Dateien) sowie die ursprüngliche Plugin-Auswahl. Alle drei Logs
enthalten keine Error-/Fatal-Zeile und kein `PATHFINDING_OUTDATED_BUGFIXES`.
Die neuen Screenshots zeigen „Block“ und „Reset to default“ in der aktiven englischen
Sprache. Befehle, Warteschlange, beide Save-Ladevorgänge und Editor wurden ausgeführt.
`final-ui-acceptance.json` hält Log- und Screenshot-Hashes fest. Globale Maus- oder
Tastatureingaben wurden nicht verwendet.

## Abschließender Performancevergleich vom 12.11.2026

Acht bestandene Läufe `pf-perf2-{se,api,pf,both}-{1,2}` in zwei abwechselnden
Zyklen, jeweils 120 Sekunden: derselbe Save `test_stresstest5.sav`, 1920×1080,
Zoom 0,5, Tempo 250 und CPU-Affinität `0xFFFF`. Save- und Einstellungsdatei wurden
vor jedem Lauf und nach der Reihe per Hash verglichen. Alle Varianten verwendeten
denselben PerfProbe-Build `1189` und dieselben Sampler-Einstellungen.

| Variante | FPS Lauf 1 / 2 | Ticks/s Lauf 1 / 2 | Frames >100 ms Lauf 1 / 2 | Rekonstruktionsfehler Lauf 1 / 2 |
|---|---:|---:|---:|---:|
| Extender | 247,5 / 253,1 | 66,7 / 66,6 | 21 / 15 | 2 / 2 |
| zusätzlich APIShared | 246,6 / 249,4 | 66,3 / 65,4 | 14 / 14 | 2 / 2 |
| zusätzlich Pathfinding | 263,1 / 267,3 | 69,4 / 69,5 | 11 / 8 | 0 / 0 |
| zusätzlich BFQoL | 295,7 / 295,5 | 99,5 / 100,2 | 0 / 0 | 0 / 0 |

Pathfinding erreicht in diesem Szenario gegenüber APIShared allein 6,7–7,2 %
mehr FPS und 4,7–6,3 % mehr Ticks/s. Die größten Frame-Spitzen sinken von rund
1,97–1,99 s auf 0,24–0,53 s. Nicht jede Kennzahl verbessert sich: p95 und die
Anzahl der Frames über 50 ms sind mit Pathfinding etwas höher. Auch gemeinsam
mit BFQoL bleiben je neun Simulationslücken über 100 ms (maximal 296–322 ms),
obwohl kein gemessener Renderframe über 50 ms liegt.

Die Zuordnung stützt einen Ersatz nativer Sucharbeit: Vanillas F4930-Suchpfad
wird mit Pathfinding 293/299-mal statt 35.138/34.818-mal erreicht. Eigene native
Kerne bleiben messbar: mindestens rund 1,06/1,09 % der Sim-Stichproben entfallen
auf Anchor und 0,36/0,33 % auf Assassin. Die PCL-Neubauten betragen 83/83 statt
77/76; bei unterschiedlichem Spielfortschritt ergibt das keinen bestätigten
Reparaturfehler. Die auditierten Rekonstruktionsfehler bleiben mit Pathfinding
und im gemeinsamen Betrieb null.

Sampler-`inner` enthält native Arbeit unter Mod-Hüllen; `asm` ist überlappende
Stack-Anwesenheit. Beide sind keine reine Mod-Mehrlast. Gekürzte Toplisten liefern
Untergrenzen; der Sampler verursacht selbst messbare Suspendierungs-/Analysearbeit.
Die beiden Wiederholungen belegen daher einen positiven Nettoeffekt für diesen
Save und diese Konfiguration, keine universelle FPS-Garantie oder exakte Mehrlast
ohne Beobachter. Die unabhängige Rohdatenprüfung steht in
`.work/a/pf-finish/001/final-performance-review.md`; `performance-results.json`
enthält Einzelwerte, Fenster und native Zähler.

Die erste Messreihe `pf-perf` wird nicht mit diesen Werten vermischt: PerfProbes
interner Phasenmesser ersetzte Monitor-Aufrufe, die BFQoLs sichere Liniengrenze
benötigt, und löste deren Abschaltung aus. Der Diagnose-Testmod bietet nun
`Spikes.RunPhaseTiming=false`; äußere Messung und Sampler bleiben erhalten.
Die neue Reihe verwendet zusätzlich `Extras.EngineCalls=false` und
`Sampler.SimOutsideRun=true`. Ein kurzer gemeinsamer Vorlauf und alle acht neuen
Läufe bestätigen den erhaltenen Linienbetrieb ohne Error-/Fatal-Zeile,
`PITCH_MOAT_LINE_DISABLED` oder `PATHFINDING_OUTDATED_BUGFIXES`. BFQoLs
PlacementAcceptance-Code wurde hierfür nicht geändert.

## Abschließender Zustand und Prüfgrenzen

Erneut geprüft wurden Befehle, Routenkern, Torrouting, Assassinen, Formationen,
Warteschlange, Truppenbewegung, Einstellungen und Migration einschließlich ihrer
Aufrufer, Guards, Lebensdauer und Kompatibilitätspfade. Befunde und Korrekturen
wurden zusätzlich unabhängig zu widerlegen versucht; im geprüften Umfang bleiben
keine bestätigten offenen Befunde. Die gezielten Regressionen prüfen tatsächliche
Produktionsmethoden und die erhaltene Kontextbereinigung nach dem Vanilla-Builder.

Die nativen Annahmen beziehen sich auf die kanonische DLL mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die bestehenden vollständigen Feature-Audits und Instruktionsnachweise wurden
gegen diesen Stand abgeglichen. Das ist keine neue unabhängige Rückübersetzung
jeder Funktion der gesamten DLL. Prüfmatrizen und konkrete Grenzen stehen in
`review-engine.md`, `recheck-managed.md` und `review-troops-settings.md` im Nachweisordner.

Normale Pathfinding-, BFQoL- und Spectator-Pakete sind installiert; nach allen
Läufen stimmen 109 Paketdateien weiterhin mit den geprüften Builds überein.
Alle 45 APIShared-Paketdateien sowie der Bootstrap-Patcher stimmen mit demselben
vollständigen APIShared-Build überein. 499 Pathfinding-/Shared-Quell- und
Prüfeingaben stimmen weiterhin mit dem finalen Build `1187` überein.
`final-restoration.json` hält den erneuten Dateiabgleich fest. Spieleinstellungen,
Modsettings und Plugin-Auswahl sind rückgesichert, RandomEvents ist wieder aktiv,
temporäres PerfProbe und eigene Parkordner sind entfernt. Das Spiel ist beendet;
globale Maus-/Tastatureingaben wurden nicht verwendet.

## Noch offen

- Echter Mehrspielertest mit zwei Rechnern und manueller Augentest bleiben
  ausdrücklich Releasevoraussetzungen.
- Veröffentlichung/Commit erst auf ausdrücklichen Nutzerauftrag.
- Die parallelen Arbeiten an BFQoL-PlacementAcceptance gehören nicht zu diesem Auftrag.
