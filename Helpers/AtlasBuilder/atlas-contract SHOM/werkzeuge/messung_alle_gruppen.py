"""Gilt der Baum-Befund auch fuer Kacheln, Gebaeude und alles Uebrige?

Nimmt jede ausgelieferte Atlasgruppe und ordnet jeden Frame einer Ankerregel
zu:

``Ziel``    der Anker des Crusader-Slots
``Quelle``  der Anker des gleichnamigen Sprites in Stronghold 1
``beides``  beide Regeln liefern denselben Punkt, die Frage stellt sich nicht
``keiner``  weder noch, gehoert angesehen

Zusaetzlich wird gezaehlt, bei wie vielen Frames Ziel und Quelle ueberhaupt
dieselbe Leinwandgroesse haben. Wo das der Fall ist, sind beide Regeln
identisch, und der ganze Streit ist gegenstandslos.
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
from atlas_contract.groups import find_group  # noqa: E402

SHC = (
    r"D:/Games/Stronghold Crusader Definitive Edition"
    r"/Stronghold Crusader Definitive Edition_Data"
)
SH1 = r"D:/Games/Stronghold Definitive Edition/Stronghold 1 Definitive Edition_Data"
ATLAS = Path(
    r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
    r"/SE144/dist/europe-production/StrongholdEuropeDE/Override/Atlas"
)


def passt(pivot_x, pivot_y, bezug, breite, hoehe) -> bool:
    try:
        verify_pivot_xy(
            pivot_x,
            pivot_y,
            bezug.pivot_x,
            bezug.pivot_y,
            bezug.width,
            bezug.height,
            breite,
            hoehe,
        )
        return True
    except AnchorError:
        return False


def main() -> None:
    namen = sorted(p.name for p in ATLAS.iterdir() if (p / "atlas.json").is_file())
    bekannt = [n for n in namen if find_group(n) is not None]
    print(f"{len(namen)} ausgelieferte Gruppen, davon {len(bekannt)} in der Ladetabelle")
    if len(bekannt) != len(namen):
        print("  nicht in der Tabelle:", sorted(set(namen) - set(bekannt)))

    print("\nlese Crusader ...", flush=True)
    ziele = target.read_groups(SHC, bekannt)
    print("lese Stronghold 1 ...", flush=True)
    quellen = target.read_groups(SH1, bekannt, require_all=False)
    print(f"  {len(quellen)} der {len(bekannt)} Gruppen gibt es auch in Stronghold 1\n")

    gesamt = Counter()
    gleiche_leinwand = 0
    frames_gesamt = 0
    auffaellig: list[tuple[str, int, int]] = []

    kopf = f"{'Gruppe':<26}{'Frames':>7}{'Ziel':>7}{'Quelle':>8}{'beides':>8}{'keiner':>8}"
    print(kopf)
    print("-" * len(kopf))
    for name in bekannt:
        ziel = ziele[name]
        quelle = quellen.get(name)
        nutzlast = json.loads((ATLAS / name / "atlas.json").read_text(encoding="utf-8"))
        nach_name = {f.name: f for f in ziel.frames.values()}
        zaehler = Counter()
        for eintrag in nutzlast["frames"]:
            z = nach_name.get(eintrag["name"])
            if z is None:
                zaehler["keiner"] += 1
                continue
            r = eintrag["rect"]
            px, py = eintrag["pivot"]["x"], eintrag["pivot"]["y"]
            ok_ziel = passt(px, py, z, r["w"], r["h"])
            q = quelle.frames.get(z.key) if quelle is not None else None
            ok_quelle = q is not None and passt(px, py, q, r["w"], r["h"])
            if q is not None and (q.width, q.height) == (z.width, z.height):
                gleiche_leinwand += 1
            if ok_ziel and ok_quelle:
                zaehler["beides"] += 1
            elif ok_ziel:
                zaehler["Ziel"] += 1
            elif ok_quelle:
                zaehler["Quelle"] += 1
            else:
                zaehler["keiner"] += 1
        anzahl = sum(zaehler.values())
        frames_gesamt += anzahl
        gesamt.update(zaehler)
        if zaehler["keiner"]:
            auffaellig.append((name, zaehler["keiner"], anzahl))
        print(
            f"{name:<26}{anzahl:>7}{zaehler['Ziel']:>7}{zaehler['Quelle']:>8}"
            f"{zaehler['beides']:>8}{zaehler['keiner']:>8}"
        )

    print("-" * len(kopf))
    print(
        f"{'SUMME':<26}{frames_gesamt:>7}{gesamt['Ziel']:>7}{gesamt['Quelle']:>8}"
        f"{gesamt['beides']:>8}{gesamt['keiner']:>8}"
    )
    print(
        f"\nFrames, bei denen Ziel und Quelle dieselbe Leinwandgroesse haben: "
        f"{gleiche_leinwand} von {frames_gesamt} "
        f"({100.0*gleiche_leinwand/max(1,frames_gesamt):.1f} %)"
    )
    if auffaellig:
        print("\nGruppen mit ungeklaerten Frames:")
        for name, offen, anzahl in sorted(auffaellig, key=lambda e: -e[1]):
            print(f"   {name:<26}{offen:>6} von {anzahl}")


if __name__ == "__main__":
    main()
