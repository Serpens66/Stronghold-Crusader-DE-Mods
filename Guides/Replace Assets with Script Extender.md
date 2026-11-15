# Replace SH1DE graphics with Script Extender / SH1DE-Grafiken mit dem Script Extender ersetzen

[English](#english) | [Deutsch](#deutsch)

## English

This guide is for modders who want to replace unit, building or terrain graphics in SHCDE with their own artwork or graphics extracted from SH1DE. You can replace a few animation frames or a complete graphics group without writing a C# plugin.

A **Sprite** is one image or animation frame. An **atlas** is a texture containing many such images. A **GM group**, such as `body_archer` or `tile_buildings1`, identifies the SHCDE graphics you want to replace. Frames omitted from your atlas keep their original SHCDE graphics, materials and masks.

### What you need

- SHCDE with BepInEx and [Script Extender](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases). PNG atlases require Script Extender **2.4.0 or newer**; BC7-DDS atlases require **2.8.0 or newer**.
- [AssetRipper](https://github.com/AssetRipper/AssetRipper/releases/latest) to export source images and Sprite metadata. Choose the Windows x64 package for Windows.
- Optionally, [SHCDE Sprite Previewer](https://gitlab.com/rawra-stronghold-crusader/shcde-sprite-previewer/-/releases) to inspect SHCDE images and masks.
- **Atlas Builder** to arrange the images, assign SHCDE names and write the required files. The [portable Windows package, release 1.0.5](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/AtlasBuilder_1.0.5) needs no installed Python. The [source directory](../Helpers/AtlasBuilder/) and [detailed instructions](../Helpers/AtlasBuilder/USAGE.md#english) describe the current source implementation.

The advanced frame filter, mixed source-metadata fallback and filling of empty target slots described below refer to the current source implementation; do not assume they are included in the portable package. To use that implementation, obtain this repository's current source, install `Helpers/AtlasBuilder/requirements.txt` in a Python environment and run `Helpers/AtlasBuilder/run_atlas_builder.py`. For DDS output, also run `Helpers/AtlasBuilder/fetch_texconv.py` to obtain its encoder.

### 1. Prepare a small set of images

Start with a few frames from one group, for example `body_archer`, rather than a complete conversion.

1. Open the relevant game's `...Definitive Edition_Data` folder in AssetRipper. For SH1DE artwork, export its Sprite images, Sprite JSON metadata and any team-colour masks.
2. Inspect the corresponding SHCDE group to find the exact target names and animation frames. The Atlas Builder also reads target metadata directly from your installed SHCDE data folder.
3. Put the replacement images in a separate source folder as individual PNG files. Preserve transparent borders. If masks are needed, prepare one matching mask per image.
4. Compare directions, poses and animation phases. Identical indices in the two games do not guarantee matching artwork. Leave uncertain frames out until you have checked them.

Typical filenames are `body_archer-0.png`, `body_archer-1.png` and `body_archer-1x.png`. A lowercase final `x` identifies an alternate frame. Building and terrain names often use a space and leading zeroes, such as `tile_buildings1 012`.

A building may need more than one group: its base can use `tile_*` images while moving or upper parts use `anim_*`. Replacing only one group leaves the other parts unchanged. Do not copy complete SH1DE AssetBundles into SHCDE.

#### Avoid damaged exports

Check the exported PNGs before packing them. A simple rectangular cut from an atlas can include fragments of neighbouring images when the Sprite uses a **Tight Mesh**, an irregular shape stored inside the atlas. Use an exporter that reconstructs that shape correctly. Atlas Builder packs the images you give it; it cannot remove contamination already present in them.

For a custom extractor, follow the texture reference in `m_RD.m_Texture` within `m_Collection`, rather than guessing from the group name. Check the source texture and a known reference frame. Keep the original atlas identity, for example its SHA-256, with your extraction notes. If cropping changes the image canvas, update its pivot metadata too. Masks can come from a differently sized source texture, but each finished colour/mask PNG pair must have identical dimensions and alignment. The [extraction reference](../Helpers/AtlasBuilder/USAGE.md#english) covers mesh reconstruction and anchor correction.

### 2. Configure Atlas Builder

1. Start `AtlasBuilder.exe`, or the source script described above.
2. Select your installed `Stronghold Crusader Definitive Edition_Data` folder.
3. Choose a new empty output mod folder, for example `SH1DEVisuals`, and fill in the mod information for `info.json`.
4. Add the target GM group and select your source PNG folder. Use a group from the builder's list; arbitrary group names are not supported for a code-free atlas mod.
5. Select the source filename prefix. `auto` detects names such as `Tree_Oak-12.png`, `tile_ruins 012.png` or `012.png`. A different source prefix does not change the SHCDE target group or remap its indices.
6. Select the mask mode and pivot source as explained below.
7. Keep **PNG** for your first test. Save the project as `*.atlas-project.json`, validate it and build the atlas.

The builder does not trim or rotate your images. It obtains target names and pixels per unit from SHCDE. Existing `info.json` files are preserved; existing atlas groups are replaced only after confirmation.

A partial atlas needs only the replacement frames. Do not add blank images or copy original SHCDE frames merely to fill gaps. In the source implementation, **Show advanced options → Source frame filter** can select indices such as `0-127, 416-447, 0x-127x` from a larger directory. An empty filter includes all frames; it does not create or remap frames.

### 3. Choose masks and alignment

#### Masks

A mask controls effects such as player colours or foliage shading. Follow the target group's mask mode in the builder:

- **Plain** groups, such as `tile_ruins` and `tile_buildings1`, use no mask.
- **TeamColour** and **Foliage** groups need a mask for every replacement frame.
- If masks share the colour directory, name them `body_archer-0_m.png`, for example. In a separate mask directory, the `_m` suffix is optional.

Every finished mask must match its colour image exactly in dimensions and placement. The builder packs them with the same layout. Inspect original SHCDE masks in Sprite Previewer when creating your own.

#### Pivot: the point that holds an image in place

The **pivot** determines how an image lines up with the game world. A wrong pivot can produce floating feet, shifted building parts or gaps between ground tiles.

| Builder setting | Use it when |
|---|---|
| **Preserve SHCDE pixel anchor** | Making normal replacements that should stay aligned with the SHCDE target. This is the default. |
| **Pivot from source metadata** | Reproducing the source game's alignment. Also select the matching AssetRipper Sprite JSON directory. The metadata must describe the actual PNG canvas. |
| **Copy normalized SHCDE pivot (legacy)** | Source and target canvases deliberately use the same relative alignment. Avoid it for differently sized images unless this is intentional. |

Do not trim images after exporting without adjusting their metadata. A normalized pivot copied from a small image onto a taller image can move the anchor by many pixels. `pixelsPerUnit` controls image scale; use the SHCDE target value rather than assuming every group uses 64.

In the source implementation, **Missing source metadata → Use SHCDE pixel anchor** permits a mixed group: existing source JSON takes precedence, and frames without it use their SHCDE target anchor. At least one matching source JSON and valid SHCDE target metadata for every fallback frame are required. Leave rejection enabled unless you intend this mix.

A genuinely missing SHCDE target Sprite is a separate case. **Target slots absent from SHCDE → Fill from validated source metadata** requires source-metadata pivot mode and complete valid source JSON. It only fills slots within the range declared by the SHCDE loader. Use it only after verifying the frame mapping; it does not guess missing animation frames.

### 4. Install and test

The output should have this structure:

    SH1DEVisuals/
      info.json
      Override/
        Atlas/
          body_archer/
            atlas.png
            atlas_m.png
            atlas.json
          tile_buildings1/
            atlas.png
            atlas.json

Close the game and copy `SH1DEVisuals` into:

    <SHCDE game directory>/BepInEx/plugins/

For a purely visual mod, the generated `info.json` uses `NetworkMode: 0`. Keep the manifest and all generated files together when sharing the mod. A loose mod folder is convenient for development; Script Extender also supports `.semod` packages.

Avoid enabling two atlas mods for the same GM group at once: only one registered atlas per group is applied, so their partial atlases are not automatically merged.

Restart the game after changing atlas files. In `BepInEx/LogOutput.log`, look for `Auto-registered atlas override`, `Applying ... atlas override(s)` and `Applied [body_archer]`. Registration alone does not confirm successful application.

Test every direction and animation, alternate frames, all player colours and zoom levels. For buildings, also check construction stages, orientations, damage, fire and moving parts. Check both replaced and omitted frames. Expand to more groups only after the small test works.

| Problem | What to check |
|---|---|
| Nothing changes | Correct mod folder, `info.json`, supported group name, `atlas.json` and texture; read the log for application errors. |
| Only part of a building changes | Other `tile_*` or `anim_*` groups may supply the remaining parts. |
| Coloured fragments surround a Sprite | Inspect the source PNG. Re-export Tight Mesh images correctly. |
| Feet or building parts shift; gaps appear | Pivot choice, transparent borders, source metadata and PPU. |
| Team colours or foliage look wrong | Correct material mode, complete masks, matching image/mask layout. |
| DDS changes do not appear | Remove an unintended PNG of the same texture; PNG takes precedence. |

### Reference for manually created atlases

Skip this section when Atlas Builder already produces the files you need.

Each group needs one `Override/Atlas/<group>/` folder. Use `atlas.png` or `atlas.dds`, an optional corresponding `atlas_m` mask and one `atlas.json`. Colour and mask may use different formats. Do not supply both PNG and DDS for the same texture.

A minimal multi-frame JSON example is:

    {
      "pixelsPerUnit": 64,
      "material": "TeamColour",
      "frames": [
        {
          "name": "body_archer-0",
          "rect": { "x": 0, "y": 0, "w": 44, "h": 97 },
          "pivot": { "x": 0.386, "y": 0.196 }
        }
      ]
    }

The numbers illustrate the format; use the actual dimensions and alignment of your images. Keep field names and complete SHCDE Sprite names exactly as required, including spaces, leading zeroes and the lowercase alternate suffix. AssetRipper's individual raw JSON files are input metadata, not a ready multi-frame `atlas.json` for a repacked texture.

- Rectangles use a **bottom-left** origin: `unityY = atlasHeight - topY - frameHeight` converts top-left coordinates. Keep rectangles inside the texture, with positive dimensions and no overlaps or rotation.
- Pack colour and masks identically, preserving transparent borders. PNG textures use point filtering and no mipmaps.
- `material` accepts `Auto`, `Plain`, `TeamColour` (also `TeamColor`) or `Foliage`. Explicit TeamColour/Foliage requires a usable mask. `Auto` uses the group's original material type with a mask and Plain without one; specify the intended mode to avoid an unintended unmasked result. Invalid material settings leave the original atlas unchanged.
- A pivot is measured relative to image dimensions. For a normal pixel anchor, `outputPivot = targetPivot × targetSize / replacementSize` per axis. The builder treats an exact `1.0` as the opposite edge and keeps it at `1.0`; pivots outside `0..1` can be valid. See the detailed guide before manually changing these cases.
- BC7-DDS is lossy and reduces texture memory. PNG is the lossless default and is often preferable for masks. DDS needs a DX10/BC7 header, dimensions aligned to four pixels, isolated 4×4 compression blocks per frame and vertically flipped storage for the Extender. JSON coordinates remain bottom-left. Atlas Builder performs this packing and flipping for you.
- Omitted original main and alternate frames remain intact. This also applies to named groups sharing an internal array; do not pad a partial atlas with empty replacements.

For detailed extraction and builder options, use the [Atlas Builder instructions](../Helpers/AtlasBuilder/USAGE.md#english). Other graphics tools include [llm-art-pipeline](https://github.com/Ensrick/llm-art-pipeline).

---

## Deutsch

Diese Anleitung richtet sich an Modder, die Einheiten-, Gebäude- oder Geländegrafiken in SHCDE durch eigene Bilder oder aus SH1DE exportierte Grafiken ersetzen möchten. Du kannst einzelne Animationsbilder oder eine vollständige Grafikgruppe austauschen, ohne ein C#-Plugin zu schreiben.

Ein **Sprite** ist ein einzelnes Bild oder Animationsbild. Ein **Atlas** ist eine Textur mit vielen solchen Bildern. Eine **GM-Gruppe**, etwa `body_archer` oder `tile_buildings1`, bezeichnet die zu ersetzenden SHCDE-Grafiken. Nicht im Atlas enthaltene Frames behalten ihre ursprünglichen SHCDE-Grafiken, Materialien und Masken.

### Was du benötigst

- SHCDE mit BepInEx und [Script Extender](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases). PNG-Atlanten benötigen Script Extender **2.4.0 oder neuer**, BC7-DDS-Atlanten **2.8.0 oder neuer**.
- [AssetRipper](https://github.com/AssetRipper/AssetRipper/releases/latest) zum Exportieren der Quellbilder und Sprite-Metadaten. Wähle unter Windows das Windows-x64-Paket.
- Optional den [SHCDE Sprite Previewer](https://gitlab.com/rawra-stronghold-crusader/shcde-sprite-previewer/-/releases), um SHCDE-Bilder und Masken anzusehen.
- Den **Atlas Builder**, der Bilder anordnet, SHCDE-Namen zuweist und die benötigten Dateien schreibt. Das [portable Windows-Paket, Release 1.0.5](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/AtlasBuilder_1.0.5) benötigt kein installiertes Python. Der [Quellordner](../Helpers/AtlasBuilder/) und die [ausführliche Anleitung](../Helpers/AtlasBuilder/USAGE.md#deutsch) beschreiben die aktuelle Quellimplementierung.

Der nachfolgend beschriebene erweiterte Frame-Filter, der gemischte Quellmetadaten-Fallback und das Ergänzen leerer Zielslots beziehen sich auf die aktuelle Quellimplementierung; setze sie im portablen Paket nicht voraus. Dafür benötigst du den aktuellen Quellstand dieses Repositorys: Installiere `Helpers/AtlasBuilder/requirements.txt` in einer Python-Umgebung und starte `Helpers/AtlasBuilder/run_atlas_builder.py`. Für DDS-Ausgabe rufst du außerdem `Helpers/AtlasBuilder/fetch_texconv.py` auf, um den Encoder zu beziehen.

### 1. Wenige Bilder vorbereiten

Beginne mit einigen Frames einer Gruppe, beispielsweise `body_archer`, statt direkt mit einer vollständigen Konvertierung.

1. Öffne den `...Definitive Edition_Data`-Ordner des jeweiligen Spiels in AssetRipper. Für SH1DE-Grafiken exportierst du dessen Spritebilder, Sprite-JSON-Metadaten und vorhandene Teamfarbenmasken.
2. Sieh dir die entsprechende SHCDE-Gruppe an, um die genauen Zielnamen und Animationsbilder zu bestimmen. Der Atlas Builder liest Zielmetadaten außerdem direkt aus deinem installierten SHCDE-Datenordner.
3. Lege die Ersatzbilder als einzelne PNG-Dateien in einen eigenen Quellordner. Erhalte transparente Ränder. Wenn Masken benötigt werden, bereite für jedes Bild eine passende Maske vor.
4. Vergleiche Richtungen, Posen und Animationsphasen. Gleiche Indizes in beiden Spielen garantieren keine passenden Bilder. Lasse unklare Frames zunächst aus.

Typische Dateinamen sind `body_archer-0.png`, `body_archer-1.png` und `body_archer-1x.png`. Ein kleines abschließendes `x` kennzeichnet ein alternatives Frame. Gebäude- und Geländenamen verwenden häufig ein Leerzeichen und führende Nullen, etwa `tile_buildings1 012`.

Ein Gebäude kann mehrere Gruppen benötigen: Grundgrafiken können aus `tile_*`, bewegte oder obere Teile aus `anim_*` stammen. Wenn du nur eine Gruppe ersetzt, bleiben die anderen Teile unverändert. Kopiere keine vollständigen SH1DE-AssetBundles nach SHCDE.

#### Fehlerhafte Exporte vermeiden

Kontrolliere die PNGs vor dem Packen. Ein einfacher rechteckiger Ausschnitt aus einem Atlas kann Teile benachbarter Bilder enthalten, wenn das Sprite ein **Tight Mesh** verwendet, also eine unregelmäßige Form innerhalb des Atlas. Verwende einen Exporter, der diese Form korrekt rekonstruiert. Der Atlas Builder packt deine Eingabebilder; bereits darin enthaltene fremde Pixel kann er nicht entfernen.

Bei einem eigenen Extraktor folgt die Quelltextur aus `m_RD.m_Texture` innerhalb von `m_Collection`, nicht aus dem vermuteten Gruppennamen. Prüfe die Quelltextur und einen bekannten Referenzframe. Halte die Identität des Quellatlas, etwa seinen SHA-256, in deinen Exportnotizen fest. Ändert ein Zuschnitt die Bildleinwand, müssen auch die Pivot-Metadaten angepasst werden. Masken können aus einer anders großen Quelltextur stammen; jedes fertige Farb-/Masken-PNG-Paar muss aber identische Maße und Ausrichtung besitzen. Die [Extraktionsreferenz](../Helpers/AtlasBuilder/USAGE.md#deutsch) erklärt Mesh-Rekonstruktion und Ankerkorrektur.

### 2. Atlas Builder einrichten

1. Starte `AtlasBuilder.exe` oder das oben beschriebene Quellskript.
2. Wähle den installierten Ordner `Stronghold Crusader Definitive Edition_Data`.
3. Wähle einen neuen, leeren Mod-Ausgabeordner, beispielsweise `SH1DEVisuals`, und trage die Modinformationen für `info.json` ein.
4. Füge die Ziel-GM-Gruppe hinzu und wähle deinen PNG-Quellordner. Verwende eine Gruppe aus der Liste des Builders; beliebige Gruppennamen funktionieren bei einem codefreien Atlas-Mod nicht.
5. Wähle das Quellpräfix. `auto` erkennt Namen wie `Tree_Oak-12.png`, `tile_ruins 012.png` oder `012.png`. Ein anderes Quellpräfix ändert weder die SHCDE-Zielgruppe noch die Zuordnung der Indizes.
6. Wähle Maskenmodus und Pivotquelle wie unten beschrieben.
7. Behalte für den ersten Test **PNG** bei. Speichere das Projekt als `*.atlas-project.json`, prüfe es und erzeuge anschließend den Atlas.

Der Builder trimmt und rotiert deine Bilder nicht. Zielnamen und Pixels-per-Unit übernimmt er aus SHCDE. Eine vorhandene `info.json` bleibt erhalten; bestehende Atlasgruppen werden nur nach Bestätigung ersetzt.

Ein partieller Atlas benötigt nur die Ersatzframes. Füge keine leeren Bilder ein und kopiere keine originalen SHCDE-Frames, nur um Lücken aufzufüllen. In der Quellimplementierung kannst du unter **Erweiterte Optionen anzeigen → Quellframe-Filter** Indizes wie `0-127, 416-447, 0x-127x` aus einem größeren Ordner auswählen. Ein leerer Filter berücksichtigt alle Frames; er erzeugt keine Frames und ordnet keine Indizes um.

### 3. Masken und Ausrichtung wählen

#### Masken

Eine Maske steuert Effekte wie Spielerfarben oder Laubdarstellung. Richte dich nach dem Maskenmodus der Zielgruppe im Builder:

- **Plain**-Gruppen wie `tile_ruins` und `tile_buildings1` verwenden keine Maske.
- **TeamColour**- und **Foliage**-Gruppen benötigen für jeden Ersatzframe eine Maske.
- Liegen Masken im Farbordner, benenne sie beispielsweise `body_archer-0_m.png`. In einem eigenen Maskenordner ist die Endung `_m` optional.

Jede fertige Maske muss in Größe und Position genau zum Farbbild passen. Der Builder packt beide mit demselben Layout. Zum Erstellen eigener Masken kannst du die Originalmasken von SHCDE im Sprite Previewer ansehen.

#### Pivot: der Punkt, der ein Bild festhält

Der **Pivot** bestimmt, wie ein Bild in der Spielwelt ausgerichtet wird. Ein falscher Pivot kann schwebende Füße, verschobene Gebäudeteile oder Lücken zwischen Bodentiles verursachen.

| Builder-Einstellung | Geeignet für |
|---|---|
| **SHCDE-Pixelanker erhalten** | Normale Ersatzbilder, die wie das SHCDE-Ziel ausgerichtet bleiben sollen. Dies ist der Standard. |
| **Pivot aus Quellmetadaten** | Die Ausrichtung des Quellspiels übernehmen. Wähle auch den Ordner mit den passenden AssetRipper-Sprite-JSONs. Die Metadaten müssen die tatsächliche PNG-Leinwand beschreiben. |
| **Normalisierten SHCDE-Pivot übernehmen (Legacy)** | Quell- und Zielbilder sollen bewusst dieselbe relative Ausrichtung verwenden. Bei unterschiedlich großen Bildern nur absichtlich einsetzen. |

Schneide exportierte Bilder nicht nachträglich zu, ohne ihre Metadaten anzupassen. Ein normalisierter Pivot, der von einem kleinen auf ein höheres Bild kopiert wird, kann den Anker um viele Pixel verschieben. `pixelsPerUnit` bestimmt die Bildskalierung; verwende den Wert des SHCDE-Ziels, statt für jede Gruppe 64 anzunehmen.

In der Quellimplementierung erlaubt **Fehlende Quellmetadaten → SHCDE-Pixelanker verwenden** eine gemischte Gruppe: Vorhandene Quell-JSONs haben Vorrang; Frames ohne sie verwenden ihren SHCDE-Zielanker. Mindestens ein passendes Quell-JSON und gültige SHCDE-Zielmetadaten für jeden Fallback-Frame sind erforderlich. Behalte die Ablehnung bei, wenn du diese Mischung nicht beabsichtigst.

Ein tatsächlich fehlendes SHCDE-Zielsprite ist ein anderer Fall. **In SHCDE fehlende Zielslots → Aus validierten Quellmetadaten ergänzen** benötigt den Quellmetadaten-Pivotmodus und vollständige gültige Quell-JSONs. Es ergänzt nur Slots innerhalb des vom SHCDE-Loader deklarierten Bereichs. Verwende es nur nach Prüfung der Frame-Zuordnung; fehlende Animationsbilder werden nicht erraten.

### 4. Installieren und testen

Die Ausgabe sollte so aufgebaut sein:

    SH1DEVisuals/
      info.json
      Override/
        Atlas/
          body_archer/
            atlas.png
            atlas_m.png
            atlas.json
          tile_buildings1/
            atlas.png
            atlas.json

Schließe das Spiel und kopiere `SH1DEVisuals` nach:

    <SHCDE-Spielordner>/BepInEx/plugins/

Für einen reinen Grafikmod verwendet die erzeugte `info.json` den Wert `NetworkMode: 0`. Gib das Manifest und alle erzeugten Dateien gemeinsam weiter. Ein loser Modordner ist für die Entwicklung praktisch; der Script Extender unterstützt auch `.semod`-Pakete.

Aktiviere möglichst nicht zwei Atlas-Mods für dieselbe GM-Gruppe gleichzeitig: Pro Gruppe wird nur ein registrierter Atlas angewendet; ihre partiellen Atlanten werden nicht automatisch zusammengeführt.

Starte das Spiel nach Änderungen an Atlasdateien neu. Suche in `BepInEx/LogOutput.log` nach `Auto-registered atlas override`, `Applying ... atlas override(s)` und `Applied [body_archer]`. Eine Registrierung allein bestätigt noch keine erfolgreiche Anwendung.

Teste alle Richtungen und Animationen, alternative Frames, sämtliche Spielerfarben und Zoomstufen. Prüfe bei Gebäuden zusätzlich Bauphasen, Ausrichtungen, Schäden, Feuer und bewegte Teile. Kontrolliere sowohl ersetzte als auch ausgelassene Frames. Erweitere den Mod erst nach einem erfolgreichen kleinen Test auf weitere Gruppen.

| Problem | Was du prüfen solltest |
|---|---|
| Nichts verändert sich | Richtiger Modordner, `info.json`, unterstützter Gruppenname, `atlas.json` und Textur; Anwendungsmeldungen im Log lesen. |
| Nur ein Gebäudeteil verändert sich | Die übrigen Teile können aus weiteren `tile_*`- oder `anim_*`-Gruppen stammen. |
| Farbige Fragmente umgeben ein Sprite | Quell-PNG ansehen. Tight-Mesh-Bilder korrekt neu exportieren. |
| Füße oder Gebäudeteile verschieben sich; Lücken erscheinen | Pivotwahl, transparente Ränder, Quellmetadaten und PPU. |
| Spielerfarben oder Laub sehen falsch aus | Richtiger Materialmodus, vollständige Masken und identisches Bild-/Maskenlayout. |
| DDS-Änderungen erscheinen nicht | Eine versehentlich vorhandene PNG derselben Textur entfernen; PNG hat Vorrang. |

### Referenz für selbst erstellte Atlanten

Diesen Abschnitt kannst du überspringen, wenn Atlas Builder die benötigten Dateien bereits erzeugt.

Jede Gruppe benötigt einen Ordner `Override/Atlas/<Gruppe>/`. Darin liegen `atlas.png` oder `atlas.dds`, gegebenenfalls eine passende `atlas_m`-Maske und eine `atlas.json`. Farbe und Maske dürfen unterschiedliche Formate verwenden. Gib für dieselbe Textur nicht zugleich PNG und DDS aus.

Ein minimales Mehrframe-JSON sieht so aus:

    {
      "pixelsPerUnit": 64,
      "material": "TeamColour",
      "frames": [
        {
          "name": "body_archer-0",
          "rect": { "x": 0, "y": 0, "w": 44, "h": 97 },
          "pivot": { "x": 0.386, "y": 0.196 }
        }
      ]
    }

Die Zahlen zeigen das Format; verwende die tatsächlichen Maße und die Ausrichtung deiner Bilder. Behalte Feldnamen und vollständige SHCDE-Spritenamen exakt bei, einschließlich Leerzeichen, führender Nullen und kleinem Alt-Suffix. Einzelne AssetRipper-Raw-JSONs sind Eingabemetadaten, keine fertige Mehrframe-`atlas.json` für eine neu gepackte Textur.

- Rechtecke haben ihren Ursprung **unten links**: `unityY = atlasHeight - topY - frameHeight` rechnet Koordinaten von oben links um. Alle Rechtecke müssen mit positiven Maßen vollständig innerhalb der Textur liegen, ohne Überlappung oder Rotation.
- Packe Farbe und Masken identisch und erhalte transparente Ränder. PNG-Texturen verwenden Punktfilterung und keine Mipmaps.
- `material` akzeptiert `Auto`, `Plain`, `TeamColour` (auch `TeamColor`) oder `Foliage`. Explizites TeamColour/Foliage benötigt eine verwendbare Maske. `Auto` verwendet mit Maske den ursprünglichen Materialtyp der Gruppe, ohne Maske Plain; gib den gewünschten Modus an, um ein unbeabsichtigt unmaskiertes Ergebnis zu vermeiden. Bei ungültigen Materialeinstellungen bleibt der ursprüngliche Atlas erhalten.
- Ein Pivot bezieht sich auf die Bildmaße. Für einen normalen Pixelanker gilt je Achse `ausgabePivot = zielPivot × zielGröße / ersatzGröße`. Der Builder behandelt genau `1.0` als gegenüberliegende Kante und behält `1.0` bei; Pivots außerhalb von `0..1` können gültig sein. Lies vor manuellen Änderungen solcher Fälle die ausführliche Anleitung.
- BC7-DDS ist verlustbehaftet und reduziert den Texturspeicher. PNG ist der verlustfreie Standard und für Masken oft vorzuziehen. DDS benötigt einen DX10/BC7-Header, auf vier Pixel ausgerichtete Maße, getrennte 4×4-Kompressionsblöcke je Frame und für den Extender vertikal gedrehte Speicherung. JSON-Koordinaten bleiben bei Ursprung unten links. Atlas Builder übernimmt dieses Packen und Drehen.
- Ausgelassene originale Main- und Alt-Frames bleiben erhalten. Das gilt auch für benannte Gruppen in einem gemeinsam verwendeten internen Array; fülle partielle Atlanten nicht mit leeren Ersatzbildern auf.

Einzelheiten zum Export und zu Builder-Optionen stehen in der [Atlas-Builder-Anleitung](../Helpers/AtlasBuilder/USAGE.md#deutsch). Weitere Grafikwerkzeuge bietet [llm-art-pipeline](https://github.com/Ensrick/llm-art-pipeline).
