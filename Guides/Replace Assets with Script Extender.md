# Replace SH1DE graphics with Script Extender / SH1DE-Grafiken mit dem Script Extender ersetzen

[English](#english) | [Deutsch](#deutsch)

## English

### Result

The Script Extender atlas system can replace complete or partial unit and building groups. Main and alternate frames left out of an atlas retain their vanilla Sprites, materials and masks.

Individual AssetRipper metadata files must still be combined into one `atlas.json` per group. Partial atlases also work with named groups in shared GM arrays. Target indices beyond the vanilla range extend the array without discarding its existing entries.

Verified atlas requirements:

- Script Extender `2.4.0` or newer for PNG; `2.8.0` or newer for BC7-DDS.
- The DDS loading path has been checked against the locally installed Script Extender `2.12.0`.

---

### Required tools

#### AssetRipper

Use AssetRipper to extract Unity atlases, Sprite images and metadata from both games.

- [Latest AssetRipper release](https://github.com/AssetRipper/AssetRipper/releases/latest)
- [Direct Windows x64 download](https://github.com/AssetRipper/AssetRipper/releases/latest/download/AssetRipper_win_x64.zip)

#### SHCDE Sprite Previewer

Useful for viewing and exporting SHCDE Sprites and masks.

- [Release v1.1.5](https://gitlab.com/rawra-stronghold-crusader/shcde-sprite-previewer/-/releases/v1.1.5)
- [Direct Windows x64 download](https://gitlab.com/api/v4/projects/77768698/packages/generic/shcde-sprite-previewer/1.1.5/shcde-sprite-viewer-win-x64.zip)

The Previewer was developed for SHCDE. AssetRipper is the safer starting point for SH1DE files.

#### SHCDE Atlas Builder

The builder packs, names and validates the finished atlases. Its portable Windows package does not require an installed Python runtime.

The linked release `1.0.5` can output PNG or BC7-DDS atlases. Colour and mask formats can be selected independently; PNG is the default. DDS requires Script Extender `2.8.0` or newer.

- [Current AtlasBuilder release (1.0.5)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/AtlasBuilder_1.0.5)
- [Direct portable Windows x64 download](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/download/AtlasBuilder_1.0.5/AtlasBuilder-portable-win64.zip)
- [Detailed AtlasBuilder usage](../Helpers/AtlasBuilder/USAGE.md#english)

---

### 1. Extract both games

Open these folders with AssetRipper:

1. `Stronghold Crusader Definitive Edition_Data`
2. The corresponding `Stronghold Definitive Edition_Data` folder

Export from both games:

- Atlas textures
- `Sprite` objects and their JSON metadata
- Sprite names
- Rectangles
- Pivots
- `pixelsPerUnit`
- Existing team-colour masks

SHCDE defines the authoritative target groups, names and indices. SH1DE supplies the new image pixels, optional masks and, when you want to reproduce the source alignment, source pivots. Do not blindly copy a normalized SHCDE pivot onto a SH1DE image with a different canvas size.

Determine the actual source texture from `m_RD.m_Texture` within the specified `m_Collection`, not from the Sprite or GM name. Before exporting, check the source atlas SHA-256 and at least one known reference frame. In SH1DE, for example, `anim_castle` references `alltiles/AllTileSprites.png`, although its target name could suggest a different texture.

Do not crop Tight Mesh Sprites as plain rectangles from `m_Rect`: pixels outside the triangles can belong to neighbouring Sprites, and the mesh may extend beyond `m_Rect`. Rasterize validated vertex, index and UV data, or use a demonstrably correct Sprite export. The [Sprite Suite](https://gitlab.com/strongholdoriginsmod/sh1de-shcde-sprite-suite/-/blob/main/src/stronghold_europe_de/pixel_identity.py) uses UnityPy's `Sprite.image` for this; still check the result against a reference frame, canvas dimensions and anchor. A PNG cropped differently later needs corrected pivot metadata. See the [AtlasBuilder usage guide](../Helpers/AtlasBuilder/USAGE.md#english) for details.

Masks can reside in a separate source texture at a lower resolution. In that case, map source coordinates to the mask texture, resize it to the colour frame with the appropriate filter and clear mask pixels outside the visible colour or mesh area. Only the finished pairs of individual PNGs must have pixel-identical dimensions; the original shared source atlases need not.

---

### 2. Choose a supported GM group

For an initial test, use for example:

    body_archer

Other supported groups include:

    body_spearman
    body_swordsman
    body_knight
    tile_buildings1
    tile_buildings2
    tile_workshops
    tile_churches
    tile_castle
    anim_buildings2
    anim_castle
    anim_windmill

The folder name must match a group registered in Script Extender. A code-free asset mod cannot use an arbitrary group name.

Not every unit or building consists of just one group. A building can use base graphics from a `tile_*` atlas and moving parts from an `anim_*` atlas. Determine which indices belong together from the extracted SHCDE assets and tests.

---

### 3. Map target and source frames

Find the matching SH1DE frame for each SHCDE target frame.

| SHCDE target | SH1DE source | verified |
|---|---|---|
| `body_archer-0` | corresponding SH1DE frame | yes/no |
| `body_archer-1` | corresponding SH1DE frame | yes/no |
| `body_archer-1x` | corresponding alternate frame | yes/no |

Matching names or indices do not prove that direction and animation phase match. Script Extender does not check this.

Do not accept ambiguous mappings automatically. Use the original SHCDE image until you have verified them.

#### Special case: a declared but empty SHCDE target slot

A missing SHCDE Sprite is normally an error that Atlas Builder rejects. There are, however, confirmed gaps within arrays explicitly declared by the game. For `body_swordsman`, `spriteLoader.addGMFile(1087, ...)` declares slots `0–1087`, but the installed assets have no normal Sprites at `416–447`. SH1DE has these exact 32 frames. The alternate range `0x–127x` is complete in both games.

For a confirmed case, select **Target slots absent from SHCDE → Fill from validated source metadata** for that group in the builder. This requires **Pivot from source metadata** and complete AssetRipper JSON files. The builder accepts only frames within the range declared by the SHCDE loader. It validates source filename, `m_Name`, prefix, index and alternate suffix, and never overwrites existing SHCDE target metadata.

The atlas name is deliberately derived from the target group and index, for example `body_swordsman-416`. The source `m_Name` is used for validation; it must not become the target name automatically because source prefixes may intentionally differ. Without this explicit option, the safe default is rejection.

---

### 4. Build a complete or partial GM group

A partial atlas contains only the frames you actually want to replace. Script Extender retains every omitted SHCDE main and alternate frame, including trailing indices, as a vanilla Sprite with its existing material. Do not add empty Sprites for missing frames or copy vanilla images merely to fill an index range. For a complete graphic replacement, identify all relevant groups and frames: a building can use multiple `tile_*` and `anim_*` groups.

Alternatively, replace a few individual Sprites under `Override/Sprites/`. Atlases are more convenient for large frame sets.

---

### 5. Pack the colour and mask atlases

Produce one set per GM group:

    atlas.png or atlas.dds
    atlas_m.png or atlas_m.dds (masked groups only)
    atlas.json

When packing:

- Disable rotation.
- Avoid automatic trimming where possible.
- Arrange colour and mask frames identically.
- Preserve transparent borders.
- Keep every rectangle fully inside the atlas.
- Do not overlap rectangles.
- Do not interpolate pixel artwork when scaling it.

For `atlas.png`, Script Extender uses:

- `FilterMode.Point`
- `TextureWrapMode.Clamp`
- No mipmaps
- `SpriteMeshType.FullRect`

For BC7-DDS, the Extender reads compressed blocks without decompressing them. Store the DDS vertically flipped relative to the upright PNG; JSON rectangles still use Unity's bottom-left origin. Width and height should be multiples of four, frames must not share a BC7 block, and this atlas needs no mipmaps. BC7 uses less GPU texture memory than RGBA32 but slightly changes pixel and mask values. PNG is therefore the lossless default; colour and mask may use different output formats. Never output both `.png` and `.dds` for the same texture: the Extender prefers PNG. AtlasBuilder 1.0.5 handles the required BC7 layout and vertical flip when DDS is selected.

One global atlas for the whole game is unsupported. Each GM group needs its own folder and is loaded as a separate texture.

---

### 6. Team-colour mask

`atlas_m.png` or `atlas_m.dds` is technically optional, but the correct choice depends on the target group and `material` mode. Atlas Builder uses a table of all 195 groups checked against Script Extender and the SHCDE loader:

- Plain groups such as `tile_ruins`, `tile_buildings1` and `tree_cactii` must not include a mask.
- TeamColour and Foliage groups require a complete mask.
- The builder rejects a wrong selection before building.

The mask must:

- Have the same overall dimensions as the finished colour atlas.
- Use the same packing layout.
- Match each colour frame pixel for pixel.

The optional root-level `"material"` field accepts `Auto`, `Plain`, `TeamColour`/`TeamColor` or `Foliage`. With a mask, `Auto` uses the GM group's vanilla material type. Without a mask, it uses `Plain` for compatibility. Explicit `TeamColour` or `Foliage` requires a usable `atlas_m.png` or `atlas_m.dds`. Invalid values or unavailable shaders cause the override to fail closed.

Script Extender does not document the meaning of individual mask channels. Inspect the original SHCDE masks with Sprite Previewer.

---

### 7. Create a shared `atlas.json`

Complete and partial groups use the same multi-frame format:

    {
      "pixelsPerUnit": 64,
      "material": "Auto",
      "frames": [
        {
          "name": "body_archer-0",
          "rect": {
            "x": 0,
            "y": 0,
            "w": 44,
            "h": 97
          },
          "pivot": {
            "x": 0.386,
            "y": 0.196
          }
        },
        {
          "name": "body_archer-1",
          "rect": {
            "x": 46,
            "y": 0,
            "w": 45,
            "h": 97
          },
          "pivot": {
            "x": 0.378,
            "y": 0.196
          }
        },
        {
          "name": "body_archer-1x",
          "rect": {
            "x": 93,
            "y": 0,
            "w": 45,
            "h": 97
          },
          "pivot": {
            "x": 0.378,
            "y": 0.196
          }
        }
      ]
    }

Write these property names exactly as shown:

    pixelsPerUnit
    frames
    name
    rect
    x
    y
    w
    h
    pivot

The schema-discriminating fields and ordinary frame fields are effectively case-sensitive in the parser.

---

### 8. Sprite names

#### Hyphen format

Typical for units:

    body_archer-0
    body_archer-169
    body_archer-169x

The lowercase final `x` marks an alternate frame. Uppercase `X` does not work.

#### Space format

Typical for building and terrain tiles:

    tile_buildings1 000
    tile_buildings1 001
    tile_buildings1 002x

Always use the complete original SHCDE names, including spaces, hyphens, underscores and leading zeroes.

---

### 9. Coordinates

JSON rectangles use a bottom-left origin.

If the atlas tool outputs coordinates from the top left:

    unityY = atlasHeight - topY - frameHeight

Script Extender does not perform this conversion.

---

### 10. Pivot and scale

Script Extender uses the pivot and `pixelsPerUnit` values from the JSON without conversion.

A pivot is normalized. The same value therefore points to a different pixel on canvases of different sizes. Copying the normalized SHCDE pivot unchanged is correct only when source and target canvases match or have intentionally identical relative alignment.

Two special cases matter:

- A pivot coordinate of exactly `1.0` denotes an edge. It must remain `1.0` on a differently sized canvas; the ordinary distance-from-bottom/left calculation would otherwise shift parts of `anim_castle`, for example.
- Pivots outside `0..1` are valid. The current game assets include negative Y pivots for `body_horse_archer_top`. Rejecting values outside `0..1` would be wrong; validate the resulting pixel anchor instead.

For ordinary replacement images, preserve the spatial SHCDE pixel anchor:

    anchorX = targetPivotX * targetWidth
    anchorY = targetPivotY * targetHeight
    outputPivotX = anchorX / replacementWidth
    outputPivotY = anchorY / replacementHeight

To reproduce the original alignment of a Sprite exported from another Unity game, you can instead use its `m_Pivot` from AssetRipper metadata. This is reliable only if the replacement image has the same canvas or was scaled proportionally to the documented `m_Rect`. Trimming or a changed transparent border must be compensated explicitly.

With proportional scaling, the normalized source pivot stays the same. If only the canvas size changes, recalculate the absolute source anchor for the new size. When the aspect ratio changes, the builder can warn about possible trimming but cannot prove the correct visual position without more information.

For the verified SH1DE groups `tile_land8`, `tile_buildings1`, `tile_churches` and `tile_ruins`, the pixel anchor is consistently `(32, 16.5)` at 64 PPU:

    64x41:  pivot (0.5, 0.40243897) -> anchor (32, 16.5)
    64x196: pivot (0.5, 0.08418399) -> anchor (32, 16.5)

Copying the first normalized Y pivot unchanged onto the 196-pixel image would place the anchor at about 78.88 rather than 16.5 pixels. Ground tiles, building bases and animated parts would shift relative to each other. Black diagonal gaps between tiles are a typical symptom.

If changed transparent borders move the visible foot point:

    pivotX = anchorPositionX / frameWidth
    pivotY = anchorPositionY / frameHeight

`pixelsPerUnit` need not be 64. The parser uses 64 only as a default when the field is absent. Use the value of the corresponding SHCDE target Sprite.

Replacement images do not technically need the original dimensions. With different dimensions, adjust the pivot and, when needed, PPU deliberately.

Atlas Builder offers three modes per group:

- `target-pixel-anchor`: preserves the SHCDE pixel anchor and is the default.
- `source-metadata`: reads each source pivot from AssetRipper JSON files.
- `target-normalized`: legacy behaviour for deliberately identical canvases.

Schema-1 builder projects load in `target-normalized` mode for compatibility and display a warning. The linked release `1.0.5` saves older projects as schema 5; PNG remains the default unless a different format is selected explicitly.

---

### 11. Combine AssetRipper JSON files

An individual raw JSON file may look like this:

    {
      "m_Name": "body_archer-86",
      "m_Rect": {
        "m_X": 5183,
        "m_Y": 7212,
        "m_Width": 43.9,
        "m_Height": 96.9
      },
      "m_Pivot": {
        "m_X": 0.386,
        "m_Y": 0.196
      },
      "m_PixelsToUnits": 64
    }

AssetRipper JSON files are input metadata, not a finished `atlas.json`. When the source-metadata pivot option is selected, Atlas Builder matches them to PNG frames and writes one shared Extender-format `frames` list per GM group. A manually created atlas needs the same conversion.

---

### 12. Output directory

For example, the finished mod can have this layout:

    SH1DEVisuals/
      info.json
      Override/
        Atlas/
          body_archer/
            atlas.png
            atlas_m.png
            atlas.json
          body_swordsman/
            atlas.png
            atlas_m.png
            atlas.json
          tile_buildings1/
            atlas.png
            atlas.json
          tile_workshops/
            atlas.png
            atlas.json
          anim_windmill/
            atlas.png
            atlas_m.png
            atlas.json

Install under:

    <SHCDE game directory>/BepInEx/plugins/SH1DEVisuals/

A packaged `.semod` file also works. A loose mod folder is more convenient for development and troubleshooting.

---

### 13. Validate before launching the game

The converter should check each group for:

- A group name supported by Script Extender 2.4.0 or newer.
- A `material` value of `Auto`, `Plain`, `TeamColour`/`TeamColor` or `Foliage`, with `atlas_m.png` or `atlas_m.dds` for explicitly masked modes.
- The correct group prefix on every frame.
- A valid numeric index in every name.
- Lowercase `x` only for alternate frames.
- No duplicate main or alternate indices.
- No duplicate names.
- Rectangles fully inside the atlas.
- Positive width and height.
- No rotated frames.
- Identical dimensions for colour and mask atlases.
- Identical packing layouts for both atlases.
- A pivot and PPU value.
- Intentional omission of target indices in partial groups; only included indices need valid target names and geometry.
- A valid DX10/BC7 header, suitable dimensions and upright runtime display for BC7-DDS, without an unintended PNG version of the same texture.

Script Extender does not perform most of these checks itself.

---

### 14. Check the log

Successful registration and application can produce:

    Registered atlas override for [body_archer] (...)
    Auto-registered atlas override for [body_archer] from [...]
    Applying 1 atlas override(s)...
    Applied [body_archer]: ... frames sliced, material=..., shader=[...], mask=yes, arraySize=..., vanillaMain=..., vanillaAlt=..., offset=...

For failures, these short messages are especially relevant:

    Unknown GM file name [...]
    Found atlas.json for [...] but no atlas.png/atlas.dds
    No frames parsed [...]
    No parseable frame indices [...]
    Failed to apply atlas [...]

---

### 15. Test in game

For a unit, check:

- Every direction.
- Idle frames.
- Movement.
- Attack.
- Death.
- Alternate frames.
- All player colours.
- Rendering at different elevations and near walls.
- Clipped feet.
- Zoom levels.

For buildings, also check:

- Construction stages.
- Variants and orientations.
- Damage and fire.
- Animated building parts.
- Flags.
- Workers.
- Goods and effects.

Start with a few unambiguously mapped frames from one group, then expand to other groups. For BC7, also check transparent borders, team colours and masks in game.

---

### Conclusion

This workflow is reliable when you follow these rules:

1. Do not copy entire SH1DE AssetBundles.
2. Use SHCDE as the authoritative target structure.
3. Build one complete or deliberately partial atlas per affected GM group.
4. Pack colour and mask atlases identically.
5. Convert raw AssetRipper JSON files into one shared `frames` list.
6. Leave omitted SHCDE frames as vanilla fallback.
7. Preserve the SHCDE pixel anchor across different canvas sizes or use validated source metadata.
8. Validate names, indices, rectangles, pivots and masks before installation.

Atlas Builder handles packing, target naming and validation. Verify correct extraction and semantic matching of the source images beforehand.

---

## Deutsch

### Ergebnis

Das Atlas-System des Script Extenders 2.4.0 kann vollständige und partielle Einheiten- und Gebäudegruppen ersetzen. Nicht ersetzte Main- und Alt-Frames behalten ihre Vanilla-Sprites, -Materialien und Masken.

Die einzelnen AssetRipper-Metadaten müssen weiterhin zu einer gemeinsamen `atlas.json` zusammengeführt werden. Partielle Atlanten sind ab 2.4.0 auch bei benannten Gruppen in gemeinsam genutzten GM-Arrays sicher; Zielindizes oberhalb des Vanilla-Bestands erweitern das Array gezielt.

Geprüfter Atlasvertrag:

- Script Extender `2.4.0` oder neuer für PNG; `2.8.0` oder neuer für BC7-DDS
- DDS-Ladepfad auch im lokal installierten Script Extender `2.12.0` geprüft

---

### Benötigte Werkzeuge

#### AssetRipper

Damit werden die Unity-Atlanten, Spritebilder und Metadaten beider Spiele extrahiert.

- [Aktuelle AssetRipper-Version](https://github.com/AssetRipper/AssetRipper/releases/latest)
- [Direkter Download für Windows x64](https://github.com/AssetRipper/AssetRipper/releases/latest/download/AssetRipper_win_x64.zip)

#### SHCDE Sprite Previewer

Hilfreich zum Anzeigen und Exportieren der SHCDE-Sprites und Masken.

- [Release v1.1.5](https://gitlab.com/rawra-stronghold-crusader/shcde-sprite-previewer/-/releases/v1.1.5)
- [Direkter Download für Windows x64](https://gitlab.com/api/v4/projects/77768698/packages/generic/shcde-sprite-previewer/1.1.5/shcde-sprite-viewer-win-x64.zip)

Der Previewer ist für SHCDE entwickelt worden. Für die SH1DE-Dateien ist AssetRipper der sicherere Ausgangspunkt.

#### SHCDE Atlas Builder

Der Builder packt, benennt und validiert die fertigen Atlanten. Die portable Windows-Ausgabe benötigt kein installiertes Python.

Das verlinkte Release `1.0.5` kann PNG- oder BC7-DDS-Atlanten ausgeben. Farb- und Maskenformat lassen sich unabhängig wählen; PNG ist der Standard. DDS setzt Script Extender `2.8.0` oder neuer voraus.

- [Aktuelles AtlasBuilder-Release (1.0.5)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/AtlasBuilder_1.0.5)
- [Direkter Download für Windows x64 (portabel)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/download/AtlasBuilder_1.0.5/AtlasBuilder-portable-win64.zip)
- [Ausführliche AtlasBuilder-Anleitung](../Helpers/AtlasBuilder/USAGE.md#deutsch)

---

### 1. Beide Spiele extrahieren

Öffne mit AssetRipper:

1. `Stronghold Crusader Definitive Edition_Data`
2. den entsprechenden `Stronghold Definitive Edition_Data`-Ordner

Exportiere aus beiden Spielen:

- Atlastexturen
- `Sprite`-Objekte beziehungsweise deren JSON-Metadaten
- Spritenamen
- Rechtecke
- Pivots
- `pixelsPerUnit`
- vorhandene Teamfarbenmasken

SHCDE bildet die verbindliche Zielgruppe, Zielnamen und Indices. SH1DE liefert die neuen Bildpixel, gegebenenfalls Masken und – wenn die ursprüngliche Ausrichtung reproduziert werden soll – die Quellpivots. Ein normalisierter SHCDE-Pivot darf nicht ungeprüft auf eine anders große SH1DE-Leinwand kopiert werden.

Die tatsächliche Quelltextur folgt aus `m_RD.m_Texture` innerhalb der angegebenen `m_Collection`, nicht aus Sprite- oder GM-Namen. Vor dem Export Quellatlas-SHA-256 und mindestens einen bekannten Referenzframe abgleichen. `anim_castle` in SH1DE verweist beispielsweise auf `alltiles/AllTileSprites.png`, obwohl der Zielname eine andere Textur vermuten lassen könnte.

Tight-Mesh-Sprites dürfen nicht blind als Rechteck aus `m_Rect` geschnitten werden: außerhalb der Dreiecksfläche können Pixel benachbarter Sprites liegen, und das Mesh kann über `m_Rect` hinausreichen. Die Pixel anhand validierter Vertex-, Index- und UV-Daten rasterisieren oder einen nachweislich korrekt rekonstruierten Sprite-Export verwenden. Die [Sprite Suite](https://gitlab.com/strongholdoriginsmod/sh1de-shcde-sprite-suite/-/blob/main/src/stronghold_europe_de/pixel_identity.py) nutzt hierfür UnityPys `Sprite.image`; das Ergebnis trotzdem gegen Referenzframe, Leinwandgröße und Anker prüfen. Ein nachträglich anders zugeschnittenes PNG benötigt korrigierte Pivot-Metadaten. Einzelheiten stehen in der [AtlasBuilder-Anleitung](../Helpers/AtlasBuilder/USAGE.md#deutsch).

Masken können in einer eigenen, niedriger aufgelösten Quelltextur liegen. In diesem Fall Quellkoordinaten auf die Maskentextur abbilden, mit der passenden Filtermethode auf die Farbframegröße bringen und die Maske außerhalb der sichtbaren Farb-/Meshfläche transparent setzen. Erst die fertigen Einzel-PNG-Paare müssen für den Builder pixelgenau gleich groß sein; die ursprünglichen gemeinsamen Quellatlanten müssen es nicht sein.

---

### 2. Eine unterstützte GM-Gruppe auswählen

Für den ersten Test eignet sich beispielsweise:

    body_archer

Weitere unterstützte Gruppen sind unter anderem:

    body_spearman
    body_swordsman
    body_knight
    tile_buildings1
    tile_buildings2
    tile_workshops
    tile_churches
    tile_castle
    anim_buildings2
    anim_castle
    anim_windmill

Der Ordnername muss einer fest im Script Extender hinterlegten Gruppe entsprechen. Beliebige Namen werden bei einem codefreien Asset-Mod nicht akzeptiert.

Nicht jede Einheit oder jedes Gebäude besteht nur aus einer Gruppe. Gebäude können beispielsweise Grundgrafiken aus einem `tile_*`-Atlas und bewegte Teile aus einem `anim_*`-Atlas verwenden. Welche konkreten Indices zusammengehören, muss anhand der extrahierten SHCDE-Assets und durch Tests bestimmt werden.

---

### 3. Ziel- und Quellframes zuordnen

Für jeden SHCDE-Zielframe muss das inhaltlich passende SH1DE-Frame ermittelt werden.

| SHCDE-Ziel | SH1DE-Quelle | geprüft |
|---|---|---|
| `body_archer-0` | passendes SH1DE-Frame | ja/nein |
| `body_archer-1` | passendes SH1DE-Frame | ja/nein |
| `body_archer-1x` | passendes alternatives Frame | ja/nein |

Gleiche Namen oder Indices beweisen nicht, dass Richtung und Animationsphase identisch sind. Der Script Extender kontrolliert das nicht.

Mehrdeutige Zuordnungen sollten nicht automatisch übernommen werden. Verwende in solchen Fällen zunächst das originale SHCDE-Bild.

#### Sonderfall: deklarierter, aber leerer SHCDE-Zielslot

Ein fehlendes SHCDE-Sprite ist normalerweise ein Fehler und wird vom Atlas Builder abgewiesen. Es gibt jedoch belegte Lücken innerhalb eines vom Spiel ausdrücklich angelegten Arrays. Bei `body_swordsman` deklariert `spriteLoader.addGMFile(1087, ...)` die Slots `0–1087`; die installierten Ressourcen enthalten trotzdem keine normalen Sprites `416–447`. SH1DE enthält genau diese 32 Frames. Die Alt-Reihe `0x–127x` ist in beiden Spielen vollständig.

Für einen solchen belegten Fall kann im Builder pro Gruppe **In SHCDE fehlende Zielslots → Aus validierten Quellmetadaten ergänzen** gewählt werden. Das setzt **Pivot aus Quellmetadaten** und vollständige AssetRipper-JSONs voraus. Der Builder akzeptiert nur Frames innerhalb des vom SHCDE-Loader deklarierten Bereichs, validiert Quelldateiname, `m_Name`, Präfix, Index und Alt-Suffix und überschreibt niemals vorhandene SHCDE-Zieldaten.

Der Atlasname wird dabei bewusst aus der Zielgruppe und dem Index erzeugt, beispielsweise `body_swordsman-416`. `m_Name` der Quelle dient zur Validierung, darf aber wegen möglicher absichtlicher Präfixzuordnungen nicht ungeprüft zum Zielnamen werden. Ohne dieses ausdrückliche Opt-in bleibt die sichere Standardreaktion ein Abbruch.

---

### 4. Vollständige oder partielle GM-Gruppe erzeugen

Ein partieller Atlas enthält nur die tatsächlich gewünschten Ersatzframes. Der Extender erhält alle ausgelassenen SHCDE-Main- und Alt-Frames einschließlich nachfolgender Indizes als Vanilla-Sprites mit ihrem bisherigen Material. Fehlende Frames weder als leere Sprites eintragen noch nur zum Auffüllen eines Indexbereichs aus Vanilla kopieren. Für einen vollständigen Grafiktausch müssen dagegen tatsächlich alle relevanten Gruppen und Frames identifiziert werden; ein Gebäude kann mehrere `tile_*`- und `anim_*`-Gruppen nutzen.

Alternativ lassen sich wenige einzelne Sprites über `Override/Sprites/` ersetzen. Für größere Framebestände sind Atlanten zweckmäßiger.

---

### 5. Neuen Farbatlas und Maskenatlas packen

Erzeuge pro GM-Gruppe:

    atlas.png oder atlas.dds
    atlas_m.png oder atlas_m.dds (nur bei maskierten Gruppen)
    atlas.json

Beim Packen:

- Rotation deaktivieren
- automatisches Trimmen möglichst deaktivieren
- Farb- und Maskenframes identisch anordnen
- transparente Ränder erhalten
- jedes Rechteck vollständig innerhalb des Atlas halten
- keine Rechtecke überlappen lassen
- Pixelgrafiken nicht interpoliert skalieren

Der Script Extender verwendet für `atlas.png`:

- `FilterMode.Point`
- `TextureWrapMode.Clamp`
- keine Mipmaps
- `SpriteMeshType.FullRect`

Bei BC7-DDS liest der Extender die komprimierten Blöcke ohne Dekompression ein. Die DDS muss dafür gegenüber dem aufrechten PNG vertikal gedreht gespeichert sein; die JSON-Rechtecke bleiben dennoch im Unity-Koordinatensystem mit Ursprung unten links. Breite und Höhe sollten Vielfache von vier sein, Frames dürfen keinen BC7-Block teilen, und Mipmaps werden für diesen Atlas nicht benötigt. BC7 benötigt weniger GPU-Texturspeicher als RGBA32, verändert aber Pixel und Maskenwerte geringfügig. Deshalb ist PNG der verlustfreie Standard; Farbe und Maske dürfen unterschiedliche Ausgabeformate haben. Nie zugleich `.png` und `.dds` für dieselbe Textur ausgeben: der Extender bevorzugt PNG. AtlasBuilder 1.0.5 übernimmt beim DDS-Build die nötige BC7-Anordnung und vertikale Drehung.

Ein einzelner globaler Atlas für das ganze Spiel wird nicht unterstützt. Jede GM-Gruppe benötigt einen eigenen Ordner und wird als eigene Textur geladen.

---

### 6. Teamfarbenmaske

`atlas_m.png` beziehungsweise `atlas_m.dds` ist technisch optional, fachlich aber von der Zielgruppe und dem `material`-Modus abhängig. Der aktuelle Atlas Builder liest diesen Vertrag aus der gegen den Script Extender und den SHCDE-Loader geprüften Tabelle aller 195 Gruppen:

- Plain-Gruppen wie `tile_ruins`, `tile_buildings1` oder `tree_cactii` dürfen keine Maske enthalten.
- TeamColour- und Foliage-Gruppen benötigen eine vollständige Maske.
- Eine falsche Auswahl wird vor dem Build abgewiesen.

Sie muss:

- dieselbe Gesamtgröße wie der fertige Farbatlas haben
- dasselbe Packlayout verwenden
- mit jedem Farbframe pixelgenau übereinstimmen

Das optionale root-level Feld `"material"` akzeptiert `Auto`, `Plain`, `TeamColour`/`TeamColor` oder `Foliage`. `Auto` übernimmt mit Maske den Vanilla-Materialtyp der GM-Gruppe und verwendet ohne Maske aus Kompatibilitätsgründen `Plain`. Explizites `TeamColour` oder `Foliage` erfordert eine verwendbare `atlas_m.png` oder `atlas_m.dds`; ungültige Werte oder fehlende Shader lassen das Override fail-closed aus.

Die Bedeutung der Maskenkanäle ist im Script Extender nicht dokumentiert. Dafür sollten die originalen SHCDE-Masken im Sprite Previewer untersucht werden.

---

### 7. Gemeinsame `atlas.json` erzeugen

Für vollständige und partielle Gruppen wird dasselbe Mehrframeformat verwendet:

    {
      "pixelsPerUnit": 64,
      "material": "Auto",
      "frames": [
        {
          "name": "body_archer-0",
          "rect": {
            "x": 0,
            "y": 0,
            "w": 44,
            "h": 97
          },
          "pivot": {
            "x": 0.386,
            "y": 0.196
          }
        },
        {
          "name": "body_archer-1",
          "rect": {
            "x": 46,
            "y": 0,
            "w": 45,
            "h": 97
          },
          "pivot": {
            "x": 0.378,
            "y": 0.196
          }
        },
        {
          "name": "body_archer-1x",
          "rect": {
            "x": 93,
            "y": 0,
            "w": 45,
            "h": 97
          },
          "pivot": {
            "x": 0.378,
            "y": 0.196
          }
        }
      ]
    }

Die Propertynamen müssen genau so geschrieben werden:

    pixelsPerUnit
    frames
    name
    rect
    x
    y
    w
    h
    pivot

Die schemaerkennenden Felder und gewöhnlichen Frame-Felder behandelt der Parser effektiv case-sensitive.

---

### 8. Spritenamen

#### Bindestrichformat

Typisch für Einheiten:

    body_archer-0
    body_archer-169
    body_archer-169x

Das kleine `x` am Ende kennzeichnet ein Alt-Frame. Ein großes `X` funktioniert nicht.

#### Leerzeichenformat

Typisch für Gebäude- und Geländetiles:

    tile_buildings1 000
    tile_buildings1 001
    tile_buildings1 002x

Verwende immer die vollständigen Originalnamen aus SHCDE einschließlich Leerzeichen, Bindestrichen, Unterstrichen und führenden Nullen.

---

### 9. Koordinaten

Die JSON-Rechtecke verwenden einen Ursprung unten links.

Wenn das Atlasprogramm Koordinaten ab oben links ausgibt:

    unityY = atlasHeight - topY - frameHeight

Diese Umrechnung übernimmt der Script Extender nicht.

---

### 10. Pivot und Skalierung

Der Script Extender übernimmt Pivot und `pixelsPerUnit` unverändert aus der JSON.

Ein Pivot ist normalisiert. Derselbe Pivotwert zeigt deshalb auf unterschiedlichen Bildgrößen nicht auf denselben Pixel. Das unveränderte Kopieren des normalisierten SHCDE-Pivots ist nur korrekt, wenn Quell- und Zielbild dieselbe Leinwand besitzen oder bewusst relativ gleich ausgerichtet wurden.

Zwei Sonderfälle sind dabei wichtig:

- Ein Pivot von exakt `1,0` bezeichnet die obere beziehungsweise rechte Kante. Bei einer anderen Leinwandgröße muss er `1,0` bleiben; die normale Abstand-von-unten-/links-Formel würde beispielsweise Teile von `anim_castle` verschieben.
- Pivots außerhalb von `0..1` sind gültig. Die aktuellen Spieldaten enthalten unter anderem negative Y-Pivots bei `body_horse_archer_top`. Eine Bereichsprüfung auf `0..1` wäre daher falsch; geprüft werden muss der resultierende Pixelanker.

Für gewöhnliche Ersatzbilder sollte der räumliche SHCDE-Pixelanker bewahrt werden:

    ankerX = zielPivotX * zielBreite
    ankerY = zielPivotY * zielHöhe
    ausgabePivotX = ankerX / ersatzBreite
    ausgabePivotY = ankerY / ersatzHöhe

Soll dagegen die ursprüngliche Ausrichtung eines aus einem anderen Unity-Spiel exportierten Sprites reproduziert werden, kann dessen `m_Pivot` aus den AssetRipper-Metadaten übernommen werden. Das ist nur zuverlässig, wenn das Ersatzbild dieselbe Leinwand besitzt oder proportional zur dokumentierten `m_Rect` skaliert wurde. Trimming oder ein geänderter transparenter Rand muss ausdrücklich ausgeglichen werden.

Bei proportionaler Skalierung bleibt der normalisierte Quellpivot erhalten. Wurde nur die Leinwand vergrößert oder verkleinert, wird der absolute Quellanker auf die neue Größe umgerechnet. Ändert sich das Seitenverhältnis, kann der Builder vor möglichem Trimming warnen, aber die inhaltlich richtige Position nicht ohne weitere Angaben beweisen.

Bei den geprüften SH1DE-Gruppen `tile_land8`, `tile_buildings1`, `tile_churches` und `tile_ruins` beträgt der Pixelanker durchgehend `(32, 16,5)` bei 64 PPU:

    64x41:  Pivot (0,5; 0,40243897) -> Anker (32; 16,5)
    64x196: Pivot (0,5; 0,08418399) -> Anker (32; 16,5)

Würde der erste normalisierte Y-Pivot unverändert auf das 196 Pixel hohe Bild angewendet, läge der Anker bei ungefähr 78,88 statt 16,5 Pixeln. Bodenplatten, Gebäudegrundbilder und animierte Teile werden dadurch gegeneinander verschoben. Schwarze diagonale Spalten zwischen Tiles sind ein typisches Symptom.

Wenn sich durch andere transparente Ränder die Position des sichtbaren Fußpunkts ändert:

    pivotX = AnkerpositionX / Framebreite
    pivotY = AnkerpositionY / Framehöhe

`pixelsPerUnit` ist nicht zwingend 64. Der Parser verwendet 64 nur als Standard, wenn kein Wert angegeben wurde. Verwende den Wert des entsprechenden SHCDE-Zielsprites.

Die Ersatzbilder müssen technisch nicht dieselben Abmessungen wie die Originale besitzen. Bei abweichenden Größen müssen Pivot und gegebenenfalls PPU aber bewusst angepasst werden.

Der Atlas Builder bietet dafür pro Gruppe drei Modi:

- `target-pixel-anchor`: bewahrt den SHCDE-Pixelanker und ist der Standard
- `source-metadata`: liest den individuellen Quellpivot aus AssetRipper-JSONs
- `target-normalized`: altes Verhalten für absichtlich identische Leinwände

Schema-1-Projekte des Builders werden aus Kompatibilitätsgründen als `target-normalized` geladen und deutlich gewarnt. Das verlinkte Release `1.0.5` speichert ältere Projekte als Schema 5; ohne ausdrückliche Formatwahl bleibt PNG der Standard.

---

### 11. AssetRipper-JSONs zusammenführen

Ein einzelnes Raw-JSON kann so aussehen:

    {
      "m_Name": "body_archer-86",
      "m_Rect": {
        "m_X": 5183,
        "m_Y": 7212,
        "m_Width": 43.9,
        "m_Height": 96.9
      },
      "m_Pivot": {
        "m_X": 0.386,
        "m_Y": 0.196
      },
      "m_PixelsToUnits": 64
    }

AssetRipper-JSONs sind Eingabemetadaten, keine fertige `atlas.json`. Der Atlas Builder ordnet sie bei gewählter Quellmetadaten-Pivotquelle den PNG-Frames zu und schreibt pro GM-Gruppe eine gemeinsame `frames`-Liste im Extender-Format. Ein selbst erstellter Atlas muss dieselbe Umwandlung leisten.

---

### 12. Ausgabeordner

Der fertige Mod sieht beispielsweise so aus:

    SH1DEVisuals/
      info.json
      Override/
        Atlas/
          body_archer/
            atlas.png
            atlas_m.png
            atlas.json
          body_swordsman/
            atlas.png
            atlas_m.png
            atlas.json
          tile_buildings1/
            atlas.png
            atlas.json
          tile_workshops/
            atlas.png
            atlas.json
          anim_windmill/
            atlas.png
            atlas_m.png
            atlas.json

Installation:

    E:\ProgrammeE\Steam\steamapps\common\
      Stronghold Crusader Definitive Edition\
        BepInEx\
          plugins\
            SH1DEVisuals\

Eine gepackte `.semod`-Datei funktioniert ebenfalls. Für Entwicklung und Fehlersuche ist ein loser Modordner praktischer.

---

### 13. Validierung vor dem Spielstart

Der Konverter sollte je Gruppe prüfen:

- Gruppenname wird von Script Extender 2.4.0 unterstützt
- `material` ist `Auto`, `Plain`, `TeamColour`/`TeamColor` oder `Foliage`, und explizit maskierte Modi besitzen `atlas_m.png` oder `atlas_m.dds`
- jedes Frame beginnt mit dem richtigen Gruppenpräfix
- jeder Name enthält einen gültigen numerischen Index
- Alt-Frames enden ausschließlich auf kleinem `x`
- keine doppelten Main- oder Alt-Indices
- keine doppelten Namen
- alle Rechtecke liegen innerhalb des Atlas
- Breite und Höhe sind positiv
- keine Frames wurden rotiert
- Farb- und Maskenatlas haben dieselbe Größe
- beide Atlanten verwenden dasselbe Layout
- Pivot und PPU sind vorhanden
- partielle Gruppen lassen fehlende Zielindices absichtlich aus; nur aufgenommene Indices brauchen gültige Zielnamen und Geometrie
- BC7-DDS hat einen gültigen DX10/BC7-Header, passende Maße, aufrechte Laufzeitdarstellung und keine versehentlich zusätzlich vorhandene PNG-Version derselben Textur

Der Script Extender führt diese Prüfungen größtenteils nicht selbst durch.

---

### 14. Prüfung im Log

Erfolgreiche Registrierung:

    Registered atlas override for [body_archer] (...)
    Auto-registered atlas override for [body_archer] from [...]
    Applying 1 atlas override(s)...
    Applied [body_archer]: ... frames sliced, material=..., shader=[...], mask=yes, arraySize=..., vanillaMain=..., vanillaAlt=..., offset=...

Bei einem Fehler sind vor allem diese kurzen Meldungen relevant:

    Unknown GM file name [...]
    Found atlas.json for [...] but no atlas.png/atlas.dds
    No frames parsed [...]
    No parseable frame indices [...]
    Failed to apply atlas [...]

---

### 15. Laufzeittest

Für eine Einheit prüfen:

- alle Richtungen
- Stillstand
- Bewegung
- Angriff
- Tod
- Alt-Frames
- sämtliche Spielerfarben
- Darstellung an Höhenstufen und Mauern
- abgeschnittene Füße
- Zoomstufen

Für Gebäude zusätzlich:

- Bauphasen
- verschiedene Varianten und Ausrichtungen
- Schäden und Feuer
- animierte Gebäudeteile
- Flaggen
- Arbeiter
- Waren und Effekte

Zuerst wenige eindeutig zugeordnete Frames einer Gruppe testen und danach auf weitere Gruppen erweitern. Bei BC7 zusätzlich transparente Ränder, Teamfarben und Masken im Spiel kontrollieren.

---

### Schlussfolgerung

Der Guide funktioniert zuverlässig, wenn folgende Regeln eingehalten werden:

1. Nicht ganze SH1DE-AssetBundles kopieren.
2. SHCDE als verbindliche Zielstruktur verwenden.
3. Pro betroffener GM-Gruppe einen vollständigen oder gezielt partiellen Atlas erzeugen.
4. Farb- und Maskenatlas identisch packen.
5. AssetRipper-Raw-JSONs zu einer gemeinsamen `frames`-Liste konvertieren.
6. Nicht ersetzte SHCDE-Frames als Vanilla-Fallback belassen.
7. Bei unterschiedlichen Leinwandgrößen den SHCDE-Pixelanker bewahren oder belegte Quellmetadaten verwenden.
8. Vor der Installation sämtliche Namen, Indices, Rechtecke, Pivots und Masken validieren.

Der Atlas Builder übernimmt Packen, Namenszuordnung und Validierung. Die korrekte Extraktion und inhaltliche Zuordnung der Quellbilder muss vorher geprüft werden.
