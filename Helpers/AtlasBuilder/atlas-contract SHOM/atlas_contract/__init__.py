"""Bausteine fuer korrekte SHCDE-SE-Atlanten.

Vier Dinge, die ein Atlasbauer aus dem Spiel wissen muss und die sich nicht
aus den gelieferten PNG-Dateien ableiten lassen:

* :mod:`atlas_contract.anchor` - wohin ein Frame gehoert, wenn seine Leinwand
  nicht die Groesse des Crusader-Slots hat.
* :mod:`atlas_contract.groups` - welche Gruppe sich ueberhaupt als Atlas
  ueberschreiben laesst und mit welchem Material sie geladen wird.
* :mod:`atlas_contract.target` - was der Zielslot vorgibt, und die
  Vanilla-Grafik jedes Slots ohne eigene Vorlage.
* :mod:`atlas_contract.completeness` - der vollstaendige Namensatz einer
  Gruppe, damit der Loader das Array nicht kuerzt.

Alle Werte stammen aus dem Dekompilat der lokal angehefteten Spielstaende.
Ein Spiel- oder Extender-Update macht sie ungueltig, bis
``werkzeuge/tabelle_erzeugen.py`` neu gelaufen ist.
"""

from __future__ import annotations

from .anchor import (
    ANCHOR_TOLERANCE_PX,
    CORNER_PIVOT_EPSILON,
    AnchorError,
    reanchored_pivot,
    reanchored_pivot_xy,
    target_anchor,
    verify_pivot,
    verify_pivot_xy,
)
from .groups import (
    LOADER_GROUPS,
    GroupError,
    LoaderGroup,
    check_mask_policy,
    check_overridable_as_atlas,
    find_group,
    loader_contract,
    require_group,
)

__all__ = [
    "ANCHOR_TOLERANCE_PX",
    "CORNER_PIVOT_EPSILON",
    "AnchorError",
    "GroupError",
    "LOADER_GROUPS",
    "LoaderGroup",
    "check_mask_policy",
    "check_overridable_as_atlas",
    "find_group",
    "loader_contract",
    "reanchored_pivot",
    "reanchored_pivot_xy",
    "require_group",
    "target_anchor",
    "verify_pivot",
    "verify_pivot_xy",
]

__version__ = "1.2.0"
