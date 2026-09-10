"""Der entscheidende Vergleich: gleiche Grafik, gleicher Pivot?

Zwei Baumgruppen liegen in beiden Spielen offenbar unveraendert vor. Wenn dort
auch die Pivots uebereinstimmen, benutzen beide Spiele DIESELBE Konvention.
Dann kommt jeder Unterschied bei den anderen Gruppen daher, dass die Grafik
eine andere ist, und nicht daher, dass die Spiele verschieden ankern.

Genau daran haengt die Frage, ob unsere Baumatlanten richtig verankert sind.
"""

from __future__ import annotations

import hashlib
import re
import statistics
import sys
from pathlib import Path

import UnityPy

SH1 = Path(r"D:/Games/Stronghold Definitive Edition/Stronghold 1 Definitive Edition_Data")
SHC = Path(
    r"D:/Games/Stronghold Crusader Definitive Edition"
    r"/Stronghold Crusader Definitive Edition_Data"
)
GRUPPEN = ("tree_apple", "tree_shrub1", "tree_shrub2", "tree_birch", "Tree_Oak", "tree_pine", "Tree_Chestnut")
MUSTER = re.compile(r"^([A-Za-z_0-9]+)-(\d+)(x?)$")


def lesen(data: Path, gruppen: tuple[str, ...]) -> dict[str, dict[int, dict]]:
    ergebnis: dict[str, dict[int, dict]] = {g: {} for g in gruppen}
    gefaltet = {g.casefold(): g for g in gruppen}
    env = UnityPy.load(str(data / "resources.assets"))
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        try:
            daten = obj.read()
        except Exception:
            continue
        name = getattr(daten, "m_Name", "") or ""
        treffer = MUSTER.match(name)
        if treffer is None or treffer.group(3):
            continue
        gruppe = gefaltet.get(treffer.group(1).casefold())
        if gruppe is None:
            continue
        rect = daten.m_Rect
        ergebnis[gruppe][int(treffer.group(2))] = {
            "obj": obj,
            "w": float(rect.width),
            "h": float(rect.height),
            "px": float(daten.m_Pivot.x),
            "py": float(daten.m_Pivot.y),
            "ppu": float(daten.m_PixelsToUnits),
        }
    return ergebnis


def main() -> None:
    print("lese Stronghold 1 ...", flush=True)
    a = lesen(SH1, GRUPPEN)
    print("lese Crusader ...", flush=True)
    b = lesen(SHC, GRUPPEN)

    print(
        f"\n{'Gruppe':<16}{'gemeinsam':>10}{'Groesse=':>10}{'Pivot=':>9}"
        f"{'Pixel=':>9}   Anker-Abstand in px (Median / Max)"
    )
    print("-" * 92)
    for gruppe in GRUPPEN:
        gemeinsam = sorted(set(a[gruppe]) & set(b[gruppe]))
        if not gemeinsam:
            print(f"{gruppe:<16}  keine gemeinsamen Indizes")
            continue
        stichprobe = gemeinsam[:: max(1, len(gemeinsam) // 40)][:40]
        groesse_gleich = pivot_gleich = pixel_gleich = 0
        abstaende: list[float] = []
        for index in stichprobe:
            links, rechts = a[gruppe][index], b[gruppe][index]
            gleiche_groesse = (links["w"], links["h"]) == (rechts["w"], rechts["h"])
            groesse_gleich += gleiche_groesse
            if (
                abs(links["px"] - rechts["px"]) < 1e-6
                and abs(links["py"] - rechts["py"]) < 1e-6
            ):
                pivot_gleich += 1
            if gleiche_groesse:
                # Abstand der beiden Anker in Pixeln, auf derselben Leinwand
                dx = (links["px"] - rechts["px"]) * links["w"]
                dy = (links["py"] - rechts["py"]) * links["h"]
                abstaende.append((dx * dx + dy * dy) ** 0.5)
                try:
                    ha = hashlib.sha256(
                        links["obj"].read().image.convert("RGBA").tobytes()
                    ).digest()
                    hb = hashlib.sha256(
                        rechts["obj"].read().image.convert("RGBA").tobytes()
                    ).digest()
                    pixel_gleich += ha == hb
                except Exception:
                    pass
        n = len(stichprobe)
        abstand = (
            f"{statistics.median(abstaende):6.2f} / {max(abstaende):6.2f}"
            if abstaende
            else "   n/a"
        )
        print(
            f"{gruppe:<16}{n:>10}{groesse_gleich:>10}{pivot_gleich:>9}"
            f"{pixel_gleich:>9}   {abstand}"
        )


if __name__ == "__main__":
    main()
