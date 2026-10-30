# MoatMove

Stand 30.10.2026. Dieses Dokument beschreibt nur den aktuellen Stand; der frühere Verlauf (Modi `precise`, `fast`, `FastNative`, Messläufe, Pläne) steht in der Git-Historie. Prüfbar über den Code unter `src/`, `native/` und die Tests unter `tests/`.

## Zweck

Addon zu BugfixesAndQoL (Testmod, nicht im Steam-Modpack):
- Einheiten queren eigene und verbündete fertige Burggräben.
- Gruppenbefehle von Spielern und KI bekommen gewichtete Routen aus einem gemeinsamen nativen Feld statt einer Vanilla-Suche je Einheit. Ein Graben wird genommen, wenn der Weg darüber günstiger ist.
- Optional: Einzelbefehle über denselben Kern (`AnchorSingleMoves`) und Ausweichen vor Feuer (`AvoidFire`).

MoatMove beeinflusst das Spielgeschehen (`NetworkMode = 1`).

## Abhängigkeiten und Zuständigkeiten

- **BugfixesAndQoL** besitzt alle nativen Befehls-Hooks (`BugfixesAndQoL/src/UnitCommands`). MoatMove registriert sich als Traversal-Provider (`FriendlyMoatTraversalProvider`, `UnitCommandPathAPI.RegisterTraversal`) und meldet `RequiredOnly = true`: Vanilla zuerst, Grabenwege nur, wo Kern oder Fast sie liefern.
- **APIShared** stellt die Routenregeln bereit (`RouteTileRules`, `RouteReplanPolicy`, `RouteSearchEvents`; Leitfaden `APIShared/docs/PATHFINDING_RULES.md`).
- **Script Extender** ab 2.14.0.
- **Native DLL:** an `CrusaderDE.dll` mit SHA-256 `FBCB9319…2831E2` gebunden. Verträge und Adressen: [UpdateToNewDLL.md](UpdateToNewDLL.md).
- Mit MoatMove läuft BugfixesAndQoLs Befehlslogik im Required-only-Ablauf. Dort ist BugfixesAndQoLs gewichtete Routenveröffentlichung aus (`TryCaptureBuilderWeightedScope`, einschließlich der gewichteten Auffüll-Optimierung); die gewichteten Gruppenrouten kommen aus dem Anker-Kern.

## Einstellungen

BepInEx-Konfiguration `BepInEx/config/MoatMove_Serp.cfg`, Abschnitt `[Movement]`. Änderungen wirken nach einem Neustart. Im Mehrspieler müssen alle Spieler dieselben Werte haben (keine Synchronisierung).

| Einstellung | Standard | Wirkung |
|---|---|---|
| `AnchorSingleMoves` | `false` | Einzelbefehle außerhalb von Gruppen nutzen den Kern (exaktes A*, Routenregeln) statt Vanillas Suche je Einheit. |
| `AvoidFire` | `false` | Feuer am Boden wird als teure Regelkachel eingetragen. Mit `AnchorSingleMoves` planen laufende Einheiten vor neuem Feuer um oder warten. |

Nur im DEVDIAG-Build (`build.bat /devdiag`, Abschnitt `[Diagnostics]`): `RuleProbeTicks`, `MoatCostProbeTicks`, `TopologyProbeTicks` (Selbstprüfungen der Regeln und der Cursor-Topologie, 0 = aus).

Es gibt keine Moduswahl mehr. Ein alter Eintrag `Mode` in einer vorhandenen Konfiguration wird ignoriert.

## Ablauf

### Gruppenbefehle (Anker-Kern)

- **Einstieg:** `TribeR3EventHooks.OnTribeIssueOrderMoveHere` (Pre/Post um `0x11B520`), nur auf dem Simulationsthread, für menschliche Spieler und KI.
- **Masken:**
  - Je Spieler, der lebende Einheiten steuert, hält der Kern Kanten- und Grabenbits aus demselben Prädikat wie Fast (`LiveEdge`).
  - Aufbau mit 4.096 Knoten je Tick; Neulesen nach Änderungen mit 1.024 Knoten je Tick.
  - Spielerunabhängige Knoten teilen ihre Wörter.
  - Kachelmeldungen von BugfixesAndQoL lesen nur das ±2-Umfeld neu; Zugangsänderungen nur die Grundflächen betroffener Torgebäude.
- **Aufgaben je Befehl:** Das Pre erfasst Starts und Profil. Es gibt eine gewichtete Aufgabe für grabenfähige Mitglieder und eine reine Bodenaufgabe für alle übrigen. Das Feld entsteht bei der ersten Abfrage eines Mitglieds.
- **Abfrage je Einheit:**
  - Abstieg im Ankerfeld, dazu ein exaktes Endstück zum eigenen Platz (Rand 8).
  - Für Starts außerhalb des Hauptfelds gibt es Zusatzanker, höchstens 48. Darüber gilt die Abfrage als „über der Grenze“, und die Einheit behält Vanilla.
  - Formationsziele werden gebündelt beantwortet (`mma_query_batch`), mit denselben Antworten wie Einzelabfragen.
- **Veröffentlichung:**
  - Ein günstigerer Grabenweg qualifiziert die Einheit (Moduserkennung).
  - Eine Bodenentscheidung ersetzt Vanillas `F4930` über `TryReplaceUnsafeFallbackPath`.
  - Jede Route wird live geprüft: Identität, Start, Ziel, Puffer, alle Kanten und Eigentümer.
- **Qualität:** Offline sind die Wege fast optimal; die Mehrkosten liegen im 95-%-Perzentil bei höchstens 6 % gegenüber einer exakten Suche je Einheit (GroupPathProbe).
- **Ausnahmen mit Fast bzw. Vanilla:**
  - Angriffe, Arbeitsaufträge, Wegpunkte und Patrouillen;
  - die Zeit, bis die Masken eines Spielers fertig sind (Kartenstart, Laden, Bündniswechsel);
  - Abfragen, die der Kern nicht beantworten kann.
- **Kern** (`native/MoatMoveAnchor.cpp`, ABI-Vertrag `0x0400000F`):
  - Threads: physische Kerne − 2, mindestens 1, höchstens 8; die Ergebnisse hängen nicht von der Threadzahl ab.
  - Speichergrenze 768 MiB, Wege höchstens 2.000 Schritte, Kantenkosten höchstens 255.

### Einzelbefehle (`AnchorSingleMoves`)

- Der Kern rechnet ein exaktes A* statt Vanillas Suche.
- Ein deterministisches Budget von 500.000 Knoten je Tick begrenzt den schlechtesten Fall.
- Über dem Budget behält ein Bodenbefehl Vanillas Weg. Assassinen werden angerechnet, aber nicht an Vanilla übergeben, weil deren Vanilla-Kette teurer ist.

### Wiederverwendung fehlgeschlagener Suchen

Beide Arten laufen nur mit geladenem Kern:
- **Bodensuchen** (`GroundFailureProofs`): Innerhalb einer Gruppenbewegung werden Starts aus einer bewiesen geschlossenen Fehlschlagmenge ohne `DAAC0`/`DA590` beantwortet.
- **Grabenmodus-Suchen** (`MoatFailureProofs`): Starts in der Komponente eines gescheiterten `DAFD0` werden ohne erneutes `DAFD0` beantwortet, solange sich die Randkacheln nicht geändert haben. Das wird bei jeder Nutzung über einen Fingerabdruck geprüft.

Beide sind an die geprüften Bytes gebunden und schalten sich bei Abweichungen ab.

### Fast (Rückfallebene)

- Gerichtete, ungewichtete Rückwärtsfelder (`FastRouteField`) in einem gemeinsamen Pool von 64 MiB. Ein Graben wird nur genommen, wenn kein Bodenweg existiert.
- Fast dient für Grabenarbeit (Ausheben, Auffüllen) und für die Cursor-Antwort beim Überfahren.
- Fehlt der Kern oder scheitert sein Start, übernimmt Fast auch die Gruppenbefehle. Dann werden Spielerbewegungen vor `0x196100` zurückgestellt und mit 8.192 Expansionen je Tick berechnet.

### Feuer (`AvoidFire`)

- `FireRouteRules` liest alle 4 Ticks Vanillas Effektmanager (Typ 9: brennendes Pech, Feuerpfeile und -töpfe, brennende Gebäude) und trägt diese Kacheln für alle Spieler als Regelkacheln mit der größten Strafe ein.
- `RouteReplanWatch` (nur mit `AnchorSingleMoves`):
  - Laufende Einheiten planen 3 Schritte vor dem Feuer um; erlaubt sind 50 % bzw. mindestens 10 Schritte Umweg.
  - Ist kein Umweg möglich, warten sie bis zu 400 Ticks (etwa 10 s).
  - Die Prüfungen haben ein Budget je Tick.

### Regeln anderer Mods

- `RouteTileRules` (Sperre oder Strafe je Kachel und Spieler) und `RouteSearchEvents` (Kantenkosten einer Suche) wirken im Kern und in Fasts Kanten.
- Vanillas eigene Suchen kennen die Regeln nicht.

### Cursor und Grabenplatzierung

- **Cursor-Antwort beim Überfahren:** Liegen Start und Ziel in derselben positiven Bodenregion, ist die Antwort sofort positiv; sonst rechnen Fasts Cursor-Felder (eigener Pool, getrennt von der Simulation).
- **Grabenplatzierung:** nutzt die Cursor-Topologie (Regionsgraph über Graben- und Portalkanten). Sie wird tabellenbasiert gebaut und beim Kartenstart vorgebaut.

### Speichern und Laden

- Zurückgestellte Fast-Befehle werden im Spielstand gespeichert (`ModSaveDataAPI`, Schlüssel `MoatMove_Serp`).
- Masken und Felder entstehen nach dem Laden neu.
- Vor Feuer wartende Einheiten werden nicht gespeichert.

## Mehrspieler

- Alle Entscheidungen hängen nur vom Simulationszustand, den Ticks und den Einheiten-IDs ab. Budgets zählen Knoten, nie Zeit.
- Die Threadzahl ändert keine Ergebnisse.
- Die Einstellungen werden nicht synchronisiert; alle Spieler brauchen dieselbe Konfiguration und denselben Build.

## Build, Tests und Messung

- **`build.bat`:**
  - Preflight: `Test-SharedBoundaries.ps1`, `Test-UnitCommandSplit.ps1 -ModDirectory Testmods/MoatMove`, `tests/Test-Preflight.ps1`;
  - Tests: `tests/MoatMove.Tests`;
  - MSBuild und der native Kern: `native/Build-MoatMoveAnchor.ps1` baut nach `.local/anchor/` und kopiert hashgeprüft ins Paket;
  - Installation: `tests/Install-Package.ps1`.
- **`tests/MoatMove.Tests`:** kompiliert die echten Quellen von MoatMove und der BugfixesAndQoL-Befehlslogik gegen Fixtures. Enthalten sind auch der Integrationstest mit der echten Kern-DLL (`AnchorIntegrationTests.cs`) und die Cursor-, Platzierungs- und Feld-Tests.
- **Kernverträge:** `Testmods/GroupPathProbe` mit `--anchor-core` prüft unabhängige Vergleiche, Thread-Unabhängigkeit und Batch-Gleichheit.
- **Messläufe im Spiel:** `tools/Run-PathfindingIteration.ps1` und `tools/Analyze-PathfindingRun.ps1` (mit PerfProbe); Ergebnisse unter `.local/runs/`. Übersicht über alle Läufe: `PathfindingRoadmap.md` und `PathfindingFinalReport.md` im Workspace.
- **Leistungszeilen im Log:** `stage=anchor-performance`, `stage=anchor-breakdown`, `stage=fast-performance`.

## Offen

- Spieltest mit Augen (Gruppenwege, Feuer, Assassinen) und ein echter Mehrspielertest.
- Ingame-Modsettings mit Host-Synchronisierung statt der Konfigurationsdatei.
- Die Mindestversionen in `info.json` und in den `BepInDependency`-Attributen (APIShared 0.4.10, BugfixesAndQoL 1.0.181) entsprechen nicht dem benötigten Stand: `RouteTileRules` kam erst mit APIShared 0.19.0, und die interne Provider-Schnittstelle stammt aus dem aktuellen BugfixesAndQoL.
- Ziel auf einer gesperrten Kachel gilt als unerreichbar (Vanilla übernimmt); vor Feuer wartende Einheiten sind nicht im Spielstand; extreme Regeln (kartenweite Sperre) erzeugen noch einzelne Lücken von 120–130 ms.
