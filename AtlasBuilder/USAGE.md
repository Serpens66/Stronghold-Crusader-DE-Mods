# SHCDE Atlas Builder

## Deutsch

`AtlasBuilder.exe` erzeugt Sprite-Atlanten im Format der Asset-API des SHCDE Script Extenders 2.3.0. Die portable Ausgabe benötigt kein installiertes Python.

### Schnellstart

1. `AtlasBuilder.exe` starten und den Ordner `Stronghold Crusader Definitive Edition_Data` auswählen.
2. Einen neuen, leeren Modordner festlegen oder einen vorhandenen Modordner auswählen.
3. Eine GM-Gruppe hinzufügen. Der Name bestimmt die Ziel-Sprites und deren Schreibweise; die Quelldateien dürfen ein anderes Präfix verwenden.
4. Farbordner, Maskenmodus und die passende Pivotquelle festlegen.
5. Das Projekt als `*.atlas-project.json` speichern, prüfen und anschließend die Atlanten erzeugen.

Das Quellpräfix `auto` erkennt den Text vor dem abschließenden numerischen Index. Akzeptiert werden beispielsweise `Tree_Oak-12.png`, `tile_ruins 012.png`, `012.png` und `body_name-12x.png`. Im selben Ordner müssen Masken auf `_m.png` enden. In einem separaten Maskenordner ist `_m` optional.

Die Ausgabe einer Gruppe besteht aus:

    Override/Atlas/<GM-Gruppe>/atlas.png
    Override/Atlas/<GM-Gruppe>/atlas_m.png   (nur mit Masken)
    Override/Atlas/<GM-Gruppe>/atlas.json

Der Builder übernimmt Zielnamen und Pixels-per-Unit direkt aus den installierten SHCDE-Sprite-Metadaten. Er trimmt und rotiert keine Bilder. Vorhandene Atlasgruppen werden nur nach Bestätigung ersetzt; ein vorhandenes `info.json` wird nie überschrieben.

### Pivotquelle und korrekte Ausrichtung

Ein Unity-Pivot ist normalisiert. Derselbe Wert bezeichnet deshalb auf unterschiedlich großen Bildern einen anderen Pixel. Das kann Bodenplatten auseinanderziehen oder Gebäude- und Animationsteile gegeneinander verschieben.

- **SHCDE-Pixelanker beibehalten** ist der Standard und für eigene Ersatzbilder normalerweise richtig. Der Builder berechnet den Pixelanker des SHCDE-Ziels und normalisiert ihn für die tatsächliche Größe des Ersatzbildes neu.
- **Pivot aus Quellmetadaten** reproduziert die ursprüngliche Ausrichtung eines mit AssetRipper extrahierten Sprites. Wähle zusätzlich den Ordner, der die einzelnen Sprite-JSONs enthält, oder einen übergeordneten AssetRipper-Exportordner. Der Builder liest nur JSON-Dateien, deren Namen zur gewählten Gruppe und zu den vorhandenen Frames passen.
- **Normalisierten SHCDE-Pivot übernehmen (Legacy)** bildet das Verhalten älterer Builder-Projekte nach. Verwende es nur, wenn Quell- und Zielbilder dieselbe Leinwand besitzen oder die normalisierten Pivots absichtlich identisch sein sollen.

Bei den geprüften SH1DE-Gruppen `tile_land8`, `tile_buildings1`, `tile_churches` und `tile_ruins` liegt der originale Pixelanker durchgehend bei `(32, 16,5)` und die PPU bei 64. Beispielsweise ergeben sowohl Pivot `(0,5; 0,40243897)` auf 64×41 Pixeln als auch `(0,5; 0,08418399)` auf 64×196 Pixeln denselben Anker. Schwarze Spalten zwischen Tiles sind ein typisches Zeichen dafür, dass stattdessen ein normalisierter Pivot von einer anders großen Leinwand kopiert wurde.

Schema-1-Projekte werden kompatibel im Legacy-Modus geöffnet und beim Öffnen gewarnt. Nach Auswahl des gewünschten Modus werden sie beim Speichern als Schema 2 abgelegt.

### Wichtige Extender-2.3.0-Grenzen

- Ein Atlas ohne Maske ist zulässig und für Plain-Gruppen wie `tile_ruins` richtig.
- Bei Teilatlanten verkürzt 2.3.0 das Zielarray bis zum höchsten enthaltenen Index. Höhere Vanilla-Frames gehen deshalb verloren; der Builder warnt davor.
- Mit einer Maske erzeugt 2.3.0 immer TeamColour-Material. Foliage kann dadurch falsch dargestellt werden, und der Materialtyp ist modseitig nicht konfigurierbar.
- Der Builder behebt diese Extender-Fehler nicht und mischt keine Vanilla-Sprites als versteckten Workaround ein.

`*.last-build.txt` neben der Projektdatei enthält das Ergebnis oder die vollständigen technischen Fehlerdetails des letzten Prüf-/Buildlaufs.

## English

`AtlasBuilder.exe` creates sprite atlases for the SHCDE Script Extender 2.3.0 Asset API. The portable package does not require Python to be installed.

### Quick start

1. Start `AtlasBuilder.exe` and select `Stronghold Crusader Definitive Edition_Data`.
2. Select a new empty mod directory or an existing mod directory.
3. Add a GM group. Its name determines the target sprites and exact target naming; source files may use a different prefix.
4. Select the colour directory, mask mode and appropriate pivot source.
5. Save the project as `*.atlas-project.json`, validate it, then build the atlases.

The `auto` source prefix detects the text before the trailing numeric index. Examples include `Tree_Oak-12.png`, `tile_ruins 012.png`, `012.png` and `body_name-12x.png`. Masks in the colour directory must end in `_m.png`; `_m` is optional in a separate mask directory.

Each group produces:

    Override/Atlas/<GM group>/atlas.png
    Override/Atlas/<GM group>/atlas_m.png   (masks only)
    Override/Atlas/<GM group>/atlas.json

The builder obtains exact target names and pixels per unit from the installed SHCDE Sprite metadata. Images are never trimmed or rotated. Existing atlas groups are replaced only after confirmation; an existing `info.json` is never overwritten.

### Pivot source and correct alignment

A Unity pivot is normalized, so the same value points to a different pixel on images with different dimensions. This can separate ground tiles or shift building and animation parts relative to each other.

- **Preserve SHCDE pixel anchor** is the default and is normally correct for custom replacements. The builder calculates the SHCDE target's pixel anchor and normalizes it for the replacement image's actual dimensions.
- **Pivot from source metadata** reproduces the original alignment of a Sprite extracted with AssetRipper. Also select the directory containing the individual Sprite JSON files, or a parent AssetRipper export directory. Only JSON files whose names match the selected group and present frames are read.
- **Copy normalized SHCDE pivot (legacy)** preserves the behavior of older builder projects. Use it only when source and target images have identical canvases or intentionally share normalized pivots.

In the verified SH1DE groups `tile_land8`, `tile_buildings1`, `tile_churches` and `tile_ruins`, the original pixel anchor is consistently `(32, 16.5)` with 64 PPU. For example, pivot `(0.5, 0.40243897)` on a 64×41 image and `(0.5, 0.08418399)` on a 64×196 image both produce the same anchor. Black gaps between tiles are a typical symptom of copying a normalized pivot from a differently sized canvas.

Schema-1 projects open compatibly in legacy mode and display a warning. After selecting the desired mode, saving upgrades them to schema 2.

### Important Script Extender 2.3.0 limitations

- An atlas without masks is valid and correct for Plain groups such as `tile_ruins`.
- For partial atlases, 2.3.0 truncates the target array after the highest included index. Higher vanilla frames are lost, so the builder displays a warning.
- With a mask, 2.3.0 always creates TeamColour materials. Foliage may therefore render incorrectly, and mods cannot configure the material type.
- The builder does not patch these Extender defects and does not mix in vanilla sprites as a hidden workaround.

The `*.last-build.txt` file next to the project stores the result or complete technical error details from the latest validation/build operation.
