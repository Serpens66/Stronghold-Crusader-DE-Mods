from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from PIL import Image, ImageChops

from build_atlases import GAP, GROUPS, SOURCE_ROOT, STAGING_ROOT


def assert_crlf(path: Path) -> None:
    data = path.read_bytes()
    if b"\r\n" not in data or b"\n" in data.replace(b"\r\n", b""):
        raise RuntimeError(f"{path}: text file is not consistently CRLF")
    if b"\\r\\n" in data:
        raise RuntimeError(f"{path}: contains a literal \\r\\n sequence")


def validate_group(group, output_root: Path) -> None:
    output_dir = output_root / "Override" / "Atlas" / group.name
    expected_files = {"atlas.png", "atlas.json"}
    if group.mask_folder:
        expected_files.add("atlas_m.png")
    actual_files = {path.name for path in output_dir.iterdir() if path.is_file()}
    if actual_files != expected_files:
        raise RuntimeError(f"{group.name}: files={sorted(actual_files)}, expected={sorted(expected_files)}")

    json_path = output_dir / "atlas.json"
    assert_crlf(json_path)
    payload = json.loads(json_path.read_text(encoding="utf-8"))
    if set(payload) != {"pixelsPerUnit", "frames"} or payload["pixelsPerUnit"] != 64:
        raise RuntimeError(f"{group.name}: invalid atlas JSON root")
    frames = payload["frames"]
    expected_names = [f"{group.name}-{index}" for index in range(group.frame_count)]
    if [frame.get("name") for frame in frames] != expected_names:
        raise RuntimeError(f"{group.name}: frame names or ordering are invalid")

    with Image.open(output_dir / "atlas.png") as atlas_file:
        atlas = atlas_file.convert("RGBA")
    mask_atlas = None
    if group.mask_folder:
        with Image.open(output_dir / "atlas_m.png") as mask_file:
            mask_atlas = mask_file.convert("RGBA")
        if mask_atlas.size != atlas.size:
            raise RuntimeError(f"{group.name}: mask atlas dimensions differ")

    rectangles: list[tuple[int, int, int, int]] = []
    for index, frame in enumerate(frames):
        if set(frame) != {"name", "rect", "pivot"}:
            raise RuntimeError(f"{frame.get('name')}: invalid frame fields")
        rect = frame["rect"]
        pivot = frame["pivot"]
        if set(rect) != {"x", "y", "w", "h"} or set(pivot) != {"x", "y"}:
            raise RuntimeError(f"{frame['name']}: invalid rect or pivot fields")
        x, unity_y, width, height = (rect[key] for key in ("x", "y", "w", "h"))
        if not all(isinstance(value, int) for value in (x, unity_y, width, height)):
            raise RuntimeError(f"{frame['name']}: non-integral rectangle")
        if x < 0 or unity_y < 0 or width <= 0 or height <= 0:
            raise RuntimeError(f"{frame['name']}: invalid rectangle")
        if x + width > atlas.width or unity_y + height > atlas.height:
            raise RuntimeError(f"{frame['name']}: rectangle exceeds atlas")
        if not (0.0 <= pivot["x"] <= 1.0 and 0.0 <= pivot["y"] <= 1.0):
            raise RuntimeError(f"{frame['name']}: pivot outside 0..1")

        top_y = atlas.height - unity_y - height
        source_path = SOURCE_ROOT / group.colour_folder / f"{frame['name']}.png"
        with Image.open(source_path) as source_file:
            source = source_file.convert("RGBA")
        atlas_crop = atlas.crop((x, top_y, x + width, top_y + height))
        if ImageChops.difference(source, atlas_crop).getbbox() is not None:
            raise RuntimeError(f"{frame['name']}: atlas pixels differ from source")

        if mask_atlas is not None:
            mask_path = SOURCE_ROOT / group.mask_folder / f"{frame['name']}_m.png"
            with Image.open(mask_path) as source_mask_file:
                source_mask = source_mask_file.convert("RGBA")
            mask_crop = mask_atlas.crop((x, top_y, x + width, top_y + height))
            if ImageChops.difference(source_mask, mask_crop).getbbox() is not None:
                raise RuntimeError(f"{frame['name']}: mask atlas pixels differ from source")
        rectangles.append((x, unity_y, width, height))

    for left_index, left in enumerate(rectangles):
        lx, ly, lw, lh = left
        for right in rectangles[left_index + 1 :]:
            rx, ry, rw, rh = right
            horizontal_gap = max(rx - (lx + lw), lx - (rx + rw))
            vertical_gap = max(ry - (ly + lh), ly - (ry + rh))
            if horizontal_gap < GAP and vertical_gap < GAP:
                raise RuntimeError(f"{group.name}: frames overlap or have less than {GAP}px separation")

    print(f"{group.name}: validated {len(frames)} frames at {atlas.width}x{atlas.height}")


def main() -> None:
    output_root = Path(sys.argv[1]).resolve() if len(sys.argv) == 2 else STAGING_ROOT
    if len(sys.argv) > 2:
        raise RuntimeError("usage: validate_atlases.py [mod-root]")
    atlas_root = output_root / "Override" / "Atlas"
    actual_groups = {path.name for path in atlas_root.iterdir() if path.is_dir()}
    expected_groups = {group.name for group in GROUPS}
    if actual_groups != expected_groups:
        raise RuntimeError(f"Atlas groups={sorted(actual_groups)}, expected={sorted(expected_groups)}")
    for group in GROUPS:
        validate_group(group, output_root)
    print("All 685 colour frames and 668 mask frames validated successfully.")


if __name__ == "__main__":
    main()
