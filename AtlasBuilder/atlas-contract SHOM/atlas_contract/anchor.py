"""Der Ankerpunkt eines Sprites, wenn die gelieferte Leinwand anders gross ist.

``Sprite.Create`` bekommt den Pivot als Bruchteil der Sprite-Groesse. Der
absolute Anker in Pixeln ist also ``Pivot * eigene Groesse``. Wer Crusaders
Pivot unveraendert auf ein anders grosses Bild schreibt, verschiebt es um
``Pivot * (Quellgroesse - Zielgroesse)`` Pixel.

Zwei Regeln, nicht eine:

1. Im Regelfall ist der Anker der **Abstand zur unteren beziehungsweise linken
   Kante**. Er bleibt in Pixeln gleich, der Pivot wird auf die neue Groesse
   umgerechnet.
2. Fuehrt der Zielslot seinen Anker dagegen an der **oberen oder rechten**
   Kante - normalisierter Pivot exakt 1,0 - dann ist der Abstand DORTHIN die
   feste Groesse, und der ist null. Der Anker liegt auf der Kante der Quelle,
   der Pivot bleibt 1,0.

Regel 2 wurde am 04.09.2026 an ``anim_castle`` gemessen. Diese Gruppe fuehrt
alle Vordergrund- und Abschlussstuecke mit Pivot (0, 1), in beiden Spielen.
Ohne die Ausnahme sitzen die Vordergrundmasken der Tuerme, Torhaeuser und des
Bergfrieds 18 bis 80 px zu tief und die Abschlussstuecke 2 bis 38 px zu hoch.

Erzeugung und Nachweis rechnen ueber dieselben Funktionen, damit sie nicht
auseinanderlaufen koennen.
"""

from __future__ import annotations

from dataclasses import dataclass

__all__ = [
    "CORNER_PIVOT_EPSILON",
    "ANCHOR_TOLERANCE_PX",
    "AnchorError",
    "target_anchor",
    "reanchored_pivot",
    "reanchored_pivot_xy",
    "verify_pivot",
    "verify_pivot_xy",
    "PivotResult",
]

# Ein Pivot gilt als Eckkonvention, wenn er auf sechs Nachkommastellen 1,0 ist.
# Die Unity-Metadaten liefern hier exakte Werte; die Toleranz faengt nur die
# Umwandlung ueber float32 ab.
CORNER_PIVOT_EPSILON = 1e-6

# Zulaessige Abweichung des nachgerechneten Ankers, in Pixeln. Alles darueber
# ist ein echter Fehler und keine Rundung.
ANCHOR_TOLERANCE_PX = 1e-4


class AnchorError(ValueError):
    """Der Anker eines Frames liegt nicht dort, wo der Zielslot ihn erwartet."""


@dataclass(frozen=True)
class PivotResult:
    """Ergebnis einer Umrechnung, mitsamt der Zwischengroessen fuer Meldungen."""

    pivot: float
    anchor_px: float
    corner_convention: bool


def _pruefe_groessen(target_size: float, source_size: float) -> None:
    if not (target_size > 0.0):
        raise AnchorError(f"Zielgroesse muss positiv sein, ist {target_size!r}")
    if not (source_size > 0.0):
        raise AnchorError(f"Quellgroesse muss positiv sein, ist {source_size!r}")


def target_anchor(target_pivot: float, target_size: float, source_size: float) -> float:
    """Absoluter Anker in Pixeln, gemessen ab unterer beziehungsweise linker Kante.

    ``target_pivot`` und ``target_size`` beschreiben den Crusader-Slot,
    ``source_size`` die Kantenlaenge des gelieferten Bildes.
    """

    _pruefe_groessen(target_size, source_size)
    if abs(target_pivot - 1.0) <= CORNER_PIVOT_EPSILON:
        return float(source_size)
    return float(target_pivot) * float(target_size)


def reanchored_pivot(
    target_pivot: float, target_size: float, source_size: float
) -> float:
    """Normalisierter Pivot fuer eine Leinwand abweichender Groesse.

    Sind Quelle und Ziel gleich gross, kommt der Zielpivot unveraendert zurueck.
    """

    return target_anchor(target_pivot, target_size, source_size) / float(source_size)


def reanchored_pivot_xy(
    target_pivot_x: float,
    target_pivot_y: float,
    target_width: float,
    target_height: float,
    source_width: float,
    source_height: float,
) -> tuple[float, float]:
    """Beide Achsen auf einmal. Die Achsen sind voneinander unabhaengig."""

    return (
        reanchored_pivot(target_pivot_x, target_width, source_width),
        reanchored_pivot(target_pivot_y, target_height, source_height),
    )


def verify_pivot(
    pivot: float,
    target_pivot: float,
    target_size: float,
    source_size: float,
    *,
    tolerance: float = ANCHOR_TOLERANCE_PX,
    label: str = "Frame",
    axis: str = "?",
) -> PivotResult:
    """Rechnet nach, dass ``pivot`` den Anker des Zielslots trifft.

    Diese Pruefung ist der eigentliche Nutzen des Moduls. Ein Pixelvergleich
    zwischen geliefertem Bild und Atlas kann einen falschen Pivot grundsaetzlich
    nicht fangen, weil der Pivot nicht Teil der Pixel ist. Ein verschobener
    Frame besteht jeden Pixeltest.
    """

    erwartet = target_anchor(target_pivot, target_size, source_size)
    tatsaechlich = float(pivot) * float(source_size)
    abweichung = abs(tatsaechlich - erwartet)
    if abweichung > tolerance:
        raise AnchorError(
            f"{label}: Anker auf Achse {axis} um {abweichung:.4f} px verschoben "
            f"(erwartet {erwartet:.4f} px, geliefert {tatsaechlich:.4f} px; "
            f"Zielpivot {target_pivot!r}, Zielgroesse {target_size!r}, "
            f"Quellgroesse {source_size!r})"
        )
    return PivotResult(
        pivot=float(pivot),
        anchor_px=erwartet,
        corner_convention=abs(target_pivot - 1.0) <= CORNER_PIVOT_EPSILON,
    )


def verify_pivot_xy(
    pivot_x: float,
    pivot_y: float,
    target_pivot_x: float,
    target_pivot_y: float,
    target_width: float,
    target_height: float,
    source_width: float,
    source_height: float,
    *,
    tolerance: float = ANCHOR_TOLERANCE_PX,
    label: str = "Frame",
) -> tuple[PivotResult, PivotResult]:
    """Beide Achsen pruefen. Wirft beim ersten Fehler."""

    return (
        verify_pivot(
            pivot_x,
            target_pivot_x,
            target_width,
            source_width,
            tolerance=tolerance,
            label=label,
            axis="x",
        ),
        verify_pivot(
            pivot_y,
            target_pivot_y,
            target_height,
            source_height,
            tolerance=tolerance,
            label=label,
            axis="y",
        ),
    )
