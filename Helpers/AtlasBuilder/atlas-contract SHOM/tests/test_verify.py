"""Tests der Atlaspruefung an einem erfundenen Miniaturspiel.

Kein Spiel, keine Assets: die Zielmetadaten werden von Hand gebaut, damit die
Pruefungen ohne Installation laufen.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from atlas_contract.anchor import reanchored_pivot  # noqa: E402
from atlas_contract.completeness import plan  # noqa: E402
from atlas_contract.groups import require_group  # noqa: E402
from atlas_contract.target import FrameKey, TargetFrame, TargetGroup  # noqa: E402
from atlas_contract.verify import verify_atlas  # noqa: E402

GRUPPE = "tree_apple"  # Bindestrichformat, Maske noetig, als Atlas erlaubt


def ziel_gruppe(anzahl: int = 4) -> TargetGroup:
    gruppe = TargetGroup(group=require_group(GRUPPE))
    for index in range(anzahl):
        key = FrameKey(index)
        gruppe.frames[key] = TargetFrame(
            name=f"{GRUPPE}-{index}",
            key=key,
            width=100.0 + index,
            height=80.0 + index,
            pivot_x=0.5,
            pivot_y=0.25,
            pixels_per_unit=64.0,
            rect_x=0.0,
            rect_y=0.0,
            path_id=1000 + index,
            texture_path_id=20,
            asset_file="resources.assets",
        )
    return gruppe


def schreibe_atlas(
    ordner: Path,
    gruppe: TargetGroup,
    *,
    keys=None,
    quell_groesse=(140, 120),
    pivot_umrechnen: bool = True,
    mit_maske: bool = True,
) -> None:
    ordner.mkdir(parents=True, exist_ok=True)
    frames = []
    x = 0
    breite, hoehe = quell_groesse
    ausgewaehlt = list(keys if keys is not None else gruppe.keys())
    atlas_breite = max(1, len(ausgewaehlt)) * breite
    for key in ausgewaehlt:
        ziel = gruppe.frames[key]
        if pivot_umrechnen:
            pivot_x = reanchored_pivot(ziel.pivot_x, ziel.width, breite)
            pivot_y = reanchored_pivot(ziel.pivot_y, ziel.height, hoehe)
        else:
            pivot_x, pivot_y = ziel.pivot_x, ziel.pivot_y
        frames.append(
            {
                "name": ziel.name,
                "rect": {"x": x, "y": 0, "w": breite, "h": hoehe},
                "pivot": {"x": pivot_x, "y": pivot_y},
            }
        )
        x += breite
    (ordner / "atlas.json").write_text(
        json.dumps({"pixelsPerUnit": 64, "frames": frames}, indent=2),
        encoding="utf-8",
    )
    Image.new("RGBA", (atlas_breite, hoehe), (0, 0, 0, 0)).save(ordner / "atlas.png")
    if mit_maske:
        Image.new("RGBA", (atlas_breite, hoehe), (0, 0, 0, 0)).save(
            ordner / "atlas_m.png"
        )


def test_umgerechneter_pivot_besteht(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe)
    bericht = verify_atlas(gruppe, tmp_path)
    assert bericht.ok, bericht.problems
    assert bericht.checked == 4


def test_unveraenderter_pivot_faellt_durch(tmp_path):
    """Der Fehler aus Smokelots Mod: fremder Pivot auf eigener Leinwand."""

    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe, pivot_umrechnen=False)
    bericht = verify_atlas(gruppe, tmp_path)
    assert not bericht.ok
    assert len(bericht.problems) == 4
    assert all("verschoben" in problem for problem in bericht.problems)


def test_freie_ankerregel_meldet_statt_zu_beanstanden(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe, pivot_umrechnen=False)
    bericht = verify_atlas(gruppe, tmp_path, anchor_rule="frei")
    assert bericht.ok
    assert any("halten nicht den Anker" in hinweis for hinweis in bericht.notes)


def test_gleiche_leinwand_braucht_keine_umrechnung(tmp_path):
    """Bei passender Groesse ist der unveraenderte Pivot korrekt."""

    gruppe = ziel_gruppe(anzahl=1)
    ziel = gruppe.frames[FrameKey(0)]
    schreibe_atlas(
        tmp_path,
        gruppe,
        quell_groesse=(int(ziel.width), int(ziel.height)),
        pivot_umrechnen=False,
    )
    assert verify_atlas(gruppe, tmp_path).ok


def test_fehlende_slots_werden_beanstandet(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe, keys=[FrameKey(0), FrameKey(1)])
    bericht = verify_atlas(gruppe, tmp_path)
    assert not bericht.ok
    assert any("Slots fehlen" in problem for problem in bericht.problems)
    assert any("geloescht" in problem for problem in bericht.problems)


def test_fehlende_maske_wird_beanstandet(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe, mit_maske=False)
    bericht = verify_atlas(gruppe, tmp_path)
    assert any("atlas_m.png" in problem for problem in bericht.problems)


def test_foliage_hinweis_erscheint(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe)
    bericht = verify_atlas(gruppe, tmp_path)
    assert any("Unlit/Foliage" in hinweis for hinweis in bericht.notes)


def test_unbekannter_name_wird_beanstandet(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe)
    pfad = tmp_path / "atlas.json"
    nutzlast = json.loads(pfad.read_text(encoding="utf-8"))
    nutzlast["frames"][0]["name"] = "tree_apple-999"
    pfad.write_text(json.dumps(nutzlast), encoding="utf-8")
    bericht = verify_atlas(gruppe, tmp_path)
    assert any("kein solcher Slot" in problem for problem in bericht.problems)


def test_falsche_aufloesung_wird_beanstandet(tmp_path):
    gruppe = ziel_gruppe()
    schreibe_atlas(tmp_path, gruppe)
    pfad = tmp_path / "atlas.json"
    nutzlast = json.loads(pfad.read_text(encoding="utf-8"))
    nutzlast["frames"][0]["pixelsPerUnit"] = 32
    pfad.write_text(json.dumps(nutzlast), encoding="utf-8")
    bericht = verify_atlas(gruppe, tmp_path)
    assert any("pixelsPerUnit" in problem for problem in bericht.problems)


def test_plan_zaehlt_die_verlorenen_slots():
    gruppe = ziel_gruppe(anzahl=148)
    bericht = plan(gruppe, [FrameKey(i) for i in range(73)])
    assert bericht.target_count == 148
    assert len(bericht.missing) == 75
    assert bericht.truncates == 75
    assert not bericht.complete


def test_plan_meldet_unbekannte_indizes():
    gruppe = ziel_gruppe(anzahl=4)
    bericht = plan(gruppe, [FrameKey(0), FrameKey(99)])
    assert bericht.unknown == [FrameKey(99)]


def test_ungueltige_ankerregel():
    gruppe = ziel_gruppe()
    with pytest.raises(ValueError):
        verify_atlas(gruppe, Path("."), anchor_rule="quatsch")


def quell_gruppe(anzahl: int = 4, faktor: float = 1.4) -> TargetGroup:
    """Eine Quellgruppe mit anderer Geometrie als das Ziel."""

    gruppe = TargetGroup(group=require_group(GRUPPE))
    for index in range(anzahl):
        key = FrameKey(index)
        gruppe.frames[key] = TargetFrame(
            name=f"{GRUPPE}-{index}",
            key=key,
            width=(100.0 + index) * faktor,
            height=(80.0 + index) * faktor,
            pivot_x=0.32,
            pivot_y=0.11,
            pixels_per_unit=64.0,
            rect_x=0.0,
            rect_y=0.0,
            path_id=2000 + index,
            texture_path_id=21,
            asset_file="resources.assets",
        )
    return gruppe


def schreibe_atlas_mit_bezug(ordner, ziel, quelle, quell_groesse=(140, 120)):
    """Schreibt einen Atlas, dessen Anker aus der Quelle stammt."""

    import json as _json

    from atlas_contract.anchor import reanchored_pivot as _rp

    ordner.mkdir(parents=True, exist_ok=True)
    breite, hoehe = quell_groesse
    frames = []
    x = 0
    for key in ziel.keys():
        bezug = quelle.frames[key]
        frames.append(
            {
                "name": ziel.frames[key].name,
                "rect": {"x": x, "y": 0, "w": breite, "h": hoehe},
                "pivot": {
                    "x": _rp(bezug.pivot_x, bezug.width, breite),
                    "y": _rp(bezug.pivot_y, bezug.height, hoehe),
                },
            }
        )
        x += breite
    (ordner / "atlas.json").write_text(_json.dumps({"pixelsPerUnit": 64, "frames": frames}))
    gesamt = max(1, len(frames)) * breite
    Image.new("RGBA", (gesamt, hoehe)).save(ordner / "atlas.png")
    Image.new("RGBA", (gesamt, hoehe)).save(ordner / "atlas_m.png")


def test_quellanker_besteht(tmp_path):
    """Ein echter Grafiktausch haelt den Anker der Quelle, nicht des Slots."""

    ziel, quelle = ziel_gruppe(), quell_gruppe()
    schreibe_atlas_mit_bezug(tmp_path, ziel, quelle)
    bericht = verify_atlas(ziel, tmp_path, anchor_rule="quelle", reference=quelle)
    assert bericht.ok, bericht.problems


def test_quellanker_faellt_unter_zielregel_durch(tmp_path):
    """Dieselben Dateien, andere Regel: die Regeln sind wirklich verschieden."""

    ziel, quelle = ziel_gruppe(), quell_gruppe()
    schreibe_atlas_mit_bezug(tmp_path, ziel, quelle)
    assert not verify_atlas(ziel, tmp_path).ok


def test_quellregel_braucht_referenz(tmp_path):
    with pytest.raises(ValueError):
        verify_atlas(ziel_gruppe(), tmp_path, anchor_rule="quelle")


def test_mischfall_wird_erkannt(tmp_path):
    """Uebernommene Crusader-Kopien tragen den Zielanker und sind kein Fehler."""

    import json as _json

    ziel, quelle = ziel_gruppe(), quell_gruppe()
    schreibe_atlas_mit_bezug(tmp_path, ziel, quelle)
    pfad = tmp_path / "atlas.json"
    nutzlast = _json.loads(pfad.read_text(encoding="utf-8"))
    kopie = ziel.frames[FrameKey(0)]
    nutzlast["frames"][0]["rect"] = {
        "x": 0, "y": 0, "w": round(kopie.width), "h": round(kopie.height),
    }
    nutzlast["frames"][0]["pivot"] = {"x": kopie.pivot_x, "y": kopie.pivot_y}
    pfad.write_text(_json.dumps(nutzlast), encoding="utf-8")
    Image.new("RGBA", (4000, 400)).save(tmp_path / "atlas.png")
    Image.new("RGBA", (4000, 400)).save(tmp_path / "atlas_m.png")
    bericht = verify_atlas(ziel, tmp_path, anchor_rule="quelle", reference=quelle)
    assert bericht.ok, bericht.problems
    assert any("Mischfall" in hinweis for hinweis in bericht.notes)
