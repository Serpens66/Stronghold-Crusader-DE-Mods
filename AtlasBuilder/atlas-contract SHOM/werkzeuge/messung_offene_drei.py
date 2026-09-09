"""Die drei Gruppen mit ungeklaerten Frames einzeln nachpruefen.

Der Reihenvergleich Index gegen Index passt dort nicht, weil die Frames aus
einem anderen Index oder einer anderen Gruppe stammen. Hier wird deshalb
gefragt: gibt es UEBERHAUPT ein Quellsprite, dessen Anker den gelieferten
Pivot erklaert?
"""

from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path

PAKET = Path(
    r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
    r"/_Veroeffentlichung/atlas-contract"
)
sys.path.insert(0, str(PAKET))

from atlas_contract import target  # noqa: E402
from atlas_contract.anchor import AnchorError, verify_pivot_xy  # noqa: E402

SHC = (
    r"D:/Games/Stronghold Crusader Definitive Edition"
    r"/Stronghold Crusader Definitive Edition_Data"
)
SH1 = r"D:/Games/Stronghold Definitive Edition/Stronghold 1 Definitive Edition_Data"
ATLAS = Path(
    r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
    r"/SE144/dist/europe-production/StrongholdEuropeDE/Override/Atlas"
)
GRUPPEN = ["body_lord", "anim_castle", "tree_birch"]


def passt(px, py, bezug, breite, hoehe) -> bool:
    try:
        verify_pivot_xy(
            px, py, bezug.pivot_x, bezug.pivot_y, bezug.width, bezug.height, breite, hoehe
        )
        return True
    except AnchorError:
        return False


def main() -> None:
    ziele = target.read_groups(SHC, GRUPPEN)
    quellen = target.read_groups(SH1, GRUPPEN + ["Tree_Oak"], require_all=False)
    print("Quellgruppen gefunden:", sorted(quellen), "\n")

    for name in GRUPPEN:
        ziel = ziele[name]
        quelle = quellen.get(name)
        nutzlast = json.loads((ATLAS / name / "atlas.json").read_text(encoding="utf-8"))
        nach_name = {f.name: f for f in ziel.frames.values()}
        zaehler = Counter()
        beispiele: list[str] = []
        groessen_treffer = 0

        for eintrag in nutzlast["frames"]:
            z = nach_name.get(eintrag["name"])
            if z is None:
                zaehler["kein Zielslot"] += 1
                continue
            r = eintrag["rect"]
            px, py = eintrag["pivot"]["x"], eintrag["pivot"]["y"]
            if passt(px, py, z, r["w"], r["h"]):
                zaehler["Zielanker"] += 1
                continue
            if quelle is not None:
                q = quelle.frames.get(z.key)
                if q is not None and passt(px, py, q, r["w"], r["h"]):
                    zaehler["Quellanker, gleicher Index"] += 1
                    continue
            # Ein anderer Index derselben Quellgruppe?
            treffer = None
            if quelle is not None:
                for kandidat in quelle.frames.values():
                    if (round(kandidat.width), round(kandidat.height)) != (
                        r["w"],
                        r["h"],
                    ):
                        continue
                    groessen_treffer += 1
                    if passt(px, py, kandidat, r["w"], r["h"]):
                        treffer = kandidat
                        break
            if treffer is not None:
                zaehler["Quellanker, anderer Index"] += 1
                continue
            # Eine andere Quellgruppe?
            fremd = None
            for gruppenname, andere in quellen.items():
                if gruppenname == name:
                    continue
                kandidat = andere.frames.get(z.key)
                if kandidat is not None and passt(px, py, kandidat, r["w"], r["h"]):
                    fremd = gruppenname
                    break
            if fremd is not None:
                zaehler[f"Quellanker aus {fremd}"] += 1
                continue
            zaehler["ungeklaert"] += 1
            if len(beispiele) < 6:
                erwartet_x = z.pivot_x * z.width
                beispiele.append(
                    f"{eintrag['name']}: geliefert {px*r['w']:.1f}/{py*r['h']:.1f} "
                    f"auf {r['w']}x{r['h']}, Zielanker {erwartet_x:.1f}/"
                    f"{z.pivot_y*z.height:.1f} auf {z.width:.0f}x{z.height:.0f}"
                )

        print(f"=== {name}: {sum(zaehler.values())} Frames ===")
        for etikett, anzahl in zaehler.most_common():
            print(f"   {etikett:<32}{anzahl:>6}")
        for zeile in beispiele:
            print(f"      {zeile}")
        print()


if __name__ == "__main__":
    main()
