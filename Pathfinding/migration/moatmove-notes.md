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
