# atlas-contract

Vier Dinge, die ein SHCDE-SE-Atlas aus dem Spiel wissen muss und die sich aus
den gelieferten PNG-Dateien nicht ableiten lassen. Als Python-Paket, ohne
Abhängigkeiten außer Pillow und, für die Spielzugriffe, UnityPy.

Das Paket ist aus einer Kette herausgelöst, mit der wir rund 150 Gruppen aus
Stronghold 1 DE nach Crusader DE übertragen haben. Es enthält nur die
allgemeingültigen Teile, keine Projektlogik.

## Das Problem, kurz

**Der Anker.** `Sprite.Create` bekommt den Pivot als Bruchteil der
Sprite-Größe. Der absolute Anker ist `Pivot * eigene Größe`. Wer Crusaders
Pivot unverändert auf ein anders großes Bild schreibt, verschiebt es um
`Pivot * (Quellgröße - Zielgröße)` Pixel. Bei Bodenkacheln reichen wenige
Pixel, damit die Kachel ihr Feld nicht mehr füllt und eine schwarze Naht
sichtbar wird.

**Die Eckkonvention.** Führt der Zielslot seinen Anker an der oberen oder
rechten Kante, also mit normalisiertem Pivot exakt 1,0, dann ist der Abstand
dorthin die feste Größe, und der ist null. Rechnet man dort nach der ersten
Regel, verschiebt sich das Bild um `Zielhöhe - Quellhöhe`. Gemessen an
`anim_castle`, wo alle Vordergrund- und Abschlussstücke Pivot (0, 1) tragen.

**Die Arraylänge.** `GameAtlasManagerAPI.ApplySingle` legt das Sprite-Array mit
`höchster Index in der atlas.json + 1` Slots an und kopiert nur die
Vanilla-Einträge, die hineinpassen. Ein Atlas mit den Frames 0 bis 72 macht aus
148 Slots 73. Der Rest ist weg.

**Die Gruppe selbst.** Zwei der 195 Ladegruppen teilen sich ein Array und
dürfen als Atlas gar nicht überschrieben werden. 69 werden ohne Maske geladen.
7 zeichnet Vanilla mit einem anderen Shader.

## Installation

```
pip install pillow unitypy
```

Das Paket selbst ist ein Ordner. Kopieren reicht.

## Die vier Befehle

```bash
# Was das Spiel über eine Gruppe weiß, und ob ein Atlas dort erlaubt ist
python -m atlas_contract gruppe tile_sea_shore

# Fehlende Slots als unveränderte Crusader-Kopie ergänzen
python -m atlas_contract fuellen tree_birch \
    --spiel ".../Stronghold Crusader Definitive Edition_Data" \
    --bilder ./meine_birken

# Atlas bauen, mit Rückschnitt- und Ankerprobe
python -m atlas_contract bauen tree_birch \
    --spiel ".../Stronghold Crusader Definitive Edition_Data" \
    --bilder ./meine_birken \
    --ausgabe ./Override/Atlas/tree_birch

# Einen fertigen Atlas nachrechnen, egal womit er gebaut wurde
python -m atlas_contract pruefen tree_birch \
    --spiel ".../Stronghold Crusader Definitive Edition_Data" \
    --atlas ./Override/Atlas/tree_birch
```

`pruefen` ist auch für fremde Atlanten gedacht. Es liest nur `atlas.json`,
`atlas.png` und `atlas_m.png` und vergleicht sie mit dem Zielspiel.

### Die Ankerregel: die wichtigste Entscheidung

Der Pivot gehört zur **Zeichnung**, nicht zum Slot. Das ist gemessen, nicht
vermutet: wo beide Spiele dieselbe Grafik führen, führen sie auch denselben
Pivot, auf 65 von 65 Frames bis auf 0,00 px. Und innerhalb von Crusader liegt
der Anker je nach Baumgruppe zwischen 3 und 50 px über der sichtbaren
Unterkante. Es gibt also keinen einheitlichen Slot-Anker, dem man sich anpassen
könnte.

Daraus folgen drei Fälle, und `bauen` wie `pruefen` kennen alle drei:

`--anker ziel` (Voreinstellung)
: Der Frame hält den Anker des Crusader-Slots. Richtig, wenn die Zeichnung
  dieselbe bleibt und nur die Leinwand eine andere ist. **Bodenkacheln fallen
  immer hierunter**, und genau dort entstehen die schwarzen Nähte.

`--anker quelle --quellspiel <..._Data>`
: Der Frame hält den Anker des Sprites, aus dem er stammt. Richtig für einen
  echten Grafiktausch, etwa eine Birke aus Stronghold 1 in einem Palmen-Slot.
  Eine Birke an den Anker einer Palme zu hängen, wäre falsch. Slots ohne eigene
  Vorlage dürfen dabei den Zielanker tragen, das ist der erwartete Mischfall
  und wird nicht beanstandet.

`--anker frei`
: Der Versatz wird nur gemessen und gemeldet. Für neu gezeichnete Grafik, deren
  Anker bewusst woanders liegt.

Wer unsicher ist: `--anker frei` einmal laufen lassen. Der Bericht nennt die
größte Abweichung in Pixeln, und daran sieht man, ob die Frage überhaupt eine
Rolle spielt. Bei gleich großer Leinwand liefern alle drei Regeln dasselbe.

## Als Baustein statt als Werkzeug

Wer einen eigenen Atlasbauer hat, nimmt nur die Bausteine. Siehe
[EINBAU.md](EINBAU.md) für den konkreten Einbau in den AtlasBuilder,
[ANKER.md](ANKER.md) für die Herleitung der Ankerregeln und
[BEFUNDE_SCRIPT_EXTENDER.md](BEFUNDE_SCRIPT_EXTENDER.md) für alles, was wir am
Loader gemessen haben.

```python
from atlas_contract import reanchored_pivot, verify_pivot_xy
from atlas_contract import check_overridable_as_atlas, check_mask_policy

pivot_x = reanchored_pivot(ziel.pivot_x, ziel.width,  bild.width)
pivot_y = reanchored_pivot(ziel.pivot_y, ziel.height, bild.height)
```

## Die Gruppentabelle

`atlas_contract/loader_groups.json` führt alle 195 `addGMFile`-Registrierungen
aus `spriteLoader.SpriteLoad` mit elf Feldern je Eintrag. Die Datei ist eine
Obermenge der `supported_gm_groups.json` des AtlasBuilders: `name` und
`dashFormat` stehen unverändert an ihrer Stelle.

| Feld | Bedeutung |
|---|---|
| `gmEnum`, `gmIndex` | Kennung des Arrays |
| `dashFormat` | `gruppe-1` statt `gruppe 001` |
| `declaredImageCount` | Suchgrenze des Loaders, nicht die Framezahl |
| `idOffset`, `additionalStorage` | die beiden Stellschrauben von `addGMFile` |
| `overridableAsAtlas` | beide Stellschrauben null |
| `material` | `foliage`, `teamcolour` oder `plain` |
| `maskPolicy` | `required` oder `forbidden` |
| `customPalette` | eigene Farbtabelle statt der Standardfarben |

Zahlen aus der Tabelle:

| Was | Anzahl |
|---|---:|
| Registrierungen | 195 |
| nicht als Atlas überschreibbar | 2 |
| ohne Maske geladen (`plain`) | 69 |
| mit `Unlit/Foliage` gezeichnet | 7 |
| mit Bindestrichnummerierung | 164 |

Die sieben Vegetationsgruppen werden nicht von Hand gepflegt, sondern aus der
Farb-Remap-Ausnahme in `SpriteMapping` abgeleitet. Ein Spielupdate, das dort
etwas ändert, fällt beim Neuerzeugen auf.

Nach einem Spielupdate muss die Tabelle neu erzeugt werden.
`werkzeuge/tabelle_erzeugen.py` tut das, braucht dafür aber ein Dekompilat des
Spiels: `Enums.cs` für die Zahlenwerte, `SpriteMapping.cs` für die Ableitung
der Vegetationsgruppen und `spriteLoader.SpriteLoad` für die 195
Registrierungen selbst. Ohne Dekompilat läuft es nicht. Melde dich, dann
erzeugen wir sie.

## Die beiden gesperrten Gruppen

```csharp
addGMFile(2167, "tile_sea_new_01", GM_NEW_SEA, ..., ID_Offset: 0,    additionalStorage: 816);
addGMFile( 816, "tile_sea_shore",  GM_NEW_SEA, ..., ID_Offset: 2167, additionalStorage: -1);
```

`addGMFile` legt `höchster Index + 1 + additionalStorage` Slots an, aber nur bei
`additionalStorage >= 0`, und schreibt jeden Frame nach `index + ID_Offset`.
`tile_sea_new_01` reserviert 816 Slots für einen Untermieter, `tile_sea_shore`
legt selbst nichts an und schreibt ab Index 2167 hinein. `ApplySingle` kennt
beide Mechanismen nicht. Ein Atlas auf einer der beiden zerstört die
Meeresdarstellung. Wir hatten `tile_sea_shore` als zweitgrößten sicheren Gewinn
eingeplant, bis wir es nachgerechnet haben.

## Was geprüft ist

**Einheitentests, ohne Spiel:** 51 Tests über Ankerumrechnung, Eckkonvention,
Namenszerlegung, Gruppentabelle, die drei Ankerregeln und die Atlasprüfung.

```bash
python -m pytest tests -q
```

**Gegen unsere Produktion:** `werkzeuge/nachweis_gegen_produktion.py` läuft nur
bei uns, weil es unser Baumanifest braucht. Es extrahiert
die Slots, die unsere eigene Kette unverändert übernommen hat, und vergleicht
sie mit den dort eingefrorenen SHA-256-Werten.

| Gruppe | Slots | Ergebnis |
|---|---:|---|
| `Tree_Oak` | 4 | Farbe und Maske bytegleich |
| `Tree_Chestnut` | 10 | Farbe und Maske bytegleich |
| `anim_flags` | 3 | abweichend, siehe unten |

Die drei `anim_flags`-Slots weichen ab, weil das Vergleichsmanifest gegen einen
älteren Spielstand geschrieben wurde. Die Abweichung liegt in der Größenordnung
von Kompressionsrauschen, ein bis zwanzig Stufen je Farbkanal, und betrifft die
Textur `anims1Sprites`, nicht `treeSprites`. Die Objektkennungen derselben
Sprites sind zwischen den beiden Ständen um null bis zwei verschoben, die
Assets-Datei hat sich also geändert. Geometrie und Pivot stimmen exakt überein.
Gegengeprüft wurde die Extraktion zusätzlich gegen einen eigenen Zuschnitt aus
der Rohtextur.

**Durchlauf von Anfang bis Ende:** `werkzeuge/durchlauf.py` erzeugt zehn
absichtlich zwanzig Pixel zu große Vorlagen, füllt die übrigen 91 Slots auf,
baut den Atlas, prüft ihn und verdirbt anschließend die Pivots, um zu zeigen,
dass die Prüfung sie fängt. Der gemessene Versatz beträgt dabei rund 19 Pixel je
Frame, genau `Pivot * 40`.

```bash
python werkzeuge/durchlauf.py ".../Stronghold Crusader Definitive Edition_Data"
```

## Grenzen

Alle Werte stammen aus dem Dekompilat der lokal angehefteten Spielstände,
SHCDE 2.80 gegen SHCDE-SE 1.39. Ein Spiel- oder Extender-Update macht sie
ungültig, bis `werkzeuge/tabelle_erzeugen.py` neu gelaufen ist.

Der eingebaute Bauer ist absichtlich schlicht: Regalpackung ohne Drehung, keine
gemeinsame Nutzung gleicher Bildbereiche, keine Nebenläufigkeit. Für große
Gruppen ist ein richtiger Bauer schneller. Die Bausteine sind dafür da,
in einen solchen eingebaut zu werden.

Das Paket ändert nichts am Shader. Die sieben Vegetationsgruppen brauchen
weiterhin einen Eingriff zur Laufzeit, weil der Atlas-Loader `Unlit/TeamColour`
baut, wo Vanilla `Unlit/Foliage` benutzt. `pruefen` weist darauf hin.
