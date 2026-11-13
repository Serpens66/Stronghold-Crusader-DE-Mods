# MoatMove: übernommenes Entwicklungswissen

Stilllegung vom 12.11.2026. Geprüfter Ausgangsstand:
`340846a58c6e1a91e4397915e9f52e94596b5e63`, damals sauberer Ordner
`Testmods/MoatMove`. Die folgenden Verweise ersetzen dessen veraltete Build- und
Installationsanweisungen; die alten Dateien bleiben über diesen Git-Stand lesbar.

## Heutige Quellen und Prüfungen

- Der frühere Provider samt Anker-/Fast-Kern, Feuer-/Pestregeln, Flucht und
  Bewegungsfällen liegt unter `Pathfinding/src/Engine`; die Befehlsintegration
  unter `Pathfinding/src/UnitCommands`. Es wird kein BFQoL-interner Provider mehr
  benötigt. Die ursprünglichen Quellblobs und Zielpfade stehen in `source-map.json`.
- Der native Kern liegt in `Pathfinding/native/PathfindingAnchor.cpp`.
  Native Verträge, historische Audits und Aktualisierungswege stehen bereits in
  `Pathfinding/UpdateToNewDLL.md`; die alte MoatMove-Datei ist dort zusammengeführt.
- Die frühere C#-Testsuite ist unter `Pathfinding/tests/Engine` weitergeführt.
  Die vorhandene Binärfixture `latest-recovery-stub.bin` stimmt bytegleich mit der
  ehemaligen MoatMove-Fixture überein. Cursor-/Platzierungs-, Fast-, Kernel-,
  Regelereignis- und Ankerintegrationstests werden vom dortigen Testprogramm geladen.
- Unabhängige Kernvergleiche, Thread-Unabhängigkeit und Batch-Gleichheit prüfen
  `Testmods/GroupPathProbe` mit `--anchor-core`. Die heutigen Lauf- und
  Analysewerkzeuge heißen `Pathfinding/tools/Run-PathfindingIteration.ps1` und
  `Analyze-PathfindingRun.ps1`.

## Beim Weiterentwickeln beachten

- Gruppenrouten und Einzelrouten, Live-Kantenprüfung und Veröffentlichung bilden
  einen gemeinsamen Vertrag. Cachetreffer benötigen gültige Karten-, Regel-,
  Spieler- und Befehlsidentitäten; Cursor- und Simulationsfelder sind getrennt.
- Deterministische Budgets zählen Arbeit, keine Laufzeit. Threadzahl und
  Batchverarbeitung dürfen Routenantworten nicht verändern.
- Feuer-/Pestflucht und wartende Bewegungen besitzen verschiedene Rückkehr- und
  Abbruchregeln. Fortsetzungen müssen neuere Befehle, Formation, Warteschlange,
  Tribe-/Einheitenidentitäten und erfolgreiche Vanilla-Bewegungsaufnahme beachten.
- Save-Datensätze und ihre historischen IDs werden nicht mit den Quellordnern
  umbenannt. Nach dem Laden brauchen gespeicherte Bewegungsfälle eine erneute
  Identitätsprüfung; Suchfelder und Masken werden neu aufgebaut.
- Die ursprüngliche Beschreibung von 03.11.2026 (`Testmods/MoatMove/MOATMOVE.md`
  im genannten Git-Stand) erklärt die damaligen Entscheidungen ausführlich.
  Ihre Konfigurationsdefaults, Abhängigkeiten und Performancezahlen sind historische
  Beobachtungen, keine heutigen Vorgaben. Aktuelle Verträge am Code und an der
  hashgebundenen Native-Baseline prüfen.

## Erhaltene historische Nachweise

- Das frühere `evidence/Test-lag-20261022-1823.log` liegt byteunverändert unter
  `_inspect/MoatMove/Test-lag-20261022-1823.log` im Workspace.
- Die 175 Dateien aus dem ignorierten `.local`-Ordner wurden vor der Löschung nach
  `.work/a/moat-retired/001/local` verschoben und einzeln per SHA-256 verifiziert.
  Der benachbarte `retention.json` nennt Ursprung, Git-Stand und Dateihashes.
- Weitere alte Provenienzdateien, Quellstände und Buildanweisungen sind im Git-Stand
  oben erhalten. Bestehende `_inspect`-Audits und geschützte Arbeitskopien wurden
  nicht entfernt. Echter Mehrspielertest und manueller Augentest bleiben offen.

## Bewahrte Optimierungslehren

Am 13.11.2026 aus `PathfindingRoadmap.md` vor deren Bereinigung übernommen
(Root-Git-Stand `e95da5315e2f7567d7ae8c8f6d24f75f3e7804b0`). Dies sind
historische Modversuche, keine Vanilla-Verträge und keine aktuellen Benchmarks.
Sie verhindern, dass bereits verworfene Ansätze ohne neue Begründung wiederholt
werden. Verbleibende Aufgaben stehen in
[Pathfinding/src/TODO.md](../src/TODO.md#allgemeine-verbesserungen-und-untersuchungen).

- **Vollständige Zielfelder für unabhängige Einzelbefehle lohnten sich nicht.**
  `pf-iter-27`: fortsetzbare Felder je Ziel kosteten 17,8 statt 7,2 s mit Vanilla.
  `pf-rule-2`: ein gemeinsames Feld ab der dritten Anfrage kostete 17,2 statt
  5,2 s; 1.859 Vollfelder für 4.586 Treffer, nur etwa 2,5 Wiederholungen je
  Ziel/Maskenstand. Diese Varianten wurden zurückgenommen. Eine neue Variante
  braucht nachgewiesene Wiederverwendung und muss Aufbau, Invalidierung und
  ungenutzte Arbeit mitrechnen; dies widerlegt nicht sämtliche Feld-Caches.
- **Rückwärtsbeweise je Ziel waren in der damaligen Fassung zu teuer.**
  Der Versuch ab `pf-iter-19` führte in `pf-iter-20/21` zu Spitzen von 307/559 ms
  und wurde zurückgenommen. Die später eingeführten, qualifizierten
  `GroundFailureProofs`/`MoatFailureProofs` sind davon zu unterscheiden. Nur
  erschöpfte Suchen mit weiterhin gültigen Eingaben beweisen Unerreichbarkeit;
  ein Budgetabbruch ist kein solcher Beweis.
- **Spekulative KI-Gruppenbatches berechneten überwiegend ungenutzte Wege.**
  `pf-iter-36`: 185.208 Vorabwege, nur 15.299 genutzt (8 %), Abfragezeit 8,8
  statt 2,0 s. Ein zusätzlicher Zustands-/Zielvergleich in `pf-iter-37` ließ
  noch 157.599 Vorabwege für 15.457 Treffer übrig; beide Varianten verworfen.
  Die tatsächlichen Neuplanentscheidungen liegen tiefer in Vanillas
  Mitgliederschleife. Das ist kein Gegenargument gegen das heute vorhandene
  Batchen bekannter Formationsziele; neue KI-Batches benötigen einen Beweis,
  welche Anfragen tatsächlich gebraucht werden.
- **Mehr vorgeschaltete A*-Proben waren kein automatischer Gewinn.**
  `pf-iter-41`: 32 statt 8 Proben je Gruppenbefehl senkten die Felder nur von
  1.488 auf 1.434; zusätzliche 115 ms Einzelwege gegen 144 ms Feldersparnis
  wurden als neutral bewertet und auf 8 zurückgestellt. Auch die tiefere
  Gleichstandsbevorzugung der damaligen Assassinen-A*-Suche (`pf-iter-30`)
  brachte keinen Gewinn und wurde zurückgenommen. Diese Werte sind keine
  allgemeingültigen Optima für einen anderen Kern oder eine andere Last.
- **Gruppenbefehle über Ticks zurückzuhalten ist keine exakte Abkürzung.**
  `0x11B520` schreibt vor den Mitgliederwegen bereits Tribe-Ziel, Modus und
  Platzdaten. Den gesamten Befehl zu verschieben lässt nachfolgende KI mit
  altem Zustand rechnen; nur Mitgliederwege zu verschieben erfordert einen
  neuen Teilzustandsvertrag. Die damalige Verzögerungsvariante wurde deshalb
  nicht umgesetzt. Native Aufnahme-/Schreibverträge stehen in der
  [Baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MANUAL_GROUP_ADMISSION.md).

Die damaligen Iterationen änderten auch das KI-Verhalten und damit die spätere
Last; rohe FPS-/Tickzahlen verschiedener Iterationen sind nicht durchgehend
direkt vergleichbar. DEVDIAG, Release, Ladephase und künstlicher Massen-Spawn
müssen getrennt bleiben. Die vollständige frühere Tabelle ist über den oben
genannten Git-Stand lesbar; vorhandene Rohprotokolle und Retentionsnachweise
bleiben erhalten. Sie sind keine neuen Abnahmen des heutigen Codes.
