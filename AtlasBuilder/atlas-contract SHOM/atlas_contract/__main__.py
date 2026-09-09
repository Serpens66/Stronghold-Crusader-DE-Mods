"""Befehlszeile: ``python -m atlas_contract <befehl>``.

Drei Befehle:

``gruppe``
    Was das Spiel ueber eine Ladegruppe weiss. Bricht ab, wenn ein Atlas dort
    Schaden anrichten wuerde.

``fuellen``
    Ergaenzt in einem Vorlagenordner alle Slots, fuer die es keine eigene
    Vorlage gibt, mit unveraenderten Crusader-Kopien. Danach ist der Satz
    vollstaendig und der Loader kuerzt nichts mehr.

``pruefen``
    Rechnet einen fertigen ``Override/Atlas/<gruppe>``-Ordner gegen das
    Zielspiel nach. Funktioniert mit jedem Atlas, egal womit er gebaut wurde.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

from .completeness import fill_missing_frames, plan
from .groups import (
    GroupError,
    check_overridable_as_atlas,
    loader_contract,
    require_group,
)
from .target import FrameKey, TargetError, read_groups
from .verify import verify_atlas


def _melde(text: str) -> None:
    print(text, flush=True)


def _vorlagen(ordner: Path, praefix: str) -> tuple[dict[FrameKey, Path], str]:
    """Liest ``<praefix><index>[x].png`` aus einem Ordner.

    Entspricht der Konvention des AtlasBuilders: Masken enden auf ``_m`` und
    werden hier uebergangen, das Praefix laesst sich erkennen.
    """

    if not ordner.is_dir():
        raise TargetError(f"Vorlagenordner fehlt: {ordner}")
    gefunden: dict[FrameKey, Path] = {}
    praefixe: dict[str, str] = {}
    for pfad in sorted(ordner.glob("*.png"), key=lambda p: p.name.casefold()):
        stamm = pfad.stem
        if stamm.casefold().endswith("_m"):
            continue
        treffer = re.fullmatch(r"(.*?)(\d+)(x?)", stamm, flags=re.IGNORECASE)
        if treffer is None:
            continue
        gefunden_praefix = treffer.group(1)
        if praefix != "auto" and gefunden_praefix.casefold() != praefix.casefold():
            continue
        schluessel = FrameKey(int(treffer.group(2)), bool(treffer.group(3)))
        if schluessel in gefunden:
            raise TargetError(f"Doppelter Index {schluessel}: {pfad}")
        praefixe.setdefault(gefunden_praefix.casefold(), gefunden_praefix)
        gefunden[schluessel] = pfad
    if praefix == "auto" and len(praefixe) > 1:
        raise TargetError(
            f"Praefix in {ordner} nicht eindeutig: {sorted(praefixe.values())!r}"
        )
    erkannt = next(iter(praefixe.values())) if praefixe else (
        "" if praefix == "auto" else praefix
    )
    return gefunden, erkannt


def befehl_gruppe(args: argparse.Namespace) -> int:
    gruppe = require_group(args.gruppe)
    _melde(f"Tabelle: {loader_contract()}")
    _melde(f"{gruppe.name}")
    _melde(f"  Kennung            : {gruppe.gm_enum} ({gruppe.gm_index})")
    _melde(f"  Namensformat       : {'gruppe-1' if gruppe.dash_format else 'gruppe 001'}")
    _melde(f"  Material           : {gruppe.material}")
    _melde(f"  Maske              : {gruppe.mask_policy}")
    _melde(f"  eigene Farbtabelle : {'ja' if gruppe.custom_palette else 'nein'}")
    _melde(f"  ID_Offset          : {gruppe.id_offset}")
    _melde(f"  additionalStorage  : {gruppe.additional_storage}")
    if gruppe.needs_foliage_material:
        _melde(
            "  ACHTUNG: Vanilla zeichnet diese Gruppe mit Unlit/Foliage. Der "
            "Atlas-Loader baut Unlit/TeamColour, sobald eine Maske vorliegt."
        )
    try:
        check_overridable_as_atlas(gruppe.name)
    except GroupError as fehler:
        _melde("")
        _melde(f"NICHT ALS ATLAS BAUBAR: {fehler}")
        return 2
    _melde("  als Atlas ueberschreibbar: ja")
    return 0


def befehl_fuellen(args: argparse.Namespace) -> int:
    gruppe = check_overridable_as_atlas(args.gruppe)
    bilder_ordner = Path(args.bilder)
    vorlagen, praefix = _vorlagen(bilder_ordner, args.praefix)
    if not vorlagen:
        _melde(f"Keine Vorlagen in {bilder_ordner} gefunden.")
        return 2
    _melde(f"{len(vorlagen)} eigene Vorlagen, Praefix {praefix!r}")

    ziel = read_groups(args.spiel, [gruppe.name], progress=_melde)[gruppe.name]
    bericht = plan(ziel, vorlagen.keys())
    _melde(bericht.summary())
    if bericht.unknown:
        _melde(
            "Diese Indizes gibt es im Zielspiel nicht und der Loader wuerde sie "
            "verwerfen: " + ", ".join(str(k) for k in bericht.unknown)
        )
        return 2
    if not bericht.missing:
        _melde("Nichts zu ergaenzen.")
        return 0

    will_maske = not gruppe.mask_forbidden
    masken_ordner = Path(args.masken) if args.masken else bilder_ordner
    geschrieben = fill_missing_frames(
        args.spiel,
        ziel,
        bericht.missing,
        colour_dir=bilder_ordner,
        mask_dir=masken_ordner,
        prefix=praefix,
        want_mask=will_maske,
        overwrite=args.ueberschreiben,
        progress=_melde,
    )
    _melde(
        f"{len(bericht.missing)} Slots als unveraenderte Crusader-Kopie ergaenzt, "
        f"{len(geschrieben)} Dateien geschrieben."
    )
    return 0


def befehl_pruefen(args: argparse.Namespace) -> int:
    gruppe = require_group(args.gruppe)
    ziel = read_groups(args.spiel, [gruppe.name], progress=_melde)[gruppe.name]
    referenz = None
    if args.anker == "quelle":
        if not args.quellspiel:
            _melde("FEHLER: --anker quelle braucht --quellspiel")
            return 2
        referenz = read_groups(args.quellspiel, [gruppe.name], progress=_melde)[gruppe.name]
    bericht = verify_atlas(
        ziel, args.atlas, anchor_rule=args.anker, reference=referenz
    )
    _melde("")
    for hinweis in bericht.notes:
        _melde(f"Hinweis: {hinweis}")
    for problem in bericht.problems:
        _melde(f"FEHLER: {problem}")
    _melde("")
    _melde(bericht.summary())
    return 0 if bericht.ok else 1


def befehl_bauen(args: argparse.Namespace) -> int:
    from .build import BuildError, build_group, inputs_from_directory

    gruppe = check_overridable_as_atlas(args.gruppe)
    eingaben = inputs_from_directory(Path(args.bilder), args.masken, args.praefix)
    _melde(f"{len(eingaben)} Vorlagen gelesen")
    ziel = read_groups(args.spiel, [gruppe.name], progress=_melde)[gruppe.name]
    referenz = None
    if args.anker == "quelle":
        if not args.quellspiel:
            _melde("FEHLER: --anker quelle braucht --quellspiel")
            return 2
        referenz = read_groups(args.quellspiel, [gruppe.name], progress=_melde)[gruppe.name]
    try:
        ordner = build_group(
            ziel,
            eingaben,
            Path(args.ausgabe),
            padding=args.abstand,
            anchor_rule=args.anker,
            reference=referenz,
            require_complete=not args.unvollstaendig_erlauben,
        )
    except BuildError as fehler:
        _melde(f"FEHLER: {fehler}")
        return 2
    _melde(f"Gebaut und geprueft: {ordner}")
    return 0


def main(argv: list[str] | None = None) -> int:
    zerleger = argparse.ArgumentParser(
        prog="atlas_contract", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    unter = zerleger.add_subparsers(dest="befehl", required=True)

    p = unter.add_parser("gruppe", help="Was das Spiel ueber eine Ladegruppe weiss")
    p.add_argument("gruppe")
    p.set_defaults(funktion=befehl_gruppe)

    p = unter.add_parser("fuellen", help="Fehlende Slots als Crusader-Kopie ergaenzen")
    p.add_argument("gruppe")
    p.add_argument("--spiel", required=True, help="Pfad auf ..._Data von SHCDE")
    p.add_argument("--bilder", required=True, help="Ordner mit den eigenen Vorlagen")
    p.add_argument("--masken", help="Ordner fuer die Masken, sonst wie --bilder")
    p.add_argument("--praefix", default="auto")
    p.add_argument("--ueberschreiben", action="store_true")
    p.set_defaults(funktion=befehl_fuellen)

    p = unter.add_parser("pruefen", help="Fertigen Atlas gegen das Zielspiel nachrechnen")
    p.add_argument("gruppe")
    p.add_argument("--spiel", required=True, help="Pfad auf ..._Data von SHCDE")
    p.add_argument("--atlas", required=True, help="Ordner mit atlas.json")
    p.add_argument(
        "--anker",
        choices=("ziel", "quelle", "frei"),
        default="ziel",
        help=(
            "ziel: der Frame haelt den Anker des Crusader-Slots (Voreinstellung, "
            "richtig fuer Kacheln und gleich geformte Grafik). quelle: der Frame "
            "haelt den Anker des Sprites, aus dem er stammt, braucht "
            "--quellspiel (richtig fuer einen echten Grafiktausch). frei: der "
            "Versatz wird nur gemeldet."
        ),
    )
    p.add_argument("--quellspiel", help="Pfad auf ..._Data des Quellspiels, fuer --anker quelle")
    p.set_defaults(funktion=befehl_pruefen)

    p = unter.add_parser("bauen", help="Atlas aus einem Vorlagenordner bauen")
    p.add_argument("gruppe")
    p.add_argument("--spiel", required=True, help="Pfad auf ..._Data von SHCDE")
    p.add_argument("--bilder", required=True, help="Ordner mit den Vorlagen")
    p.add_argument("--masken", help="Ordner mit den Masken, sonst wie --bilder")
    p.add_argument("--ausgabe", required=True, help="Zielordner Override/Atlas/<gruppe>")
    p.add_argument("--praefix", default="auto")
    p.add_argument("--abstand", type=int, default=1, help="Randabstand beim Packen")
    p.add_argument("--anker", choices=("ziel", "quelle", "frei"), default="ziel")
    p.add_argument("--quellspiel", help="Pfad auf ..._Data des Quellspiels, fuer --anker quelle")
    p.add_argument(
        "--unvollstaendig-erlauben",
        action="store_true",
        dest="unvollstaendig_erlauben",
        help="Baut auch, wenn Slots fehlen. Der Loader kuerzt dann das Array.",
    )
    p.set_defaults(funktion=befehl_bauen)

    args = zerleger.parse_args(argv)
    try:
        return int(args.funktion(args))
    except (GroupError, TargetError) as fehler:
        _melde(f"FEHLER: {fehler}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
