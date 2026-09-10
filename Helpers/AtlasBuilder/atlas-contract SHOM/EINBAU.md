# Einbau in den AtlasBuilder

Bezieht sich auf `AtlasBuilder/atlas_builder/core.py`, Stand
`AtlasBuilder_0.1.0`. Vier Eingriffe, zusammen etwa dreißig Zeilen.

## 1. Die Zielgröße mitlesen

`TargetFrame` kennt heute Pivot und Auflösung, aber nicht die Leinwandgröße des
Slots. Ohne die ist die Ankerumrechnung nicht berechenbar. Das ist die
eigentliche Ursache des Versatzes.

```python
@dataclass(frozen=True)
class TargetFrame:
    name: str
    pivot_x: float
    pivot_y: float
    pixels_per_unit: float
    width: float       # neu
    height: float      # neu
```

In `read_target_metadata`, wo heute `m_Pivot` und `m_PixelsToUnits` gelesen
werden (Zeile 215 bis 219):

```python
target = TargetFrame(
    name=name,
    pivot_x=float(data.m_Pivot.x),
    pivot_y=float(data.m_Pivot.y),
    pixels_per_unit=float(data.m_PixelsToUnits),
    width=float(data.m_Rect.width),      # neu
    height=float(data.m_Rect.height),    # neu
)
```

## 2. Den Pivot umrechnen

In `build_group`, Zeile 360 bis 364. Heute steht dort die eigene Bildgröße
zusammen mit dem fremden Pivot:

```python
frame = {
    "name": target.name,
    "rect": {"x": x, "y": height - top_y - frame_height, "w": frame_width, "h": frame_height},
    "pivot": {"x": target.pivot_x, "y": target.pivot_y},
}
```

Neu:

```python
from atlas_contract import reanchored_pivot

pivot_x = reanchored_pivot(target.pivot_x, target.width,  frame_width)
pivot_y = reanchored_pivot(target.pivot_y, target.height, frame_height)
frame = {
    "name": target.name,
    "rect": {"x": x, "y": height - top_y - frame_height, "w": frame_width, "h": frame_height},
    "pivot": {"x": pivot_x, "y": pivot_y},
}
```

Sind Quelle und Ziel gleich groß, kommt der Zielpivot unverändert zurück. Der
Eingriff ändert also nichts an Gruppen, die heute schon stimmen.

**Wichtig, welcher Bezug hier steht.** Der Pivot gehört zur Zeichnung, nicht
zum Slot. Wo beide Spiele dieselbe Grafik führen, führen sie auch denselben
Pivot, auf 65 von 65 geprüften Frames bis auf 0,00 px. Und innerhalb von
Crusader liegt der Anker je nach Baumgruppe zwischen 3 und 50 px über der
sichtbaren Unterkante, es gibt also gar keinen einheitlichen Slot-Anker.

Für **Bodenkacheln und gleich geformte Grafik** ist der Crusader-Slot der
richtige Bezug, so wie oben. Dort entstehen auch die schwarzen Nähte.

Für einen **echten Grafiktausch** ist der Bezug dagegen das Quellsprite: eine
Birke gehört an den Anker der Birke, nicht an den der Palme, die vorher im Slot
lag. Dafür müsste dein Builder auch die Quellinstallation lesen und den Pivot
von dort nehmen. Die Formel bleibt dieselbe, nur `target.*` wird zu `quelle.*`.
Im Paket ist das `--anker quelle --quellspiel <..._Data>`, und der Prüfbefehl
lässt dabei unveränderte Crusader-Kopien als Mischfall durchgehen.

Wer das ohne Fremdpaket machen will, braucht diese acht Zeilen:

```python
CORNER_PIVOT_EPSILON = 1e-6

def reanchored_pivot(target_pivot, target_size, source_size):
    """Normalisierter Pivot für eine Leinwand abweichender Größe."""
    if abs(target_pivot - 1.0) <= CORNER_PIVOT_EPSILON:
        anchor = source_size          # Anker liegt auf der Kante der Quelle
    else:
        anchor = target_pivot * target_size
    return anchor / source_size
```

## 3. Die Pivot-Prüfung ersetzen

`validate_generated_group`, Zeile 414:

```python
if not 0 <= frame["pivot"]["x"] <= 1 or not 0 <= frame["pivot"]["y"] <= 1:
    raise AtlasBuilderError(f"{target.name}: generated pivot is invalid")
```

Diese Prüfung schlägt nach der Umrechnung an, sobald die Quelle kleiner ist als
das Ziel, und sie ist auch für Vanilla falsch: das Spiel liefert selbst Pivots
außerhalb von [0,1] aus, `body_horse_archer_top-1` hat `pivot_y = -0.1196`.

An ihre Stelle gehört die Ankerprobe. Der Rückschnitt darunter vergleicht Pixel
und kann einen falschen Pivot grundsätzlich nicht fangen, weil der Pivot nicht
Teil der Pixel ist:

```python
from atlas_contract import verify_pivot_xy

verify_pivot_xy(
    frame["pivot"]["x"], frame["pivot"]["y"],
    target.pivot_x, target.pivot_y,
    target.width, target.height,
    width, height,
    label=target.name,
)
```

`verify_pivot_xy` wirft `AnchorError`, wenn der Anker um mehr als 1e-4 Pixel
danebenliegt.

## 4. Die Gruppentabelle austauschen

`atlas_builder/assets/supported_gm_groups.json` führt heute `name` und
`dashFormat`. Beide Listen stimmen exakt überein, alle 195 Namen und jedes
`dashFormat`. Die Datei aus diesem Paket ist eine Obermenge und lässt sich
direkt einsetzen; `load_supported_groups` liest sie unverändert weiter.

Damit werden drei Prüfungen möglich, die heute fehlen:

```python
from atlas_contract import check_overridable_as_atlas, check_mask_policy, require_group

# In prepare_project, direkt nach der Namensauflösung:
check_overridable_as_atlas(config.gm_file_name)

# In discover_source_group, nachdem mask_mode feststeht:
check_mask_policy(config.gm_file_name, has_mask=config.mask_mode != "none")

# Hinweis für den Nutzer, wenn eine Vegetationsgruppe gebaut wird:
if require_group(config.gm_file_name).needs_foliage_material:
    warnings.append(
        "Vanilla zeichnet diese Gruppe mit Unlit/Foliage. Der Atlas-Loader baut "
        "Unlit/TeamColour, sobald eine Maske vorliegt."
    )
```

`check_overridable_as_atlas` verhindert genau zwei Gruppen, `tile_sea_new_01`
und `tile_sea_shore`. Beide teilen sich ein Array; ein Atlas auf einer der
beiden zerstört die Meeresdarstellung.

`check_mask_policy` fängt die 69 Gruppen, die das Spiel ohne Maske und mit dem
Standardmaterial lädt. `tree_cactii` ist so ein Fall und sieht auf den ersten
Blick aus wie ein Baum.

## Die Teilatlanten

Die Warnung, wenn der höchste Zielindex über dem höchsten Quellindex liegt,
steht schon im Builder und ist genau an der richtigen Stelle. Sie müsste nur
zwingend sein statt nur ein Hinweis, denn der Loader legt das Array aus dem
höchsten gelieferten Index an und löscht alles darüber.

Wer sie zwingend macht, braucht einen Weg, die Lücken zu füllen. Dafür gibt es

```bash
python -m atlas_contract fuellen <gruppe> --spiel <..._Data> --bilder <ordner>
```

Das schreibt jeden Slot ohne eigene Vorlage als unveränderte Crusader-Kopie in
denselben Ordner, benannt nach der Konvention des Builders
(`<präfix><index>[x].png` und `<präfix><index>[x]_m.png`). Beim nächsten Lauf
liest der Builder sie wie eigene Vorlagen ein, und der Satz ist vollständig.

Die Bilder kommen aus `Sprite.image`. Das ist genau richtig, weil der Loader
jedes ersetzte Sprite als `SpriteMeshType.FullRect` neu anlegt: was Vanilla
wegen seines engen Netzes nie gezeichnet hat, darf im Ersatz auch nicht
auftauchen.

## Prüfen, ohne etwas einzubauen

Der Prüfbefehl liest nur die fertigen Dateien und braucht den Builder nicht:

```bash
python -m atlas_contract pruefen <gruppe> \
    --spiel ".../Stronghold Crusader Definitive Edition_Data" \
    --atlas ".../Override/Atlas/<gruppe>"
```

An einem bestehenden Mod zeigt das in einem Durchlauf, welche Frames
verrutscht sind und um wie viele Pixel.
