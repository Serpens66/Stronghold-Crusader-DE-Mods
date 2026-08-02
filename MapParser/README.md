# Stronghold Crusader DE MapParser

`MapParser` ist ein eigenständiger, ausschließlich lesender Parser für
Stronghold Crusader Definitive Edition `.map`-Dateien. Er liest Metadaten und
das Section-Verzeichnis, validiert Größen und Grenzen und dekomprimiert
PKWARE-DCL-Sections erst bei Bedarf. Weder Karten noch die Spielinstallation
werden verändert.

Der Parser ist die Datengrundlage für eine spätere AIV-Platzierungsprüfung.
Vanillas Fit-Regeln und die Lobby-Integration gehören bewusst noch nicht zu
diesem Projekt.

## Projekte

- `MapParser.Core` (`netstandard2.0`): paketfreie Bibliothek und öffentliche API
- `MapParser.Cli` (`net10.0`): `info`, `list`, `keeps`, `dump` und `validate`
- `MapParser.Tests` (`net10.0`): synthetische Negativ-, DCL-, CRC- und
  Directory-Tests sowie optionale lokale Integration und Python-Parität
- `Reference`: strikt lesender Python-Referenzparser und relevante
  Kaitai-Dokumentation

## Bauen und testen

Unter Windows genügt:

    build.bat

Das Skript baut die Solution in `Release`, führt die paketfreien synthetischen
Tests aus und prüft die erzeugte EXE. Es kopiert nichts in den Spieleordner.

Die entsprechenden Entwicklerbefehle sind:

    dotnet build MapParser.sln -c Release
    dotnet run --project MapParser.Tests/MapParser.Tests.csproj -c Release --no-build

Der vollständige lokale Corpus-Test kann mit den vorhandenen Kartenordnern
explizit gestartet werden:

    dotnet run --project MapParser.Tests/MapParser.Tests.csproj -c Release --no-build -- "<StreamingAssets\Maps>" "<LocalLow\...\Maps>"

Dabei werden alle verfügbaren Sections gelesen, DCL-Ausgabelängen und CRC32
geprüft. Proprietäre Karten werden weder kopiert noch als Test-Fixtures ins
Repository aufgenommen.

## CLI

Die fertige Anwendung liegt nach dem Build unter:

    MapParser.Cli\bin\Release\net10.0\MapParser.exe

Beispiele:

    MapParser.exe info "C:\Maps\Beispiel.map"
    MapParser.exe list "C:\Maps\Beispiel.map"
    MapParser.exe keeps "C:\Maps\Beispiel.map"
    MapParser.exe dump "C:\Maps\Beispiel.map" 3003 logic.bin
    MapParser.exe validate "C:\Maps"

`dump` schreibt nur den dekomprimierten Inhalt einer Section in eine separate
Datei; die Quellkarte bleibt unverändert. Eine logische ID wie `1003` findet
auch die SCDE-Section `3003`, sofern keine Section mit exakt dieser Original-ID
vorhanden ist.

## Öffentliche Core-API

`MapFileReader.Parse(...)` besitzt Überladungen für Dateipfad, `Stream` und
`byte[]`. Der Pfad-Overload akzeptiert ausschließlich `.map`; `.sav` und `.msv`
sind absichtlich nicht unterstützt.

    MapDocument map = MapFileReader.Parse(path);
    Console.WriteLine(map.Metadata.WorldSize);
    Console.WriteLine(map.Directory?.Capacity);

    if (map.HasPlacementLayers)
    {
        MapTileLayers layers = map.ReadPlacementLayers();
        int flags = layers.TerrainFlags[tileId];
    }

    if (map.HasPlacementSnapshot)
    {
        MapPlacementSnapshot snapshot = map.ReadPlacementSnapshot();
        MapPlacementTile tile = snapshot.GetTile(x, y);
        Console.WriteLine(tile.TerrainFlags);
    }

    MapKeepAnchorResult keep = map.ReadKeepAnchors().GetSlot(slotIndex);
    if (keep.Status == MapKeepAnchorStatus.Exact)
        Console.WriteLine(keep.Coordinate);

`MapDocument` behält die ursprünglichen Dateibytes intern. `MapSectionInfo`
enthält Original-ID, logische ID, Speichertyp, Größen und Offsets; erst
`ReadContent()` löst Dekompression und CRC-Prüfung aus. Zurückgegebene Arrays
sind Kopien. Die typisierten Placement-Layer sind schreibgeschützte Listen:

- `TerrainFlags` (`int`)
- `SecondaryLogic`, `Heights`, `DefaultHeights`, `OwnerOccupancy` (`byte`)
- `Organisms`, `BuildingOccupancy`, `EntityOccupancy` (`ushort`)

`MapPlacementSnapshot` führt diese acht Roh-Layer mit der belegten
`MapTileGeometry` zusammen. Tiles können über Tile-ID oder `(x,y)` gelesen
werden; der Snapshot interpretiert dabei bewusst noch keine Flagbits. Fehlende,
nicht verfügbare oder längeninkonsistente Layer liefern eine
`MapPlacementSnapshotException` mit einem maschinenlesbaren `FailureKind`.

`ReadKeepAnchors()` liest den exakten nativen Keep-Anker jedes auswählbaren
Slots aus Section 1013. Unsichere Fälle liefern `NotEvaluable` mit einem
maschinenlesbaren `MapKeepAnchorFailureKind`; U4-Radarwerte werden nie als
Tile-Näherung ausgegeben. Der native Nachweis steht in
`Docs/MAP_KEEP_TILE_ANCHORS.md`.

Alte Tile-IDs wie `1003` und neue IDs wie `3003` werden über
`LogicalSectionId` zusammengeführt; `SectionId` bewahrt die Original-ID.

## Varianten und strikte Validierung

Normale Directory-Tags werden dynamisch ausgewertet:

- `2036` → 100 Slots
- `3036` → 150 Slots
- `4036` → 200 Slots

Die bekannten Sondertags `1076`, `2100` und `2108` liefern Metadaten, aber kein
vorgebliches Section-Verzeichnis. Ihr Rest steht als opaque Tail zur Verfügung,
`SectionsAvailable` und `HasPlacementLayers` sind `false`.

Einige ausgelieferte Karten besitzen bei Section `1190` einen widersprüchlichen
DCL-Eintrag, dessen angeblicher komprimierter Inhalt physisch ausschließlich aus
Nullpadding besteht. Diese exakt erkannte Anomalie erhält den Speichertyp
`UnavailableZeroFilledDcl`; `IsContentAvailable` ist `false` und
`ReadContent()` meldet die fehlenden Daten, statt erfundene Bytes zurückzugeben.
Alle anderen Sections derselben Karte – insbesondere die Placement-Layer –
werden normal und strikt geprüft.

Reguläre zusätzliche Daten hinter dem deklarierten Section-Payload, etwa ein
Script-Extender-ZIP-Anhang, bleiben ebenfalls als opaque Tail erhalten.

## Python-Referenz und Parität

Der Referenzparser liegt unter `Reference/scde_map_parser.py`. Er enthält keine
Schreib-, Replace-, Rewrite- oder Build-Funktionen. Sein `manifest`-Befehl dient
der automatischen Ergebnisparität:

    python Reference\scde_map_parser.py manifest "C:\Maps\Beispiel.map"

Der C#-Test-Harness kann Referenzparser und Core direkt vergleichen:

    dotnet run --project MapParser.Tests/MapParser.Tests.csproj -c Release --no-build -- --parity "<python.exe>" "Reference\scde_map_parser.py" "<map1>" "<map2>"

Verglichen werden Metadaten, Directory-Daten, Section-Inventar, Größen,
Speichertypen und SHA-256 aller verfügbaren dekomprimierten Sections.
