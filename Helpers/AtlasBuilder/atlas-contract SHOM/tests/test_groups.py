"""Tests der Ladetabelle. Brauchen kein Spiel und keine Assets."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from atlas_contract.groups import (  # noqa: E402
    LOADER_GROUPS,
    GroupError,
    check_mask_policy,
    check_overridable_as_atlas,
    find_group,
    require_group,
)

FOLIAGE = {
    "tree_birch",
    "tree_pine",
    "Tree_Chestnut",
    "Tree_Oak",
    "tree_shrub1",
    "tree_shrub2",
    "tree_apple",
}


def test_tabelle_hat_195_registrierungen():
    """So viele ``addGMFile``-Aufrufe stehen im Ladecode."""

    assert len(LOADER_GROUPS) == 195


def test_genau_zwei_gruppen_sind_gesperrt():
    gesperrt = {
        name for name, g in LOADER_GROUPS.items() if not g.overridable_as_atlas
    }
    assert gesperrt == {"tile_sea_new_01", "tile_sea_shore"}


def test_beide_gesperrten_gruppen_teilen_eine_kennung():
    """Deshalb zerstoert ein Override die Arrays der jeweils anderen."""

    kennungen = {LOADER_GROUPS[n].gm_enum for n in ("tile_sea_new_01", "tile_sea_shore")}
    assert kennungen == {"GM_NEW_SEA"}


def test_sperre_bricht_ab():
    for name in ("tile_sea_shore", "tile_sea_new_01"):
        with pytest.raises(GroupError) as fehler:
            check_overridable_as_atlas(name)
        assert "GM_NEW_SEA" in str(fehler.value)


def test_die_sieben_vegetationsgruppen():
    """Abgeleitet aus der Farb-Remap-Ausnahme, nicht von Hand gepflegt."""

    foliage = {name for name, g in LOADER_GROUPS.items() if g.needs_foliage_material}
    assert foliage == FOLIAGE


def test_foliage_kennungen_sind_die_erwarteten():
    indizes = sorted(LOADER_GROUPS[n].gm_index for n in FOLIAGE)
    assert indizes == [29, 30, 31, 70, 71, 72, 97]


def test_plain_shader_gruppen_duerfen_keine_maske_haben():
    plain = [name for name, g in LOADER_GROUPS.items() if g.material == "plain"]
    assert len(plain) == 69
    for name in plain:
        assert LOADER_GROUPS[name].mask_policy == "forbidden"


def test_kaktus_ist_kein_baum():
    """Sieht aus wie Vegetation, wird aber ohne Maske und weiss geladen."""

    kaktus = require_group("tree_cactii")
    assert kaktus.material == "plain"
    assert kaktus.mask_forbidden
    with pytest.raises(GroupError):
        check_mask_policy("tree_cactii", has_mask=True)


def test_maske_fehlt_wird_beanstandet():
    with pytest.raises(GroupError):
        check_mask_policy("tree_birch", has_mask=False)
    assert check_mask_policy("tree_birch", has_mask=True).name == "tree_birch"


def test_suche_ignoriert_gross_und_kleinschreibung():
    assert find_group("TREE_OAK") is not None
    assert find_group("tree_oak").name == "Tree_Oak"
    assert find_group("gibtsnicht") is None


def test_unbekannte_gruppe_bricht_ab():
    with pytest.raises(GroupError):
        require_group("gibtsnicht")


def test_namensformat_ist_gemischt():
    """164 Gruppen nummerieren mit Bindestrich, der Rest mit Leerzeichen."""

    mit_strich = sum(1 for g in LOADER_GROUPS.values() if g.dash_format)
    assert mit_strich == 164
    assert len(LOADER_GROUPS) - mit_strich == 31
