# DependencyBootstrapProbe — aktueller Stand und Nachweise

Stand 17.10.2026: Lifecycle-Spielstart automatisiert bestanden; neue Probe mit
Runtime-Paarbindung gebaut und installiert. Noch offen: deren eigener Spielstart
mit `Check-Run.ps1 -Mode Active -RequirePair` und manueller Menübestätigung.

## Für die laufende Arbeit benötigt

- [Aktueller Status](lifecycle-runtime/STATUS.md)
- [Buildprotokoll](lifecycle-runtime/pair-build.log)
- [Installierte Identitäten](lifecycle-runtime/pair-installed.json): beide DLLs,
  Paarmanifest geprüft; APIShared und Loader unverändert.
- [Ausgewerteter Spielstart](Lifecycle-accepted-20261017-152951/check-result.log):
  Trace, beide Lifecycle-Berichte, Unity-/BepInEx-Log und Hashliste liegen zusammen.
- Rückfallsicherung: `lifecycle-runtime/backup-f73ee48f0d184dba8fb5c74ed1339bd2`.
- Aktuelle Quellen: `lifecycle-runtime/pair-sources.zip` mit `pair-source-hashes.json`.
- [Paketläufe als Tabelle](lifecycle-runtime/package-runs.csv); aktuelle Tests in
  `lifecycle-runtime/pair-contract-tests.log`, `pair-log-tests.log`, `pair-package-tests.log`.

## Komprimierte Testdaten statt hunderter Kopien

[compacted](compacted) enthält geprüfte Archive mit Inhaltsdeduplizierung.
Identische Dateien werden pro Archiv nur einmal gespeichert. Die CSV-Indizes
ordnen ursprüngliche Pfade den SHA-256-Objekten zu; die Summary-Dateien enthalten
Dateizahl, Größen und Archivhash. Junctions werden nur dokumentiert, nie verfolgt.

| Zusammenfassung | Dateien vor Zusammenfassung | ZIP-Größe |
|---|---:|---:|
| Historisches Paketmodell | 195 | 838.323 Bytes |
| Historische Implementierungstests | 584 | 177.644 Bytes |
| Neue Paketläufe samt älteren Ergebnisdateien | 201 | 104.035 Bytes |
| Alter Quellsnapshot | 40 | 77.878 Bytes |

Die letzten beiden Archive tragen den Zusatz `-pair-stage`. Dort liegen auch die
früher verstreuten Ergebnisdateien; die Tabelle oben verlinkt deren Zusammenfassung.
`lifecycle-implementation` bleibt als Arbeitswurzel zukünftiger Tests bestehen,
enthält aktuell aber keine losen Fixture-DLLs oder hunderte Testunterordner.

Ein Archiv enthält `manifest.json` mit Originalpfad, Hash, Länge und Linkmetadaten;
der Inhalt liegt unter `objects/<SHA256>`. Nur benötigte Dateien in ein neues
Workspace-Verzeichnis rekonstruieren. Archive niemals ins Spiel entpacken: sie
enthalten absichtlich ungültige Testpakete. Aktive Tests erzeugen ihre Eingaben neu.

## Bewusst erhaltene Historie

`Correction-accepted-20261016-2154` wird weiterhin von Pakettests verwendet;
`Shadow-accepted-20261016-1946` liefert die geprüften frühen Quellhashes für den
Übernahmetreiber. Observe-/Active-Spielnachweise und die vorherige Rückfallsicherung
sind klein und helfen bei einer Regression des frühen Startpfads. Sie bleiben erhalten.
Alte Arbeitsnotizen und Auditlogs in `lifecycle-runtime` sind ausdrücklich historische
Nachweise; für den aktuellen Zustand gelten die oben verlinkten Dateien.
## Lokaler Updateversuch A -> B (17.10.2026)

Pair-accepted-20261017-161346 enthält den bestandenen Paarbindungslauf samt hashes.json. local-update enthält Prüf- und Buildlogs sowie installation.json mit A/B-, APIShared-, Updater- und Map-Hashes. A ist installiert, B liegt unter Testmods/DependencyBootstrapProbe/update-trial; realer Austausch noch offen. Am Update-Dialog zuerst Stage prüfen. Keine neuen massenhaften Paketfixtures.
