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
umgeht unter anderem die Ressourcenbegrenzung. Mapperfamilien besitzen
unterschiedliche native Ausführungszweige, weshalb „geplant“ nicht automatisch
„als Gebäude-ID erfolgreich erzeugt“ bedeutet.

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
| `ProjectedPrebuiltAivBuilding` | als Näherung | aus Plan und erfolgreicher Fit-Prüfung abgeleitetes Gebäude eines bereits ausgeführten Sofortspawns; noch nicht live beobachtet |
| `ProjectedPrebuiltAivTile` | als Näherung | entsprechend abgeleitete Mauer-, Graben-, Fallen- oder andere Tile-Belegung |
| `PrebuiltAivBuilding` | ja | durch einen früheren Sofortspawn live beobachtetes AIV-Gebäude |
| `PrebuiltAivTile` | ja | durch einen früheren Sofortspawn live beobachtete sonstige Tile-Belegung |
| `RuntimeBuildingUnknown` | ja | echtes Laufzeitgebäude, dessen genauere Herkunft in der Aufnahme fehlt |

`BuildingId` bleibt ausschließlich rohe reale Map-/Laufzeitevidenz. Eine
Offline-Projektion darf keine künstliche Gebäude-ID erfinden. Simulierte
Sofortspawn-Belegung trägt stattdessen ihre explizite Herkunft. Eine nur aus
dem Plan abgeleitete Belegung erzeugt
`ProjectedPriorAivPrebuildOccupied`; eine live bestätigte Belegung erzeugt
`PriorAivPrebuiltOccupied`. Geplante oder nur zur Ausführung vorgemerkte
Elemente erzeugen keinen dieser Blockergründe.

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
- Frühere AIV-Projektionen werden ausschließlich bei Wert `1` als bereits
  ausgeführte PreBuild-Belegung weitergetragen. Bei Wert `0` werden sie nicht
  als Blocker übernommen. Ein fehlender oder unbekannter Wert macht den Korpus
  ungültig und wird bereits beim Import oder vor der Auswertung abgewiesen.

Die als `ProjectedPrebuiltAiv*` bezeichnete Offline-Belegung ist eine
deterministische Annäherung an die erfolgreiche native Ausführung. Wo exakte
Gebäude-IDs oder Mapper-Sondereffekte entscheidend sind, bleibt ein vor der
nächsten Spielerprüfung aufgenommener Live-Grid-Snapshot die maßgebliche
Evidenz.

## Reproduzierbare Audit-Artefakte

- Treiber: `.native-analysis/Run-Chat10-Prebuild-Order-Audit.cmd`
- stdout: `.native-analysis/chat10-prebuild-order.stdout.log`
- stderr: `.native-analysis/chat10-prebuild-order.stderr.log`
- Candidate Loader: `.native-analysis/chat10-candidate-loader.stdout.log`
- Fit-Prüfung: `.native-analysis/chat10-evaluate-fit.stdout.log`
