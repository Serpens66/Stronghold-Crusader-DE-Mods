"""Der Loader kuerzt Arrays. Deshalb muss ein Atlas vollstaendig sein.

``GameAtlasManagerAPI.ApplySingle`` legt das neue Sprite-Array mit
``hoechster Index in der atlas.json + 1`` Slots an und kopiert nur die
Vanilla-Eintraege, die in diese neue Laenge passen. Ein Atlas mit den Frames
0 bis 72 macht aus einem 148 Slots breiten Array eines mit 73 Slots. Die
Frames 73 bis 147 sind danach weg, und das Spiel meldet ein fehlendes Sprite.

Die Loesung ist nicht, weniger zu ersetzen, sondern mehr auszuliefern: jeder
Slot ohne eigene Vorlage geht als unveraenderte Kopie des Crusader-Sprites mit,
auf seiner eigenen Leinwand, mit seinem eigenen Pivot und seiner eigenen
Aufloesung.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Sequence

from .target import FrameKey, TargetError, TargetGroup, extract_frames

__all__ = ["CompletenessReport", "plan", "fill_missing_frames"]


@dataclass
class CompletenessReport:
    """Was zwischen den gelieferten Frames und dem Zielarray fehlt."""

    group: str
    delivered: list[FrameKey] = field(default_factory=list)
    missing: list[FrameKey] = field(default_factory=list)
    unknown: list[FrameKey] = field(default_factory=list)
    target_count: int = 0
    main_array_length: int = 0
    alt_array_length: int = 0

    @property
    def complete(self) -> bool:
        return not self.missing and not self.unknown

    @property
    def truncates(self) -> int:
        """Wie viele Vanilla-Slots dieser Atlas loeschen wuerde.

        Der Loader legt das Array aus dem hoechsten gelieferten Index an. Alles
        oberhalb davon faellt weg.
        """

        verlust = 0
        for alternate, laenge in (
            (False, self.main_array_length),
            (True, self.alt_array_length),
        ):
            indizes = [k.index for k in self.delivered if k.alternate is alternate]
            if not indizes:
                continue
            neue_laenge = max(indizes) + 1
            if neue_laenge < laenge:
                verlust += laenge - neue_laenge
        return verlust

    def summary(self) -> str:
        if self.complete:
            return (
                f"{self.group}: vollstaendig, {len(self.delivered)} von "
                f"{self.target_count} Slots"
            )
        teile = [f"{self.group}: {len(self.delivered)} von {self.target_count} Slots"]
        if self.missing:
            teile.append(f"{len(self.missing)} fehlen")
        if self.unknown:
            teile.append(
                f"{len(self.unknown)} unbekannt ({', '.join(str(k) for k in self.unknown[:5])}"
                + (" ..." if len(self.unknown) > 5 else "")
                + ")"
            )
        if self.truncates:
            teile.append(f"{self.truncates} Vanilla-Slots wuerden geloescht")
        return ", ".join(teile)


def plan(group: TargetGroup, delivered: Iterable[FrameKey]) -> CompletenessReport:
    """Vergleicht die gelieferten Slots mit dem, was das Zielspiel fuehrt."""

    geliefert = sorted(set(delivered))
    ziel = set(group.frames)
    bericht = CompletenessReport(
        group=group.name,
        delivered=geliefert,
        missing=sorted(ziel - set(geliefert)),
        unknown=sorted(set(geliefert) - ziel),
        target_count=len(ziel),
        main_array_length=group.loader_array_length(alternate=False),
        alt_array_length=group.loader_array_length(alternate=True),
    )
    return bericht


def fill_missing_frames(
    data_dir: Path | str,
    group: TargetGroup,
    missing: Sequence[FrameKey],
    *,
    colour_dir: Path,
    mask_dir: Path | None,
    prefix: str,
    want_mask: bool,
    overwrite: bool = False,
    progress=None,
) -> list[Path]:
    """Schreibt die fehlenden Slots als unveraenderte Crusader-Kopien.

    Die Dateinamen folgen der Konvention ``<praefix><index>[x].png`` und
    ``<praefix><index>[x]_m.png``, damit ein Atlasbauer sie beim naechsten Lauf
    wie eigene Vorlagen einliest.

    Die Bilder kommen aus ``Sprite.image``. Das ist genau richtig, weil der
    Loader jedes ersetzte Sprite als ``SpriteMeshType.FullRect`` neu anlegt:
    was Vanilla wegen seines engen Netzes nie gezeichnet hat, darf im Ersatz
    auch nicht auftauchen.
    """

    colour_dir = Path(colour_dir)
    colour_dir.mkdir(parents=True, exist_ok=True)
    if want_mask:
        mask_dir = Path(mask_dir) if mask_dir is not None else colour_dir
        mask_dir.mkdir(parents=True, exist_ok=True)

    fehlend = sorted(set(missing))
    if not fehlend:
        return []
    unbekannt = [k for k in fehlend if k not in group.frames]
    if unbekannt:
        raise TargetError(
            f"{group.name}: diese Slots gibt es im Zielspiel nicht: "
            + ", ".join(str(k) for k in unbekannt)
        )

    bilder = extract_frames(
        data_dir, group, fehlend, want_mask=want_mask, progress=progress
    )

    geschrieben: list[Path] = []
    for key in fehlend:
        bild, maske = bilder[key]
        stamm = f"{prefix}{key.index}{'x' if key.alternate else ''}"
        farbe_pfad = colour_dir / f"{stamm}.png"
        if farbe_pfad.exists() and not overwrite:
            raise TargetError(
                f"{farbe_pfad} liegt schon vor. Mit --ueberschreiben erzwingen."
            )
        bild.save(farbe_pfad, format="PNG", optimize=True)
        geschrieben.append(farbe_pfad)
        if want_mask:
            assert maske is not None and mask_dir is not None
            maske_pfad = mask_dir / f"{stamm}_m.png"
            if maske_pfad.exists() and not overwrite:
                raise TargetError(
                    f"{maske_pfad} liegt schon vor. Mit --ueberschreiben erzwingen."
                )
            maske.save(maske_pfad, format="PNG", optimize=True)
            geschrieben.append(maske_pfad)
    return geschrieben
