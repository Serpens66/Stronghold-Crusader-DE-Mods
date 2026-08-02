# Offline-Keep-Tile-Anker

## Ergebnis

Der exakte native Keep-Anker eines auswählbaren Skirmish-Slots steht in
Map-Section `1013`. Diese Section enthält 2000 serialisierte Gebäudedatensätze
zu je `0x32C` Byte. Für einen aktiven Keep-Datensatz gelten:

| Feld | Offset | Bedeutung |
|---|---:|---|
| `AliveState` | `0xD0` | `2` = `IsAlive` |
| `BuildingType` | `0xD2` | `41` = `STRUCT_KEEP_TWO`, der Skirmish-Startmarker |
| `Owner` | `0xD6` | 1-basierte Slotnummer |
| `TileX` | `0xEE` | native globale X-Koordinate |
| `TileY` | `0xF0` | native globale Y-Koordinate |
| `TileId` | `0xF4` | gespeicherte lineare Tile-ID |

`TileX` und `TileY` liegen bereits im globalen 800-Zeilen-Koordinatensystem
aus `MAP_COORDINATE_SYSTEM.md`. Sie benötigen weder Radar-Skalierung noch einen
World-Size-Versatz. Die U4-Radarwerte werden ausschließlich verwendet, um
auswählbare Slots von `(-1,-1)`-Einträgen zu unterscheiden.

## Nativer Nachweis

Analysierte Binärdatei:

- Datei: `x86_64/CrusaderDE.dll`
- SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- bevorzugte Image Base: `0x180000000`

Relevante RVA-Werte:

| RVA | Rolle |
|---:|---|
| `0x0935A0` | Skirmish-Spielstart und Keep-Erzeugung |
| `0x0949F1` | lädt pro Slot die zwei vorliegenden `int16`-Koordinaten aus dem ausgewählten Gebäudedatensatz |
| `0x094D9E` | erster relevanter `BuildStructure`-Aufruf mit `MAPPER_KEEP2` |
| `0x09511A` | weiterer Keep-`BuildStructure`-Aufruf |
| `0x09528A` | weiterer Keep-`BuildStructure`-Aufruf |
| `0x06C7F0` | `BuildStructure`-Funktion |

Der Startcode verwendet einen Datensatzschritt von `0x32C`, liest X/Y an den
Feldpositionen `0xEE/0xF0` des `GameBuilding`-Layouts und kopiert beide Werte in
seinen lokalen Slotpuffer. Die späteren Keep-Aufrufe übergeben genau dieses
Koordinatenpaar als `TileX/TileY` an `BuildStructure`; dazwischen findet keine
Skalierung, Rundung oder Projektion statt. Der Script Extender exponiert an
diesem Punkt dieselben Argumente in `BuildStructureEventArgs`.

Damit ist die Offline-/Runtime-Identität nicht nur aus ähnlichen Messwerten
abgeleitet: Offline-Parser und Runtime-Keep-Aufruf lesen dasselbe serialisierte
Koordinatenpaar. Der gespeicherte `TileId` wurde zusätzlich gegen die native
Row-LUT-Formel geprüft, ist aber nicht die Quelle des ausgegebenen Ankers.

## Abgleich realer Karten

Die folgenden Integerwerte wurden read-only aus installierten Karten gelesen.
Es wurden keine proprietären Kartendaten in das Repository kopiert. `S` ist der
nullbasierte Lobby-Slot; die native Spalte ist zugleich das unverändert an den
Runtime-Keep-Aufruf übergebene Paar.

| Karte | World Size | S | U4-Radar | nativ | Tile-ID |
|---|---:|---:|---:|---:|---:|
| Height Advantage | 160 | 0 | `(64,120)` | `(393,427)` | 181664 |
| Height Advantage | 160 | 1 | `(138,55)` | `(398,357)` | 128162 |
| Bow Ridge | 200 | 0 | `(156,91)` | `(425,366)` | 134714 |
| Bow Ridge | 200 | 1 | `(32,91)` | `(363,428)` | 182379 |
| A Layered Approach | 300 | 0 | `(147,142)` | `(490,392)` | 154539 |
| A Layered Approach | 300 | 1 | `(135,53)` | `(389,315)` | 99845 |
| A Layered Approach | 300 | 2 | `(50,57)` | `(308,405)` | 164683 |
| A Friend Indeed | 400 | 0 | `(29,54)` | `(284,423)` | 178555 |
| A Friend Indeed | 400 | 1 | `(67,26)` | `(294,356)` | 127343 |
| A Friend Indeed | 400 | 2 | `(175,70)` | `(446,293)` | 86482 |
| A Friend Indeed | 400 | 3 | `(163,114)` | `(478,348)` | 121879 |
| A Friend Indeed | 400 | 4 | `(19,149)` | `(369,528)` | 246785 |
| A Friend Indeed | 400 | 5 | `(50,171)` | `(422,519)` | 241861 |

Die maschinenlesbaren Beobachtungen stehen in
`MapParser.Tests/Fixtures/MapKeepAnchorVectors.json`. Die Tests erzeugen daraus
synthetische Section-1013-Datensätze und prüfen alle vier World Sizes.

Ein zusätzlicher read-only Corpus-Scan erfasste 238 installierte Karten. Unter
den neun als Skirmish gekennzeichneten Dateien waren sieben über Section 1013
eindeutig auswertbar; alle 27 gefundenen Startmarker hatten Typ 41.
`Conquest.map` und `TheJordanValley.map` besitzen keine Section 1013 und werden
deshalb bewusst als `NotEvaluable/BuildingSectionMissing` gemeldet.

## Offline-API

`MapDocument.ReadKeepAnchors()` gibt acht `MapKeepAnchorResult`-Einträge zurück.
Ein Ergebnis mit `Status == Exact` besitzt immer:

- einen auswählbaren Slot,
- genau einen lebenden Keep-Datensatz desselben Owners,
- eine gültige native Koordinate innerhalb der World-Size-Raute,
- und eine daraus berechnete exakte `TileId`.

Alle anderen Fälle liefern `NotEvaluable` mit einem expliziten
`MapKeepAnchorFailureKind`. Abgedeckt sind nicht auswählbare oder inkonsistente
U4-Slots, fehlende beziehungsweise nicht verfügbare Sections, falsche
Sectionlänge, nicht unterstützte Geometrie, fehlende oder mehrdeutige
Keep-Datensätze sowie ungültige oder außerhalb der Welt liegende Koordinaten.
Es gibt bewusst keine Radar-Näherung als Fallback.

Die CLI-Ausgabe kann separat geprüft werden mit:

    MapParser keeps <file.map>

Chat 5 darf nur `Coordinate` eines `Exact`-Ergebnisses als AIV-Weltanker
verwenden.
