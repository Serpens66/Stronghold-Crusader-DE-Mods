# Übergabe: Chat 10 – Sofortspawn-Zwischenzustände

## Auftrag und Stopplinie

Chat 10 bleibt geöffnet. Chat 11 darf noch nicht beginnen. Die alte
144-Fall-Baseline ist gelöscht, weil ihre Daten weder Kartenstart-IDs noch den
Sofortspawn-Wert zuverlässig erfassten. Sie darf nicht wiederhergestellt oder
als Regression verwendet werden.

Die neue No-PreBuild-Baseline ist vollständig exakt. Offen sind ausschließlich
18 Fälle aus einer expliziten Sitzung mit `advopt_pre_build=1`.

## Zuerst lesen

1. `MapParser/AIV_PLACEMENT_ROADMAP.md`, Abschnitt Chat 10;
2. dieses Dokument vollständig;
3. `MapParser/Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md`;
4. `MapParser/Docs/AIV_PREBUILD_AND_OVERLAP_ORDER.md`;
5. `AIVPlacement/AIVPlacement.Core/AivPreplacementMapState.cs`;
6. `AIVPlacement/AIVPlacement.OracleComparison/Program.cs`;
7. `ActiveAIVDetector/src/AivPlacementOracle.cs`.

## Gesicherter Stand

- ActiveAIVDetector 0.9.1 ist gebaut und installiert.
- `advopt_pre_build` wird vor `StartSkirmishGame` erfasst und pro Map-Load-
  Sitzung in jede Oracle-Zeile übernommen.
- Das Live-Grid verwendet seit 0.9.1 den vom Validator beobachteten
  Placement-State-Zeiger.
- Der neue No-PreBuild-Corpus umfasst 24 Fälle: 24 exakt, 0 Mismatches,
  0 `NotEvaluable`, 0 Fehler.
- Der neue Paarkorpus umfasst 48 Fälle. Modus `0`: 24/24 exakt. Modus `1`:
  6/24 exakt, 18 Mismatches. Es gibt 0 Fehler.
- `AIVPlacement/build.bat` meldet 29/29 erfolgreiche Tests.

Die gültigen Corpora liegen unter:

- `AIVPlacement/OracleCorpus/Captured-2026-08-03-SessionAware/v-thasos.json`;
- `AIVPlacement/OracleCorpus/Captured-2026-08-03-SessionAware-Paired/v-thasos.json`.

## Ursache der früheren No-PreBuild-Mismatches

Vanilla entfernt den serialisierten Startkomplex eines akzeptierten
KI-Spielers und baut ihn passend zur ausgewählten AIV-Rotation neu auf. Dazu
gehören nicht nur Gebäudezellen, sondern auch eindeutig angrenzende Wall-Flags.
Ein abgelehnter Start bleibt unverändert. Die Offline-Rekonstruktion bildet
dies nun ab; dadurch sind alle 24 No-PreBuild-Fälle exakt.

Verbindliche Rotationsregel: AIV und realer Startkomplex werden immer gemeinsam
gedreht. Keep, 5×5-Vorratslager und weitere gekoppelte Startgebäude verwenden
dieselbe ausgewählte Orientierung; eine unabhängige AIV-Rotation ist kein
gültiges Modell des nativen Kartenstarts.

Die Transformationsbasis relativ zum serialisierten Keep-Anker lautet:

| Rotation | Zielrelativkoordinate aus `(x,y)` |
| ---: | --- |
| 0 | `(x + 1, y + 1)` |
| 90 | `(y + 1, 12 - x)` |
| 180 | `(12 - x, 12 - y)` |
| 270 | `(12 - y, x + 1)` |

## Vorbereiteter nächster Lauf

Der erste Wildcard-Lauf vom 2026-08-03 um 16:46 Uhr ist als Evidenz gesichert,
aber nicht die benötigte Sechserfolge: Version 0.9.1 zählte sechs
Kandidatenversuche und erfasste dadurch vier Rotationen von Spieler 2 sowie
zwei von Spieler 3. Außerdem schrieb sie nur für den ersten Capture ein
vollständiges Live-Building-Grid. Der Log selbst ist gültig und reproduziert
den 24-Fall-Sofortspawn-Stand mit 6 exakten Fällen und 18 Mismatches. Die beiden
Spieler-3-Traces belegen jeweils 39 bereits vor dem gemeinsamen Validator
blockierte Zellen, reichen aber nicht für alle Spielerübergänge.

ActiveAIVDetector 0.9.2 begrenzt einen negativen Spielerfilter deshalb auf
höchstens einen Capture pro Spieler und schreibt für jeden Capture ein eigenes
Live-Building-Grid.

Die Trace-Vorlage und die installierte Konfiguration müssen auf folgendem
Profil stehen:

    Enabled = true
    PlayerId = -1
    CandidateId = 0
    Orientation = -1
    KeepX = -1
    KeepY = -1
    MaximumCaptureCount = 6

Der Benutzer startet genau einmal `v_Thasos.map` mit demselben
`testlord_serpcastle1` in allen sechs KI-Slots und aktiviert Sofortspawn. Die
zufällige Keep-Zuteilung ist dabei unerheblich; die Session bindet jeden
Spieler an seine tatsächliche Keep-Koordinate.

Der Lauf soll sechs Per-Cell-Traces und sechs vollständige Live-Building-Grids
erzeugen: jeweils den ersten Kandidatenversuch der Spieler 2 bis 7. Kein
weiterer Editorstart und kein zweiter No-PreBuild-Lauf sind nötig.

## Auswertung nach dem Lauf

1. `LogOutput.log`, alle sechs Trace-Dateien und alle sechs Grids in einen neuen
   Evidenzordner kopieren und mit SHA-256 binden.
2. Prüfen, dass der Log ActiveAIVDetector 0.9.2 und
   `advopt_pre_build=1` nennt.
3. Für jeden Dump prüfen, dass der Placement-State-Zeiger aus Validatorcalls
   stammt und keine Pointer-Warnung vorliegt.
4. Den Log mit dem aktuellen `import-log` neu importieren; keine Sollwerte aus
   dem älteren Paarkorpus manuell übernehmen.
5. Die realen Zustandsdifferenzen zwischen den sechs Grids bestimmen. Nur
   tatsächlich hinzugekommene Gebäude- und Tile-Zustände dürfen in die
   PreBuild-Fortschreibung eingehen.
6. Zuerst einen betroffenen Folgespieler, danach alle 24 Sofortspawn-Fälle und
   zuletzt beide 24-Fall-Sitzungen vergleichen.
7. Jede Korrektur erhält einen kurzen Warum-Kommentar und einen synthetischen
   Test. Danach `AIVPlacement/build.bat` ausführen.

## Evidenz- und Änderungsregeln

- Map-, AIV- und native Sollwerte niemals an das Offline-Ergebnis angleichen.
- Fälle ohne nichtleere `SessionId` oder eindeutiges `PreBuildSetting` nicht in
  einen kanonischen Corpus aufnehmen.
- Ein ungültiges Live-Grid nicht durch Plausibilität retten.
- `BuildingId` nur für wirklich vorhandene Map-/Laufzeitgebäude verwenden;
  projizierte PreBuild-Herkunft bleibt getrennt.
- Proprietäre Map- oder AIV-Dateien nicht in das Repository kopieren.
- Geänderte Textdateien vor Builds auf CRLF prüfen.
- Chat 11 erst nach 0 ungeklärten Mismatches und 0 Fehlern beginnen.

## Kopierbarer Startprompt

> Setze Chat 10 aus `MapParser/AIV_PLACEMENT_ROADMAP.md` fort und lies zuerst
> `MapParser/Docs/CHAT10_ORACLE_MISMATCH_HANDOFF.md` vollständig. Werte den neu
> erfassten Thasos-Sofortspawn-Lauf mit sechs Zwischenzuständen aus. Verwende
> ausschließlich Fälle mit expliziter `SessionId` und `PreBuildSetting=1` und
> beginne Chat 11 nicht vor 0 ungeklärten Mismatches und 0 Fehlern.
