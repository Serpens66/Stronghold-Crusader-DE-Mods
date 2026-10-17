# DependencyBootstrapProbe — Nachweise und aufgeräumte Testdaten

Stand 17.10.2026. Die Lifecycle-Probe für SE 2.14.2 wurde gebaut und installiert.
Ihr Spielnachweis steht aus. Aktueller Status: [lifecycle-runtime/STATUS.md](lifecycle-runtime/STATUS.md).

## Direkt behalten

- `Correction-accepted-20261016-2154`: akzeptierter Korrektur-Spielnachweis und
  Binärdateien; weiterhin Eingabe der aktiven Pakettests und des Übernahmetreibers.
- `Shadow-accepted-20261016-1946`: akzeptierter Schattenlauf und frühe Quellhashes,
  die der Übernahmetreiber weiterhin prüft.
- Frühere Observe-/Active-Nachweise: hilfreiche Historie des behobenen Startfehlers.
- `lifecycle-runtime`: aktueller Quellstand, Build-/Prüflogs, Identitäten, Audit und
  Sicherung der vorher installierten Probe einschließlich Schaltern.
- In `package-lifecycle` und `lifecycle-implementation`: relevante Ergebnisdateien,
  Provenienz, Statusberichte und Logs an ihren bisherigen Pfaden.

## Zusammengefasste Fixture-Daten

Die mehrfachen DLL-/Paketkopien sind keine zusätzlichen Builds oder benötigten
Installationen. Jeder unterschiedliche Dateiinhalt ist jetzt genau einmal in
einem komprimierten Archiv gespeichert:

| Testbereich | Frühere Dateien | Frühere Nutzdaten | Unterschiedliche Inhalte | ZIP |
|---|---:|---:|---:|---:|
| Altes Paketmodell | 195 | 61,09 MiB | 13 | 818,67 KiB |
| Unabhängige Implementierung | 584 | 8,94 MiB | 82 | 173,48 KiB |

Archive und lesbare Dateizuordnung: [compacted](compacted).
Zusammen 779 Fixture-Dateien mit rund 70 MiB wurden in rund 0,97 MiB ZIP-Daten
gebündelt, zuzüglich kleiner Indizes und direkt lesbarer Berichte.

Alle gespeicherten Inhalte wurden vor dem Entfernen der losen Dateien erneut
per SHA-256 geprüft. Neun Junction-/Linkeinträge wurden nur als Metadaten
aufgezeichnet und nicht verfolgt; entfernt wurden ausschließlich die Verweise.
Akzeptierte Spielarchive, Rückwegsicherung und installierte Dateien waren keine
Bereinigungsziele. Es ging kein unterschiedlicher regulärer Dateiinhalt verloren.

## Archiv lesen

Jedes ZIP enthält `manifest.json`: ursprünglicher relativer Pfad, SHA-256,
Dateilänge sowie gegebenenfalls Linkmetadaten. Der zugehörige Inhalt liegt unter
`objects/<SHA256>`. Die CSV-Indizes daneben dienen der schnellen Suche; die
`*-summary.json` dokumentieren auch die SHA-256 des gesamten Archivs.

Nicht einfach das ZIP ins Spiel entpacken. Es enthält synthetisch veränderte und
absichtlich fehlerhafte Testpakete. Bei Bedarf einzelne Objekte anhand des
Manifests ausschließlich in einem neuen Workspace-Fixture rekonstruieren.
Aktive Tests erzeugen ihre Fixtures selbst; sie benötigen diese alten Kopien nicht.
Eine automatische Bereinigung zukünftiger Läufe wurde nicht eingerichtet.
