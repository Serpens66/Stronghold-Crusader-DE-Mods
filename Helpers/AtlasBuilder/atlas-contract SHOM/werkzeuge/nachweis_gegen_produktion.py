"""Nachweis: liefert dieses Paket dieselben Pixel wie unsere Produktionskette?

Unsere eigene Kette schreibt fuer jeden unveraendert uebernommenen
Crusader-Frame einen Beweis ins Baumanifest, darunter den SHA-256 ueber die
RGBA-Bytes des Ziel-Sprites. Dieses Skript extrahiert dieselben Slots mit
:mod:`atlas_contract.target` und vergleicht die Hashes.

Zusaetzlich wird die Maske geprueft: der ausgelieferte ``atlas_m.png`` enthaelt
an der im ``atlas.json`` genannten Stelle genau den projizierten
Vanilla-Maskenausschnitt. Der wird herausgeschnitten und bytegleich verglichen.

Aufruf::

    python werkzeuge/nachweis_gegen_produktion.py <SHCDE_Data> [gruppe ...]
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

from PIL import Image

PAKET = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PAKET))

from atlas_contract import target  # noqa: E402

PRODUKTION = (
    PAKET.parents[1]
    / "SE144"
    / "dist"
    / "europe-production"
    / "StrongholdEuropeDE"
)
MANIFEST = PRODUKTION / "build-manifest.json"
STANDARD_GRUPPEN = ("Tree_Oak", "Tree_Chestnut", "anim_flags")


def keep_zeilen(gruppe: str) -> list[dict]:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    for eintrag in manifest.get("outputs", []):
        metadaten = eintrag.get("metadata") or {}
        if metadaten.get("gm_file_name") != gruppe:
            continue
        politik = metadaten.get("sparse_keep_policy")
        if politik:
            return list(politik["frame_proofs"])
    raise SystemExit(f"Keine sparse-keep-Beweise fuer {gruppe} im Manifest")


def atlas_frames(gruppe: str) -> dict[str, dict]:
    pfad = PRODUKTION / "Override" / "Atlas" / gruppe / "atlas.json"
    nutzlast = json.loads(pfad.read_text(encoding="utf-8"))
    return {frame["name"]: frame for frame in nutzlast["frames"]}


def main() -> int:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    data_dir = Path(sys.argv[1])
    gruppen = tuple(sys.argv[2:]) or STANDARD_GRUPPEN

    metadaten = target.read_groups(data_dir, gruppen, progress=lambda s: print(" ", s))

    gesamt = 0
    fehler = 0
    for name in gruppen:
        gruppe = metadaten[name]
        zeilen = keep_zeilen(name)
        json_frames = atlas_frames(name)
        maskenatlas_pfad = PRODUKTION / "Override" / "Atlas" / name / "atlas_m.png"
        maskenatlas = (
            Image.open(maskenatlas_pfad).convert("RGBA")
            if maskenatlas_pfad.is_file()
            else None
        )

        schluessel = []
        nach_name = {}
        for zeile in zeilen:
            passend = [
                frame for frame in gruppe.frames.values() if frame.name == zeile["name"]
            ]
            if len(passend) != 1:
                print(f"  FEHLER {zeile['name']}: kein eindeutiger Zielslot")
                fehler += 1
                continue
            schluessel.append(passend[0].key)
            nach_name[passend[0].key] = zeile

        bilder = target.extract_frames(
            data_dir, gruppe, schluessel, want_mask=maskenatlas is not None
        )

        print(f"\n=== {name}: {len(schluessel)} unveraendert uebernommene Slots ===")
        for key in sorted(bilder):
            zeile = nach_name[key]
            bild, maske = bilder[key]
            gesamt += 1
            eigen = hashlib.sha256(bild.tobytes()).hexdigest()
            gleich_farbe = eigen == zeile["target_rgba_sha256"]

            gleich_maske = None
            if maske is not None and maskenatlas is not None:
                frame = json_frames.get(zeile["name"])
                if frame is None:
                    gleich_maske = False
                else:
                    r = frame["rect"]
                    oben = maskenatlas.height - r["y"] - r["h"]
                    ausschnitt = maskenatlas.crop(
                        (r["x"], oben, r["x"] + r["w"], oben + r["h"])
                    )
                    gleich_maske = ausschnitt.tobytes() == maske.tobytes()

            zustand = "OK  " if gleich_farbe and gleich_maske is not False else "FEHL"
            if zustand == "FEHL":
                fehler += 1
            print(
                f"  {zustand} {zeile['name']:<22} {bild.size[0]:>4}x{bild.size[1]:<4} "
                f"Farbe={'gleich' if gleich_farbe else 'ABWEICHUNG'} "
                f"Maske={'gleich' if gleich_maske else ('ABWEICHUNG' if gleich_maske is False else 'n/a')}"
            )

    print(f"\n{gesamt - fehler} von {gesamt} Slots bytegleich mit der Produktion")
    return 1 if fehler else 0


if __name__ == "__main__":
    raise SystemExit(main())
