# Übergabe: Chat 10 – Sofortspawn-Zwischenzustände

## Auftrag und Stopplinie

Chat 10 ist für die erste Ausbaustufe mit deaktiviertem Sofortspawn
abgeschlossen; Chat 11 ist der nächste Schritt. Die alte 144-Fall-Baseline ist
gelöscht, weil ihre Daten weder Kartenstart-IDs noch den Sofortspawn-Wert
zuverlässig erfassten. Sie darf nicht wiederhergestellt oder als Regression
verwendet werden.

Die neue No-PreBuild-Baseline ist vollständig exakt. Der Sofortspawn-Lauf ist
ausgewertet, die auffälligen nativen Ausführungszweige sind statisch
rekonstruiert und der schmale Laufzeittrace der einzelnen
`ExecuteBuildStep`-Frames ist inzwischen vollständig abgenommen. Es besteht
kein ungeklärter Oracle-Mismatch. Die sequenzielle Sofortspawn-Erweiterung ist
bewusst auf Chats 14 bis 16 verschoben, nachdem Chats 11 bis 13 zuerst das
No-PreBuild-System einschließlich Lobby fertigstellen.

## Zuerst lesen

1. `MapParser/AIV_PLACEMENT_ROADMAP.md`, Abschnitt Chat 10;
2. dieses Dokument vollständig;
3. `MapParser/Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md`;
4. `MapParser/Docs/AIV_PREBUILD_AND_OVERLAP_ORDER.md`;
5. `AIVPlacement/AIVPlacement.Core/AivPreplacementMapState.cs`;
6. `AIVPlacement/AIVPlacement.OracleComparison/Program.cs`;
7. `ActiveAIVDetector/src/AivPlacementOracle.cs`.

## Gesicherter Stand

- ActiveAIVDetector 0.9.3 ist gebaut und installiert. Der diagnostische
  `ExecuteBuildStep`-Hook ist standardmäßig deaktiviert und wurde nur über das
  explizite `/trace`-Profil für den abgeschlossenen Lauf aktiviert.
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

## Ergebnisse der statischen und dynamischen Analyse

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
7. Als nächster Schritt war ein schmaler Laufzeittrace um `ExecuteBuildStep`
   vorgesehen, der pro Frame Mapper, Rückgabewert und Building-Grid-Differenz
   protokolliert und insbesondere den konkreten Mapper-52-Schritt entscheidet.
8. Dieser Laufzeittrace ist mit ActiveAIVDetector 0.9.3 abgeschlossen. Er
   enthält für Spieler 2 lückenlos 77 Frames `0..76`, genau einen konsistenten
   Placement-State-Zeiger, 0 Pointerprobleme, 0 Capturefehler und 0
   Hookwarnungen. Mapper 105 gab in Frame 28 `0` zurück und änderte keine
   Building-ID. Mapper 89 gab in Frame 36 `1` zurück und erzeugte zwei IDs mit
   je 25 Zellen. Mapper 52 gab in Frame 41 `0` zurück und änderte das
   Building-Grid an keiner Stelle. Die Evidenz liegt hashgebunden unter
   `.native-analysis/chat10-next/thasos-execute-build-step-20260803-182959/`.

Die zugehörigen Read-only-Audits liegen in:

- `.native-analysis/chat10-execute-build-step-branches.stdout.log`;
- `.native-analysis/chat10-prebuild-constructors.stdout.log`;
- `.native-analysis/chat10-prebuild-common-checks.stdout.log`.

## Arbeitsbaum bei der Übergabe

Der fachliche Code-/Corpusstand ist im lokalen Git-Commit `4fa22c0` (`163`)
gesichert. Der aktuelle Arbeitsbaum enthält zusätzlich die
ActiveAIVDetector-0.9.3-Implementierung samt Buildausgabe, Diagnoseprofil und
aktualisierter Chat-10-Dokumentation. Die hashgebundene Laufzeitevidenz liegt
unter `.native-analysis/chat10-next/thasos-execute-build-step-20260803-182959/`.
Keine Chat-10-Änderung darf durch ältere Dateien ersetzt werden. Insbesondere ist
`AIVPlacement/AIVPlacement.Core/AivProjectedPrebuildMapState.cs` bewusst aus
dem Repository gelöscht; die zugehörigen projizierten Occupancy-/Issue-Typen
und Tests wurden ebenfalls entfernt. `AIVPlacement.OracleComparison`
klassifiziert abhängige PreBuild-Spieler stattdessen als `NotEvaluable`.

Die beiden kanonischen Reports wurden mit diesem Stand neu erzeugt. Der letzte
erhöhte Aufruf von `AIVPlacement/build.bat` war erfolgreich: 0 Warnungen,
0 Fehler und 29/29 Tests. Der erhöhte Aufruf von
`ActiveAIVDetector/build.bat /nopause /trace` war ebenfalls mit 0 Warnungen und
0 Fehlern erfolgreich. Alle geänderten Textdateien wurden anschließend mit
ordinaler Rücklesekontrolle auf CRLF geprüft.

## Abgeschlossener Implementierungsschritt

ActiveAIVDetector 0.9.3 besitzt jetzt die rein diagnostische, standardmäßig
deaktivierte `Oracle prebuild trace`-Option. Vorhandene Lifecycle- und
Zell-Trace-Funktionalität blieb unverändert. Die folgenden Signatur-, Layout-
und Aufnahmevorgaben dokumentieren die implementierte Grundlage.

Die belegte native Signatur von `ExecuteBuildStep` lautet:

    int ExecuteBuildStep(
        ulong aivStateAddress,
        int playerId,
        int frameIndex,
        int restrictedMode,
        byte freeOrForced)

Die Funktion liegt für die gebundene DLL bei RVA `0x509F0`. Ein möglicher
Signaturanfang ist
`40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2`; dessen
Eindeutigkeit wurde gegen genau diese DLL geprüft: ein Treffer bei RVA
`0x509F0`. Der Sofortspawn-Aufrufer übergibt `restrictedMode=0` und
`freeOrForced=1`.

Der vorbereitete Frame-Eintrag lässt sich ohne Heuristik lesen:

1. aktiver Layoutindex als `int32` bei
   `nativeBase + 0x379B05C + playerId * 0x583C`;
2. `entryIndex = activeLayoutIndex * 0x922 + frameIndex`;
3. Eintragsadresse `aivStateAddress + 0x38 + entryIndex * 0x0C`;
4. Statusbyte bei `+0`, Hilfsbyte bei `+1`, Mapper als vorzeichenbehafteter
   `int16` bei `+2`, Positionsanzahl als `int16` bei `+4` und erster
   Positionsindex als `int32` bei `+8`.

Der Hook arbeitet nur bei explizit aktivierter Diagnose und passendem
Spielerfilter. Für jeden passenden Frame werden vor und nach dem Trampoline
mindestens erfasst:

- Zeitstempel mit Millisekunden, Session-/Mapbezug, Spieler-ID und Frameindex;
- Mapper, Status, Positionsanzahl, `restrictedMode`, `freeOrForced` und
  Funktionsrückgabewert;
- alle Änderungen des realen 320800-Zellen-`BuildingId`-Grids als
  `tileId, beforeId, afterId` sowie Summen für hinzugefügt, entfernt und ersetzt;
- ein konsistenter Placement-State-Zeiger. Dafür den bereits durch den nativen
  Validator beobachteten Zeiger wiederverwenden und Pointerabweichungen wie
  beim bestehenden Zelltrace ausdrücklich melden; nicht blind eine neue
  Strukturadresse annehmen.

Die Vorher-/Nachher-Aufnahme liegt synchron um genau den Trampoline-Aufruf.
Reentranz wird abgewiesen. Der Diagnosecode verändert weder Rückgabewert noch
nativen Zustand. Mapper 52, 89 und 105 werden in der Auswertung hervorgehoben;
alle Frames des gefilterten Spielers bleiben erhalten.

Das verwendete Profil der neuen Sektion lautet:

    [Oracle prebuild trace]
    Enabled = true
    PlayerId = 2
    MaximumCaptureCount = 1

Der bisherige Abschnitt `[Oracle cell trace]` behält die Werte des
abgeschlossenen Wildcard-Laufs, ist im installierten Profil aber deaktiviert.
Das Buildskript installiert die Diagnosekonfiguration weiterhin nur mit
`/trace`; die Vorlage
`ActiveAIVDetector/Diagnostics/Chat10-Bow-Ridge-Trace.cfg` enthält beide
Sektionen getrennt.

Die geänderten Textdateien wurden vor dem erhöhten Aufruf von
`ActiveAIVDetector/build.bat /nopause /trace` ordinal auf CRLF geprüft. Der
Build war mit 0 Warnungen und 0 Fehlern erfolgreich und installierte Plugin
sowie explizite Trace-Konfiguration.

## Abgeschlossener Spielstart und Abnahme

Der Benutzer führte nach dem erfolgreichen Build genau den verlangten
Spielstart aus:

- `v_Thasos.map`;
- alle sechs KI-Slots mit `testlord_serpcastle1`;
- Sofortspawn/„Completed enemy castles“ aktiviert.

Spieler 2 wurde automatisch vergeben; kein Editorstart und kein zusätzlicher
No-PreBuild-Lauf waren nötig. Log, installierte Konfiguration, `info.json` und
Trace liegen unter
`.native-analysis/chat10-next/thasos-execute-build-step-20260803-182959/` und
sind in `SHA256SUMS.txt` gebunden. Bestätigt sind ActiveAIVDetector 0.9.3,
`advopt_pre_build=1`, genau ein vollständiger Spieler-2-Prebuild-Trace und
keine Pointer-, Capture- oder Hookwarnung.

Abnahme dieses Schritts:

- Mapper 52 brach mit Rückgabewert `0` ohne Building-Grid-Änderung ab;
- Mapper 89 zeigt mit Rückgabewert `1` die erwarteten zwei IDs und 50 Zellen;
- Mapper 105 brach mit Rückgabewert `0` ohne Building-Grid-Änderung im
  Drawbridge-Vorbedingungspfad ab;
- die Dokumentation und gegebenenfalls das Offline-Modell werden nur aus dieser
  Evidenz angepasst; `NotEvaluable` bleibt bis zu einer tatsächlich exakten
  sequenziellen Rekonstruktion bestehen;
- Chat 11 darf jetzt mit dem ausdrücklich auf `advopt_pre_build=0` begrenzten
  Lobby-Datenfluss beginnen. Die Sofortspawn-Erweiterung beginnt erst in
  Chat 14.

## Evidenz- und Änderungsregeln

- Map-, AIV- und native Sollwerte niemals an das Offline-Ergebnis angleichen.
- Fälle ohne nichtleere `SessionId` oder eindeutiges `PreBuildSetting` nicht in
  einen kanonischen Corpus aufnehmen.
- Ein ungültiges Live-Grid nicht durch Plausibilität retten.
- `BuildingId` nur für wirklich vorhandene Map-/Laufzeitgebäude verwenden;
  aus einem Plan keine projizierte PreBuild-Belegung erzeugen.
- Proprietäre Map- oder AIV-Dateien nicht in das Repository kopieren.
- Geänderte Textdateien vor Builds auf CRLF prüfen.
- Chats 11 bis 13 bleiben produktiv auf `advopt_pre_build=0` begrenzt; Modus
  `1` liefert bis zur späteren Erweiterung ausdrücklich `NotEvaluable`.

## Kopierbarer Startprompt

> Chat 10 ist für deaktivierten Sofortspawn abgeschlossen. Bearbeite als
> Nächstes Chat 11 aus `MapParser/AIV_PLACEMENT_ROADMAP.md` und binde nur den
> No-PreBuild-Lobby-Datenfluss an. Behandle `advopt_pre_build=1` bis zu den
> späteren Sofortspawn-Chats 14 bis 16 ausdrücklich als `NotEvaluable`.
