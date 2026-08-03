# Übergabe: Chat 10 – Sofortspawn-Zwischenzustände

## Auftrag und Stopplinie

Chat 10 bleibt geöffnet. Chat 11 darf noch nicht beginnen. Die alte
144-Fall-Baseline ist gelöscht, weil ihre Daten weder Kartenstart-IDs noch den
Sofortspawn-Wert zuverlässig erfassten. Sie darf nicht wiederhergestellt oder
als Regression verwendet werden.

Die neue No-PreBuild-Baseline ist vollständig exakt. Der Sofortspawn-Lauf ist
ausgewertet und die auffälligen nativen Ausführungszweige sind statisch
rekonstruiert. Offen ist ein schmaler Laufzeittrace der einzelnen
`ExecuteBuildStep`-Frames, nicht mehr ein ungeklärter Oracle-Mismatch.

## Zuerst lesen

1. `MapParser/AIV_PLACEMENT_ROADMAP.md`, Abschnitt Chat 10;
2. dieses Dokument vollständig;
3. `MapParser/Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md`;
4. `MapParser/Docs/AIV_PREBUILD_AND_OVERLAP_ORDER.md`;
5. `AIVPlacement/AIVPlacement.Core/AivPreplacementMapState.cs`;
6. `AIVPlacement/AIVPlacement.OracleComparison/Program.cs`;
7. `ActiveAIVDetector/src/AivPlacementOracle.cs`.

## Gesicherter Stand

- ActiveAIVDetector 0.9.2 ist gebaut und installiert.
- `advopt_pre_build` wird vor `StartSkirmishGame` erfasst und pro Map-Load-
  Sitzung in jede Oracle-Zeile übernommen.
- Das Live-Grid verwendet seit 0.9.1 den vom Validator beobachteten
  Placement-State-Zeiger.
- Der neue No-PreBuild-Corpus umfasst 24 Fälle: 24 exakt, 0 Mismatches,
  0 `NotEvaluable`, 0 Fehler.
- Der neue Paarkorpus umfasst 48 Fälle. Modus `0`: 24/24 exakt. Modus `1`:
  4/24 exakt, 20 technisch begründete `NotEvaluable`. Es gibt 0 Mismatches
  und 0 Fehler.
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

## Ausgewerteter Sofortspawn-Lauf

ActiveAIVDetector 0.9.2 begrenzt einen negativen Spielerfilter auf höchstens
einen Capture pro Spieler und schreibt für jeden Capture ein eigenes
Live-Building-Grid. Der erfolgreiche Lauf verwendete folgendes Profil:

    Enabled = true
    PlayerId = -1
    CandidateId = 0
    Orientation = -1
    KeepX = -1
    KeepY = -1
    MaximumCaptureCount = 6

Der erfolgreiche Lauf liegt unter
`.native-analysis/chat10-next/thasos-prebuild-20260803-165341/`; der Log-Hash
lautet
`D10604EC3BE35D39EF2CC72CEA9BD1F6F68F3EF8B56205DBE55EED585F23357F`.
Er enthält sechs Per-Cell-Traces und sechs vollständige Live-Building-Grids,
jeweils für den ersten Kandidatenversuch der Spieler 2 bis 7. Der Log bestätigt
ActiveAIVDetector 0.9.2 und `advopt_pre_build=1`; es gibt keine Pointer-Warnung.

## Ergebnis und nächste Analyse

1. Spieler 2 erzeugte 757 AIV-Gebäudezellen. Gegen die frühere Projektion waren
   707 identisch, 50 nur projiziert und 50 nur live vorhanden.
2. Die nur projizierten Zellen gehörten zu den früher angenommenen Footprints
   von Mapper 52 (Stockpile) und Mapper 105 (Drawbridge). Die nur live
   vorhandenen Zellen gehörten zu Mapper 89 (Tunnelers Guild einschließlich
   5×5-Hof), obwohl dessen Fit-Prüfung blockiert war.
3. Beim ersten Spieler-3-Versuch erklärte reale Building-Belegung alle zwölf
   `nativeOnly`-Zellen. Die Projektion aus `Placeable`-Kernfootprints ist damit
   widerlegt und vollständig entfernt.
4. Ohne beobachteten Live-Zustand sind alle Spieler nach einem ausgeführten
   früheren Prebuild `NotEvaluable`. Der Paarkorpus hat dadurch 28 exakte
   Fälle, 20 `NotEvaluable`, 0 Mismatches und 0 Fehler.
5. `ExecuteBuildStep` ab RVA `0x509F0` ist gezielt untersucht:
   `freeOrForced=true` überspringt den Ressourcen-/Verfügbarkeitsaufruf RVA
   `0xCB630`, nicht aber alle übrigen Zweige. RVA `0x5C000` validiert den
   aktuellen sequenziellen Live-Zustand mit echter Spieler-ID und `mode=1`;
   sein Rückgabewert wird ignoriert, sodass ein blockierter Footprint den
   Konstruktor nicht verhindert.
6. Tunnelers Guild (Konstruktor RVA `0x76670`) erzeugt Hauptgebäude plus
   separaten 5×5-Hof und erklärt exakt die zwei IDs/50 Zellen. Drawbridge
   benötigt über RVA `0x793E0` ein passendes lebendes Tor samt Orientierung.
   Goods Yard (RVA `0x760F0`) erzeugt vier Gebäudedatensätze plus neun
   Verbindungstiles; das konkrete Fehlen am früher projizierten Footprint ist
   daher keine allgemeine Tile-only-Regel.
7. Als Nächstes einen schmalen Laufzeittrace um `ExecuteBuildStep` ergänzen,
   der pro Frame Mapper, Rückgabewert und Building-Grid-Differenz protokolliert.
   Damit ist insbesondere zu entscheiden, ob der konkrete Mapper-52-Schritt
   abbrach oder mit nativer Koordinaten-/Mehrkomponentenlogik an anderer Stelle
   wirkte.

Die zugehörigen Read-only-Audits liegen in:

- `.native-analysis/chat10-execute-build-step-branches.stdout.log`;
- `.native-analysis/chat10-prebuild-constructors.stdout.log`;
- `.native-analysis/chat10-prebuild-common-checks.stdout.log`.

## Evidenz- und Änderungsregeln

- Map-, AIV- und native Sollwerte niemals an das Offline-Ergebnis angleichen.
- Fälle ohne nichtleere `SessionId` oder eindeutiges `PreBuildSetting` nicht in
  einen kanonischen Corpus aufnehmen.
- Ein ungültiges Live-Grid nicht durch Plausibilität retten.
- `BuildingId` nur für wirklich vorhandene Map-/Laufzeitgebäude verwenden;
  aus einem Plan keine projizierte PreBuild-Belegung erzeugen.
- Proprietäre Map- oder AIV-Dateien nicht in das Repository kopieren.
- Geänderte Textdateien vor Builds auf CRLF prüfen.
- Chat 11 erst nach 0 ungeklärten Mismatches und 0 Fehlern beginnen.

## Kopierbarer Startprompt

> Setze Chat 10 aus `MapParser/AIV_PLACEMENT_ROADMAP.md` fort und lies zuerst
> `MapParser/Docs/CHAT10_ORACLE_MISMATCH_HANDOFF.md` vollständig. Werte den neu
> ausgewerteten Thasos-Sofortspawn-Lauf mit sechs Zwischenzuständen als
> Evidenz. Verwende die dokumentierte native Analyse der auffälligen
> `ExecuteBuildStep`-Zweige und ergänze als Nächstes einen Laufzeittrace pro
> ausgeführtem Frame für Mapper, Rückgabewert und Building-Grid-Differenz.
> Verwende bis zur vollständigen sequenziellen Rekonstruktion für abhängige
> Spieler `NotEvaluable` und beginne Chat 11 noch nicht.
