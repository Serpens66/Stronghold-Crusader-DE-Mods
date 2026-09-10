"""Tests der Namenszerlegung.

Sprite-Namen sind ``<gruppe><trenner><index>[x]``. Der Gruppenname darf selbst
Trenner enthalten und sogar selbst auf Trenner und Ziffer enden. Deshalb ist
die Zerlegung mehrdeutig, und der laengste bekannte Gruppenname gewinnt.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from atlas_contract.groups import LOADER_GROUPS  # noqa: E402
from atlas_contract.target import FrameKey, _zerlegungen  # noqa: E402


def erste(name: str):
    return next(iter(_zerlegungen(name)), None)


def test_bindestrichformat():
    assert erste("tree_apple-15") == ("tree_apple", True, FrameKey(15))


def test_alternativrahmen():
    assert erste("body_lord-7x") == ("body_lord", True, FrameKey(7, True))


def test_leerzeichenformat_mit_fuehrenden_nullen():
    assert erste("anim_castle 001") == ("anim_castle", False, FrameKey(1))


def test_gruppenname_mit_leerzeichen():
    assert erste("puff of smoke 003") == ("puff of smoke", False, FrameKey(3))


def test_gruppenname_mit_bindestrich_und_ziffern():
    assert erste("smoke-30x30-5") == ("smoke-30x30", True, FrameKey(5))


def test_gruppenname_endet_selbst_auf_ziffer():
    """``float_pop_circ-1`` ist ein Gruppenname, kein Frame von ``float_pop_circ``."""

    aufteilungen = list(_zerlegungen("float_pop_circ-1-0"))
    assert aufteilungen == [("float_pop_circ-1", True, FrameKey(0))]
    assert "float_pop_circ-1" in LOADER_GROUPS
    # Und der Frame 1 der kuerzeren Gruppe heisst schlicht anders.
    assert list(_zerlegungen("float_pop_circ-1")) == [
        ("float_pop_circ", True, FrameKey(1))
    ]


def test_es_gibt_hoechstens_eine_zerlegung():
    """Hinter dem Trenner duerfen nur Ziffern stehen, das macht die Sache eindeutig.

    Ein frueherer Trenner scheidet immer aus, weil sein Rest den spaeteren
    Trenner enthaelt. ``float_pop_circ-1-0`` ist damit eindeutig Frame 0 der
    Gruppe ``float_pop_circ-1`` und nicht Frame 1 von ``float_pop_circ``.
    """

    for name in ("a-1-2", "float_pop_circ-1-0", "smoke-30x30-5", "puff of smoke 003"):
        assert len(list(_zerlegungen(name))) == 1, name


def test_ohne_index_keine_zerlegung():
    assert erste("body_lord") is None
    assert erste("White1px") is None


def test_trenner_am_anfang_zaehlt_nicht():
    assert erste("-5") is None


def test_jede_ladegruppe_ist_wiederauffindbar():
    """Fuer jede der 195 Gruppen muss der eigene Name zurueckkommen."""

    for name, gruppe in LOADER_GROUPS.items():
        beispiel = f"{name}-7" if gruppe.dash_format else f"{name} 007"
        treffer = [teil for teil in _zerlegungen(beispiel) if teil[0] == name]
        assert treffer, f"{name}: {beispiel!r} laesst sich nicht zurueckfuehren"
        assert treffer[0][1] is gruppe.dash_format
        assert treffer[0][2] == FrameKey(7)
