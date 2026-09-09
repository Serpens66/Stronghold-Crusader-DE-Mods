"""Tests der Ankerumrechnung. Brauchen kein Spiel und keine Assets."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from atlas_contract.anchor import (  # noqa: E402
    AnchorError,
    reanchored_pivot,
    reanchored_pivot_xy,
    target_anchor,
    verify_pivot,
)


def test_gleiche_groesse_aendert_nichts():
    """Passt die Leinwand, bleibt der Zielpivot unveraendert."""

    for pivot in (0.0, 0.25, 0.4906, 0.5, 0.99999):
        assert reanchored_pivot(pivot, 212.0, 212.0) == pytest.approx(pivot)


def test_anker_bleibt_in_pixeln_gleich():
    """Der absolute Anker ist die Groesse, die erhalten bleibt."""

    ziel_pivot, ziel_breite = 0.25, 200.0
    erwartet = 50.0
    for quelle in (50.0, 137.0, 200.0, 640.0):
        pivot = reanchored_pivot(ziel_pivot, ziel_breite, quelle)
        assert pivot * quelle == pytest.approx(erwartet)


def test_ohne_umrechnung_verschiebt_es_sich():
    """Der Fehler, um den es geht: gleicher Pivot, andere Groesse."""

    ziel_pivot, ziel_breite, quell_breite = 0.5, 100.0, 160.0
    falsch = ziel_pivot * quell_breite
    richtig = target_anchor(ziel_pivot, ziel_breite, quell_breite)
    assert richtig == pytest.approx(50.0)
    assert falsch - richtig == pytest.approx(30.0)


def test_eckkonvention_haelt_die_kante():
    """Pivot exakt 1,0 misst von der gegenueberliegenden Kante, Abstand null."""

    assert target_anchor(1.0, 183.0, 165.0) == pytest.approx(165.0)
    assert reanchored_pivot(1.0, 183.0, 165.0) == pytest.approx(1.0)


def test_eckkonvention_ohne_ausnahme_waere_18_px_daneben():
    """Der gemessene anim_castle-Fall: 183 - 165 = 18 px."""

    ziel_hoehe, quell_hoehe = 183.0, 165.0
    mit_ausnahme = target_anchor(1.0, ziel_hoehe, quell_hoehe)
    ohne_ausnahme = 1.0 * ziel_hoehe
    assert ohne_ausnahme - mit_ausnahme == pytest.approx(18.0)


def test_eckkonvention_nur_bei_exakt_eins():
    """Bruchteil-Pivots sind echte Layoutpunkte und bleiben unangetastet."""

    assert target_anchor(0.999, 183.0, 165.0) == pytest.approx(0.999 * 183.0)


def test_null_pivot_bleibt_null():
    """Die linke beziehungsweise untere Kante ist Abstand null."""

    assert target_anchor(0.0, 183.0, 165.0) == 0.0
    assert reanchored_pivot(0.0, 183.0, 165.0) == 0.0


def test_pivot_darf_ueber_eins_liegen():
    """Ist die Quelle kleiner als das Ziel, verlaesst der Pivot [0,1].

    Eine Pruefung auf 0 <= pivot <= 1 wuerde hier korrekte Ausgaben verwerfen.
    Das Spiel selbst liefert ebenfalls Pivots ausserhalb des Bereichs aus.
    """

    pivot = reanchored_pivot(0.9, 200.0, 100.0)
    assert pivot == pytest.approx(1.8)


def test_beide_achsen_sind_unabhaengig():
    x, y = reanchored_pivot_xy(0.5, 1.0, 100.0, 183.0, 160.0, 165.0)
    assert x == pytest.approx(50.0 / 160.0)
    assert y == pytest.approx(1.0)


def test_verify_nimmt_den_eigenen_wert_an():
    pivot = reanchored_pivot(0.4906, 212.0, 157.0)
    ergebnis = verify_pivot(pivot, 0.4906, 212.0, 157.0)
    assert ergebnis.anchor_px == pytest.approx(0.4906 * 212.0)
    assert ergebnis.corner_convention is False


def test_verify_faengt_den_unveraenderten_pivot():
    """Genau der Fehler, den ein Pixelvergleich nicht sehen kann."""

    with pytest.raises(AnchorError) as fehler:
        verify_pivot(0.4906, 0.4906, 212.0, 157.0, label="tree-1", axis="x")
    assert "verschoben" in str(fehler.value)


def test_verify_faengt_die_fehlende_eckausnahme():
    falsch = 1.0 * 183.0 / 165.0
    with pytest.raises(AnchorError):
        verify_pivot(falsch, 1.0, 183.0, 165.0)


def test_groessen_muessen_positiv_sein():
    for ziel, quelle in ((0.0, 10.0), (10.0, 0.0), (-1.0, 10.0)):
        with pytest.raises(AnchorError):
            target_anchor(0.5, ziel, quelle)
