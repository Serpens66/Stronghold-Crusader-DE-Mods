# Native Sofortspawn- und Überschneidungsreihenfolge

## Geltungsbereich und Binärbezug

Diese Beschreibung gilt für Stronghold Crusader Definitive Edition 2.7.0.1
und die untersuchte `CrusaderDE.dll` mit SHA-256
`17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`.
Die bevorzugte Image Base ist `0x180000000`; alle folgenden RVAs sind daher
von laufzeitabhängigen VAs unabhängig.

Die Lobbyoption **Completed enemy castles** wird managed als
`MPsetupData.advopt_pre_build` geführt und vor Spielstart in die native
Startstruktur übertragen. `0` bedeutet aus, `1` bedeutet an. Ein nicht
erfasster Wert ist kein zulässiger Ersatz für eine Annahme: Eine
sitzungsabhängige Offline-Auswertung ist dann `NotEvaluable`.

## Äußere Reihenfolge: vollständiger Spieler, dann nächster Spieler

Die native Kartenstartschleife verarbeitet die Spieler-IDs aufsteigend von
`1` bis `8`. Spieler-ID `0` ist Natur und wird hier nicht als Spieler
durchlaufen. Dass der spätere Einzelzellen-Validator mit Spieler-ID `0`
aufgerufen wird, ist ein davon unabhängiger Prüfmodus.

Für jeden aktiven KI-Spieler läuft der relevante Ablauf vollständig ab,
bevor die Schleife die Spieler-ID erhöht:

1. AIV-Kandidaten und zulässige Rotationen prüfen;
2. den finalen Kandidaten auswählen und dessen Layout vorbereiten;
3. den eigenen gedrehten Startkomplex, insbesondere Keep und Vorratslager,
   erzeugen;
4. bei aktivem `advopt_pre_build` die vorbereitete AIV zu `100 %` ausführen;
5. erst danach zur nächsten Spieler-ID wechseln.

Die in Schritt 1 ausgewählte Orientierung ist eine gemeinsame Eigenschaft von
AIV und Spielerstart. Der in Schritt 3 erzeugte Keep, das 5×5-Vorratslager und
weitere gekoppelte Startgebäude werden mit genau derselben Rotation neu
aufgebaut. Eine AIV-Rotation unabhängig vom Keep gibt es in diesem Ablauf nicht.

Der entscheidende native Abschnitt liegt im Kartenstartpfad um RVA
`0x95180`. Der Sofortspawn-Test liest die globale Option an VA
`0x1887EB2E8`. Bei aktivem Modus setzt RVA `0x9523E` das Spielerbit, lädt an
RVA `0x95244` den Prozentwert `100` und ruft an RVA `0x95255`
`ExecuteToPercentage` (RVA `0x551C0`) auf. Erst anschließend erhöhen RVA
`0x95296` die Spieler-ID und RVA `0x9529C` den Spielerzustand um `0x583C`;
RVA `0x952A7` vergleicht gegen `9`.

Folge: Bei der Prüfung von Spieler `N` sind die Startkomplexe aller bereits
verarbeiteten Spieler real vorhanden. Die AIV-Elemente früherer KI-Spieler
sind nur dann ebenfalls real vorhanden, wenn Sofortspawn aktiv ist.

## Kandidatenprüfung: Rasterprüfung, nicht AIV-gegen-AIV nach Frames

Die offizielle Fit-Prüfung führt keinen eigenständigen Paarvergleich
„AIVJSON A gegen AIVJSON B“ aus. `LoadCandidate` (RVA `0x54590`) liest den
aktuellen Kandidaten in ursprünglicher Frame- und Positionsreihenfolge und
verdichtet ihn in ein 100×100-Mapperraster sowie ein paralleles Scoreraster.
Für jede belegte Footprintzelle wird der Mapperwert und `Frameordinal + 1`
geschrieben. Beanspruchen mehrere Einträge dieselbe Rasterzelle, überschreibt
der später geladene Eintrag den früheren.

`EvaluateCandidateFit` (RVA `0x562F0`) scannt danach genau dieses fertige
Raster zeilenweise, jeweils Spalten `0..99` und Zeilen `0..99`. Nur die im
fertigen Raster ungleich null belegten Zellen werden gegen den zu diesem
Zeitpunkt realen TileManager-Zustand geprüft. Der gemeinsame Validator
(RVA `0x7A2D0`) erhält dabei `playerId=0` und `mode=0`.

Damit gilt für Kollisionen zwischen Burgen:

- Sofortspawn **aus**: Eine lediglich geplante AIV eines früheren Spielers ist
  kein Blocker. Dessen bereits erzeugter Startkomplex kann trotzdem blockieren.
- Sofortspawn **an**: Die AIV eines früheren Spielers wurde vor der Prüfung des
  nächsten Spielers ausgeführt. Tatsächlich erzeugte Gebäude, Mauern und
  sonstige Tile-Änderungen sind daher normale Live-Eingaben der nächsten
  Fit-Prüfung.
- Die geplanten Elemente des gerade geprüften Kandidaten blockieren sich nicht
  wie bereits vorhandene Gebäude. Interne Mehrfachbelegung wird beim Aufbau
  des Kandidatenrasters durch die Überschreibereihenfolge aufgelöst.

Die zwei Thasos-Läufe mit identischer Spieler-5-Position bestätigen den
Zustandsunterschied: Ohne Sofortspawn waren je Rotation
`320/189/275/284` Rasterzellen blockiert, mit Sofortspawn
`433/313/354/353`. Es gab ausschließlich zusätzliche `free -> blocked`-
Übergänge; die frühen Kandidatenablehnungen blieben unverändert.

## Sofortspawn innerhalb einer AIV

`ExecuteToPercentage` (RVA `0x551C0`) berechnet aus dem höchsten vorbereiteten
Build-Frame und dem Prozentwert den letzten auszuführenden Frame. Es ruft
`ExecuteBuildStep` (RVA `0x509F0`) beginnend mit Frame `0` auf, erhöht den
Frameindex jeweils um eins und läuft einschließlich des berechneten letzten
Frames. Bei `100 %` werden somit alle vorbereiteten Frames in aufsteigender
Reihenfolge ausgeführt.

`ExecuteBuildStep` adressiert genau den vorbereiteten Eintrag dieses Frames.
Enthält er mehrere Positionen, werden diese in der Reihenfolge des
vorbereiteten Positionsarrays von Index `0` aufwärts verarbeitet. Der
Sofortspawn-Aufruf verwendet `restrictedMode=0` und `freeOrForced=true`; dies
umgeht den Ressourcen-/Verfügbarkeitsaufruf bei RVA `0xCB630`, aber nicht den
gesamten übrigen Ablauf. Mapperabhängige Vorbedingungen, der
Drawbridge-Nachbarschaftstest, die Erzeugbarkeitsprüfung und der jeweilige
Gebäudekonstruktor bleiben aktiv.

Für normale Gebäude löst RVA `0x69400` den Mapper zum internen Strukturtyp
auf. Anschließend ruft `ExecuteBuildStep` RVA `0x5C000` für den Kernfootprint
und bei bestimmten Mappern für zusätzliche Flächen auf. Diese Hilfsfunktion
prüft den zu diesem Frame bereits veränderten Live-Zustand mit dem gemeinsamen
Validator RVA `0x7A2D0`, diesmal mit der echten Spieler-ID und `mode=1`. Nur
wenn alle Zellen diese Prüfung bestehen, kann sie passende vorhandene
Laufzeitdatensätze entfernen. Ihr Rückgabewert wird von `ExecuteBuildStep`
jedoch nicht ausgewertet. Eine fehlgeschlagene Footprint-Prüfung verhindert im
Sofortspawn-Pfad daher den nachfolgenden Bau nicht.

Danach entscheidet RVA `0xC2E00`, ob der aufgelöste Strukturtyp grundsätzlich
erzeugt werden darf. Bei Erfolg ruft RVA `0x6C7F0` den typspezifischen
Konstruktor auf und reicht `freeOrForced=true` weiter. Erst dessen globale
Fehleranzeige bestimmt, ob der Build-Schritt als erfolgreich zurückkehrt.
„Geplant“ bedeutet deshalb weder automatisch „erzeugt“ noch „nur bei
bestandener Fit-Prüfung erzeugt“.

Für die im Thasos-Lauf auffälligen Mapper sind folgende Zweige belegt:

- Mapper 89 wird zu `STRUCT_TUNNELLERS_GUILD` aufgelöst. Konstruktor RVA
  `0x76670` erzeugt das Hauptgebäude und zusätzlich einen zweiten Datensatz vom
  Strukturtyp `59` für den 5×5-Hof. Beide Footprints schreiben reale
  `BuildingId`-Zellen. Dies erklärt exakt die zwei beobachteten Gebäude-IDs und
  50 Zellen trotz zuvor blockierter Kandidaten-Fit-Prüfung.
- Mapper 88 verwendet analog Konstruktor RVA `0x72E20`; das zweite 5×5-Gebäude
  hat dort Strukturtyp `53`.
- Mapper 105 durchläuft vor dem allgemeinen Pfad RVA `0x793E0`. Der Resolver
  sucht in vier Richtungen nach einem passenden lebenden Tor und wertet dessen
  Orientierung aus. Ohne passende Nachbarschaft bricht der Build-Schritt ab.
  Bei Erfolg schreibt der Drawbridge-Konstruktor RVA `0x72C30` selbst reale
  `BuildingId`-Zellen und verändert zusätzlich Tile-Zustand.
- Mapper 52 wird zu `STRUCT_GOODS_YARD` aufgelöst. Konstruktor RVA `0x760F0`
  erzeugt vier Gebäudedatensätze, stempelt deren Kernzellen in das
  `BuildingId`-Grid und setzt neun zusätzliche Verbindungstiles. Das Fehlen
  der 25 früher projizierten Zellen an der geplanten Position im konkreten
  Trace darf deshalb nicht als allgemeine Aussage „Stockpiles erzeugen keine
  BuildingId“ interpretiert werden.

## Gezielte Laufzeitabnahme der drei auffälligen Mapper

ActiveAIVDetector 0.9.3 erfasste auf `v_Thasos.map` synchron um jeden
`ExecuteBuildStep`-Trampoline-Aufruf das vollständige reale
320800-Zellen-`BuildingId`-Grid. Der Lauf ist unter
`.native-analysis/chat10-next/thasos-execute-build-step-20260803-182959/`
gesichert und in `SHA256SUMS.txt` gebunden. Er enthält für Spieler 2 genau 77
lückenlose Frames `0..76`, überall `restrictedMode=0`, `freeOrForced=1` und
denselben vom Validator beobachteten Placement-State-Zeiger. Es gab keine
Pointer-, Capture- oder Hookfehler.

- Mapper 105 wurde in Frame 28 mit Status `1` und einer Position aufgerufen,
  gab `0` zurück und änderte keine `BuildingId`. Der konkrete Drawbridge-
  Resolver fand daher keine zulässige Gate-/Orientierungskombination; der
  Konstruktor erzeugte nichts.
- Mapper 89 wurde in Frame 36 mit Status `1` und einer Position aufgerufen,
  gab `1` zurück und fügte exakt 50 Zellen hinzu. Gebäude-ID 33 und 34 belegen
  je 25 Zellen und sind die erwarteten getrennten Datensätze für Hauptgebäude
  und Hof.
- Mapper 52 wurde in Frame 41 mit Status `1` und einer Position aufgerufen,
  gab `0` zurück und änderte keine `BuildingId`. Der konkrete Goods-Yard-
  Schritt brach vor erfolgreicher Konstruktion ab und wirkte auch nicht mit
  einer abweichenden nativen Koordinate auf das Building-Grid.

Über den gesamten ersten Spieler entstanden 757 neue Gebäudezellen, aber keine
entfernten oder ersetzten. Später im Spiel sichtbare Abrisse können daher nur
aus nachfolgenden Spielern oder späterer Spiellogik stammen; sie passen zur
sequenziellen Bereinigungsmöglichkeit, sind in diesem Spieler-2-Fenster aber
nicht positionsgenau erfasst.

## Verbindliche Zustands- und Blockerbegriffe

`AivTileOccupancyKind` trennt Ursache und Zeitpunkt:

| Herkunft | Blockiert die aktuelle Fit-Prüfung? | Bedeutung |
| --- | --- | --- |
| `MapPreplacedBuilding` | ja | echtes, von der Map serialisiertes Gebäude, das kein normalisierter Spielerstart ist |
| `PlayerStartKeep` | ja, sobald früherer Start erzeugt ist | Keep des bereits verarbeiteten Spielerstarts |
| `PlayerStartStockpile` | ja, sobald früherer Start erzeugt ist | gedrehtes 5×5-Vorratslager des Spielerstarts |
| `PlayerStartBuilding` | ja, sobald früherer Start erzeugt ist | sonstiger Teil des Startkomplexes |
| `PlannedAivElement` | nein | Element des gerade untersuchten AIV-Plans |
| `ScheduledAivPrebuild` | noch nicht | Element, das bei aktivem Sofortspawn anschließend ausgeführt werden soll |
| `PrebuiltAivBuilding` | ja | durch einen früheren Sofortspawn live beobachtetes AIV-Gebäude |
| `PrebuiltAivTile` | ja | durch einen früheren Sofortspawn live beobachtete sonstige Tile-Belegung |
| `RuntimeBuildingUnknown` | ja | echtes Laufzeitgebäude, dessen genauere Herkunft in der Aufnahme fehlt |

`BuildingId` bleibt ausschließlich rohe reale Map-/Laufzeitevidenz. Eine live
bestätigte Belegung erzeugt `PriorAivPrebuiltOccupied`. Aus einem AIV-Plan wird
keine blockierende Sofortspawn-Belegung abgeleitet. Geplante oder nur zur
Ausführung vorgemerkte Elemente erzeugen diesen Blockergrund nicht.

Diese Herkunftstypen ersetzen nicht die übrigen nativen Ausschlussgründe.
Terrain-/Logic-Flags, Höhe, Entity-Belegung, Owner-/Mauerzustand,
Kartenrand und Mapper-Sonderregeln bleiben eigenständige
`AivPlacementIssueKind`-Gründe. Organismen sind im hier untersuchten
Skirmish-Prüfmodus mit Validator-Spieler `0` kein Ausschlussgrund; Spieler `0`
bezeichnet dabei weiterhin Natur und keinen KI-Spieler.

## Umsetzung im Projekt

- `ActiveAIVDetector` liest `advopt_pre_build` vor
  `StartSkirmishGame`, protokolliert ihn in jeder Selection-/Attempt-Zeile und
  schreibt ihn in Cell-Trace und Live-Building-Grid.
- Der Log-Importer übernimmt den Wert in jeden Oracle-Fall. Innerhalb einer
  `SessionId` müssen alle Fälle denselben Wert besitzen.
- Sitzungen werden in offizieller Spieler-ID-Reihenfolge ausgewertet. Für
  Spieler `N` werden nur die Startkomplexe früherer Spieler behalten; der
  eigene Start ist während seiner Kandidatenprüfung noch nicht vorhanden.
- Ein akzeptierter früherer KI-Start wird nicht an seiner serialisierten Lage
  behalten, sondern mit der nativ ausgewählten AIV-Rotation rekonstruiert. Ein
  abgelehnter Start bleibt unverändert serialisiert.
- Bei Wert `0` werden frühere AIV-Pläne nicht als Blocker übernommen. Bei Wert
  `1` bleibt der erste KI-Spieler offline auswertbar. Sobald eine ausgewählte
  AIV nativ ausgeführt wurde, benötigen alle späteren Spieler den beobachteten
  Live-Zustand; ohne ihn ist die sitzungsabhängige Auswertung `NotEvaluable`.
- Ein fehlender oder unbekannter Wert macht den Korpus ungültig und wird
  bereits beim Import oder vor der Auswertung abgewiesen.

Der frühere Versuch, alle als `Placeable` bewerteten Kernfootprints als
erfolgreich ausgeführte PreBuild-Belegung zu behandeln, ist durch den
ActiveAIVDetector-0.9.2-Lauf widerlegt und vollständig entfernt. Bei Spieler 2
ergaben Plan und Live-Grid zwar jeweils 757 Gebäudezellen, aber nur 707 waren
identisch. Je 25 geplante Zellen von Mapper 52 (Stockpile) und Mapper 105
(Drawbridge) waren an ihren früher projizierten Footprints nicht live belegt.
Stattdessen existierten 50 Zellen von Mapper 89 (Tunnelers Guild einschließlich
Hof), obwohl dessen Fit-Prüfung blockiert war. Für Spieler 3 erklärte reale
Building-Belegung alle zwölf `nativeOnly`-Zellen des ersten Traces.

Damit ist ein Live-Grid-Snapshot maßgebliche Evidenz für eine konkrete Sitzung,
aber kein allgemeiner Ersatz für die vollständige sequenzielle Ausführung. Die
relevanten Konstruktoren erklären, warum aus einem statischen Plan weder die
erzeugten Gebäude-IDs noch deren Footprints exakt abgeleitet werden können.

## Reproduzierbare Audit-Artefakte

- Treiber: `.native-analysis/Run-Chat10-Prebuild-Order-Audit.cmd`
- stdout: `.native-analysis/chat10-prebuild-order.stdout.log`
- stderr: `.native-analysis/chat10-prebuild-order.stderr.log`
- Candidate Loader: `.native-analysis/chat10-candidate-loader.stdout.log`
- Fit-Prüfung: `.native-analysis/chat10-evaluate-fit.stdout.log`
- ExecuteBuildStep-Zweige:
  `.native-analysis/chat10-execute-build-step-branches.stdout.log`
- Gebäudekonstruktoren:
  `.native-analysis/chat10-prebuild-constructors.stdout.log`
- gemeinsame Vorprüfungen:
  `.native-analysis/chat10-prebuild-common-checks.stdout.log`
- Spieler-2-`ExecuteBuildStep`-Laufzeittrace und Hashmanifest:
  `.native-analysis/chat10-next/thasos-execute-build-step-20260803-182959/`
