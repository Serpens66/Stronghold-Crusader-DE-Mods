"""Ein kleiner, vollstaendiger Atlasbauer.

Bewusst schlicht gehalten: er packt die gelieferten PNG-Dateien, schreibt die
``atlas.json`` mit umgerechneten Ankern und prueft das Ergebnis, bevor er es
stehen laesst. Wer einen komfortableren Bauer hat, benutzt aus diesem Paket
nur :mod:`atlas_contract.anchor`, :mod:`atlas_contract.groups` und
:mod:`atlas_contract.verify`.

Zwei Pruefungen laufen immer und brechen ab:

* **Rueckschnitt.** Jeder Frame wird nach dem Packen wieder aus dem Atlas
  herausgeschnitten und bytegleich mit der Vorlage verglichen.
* **Ankerprobe.** Jeder geschriebene Pivot wird gegen den Zielslot
  nachgerechnet. Der Rueckschnitt allein kann einen falschen Anker nicht
  fangen, weil der Anker nicht Teil der Pixel ist.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Mapping, Sequence

from PIL import Image, ImageChops

from .anchor import reanchored_pivot
from .completeness import plan
from .groups import check_overridable_as_atlas
from .target import FrameKey, TargetError, TargetGroup
from .verify import verify_atlas

__all__ = ["BuildInput", "build_group"]


class BuildError(RuntimeError):
    """Der Atlas konnte nicht korrekt gebaut werden."""


@dataclass(frozen=True)
class BuildInput:
    key: FrameKey
    colour: Path
    mask: Path | None


def _naechste_zweierpotenz(wert: int) -> int:
    groesse = 64
    while groesse < wert:
        groesse *= 2
    return groesse


def _packen(
    groessen: Sequence[tuple[FrameKey, int, int]],
    *,
    padding: int,
    limit: int,
) -> tuple[int, int, dict[FrameKey, tuple[int, int]]]:
    """Regalpackung ohne Drehung, kleinste passende Zweierpotenz in der Breite."""

    breiteste = max(breite for _key, breite, _hoehe in groessen) + 2 * padding
    kandidat = max(64, _naechste_zweierpotenz(breiteste))
    while kandidat <= limit:
        platz: dict[FrameKey, tuple[int, int]] = {}
        x = padding
        y = padding
        zeilenhoehe = 0
        passt = True
        for key, breite, hoehe in groessen:
            if x != padding and x + breite + padding > kandidat:
                x = padding
                y += zeilenhoehe + padding
                zeilenhoehe = 0
            if breite + 2 * padding > kandidat:
                passt = False
                break
            platz[key] = (x, y)
            x += breite + padding
            zeilenhoehe = max(zeilenhoehe, hoehe)
        hoehe_gesamt = y + zeilenhoehe + padding
        if passt and hoehe_gesamt <= limit:
            return kandidat, _naechste_zweierpotenz(hoehe_gesamt), platz
        kandidat *= 2
    raise BuildError(f"Die Frames passen in keinen Atlas bis {limit} Pixel")


def build_group(
    group: TargetGroup,
    frames: Mapping[FrameKey, BuildInput],
    output_dir: Path,
    *,
    padding: int = 1,
    max_dimension: int = 8192,
    anchor_rule: str = "ziel",
    reference: TargetGroup | None = None,
    require_complete: bool = True,
) -> Path:
    """Baut ``atlas.png``, ``atlas_m.png`` und ``atlas.json`` fuer eine Gruppe."""

    ladegruppe = check_overridable_as_atlas(group.name)
    will_maske = not ladegruppe.mask_forbidden

    bericht = plan(group, frames.keys())
    if bericht.unknown:
        raise BuildError(
            f"{group.name}: diese Indizes gibt es im Zielspiel nicht: "
            + ", ".join(str(k) for k in bericht.unknown)
        )
    if require_complete and bericht.missing:
        raise BuildError(
            f"{group.name}: {len(bericht.missing)} von {bericht.target_count} "
            f"Slots fehlen. Der Loader wuerde {bericht.truncates} Vanilla-Frames "
            f"loeschen. Erst 'atlas_contract fuellen' laufen lassen."
        )

    bilder: dict[FrameKey, Image.Image] = {}
    masken: dict[FrameKey, Image.Image] = {}
    for key, eingabe in frames.items():
        with Image.open(eingabe.colour) as datei:
            bilder[key] = datei.convert("RGBA")
        if will_maske:
            if eingabe.mask is None:
                raise BuildError(
                    f"{group.name}: zu Slot {key} fehlt die Maske. Diese Gruppe "
                    f"wird vom Spiel mit Maske geladen."
                )
            with Image.open(eingabe.mask) as datei:
                masken[key] = datei.convert("RGBA")
            if masken[key].size != bilder[key].size:
                raise BuildError(
                    f"{group.name}: Slot {key} hat Maske {masken[key].size} zu "
                    f"Bild {bilder[key].size}"
                )
        elif eingabe.mask is not None:
            raise BuildError(
                f"{group.name}: Slot {key} bringt eine Maske mit, aber die "
                f"Gruppe wird ohne Maske geladen (plainShader)."
            )

    reihenfolge = sorted(frames)
    groessen = [(key, bilder[key].width, bilder[key].height) for key in reihenfolge]
    breite, hoehe, platz = _packen(groessen, padding=padding, limit=max_dimension)

    atlas = Image.new("RGBA", (breite, hoehe), (0, 0, 0, 0))
    masken_atlas = (
        Image.new("RGBA", (breite, hoehe), (0, 0, 0, 0)) if will_maske else None
    )
    eintraege = []
    for key in reihenfolge:
        bild = bilder[key]
        x, y_oben = platz[key]
        atlas.paste(bild, (x, y_oben))
        if masken_atlas is not None:
            masken_atlas.paste(masken[key], (x, y_oben))
        ziel = group.frames[key]
        bezug = ziel
        if anchor_rule == "quelle":
            if reference is None:
                raise BuildError("Die Ankerregel 'quelle' braucht eine Referenzgruppe")
            if key not in reference.frames:
                raise BuildError(
                    f"{ziel.name}: im Quellspiel gibt es Index {key} nicht, der "
                    f"Anker laesst sich nicht uebernehmen"
                )
            bezug = reference.frames[key]
        if anchor_rule == "frei":
            pivot_x, pivot_y = ziel.pivot_x, ziel.pivot_y
        else:
            pivot_x = reanchored_pivot(bezug.pivot_x, bezug.width, bild.width)
            pivot_y = reanchored_pivot(bezug.pivot_y, bezug.height, bild.height)
        eintrag = {
            "name": ziel.name,
            "rect": {
                "x": x,
                "y": hoehe - y_oben - bild.height,
                "w": bild.width,
                "h": bild.height,
            },
            "pivot": {"x": pivot_x, "y": pivot_y},
        }
        if abs(ziel.pixels_per_unit - 64.0) > 1e-6:
            eintrag["pixelsPerUnit"] = ziel.pixels_per_unit
        eintraege.append(eintrag)

    # Rueckschnitt vor dem Schreiben: was im Atlas steht, muss die Vorlage sein.
    for key, eintrag in zip(reihenfolge, eintraege):
        r = eintrag["rect"]
        oben = hoehe - r["y"] - r["h"]
        ausschnitt = atlas.crop((r["x"], oben, r["x"] + r["w"], oben + r["h"]))
        if ImageChops.difference(ausschnitt, bilder[key]).getbbox():
            raise BuildError(f"{eintrag['name']}: Rueckschnitt weicht von der Vorlage ab")
        if masken_atlas is not None:
            ausschnitt = masken_atlas.crop(
                (r["x"], oben, r["x"] + r["w"], oben + r["h"])
            )
            if ImageChops.difference(ausschnitt, masken[key]).getbbox():
                raise BuildError(
                    f"{eintrag['name']}: Maskenrueckschnitt weicht von der Vorlage ab"
                )

    import json

    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    atlas.save(output_dir / "atlas.png", format="PNG", optimize=True)
    if masken_atlas is not None:
        masken_atlas.save(output_dir / "atlas_m.png", format="PNG", optimize=True)
    (output_dir / "atlas.json").write_text(
        json.dumps({"pixelsPerUnit": 64, "frames": eintraege}, indent=2) + "\n",
        encoding="utf-8",
    )

    nachpruefung = verify_atlas(
        group, output_dir, anchor_rule=anchor_rule, reference=reference
    )
    if not nachpruefung.ok:
        raise BuildError(
            f"{group.name}: der gebaute Atlas besteht die eigene Pruefung nicht:\n  "
            + "\n  ".join(nachpruefung.problems)
        )
    return output_dir


def inputs_from_directory(
    colour_dir: Path,
    mask_dir: Path | None,
    prefix: str,
) -> dict[FrameKey, BuildInput]:
    """Liest ``<praefix><index>[x].png`` und die zugehoerigen Masken ein."""

    from .__main__ import _vorlagen

    farben, erkannt = _vorlagen(Path(colour_dir), prefix)
    if not farben:
        raise BuildError(f"Keine Vorlagen in {colour_dir}")
    masken_ordner = Path(mask_dir) if mask_dir else Path(colour_dir)
    ergebnis: dict[FrameKey, BuildInput] = {}
    for key, pfad in farben.items():
        stamm = f"{erkannt}{key.index}{'x' if key.alternate else ''}_m.png"
        maske = masken_ordner / stamm
        ergebnis[key] = BuildInput(
            key=key, colour=pfad, mask=maske if maske.is_file() else None
        )
    if not ergebnis:
        raise TargetError(f"Keine Frames in {colour_dir}")
    return ergebnis
