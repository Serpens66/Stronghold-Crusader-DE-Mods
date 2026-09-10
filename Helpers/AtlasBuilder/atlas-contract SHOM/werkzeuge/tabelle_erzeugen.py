"""Erzeugt ``atlas_contract/loader_groups.json`` aus unserer Ladetabelle.

Quellen, alle drei im Dekompilat der lokal angehefteten Spielstände:

* ``spriteLoader.SpriteLoad`` - die 195 ``addGMFile``-Registrierungen. Sie
  stehen bei uns bereits als ``LOADER_GROUP_TABLE`` und werden hier nur
  serialisiert.
* ``Enums.GM`` - der Zahlenwert je Kennung.
* ``SpriteMapping.cs`` - die Ausnahme vom Farb-Remap. Daraus leiten wir ab,
  welche Gruppen Vanilla mit ``Unlit/Foliage`` zeichnet, statt eine Liste von
  Hand zu führen.

Das Ergebnis ist eine Obermenge von Serps ``supported_gm_groups.json``: die
Felder ``name`` und ``dashFormat`` bleiben unverändert an ihrer Stelle, alles
Weitere kommt dazu.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

PROJEKT = Path(__file__).resolve().parents[3]
SE144_SRC = PROJEKT / "SE144" / "src"
DEKOMPILAT = PROJEKT / "StrongholdEuropeDE" / "cache" / "decompiled" / "shcde28_project"
ZIEL = Path(__file__).resolve().parents[1] / "atlas_contract" / "loader_groups.json"

sys.path.insert(0, str(SE144_SRC))
from stronghold_europe_de.loader_groups import LOADER_GROUP_TABLE  # noqa: E402


def gm_indizes() -> dict[str, int]:
    """``GM_*`` auf seinen Zahlenwert, gelesen aus ``Enums.cs``."""

    text = (DEKOMPILAT / "Enums.cs").read_text(encoding="utf-8", errors="replace")
    # Das letzte Mitglied einer Aufzaehlung traegt kein Komma.
    treffer = re.findall(r"^\s*(GM_[A-Z0-9_]+)\s*=\s*(\d+)\s*,?\s*$", text, re.MULTILINE)
    if not treffer:
        raise SystemExit("Keine GM-Konstanten in Enums.cs gefunden")
    return {name: int(wert) for name, wert in treffer}


def foliage_indizes() -> set[int]:
    """Die Gruppen, die Vanilla mit ``Unlit/Foliage`` zeichnet.

    ``SpriteMapping`` wendet den Farb-Remap auf alles an AUSSER auf diese
    Kennungen. Wir lesen die Bedingung, statt die sieben Namen zu tippen, damit
    ein Spielupdate hier auffliegt und nicht stillschweigend danebenliegt.
    """

    text = (DEKOMPILAT / "SpriteMapping.cs").read_text(encoding="utf-8", errors="replace")
    muster = re.compile(
        r"\(uint\)\(file - (\d+)\) > (\d+)u && \(uint\)\(file - (\d+)\) > (\d+)u"
        r" && file != (\d+)"
    )
    treffer = muster.search(text)
    if treffer is None:
        raise SystemExit(
            "Die Foliage-Ausnahme in SpriteMapping.cs sieht nicht mehr aus wie "
            "erwartet - die Ableitung muss neu geprueft werden"
        )
    erste_basis, erste_spanne, zweite_basis, zweite_spanne, einzeln = (
        int(wert) for wert in treffer.groups()
    )
    ergebnis = {einzeln}
    for basis, spanne in ((erste_basis, erste_spanne), (zweite_basis, zweite_spanne)):
        # `(uint)(file - basis) > spanne` ist falsch fuer basis .. basis+spanne
        ergebnis.update(range(basis, basis + spanne + 1))
    return ergebnis


def main() -> None:
    indizes = gm_indizes()
    foliage = foliage_indizes()

    gruppen = []
    for name, gruppe in LOADER_GROUP_TABLE.items():
        if gruppe.gm_enum not in indizes:
            raise SystemExit(f"{name}: {gruppe.gm_enum} steht nicht in Enums.cs")
        gm_index = indizes[gruppe.gm_enum]
        if gruppe.plain_shader:
            material = "plain"
        elif gm_index in foliage:
            material = "foliage"
        else:
            material = "teamcolour"
        gruppen.append(
            {
                "name": name,
                "dashFormat": gruppe.dash_format,
                "gmEnum": gruppe.gm_enum,
                "gmIndex": gm_index,
                "declaredImageCount": gruppe.declared_image_count,
                "idOffset": gruppe.id_offset,
                "additionalStorage": gruppe.additional_storage,
                "overridableAsAtlas": gruppe.owns_its_gm_arrays,
                "material": material,
                "maskPolicy": "forbidden" if material == "plain" else "required",
                "customPalette": gruppe.custom_palette,
            }
        )

    gruppen.sort(key=lambda eintrag: eintrag["name"].casefold())
    nutzlast = {
        "sourceContract": "SHCDE 2.80 spriteLoader.SpriteLoad (195 addGMFile)",
        "schema": 1,
        "groups": gruppen,
    }
    ZIEL.parent.mkdir(parents=True, exist_ok=True)
    ZIEL.write_text(
        json.dumps(nutzlast, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )

    nicht_ueberschreibbar = [g["name"] for g in gruppen if not g["overridableAsAtlas"]]
    print(f"geschrieben: {ZIEL}")
    print(f"  Gruppen              : {len(gruppen)}")
    print(f"  Foliage-Kennungen    : {sorted(foliage)}")
    print(f"  material=foliage     : {sum(1 for g in gruppen if g['material']=='foliage')}")
    print(f"  material=plain       : {sum(1 for g in gruppen if g['material']=='plain')}")
    print(f"  material=teamcolour  : {sum(1 for g in gruppen if g['material']=='teamcolour')}")
    print(f"  nicht ueberschreibbar: {nicht_ueberschreibbar}")


if __name__ == "__main__":
    main()
