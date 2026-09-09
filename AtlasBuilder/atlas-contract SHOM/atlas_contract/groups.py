"""Die 195 Ladegruppen des Spiels, mit den Feldern, an denen Fehler haengen.

Gruppenzugehoerigkeit kommt aus den ``addGMFile``-Aufrufen in
``spriteLoader.SpriteLoad``, nicht aus einer Namensregel. Namen lassen sich
nicht zuverlaessig gruppieren: das Spiel benutzt beide Nummerierungen
(``<gruppe> 001`` und ``<gruppe>-1``), Gruppennamen enthalten selbst
Leerzeichen (``puff of smoke``) und Bindestriche (``smoke-30x30``), und zwei
Ordner teilen sich eine GM-Kennung.

Zwei Felder entscheiden ueber Erfolg oder Schaden:

``overridableAsAtlas``
    ``addGMFile`` legt ``hoechster Index + 1 + additionalStorage`` Slots an,
    aber nur bei ``additionalStorage >= 0``, und schreibt jeden Frame nach
    ``index + ID_Offset``. ``GameAtlasManagerAPI.ApplySingle`` kennt beide
    Mechanismen nicht: es legt ``hoechster Index im Override + 1`` Slots an und
    schreibt am rohen Index. Eine Gruppe ist als Atlas nur dann sicher, wenn
    beide Stellschrauben null sind. Genau zwei der 195 fallen durch, beide auf
    ``GM_NEW_SEA``.

``material`` / ``maskPolicy``
    ``addGMFile`` setzt bei ``plainShader`` das maskenlose Standardmaterial.
    Sonst gilt das Materialarray der Quelltextur, und das ist fuer die sieben
    Vegetationsgruppen ``Unlit/Foliage`` statt ``Unlit/TeamColour``. Wer einer
    ``plain``-Gruppe eine Maske mitgibt, bekommt das falsche Material.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator, Mapping

__all__ = [
    "LoaderGroup",
    "LOADER_GROUPS",
    "GroupError",
    "find_group",
    "require_group",
    "check_overridable_as_atlas",
    "check_mask_policy",
    "loader_contract",
]

_TABELLE = Path(__file__).with_name("loader_groups.json")


class GroupError(ValueError):
    """Die gewaehlte Gruppe passt nicht zu dem, was das Spiel dort erwartet."""


@dataclass(frozen=True)
class LoaderGroup:
    """Eine ``addGMFile``-Registrierung."""

    name: str
    dash_format: bool
    gm_enum: str
    gm_index: int
    declared_image_count: int
    id_offset: int
    additional_storage: int
    overridable_as_atlas: bool
    material: str
    mask_policy: str
    custom_palette: bool

    @property
    def needs_foliage_material(self) -> bool:
        """Vanilla zeichnet diese Gruppe mit ``Unlit/Foliage``.

        Der Atlas-Loader baut stattdessen ``Unlit/TeamColour``, sobald eine
        ``atlas_m.png`` vorliegt. Ohne Eingriff zur Laufzeit rendert die Gruppe
        durch den falschen Shader.
        """

        return self.material == "foliage"

    @property
    def mask_forbidden(self) -> bool:
        return self.mask_policy == "forbidden"


def _laden() -> tuple[dict[str, LoaderGroup], str]:
    nutzlast = json.loads(_TABELLE.read_text(encoding="utf-8"))
    tabelle: dict[str, LoaderGroup] = {}
    for eintrag in nutzlast["groups"]:
        gruppe = LoaderGroup(
            name=eintrag["name"],
            dash_format=bool(eintrag["dashFormat"]),
            gm_enum=eintrag["gmEnum"],
            gm_index=int(eintrag["gmIndex"]),
            declared_image_count=int(eintrag["declaredImageCount"]),
            id_offset=int(eintrag["idOffset"]),
            additional_storage=int(eintrag["additionalStorage"]),
            overridable_as_atlas=bool(eintrag["overridableAsAtlas"]),
            material=eintrag["material"],
            mask_policy=eintrag["maskPolicy"],
            custom_palette=bool(eintrag["customPalette"]),
        )
        if gruppe.name in tabelle:
            raise GroupError(f"Doppelter Gruppenname in der Tabelle: {gruppe.name}")
        tabelle[gruppe.name] = gruppe
    return tabelle, str(nutzlast.get("sourceContract", ""))


LOADER_GROUPS, LOADER_CONTRACT = _laden()


def loader_contract() -> str:
    """Woraus die Tabelle stammt. Nach einem Spielupdate neu erzeugen."""

    return LOADER_CONTRACT


def find_group(name: str) -> LoaderGroup | None:
    """Gruppe suchen, Gross- und Kleinschreibung egal."""

    treffer = LOADER_GROUPS.get(name)
    if treffer is not None:
        return treffer
    gefaltet = name.casefold()
    for gruppe in LOADER_GROUPS.values():
        if gruppe.name.casefold() == gefaltet:
            return gruppe
    return None


def require_group(name: str) -> LoaderGroup:
    gruppe = find_group(name)
    if gruppe is None:
        raise GroupError(f"Unbekannte Ladegruppe: {name}")
    return gruppe


def check_overridable_as_atlas(name: str) -> LoaderGroup:
    """Bricht ab, wenn ein Atlas auf dieser Gruppe Schaden anrichten wuerde."""

    gruppe = require_group(name)
    if gruppe.overridable_as_atlas:
        return gruppe
    if gruppe.additional_storage < 0:
        grund = (
            f"sie legt keine eigenen Arrays an (additionalStorage="
            f"{gruppe.additional_storage}) und schreibt ab Index "
            f"{gruppe.id_offset} in die Arrays einer anderen Gruppe"
        )
    else:
        grund = (
            f"ihr Array ist absichtlich {gruppe.additional_storage} Slots "
            f"breiter als ihre eigenen Frames, reserviert fuer einen "
            f"Untermieter"
        )
    raise GroupError(
        f"{gruppe.name} darf nicht als Atlas ueberschrieben werden: {grund}. "
        f"Ein Override wuerde die Arrays von {gruppe.gm_enum} zerstoeren. "
        f"Nur Einzelsprites unter Override/Sprites erreichen diese Gruppe, "
        f"weil sie nach Namen ersetzen ohne Arrays anzufassen."
    )


def check_mask_policy(name: str, has_mask: bool) -> LoaderGroup:
    """Bricht ab, wenn Maske und Material der Gruppe nicht zusammenpassen."""

    gruppe = require_group(name)
    if gruppe.mask_forbidden and has_mask:
        raise GroupError(
            f"{gruppe.name} wird vom Spiel ohne Maske und mit dem "
            f"Standardmaterial geladen (plainShader). Eine atlas_m.png erzeugt "
            f"hier das falsche Material - liefere die Gruppe ohne Maske aus."
        )
    if not gruppe.mask_forbidden and not has_mask:
        raise GroupError(
            f"{gruppe.name} wird vom Spiel mit Maske geladen "
            f"(material={gruppe.material}). Ohne atlas_m.png fehlt die "
            f"Spielerfarbe beziehungsweise die Vegetationsfaerbung."
        )
    return gruppe


def __iter__() -> Iterator[LoaderGroup]:  # pragma: no cover - Bequemlichkeit
    return iter(LOADER_GROUPS.values())


def as_mapping() -> Mapping[str, LoaderGroup]:
    return LOADER_GROUPS
