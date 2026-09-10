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

Ein Pivot von exakt `1,0` ist eine Kantenkonvention: Er bleibt bei einer anders großen Leinwand `1,0`, damit die obere beziehungsweise rechte Kante fest bleibt. Pivots außerhalb von `0..1` sind ebenfalls gültig und kommen in SHCDE tatsächlich vor; der Builder prüft daher den berechneten Pixelanker statt den Pivot künstlich auf diesen Bereich zu begrenzen.

Bei Quellmetadaten bleiben normalisierte Pivots bei einer proportional skalierten Leinwand erhalten. Ändert sich nur die Leinwand beziehungsweise ihr Seitenverhältnis, bewahrt der Builder den absoluten Quellanker. Eine Warnung weist auf mögliches Trimming hin, weil dessen korrekter Ausgleich nicht allein aus Rect und PNG-Größe ableitbar ist.

Bei den geprüften SH1DE-Gruppen `tile_land8`, `tile_buildings1`, `tile_churches` und `tile_ruins` liegt der originale Pixelanker durchgehend bei `(32, 16,5)` und die PPU bei 64. Beispielsweise ergeben sowohl Pivot `(0,5; 0,40243897)` auf 64×41 Pixeln als auch `(0,5; 0,08418399)` auf 64×196 Pixeln denselben Anker. Schwarze Spalten zwischen Tiles sind ein typisches Zeichen dafür, dass stattdessen ein normalisierter Pivot von einer anders großen Leinwand kopiert wurde.

Schema-1-Projekte werden kompatibel im Legacy-Modus geöffnet und beim Öffnen gewarnt. Schema-2-Projekte behalten ihre bisherige strikte Zielprüfung. Beim nächsten Speichern werden ältere Projekte als Schema 3 abgelegt.

### In SHCDE fehlende Zielslots

Normalerweise muss **In SHCDE fehlende Zielslots** auf **Ablehnen** stehen. Manche vom Spiel deklarierten Arrays enthalten jedoch echte leere Slots. Bestätigtes Beispiel: `body_swordsman` besitzt den Bereich `0–1087`, aber SHCDE enthält keine Sprites für `416–447`, während SH1DE diese 32 Frames besitzt.

Nur für einen belegten Fall kann pro Gruppe **Aus validierten Quellmetadaten ergänzen** gewählt werden. Das ist ausschließlich zusammen mit **Pivot aus Quellmetadaten** möglich. Der Builder prüft dann Quelldateiname, `m_Name`, Präfix, Index, Alt-Suffix, Rect, Pivot und PPU, lässt keine Nummer außerhalb des vom SHCDE-Loader deklarierten Arrays zu und ergänzt nur tatsächlich fehlende Zieldaten. Vorhandene SHCDE-Metadaten haben immer Vorrang.

Der ausgegebene Spritename wird aus der Ziel-GM-Gruppe erzeugt. Das ist wichtig, wenn das Quellpräfix absichtlich anders lautet: Der Script Extender erkennt nur Zielnamen wie `body_swordsman-416`, nicht einen beliebigen Namen aus dem Quellspiel. Eine deutliche Buildwarnung nennt Anzahl und Bereiche aller ergänzten Slots.

### Wichtige Extender-2.3.0-Grenzen

- Der Builder kennt den Material- und Maskenvertrag aller 195 registrierten Gruppen. Plain-Gruppen wie `tile_ruins` müssen ohne Maske gebaut werden; TeamColour- und Foliage-Gruppen benötigen eine vollständige Maske.
- `tile_sea_new_01` und `tile_sea_shore` teilen sich dasselbe Sprite-Array mit Offset und Zusatzspeicher. Der Atlas-Loader berücksichtigt das nicht; der Builder sperrt deshalb Atlas-Ausgaben für beide Gruppen und verweist auf Einzelsprite-Overrides.
- Bei Teilatlanten verkürzt 2.3.0 das Zielarray bis zum höchsten enthaltenen Index. Höhere Vanilla-Frames gehen deshalb verloren; der Builder warnt davor.
- Auch Lücken unterhalb des höchsten Index werden gemeldet, weil erhaltene Vanilla-Sprites mit der neuen Atlasmaske falsch aussehen können.
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

A pivot of exactly `1.0` is an edge convention: it remains `1.0` on a differently sized canvas so the top or right edge stays fixed. Pivots outside `0..1` are valid as well and occur in SHCDE itself; the builder therefore validates the calculated pixel anchor instead of artificially restricting the normalized value.

With source metadata, normalized pivots remain unchanged when the canvas is scaled proportionally. If only the canvas or its aspect ratio changes, the builder preserves the absolute source anchor. A warning identifies possible trimming because its correct compensation cannot be inferred from the Rect and PNG dimensions alone.

In the verified SH1DE groups `tile_land8`, `tile_buildings1`, `tile_churches` and `tile_ruins`, the original pixel anchor is consistently `(32, 16.5)` with 64 PPU. For example, pivot `(0.5, 0.40243897)` on a 64×41 image and `(0.5, 0.08418399)` on a 64×196 image both produce the same anchor. Black gaps between tiles are a typical symptom of copying a normalized pivot from a differently sized canvas.

Schema-1 projects open compatibly in legacy mode and display a warning. Schema-2 projects retain their previous strict target validation. Saving an older project upgrades it to schema 3.

### Target slots absent from SHCDE

Normally, **Target slots absent from SHCDE** must remain set to **Reject**. Some arrays declared by the game contain genuine empty slots. Confirmed example: `body_swordsman` declares the range `0–1087`, but SHCDE has no Sprites for `416–447`, while SH1DE contains those 32 frames.

For a verified case only, select **Fill from validated source metadata** for that group. This option is available exclusively with **Pivot from source metadata**. The builder then validates the source filename, `m_Name`, prefix, index, alternate suffix, Rect, pivot and PPU, refuses indices outside the array declared by the SHCDE loader, and fills only target metadata that is genuinely absent. Existing SHCDE metadata always takes precedence.

The emitted Sprite name is constructed from the target GM group. This matters when the source prefix intentionally differs: the Script Extender recognizes target names such as `body_swordsman-416`, not an arbitrary source-game name. A prominent build warning lists the number and ranges of all filled slots.

### Important Script Extender 2.3.0 limitations

- The builder knows the material and mask contract of all 195 registered groups. Plain groups such as `tile_ruins` must be built without a mask; TeamColour and Foliage groups require a complete mask.
- `tile_sea_new_01` and `tile_sea_shore` share one Sprite array through an offset and additional storage. The atlas loader ignores that contract, so the builder blocks atlas output for both groups and directs users to individual Sprite overrides.
- For partial atlases, 2.3.0 truncates the target array after the highest included index. Higher vanilla frames are lost, so the builder displays a warning.
- Gaps below the highest index are also reported because preserved Vanilla Sprites can render incorrectly with the replacement atlas mask.
- With a mask, 2.3.0 always creates TeamColour materials. Foliage may therefore render incorrectly, and mods cannot configure the material type.
- The builder does not patch these Extender defects and does not mix in vanilla sprites as a hidden workaround.

The `*.last-build.txt` file next to the project stores the result or complete technical error details from the latest validation/build operation.
