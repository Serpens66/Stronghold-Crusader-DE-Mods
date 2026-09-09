"""Wo liegt der Ankerpunkt eines Baumes relativ zu seiner gezeichneten Flaeche?

Die Frage hinter der Beanstandung: unsere Baumatlanten uebernehmen den Pivot
der Stronghold-1-Quelle, statt den absoluten Anker des Crusader-Slots zu
halten. Welche der beiden Regeln stimmt, laesst sich messen, ohne das Spiel zu
starten.

Vorgehen: fuer jeden Frame den Ankerpunkt in Pixeln ausrechnen und ihn mit dem
umschliessenden Rechteck der sichtbaren Pixel vergleichen.

* ``dx``  Abstand des Ankers von der waagerechten Mitte der sichtbaren Flaeche
* ``dy``  Abstand des Ankers ueber der Unterkante der sichtbaren Flaeche

Stimmen diese beiden Groessen zwischen Crusaders eigenen Baeumen und unseren
gelieferten Frames ueberein, dann stehen unsere Baeume genauso auf ihrer Kachel
wie Crusaders eigene. Dann ist der uebernommene Pivot richtig und die
Umrechnung auf den Crusader-Anker waere falsch.
"""

from __future__ import annotations

import json
import statistics
import sys
from pathlib import Path

from PIL import Image

PAKET = Path(
    r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
    r"/_Veroeffentlichung/atlas-contract"
)
sys.path.insert(0, str(PAKET))

from atlas_contract import target  # noqa: E402
from atlas_contract.anchor import target_anchor  # noqa: E402

DATA = (
    r"D:/Games/Stronghold Crusader Definitive Edition"
    r"/Stronghold Crusader Definitive Edition_Data"
)
ATLAS = Path(
    r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
    r"/SE144/dist/europe-production/StrongholdEuropeDE/Override/Atlas"
)
GRUPPEN = [
    "tree_birch",
    "tree_pine",
    "Tree_Oak",
    "Tree_Chestnut",
    "tree_apple",
    "tree_shrub1",
    "tree_shrub2",
]
ALPHA_SCHWELLE = 8


def masse(bild: Image.Image, pivot_x: float, pivot_y: float):
    """Anker relativ zur sichtbaren Flaeche. Gibt (dx, dy, breite, hoehe)."""

    alpha = bild.getchannel("A").point(lambda wert: 255 if wert >= ALPHA_SCHWELLE else 0)
    kasten = alpha.getbbox()
    if kasten is None:
        return None
    links, oben, rechts, unten = kasten
    mitte_x = (links + rechts) / 2.0
    # Unterkante der sichtbaren Flaeche, gezaehlt von unten wie der Pivot.
    unterkante_von_unten = bild.height - unten
    anker_x = pivot_x * bild.width
    anker_y = pivot_y * bild.height
    return (
        anker_x - mitte_x,
        anker_y - unterkante_von_unten,
        rechts - links,
        unten - oben,
    )


def bericht(name: str, werte: list[float]) -> str:
    if not werte:
        return f"{name}: keine Werte"
    return (
        f"{name}: Median {statistics.median(werte):+7.1f}  "
        f"Mittel {statistics.fmean(werte):+7.1f}  "
        f"Streuung {statistics.pstdev(werte):6.1f}  "
        f"Spanne {min(werte):+.0f} .. {max(werte):+.0f}  (n={len(werte)})"
    )


def main() -> None:
    print("Lese Crusader-Zielmetadaten ...")
    ziele = target.read_groups(DATA, GRUPPEN, progress=lambda s: print("  ", s))

    for name in GRUPPEN:
        gruppe = ziele[name]
        keys = gruppe.keys()
        print(f"\n{'='*78}\n{name}: {len(keys)} Slots\n{'='*78}")

        # --- Crusaders eigene Baeume -------------------------------------
        stichprobe = keys[:: max(1, len(keys) // 40)][:40]
        roh = target.extract_frames(DATA, gruppe, stichprobe, want_mask=False)
        vanilla_dx: list[float] = []
        vanilla_dy: list[float] = []
        for key in stichprobe:
            frame = gruppe.frames[key]
            gemessen = masse(roh[key][0], frame.pivot_x, frame.pivot_y)
            if gemessen is None:
                continue
            vanilla_dx.append(gemessen[0])
            vanilla_dy.append(gemessen[1])
        print("  Crusader, wie das Spiel selbst zeichnet:")
        print("    " + bericht("dx zur Mitte    ", vanilla_dx))
        print("    " + bericht("dy ueber Unterk.", vanilla_dy))

        # --- unsere gelieferten Frames -----------------------------------
        json_pfad = ATLAS / name / "atlas.json"
        if not json_pfad.is_file():
            print("  (kein ausgelieferter Atlas)")
            continue
        nutzlast = json.loads(json_pfad.read_text(encoding="utf-8"))
        atlas = Image.open(ATLAS / name / "atlas.png").convert("RGBA")
        nach_name = {f.name: (k, f) for k, f in gruppe.frames.items()}

        unser_dx: list[float] = []
        unser_dy: list[float] = []
        umgerechnet_dx: list[float] = []
        umgerechnet_dy: list[float] = []
        eigene = 0
        for eintrag in nutzlast["frames"]:
            treffer = nach_name.get(eintrag["name"])
            if treffer is None:
                continue
            _key, ziel = treffer
            r = eintrag["rect"]
            oben = atlas.height - r["y"] - r["h"]
            bild = atlas.crop((r["x"], oben, r["x"] + r["w"], oben + r["h"]))
            if (r["w"], r["h"]) == (round(ziel.width), round(ziel.height)):
                continue  # unveraendert uebernommener Crusader-Frame
            eigene += 1
            gemessen = masse(bild, eintrag["pivot"]["x"], eintrag["pivot"]["y"])
            if gemessen is None:
                continue
            unser_dx.append(gemessen[0])
            unser_dy.append(gemessen[1])
            # Was die Umrechnung auf den Crusader-Anker ergeben haette:
            anker_x = target_anchor(ziel.pivot_x, ziel.width, r["w"])
            anker_y = target_anchor(ziel.pivot_y, ziel.height, r["h"])
            ersatz = masse(bild, anker_x / r["w"], anker_y / r["h"])
            if ersatz is not None:
                umgerechnet_dx.append(ersatz[0])
                umgerechnet_dy.append(ersatz[1])
        print(f"  unsere gelieferten Frames ({eigene} eigene, Rest uebernommen):")
        print("    " + bericht("dx zur Mitte    ", unser_dx))
        print("    " + bericht("dy ueber Unterk.", unser_dy))
        print("  dieselben Frames, haetten wir auf den Crusader-Anker umgerechnet:")
        print("    " + bericht("dx zur Mitte    ", umgerechnet_dx))
        print("    " + bericht("dy ueber Unterk.", umgerechnet_dy))


if __name__ == "__main__":
    main()
