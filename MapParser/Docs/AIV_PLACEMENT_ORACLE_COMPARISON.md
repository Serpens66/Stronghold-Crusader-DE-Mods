# Chat 10: Offline-/Oracle-Vergleich

## Aktueller Abnahmestand

Die frühere 144-Fall-Matrix wurde am 2026-08-03 vollständig entfernt. Ihre
Logs enthielten weder verlässliche Kartenstart-IDs noch einen pro Sitzung
erfassten Wert von `advopt_pre_build`; sie ist deshalb keine Regression mehr.

Kanonisch sind nur neu importierte Fälle mit

- nichtleerer `SessionId`,
- eindeutigem `PreBuildSetting` `0` oder `1`,
- unverändertem Map- und AIV-Hash und
- einem unveränderten nativen Sollwert.

Der aktuelle Stand auf `v_Thasos.map` ist:

| Corpus | Modus | Fälle | Exakt | Mismatch | `NotEvaluable` | Fehler |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Captured-2026-08-03-SessionAware` | `0` | 24 | 24 | 0 | 0 | 0 |
| `Captured-2026-08-03-SessionAware-Paired` | `0` | 24 | 24 | 0 | 0 | 0 |
| `Captured-2026-08-03-SessionAware-Paired` | `1` | 24 | 6 | 18 | 0 | 0 |

Chat 10 bleibt wegen der 18 Sofortspawn-Mismatches geöffnet. Chat 11 darf noch
nicht beginnen.

## Reproduzierbarkeit

- `CrusaderDE.dll`: SHA-256
  `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- bevorzugte Image Base: `0x180000000`
- `v_Thasos.map`: SHA-256
  `84DCF2A480A4334DFC0C4BAE54DA49BACFE1D7B31F1D9AD2E171CF1F3B60275C`
- gültiger ActiveAIVDetector-0.9.1-No-PreBuild-Log: SHA-256
  `E67137A68765E4F1A573108CCCE2FF7E592E67BB8E2281B571648C257E037540`
- gepaarter Log mit explizitem `advopt_pre_build=1` und `0`: SHA-256
  `713F781F4D806CB07C5F4515ABD25903D6D9FB3B57CF743CFC3BD370D4E928DD`

Die Manifeste und Berichte liegen ausschließlich unter:

- `AIVPlacement/OracleCorpus/Captured-2026-08-03-SessionAware/`;
- `AIVPlacement/OracleCorpus/Captured-2026-08-03-SessionAware-Paired/`;
- den gleichnamigen Unterordnern von
  `AIVPlacement/OracleCorpus/Results/`.

Die 0.9.1-Laufzeitevidenz liegt unter
`.native-analysis/chat10-next/thasos-player7-no-prebuild-fixed-grid/`. Der
gepaarte 0.9.0-Log bleibt unter
`.native-analysis/chat10-next/thasos-player7-prebuild-capture/`, weil seine
Oracle-Aggregate und Optionswerte gültig sind. Sein Live-Grid ist ausdrücklich
ungültig: Version 0.9.0 las dafür `session.AivStateAddress` statt des vom
Validator verwendeten Placement-State-Zeigers.

## Bestätigte Korrekturen

ActiveAIVDetector 0.9.1 erfasst den Placement-State-Zeiger direkt aus den
Validator-Aufrufen, prüft seine Konsistenz und erzeugt daraus das vollständige
Live-Building-Grid. Der installierte Lauf bestätigte
`advopt_pre_build=0`, Spieler 7, Keep `(433,373)`, Rotation 0 sowie 1817
ausgewertete und 105 blockierte Zellen. Offline und nativ stimmen für alle
1817 Zellen dieses Falls überein.

Der native Kartenstart baut einen akzeptierten KI-Startkomplex nach der
Rotationsauswahl neu auf. Er behält nicht den serialisierten Footprint an der
ursprünglichen Lage. `AivPreplacementMapState` rekonstruiert deshalb für jeden
bereits verarbeiteten KI-Slot

- alle Gebäudezellen des Startkomplexes,
- die gewählte Rotation `0`, `90`, `180` oder `270` und
- die eindeutig gekoppelten Wall-Flags der Nachbarzellen.

Ein abgelehnter KI-Start bleibt dagegen unverändert serialisiert. Die
Transformationen sind durch den nativen Startloop bei RVA `0x935A0`, den
Building-Aufruf bei RVA `0x6C7F0` und das gültige 0.9.1-Live-Grid belegt. Damit
stieg der neue No-PreBuild-Corpus zuerst von 17/24 auf 20/24 und nach Mitnahme
der Wall-Flags auf 24/24 exakte Fälle. Der Build enthält 29 erfolgreiche
synthetische Tests.

## Sitzungs- und Sofortspawn-Modell

Der native Start verarbeitet Spieler vollständig in ID-Reihenfolge `1..8`.
Frühere reale Startkomplexe sind in beiden Modi vorhanden. Frühere AIV-Pläne
blockieren ohne Sofortspawn nicht. Bei `advopt_pre_build=1` sieht der nächste
Spieler dagegen die tatsächlich ausgeführten Gebäude und Tile-Änderungen.

Die bisherige Offline-Projektion des ausgewählten AIV-Plans ist dafür noch zu
grob: Der gültige Paarkorpus erreicht im Modus `1` nur 6/24 exakte Fälle. Diese
18 Mismatches werden nicht durch angepasste Sollwerte oder eine
`NotEvaluable`-Umetikettierung verdeckt.

## Nächste Laufzeitevidenz

Die installierte Trace-Konfiguration wird für genau einen Thasos-Lauf mit
Sofortspawn auf folgende Filter gesetzt:

    PlayerId = -1
    CandidateId = 0
    Orientation = -1
    KeepX = -1
    KeepY = -1
    MaximumCaptureCount = 6

Damit wird jeweils der erste Kandidatenversuch der Spieler 2 bis 7 erfasst.
Jeder Dump enthält das reale vollständige Building-Grid vor diesem Spieler.
Ein einziger Start mit allen sechs KI-Slots reicht deshalb aus, um die
PreBuild-Zustandsübergänge zu bestimmen.

## Abnahmekriterien

Chat 10 ist erst abgeschlossen, wenn

- der neue Sofortspawn-Lauf valide Placement-State-Pointer und sechs Grids
  liefert;
- die 18 verbleibenden Fälle erklärt und korrigiert oder technisch zwingend
  als `NotEvaluable` belegt sind;
- alle neuen sitzungs- und modusgebundenen Fälle 0 ungeklärte Mismatches und
  0 Fehler liefern;
- `AIVPlacement/build.bat` weiterhin vollständig erfolgreich ist.
