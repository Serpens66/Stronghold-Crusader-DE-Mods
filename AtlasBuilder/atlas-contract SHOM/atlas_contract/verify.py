"""Einen fertigen Atlas gegen das Zielspiel nachrechnen.

Diese Pruefung ist unabhaengig davon, womit der Atlas gebaut wurde. Sie liest
``atlas.json`` und vergleicht jeden Frame mit dem Slot, den er ersetzt:

* liegt der Anker dort, wo der Zielslot ihn fuehrt,
* stimmt die Aufloesung,
* ist das Rechteck im Atlas enthalten,
* fehlt kein Slot, sodass der Loader das Array kuerzen wuerde,
* passt die Maske zum Material der Gruppe.

Ein Pixelvergleich zwischen Vorlage und Atlas kann den Anker grundsaetzlich
nicht fangen, weil der Anker nicht Teil der Pixel ist. Ein verschobener Frame
besteht jeden Pixeltest.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

from .anchor import (
    ANCHOR_TOLERANCE_PX,
    AnchorError,
    target_anchor,
    verify_pivot_xy,
)
from .completeness import plan
from .target import FrameKey, TargetGroup

__all__ = ["VerifyReport", "verify_atlas"]

_PPU_TOLERANZ = 1e-4


@dataclass
class VerifyReport:
    group: str
    checked: int = 0
    problems: list[str] = field(default_factory=list)
    notes: list[str] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return not self.problems

    def summary(self) -> str:
        if self.ok:
            return f"{self.group}: {self.checked} Frames geprueft, keine Beanstandung"
        return (
            f"{self.group}: {self.checked} Frames geprueft, "
            f"{len(self.problems)} Beanstandungen"
        )


def _frame_key(name: str, group: TargetGroup) -> FrameKey | None:
    for key, frame in group.frames.items():
        if frame.name.casefold() == name.casefold():
            return key
    return None


def verify_atlas(
    group: TargetGroup,
    atlas_dir: Path | str,
    *,
    tolerance: float = ANCHOR_TOLERANCE_PX,
    anchor_rule: str = "ziel",
    reference: TargetGroup | None = None,
) -> VerifyReport:
    """Alle Pruefungen ueber einen Ordner ``Override/Atlas/<gruppe>``.

    ``anchor_rule`` waehlt, woran der Anker gemessen wird:

    ``"ziel"``
        Der Frame muss den Anker des Crusader-Slots halten. Das ist die Regel
        fuer eine Uebertragung, die dort landen soll, wo Crusader das Bild
        fuehrt - insbesondere fuer Bodenkacheln, wo schon wenige Pixel
        Versatz eine sichtbare Naht ergeben.

    ``"quelle"``
        Der Frame haelt den Anker des Sprites, aus dem er stammt. ``reference``
        muss dann die Gruppe im Quellspiel sein. Das ist die Regel fuer einen
        echten Grafiktausch: der Anker gehoert zur Zeichnung, nicht zum Slot.

    ``"frei"``
        Der Frame bringt seinen eigenen Anker mit. Dann wird der Versatz nur
        gemessen und als Hinweis ausgegeben, nicht beanstandet. Sinnvoll fuer
        neu gezeichnete Grafik, deren Anker bewusst woanders liegt.
    """

    if anchor_rule not in ("ziel", "quelle", "frei"):
        raise ValueError(f"Unbekannte Ankerregel: {anchor_rule!r}")
    if anchor_rule == "quelle" and reference is None:
        raise ValueError("Die Ankerregel 'quelle' braucht eine Referenzgruppe")

    atlas_dir = Path(atlas_dir)
    bericht = VerifyReport(group=group.name)

    json_pfad = atlas_dir / "atlas.json"
    if not json_pfad.is_file():
        bericht.problems.append(f"{json_pfad} fehlt")
        return bericht
    nutzlast = json.loads(json_pfad.read_text(encoding="utf-8"))
    frames = nutzlast.get("frames") or []
    standard_ppu = float(nutzlast.get("pixelsPerUnit", 64.0))

    hat_maske = (atlas_dir / "atlas_m.png").is_file()
    if group.group.mask_forbidden and hat_maske:
        bericht.problems.append(
            f"{group.name} wird vom Spiel ohne Maske geladen (plainShader), "
            f"aber es liegt eine atlas_m.png daneben"
        )
    if not group.group.mask_forbidden and not hat_maske:
        bericht.problems.append(
            f"{group.name} wird vom Spiel mit Maske geladen "
            f"(material={group.group.material}), aber es fehlt die atlas_m.png"
        )
    if group.group.needs_foliage_material:
        bericht.notes.append(
            f"{group.name} zeichnet Vanilla mit Unlit/Foliage. Der Atlas-Loader "
            f"baut Unlit/TeamColour, sobald eine Maske vorliegt. Ohne einen "
            f"Eingriff zur Laufzeit rendert die Gruppe durch den falschen Shader."
        )

    atlas_groesse = None
    atlas_png = atlas_dir / "atlas.png"
    if atlas_png.is_file():
        try:
            from PIL import Image

            with Image.open(atlas_png) as bild:
                atlas_groesse = bild.size
        except Exception as fehler:  # pragma: no cover
            bericht.problems.append(f"atlas.png nicht lesbar: {fehler}")
    else:
        bericht.problems.append("atlas.png fehlt")

    geliefert: list[FrameKey] = []
    gesehen: set[str] = set()
    abweichungen: list[tuple[str, float, float]] = []
    uebernommen = 0
    for eintrag in frames:
        name = str(eintrag.get("name", ""))
        if name in gesehen:
            bericht.problems.append(f"{name}: kommt zweimal in der atlas.json vor")
            continue
        gesehen.add(name)

        key = _frame_key(name, group)
        if key is None:
            bericht.problems.append(
                f"{name}: kein solcher Slot im Zielspiel - der Loader wuerde "
                f"den Frame verwerfen oder falsch einsortieren"
            )
            continue
        geliefert.append(key)
        ziel = group.frames[key]
        bezug = ziel
        if anchor_rule == "quelle":
            assert reference is not None
            bezug = reference.frames.get(key)
            if bezug is None:
                bericht.problems.append(
                    f"{name}: im Quellspiel gibt es diesen Index nicht, der "
                    f"Anker laesst sich nicht uebernehmen"
                )
                continue
        bericht.checked += 1

        rect = eintrag.get("rect") or {}
        try:
            breite = float(rect["w"])
            hoehe = float(rect["h"])
            links = float(rect["x"])
            unten = float(rect["y"])
        except (KeyError, TypeError, ValueError):
            bericht.problems.append(f"{name}: rect fehlt oder ist unvollstaendig")
            continue
        if breite <= 0 or hoehe <= 0:
            bericht.problems.append(f"{name}: rect hat keine positive Groesse")
            continue
        if atlas_groesse is not None and (
            links < 0
            or unten < 0
            or links + breite > atlas_groesse[0]
            or unten + hoehe > atlas_groesse[1]
        ):
            bericht.problems.append(
                f"{name}: rect liegt ausserhalb der atlas.png {atlas_groesse}"
            )

        pivot = eintrag.get("pivot") or {}
        try:
            pivot_x = float(pivot["x"])
            pivot_y = float(pivot["y"])
        except (KeyError, TypeError, ValueError):
            bericht.problems.append(f"{name}: pivot fehlt oder ist unvollstaendig")
            continue
        try:
            verify_pivot_xy(
                pivot_x,
                pivot_y,
                bezug.pivot_x,
                bezug.pivot_y,
                bezug.width,
                bezug.height,
                breite,
                hoehe,
                tolerance=tolerance,
                label=name,
            )
        except AnchorError as fehler:
            # Ein Slot ohne eigene Vorlage geht als unveraenderte Kopie des
            # Zielsprites mit. Der traegt dann selbstverstaendlich den Anker des
            # Ziels und nicht den der Quelle. Unter der Regel 'quelle' ist das
            # kein Fehler, sondern der erwartete Mischfall.
            if anchor_rule == "quelle":
                try:
                    verify_pivot_xy(
                        pivot_x,
                        pivot_y,
                        ziel.pivot_x,
                        ziel.pivot_y,
                        ziel.width,
                        ziel.height,
                        breite,
                        hoehe,
                        tolerance=tolerance,
                        label=name,
                    )
                    uebernommen += 1
                    continue
                except AnchorError:
                    pass
            if anchor_rule != "frei":
                bericht.problems.append(str(fehler))
            else:
                versatz_x = pivot_x * breite - target_anchor(
                    ziel.pivot_x, ziel.width, breite
                )
                versatz_y = pivot_y * hoehe - target_anchor(
                    ziel.pivot_y, ziel.height, hoehe
                )
                abweichungen.append((name, versatz_x, versatz_y))

        ppu = float(eintrag.get("pixelsPerUnit", standard_ppu))
        if abs(ppu - ziel.pixels_per_unit) > _PPU_TOLERANZ:
            bericht.problems.append(
                f"{name}: pixelsPerUnit {ppu} statt {ziel.pixels_per_unit}"
            )

    if uebernommen:
        bericht.notes.append(
            f"{uebernommen} von {bericht.checked} Frames tragen den Anker ihres "
            f"Crusader-Slots statt den der Quelle. Das ist der erwartete "
            f"Mischfall: Slots ohne eigene Vorlage gehen als unveraenderte "
            f"Kopie mit."
        )

    if abweichungen:
        groesste = max(
            abweichungen, key=lambda eintrag: max(abs(eintrag[1]), abs(eintrag[2]))
        )
        bericht.notes.append(
            f"{len(abweichungen)} von {bericht.checked} Frames halten nicht den "
            f"Anker ihres Crusader-Slots. Groesste Abweichung {groesste[0]}: "
            f"x {groesste[1]:+.1f} px, y {groesste[2]:+.1f} px. Unter der Regel "
            f"'frei' ist das erlaubt; unter 'ziel' waere es ein Fehler."
        )

    vollstaendigkeit = plan(group, geliefert)
    if vollstaendigkeit.missing:
        verlust = vollstaendigkeit.truncates
        meldung = (
            f"{len(vollstaendigkeit.missing)} von {vollstaendigkeit.target_count} "
            f"Slots fehlen in der atlas.json"
        )
        if verlust:
            meldung += (
                f"; davon wuerden {verlust} Vanilla-Frames beim Laden geloescht, "
                f"weil der Loader das Array aus dem hoechsten gelieferten Index "
                f"anlegt"
            )
        bericht.problems.append(meldung)

    return bericht
