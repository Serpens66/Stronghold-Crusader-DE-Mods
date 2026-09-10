# SHCDE Atlas Builder

## Deutsch

`AtlasBuilder.exe` erzeugt Sprite-Atlanten im Format der Asset-API des SHCDE Script Extenders. Die portable Ausgabe benötigt kein installiertes Python.

**Alle Angaben und erzeugten Atlanten setzen Script Extender 2.4.0 oder neuer voraus.**

### Allgemeine Anwendung

#### Schnellstart

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

#### Pivotquelle und korrekte Ausrichtung

Ein Unity-Pivot ist normalisiert. Derselbe Wert bezeichnet deshalb auf unterschiedlich großen Bildern einen anderen Pixel. Das kann Bodenplatten auseinanderziehen oder Gebäude- und Animationsteile gegeneinander verschieben.

- **SHCDE-Pixelanker beibehalten** ist der Standard und für eigene Ersatzbilder normalerweise richtig. Der Builder berechnet den Pixelanker des SHCDE-Ziels und normalisiert ihn für die tatsächliche Größe des Ersatzbildes neu.
- **Pivot aus Quellmetadaten** reproduziert die ursprüngliche Ausrichtung eines mit AssetRipper extrahierten Sprites. Wähle zusätzlich den Ordner, der die einzelnen Sprite-JSONs enthält, oder einen übergeordneten AssetRipper-Exportordner. Der Builder liest nur JSON-Dateien, deren Namen zur gewählten Gruppe und zu den vorhandenen Frames passen.
- **Normalisierten SHCDE-Pivot übernehmen (Legacy)** bildet das Verhalten älterer Builder-Projekte nach. Verwende es nur, wenn Quell- und Zielbilder dieselbe Leinwand besitzen oder die normalisierten Pivots absichtlich identisch sein sollen.

Ein Pivot von exakt `1,0` ist eine Kantenkonvention: Er bleibt bei einer anders großen Leinwand `1,0`, damit die obere beziehungsweise rechte Kante fest bleibt. Pivots außerhalb von `0..1` sind ebenfalls gültig und kommen in SHCDE tatsächlich vor; der Builder prüft daher den berechneten Pixelanker statt den Pivot künstlich auf diesen Bereich zu begrenzen.

Bei Quellmetadaten bleiben normalisierte Pivots bei einer proportional skalierten Leinwand erhalten. Ändert sich nur die Leinwand beziehungsweise ihr Seitenverhältnis, bewahrt der Builder den absoluten Quellanker. Eine Warnung weist auf mögliches Trimming hin, weil dessen korrekter Ausgleich nicht allein aus Rect und PNG-Größe ableitbar ist.

Bei den geprüften SH1DE-Gruppen `tile_land8`, `tile_buildings1`, `tile_churches` und `tile_ruins` liegt der originale Pixelanker durchgehend bei `(32, 16,5)` und die PPU bei 64. Beispielsweise ergeben sowohl Pivot `(0,5; 0,40243897)` auf 64×41 Pixeln als auch `(0,5; 0,08418399)` auf 64×196 Pixeln denselben Anker. Schwarze Spalten zwischen Tiles sind ein typisches Zeichen dafür, dass stattdessen ein normalisierter Pivot von einer anders großen Leinwand kopiert wurde.

Schema-1-Projekte werden kompatibel im Legacy-Modus geöffnet und beim Öffnen gewarnt. Schema-2-Projekte behalten ihre bisherige strikte Zielprüfung. Beim nächsten Speichern werden ältere Projekte als Schema 3 abgelegt.

### Grafiken aus Stronghold 1 DE übertragen

#### Tight-Mesh-Sprites korrekt extrahieren

Der Atlas Builder erwartet bereits korrekt extrahierte Einzel-PNGs. Er schneidet keine Sprites aus einem Quellatlas aus und kann Verunreinigungen in den Eingabebildern nicht nachträglich reparieren.

Bei AssetRipper-Daten darf die Quelltextur nicht aus dem Sprite- oder GM-Gruppennamen abgeleitet werden. Maßgeblich ist `m_RD.m_Texture` innerhalb der angegebenen `m_Collection`. Eine falsche Textur kann sämtliche Rechteck-, Pivot- und FullRect-Prüfungen bestehen und trotzdem gültige fremde Pixel liefern. Deshalb müssen der SHA-256 des verwendeten Quellatlas und mindestens ein bekannter Referenzframe geprüft werden. Bestätigtes SH1DE-Beispiel: `anim_castle` referenziert PathID `26` auf `alltiles/AllTileSprites.png`, nicht auf `anims1Sprites.png`.

Bei Unity-Sprites mit **Tight Mesh** darf der gemeinsame Farb- oder Maskenatlas nicht einfach rechteckig anhand von `m_Rect` ausgeschnitten werden. Das Rechteck kann Pixel benachbarter Atlasobjekte enthalten; außerdem können die tatsächlichen UV-Vertices über einzelne `m_Rect`-Kanten hinausragen. Das wurde an den bereitgestellten SH1DE-Swordsman-Daten bestätigt: Alle 1.216 Frames besitzen Meshdaten, und einzelne UV-Meshes überschreiten eine `m_Rect`-Kante um bis zu ungefähr 27 Pixel.

Ein geeigneter SH1DE-Extraktor muss daher:

1. Positionen und UVs aus `m_RD.m_VertexData` sowie die Dreiecke aus `m_RD.m_IndexBuffer` und `m_SubMeshes` lesen;
2. den benötigten Ausschnitt aus den UV-Meshgrenzen bestimmen, nicht allein aus `m_Rect`;
3. die indizierten Dreiecke für Farb- und Maskenatlas identisch rasterisieren und alle Pixel außerhalb der Dreiecksfläche transparent setzen;
4. den Anker und den Pivot für die neue Ausgabeleinwand wie unten beschrieben neu berechnen;
5. korrigierte Quellmetadaten mit tatsächlicher PNG-Größe und neu normalisiertem Pivot ausgeben.

Beim Dekodieren von `m_VertexData` darf nicht angenommen werden, dass Positions- und UV-Werte beliebig oder nach einem universellen festen Schema verschachtelt sind. Der Extraktor muss alle aktiven Einträge in `m_Channels` auswerten und Kanalnummer, Stream, Offset, Format und Dimension gegen ein unterstütztes Layout prüfen. In den geprüften SH1DE-Tight-Mesh-Daten liegen Positionen als drei Floatwerte in Stream 0 und UVs als zwei Floatwerte in Stream 1; der UV-Block beginnt nach dem Positionsblock an der nächsten 16-Byte-Grenze:

    uvStreamStart = align16(vertexCount * 3 * sizeof(float))

Der berechnete Streamanfang und die erwartete Datenlänge müssen exakt zu `m_Data` passen. Abweichende oder unbekannte Kanal- und Streamlayouts müssen fail-closed abgelehnt werden, statt sie mit diesem bestätigten SH1DE-Schema zu dekodieren.

Eine reine Rechteckextraktion kann farbige Fragmente anderer Sprites erzeugen. Der Builder packt solche bereits verunreinigten Pixel anschließend unverändert und besteht dabei zu Recht seinen Pixelvergleich, weil Quelle und gebauter Atlas identisch sind. Weder eine andere Pivotoption noch ein größerer Packabstand behebt diesen Eingabefehler.

#### FullRect-Sprites ohne brauchbaren UV-Stream

AssetRipper kann bei **FullRect-Sprites** einen vorhandenen, aber vollständig genullten UV-Stream exportieren. Das wurde bei allen geprüften SH1DE-Frames von `tile_castle` beobachtet. Ein solcher Stream enthält keine verwertbaren Atlas-UVs und darf deshalb nicht für die oben beschriebene Tight-Mesh-Rekonstruktion verwendet werden.

Ein FullRect-Sprite kann stattdessen als rechteckig extrahierbar akzeptiert werden, wenn alle folgenden Prüfungen erfüllt sind:

- `m_IsPolygon == false`;
- exakt vier Vertices und sechs Dreiecksindizes;
- ein integrales `m_Rect`, das vollständig innerhalb des Quellatlas liegt;
- Vertexausdehnung multipliziert mit `m_PixelsToUnits` entspricht Breite und Höhe von `m_Rect`;
- die aus `m_Rect` und `m_Pivot` abgeleitete Lage stimmt mit `m_UvTransform` überein.

Schlägt eine dieser Prüfungen fehl, darf ein genullter UV-Stream nicht stillschweigend als FullRect behandelt werden. Die Extraktion muss dann abbrechen oder durch eine separat belegte Metadatenquelle abgesichert werden.

Unity-Rechtecke verwenden den Ursprung links unten. PNG-Bibliotheken verwenden häufig den Ursprung links oben. Für einen rechteckigen Crop muss die obere Y-Koordinate deshalb so umgerechnet werden:

    topY = atlasHeight - (rectY + rectHeight)

#### Ankerpunkte aus den gerippten Daten wiederherstellen

Die ursprünglichen Ankerpunkte müssen nicht geschätzt werden. Bei einem korrekt gerippten Sprite lassen sie sich aus einer zusammengehörenden lokalen Meshposition `(vertexX, vertexY)`, deren Atlas-UV `(uvX, uvY)`, der Atlasgröße und `m_PixelsToUnits` (`PPU`) rekonstruieren:

    anchorX = uvX * atlasWidth  - vertexX * PPU
    anchorY = uvY * atlasHeight - vertexY * PPU

Mehrere Vertices desselben Sprites müssen bis auf kleine Rundungsabweichungen denselben Anker ergeben. Nach dem Tight-Mesh-Ausschnitt mit der linken unteren Ecke `(cropLeft, cropBottom)` wird dieser Pixelanker auf die neue PNG-Leinwand normalisiert:

    newPivotX = (anchorX - cropLeft)   / newWidth
    newPivotY = (anchorY - cropBottom) / newHeight

Der Extraktor muss diese berechneten Werte zusammen mit der tatsächlichen PNG-Größe in korrigierte Sprite-JSONs schreiben. Im Atlas Builder wird anschließend **Pivot aus Quellmetadaten** gewählt und der Ordner mit diesen korrigierten JSONs angegeben. Die unveränderte AssetRipper-Datei beschreibt noch die ursprüngliche Atlas-/Rect-Geometrie; ihr `m_Pivot` darf nach einem anders zugeschnittenen Einzel-PNG nicht blind übernommen werden.

Für Farb- und Maskenbild müssen exakt derselbe Ausschnitt, dieselbe Dreiecksmaske und derselbe Pivot verwendet werden. Stehen nur bereits falsch rechteckig ausgeschnittene PNGs ohne ursprünglichen Atlas und ohne Vertex-/UV-/Indexdaten zur Verfügung, lässt sich der exakte Anker normalerweise nicht mehr zuverlässig rekonstruieren.

Eine allgemeine automatische Tight-Mesh-Prüfung ist aus einem Einzel-PNG und den normalen Basisfeldern `m_Rect`/`m_Pivot` nicht eindeutig möglich. Dafür müsste zusätzlich die genaue Extraktionstransformation oder ein vertrauenswürdiger Mesh-Sidecar mit lokalen Vertices und Dreiecken vorliegen. Bei eigenen Ersatzgrafiken können Pixel außerhalb des ursprünglichen Unity-Meshes außerdem beabsichtigt sein. Deshalb erzwingt der Builder derzeit keine solche Prüfung.

#### Empfohlener SH1DE-Ablauf

1. Den originalen SH1DE-Farb- und gegebenenfalls Maskenatlas sowie die Sprite-Metadaten vollständig rippen.
2. Tight-Mesh-Frames dreiecksbasiert extrahieren und dabei korrigierte Einzel-PNGs und JSONs erzeugen.
3. In Atlas Builder die passende SHCDE-GM-Gruppe und das gewünschte SH1DE-Quellpräfix auswählen.
4. **Pivot aus Quellmetadaten** und den Ordner mit den korrigierten JSONs einstellen.
5. Fehlende SHCDE-Zielslots grundsätzlich ablehnen und nur bei einem nachgewiesenen leeren Zielbereich die unten beschriebene Opt-in-Regel verwenden.
6. Vorschau und Warnungen prüfen, den Atlas bauen und Ausrichtung sowie Animation im Spiel testen.

#### Rundturm: `tile_castle` und `anim_castle` gemeinsam prüfen

Der SHCDE-Rundturm wird nicht aus nur einer GM-Gruppe gezeichnet. `tile_castle` enthält den statischen Turmkörper; eine zusätzliche obere Ebene mit Aufbauten beziehungsweise Animationen stammt aus `GM_CASTLE_ANIMS` und der Sprite-Gruppe `anim_castle`. Beim untersuchten `SkinTest` wurde nur `tile_castle` ersetzt. Das Laufzeitlog belegt für die weiterhin braune Turmkrone einen unveränderten Aufruf von `anim_castle 047`. Der neue Pivotmodus kann Teile korrekt ausrichten, ersetzt aber keine Grafik aus einer nicht konfigurierten Gruppe.

Ob die Quelldateien in SH1DE unter `alltiles` und in SHCDE unter `sprites` liegen, bestimmt nicht die Zielgruppe. Maßgeblich sind der tatsächliche Spritename und dessen SHCDE-GM-Zuordnung. Für einen allgemeinen, unbedingten Austausch müssen daher `tile_castle` und `anim_castle` untersucht und gegebenenfalls als getrennte Atlanten gebaut werden. Der Builder ordnet fehlende `anim_castle`-Frames niemals heuristisch `tile_castle` oder anderen Indizes zu.

Auch `anim_castle` ist zwischen den geprüften Spielen nicht vollständig deckungsgleich:

- SH1DE besitzt 122 Frames im lückenhaften Bereich `1–127`;
- SHCDE besitzt 106 Frames im lückenhaften Bereich `1–138`;
- 95 Frames sind gemeinsam;
- 27 Frames gibt es nur in SH1DE: `15–18`, `25–26`, `36–43`, `84–89`, `91–93`, `122–125`;
- 11 Frames gibt es nur in SHCDE: `128–138`.

Ein globaler SH1DE-Atlas, der bei Index `127` endet, bewahrt die nachfolgenden SHCDE-Frames `128–138` als Vanilla-Grafiken. Fehlende direkte Entsprechungen müssen weiterhin manuell vorbereitet werden, denn der Builder erfindet keine Ersatzgrafiken. Für einen kulturabhängigen Austausch wie `SkinTest` bleiben private Atlanten mit Runtime-Auswahl erforderlich. Die unten beschriebene abweichende `tile_castle`-Indexmenge ist ein zusätzliches Kompatibilitätsthema, aber nicht die belegte Hauptursache der braunen Turmkrone.

#### Sonderfall `tile_castle`: unterschiedliche Framebereiche

Die geprüften Versionen besitzen keine deckungsgleichen `tile_castle`-Indexmengen:

- SH1DE enthält 1.467 Frames und reicht bis Index `1569`;
- die SH1DE-Indizes `812–1071` existieren in SHCDE nicht;
- SHCDE besitzt dafür zusätzliche Indizes `1570–1596`;
- damit überlappen nur 1.207 Frames.

Ein globaler `Override/Atlas/tile_castle` bewahrt ausgelassene SHCDE-Frames einschließlich der nachfolgenden Indizes `1570–1596`. SH1DE-only-Indizes dürfen dennoch nur mit nachgewiesener Zuordnung und validierten Quellmetadaten ergänzt oder müssen bewusst ausgelassen werden. Ein privater Atlas ist nötig, wenn ein Runtime-Mod beispielsweise kulturabhängig zwischen Grafiksätzen umschalten soll.

#### UI-Masteratlanten ohne Sprite-Metadaten

Bei UI-Masteratlanten ohne einzelne Sprite-Metadaten lässt sich der Crop nicht über den normalen Spritevertrag verifizieren. Für jeden Ausschnitt sollten mindestens SHA-256 der Quelldatei, Atlasbreite und -höhe, Crop-Rechteck sowie verwendeter Koordinatenursprung gespeichert werden. Die Crop-Ränder müssen zusätzlich visuell oder durch eine Alpharandprüfung darauf kontrolliert werden, ob Fragmente benachbarter Motive enthalten sind. Diese Angaben machen den Export reproduzierbar, ersetzen aber keine fehlenden Pivot- oder Mesh-Metadaten.

### In SHCDE fehlende Zielslots

Normalerweise muss **In SHCDE fehlende Zielslots** auf **Ablehnen** stehen. Manche vom Spiel deklarierten Arrays enthalten jedoch echte leere Slots. Bestätigtes Beispiel: `body_swordsman` besitzt den Bereich `0–1087`, aber SHCDE enthält keine Sprites für `416–447`, während SH1DE diese 32 Frames besitzt.

Nur für einen belegten Fall kann pro Gruppe **Aus validierten Quellmetadaten ergänzen** gewählt werden. Das ist ausschließlich zusammen mit **Pivot aus Quellmetadaten** möglich. Der Builder prüft dann Quelldateiname, `m_Name`, Präfix, Index, Alt-Suffix, Rect, Pivot und PPU, lässt keine Nummer außerhalb des vom SHCDE-Loader deklarierten Arrays zu und ergänzt nur tatsächlich fehlende Zieldaten. Vorhandene SHCDE-Metadaten haben immer Vorrang.

Der ausgegebene Spritename wird aus der Ziel-GM-Gruppe erzeugt. Das ist wichtig, wenn das Quellpräfix absichtlich anders lautet: Der Script Extender erkennt nur Zielnamen wie `body_swordsman-416`, nicht einen beliebigen Namen aus dem Quellspiel. Eine deutliche Buildwarnung nennt Anzahl und Bereiche aller ergänzten Slots.

### Material, Masken und Teilatlanten

- Der Builder kennt den Material- und Maskenvertrag aller 195 registrierten Gruppen und schreibt `Plain`, `TeamColour` oder `Foliage` ausdrücklich in `atlas.json`.
- Plain-Gruppen wie `tile_ruins` werden ohne Maske gebaut; TeamColour- und Foliage-Gruppen benötigen eine vollständige Maske.
- Teilatlanten sind zulässig. Nicht enthaltene SHCDE-Frames bleiben einschließlich Lücken und nachfolgender Indizes als Vanilla-Grafiken erhalten.
- `tile_sea_new_01` und `tile_sea_shore` werden trotz ihres gemeinsam genutzten Sprite-Arrays korrekt über ihre Gruppenoffsets zugeordnet.

## English

`AtlasBuilder.exe` creates sprite atlases for the SHCDE Script Extender Asset API. The portable package does not require Python to be installed.

**All instructions and generated atlases require Script Extender 2.4.0 or newer.**

### General use

#### Quick start

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

#### Pivot source and correct alignment

A Unity pivot is normalized, so the same value points to a different pixel on images with different dimensions. This can separate ground tiles or shift building and animation parts relative to each other.

- **Preserve SHCDE pixel anchor** is the default and is normally correct for custom replacements. The builder calculates the SHCDE target's pixel anchor and normalizes it for the replacement image's actual dimensions.
- **Pivot from source metadata** reproduces the original alignment of a Sprite extracted with AssetRipper. Also select the directory containing the individual Sprite JSON files, or a parent AssetRipper export directory. Only JSON files whose names match the selected group and present frames are read.
- **Copy normalized SHCDE pivot (legacy)** preserves the behavior of older builder projects. Use it only when source and target images have identical canvases or intentionally share normalized pivots.

A pivot of exactly `1.0` is an edge convention: it remains `1.0` on a differently sized canvas so the top or right edge stays fixed. Pivots outside `0..1` are valid as well and occur in SHCDE itself; the builder therefore validates the calculated pixel anchor instead of artificially restricting the normalized value.

With source metadata, normalized pivots remain unchanged when the canvas is scaled proportionally. If only the canvas or its aspect ratio changes, the builder preserves the absolute source anchor. A warning identifies possible trimming because its correct compensation cannot be inferred from the Rect and PNG dimensions alone.

In the verified SH1DE groups `tile_land8`, `tile_buildings1`, `tile_churches` and `tile_ruins`, the original pixel anchor is consistently `(32, 16.5)` with 64 PPU. For example, pivot `(0.5, 0.40243897)` on a 64×41 image and `(0.5, 0.08418399)` on a 64×196 image both produce the same anchor. Black gaps between tiles are a typical symptom of copying a normalized pivot from a differently sized canvas.

Schema-1 projects open compatibly in legacy mode and display a warning. Schema-2 projects retain their previous strict target validation. Saving an older project upgrades it to schema 3.

### Transferring graphics from Stronghold 1 DE

#### Correctly extract Tight Mesh Sprites

The Atlas Builder expects correctly extracted individual PNGs. It does not cut Sprites out of a source atlas and cannot repair contamination that is already present in its input images.

With AssetRipper data, the source texture must not be inferred from the Sprite or GM-group name. The authoritative reference is `m_RD.m_Texture` within the specified `m_Collection`. A wrong texture can pass every rectangle, pivot and FullRect check while still returning valid pixels belonging to another asset. Therefore, verify the source atlas SHA-256 and at least one known reference frame. Confirmed SH1DE example: `anim_castle` references PathID `26` to `alltiles/AllTileSprites.png`, not `anims1Sprites.png`.

For Unity Sprites using a **Tight Mesh**, do not crop the shared colour or mask atlas as a rectangle based only on `m_Rect`. That rectangle can contain pixels belonging to neighbouring atlas objects, and the actual UV vertices may extend beyond individual `m_Rect` edges. This was confirmed in the supplied SH1DE swordsman data: all 1,216 frames contain mesh data, and individual UV meshes extend beyond an `m_Rect` edge by up to approximately 27 pixels.

A suitable SH1DE extractor must therefore:

1. read positions and UVs from `m_RD.m_VertexData`, and triangles from `m_RD.m_IndexBuffer` and `m_SubMeshes`;
2. determine the required crop from the UV mesh bounds rather than from `m_Rect` alone;
3. rasterize the indexed triangles identically for the colour and mask atlases, clearing every pixel outside the triangle area to transparency;
4. recalculate the anchor and pivot for the new output canvas as described below;
5. emit corrected source metadata containing the actual PNG dimensions and newly normalized pivot.

When decoding `m_VertexData`, position and UV values must not be assumed to be arbitrarily interleaved or to follow one universal fixed layout. The extractor must inspect every active `m_Channels` entry and validate channel number, stream, offset, format and dimension against a supported layout. In the examined SH1DE Tight Mesh data, positions are three float values in stream 0 and UVs are two float values in stream 1; the UV block begins after the position block at the next 16-byte boundary:

    uvStreamStart = align16(vertexCount * 3 * sizeof(float))

The calculated stream start and expected total data length must match `m_Data` exactly. Differing or unknown channel and stream layouts must be rejected fail-closed rather than decoded using this confirmed SH1DE layout.

A rectangular crop can introduce coloured fragments from unrelated Sprites. The builder then preserves those already contaminated pixels exactly and correctly passes its pixel comparison because the input and generated atlas match. Changing the pivot mode or increasing the packing gap cannot fix this input defect.

#### FullRect Sprites without a usable UV stream

AssetRipper can export a present but entirely zero-filled UV stream for **FullRect Sprites**. This was observed for every examined SH1DE `tile_castle` frame. Such a stream contains no usable atlas UVs and must not be used for the Tight Mesh reconstruction described above.

A FullRect Sprite may instead be accepted for rectangular extraction when all of the following checks pass:

- `m_IsPolygon == false`;
- exactly four vertices and six triangle indices;
- an integral `m_Rect` located completely within the source atlas;
- vertex extent multiplied by `m_PixelsToUnits` equals the width and height of `m_Rect`;
- the placement derived from `m_Rect` and `m_Pivot` agrees with `m_UvTransform`.

If any check fails, a zero-filled UV stream must not silently be treated as FullRect. Extraction must stop or be supported by a separately verified metadata source.

Unity rectangles use a bottom-left origin, while PNG libraries commonly use a top-left origin. Convert the upper Y coordinate for a rectangular crop as follows:

    topY = atlasHeight - (rectY + rectHeight)

#### Reconstructing anchors from ripped data

The original anchors do not need to be guessed. For a correctly ripped Sprite, they can be reconstructed from a matching local mesh position `(vertexX, vertexY)`, its atlas UV `(uvX, uvY)`, the atlas dimensions, and `m_PixelsToUnits` (`PPU`):

    anchorX = uvX * atlasWidth  - vertexX * PPU
    anchorY = uvY * atlasHeight - vertexY * PPU

Multiple vertices of the same Sprite must produce the same anchor apart from small rounding differences. After taking the Tight Mesh crop whose lower-left corner is `(cropLeft, cropBottom)`, normalize that pixel anchor for the new PNG canvas:

    newPivotX = (anchorX - cropLeft)   / newWidth
    newPivotY = (anchorY - cropBottom) / newHeight

The extractor must write these calculated values and the actual PNG dimensions to corrected Sprite JSON files. Then select **Pivot from source metadata** in Atlas Builder and provide the directory containing those corrected JSON files. The unchanged AssetRipper file still describes the original atlas/Rect geometry; its `m_Pivot` must not be copied blindly after producing an individual PNG with a different crop.

The colour and mask image must use exactly the same crop, triangle mask and pivot. If only incorrectly rectangular-cropped PNGs remain and the original atlas and vertex/UV/index data are unavailable, the exact anchor normally cannot be reconstructed reliably.

A general Tight Mesh check cannot be derived unambiguously from an individual PNG and the normal `m_Rect`/`m_Pivot` fields alone. It would additionally require the exact extraction transform or a trusted mesh sidecar containing local vertices and triangles. Pixels outside the historical Unity mesh may also be intentional in custom replacement artwork. The builder therefore does not currently enforce such a check.

#### Recommended SH1DE workflow

1. Rip the original SH1DE colour atlas, optional mask atlas and complete Sprite metadata.
2. Extract Tight Mesh frames by rasterizing their triangles, producing corrected individual PNGs and JSON files.
3. In Atlas Builder, select the corresponding SHCDE GM group and the desired SH1DE source prefix.
4. Select **Pivot from source metadata** and the directory containing the corrected JSON files.
5. Reject absent SHCDE target slots by default; use the opt-in rule below only for a verified empty target range.
6. Review the preview and warnings, build the atlas, then test alignment and animation in game.

#### Round tower: inspect `tile_castle` and `anim_castle` together

The SHCDE round tower is not drawn from a single GM group. `tile_castle` contains the static tower body, while an additional upper layer containing structures or animation comes from `GM_CASTLE_ANIMS` and the `anim_castle` Sprite group. The examined `SkinTest` replaced only `tile_castle`. Its runtime log confirms an unchanged call to `anim_castle 047` for the brown tower crown. The new pivot mode can align parts correctly, but it cannot replace artwork from a group that was never configured.

Whether source files are stored below `alltiles` in SH1DE or below `sprites` in SHCDE does not determine the target group. The actual Sprite name and its SHCDE GM assignment do. A general unconditional replacement must therefore inspect and, where required, build `tile_castle` and `anim_castle` as separate atlases. The builder never heuristically maps missing `anim_castle` frames to `tile_castle` or other indices.

The verified `anim_castle` sets are not identical either:

- SH1DE has 122 frames in the sparse range `1–127`;
- SHCDE has 106 frames in the sparse range `1–138`;
- 95 frames are shared;
- 27 frames exist only in SH1DE: `15–18`, `25–26`, `36–43`, `84–89`, `91–93`, `122–125`;
- 11 frames exist only in SHCDE: `128–138`.

A global SH1DE atlas ending at index `127` preserves the trailing SHCDE frames `128–138` as vanilla artwork. Missing direct counterparts must still be prepared manually because the builder does not invent replacement artwork. Culture-dependent replacement such as `SkinTest` continues to require private atlases and runtime selection. The differing `tile_castle` index set described below is an additional compatibility concern, but it is not the confirmed main cause of the brown crown.

#### Special case `tile_castle`: differing frame ranges

The examined game versions do not have matching `tile_castle` index sets:

- SH1DE contains 1,467 frames and reaches index `1569`;
- SH1DE indices `812–1071` are absent from SHCDE;
- SHCDE instead has additional indices `1570–1596`;
- only 1,207 frames therefore overlap.

A global `Override/Atlas/tile_castle` preserves omitted SHCDE frames, including trailing indices `1570–1596`. SH1DE-only indices must still be added only with a proven mapping and validated source metadata, or deliberately omitted. A private atlas is required when a runtime mod needs conditional selection, for example between culture-dependent artwork sets.

#### UI master atlases without Sprite metadata

For UI master atlases without individual Sprite metadata, the crop cannot be verified through the normal Sprite contract. Store at least the source file SHA-256, atlas width and height, crop rectangle, and coordinate origin used for every crop. Crop borders must also be inspected visually or with an alpha-border check for fragments belonging to adjacent artwork. This information makes extraction reproducible but does not replace missing pivot or mesh metadata.

### Target slots absent from SHCDE

Normally, **Target slots absent from SHCDE** must remain set to **Reject**. Some arrays declared by the game contain genuine empty slots. Confirmed example: `body_swordsman` declares the range `0–1087`, but SHCDE has no Sprites for `416–447`, while SH1DE contains those 32 frames.

For a verified case only, select **Fill from validated source metadata** for that group. This option is available exclusively with **Pivot from source metadata**. The builder then validates the source filename, `m_Name`, prefix, index, alternate suffix, Rect, pivot and PPU, refuses indices outside the array declared by the SHCDE loader, and fills only target metadata that is genuinely absent. Existing SHCDE metadata always takes precedence.

The emitted Sprite name is constructed from the target GM group. This matters when the source prefix intentionally differs: the Script Extender recognizes target names such as `body_swordsman-416`, not an arbitrary source-game name. A prominent build warning lists the number and ranges of all filled slots.

### Materials, masks and partial atlases

- The builder knows the material and mask contract of all 195 registered groups and writes `Plain`, `TeamColour` or `Foliage` explicitly to `atlas.json`.
- Plain groups such as `tile_ruins` are built without a mask; TeamColour and Foliage groups require a complete mask.
- Partial atlases are supported. Omitted SHCDE frames, including gaps and trailing indices, remain as vanilla artwork.
- `tile_sea_new_01` and `tile_sea_shore` are assigned correctly through their group offsets despite sharing one Sprite array.
