from __future__ import annotations

import argparse
import json
import math
import shutil
import sys
from pathlib import Path

from PIL import Image


NORMAL_COUNT = 1088
ALTERNATE_COUNT = 128
SOURCE_ATLAS_SIZE = (8192, 8192)
SOURCE_TEXTURE_PATH_ID = 37


def write_crlf_json(path: Path, payload: object) -> None:
    text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    path.write_bytes(text.replace("\n", "\r\n").encode("utf-8"))


def rounded_pixel(value: float) -> int:
    return int(math.floor(value + 0.5))


def frame_key(name: str) -> tuple[int, bool]:
    prefix = "body_swordsman-"
    if not name.startswith(prefix):
        raise ValueError(f"Unexpected sprite name: {name}")
    suffix = name[len(prefix):]
    alternate = suffix.endswith("x")
    number = suffix[:-1] if alternate else suffix
    return int(number), alternate


def ensure_inside_skin_test(path: Path, skin_test: Path) -> None:
    path.resolve().relative_to(skin_test.resolve())


def reset_directory(path: Path, skin_test: Path) -> None:
    ensure_inside_skin_test(path, skin_test)
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)


def extract_frames(source_root: Path, skin_test: Path) -> None:
    sprites = source_root / "Assets" / "Resources" / "sprites"
    colour_atlas_path = sprites / "body13Sprites.png"
    mask_atlas_path = sprites / "body13Sprites_m.png"
    metadata_paths = sorted(sprites.glob("body_swordsman-*.json"), key=lambda item: frame_key(item.stem))
    if len(metadata_paths) != NORMAL_COUNT + ALTERNATE_COUNT:
        raise RuntimeError(f"Expected 1216 source metadata files, found {len(metadata_paths)}")

    colour_dir = skin_test / "AtlasSource" / "Colour"
    metadata_dir = skin_test / "AtlasSource" / "Metadata"
    reset_directory(colour_dir, skin_test)
    reset_directory(metadata_dir, skin_test)

    seen_normal: set[int] = set()
    seen_alternate: set[int] = set()
    with Image.open(colour_atlas_path) as colour_image, Image.open(mask_atlas_path) as mask_image:
        if colour_image.size != SOURCE_ATLAS_SIZE or mask_image.size != SOURCE_ATLAS_SIZE:
            raise RuntimeError(f"Unexpected source atlas dimensions: colour={colour_image.size}, mask={mask_image.size}")
        colour = colour_image.convert("RGBA")
        mask = mask_image.convert("RGBA")
        atlas_height = colour.height

        for metadata_path in metadata_paths:
            payload = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            name = str(payload["m_Name"])
            if name != metadata_path.stem:
                raise RuntimeError(f"Metadata filename and m_Name differ: {metadata_path.name} / {name}")
            index, alternate = frame_key(name)
            (seen_alternate if alternate else seen_normal).add(index)

            render_data = payload.get("m_RD", {})
            texture = render_data.get("m_Texture", {})
            if int(texture.get("m_PathID", -1)) != SOURCE_TEXTURE_PATH_ID:
                raise RuntimeError(f"{name}: unexpected source texture PathID")
            rect = payload["m_Rect"]
            texture_rect = render_data.get("m_TextureRect", {})
            for rect_key in ("m_X", "m_Y", "m_Width", "m_Height"):
                if abs(float(rect[rect_key]) - float(texture_rect.get(rect_key, math.nan))) > 0.0001:
                    raise RuntimeError(f"{name}: m_Rect and m_RD.m_TextureRect differ")
            if int(render_data.get("m_SettingsRaw", -1)) != 64:
                raise RuntimeError(f"{name}: unexpected SpriteRenderData settings")
            if render_data.get("m_TextureRectOffset") != {"m_X": 0, "m_Y": 0}:
                raise RuntimeError(f"{name}: unexpected texture rectangle offset")
            if render_data.get("m_AtlasRectOffset") != {"m_X": -1, "m_Y": -1}:
                raise RuntimeError(f"{name}: unexpected atlas rectangle offset")
            old_x = float(rect["m_X"])
            old_y = float(rect["m_Y"])
            old_width = float(rect["m_Width"])
            old_height = float(rect["m_Height"])
            left = rounded_pixel(old_x)
            bottom = rounded_pixel(old_y)
            right = rounded_pixel(old_x + old_width)
            top = rounded_pixel(old_y + old_height)
            width = right - left
            height = top - bottom
            if width <= 0 or height <= 0:
                raise RuntimeError(f"{name}: rounded rectangle is empty")

            crop_box = (left, atlas_height - top, right, atlas_height - bottom)
            output_stem = metadata_path.stem
            colour.crop(crop_box).save(colour_dir / f"{output_stem}.png", optimize=True)
            mask.crop(crop_box).save(colour_dir / f"{output_stem}_m.png", optimize=True)

            pivot = payload["m_Pivot"]
            anchor_x = float(pivot["m_X"]) * old_width + old_x - left
            anchor_y = float(pivot["m_Y"]) * old_height + old_y - bottom
            corrected = {
                "m_Name": name,
                "m_Rect": {"m_X": 0, "m_Y": 0, "m_Width": width, "m_Height": height},
                "m_Pivot": {"m_X": anchor_x / width, "m_Y": anchor_y / height},
                "m_PixelsToUnits": float(payload["m_PixelsToUnits"]),
            }
            write_crlf_json(metadata_dir / metadata_path.name, corrected)

    if seen_normal != set(range(NORMAL_COUNT)):
        raise RuntimeError("Normal source indices are not exactly 0..1087")
    if seen_alternate != set(range(ALTERNATE_COUNT)):
        raise RuntimeError("Alternate source indices are not exactly 0x..127x")


def build_private_atlas(workspace: Path, skin_test: Path) -> None:
    atlas_builder = workspace / "AtlasBuilder"
    sys.path.insert(0, str(atlas_builder))
    from atlas_builder.core import build_project
    from atlas_builder.models import ProjectConfig

    project_path = skin_test / "SkinTest.atlas-project.json"
    staging = skin_test / ".atlas-build"
    reset_directory(staging, skin_test)
    project = ProjectConfig.load(project_path)
    project.output_mod_directory = str(staging)
    result = build_project(project, overwrite_existing=True)
    if result.colour_frames != NORMAL_COUNT + ALTERNATE_COUNT or result.mask_frames != NORMAL_COUNT + ALTERNATE_COUNT:
        raise RuntimeError(f"AtlasBuilder returned unexpected counts: {result}")
    gap_warnings = [warning for warning in result.warnings if "32" in warning and "416" in warning and "447" in warning]
    if len(gap_warnings) != 1:
        raise RuntimeError(f"AtlasBuilder did not report the expected SHCDE target gap: {result.warnings}")

    generated = staging / "Override" / "Atlas" / "body_swordsman"
    private_assets = skin_test / "Assets" / "CrusaderSwordsman"
    reset_directory(private_assets, skin_test)
    for name in ("atlas.png", "atlas_m.png", "atlas.json"):
        shutil.copy2(generated / name, private_assets / name)
    shutil.rmtree(staging)


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract SH1DE swordsman frames and build SkinTest's private atlas.")
    parser.add_argument(
        "--source-root",
        type=Path,
        default=Path(r"D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\SH1DE_RippedFiles"),
    )
    args = parser.parse_args()
    skin_test = Path(__file__).resolve().parents[1]
    workspace = skin_test.parent
    extract_frames(args.source_root.resolve(), skin_test)
    build_private_atlas(workspace, skin_test)
    print("Prepared 1088 normal and 128 alternate SH1DE swordsman frames with complete masks.")


if __name__ == "__main__":
    main()
