"""Dieselbe Ankermessung an den Baeumen von Stronghold 1 DE.

Wenn Stronghold 1 seine Baeume mit einem ebenso engen Abstand ueber der
Unterkante fuehrt wie Crusader, dann ist der uebernommene Pivot fuer sich
genommen sauber, und die Frage ist nur, welche der beiden Konventionen im
Zielspiel gilt. Streut unsere Auslieferung dagegen staerker als BEIDE Spiele,
dann ist etwas anderes schiefgegangen.
"""

from __future__ import annotations

import re
import statistics
import sys
from collections import defaultdict
from pathlib import Path

import UnityPy
from PIL import Image

DATA = Path(r"D:/Games/Stronghold Definitive Edition/Stronghold 1 Definitive Edition_Data")
MUSTER = re.compile(r"^(tree_[a-z0-9_]+|Tree_[A-Za-z]+)-(\d+)(x?)$")
ALPHA_SCHWELLE = 8


def masse(bild: Image.Image, pivot_x: float, pivot_y: float):
    alpha = bild.getchannel("A").point(lambda wert: 255 if wert >= ALPHA_SCHWELLE else 0)
    kasten = alpha.getbbox()
    if kasten is None:
        return None
    links, _oben, rechts, unten = kasten
    mitte_x = (links + rechts) / 2.0
    return (
        pivot_x * bild.width - mitte_x,
        pivot_y * bild.height - (bild.height - unten),
    )


def bericht(name: str, werte: list[float]) -> str:
    if not werte:
        return f"{name}: keine Werte"
    return (
        f"{name}: Median {statistics.median(werte):+7.1f}  "
        f"Streuung {statistics.pstdev(werte):6.1f}  "
        f"Spanne {min(werte):+.0f} .. {max(werte):+.0f}  (n={len(werte)})"
    )


def main() -> None:
    primary = DATA / "resources.assets"
    dateien = ([primary] if primary.is_file() else []) + [
        p for p in sorted(DATA.glob("*.assets")) if p != primary
    ]
    gefunden: dict[str, list] = defaultdict(list)
    for pfad in dateien:
        print("lese", pfad.name, flush=True)
        env = UnityPy.load(str(pfad))
        for obj in env.objects:
            if obj.type.name != "Sprite":
                continue
            try:
                daten = obj.read()
            except Exception:
                continue
            name = getattr(daten, "m_Name", "") or ""
            treffer = MUSTER.match(name)
            if treffer is None:
                continue
            gefunden[treffer.group(1)].append((int(treffer.group(2)), obj, daten))
        if gefunden:
            break

    print(f"\nGruppen in Stronghold 1 DE: {sorted(gefunden)}\n")
    for gruppe in sorted(gefunden):
        eintraege = sorted(gefunden[gruppe], key=lambda e: e[0])
        stichprobe = eintraege[:: max(1, len(eintraege) // 40)][:40]
        dx: list[float] = []
        dy: list[float] = []
        groessen: list[tuple[int, int]] = []
        for _index, obj, daten in stichprobe:
            try:
                bild = obj.read().image.convert("RGBA")
            except Exception:
                continue
            gemessen = masse(bild, float(daten.m_Pivot.x), float(daten.m_Pivot.y))
            if gemessen is None:
                continue
            dx.append(gemessen[0])
            dy.append(gemessen[1])
            groessen.append(bild.size)
        print(f"{gruppe}: {len(eintraege)} Frames")
        print("   " + bericht("dx zur Mitte    ", dx))
        print("   " + bericht("dy ueber Unterk.", dy))


if __name__ == "__main__":
    main()
