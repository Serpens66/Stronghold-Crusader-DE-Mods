# Stronghold Crusader DE AIV Parser

`AIVParser` reads the Definitive Edition `.aivjson` castle blueprint format. It preserves
the original build order while resolving mapper names, the keep anchor, 100x100 grid
coordinates, unit slots, pauses, rotations, and keep-relative placement deltas.
Known DE buildings also expose their square tile footprint, stored placement anchor,
rotated bounds, anchor delta from the keep, and separately modeled
associated blocked areas.

The tool does not modify AIV files or game state. HD `.aiv` containers and DE `.baiv`
raw data are intentionally outside V1; the local Script Extender already provides the
`SHCDESE.AIVDecoder` converter for `.baiv`.

## Build and test

For a normal Windows build, double-click `build.bat`. It creates a Release build,
runs all automated tests, and shows the location of the finished parser. The window
stays open afterwards so that success or error messages can be read.

The equivalent developer commands are:

    dotnet build AIVParser.sln -c Release
    dotnet run --project AIVParser.Tests/AIVParser.Tests.csproj -c Release --no-build

All projects are package-free. `AIVParser.Core` targets `netstandard2.0` so a future
net481 BepInEx plugin can reference it. Its DTOs use public fields with the game's
original spellings and can be populated by Unity `JsonUtility`.

## Schnellstart für Nicht-Programmierer

### 1. Parser einmal bauen

1. Öffne den Ordner `AIVParser`.
2. Starte `build.bat` mit einem Doppelklick.
3. Warte auf die Meldung `Build und Tests waren erfolgreich`.
4. Drücke eine Taste, um das Fenster zu schließen.

Dafür muss das .NET 10 SDK installiert sein. Falls es fehlt, meldet `build.bat` dies,
ohne Dateien im Spielordner zu verändern. Nach einem erfolgreichen Build befindet
sich das ausführbare Programm hier:

    AIVParser.Cli\bin\Release\net10.0\AIVParser.exe

Der Parser ist ein Offline-Werkzeug. Er verändert weder die ausgewählte `.aivjson`
noch das Spiel und muss nicht in einen BepInEx-Ordner kopiert werden.

### 2. Eine AIV-Datei auswählen

Die DE-AIVs befinden sich üblicherweise in einem dieser Ordner:

    C:\Users\<Benutzername>\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\CustomLords
    C:\Users\<Benutzername>\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\ExtendedLords
    Steam\steamapps\common\Stronghold Crusader Definitive Edition - Castle & CPU Lord Editor\CrusaderCastleEditorUnity_Data\StreamingAssets\Villages

Suche dort die gewünschte Datei mit der Endung `.aivjson`. Für die folgenden Befehle
wird ihr vollständiger Pfad benötigt. Am einfachsten lässt er sich im Windows Explorer
über `Als Pfad kopieren` übernehmen.

### 3. SVG und lesbares JSON erzeugen

Öffne den Ordner `AIVParser` im Windows Explorer. Klicke mit der rechten Maustaste auf
eine freie Stelle und wähle `Im Terminal öffnen`. Führe anschließend diesen Befehl aus:

    .\AIVParser.Cli\bin\Release\net10.0\AIVParser.exe inspect "C:\vollständiger\Pfad\burg.aivjson"

Ersetze nur den Pfad innerhalb der Anführungszeichen. Die Anführungszeichen sind
wichtig, weil die Stronghold-Ordner Leerzeichen enthalten.

Bei Erfolg entsteht im Ordner `AIVParser-output`:

- `burg.svg`: die grafische 100×100-Burgansicht; sie kann beispielsweise mit Edge,
  Firefox oder Chrome geöffnet werden
- `burg.parsed.json`: eine ausführlichere, semantisch benannte Darstellung für weitere
  Werkzeuge oder die spätere Spawn-Implementierung

Die ursprüngliche `.aivjson` bleibt unverändert.

### Ansicht drehen

Mit `--rotation` kann die Vorschau in 90-Grad-Schritten gedreht werden:

    .\AIVParser.Cli\bin\Release\net10.0\AIVParser.exe inspect "C:\vollständiger\Pfad\burg.aivjson" --rotation 90

Erlaubt sind `0`, `90`, `180` und `270`. Ohne Angabe wird `0` verwendet. Falls die
Vorschau gegenüber dem offiziellen Editor gedreht erscheint, kann so die passende
Ansicht gewählt werden.

### Anderen Ausgabeordner verwenden

Mit `-o` lässt sich der Zielordner selbst festlegen:

    .\AIVParser.Cli\bin\Release\net10.0\AIVParser.exe inspect "C:\vollständiger\Pfad\burg.aivjson" -o "D:\Meine AIV Vorschauen"

Ein noch nicht vorhandener Ausgabeordner wird automatisch angelegt.

### AIVs nur überprüfen

`validate` prüft eine einzelne `.aivjson` oder rekursiv einen ganzen Ordner, erzeugt
aber keine SVG:

    .\AIVParser.Cli\bin\Release\net10.0\AIVParser.exe validate "C:\vollständiger\Pfad\ExtendedLords"

Am Ende zeigt `Summary`, wie viele Dateien gültig oder fehlerhaft waren. Meldungen mit
`WARN` weisen auf auffällige, aber lesbare Daten hin; `ERROR` kennzeichnet eine Datei,
die nicht korrekt interpretiert werden konnte.

## Validate files

    dotnet run --project AIVParser.Cli/AIVParser.Cli.csproj -c Release -- validate "C:\path\to\ExtendedLords"

Directories are searched recursively for `*.aivjson`. Validation writes no files.
Exit code `0` means all files are structurally valid, `1` means at least one invalid
file, and `2` indicates usage or I/O failure.

## Inspect one castle

    dotnet run --project AIVParser.Cli/AIVParser.Cli.csproj -c Release -- inspect "C:\path\castle.aivjson" --rotation 90

Inspection writes `<name>.parsed.json` and `<name>.svg` to `AIVParser-output` in the
current directory. Use `-o <directory>` to choose another output folder. The SVG is
self-contained and every placement has a tooltip with its frame, type, source offset,
grid coordinates, footprint, stored anchor, and pause flag. Building colors retain the
official editor's broad visual groups while traps and path types remain distinct.
Green cross-hatching marks associated blocked areas from the native AIV loader,
including all five Keep reservations, the mapper-79/86/87 yards, the mapper-88/89
yard, and the Oil Smelter yard.

## Verwendung in einem BepInEx-Mod

`AIVParser.Core` wurde absichtlich getrennt von der `net10.0`-Kommandozeilenanwendung
gehalten. Die Core-Bibliothek zielt auf `netstandard2.0` und kann deshalb von einem
`net481`-BepInEx-Mod referenziert werden. Die CLI und `System.Text.Json` werden im
Spiel nicht benötigt.

Dieser Abschnitt beschreibt den voraussichtlichen Ablauf für einen späteren
Burg-Spawner. Der Parser liefert bereits Baufolge, Mapper, AIV-Koordinaten,
Gebäudegrößen und Rotation. Er erzeugt derzeit aber selbst keine Spielobjekte.
Insbesondere die endgültige Umrechnung von AIV-Row/Column auf die lokalen
Spiel-Tile-X/Y-Koordinaten muss noch im Spiel verifiziert werden.

### Core-Bibliothek referenzieren

Während der Entwicklung kann das Mod-Projekt direkt auf das Core-Projekt verweisen:

    <ItemGroup>
      <ProjectReference Include="..\AIVParser\AIVParser.Core\AIVParser.Core.csproj" />
    </ItemGroup>

Alternativ kann nach dem AIVParser-Build
`AIVParser.Core\bin\Release\netstandard2.0\AIVParser.Core.dll` als normale
Assembly-Referenz eingebunden werden. Beim Verteilen des Mods muss
`AIVParser.Core.dll` neben der Plugin-DLL im BepInEx-Pluginordner liegen.

### AIV im Mod laden und parsen

Die JSON-DTOs besitzen öffentliche Felder mit den originalen Namen des Spiels.
Dadurch kann Unitys bereits vorhandenes `JsonUtility` verwendet werden:

    using System.IO;
    using AIVParser.Core;
    using UnityEngine;

    string aivPath = @"C:\Pfad\zur\burg.aivjson";
    string json = File.ReadAllText(aivPath);
    AivJsonDocument document =
        JsonUtility.FromJson<AivJsonDocument>(json);

    AivParseResult result =
        new AivBlueprintParser().Parse(document, aivPath);

    if (!result.IsValid)
    {
        foreach (AivDiagnostic diagnostic in result.Diagnostics)
        {
            // Im echten Mod mit Zeitstempel über den BepInEx-Logger ausgeben.
            Logger.LogError(
                diagnostic.Code + " " +
                diagnostic.Location + ": " +
                diagnostic.Message);
        }

        return;
    }

Nach erfolgreichem Parsen steht die Burg in `result.Blueprint`. Relevant sind vor
allem:

- `KeepAnchor`: gespeicherter AIV-Anker des Keeps
- `Frames`: Baufolge in der Reihenfolge der AIV
- `AivBuildFrame.Mapper`: semantischer Mapper samt Kategorie und Gebäudegröße
- `AivBuildFrame.Positions`: ein oder mehrere Platzierungspunkte des Frames
- `ShouldPause` und `PauseDelayAmount`: Pausen der ursprünglichen Baufolge
- `MiscItems`: Einheitenplätze, Feuerstellen und Flaggen mit erhaltenen Slotnummern

Unbekannte Mapper bleiben im Ergebnis erhalten. Ein Spawner sollte sie anhand von
`frame.Mapper.IsKnown` erkennen, protokollieren und überspringen, anstatt einen
unbekannten Wert blind an das Spiel zu übergeben.

### AIV relativ zu einem Weltanker ausrichten

Als Weltanker bietet sich der tatsächliche Begin-Tile des bereits vorhandenen
Keep-Gebäudes an. `GamePlayerManagerAPI.GetPlayerKeepPosition(...)` sollte dafür
nicht allein als Wahrheit verwendet werden. In der funktionierenden
SpawnCastle-Integration wird das Keep in
`GameBuildingManagerAPI.Instance.GetBuildingsAsSpan()` über Besitzer und
`STRUCT_KEEP_ONE` bis `STRUCT_KEEP_FIVE` gesucht. Dabei werden sowohl
`AliveState.NeedsInit` als auch `AliveState.IsAlive` zugelassen, weil das Keep beim
frühen Kartenstart noch initialisiert werden kann.

Der bestätigte Weltanker ist anschließend
`r_TilePositionXBegin/r_TilePositionYBegin` dieses realen Keep-Gebäudes. Für jeden
AIV-Punkt berechnet der Parser zunächst die Differenz zum AIV-Keep:

    AivRotation rotation = AivRotation.Degrees0;
    AivGridPoint aivKeep = result.Blueprint.KeepAnchor.Value;

    foreach (AivBuildFrame frame in result.Blueprint.Frames)
    {
        foreach (AivGridPoint point in frame.Positions)
        {
            AivGridDelta delta =
                AivGridTransform.GetAnchorDelta(
                    point,
                    aivKeep,
                    rotation);

            int tileX = realKeepBeginX + delta.Column;
            int tileY = realKeepBeginY - delta.Row;
        }
    }

Diese Zuordnung wurde mit einer tatsächlich gespawnten AIV-Burg für
`AivRotation.Degrees0` bestätigt: AIV-Spalten laufen in Welt-X-Richtung, AIV-Zeilen
laufen invertiert zur Welt-Y-Richtung. Der Parser selbst bleibt absichtlich
spielunabhängig und gibt weiterhin nur AIV-Punkte und Ankerdifferenzen zurück; die
obige Abbildung gehört in den Spieladapter des jeweiligen Mods.

Die gewählte Rotation gilt immer gemeinsam für AIV und realen Startkomplex. Dazu
gehören der Keep, das 5×5-Vorratslager und weitere an den Spielerstart gekoppelte
Gebäude. Ein bereits vorhandener Keep darf deshalb nur dann als Weltanker dienen
und im AIV-Frame übersprungen werden, wenn der vorhandene Startkomplex bereits
dieselbe Orientierung besitzt. Bei abweichender Orientierung muss der
Startkomplex passend rekonstruiert werden; eine vom Keep unabhängige AIV-Rotation
entspricht nicht dem nativen Kartenstart. Bei einer vollständig neuen Burg wird
der Keep-Frame am gewählten Weltanker mit derselben Rotation erzeugt.

### Normale Gebäude erzeugen

Für normale Gebäude und den Keep ist
`GameBuildingManagerAPI.CreatePrefab(...)` die vorgesehene Script-Extender-API.
Sie erzeugt das vollständige Spielgebäude einschließlich der zugehörigen
Tile-, Visual- und Interaktionsregistrierungen. Der prinzipielle Teil eines
Spawners sieht nach der verifizierten Koordinatenumrechnung so aus:

    using AIVParser.Core;
    using SHCDESE.API;
    using SHCDESE.Interop;

    if (frame.Mapper.Category == AivItemCategory.Building ||
        frame.Mapper.Category == AivItemCategory.Keep)
    {
        eMappers mapper = (eMappers)frame.Mapper.Value;
        int scale = BuildingScales.GetScale(mapper);

        foreach (AivGridPoint point in frame.Positions)
        {
            AivGridDelta delta =
                AivGridTransform.GetAnchorDelta(
                    point,
                    result.Blueprint.KeepAnchor.Value,
                    rotation);

            int tileX = realKeepBeginX + delta.Column;
            int tileY = realKeepBeginY - delta.Row;

            GameBuildingManagerAPI.Instance.CreatePrefab(
                playerId,
                tileX,
                tileY,
                mapper,
                scale,
                0,
                true,
                bypassPlacementRules: true);
        }
    }

`bIsFree: true` verhindert Ressourcenkosten beim Wiederherstellen einer fertigen
AIV-Burg. `bypassPlacementRules: true` ist für das Wiederherstellen einer bereits
validierten fertigen Burg sinnvoll, darf aber erst nach einer eigenen Kartenrand-
und Footprint-Überlappungsprüfung verwendet werden. Diese Prüfung muss das reale
Keep mit dessen `r_OccupyTileGridSize` einschließen. Andernfalls kann das Spiel
ungültige oder überlappende Platzierungen akzeptieren.

Der Rückgabewert von `CreatePrefab(...)` sollte nicht als zuverlässige Gebäude-ID
behandelt werden. Falls der Mod die erzeugten IDs benötigt, sollte er sie synchron
über `BuildingR3EventHooks.OnBuildingSpawn` in der `Post`-Phase erfassen.

### Mauern, Pech, Gräben und andere AIV-Typen

Nicht jeder AIV-Frame ist ein normales Gebäude. Der Spawner sollte anhand von
`frame.Mapper.Category` getrennte Wege verwenden:

- normale Gebäude und Keep: `GameBuildingManagerAPI.CreatePrefab(...)`
- hohe und niedrige Mauerpfade: benachbarte Punkte zu Segmenten gruppieren und
  `GameBuildingManagerAPI.CreateWall(...)` verwenden
- Pechgrabenpunkte: `GamePitchManagerAPI.CreatePitch(...)`
- Zinnen, Treppen, Wassergräben und Fallen: nicht blind über `CreatePrefab(...)`
  erzeugen; die passende native beziehungsweise Script-Extender-API muss je Kategorie
  noch verifiziert werden
- `MiscItems`: getrennt nach normaler Einheit, Feuerstelle und Flagge behandeln;
  Einheitenslots erst nach den Gebäuden beziehungsweise Türmen erzeugen

`AivBlockedAreaCatalog` beschreibt zusätzliche reservierte oder blockierte Flächen,
zum Beispiel den Hof der Barracks oder das Keep-Lagerfeuer. Diese Flächen sind
Hilfsdaten für Platzierungs- und Überlappungsprüfungen und keine zusätzlichen
Gebäude, die einzeln gespawnt werden sollen.

### Baufolge und sicherer Zeitpunkt

Die Frames sollten in `BuildIndex`-Reihenfolge verarbeitet werden. Für eine sofort
fertige Burg können die AIV-Pausen bewusst ignoriert werden. Soll die originale
Baufolge sichtbar nachgestellt werden, müssen `ShouldPause` und
`PauseDelayAmount` in einem persistenten Laufzeitobjekt berücksichtigt werden.

Die Integration sollte erst nach `CrusaderLibrary.Instance.LibraryLoaded` ihre
Script-Extender-Hooks registrieren und nur auf einer vollständig gestarteten Karte
spawnen. Sie darf nicht dauerhaft von `Update()`, Coroutines oder `OnDestroy()` der
kurzlebigen `BaseUnityPlugin`-Komponente abhängen. Ein direkt in
`OnStartMap(Post)` angelegter verzögerter `TimerEngine`-Auftrag kann beim weiteren
Laden der Kartendaten verloren gehen. Für verzögertes Spawning ist deshalb ein
persistenter Tick-/Hook-Mechanismus mit eigener Zeitmessung geeigneter.

Vor dem Spawn sollte der Mod mindestens Spieler-ID, Kartenstatus, Zielanker,
Kartenränder, unbekannte Mapper und Footprint-Überlappungen prüfen. Außerdem sollte
jeder geplante und tatsächlich ausgeführte Spawn mit Zeitstempel protokolliert
werden. Damit lassen sich falsche Achsen, ausgelassene Frames oder durch
Platzierungsregeln abgelehnte Gebäude nachvollziehen.

## Coordinate contract

The parser itself remains independent from game-world coordinates:

- `Row = encodedOffset / 100`
- `Column = encodedOffset % 100`
- 90-degree rotation maps `(row, column)` to `(column, 99-row)`
- placement deltas are calculated relative to the equally rotated keep
- a building offset is the editor-space upper-left corner of its `size x size`
  footprint; the footprint extends towards smaller rows and larger columns
- the stored anchor remains available for later world placement
- the verified SHCDE adapter for zero-degree rotation maps
  `worldX = realKeepBeginX + delta.Column` and
  `worldY = realKeepBeginY - delta.Row`
- `realKeepBegin` must come from the actual owned keep building, not solely from
  the player-resource keep position

The SVG draws rows upward, matching the official AIV editor. This affects only screen
Y; exported row/column values and rotation math remain unchanged.

Footprint sizes are based on the DE Script Extender's `BuildingScales` table and were
cross-checked against Sourcehold's AIV JSON import logic. The DE table is preferred
where older HD values differ, notably for barracks and several other buildings.

The additional barracks, Engineers Guild, and Oil Smelter reservations use the
offset table found unchanged in the local DE native binary and in the reversed HD
`TerrainDefinedData`. The centered 5x5 keep-campfire area is marked separately as
editor-derived in semantic JSON, so a later spawner can distinguish visual placement
guidance from the confirmed native reservation table.

This retains all information needed by a later spawner while leaving the final
Row/Column-to-world-axis verification explicit.
