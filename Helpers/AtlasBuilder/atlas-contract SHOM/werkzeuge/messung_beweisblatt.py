"""Beweisblatt: wo sitzt der Ankerpunkt auf dem Baum?

Je Gruppe drei Spalten:

1. Crusaders eigener Frame mit seinem eigenen Anker.
2. Unser ausgelieferter Frame mit dem Anker, den wir schreiben.
3. Derselbe Frame mit dem Anker, den die Umrechnung auf den Crusader-Slot
   ergeben haette.

Der Anker ist der Punkt, der auf dem Kachelpunkt landet. Er gehoert an den
Stammfuss. Das Fadenkreuz zeigt, wo er liegt.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

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
AUSWAHL = [("tree_birch", 20), ("Tree_Oak", 20), ("tree_pine", 20), ("Tree_Chestnut", 20)]
ZELLE = (300, 300)
RAND = 34
GRUND = (26, 28, 30, 255)
HELL = (232, 232, 232, 255)
GRUEN = (120, 220, 140, 255)
ROT = (240, 110, 110, 255)
GRAU = (150, 150, 150, 255)


def zelle(bild: Image.Image, anker_x: float, anker_y: float, farbe, titel: str, unten: str):
    """Ein Frame auf dunklem Grund, mit Fadenkreuz auf dem Anker."""

    flaeche = Image.new("RGBA", ZELLE, GRUND)
    zeichner = ImageDraw.Draw(flaeche)
    skala = min(1.0, (ZELLE[0] - 40) / bild.width, (ZELLE[1] - 70) / bild.height)
    breite = max(1, int(bild.width * skala))
    hoehe = max(1, int(bild.height * skala))
    klein = bild.resize((breite, hoehe), Image.Resampling.NEAREST)
    x0 = (ZELLE[0] - breite) // 2
    y0 = ZELLE[1] - 26 - hoehe
    flaeche.alpha_composite(klein, (x0, y0))
    zeichner.rectangle([x0, y0, x0 + breite - 1, y0 + hoehe - 1], outline=GRAU)

    # Anker: x von links, y von unten
    ax = x0 + anker_x * skala
    ay = y0 + hoehe - anker_y * skala
    zeichner.line([(ax, y0 - 6), (ax, y0 + hoehe + 6)], fill=farbe, width=1)
    zeichner.line([(x0 - 6, ay), (x0 + breite + 6, ay)], fill=farbe, width=1)
    zeichner.ellipse([ax - 4, ay - 4, ax + 4, ay + 4], outline=farbe, width=2)

    zeichner.text((8, 6), titel, fill=HELL)
    zeichner.text((8, ZELLE[1] - 18), unten, fill=farbe)
    return flaeche


def main() -> None:
    ziele = target.read_groups(DATA, [name for name, _ in AUSWAHL])
    zeilen = []
    for name, index in AUSWAHL:
        gruppe = ziele[name]
        key = target.FrameKey(index)
        vanilla = target.extract_frames(DATA, gruppe, [key], want_mask=False)[key][0]
        ziel = gruppe.frames[key]

        nutzlast = json.loads((ATLAS / name / "atlas.json").read_text(encoding="utf-8"))
        atlas = Image.open(ATLAS / name / "atlas.png").convert("RGBA")
        eintrag = next(f for f in nutzlast["frames"] if f["name"] == ziel.name)
        r = eintrag["rect"]
        oben = atlas.height - r["y"] - r["h"]
        unser = atlas.crop((r["x"], oben, r["x"] + r["w"], oben + r["h"]))

        a_ist = (eintrag["pivot"]["x"] * r["w"], eintrag["pivot"]["y"] * r["h"])
        a_um = (
            target_anchor(ziel.pivot_x, ziel.width, r["w"]),
            target_anchor(ziel.pivot_y, ziel.height, r["h"]),
        )
        zeilen.append(
            [
                zelle(
                    vanilla,
                    ziel.pivot_x * ziel.width,
                    ziel.pivot_y * ziel.height,
                    GRAU,
                    f"{ziel.name}  Crusader",
                    f"Anker {ziel.pivot_x*ziel.width:.0f} / {ziel.pivot_y*ziel.height:.0f}",
                ),
                zelle(
                    unser,
                    a_ist[0],
                    a_ist[1],
                    GRUEN,
                    f"{ziel.name}  unsere Lieferung",
                    f"Anker {a_ist[0]:.0f} / {a_ist[1]:.0f}",
                ),
                zelle(
                    unser,
                    a_um[0],
                    a_um[1],
                    ROT,
                    f"{ziel.name}  waere umgerechnet",
                    f"Anker {a_um[0]:.0f} / {a_um[1]:.0f}",
                ),
            ]
        )

    breite = ZELLE[0] * 3 + RAND * 4
    hoehe = ZELLE[1] * len(zeilen) + RAND * (len(zeilen) + 1) + 30
    blatt = Image.new("RGBA", (breite, hoehe), (16, 17, 19, 255))
    zeichner = ImageDraw.Draw(blatt)
    zeichner.text(
        (RAND, 12),
        "Ankerpunkt unserer Baumatlanten. Grau: Crusaders eigener Frame. "
        "Gruen: was wir ausliefern. Rot: was die Umrechnung ergaebe.",
        fill=HELL,
    )
    for zeile, spalten in enumerate(zeilen):
        for spalte, flaeche in enumerate(spalten):
            blatt.alpha_composite(
                flaeche,
                (RAND + spalte * (ZELLE[0] + RAND), 30 + RAND + zeile * (ZELLE[1] + RAND)),
            )
    ziel_pfad = Path(sys.argv[1] if len(sys.argv) > 1 else "baum_anker.png")
    blatt.convert("RGB").save(ziel_pfad, format="PNG", optimize=True)
    print("geschrieben:", ziel_pfad, blatt.size)


if __name__ == "__main__":
    main()
