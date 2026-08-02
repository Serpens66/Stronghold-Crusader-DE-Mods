# SCDE-Koordinatensysteme und native Row-LUT

## Ergebnis

Die Tile-Layer von Stronghold Crusader Definitive Edition sind keine
`800 * 800`-Matrix. Sie enthalten immer `320800` Einträge für eine Raute mit
800 Zeilen. Die native Abbildung lautet:

    tileId = x + rowStart[y]

`rowStart[y]` stammt aus einer nativen Tabelle mit einem 12-Byte-Schritt pro
Zeile. Vor der Addition müssen `(x,y)` gegen die gültige Spanne der Zeile
geprüft werden. Ohne diese Prüfung können ungültige Koordinaten wegen
überlappender Zahlenbereiche scheinbar gültige IDs anderer Zeilen ergeben.

Eine zweite wichtige Erkenntnis ist negativ: Die acht Keep-Positionen im
U4-Metadatenblock sind 200×200-Radar-/Vorschaukoordinaten. Sie sind keine
Tile-Koordinaten und dürfen weder direkt noch mit einem World-Size-Versatz in
die Row-LUT eingesetzt werden.

## Analysierte Binärdatei

- Datei: `x86_64/CrusaderDE.dll`
- SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- bevorzugte Image Base: `0x180000000`

Alle folgenden Adressen sind RVA-Werte relativ zur Image Base und damit trotz
ASLR reproduzierbar:

| RVA | Rolle |
|---:|---|
| `0x04AE70` | initialisiert die 800-Zeilen-Geometrie, Row-Daten und die feste 800×800-Gültigkeitsmaske |
| `0x04F3F0` | baut die inverse `tileId -> y`-Tabelle aus Row-LUT und Gültigkeitsmaske |
| `0x06E521` | nativer Row-LUT-Verbraucher; bildet ausdrücklich `rowStart[y] + x` |
| `0x062970` | native Rückabbildung; liest `y` aus der inversen Tabelle und berechnet `x = tileId - rowStart[y]` |
| `0x070609` | setzt die Kartenrand-Flags für die aktuelle World Size auf derselben festen Geometrie |

Die im untersuchten Build verwendeten Daten liegen bei Row-LUT-RVA
`0x402DEBC` und inverser Tabellen-RVA `0x3AAC234`. Diese Datenadressen sind
nur Diagnosewerte für genau den genannten Hash und keine stabile API.

Die Row-LUT wurde zusätzlich über die eindeutige Signatur
`48 8D 91 ?? ?? ?? ?? 48 8D 14 82` gefunden. Der Script Extender verwendet
dieselbe Signatur und dieselbe Formel in `GameTileManagerAPI.GetTileId`.

## Native Tile-Koordinaten

`x` und `y` sind logische Kartenachsen, keine Bildschirmkoordinaten:

- wachsendes `x` läuft innerhalb einer LUT-Zeile von deren erstem zu deren
  letztem Tile;
- wachsendes `y` wechselt zur nächsten LUT-Zeile;
- eine Kamerarotation ändert weder `(x,y)` noch `tileId`;
- beide Achsen liegen im festen Bereich `0..799`, aber nicht jedes Paar in
  diesem Quadrat gehört zur Kartenraute.

Für `0 <= y < 400` gilt:

    firstX(y)   = 399 - y
    lastX(y)    = 400 + y
    width(y)    = 2*y + 2
    firstId(y)  = y*(y + 1)
    rowStart(y) = firstId(y) - firstX(y)
                = y*y + 2*y - 399

Für `400 <= y < 800`, mit `n = 800 - y`, gilt:

    firstX(y)   = y - 400
    lastX(y)    = 1199 - y
    width(y)    = 2*n
    firstId(y)  = 320800 - n*(n + 1)
    rowStart(y) = firstId(y) - firstX(y)

Eine Koordinate ist in der maximalen Geometrie genau dann gültig, wenn
`0 <= y < 800` und `firstX(y) <= x <= lastX(y)`. Gültige IDs liegen lückenlos
in `0..320799`. Die vier mittleren Tiles sind `(399,399)`, `(400,399)`,
`(399,400)` und `(400,400)`.

### Aufbau der Row-LUT

Der Initialisierer durchläuft exakt 800 Zeilen und rückt den Schreibzeiger pro
Zeile um drei `int32`-Werte beziehungsweise 12 Byte weiter. Der von der
Signatur gelieferte Zeiger zeigt auf das von der Engine verwendete
`rowStart`-Feld; deshalb wird es als `rowTable[3*y]` gelesen. Der Initialisierer
schreibt außerdem zwei interne Werte unmittelbar vor jedem `rowStart`-Feld.
Ihre Semantik wird für die XY-Abbildung nicht benötigt und bleibt bewusst
nicht Teil des Offline-Vertrags.

Die inverse Tabelle enthält `uint16 y` für jede der 320800 gültigen IDs. Die
native Rückabbildung ist daher:

    y = inverseRow[tileId]
    x = tileId - rowStart[y]

## World Size und Kartenrand

Die untersuchten realen Karten der Größen 160, 200, 300 und 400 besitzen für
jeden vollständigen Placement-Layer ebenfalls 320800 Einträge. Die native
Grenzfunktion berechnet:

    border = (800 - worldSize) / 2

Sie verwendet anschließend die feste 800er Geometrie und setzt Randflags auf
einer eingerückten Raute. Es wird weder eine neue LUT erzeugt noch werden die
Sections auf `worldSize * worldSize` verkürzt.

Für eine gerade, unterstützte World Size kann ein lokales Tile-Koordinatensystem
zur Beschreibung dieser eingerückten Raute verwendet werden:

    nativeX = localX + border
    nativeY = localY + border

Dabei ist auch der lokale Bereich rautenförmig, nicht rechteckig. Die erste und
letzte lokale Zeile enthalten je zwei Tiles, die beiden mittleren Zeilen je
`worldSize` Tiles. Ein Tile kann also in der globalen 800er Geometrie eine
gültige ID besitzen und trotzdem außerhalb der aktuell spielbaren World-Size-
Raute liegen.

Die derzeitige Script-Extender-Hilfsmethode `IsTileInsideMapBounds` verwendet
eine theoretische Manhattan-Distanz um `(400,400)`. Sie stimmt an den paarigen
Mittel- und Randzeilen nicht exakt mit der nativen Grenzfunktion überein und ist
deshalb kein Beleg für Offline-Grenzprüfungen. Ebenso ist `IsValidTileId` mit
seiner Schranke `800*800` zu weit; die linearen Tile-Layer enden bei 320799.

## Lineare Map-Sections

Alle von `MapTileLayers` zusammengeführten Placement-Sections verwenden
dieselbe `tileId` als Elementindex:

| Logische Section | Elementtyp | Bytes bei 320800 Tiles |
|---:|---|---:|
| 1003 Logic | `int32` | 1283200 |
| 1037 Logic2 | `byte` | 320800 |
| 1005 Height | `byte` | 320800 |
| 1045 DefaultHeight | `byte` | 320800 |
| 1004 Organism | `uint16` | 641600 |
| 1012 Building | `uint16` | 641600 |
| 1026 Entity | `uint16` | 641600 |
| 1043 WallOwner | `byte` | 320800 |

SCDE-Dateien können die entsprechenden neuen 3000er IDs tragen; der Parser
normalisiert sie auf die logischen IDs. Diese ID-Normalisierung verändert
nicht die Tile-Reihenfolge.

## U4-Keep-Positionen

Der native Map-Lader kopiert die 64 U4-Bytes unverändert in den globalen
Keep-Puffer. `DLL_LoadMapToPlay` gibt je Komponente nur den niederwertigen
`int16`-Wert an Unity zurück. Managed Code übernimmt die Werte unverändert in
`GameData.Keep_Locations`. `GameData.getKeepPosition` zieht lediglich den
Icon-Versatz `(4,6)` ab und positioniert damit Schilde auf einer 200×200-Radar-
Darstellung; die skalierte Variante rechnet ebenfalls ausdrücklich von 200
auf 232 Pixel um.

Damit gelten für U4:

- `x` wächst auf der Radaransicht nach rechts, `y` nach unten;
- `(-1,-1)` kennzeichnet einen unbenutzten Slot;
- die Werte bleiben über verschiedene World Sizes im ungefähr 200×200 großen
  Vorschauraum;
- die Rasterung ist für eine exakte Umkehrung auf native Tiles nicht geeignet;
- U4 darf nur für Lobbydarstellung und Slotzuordnung verwendet werden, nicht
  als AIV-Weltanker.

Beobachtete reale Metadatenbeispiele sind in der Fixture enthalten. Es werden
keine proprietären Kartendaten kopiert, sondern nur die acht bereits vom
MapParser ausgegebenen Integer-Metadaten als Gegenbeispiele festgehalten.

Der tatsächliche Runtime-Keep-Anker ist ein anderer Wert: Der Script Extender
liest ihn nach dem Spawn aus `GamePlayerResources.r_KeepTilePositionX/Y`; die
lokale SpawnCastle-Implementierung verwendet außerdem die Tile-Position des
gebauten Keep-Gebäudes. Vor dem Mapstart muss dieser native Tile-Anker daher aus
einer noch zu belegenden Offline-Datenquelle abgeleitet werden. U4 ersetzt
diesen noch offenen Schritt nicht.

## AIV-Grid, Keep-Anker und Rotation

Das AIVJSON verwendet ein unabhängiges 100×100-Grid:

    row    = encodedOffset / 100
    column = encodedOffset % 100

Der vorhandene `AIVParser` rotiert um dieses gesamte Grid:

| Rotation | Ergebnis `(row,column)` |
|---:|---|
| 0° | `(row, column)` |
| 90° | `(column, 99-row)` |
| 180° | `(99-row, 99-column)` |
| 270° | `(99-column, row)` |

Relative Offsets werden erst nach gemeinsamer Rotation von Element und
AIV-Keep-Anker gebildet. Die derzeit im Workspace getestete Projektion auf
native Weltkoordinaten lautet anschließend:

    worldX = keepTileX + deltaColumn
    worldY = keepTileY - deltaRow

Diese Projektion benötigt ausdrücklich einen echten nativen Keep-Tile-Anker.
Sie darf nicht mit einer U4-Radarposition aufgerufen werden. Footprint- und
Placement-Semantik sind nicht Teil dieses Schritts.

## Testvektoren

Die maschinenlesbaren Vektoren stehen in
`MapParser.Tests/Fixtures/MapTileGeometryVectors.json`. Sie decken ab:

- erste und letzte gültige Position früher, mittlerer und später Zeilen;
- alle vier Randbereiche und die vier mittleren Tiles;
- Zeilenübergänge und inverse Roundtrips;
- World-Size-Ränder für 160, 200, 300 und 400;
- negative, quadratisch gültig aussehende, aber außerhalb der Raute liegende
  Koordinaten sowie ungültige Tile-IDs;
- reale U4-Radarbeispiele für 160, 200, 300 und 400, ausdrücklich ohne
  erfundene Tile-ID.

## Folgerung für die Roadmap

Chat 1 belegt die Section-Geometrie vollständig. Der ursprüngliche Plan setzte
jedoch implizit voraus, dass U4 die vor dem Mapstart benötigten Keep-Tile-
Koordinaten liefert. Diese Annahme ist widerlegt. Vor der AIV-Projektion ist
deshalb ein eigener Nachweis nötig, aus welchen Map-Sections oder welcher
nativen Ladeoperation sich pro Keep-Slot der exakte Tile-Anker offline ableiten
lässt. Falls das nicht verlustfrei möglich ist, muss das Ergebnis vor Mapstart
für diesen Slot `NotEvaluable` bleiben.
