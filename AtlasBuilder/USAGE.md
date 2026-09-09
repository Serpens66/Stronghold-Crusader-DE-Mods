# SHCDE Atlas Builder

## Deutsch

`AtlasBuilder.exe` erzeugt Sprite-Atlanten im Format der Asset-API des SHCDE Script Extenders 2.3.0. Die portable Ausgabe benötigt kein installiertes Python.

### Schnellstart

1. `AtlasBuilder.exe` starten und den Ordner `Stronghold Crusader Definitive Edition_Data` auswählen.
2. Einen neuen, leeren Modordner festlegen oder einen vorhandenen Modordner auswählen.
3. Eine GM-Gruppe hinzufügen. Der Name bestimmt die Ziel-Sprites und deren Schreibweise; die Quelldateien dürfen ein anderes Präfix verwenden.
4. Farbordner, Maskenmodus und bei Bedarf einen separaten Maskenordner festlegen.
5. Das Projekt als `*.atlas-project.json` speichern, prüfen und anschließend die Atlanten erzeugen.

Das Quellpräfix `auto` erkennt den Text vor dem abschließenden numerischen Index. Akzeptiert werden beispielsweise `Tree_Oak-12.png`, `tile_ruins 012.png`, `012.png` und `body_name-12x.png`. Im selben Ordner müssen Masken auf `_m.png` enden. In einem separaten Maskenordner ist `_m` optional.

Die Ausgabe einer Gruppe besteht aus:

    Override/Atlas/<GM-Gruppe>/atlas.png
    Override/Atlas/<GM-Gruppe>/atlas_m.png   (nur mit Masken)
    Override/Atlas/<GM-Gruppe>/atlas.json

Der Builder übernimmt Zielnamen, normalisierte Pivots und Pixels-per-Unit direkt aus den installierten SHCDE-Sprite-Metadaten. Er trimmt und rotiert keine Bilder. Vorhandene Atlasgruppen werden nur nach Bestätigung ersetzt; ein vorhandenes `info.json` wird nie überschrieben.

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
4. Select the colour directory, mask mode and, if applicable, a separate mask directory.
5. Save the project as `*.atlas-project.json`, validate it, then build the atlases.

The `auto` source prefix detects the text before the trailing numeric index. Examples include `Tree_Oak-12.png`, `tile_ruins 012.png`, `012.png` and `body_name-12x.png`. Masks in the colour directory must end in `_m.png`; `_m` is optional in a separate mask directory.

Each group produces:

    Override/Atlas/<GM group>/atlas.png
    Override/Atlas/<GM group>/atlas_m.png   (masks only)
    Override/Atlas/<GM group>/atlas.json

The builder obtains exact target names, normalized pivots and pixels per unit from the installed SHCDE Sprite metadata. Images are never trimmed or rotated. Existing atlas groups are replaced only after confirmation; an existing `info.json` is never overwritten.

### Important Script Extender 2.3.0 limitations

- An atlas without masks is valid and correct for Plain groups such as `tile_ruins`.
- For partial atlases, 2.3.0 truncates the target array after the highest included index. Higher vanilla frames are lost, so the builder displays a warning.
- With a mask, 2.3.0 always creates TeamColour materials. Foliage may therefore render incorrectly, and mods cannot configure the material type.
- The builder does not patch these Extender defects and does not mix in vanilla sprites as a hidden workaround.

The `*.last-build.txt` file next to the project stores the result or complete technical error details from the latest validation/build operation.
