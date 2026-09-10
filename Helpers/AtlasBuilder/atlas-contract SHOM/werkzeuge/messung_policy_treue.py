"""Haelt jede Gruppe die Geometrieregel, die ihr Profil deklariert?

Unsere Kette kennt drei Regeln, und jede Gruppe waehlt eine davon:

``target_canvas``
    Der Frame sitzt auf der Crusader-Leinwand und traegt den Crusader-Pivot.

``native_source_reanchored_v1``
    Der Frame behaelt seine eigene Groesse, der Anker bleibt der des
    Crusader-Slots. Richtig fuer Kacheln und Mauerwerk: die Kachel muss auf
    ihrer Kachel sitzen.

``native_source_authored_pivot_v1``
    Der Frame behaelt seine eigene Groesse UND den Anker der Zeichnung. Richtig
    fuer einen Grafiktausch: eine Birke gehoert an den Anker der Birke.

Unabhaengig davon darf jeder Slot ohne eigene Vorlage als unveraenderte
Crusader-Kopie mitgehen; der traegt dann Groesse und Pivot des Ziels.

Dieses Skript prueft jeden ausgelieferten Frame gegen die Regel seiner Gruppe.
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
BUILD = Path(
    r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
    r"/SE144/dist/europe-production/StrongholdEuropeDE"
)
ATLAS = BUILD / "Override" / "Atlas"
# Frisch gebaute Gruppen ueberschreiben den Stand aus dem alten Gesamtbau.
FRISCH = {
    "anim_castle": Path(
        r"E:/Stronghold Crusader Definitive Edition Baldwin and Bullseye MULTi20"
        r"/SE144/dist/anim-castle-v3-canary/StrongholdEuropeDE/Override/Atlas/anim_castle"
    ),
}


def atlas_ordner(name: str) -> Path:
    return FRISCH.get(name, ATLAS / name)


def passt(px, py, bezug, breite, hoehe) -> bool:
    try:
        verify_pivot_xy(
            px, py, bezug.pivot_x, bezug.pivot_y, bezug.width, bezug.height, breite, hoehe
        )
        return True
    except AnchorError:
        return False


def politik(md: dict) -> str:
    geo = md.get("geometry_policy")
    if isinstance(geo, dict):
        return str(geo.get("mode", "target_canvas"))
    return str(geo or "target_canvas")


def main() -> None:
    manifest = json.loads((BUILD / "build-manifest.json").read_text(encoding="utf-8"))
    regeln: dict[str, str] = {}
    for eintrag in manifest.get("outputs", []):
        md = eintrag.get("metadata") or {}
        if md.get("gm_file_name"):
            regeln[md["gm_file_name"]] = politik(md)

    namen = sorted(
        p.name
        for p in ATLAS.iterdir()
        if (p / "atlas.json").is_file() and find_group(p.name) is not None
    )
    print(f"{len(namen)} Gruppen, Regelverteilung:", Counter(regeln.get(n, '?') for n in namen))

    print("\nlese Crusader ...", flush=True)
    ziele = target.read_groups(SHC, namen)
    print("lese Stronghold 1 ...", flush=True)
    quellen = target.read_groups(SH1, namen, require_all=False)

    gesamt = Counter()
    verstoesse: dict[str, list[str]] = {}
    for name in namen:
        regel = regeln.get(name, "target_canvas")
        ziel = ziele[name]
        quelle = quellen.get(name)
        nutzlast = json.loads((atlas_ordner(name) / "atlas.json").read_text(encoding="utf-8"))
        nach_name = {f.name: f for f in ziel.frames.values()}
        for eintrag in nutzlast["frames"]:
            z = nach_name.get(eintrag["name"])
            if z is None:
                gesamt["kein Zielslot"] += 1
                verstoesse.setdefault(name, []).append(eintrag["name"])
                continue
            r = eintrag["rect"]
            px, py = eintrag["pivot"]["x"], eintrag["pivot"]["y"]
            ist_kopie = (r["w"], r["h"]) == (round(z.width), round(z.height)) and passt(
                px, py, z, r["w"], r["h"]
            )
            if ist_kopie:
                gesamt["unveraenderte Crusader-Kopie"] += 1
                continue
            if regel == "target_canvas":
                gesamt["Regel verletzt"] += 1
                verstoesse.setdefault(name, []).append(eintrag["name"])
            elif regel == "native_source_reanchored_v1":
                if passt(px, py, z, r["w"], r["h"]):
                    gesamt["Zielanker wie deklariert"] += 1
                else:
                    gesamt["Regel verletzt"] += 1
                    verstoesse.setdefault(name, []).append(eintrag["name"])
            elif regel == "native_source_authored_pivot_v1":
                q = quelle.frames.get(z.key) if quelle is not None else None
                if q is not None and passt(px, py, q, r["w"], r["h"]):
                    gesamt["Quellanker wie deklariert"] += 1
                    continue
                gefunden = False
                for andere in quellen.values():
                    for kandidat in andere.frames.values():
                        if (round(kandidat.width), round(kandidat.height)) == (
                            r["w"],
                            r["h"],
                        ) and passt(px, py, kandidat, r["w"], r["h"]):
                            gefunden = True
                            break
                    if gefunden:
                        break
                if gefunden:
                    gesamt["Quellanker, umgesetzter Index"] += 1
                else:
                    gesamt["Regel verletzt"] += 1
                    verstoesse.setdefault(name, []).append(eintrag["name"])
            else:
                gesamt["unbekannte Regel"] += 1

    print("\nErgebnis ueber alle ausgelieferten Frames:")
    for etikett, anzahl in gesamt.most_common():
        print(f"   {etikett:<32}{anzahl:>8}")
    print(f"   {'SUMME':<32}{sum(gesamt.values()):>8}")
    if verstoesse:
        print("\nGruppen mit Regelverstoessen:")
        for name, frames in sorted(verstoesse.items(), key=lambda e: -len(e[1])):
            print(f"   {name:<26}{len(frames):>6}   z.B. {', '.join(frames[:4])}")
    else:
        print("\nKeine Regelverstoesse.")


if __name__ == "__main__":
    main()
