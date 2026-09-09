from __future__ import annotations

import json
import re
import shutil
from dataclasses import dataclass
from pathlib import Path

import UnityPy
from PIL import Image


WORKSPACE = Path(r"D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods")
SOURCE_ROOT = WORKSPACE / "Baeume_Test" / "Alle frames"
GAME_RESOURCES = Path(
    r"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
    r"\Stronghold Crusader Definitive Edition_Data\resources.assets"
)
STAGING_ROOT = WORKSPACE / ".inspect" / "SH1BaeumeAtlasBuilder" / "output"

GAP = 2
OUTER_MARGIN = 2
MAX_TEXTURE_SIZE = 8192


@dataclass(frozen=True)
class Group:
    name: str
    colour_folder: str
    mask_folder: str | None
    frame_count: int


GROUPS = (
    Group("Tree_Oak", "Oak png", "Oak mask png", 148),
    Group("tree_pine", "Chestnut png", "Chestnut mask png", 148),
    Group("tree_shrub1", "shrub1", "shrub1 mask", 25),
    Group("tree_shrub2", "shrub2", "shrub2 - mask", 25),
    Group("tree_apple", "tree_apple", "tree_apple - mask", 101),
    Group("tree_birch", "birch png", "birch mask png", 73),
    Group("Tree_Chestnut", "Pine normal png", "Pine mask png", 148),
    Group("tree_cactii", "tree cacti", None, 17),
)


def read_sprite_metadata() -> dict[str, tuple[float, float, float]]:
    wanted = {f"{group.name}-{index}" for group in GROUPS for index in range(group.frame_count)}
    metadata: dict[str, tuple[float, float, float]] = {}
    environment = UnityPy.load(str(GAME_RESOURCES))

    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        data = obj.read()
        name = getattr(data, "m_Name", getattr(data, "name", ""))
        if name not in wanted:
            continue
        if name in metadata:
            raise RuntimeError(f"Duplicate vanilla Sprite metadata: {name}")
        pivot = data.m_Pivot
        metadata[name] = (float(pivot.x), float(pivot.y), float(data.m_PixelsToUnits))

    missing = sorted(wanted - metadata.keys())
    if missing:
        raise RuntimeError(f"Missing vanilla Sprite metadata ({len(missing)}): {', '.join(missing[:10])}")
    return metadata


def validate_source(group: Group) -> list[tuple[str, Path, Path | None, int, int]]:
    colour_dir = SOURCE_ROOT / group.colour_folder
    mask_dir = SOURCE_ROOT / group.mask_folder if group.mask_folder else None
    expected_names = {f"{group.name}-{index}" for index in range(group.frame_count)}

    colour_paths: dict[str, Path] = {}
    for path in colour_dir.glob("*.png"):
        if re.fullmatch(re.escape(group.name) + r"-\d+", path.stem):
            colour_paths[path.stem] = path

    if set(colour_paths) != expected_names:
        missing = sorted(expected_names - colour_paths.keys())
        extra = sorted(colour_paths.keys() - expected_names)
        raise RuntimeError(f"{group.name}: invalid colour set; missing={missing}, extra={extra}")

    mask_paths: dict[str, Path] = {}
    if mask_dir:
        for path in mask_dir.glob("*.png"):
            base_name = re.sub(r"_m$", "", path.stem)
            if re.fullmatch(re.escape(group.name) + r"-\d+", base_name):
                mask_paths[base_name] = path
        if set(mask_paths) != expected_names:
            missing = sorted(expected_names - mask_paths.keys())
            extra = sorted(mask_paths.keys() - expected_names)
            raise RuntimeError(f"{group.name}: invalid mask set; missing={missing}, extra={extra}")

    result: list[tuple[str, Path, Path | None, int, int]] = []
    for index in range(group.frame_count):
        name = f"{group.name}-{index}"
        colour_path = colour_paths[name]
        mask_path = mask_paths.get(name)
        with Image.open(colour_path) as colour:
            width, height = colour.size
            colour.verify()
        if mask_path:
            with Image.open(mask_path) as mask:
                if mask.size != (width, height):
                    raise RuntimeError(
                        f"{name}: mask is {mask.width}x{mask.height}, colour is {width}x{height}"
                    )
                mask.verify()
        result.append((name, colour_path, mask_path, width, height))
    return result


def shelf_pack(
    frames: list[tuple[str, Path, Path | None, int, int]], width: int
) -> tuple[dict[str, tuple[int, int, int, int]], int] | None:
    placements: dict[str, tuple[int, int, int, int]] = {}
    x = OUTER_MARGIN
    y = OUTER_MARGIN
    row_height = 0

    for name, _colour, _mask, frame_width, frame_height in frames:
        if frame_width + 2 * OUTER_MARGIN > width:
            return None
        if x != OUTER_MARGIN and x + frame_width + OUTER_MARGIN > width:
            x = OUTER_MARGIN
            y += row_height + GAP
            row_height = 0
        placements[name] = (x, y, frame_width, frame_height)
        x += frame_width + GAP
        row_height = max(row_height, frame_height)

    height = y + row_height + OUTER_MARGIN
    if height > MAX_TEXTURE_SIZE:
        return None
    return placements, height


def choose_layout(
    frames: list[tuple[str, Path, Path | None, int, int]]
) -> tuple[int, int, dict[str, tuple[int, int, int, int]]]:
    candidates: list[tuple[int, int, int, dict[str, tuple[int, int, int, int]]]] = []
    for width in (512, 1024, 2048, 4096, 8192):
        packed = shelf_pack(frames, width)
        if packed is None:
            continue
        placements, height = packed
        candidates.append((width * height, width, height, placements))
    if not candidates:
        raise RuntimeError("Frames do not fit within an 8192x8192 atlas")
    _area, width, height, placements = min(candidates, key=lambda item: (item[0], item[1]))
    return width, height, placements


def build_group(
    group: Group,
    metadata: dict[str, tuple[float, float, float]],
) -> None:
    frames = validate_source(group)
    atlas_width, atlas_height, placements = choose_layout(frames)
    output_dir = STAGING_ROOT / "Override" / "Atlas" / group.name
    output_dir.mkdir(parents=True, exist_ok=True)

    colour_atlas = Image.new("RGBA", (atlas_width, atlas_height), (0, 0, 0, 0))
    mask_atlas = Image.new("RGBA", (atlas_width, atlas_height), (0, 0, 0, 0)) if group.mask_folder else None
    json_frames: list[dict[str, object]] = []

    for name, colour_path, mask_path, width, height in frames:
        x, top_y, _, _ = placements[name]
        with Image.open(colour_path) as source:
            colour_atlas.paste(source.convert("RGBA"), (x, top_y))
        if mask_atlas is not None and mask_path is not None:
            with Image.open(mask_path) as source_mask:
                mask_atlas.paste(source_mask.convert("RGBA"), (x, top_y))

        pivot_x, pivot_y, ppu = metadata[name]
        if ppu != 64.0:
            raise RuntimeError(f"{name}: unexpected vanilla pixels-per-unit value {ppu}")
        unity_y = atlas_height - top_y - height
        json_frames.append(
            {
                "name": name,
                "rect": {"x": x, "y": unity_y, "w": width, "h": height},
                "pivot": {"x": pivot_x, "y": pivot_y},
            }
        )

    colour_atlas.save(output_dir / "atlas.png", format="PNG", optimize=True)
    if mask_atlas is not None:
        mask_atlas.save(output_dir / "atlas_m.png", format="PNG", optimize=True)

    payload = {"pixelsPerUnit": 64, "frames": json_frames}
    json_text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    (output_dir / "atlas.json").write_bytes(json_text.replace("\n", "\r\n").encode("utf-8"))
    print(f"{group.name}: {group.frame_count} frames, {atlas_width}x{atlas_height}, mask={mask_atlas is not None}")


def main() -> None:
    if not SOURCE_ROOT.is_dir():
        raise RuntimeError(f"Missing source folder: {SOURCE_ROOT}")
    if not GAME_RESOURCES.is_file():
        raise RuntimeError(f"Missing game resources: {GAME_RESOURCES}")

    if STAGING_ROOT.exists():
        shutil.rmtree(STAGING_ROOT)
    STAGING_ROOT.mkdir(parents=True)

    metadata = read_sprite_metadata()
    for group in GROUPS:
        build_group(group, metadata)


if __name__ == "__main__":
    main()
