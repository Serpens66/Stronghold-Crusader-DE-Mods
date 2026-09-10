"""Was der Crusader-Slot einer Gruppe wirklich vorgibt.

Zwei Dinge, die ein Atlasbauer aus dem Zielspiel braucht und die man nicht
raten kann:

1. **Die Leinwandgroesse jedes Slots** (``m_Rect``). Ohne sie laesst sich der
   Pivot nicht umrechnen, siehe :mod:`atlas_contract.anchor`.
2. **Die Vanilla-Grafik jedes Slots**, um Frames ohne eigene Vorlage
   unveraendert mitzuliefern. Der Loader legt das Array aus dem hoechsten
   Index im Override an und kopiert nur, was in die neue Laenge passt. Wer 73
   Frames liefert, wo 148 stehen, loescht 75.

Die Frames werden ueber ``Sprite.image`` dekodiert. Das ist derselbe Weg, den
unsere eigene Kette als kanonische Referenz benutzt.

Die Maske hat keine eigenen Sprite-Objekte. Sie wird aus dem ``*_m``-Atlas
projiziert, mit dem geprueften Rechteck des Hauptsprites. Das ist noetig, weil
das Spiel einzelne Masken kleiner speichert als ihren Hauptatlas -
``treeSprites_m`` liegt in Crusader als 1024x2048 vor, waehrend ``treeSprites``
4096x8192 misst.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Iterator, Sequence

from PIL import Image

from .groups import LoaderGroup, require_group

__all__ = [
    "FrameKey",
    "TargetFrame",
    "TargetGroup",
    "TargetError",
    "read_groups",
    "crop_mask_for_sprite",
    "asset_files",
]


class TargetError(RuntimeError):
    """Das Zielspiel liefert nicht, was diese Gruppe braucht."""


@dataclass(frozen=True, order=True)
class FrameKey:
    index: int
    alternate: bool = False

    def __str__(self) -> str:
        return f"{self.index}{'x' if self.alternate else ''}"


@dataclass(frozen=True)
class TargetFrame:
    """Ein Slot in ``gmSprites`` beziehungsweise ``gmAltSprites``."""

    name: str
    key: FrameKey
    width: float
    height: float
    pivot_x: float
    pivot_y: float
    pixels_per_unit: float
    rect_x: float
    rect_y: float
    path_id: int
    texture_path_id: int
    asset_file: str

    @property
    def size(self) -> tuple[int, int]:
        return (round(self.width), round(self.height))


@dataclass
class TargetGroup:
    """Alle Slots einer Ladegruppe im Zielspiel."""

    group: LoaderGroup
    frames: dict[FrameKey, TargetFrame] = field(default_factory=dict)

    @property
    def name(self) -> str:
        return self.group.name

    def __len__(self) -> int:
        return len(self.frames)

    def __iter__(self) -> Iterator[TargetFrame]:
        return iter(sorted(self.frames.values(), key=lambda frame: frame.key))

    def keys(self, *, alternate: bool | None = None) -> list[FrameKey]:
        return sorted(
            key
            for key in self.frames
            if alternate is None or key.alternate is alternate
        )

    def loader_array_length(self, *, alternate: bool = False) -> int:
        """``hoechster Index + 1`` - so gross legt der Loader das Array an."""

        vorhanden = [key.index for key in self.frames if key.alternate is alternate]
        return max(vorhanden) + 1 if vorhanden else 0

    def sprite_name(self, key: FrameKey) -> str:
        """Der Name, den der Loader fuer diesen Slot nachschlaegt.

        Entspricht ``spriteLoader.getKey``: ohne Bindestrichformat drei
        Stellen mit fuehrenden Nullen und einem Leerzeichen, mit
        Bindestrichformat die blanke Zahl und ein angehaengtes ``x`` fuer den
        Alternativrahmen.
        """

        if not self.group.dash_format:
            if key.alternate:
                raise TargetError(
                    f"{self.name}: Alternativrahmen gibt es nur im "
                    f"Bindestrichformat"
                )
            return f"{self.name} {key.index:03d}"
        return f"{self.name}-{key.index}{'x' if key.alternate else ''}"


def asset_files(data_dir: Path) -> list[Path]:
    """``resources.assets`` zuerst, danach alle weiteren in fester Reihenfolge."""

    data_dir = Path(data_dir)
    if not data_dir.is_dir():
        raise TargetError(f"Datenverzeichnis fehlt: {data_dir}")
    primary = data_dir / "resources.assets"
    weitere = [p for p in sorted(data_dir.glob("*.assets")) if p != primary]
    dateien = ([primary] if primary.is_file() else []) + weitere
    if not dateien:
        raise TargetError(f"Keine Unity-Assets in: {data_dir}")
    return dateien


# Ein Sprite-Name endet auf Trenner + Index + optionalem ``x``. Der Rest ist
# der Gruppenname. Gruppennamen duerfen selbst Bindestriche und Leerzeichen
# enthalten (``smoke-30x30``, ``puff of smoke``) und sogar selbst auf Trenner
# und Ziffer enden (``float_pop_circ-1``). Deshalb werden alle moeglichen
# Aufteilungen erzeugt, der laengste Gruppenname zuerst, und der Aufrufer nimmt
# die erste, die er kennt. Eine rein gierige Zerlegung wuerde Frames still
# verlieren, sobald es beide Gruppen gibt.
_INDEXTEIL = re.compile(r"(\d+)(x?)\Z")


def _zerlegungen(name: str):
    """``(Gruppenname, Bindestrichformat, Schluessel)``, laengster Name zuerst."""

    for stelle in range(len(name) - 1, 0, -1):
        if name[stelle] not in "- ":
            continue
        treffer = _INDEXTEIL.fullmatch(name[stelle + 1 :])
        if treffer is None:
            continue
        yield (
            name[:stelle],
            name[stelle] == "-",
            FrameKey(int(treffer.group(1)), bool(treffer.group(2))),
        )


def _texture_path_id(sprite_data: object) -> int:
    render = getattr(sprite_data, "m_RD", None)
    if render is None:
        return 0
    zeiger = getattr(render, "texture", None) or getattr(render, "m_Texture", None)
    if zeiger is None:
        return 0
    return int(getattr(zeiger, "m_PathID", 0) or getattr(zeiger, "path_id", 0) or 0)


def read_groups(
    data_dir: Path | str,
    names: Sequence[str],
    *,
    progress=None,
    require_all: bool = True,
) -> dict[str, TargetGroup]:
    """Alle Slots der genannten Gruppen aus dem Zielspiel lesen.

    Liest nur Metadaten, dekodiert also keine Pixel. Das ist der Schritt, den
    ein Atlasbauer vor jedem Bau braucht.

    ``require_all=False`` laesst Gruppen weg, die es in dieser Installation
    nicht gibt, statt abzubrechen. Sinnvoll, wenn dieselbe Namensliste gegen
    zwei Spiele gehalten wird.
    """

    import UnityPy

    gruppen = {name: require_group(name) for name in names}
    ergebnis = {name: TargetGroup(group=gruppe) for name, gruppe in gruppen.items()}
    offen = set(gruppen)

    dateien = asset_files(Path(data_dir))
    for pfad in dateien:
        if not offen:
            break
        if progress is not None:
            progress(f"lese {pfad.name}")
        try:
            umgebung = UnityPy.load(str(pfad))
        except Exception as fehler:  # pragma: no cover - defekte Installation
            raise TargetError(f"Unity-Datei nicht lesbar: {pfad}: {fehler}") from fehler
        gefunden_hier: set[str] = set()
        # Nachschlagen statt durchprobieren: der Name wird einmal zerlegt und
        # der Gruppenteil in einer Tabelle gesucht. Sonst kostet ein Lauf ueber
        # 100.000 Sprites mal 150 Gruppen.
        nachschlag = {name.casefold(): name for name in offen}
        for obj in umgebung.objects:
            if obj.type.name != "Sprite":
                continue
            try:
                daten = obj.read()
            except Exception:  # pragma: no cover - einzelnes defektes Objekt
                continue
            sprite_name = getattr(daten, "m_Name", "") or ""
            for roher_name, bindestrich, schluessel in _zerlegungen(sprite_name):
                name = nachschlag.get(roher_name.casefold())
                if name is None:
                    continue
                if bindestrich != gruppen[name].dash_format:
                    continue
                if schluessel.alternate and not bindestrich:
                    continue
                rect = daten.m_Rect
                frame = TargetFrame(
                    name=sprite_name,
                    key=schluessel,
                    width=float(rect.width),
                    height=float(rect.height),
                    pivot_x=float(daten.m_Pivot.x),
                    pivot_y=float(daten.m_Pivot.y),
                    pixels_per_unit=float(daten.m_PixelsToUnits),
                    rect_x=float(rect.x),
                    rect_y=float(rect.y),
                    path_id=int(obj.path_id),
                    texture_path_id=_texture_path_id(daten),
                    asset_file=pfad.name,
                )
                vorhanden = ergebnis[name].frames.get(schluessel)
                if vorhanden is not None and vorhanden.path_id != frame.path_id:
                    raise TargetError(
                        f"{name}: Slot {schluessel} kommt zweimal vor "
                        f"({vorhanden.asset_file} und {pfad.name})"
                    )
                ergebnis[name].frames[schluessel] = frame
                gefunden_hier.add(name)
                # Der laengste passende Gruppenname gewinnt; eine kuerzere
                # Aufteilung desselben Namens darf denselben Frame nicht noch
                # einmal einer anderen Gruppe zuschlagen.
                break
        offen -= gefunden_hier

    leer = [name for name, gruppe in ergebnis.items() if not gruppe.frames]
    if leer:
        if require_all:
            raise TargetError(
                "Keine Ziel-Sprites gefunden fuer: " + ", ".join(sorted(leer))
            )
        for name in leer:
            del ergebnis[name]
    return ergebnis


def crop_mask_for_sprite(
    mask_atlas: Image.Image,
    *,
    rect_x: float,
    rect_y: float,
    logical_size: tuple[int, int],
    main_atlas_size: tuple[int, int],
) -> Image.Image:
    """Maskenausschnitt aus Unitys Koordinaten von unten links.

    Die Vergroesserung mit Nearest Neighbour ist Absicht: das Spiel speichert
    einzelne Masken kleiner als ihren Hauptatlas.
    """

    main_width, main_height = main_atlas_size
    if main_width <= 0 or main_height <= 0:
        raise TargetError(f"Ungueltige Atlasgroesse: {main_atlas_size}")
    logical_width, logical_height = logical_size
    scale_x = mask_atlas.width / main_width
    scale_y = mask_atlas.height / main_height
    links = round(rect_x * scale_x)
    unten = round(rect_y * scale_y)
    rechts = round((rect_x + logical_width) * scale_x)
    oben = round((rect_y + logical_height) * scale_y)
    box = (links, mask_atlas.height - oben, rechts, mask_atlas.height - unten)
    if (
        box[0] < 0
        or box[1] < 0
        or box[2] > mask_atlas.width
        or box[3] > mask_atlas.height
        or box[0] >= box[2]
        or box[1] >= box[3]
    ):
        raise TargetError(
            f"Maskenausschnitt liegt ausserhalb des Maskenatlas: box={box}, "
            f"Maske={mask_atlas.size}"
        )
    rgba = mask_atlas if mask_atlas.mode == "RGBA" else mask_atlas.convert("RGBA")
    ausschnitt = rgba.crop(box)
    if ausschnitt.size != logical_size:
        ausschnitt = ausschnitt.resize(logical_size, Image.Resampling.NEAREST)
    return ausschnitt


def extract_frames(
    data_dir: Path | str,
    group: TargetGroup,
    keys: Iterable[FrameKey],
    *,
    want_mask: bool,
    progress=None,
) -> dict[FrameKey, tuple[Image.Image, Image.Image | None]]:
    """Die Vanilla-Grafik der genannten Slots dekodieren.

    Gibt je Slot das Farbbild und, wenn verlangt, die projizierte Maske
    zurueck. Beide haben exakt die Leinwandgroesse des Slots.
    """

    import UnityPy

    gesucht = {schluessel: group.frames[schluessel] for schluessel in keys}
    if not gesucht:
        return {}
    dateien = {frame.asset_file for frame in gesucht.values()}
    if len(dateien) != 1:
        raise TargetError(
            f"{group.name}: Slots liegen in mehreren Assets-Dateien: "
            f"{sorted(dateien)}"
        )
    pfad = Path(data_dir) / next(iter(dateien))
    if progress is not None:
        progress(f"dekodiere {len(gesucht)} Vanilla-Frames aus {pfad.name}")

    umgebung = UnityPy.load(str(pfad))
    nach_path_id = {frame.path_id: schluessel for schluessel, frame in gesucht.items()}
    texturen: dict[int, object] = {}
    texturen_nach_name: dict[str, object] = {}
    sprites: dict[int, object] = {}

    for obj in umgebung.objects:
        if obj.type.name == "Sprite" and obj.path_id in nach_path_id:
            sprites[obj.path_id] = obj
        elif obj.type.name == "Texture2D":
            try:
                daten = obj.read()
            except Exception:  # pragma: no cover
                continue
            texturen[obj.path_id] = daten
            texturen_nach_name[getattr(daten, "m_Name", "") or ""] = daten

    fehlend = sorted(set(nach_path_id) - set(sprites))
    if fehlend:
        raise TargetError(f"{group.name}: Sprite-Objekte nicht wiedergefunden: {fehlend}")

    masken_bild: Image.Image | None = None
    haupt_groesse: tuple[int, int] | None = None
    if want_mask:
        haupt_ids = {frame.texture_path_id for frame in gesucht.values()}
        if len(haupt_ids) != 1 or 0 in haupt_ids:
            raise TargetError(
                f"{group.name}: Slots liegen auf mehreren Texturen {sorted(haupt_ids)}"
            )
        haupt = texturen.get(next(iter(haupt_ids)))
        if haupt is None:
            raise TargetError(f"{group.name}: Haupttextur nicht gefunden")
        haupt_name = getattr(haupt, "m_Name", "") or ""
        haupt_groesse = (int(haupt.m_Width), int(haupt.m_Height))
        maske = texturen_nach_name.get(haupt_name + "_m")
        if maske is None:
            raise TargetError(
                f"{group.name}: Maskentextur {haupt_name}_m nicht gefunden"
            )
        masken_bild = maske.image.convert("RGBA")

    ergebnis: dict[FrameKey, tuple[Image.Image, Image.Image | None]] = {}
    for path_id, schluessel in nach_path_id.items():
        frame = gesucht[schluessel]
        bild = sprites[path_id].read().image.convert("RGBA")
        if bild.size != frame.size:
            raise TargetError(
                f"{frame.name}: dekodierte Groesse {bild.size} weicht von "
                f"m_Rect {frame.size} ab"
            )
        ausschnitt = None
        if want_mask:
            assert masken_bild is not None and haupt_groesse is not None
            ausschnitt = crop_mask_for_sprite(
                masken_bild,
                rect_x=frame.rect_x,
                rect_y=frame.rect_y,
                logical_size=frame.size,
                main_atlas_size=haupt_groesse,
            )
        ergebnis[schluessel] = (bild, ausschnitt)
    return ergebnis
