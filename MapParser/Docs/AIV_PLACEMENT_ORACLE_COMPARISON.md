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

Der Importer und der Vergleich besitzen dafür keinen Legacy-Modus mehr:
sitzungslose Fälle, unbekannte Sofortspawn-Werte, fehlende native
Selection-Zeilen und manuelle Modus-Overrides werden abgewiesen.

Der aktuelle Stand auf `v_Thasos.map` ist:

| Corpus | Modus | Fälle | Exakt | Mismatch | `NotEvaluable` | Fehler |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Captured-2026-08-03-SessionAware` | `0` | 24 | 24 | 0 | 0 | 0 |
| `Captured-2026-08-03-SessionAware-Paired` | `0` | 24 | 24 | 0 | 0 | 0 |
| `Captured-2026-08-03-SessionAware-Paired` | `1` | 24 | 4 | 0 | 20 | 0 |

Der gesamte Paarkorpus enthält damit 28 exakte Fälle, 20 technisch begründete
`NotEvaluable`, 0 Mismatches und 0 Fehler. Chat 10 bleibt für die gezielte
Rekonstruktion der mapperabhängigen nativen Spawnregeln geöffnet. Der
`ExecuteBuildStep`-Trace für Spieler 2 ist inzwischen vollständig abgenommen;
Chat 11 hat noch nicht begonnen.

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
- ActiveAIVDetector-0.9.3-`ExecuteBuildStep`-Log: SHA-256
  `AB1DA48994AB1AEABD6C099478E4568035BC5E9F6CE405930E2DDDF4628BA13E`
- Spieler-2-Prebuild-Trace: SHA-256
  `66835ECAF440A17A19881439DAD1C5B10E9B2E19A1B041DD2071F83DD2D9E234`

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

Der gültige ActiveAIVDetector-0.9.2-Sofortspawn-Lauf liegt unter
`.native-analysis/chat10-next/thasos-prebuild-20260803-165341/`. Sein Log hat
SHA-256
`D10604EC3BE35D39EF2CC72CEA9BD1F6F68F3EF8B56205DBE55EED585F23357F`.
Er enthält je einen Trace und ein vollständiges Building-Grid für Spieler 2
bis 7, explizit `advopt_pre_build=1` und keine Pointer-Warnung.

Der gezielte ActiveAIVDetector-0.9.3-Lauf liegt unter
`.native-analysis/chat10-next/thasos-execute-build-step-20260803-182959/`.
`SHA256SUMS.txt` bindet Log, Konfiguration, `info.json` und den vollständigen
77-Frame-Trace. Der Lauf verwendete `advopt_pre_build=1`, Spieler 2,
`restrictedMode=0` und `freeOrForced=1`. Alle Frames verwendeten denselben vom
Validator beobachteten Placement-State-Zeiger; es gab 0 Pointerprobleme,
0 Capturefehler und keine Hookwarnung.

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

Diese Rotation ist dieselbe Auswahl, die für die AIV gilt. AIV, Keep,
Vorratslager und weitere gekoppelte Startgebäude werden nie unabhängig
voneinander gedreht. Der feste Ursprung der rotierten 100×100-Fit-Grids ist nur
eine Koordinatenregel und hebt diese Kopplung nicht auf.

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

Die frühere Offline-Projektion des ausgewählten AIV-Plans war dafür nicht nur
zu grob, sondern sachlich falsch. Der 0.9.2-Lauf zeigt, dass der tatsächliche
Spawn weder der Menge der `Placeable`-Elemente noch ausschließlich deren
Kernfootprints entspricht. Die Projektionsklasse und ihre exklusiven
Herkunftstypen wurden deshalb entfernt. Nach einem ausgeführten früheren
Prebuild wird ohne beobachteten Live-Zustand jetzt ausdrücklich
`NotEvaluable` geliefert.

## Ausgewertete Laufzeitevidenz

Ein erster Lauf mit Version 0.9.1 bestätigte erneut `advopt_pre_build=1` und
alle 24 nativen Fälle, deckte aber einen Fehler im Diagnosefilter auf:
`MaximumCaptureCount` zählte Rotationen statt Spieler und das vollständige
Grid wurde pro Prozess nur einmal geschrieben. Dadurch entstanden vier Dumps
für Spieler 2, zwei für Spieler 3 und nur ein vollständiges Grid. Der Lauf ist
unter `.native-analysis/chat10-next/thasos-prebuild-20260803-164639/` mit dem
Log-Hash
`90DFAAB2CE9C4E474A68B02B4E540F81BA4B185963D99A5A2D3B67CDC6997E46`
gesichert. ActiveAIVDetector 0.9.2 korrigiert beide Diagnoseprobleme.

Der erfolgreiche Thasos-Lauf verwendete folgende Filter:

    PlayerId = -1
    CandidateId = 0
    Orientation = -1
    KeepX = -1
    KeepY = -1
    MaximumCaptureCount = 6

Damit wurde jeweils der erste Kandidatenversuch der Spieler 2 bis 7 erfasst.
Jeder Dump enthält das reale vollständige Building-Grid vor diesem Spieler.
Die Zustandsfolge belegt zugleich, dass frühere Ausführung vorhandene Zellen
ersetzen oder entfernen kann und deshalb keine monotone Planvereinigung ist.

Beim ersten akzeptierten Prebuild von Spieler 2 entstanden 757 AIV-
Gebäudezellen. Gegen die frühere Projektion waren 707 identisch, 50 nur
projiziert und 50 nur live vorhanden. Die nur projizierten Zellen gehörten zu
den geplanten Footprints von Stockpile und Drawbridge; die nur live vorhandenen
Zellen zum zuvor als blockiert bewerteten Tunnelers Guild samt 5×5-Hof. Beim
ersten Versuch von Spieler 3 waren alle zwölf Differenzen zusätzliche reale
Gebäudezellen.

## Native Erklärung der auffälligen Spawnzweige

Der Read-only-Audit von `ExecuteBuildStep` (RVA `0x509F0`) zeigt, dass
`freeOrForced=true` nur den Ressourcen-/Verfügbarkeitsaufruf bei RVA `0xCB630`
umgeht. Der Footprint-Helfer RVA `0x5C000` prüft zwar den sequenziell bereits
veränderten Live-Zustand mit echter Spieler-ID und Validator-Modus `1`; sein
Rückgabewert wird danach aber ignoriert. Deshalb kann ein Konstruktor auch nach
fehlgeschlagener Footprint-Prüfung ausgeführt werden.

Für Mapper 89 erzeugt Konstruktor RVA `0x76670` neben dem Hauptgebäude einen
zweiten 5×5-Datensatz vom Strukturtyp `59`. Dies entspricht genau den zwei
Gebäude-IDs und 50 zusätzlichen Zellen des Traces. Mapper 105 besitzt dagegen
mit RVA `0x793E0` einen vorgeschalteten Resolver für ein passendes lebendes Tor
und dessen Orientierung. Mapper 52 ist ebenfalls kein „Tile-only“-Sonderfall:
Sein Konstruktor RVA `0x760F0` erzeugt vier Gebäudedatensätze und zusätzliche
Verbindungstiles. Die konkreten 25 Stockpile-Zellen fehlten nur an der früher
projizierten Position dieses Laufs; die genaue Abbruch- oder
Koordinatenentscheidung dieses einzelnen Frames erfordert einen
`ExecuteBuildStep`-Laufzeittrace.

## Gezielter `ExecuteBuildStep`-Laufzeittrace

Der 0.9.3-Trace erfasste lückenlos die Frames `0..76` des ersten
Spieler-2-Sofortspawns. Insgesamt änderten die Frames 757 zuvor freie
`BuildingId`-Zellen; innerhalb dieses ersten Spielers gab es keine entfernten
oder ersetzten IDs. Die drei zuvor offenen Mapper sind damit direkt belegt:

- Mapper 105, Frame 28: Status `1`, eine Position, Rückgabewert `0`, keine
  `BuildingId`-Änderung. Der vorbereitete Drawbridge-Schritt wurde aufgerufen,
  scheiterte aber vor einem erfolgreichen Konstruktor; das entspricht dem
  fehlgeschlagenen Gate-/Orientierungsresolver.
- Mapper 89, Frame 36: Status `1`, eine Position, Rückgabewert `1`, exakt 50
  hinzugefügte Zellen. Gebäude-ID 33 und 34 belegen jeweils 25 Zellen und
  bestätigen Hauptgebäude plus separaten 5×5-Hof.
- Mapper 52, Frame 41: Status `1`, eine Position, Rückgabewert `0`, keine
  `BuildingId`-Änderung. Der konkrete Goods-Yard-Schritt brach somit ab; er
  erzeugte weder den früher projizierten Footprint noch Gebäudedatensätze an
  einer anderen Stelle.

Die vom Benutzer später im Spiel beobachteten Gebäudeabrisse stammen nicht aus
dem erfassten Spieler-2-Fenster (`removed=0`, `replaced=0`). Sie sind mit den
anschließend ausgeführten Spielern und der bereits belegten sequenziellen
Bereinigungslogik vereinbar, bilden aber ohne deren eigenen Frame-Trace keine
zusätzliche positionsgenaue Evidenz.

Diese Ergebnisse erklären die drei auffälligen Frames, liefern jedoch noch
keine vollständige mapperabhängige Offline-Ausführung für alle späteren
Spieler. Die 20 abhängigen Sofortspawn-Fälle bleiben deshalb bewusst
`NotEvaluable`; es werden keine Planfootprints als Live-Belegung projiziert.

## Abnahmekriterien

Chat 10 ist erst abgeschlossen, wenn

- der neue Sofortspawn-Lauf valide Placement-State-Pointer und sechs Grids
  liefert;
- der gezielte Spieler-2-Trace alle `ExecuteBuildStep`-Frames ohne Pointer-,
  Capture- oder Hookfehler bindet und Mapper 52, 89 und 105 entscheidet;
- die 20 abhängigen PreBuild-Fälle erklärt und korrigiert oder technisch zwingend
  als `NotEvaluable` belegt sind;
- alle neuen sitzungs- und modusgebundenen Fälle 0 ungeklärte Mismatches und
  0 Fehler liefern;
- `AIVPlacement/build.bat` weiterhin vollständig erfolgreich ist.
