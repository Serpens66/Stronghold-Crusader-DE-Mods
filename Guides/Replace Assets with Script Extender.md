# Funktionsfähiger Ablauf für vollständige SH1DE-Grafik-Overrides

## Ergebnis

Das Atlas-System des Script Extenders 2.3.0 kann vollständige Einheiten- und Gebäudegruppen ersetzen. Für einen zuverlässigen Mod müssen jedoch **vollständige GM-Gruppen** erzeugt werden.

Partielle Atlanten sind wegen eines Fehlers im Script Extender derzeit nicht allgemein sicher. Außerdem müssen die einzelnen AssetRipper-Metadaten zu einer gemeinsamen `atlas.json` zusammengeführt werden. Dadurch wird die Erstellung unnötig aufwendiger, als der offizielle Guide vermuten lässt.

Geprüfte Version:

- Script Extender `2.3.0`
- Commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`

---

# Benötigte Werkzeuge

## AssetRipper

Damit werden die Unity-Atlanten, Spritebilder und Metadaten beider Spiele extrahiert.

- [Aktuelle AssetRipper-Version](https://github.com/AssetRipper/AssetRipper/releases/latest)
- [Direkter Download für Windows x64](https://github.com/AssetRipper/AssetRipper/releases/latest/download/AssetRipper_win_x64.zip)

## SHCDE Sprite Previewer

Hilfreich zum Anzeigen und Exportieren der SHCDE-Sprites und Masken.

- [Release v1.1.5](https://gitlab.com/rawra-stronghold-crusader/shcde-sprite-previewer/-/releases/v1.1.5)
- [Direkter Download für Windows x64](https://gitlab.com/api/v4/projects/77768698/packages/generic/shcde-sprite-previewer/1.1.5/shcde-sprite-viewer-win-x64.zip)

Der Previewer ist für SHCDE entwickelt worden. Für die SH1DE-Dateien ist AssetRipper der sicherere Ausgangspunkt.

---

# 1. Beide Spiele extrahieren

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

SHCDE bildet dabei immer die Zieldefinition. SH1DE liefert lediglich die neuen Bildpixel und gegebenenfalls Masken.

---

# 2. Eine unterstützte GM-Gruppe auswählen

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

# 3. Ziel- und Quellframes zuordnen

Für jeden SHCDE-Zielframe muss das inhaltlich passende SH1DE-Frame ermittelt werden.

| SHCDE-Ziel | SH1DE-Quelle | geprüft |
|---|---|---|
| `body_archer-0` | passendes SH1DE-Frame | ja/nein |
| `body_archer-1` | passendes SH1DE-Frame | ja/nein |
| `body_archer-1x` | passendes alternatives Frame | ja/nein |

Gleiche Namen oder Indices beweisen nicht, dass Richtung und Animationsphase identisch sind. Der Script Extender kontrolliert das nicht.

Mehrdeutige Zuordnungen sollten nicht automatisch übernommen werden. Verwende in solchen Fällen zunächst das originale SHCDE-Bild.

---

# 4. Immer die vollständige GM-Gruppe erzeugen

Für Script Extender 2.3.0 muss der Atlas enthalten:

- alle verwendeten Main-Frames
- alle verwendeten Alt-Frames
- den höchsten originalen Frameindex
- alle notwendigen Originalframes, für die kein SH1DE-Ersatz existiert

Wenn beispielsweise nur ein bestimmtes Gebäude aus `tile_workshops` ersetzt werden soll, sollte der neue Atlas trotzdem die komplette Gruppe enthalten:

- gewünschtes Gebäude aus SH1DE
- alle übrigen Frames aus SHCDE

Alternativ können wenige einzelne Sprites über `Override/Sprites/` ersetzt werden. Für tausende Frames ist das aber weniger effizient, weil jedes Bild als eigene Textur und mit einem eigenen Material verarbeitet wird.

---

# 5. Neuen Farbatlas und Maskenatlas packen

Erzeuge pro GM-Gruppe:

    atlas.png
    atlas_m.png
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

Ein einzelner globaler Atlas für das ganze Spiel wird nicht unterstützt. Jede GM-Gruppe benötigt einen eigenen Ordner und wird als eigene Textur geladen.

---

# 6. Teamfarbenmaske

`atlas_m.png` ist technisch optional, für Einheiten und teamgefärbte Gebäude aber normalerweise notwendig.

Sie muss:

- dieselbe Gesamtgröße wie `atlas.png` haben
- dasselbe Packlayout verwenden
- mit jedem Farbframe pixelgenau übereinstimmen

Ohne `atlas_m.png` weist der Script Extender der gesamten GM-Gruppe die einfachen Materialien ohne Teamfarbenmaske zu.

Die Bedeutung der Maskenkanäle ist im Script Extender nicht dokumentiert. Dafür sollten die originalen SHCDE-Masken im Sprite Previewer untersucht werden.

---

# 7. Gemeinsame `atlas.json` erzeugen

Für vollständige Gruppen muss das Mehrframeformat verwendet werden:

    {
      "pixelsPerUnit": 64,
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

Der verwendete Parser behandelt diese Namen effektiv case-sensitive.

---

# 8. Spritenamen

## Bindestrichformat

Typisch für Einheiten:

    body_archer-0
    body_archer-169
    body_archer-169x

Das kleine `x` am Ende kennzeichnet ein Alt-Frame. Ein großes `X` funktioniert nicht.

## Leerzeichenformat

Typisch für Gebäude- und Geländetiles:

    tile_buildings1 000
    tile_buildings1 001
    tile_buildings1 002x

Verwende immer die vollständigen Originalnamen aus SHCDE einschließlich Leerzeichen, Bindestrichen, Unterstrichen und führenden Nullen.

---

# 9. Koordinaten

Die JSON-Rechtecke verwenden einen Ursprung unten links.

Wenn das Atlasprogramm Koordinaten ab oben links ausgibt:

    unityY = atlasHeight - topY - frameHeight

Diese Umrechnung übernimmt der Script Extender nicht.

---

# 10. Pivot und Skalierung

Der Script Extender übernimmt Pivot und `pixelsPerUnit` unverändert aus der JSON.

Deshalb sollten vorzugsweise die SHCDE-Zielwerte übernommen werden.

Wenn sich durch andere transparente Ränder die Position des sichtbaren Fußpunkts ändert:

    pivotX = AnkerpositionX / Framebreite
    pivotY = AnkerpositionY / Framehöhe

`pixelsPerUnit` ist nicht zwingend 64. Der Parser verwendet 64 nur als Standard, wenn kein Wert angegeben wurde. Verwende den Wert des entsprechenden SHCDE-Zielsprites.

Die Ersatzbilder müssen technisch nicht dieselben Abmessungen wie die Originale besitzen. Bei abweichenden Größen müssen Pivot und gegebenenfalls PPU aber bewusst angepasst werden.

---

# 11. AssetRipper-JSONs zusammenführen

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

Dieses Format lädt in Version 2.3.0 nur genau ein Frame.

Der Extender sammelt nicht automatisch mehrere AssetRipper-JSON-Dateien. Ein Konvertierungsskript muss deshalb alle Metadaten in eine gemeinsame `frames`-Liste umwandeln.

Das ist einer der Punkte, durch die der aktuelle Script Extender den Arbeitsablauf unnötig kompliziert macht.

---

# 12. Ausgabeordner

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
            atlas_m.png
            atlas.json
          tile_workshops/
            atlas.png
            atlas_m.png
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

# 13. Validierung vor dem Spielstart

Der Konverter sollte je Gruppe prüfen:

- Gruppenname wird von Script Extender 2.3.0 unterstützt
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
- höchster Index entspricht mindestens dem höchsten SHCDE-Index
- alle tatsächlich vorhandenen SHCDE-Frames sind abgedeckt

Der Script Extender führt diese Prüfungen größtenteils nicht selbst durch.

---

# 14. Prüfung im Log

Erfolgreiche Registrierung:

    Registered atlas override for [body_archer] (...)
    Auto-registered atlas override for [body_archer] from [...]
    Applying 1 atlas override(s)...
    Applied [body_archer]: ... frames sliced, mask=yes, arraySize=...

Bei einem Fehler sind vor allem diese kurzen Meldungen relevant:

    Unknown GM file name [...]
    Found atlas.json but no atlas.png
    No frames parsed [...]
    No parseable frame indices [...]
    Failed to apply atlas [...]

---

# 15. Laufzeittest

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

Zuerst nur eine vollständige Gruppe wie `body_archer` umsetzen. Erst nach einem erfolgreichen Test den Konverter auf weitere Gruppen anwenden.

---

# Fehler im Script Extender, die den Ablauf betreffen

## Kurzbericht für den Autor

### Atlas override issues in v2.3.0

1. Partial overrides truncate `gmSprites` and `gmAltSprites` because `arraySize` is only `maxFrameIdx + 1`. It should preserve at least the original array lengths.
2. Preserved original sprites may use incorrect team masks when the replacement atlas has a different layout.
3. The raw AssetRipper schema loads only one frame from the single discovered `atlas.json`; multiple raw frame files are not collected.
4. Documentation says all sprite groups are supported, but automatic discovery is restricted to the hard-coded `_gmFileNameToEnum` table.

Until these issues are fixed, complete GM-group replacement with the multi-frame JSON format is the reliable workflow.

---

# Schlussfolgerung

Der Guide funktioniert zuverlässig, wenn folgende Regeln eingehalten werden:

1. Nicht ganze SH1DE-AssetBundles kopieren.
2. SHCDE als verbindliche Zielstruktur verwenden.
3. Pro GM-Gruppe einen vollständigen Atlas erzeugen.
4. Farb- und Maskenatlas identisch packen.
5. AssetRipper-Raw-JSONs zu einer gemeinsamen `frames`-Liste konvertieren.
6. Keine partiellen Atlas-Overrides verwenden.
7. Vor der Installation sämtliche Namen, Indices, Rechtecke, Pivots und Masken validieren.

Der sinnvollste nächste Schritt wäre deshalb nicht die manuelle Erstellung tausender Dateien, sondern ein Konvertierungsprogramm, das die AssetRipper-Ausgaben beider Spiele einliest und daraus automatisch validierte SHCDE-Atlasordner erzeugt.