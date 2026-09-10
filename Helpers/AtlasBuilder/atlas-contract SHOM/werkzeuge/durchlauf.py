"""Durchlauf von Anfang bis Ende, mit echten Spieldaten.

Erzeugt zehn absichtlich zu grosse Vorlagen, laesst den Rest auffuellen, baut
den Atlas und prueft ihn. Am Schluss werden Pivots absichtlich verdorben, damit
sichtbar ist, dass die Pruefung sie faengt.

Aufruf::

    python werkzeuge/durchlauf.py "<Pfad>/Stronghold Crusader Definitive Edition_Data"
"""

from __future__ import annotations

import shutil
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from atlas_contract import target  # noqa: E402
from atlas_contract.__main__ import main  # noqa: E402

if len(sys.argv) < 2:
    raise SystemExit("Aufruf: python werkzeuge/durchlauf.py <SHCDE_Data> [Arbeitsordner]")
DATA = sys.argv[1]
ARBEIT = Path(sys.argv[2] if len(sys.argv) > 2 else "durchlauf_arbeit")
GRUPPE = "tree_apple"
EIGENE = [0, 3, 7, 11, 20, 33, 44, 55, 66, 77]
RAND = 20

if ARBEIT.exists():
    shutil.rmtree(ARBEIT)
bilder = ARBEIT / "vorlagen"
bilder.mkdir(parents=True)
ausgabe = ARBEIT / "Override" / "Atlas" / GRUPPE

print("== 1. Zehn eigene Vorlagen erzeugen, absichtlich groesser als der Slot ==")
gruppe = target.read_groups(DATA, [GRUPPE])[GRUPPE]
keys = [target.FrameKey(i) for i in EIGENE]
roh = target.extract_frames(DATA, gruppe, keys, want_mask=True)
for key in keys:
    bild, maske = roh[key]
    for quelle, endung in ((bild, ""), (maske, "_m")):
        gross = Image.new(
            "RGBA", (quelle.width + 2 * RAND, quelle.height + 2 * RAND), (0, 0, 0, 0)
        )
        gross.paste(quelle, (RAND, RAND))
        gross.save(bilder / f"apfel{key.index}{endung}.png")
print(f"   {len(keys)} Vorlagen in {bilder}, je {2*RAND} px groesser als das Ziel")

print("\n== 2. Bauen ohne Auffuellen: muss abbrechen ==")
code = main(
    ["bauen", GRUPPE, "--spiel", DATA, "--bilder", str(bilder), "--ausgabe", str(ausgabe)]
)
print(f"   Rueckgabewert {code} (erwartet 2)")
assert code == 2, "Ein unvollstaendiger Atlas haette nicht gebaut werden duerfen"

print("\n== 3. Fehlende Slots als Crusader-Kopie ergaenzen ==")
code = main(["fuellen", GRUPPE, "--spiel", DATA, "--bilder", str(bilder)])
print(f"   Rueckgabewert {code}")
assert code == 0
anzahl = len(list(bilder.glob("*.png")))
print(f"   {anzahl} Dateien im Vorlagenordner")

print("\n== 4. Bauen ==")
code = main(
    ["bauen", GRUPPE, "--spiel", DATA, "--bilder", str(bilder), "--ausgabe", str(ausgabe)]
)
print(f"   Rueckgabewert {code}")
assert code == 0

print("\n== 5. Unabhaengig nachpruefen ==")
code = main(["pruefen", GRUPPE, "--spiel", DATA, "--atlas", str(ausgabe)])
print(f"   Rueckgabewert {code}")
assert code == 0

print("\n== 6. Gegenprobe: Pivot absichtlich falsch schreiben ==")
import json

pfad = ausgabe / "atlas.json"
nutzlast = json.loads(pfad.read_text(encoding="utf-8"))
kaputt = 0
for eintrag in nutzlast["frames"]:
    ziel = next(f for f in gruppe.frames.values() if f.name == eintrag["name"])
    if eintrag["rect"]["w"] != round(ziel.width):
        eintrag["pivot"] = {"x": ziel.pivot_x, "y": ziel.pivot_y}
        kaputt += 1
pfad.write_text(json.dumps(nutzlast, indent=2), encoding="utf-8")
print(f"   {kaputt} Frames auf den unveraenderten Crusader-Pivot gesetzt")
code = main(["pruefen", GRUPPE, "--spiel", DATA, "--atlas", str(ausgabe)])
print(f"   Rueckgabewert {code} (erwartet 1)")
assert code == 1

print("\nDurchlauf bestanden.")
