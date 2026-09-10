from __future__ import annotations

import argparse
import base64
import hashlib
import json
import math
import shutil
import struct
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


NORMAL_COUNT = 1088
ALTERNATE_COUNT = 128
SOURCE_ATLAS_SIZE = (8192, 8192)
SOURCE_TEXTURE_PATH_ID = 37
FLOAT_FORMAT = 0
TRIANGLE_TOPOLOGY = 0
POSITION_CHANNEL = 0
UV_CHANNEL = 4
MESH_EPSILON = 0.001


def write_crlf_json(path: Path, payload: object) -> None:
    text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    path.write_bytes(text.replace("\n", "\r\n").encode("utf-8"))


def rounded_pixel(value: float) -> int:
    return int(math.floor(value + 0.5))


def aligned_16(value: int) -> int:
    return (value + 15) & ~15


def decode_mesh(payload: dict, atlas_width: int, atlas_height: int) -> tuple[list[tuple[float, float]], list[int], tuple[int, int, int, int], tuple[float, float]]:
    name = str(payload["m_Name"])
    render_data = payload["m_RD"]
    vertex_data = render_data["m_VertexData"]
    vertex_count = int(vertex_data["m_VertexCount"])
    if vertex_count < 3:
        raise RuntimeError(f"{name}: tight mesh has fewer than three vertices")

    channels = vertex_data["m_Channels"]
    active_channels = [
        (index, int(channel["m_Stream"]), int(channel["m_Offset"]), int(channel["m_Format"]), int(channel["m_Dimension"]))
        for index, channel in enumerate(channels)
        if int(channel["m_Dimension"]) > 0
    ]
    expected_channels = [
        (POSITION_CHANNEL, 0, 0, FLOAT_FORMAT, 3),
        (UV_CHANNEL, 1, 0, FLOAT_FORMAT, 2),
    ]
    if active_channels != expected_channels:
        raise RuntimeError(f"{name}: unsupported vertex channel layout: {active_channels}")

    try:
        raw_vertices = base64.b64decode(vertex_data["m_Data"], validate=True)
        raw_indices = base64.b64decode(render_data["m_IndexBuffer"], validate=True)
    except (ValueError, TypeError) as exc:
        raise RuntimeError(f"{name}: invalid base64 mesh data") from exc

    uv_offset = aligned_16(vertex_count * 12)
    expected_vertex_bytes = uv_offset + vertex_count * 8
    if len(raw_vertices) != expected_vertex_bytes:
        raise RuntimeError(
            f"{name}: vertex data length is {len(raw_vertices)}, expected {expected_vertex_bytes}"
        )
    if len(raw_indices) == 0 or len(raw_indices) % 6 != 0:
        raise RuntimeError(f"{name}: index buffer is not a non-empty UInt16 triangle list")

    positions = [struct.unpack_from("<3f", raw_vertices, index * 12) for index in range(vertex_count)]
    uvs = [struct.unpack_from("<2f", raw_vertices, uv_offset + index * 8) for index in range(vertex_count)]
    if any(abs(position[2]) > MESH_EPSILON for position in positions):
        raise RuntimeError(f"{name}: sprite mesh contains a non-zero Z position")

    indices = list(struct.unpack(f"<{len(raw_indices) // 2}H", raw_indices))
    if any(index >= vertex_count for index in indices):
        raise RuntimeError(f"{name}: mesh index exceeds vertex count")
    submeshes = render_data["m_SubMeshes"]
    if len(submeshes) != 1:
        raise RuntimeError(f"{name}: expected exactly one sprite submesh")
    submesh = submeshes[0]
    if (
        int(submesh["m_Topology"]) != TRIANGLE_TOPOLOGY
        or int(submesh["m_FirstByte"]) != 0
        or int(submesh["m_BaseVertex"]) != 0
        or int(submesh["m_FirstVertex"]) != 0
        or int(submesh["m_IndexCount"]) != len(indices)
        or int(submesh["m_VertexCount"]) != vertex_count
    ):
        raise RuntimeError(f"{name}: unsupported sprite submesh contract")

    atlas_vertices: list[tuple[float, float]] = []
    for u, v in uvs:
        atlas_x = u * atlas_width
        atlas_y = v * atlas_height
        if abs(atlas_x - rounded_pixel(atlas_x)) > MESH_EPSILON or abs(atlas_y - rounded_pixel(atlas_y)) > MESH_EPSILON:
            raise RuntimeError(f"{name}: UV does not resolve to an integral atlas coordinate")
        atlas_vertices.append((atlas_x, atlas_y))

    left = rounded_pixel(min(point[0] for point in atlas_vertices))
    bottom = rounded_pixel(min(point[1] for point in atlas_vertices))
    right = rounded_pixel(max(point[0] for point in atlas_vertices))
    top = rounded_pixel(max(point[1] for point in atlas_vertices))
    if left < 0 or bottom < 0 or right > atlas_width or top > atlas_height or right <= left or top <= bottom:
        raise RuntimeError(f"{name}: tight mesh bounds are outside the source atlas")

    pixels_per_unit = float(payload["m_PixelsToUnits"])
    if not math.isfinite(pixels_per_unit) or pixels_per_unit <= 0:
        raise RuntimeError(f"{name}: invalid pixels-per-unit value")
    anchor_x_values = [uv[0] - position[0] * pixels_per_unit for uv, position in zip(atlas_vertices, positions)]
    anchor_y_values = [uv[1] - position[1] * pixels_per_unit for uv, position in zip(atlas_vertices, positions)]
    if max(anchor_x_values) - min(anchor_x_values) > MESH_EPSILON or max(anchor_y_values) - min(anchor_y_values) > MESH_EPSILON:
        raise RuntimeError(f"{name}: vertex positions and UVs do not share a stable pivot anchor")
    anchor_x = sum(anchor_x_values) / vertex_count
    anchor_y = sum(anchor_y_values) / vertex_count

    transform = render_data["m_UvTransform"]
    if (
        abs(float(transform["m_X"]) - pixels_per_unit) > MESH_EPSILON
        or abs(float(transform["m_Z"]) - pixels_per_unit) > MESH_EPSILON
        or abs(float(transform["m_Y"]) - anchor_x) > MESH_EPSILON
        or abs(float(transform["m_W"]) - anchor_y) > MESH_EPSILON
    ):
        raise RuntimeError(f"{name}: UV transform differs from the reconstructed pivot contract")

    return atlas_vertices, indices, (left, bottom, right, top), (anchor_x, anchor_y)


def apply_tight_mesh(image: Image.Image, atlas_vertices: list[tuple[float, float]], indices: list[int], bounds: tuple[int, int, int, int]) -> tuple[Image.Image, list[tuple[float, float]], int]:
    left, bottom, right, top = bounds
    top_origin_vertices = [(x - left, top - y) for x, y in atlas_vertices]
    geometry_mask = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(geometry_mask)
    for offset in range(0, len(indices), 3):
        draw.polygon(
            [top_origin_vertices[indices[offset]], top_origin_vertices[indices[offset + 1]], top_origin_vertices[indices[offset + 2]]],
            fill=255,
        )
    outside_before = ImageChops.multiply(image.getchannel("A"), ImageChops.invert(geometry_mask))
    removed_alpha_pixels = sum(outside_before.histogram()[1:])
    cleaned = Image.composite(image, Image.new("RGBA", image.size, (0, 0, 0, 0)), geometry_mask)
    outside_alpha = ImageChops.multiply(cleaned.getchannel("A"), ImageChops.invert(geometry_mask))
    if outside_alpha.getbbox() is not None or cleaned.getchannel("A").getbbox() is None:
        raise RuntimeError("Tight-mesh cleanup produced invalid alpha coverage")
    bottom_origin_vertices = [(x - left, y - bottom) for x, y in atlas_vertices]
    return cleaned, bottom_origin_vertices, removed_alpha_pixels


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


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
            if int(render_data.get("m_SettingsRaw", -1)) != 64:
                raise RuntimeError(f"{name}: unexpected SpriteRenderData settings")
            if render_data.get("m_TextureRectOffset") != {"m_X": 0, "m_Y": 0}:
                raise RuntimeError(f"{name}: unexpected texture rectangle offset")
            if render_data.get("m_AtlasRectOffset") != {"m_X": -1, "m_Y": -1}:
                raise RuntimeError(f"{name}: unexpected atlas rectangle offset")
            atlas_vertices, indices, bounds, anchor = decode_mesh(payload, colour.width, colour.height)
            left, bottom, right, top = bounds
            width = right - left
            height = top - bottom
            crop_box = (left, atlas_height - top, right, atlas_height - bottom)
            output_stem = metadata_path.stem
            cleaned_colour, local_vertices, removed_colour_pixels = apply_tight_mesh(
                colour.crop(crop_box), atlas_vertices, indices, bounds
            )
            cleaned_mask, mask_local_vertices, removed_mask_pixels = apply_tight_mesh(
                mask.crop(crop_box), atlas_vertices, indices, bounds
            )
            if local_vertices != mask_local_vertices:
                raise RuntimeError(f"{name}: colour and mask geometry differ")
            colour_output = colour_dir / f"{output_stem}.png"
            mask_output = colour_dir / f"{output_stem}_m.png"
            cleaned_colour.save(colour_output, optimize=True)
            cleaned_mask.save(mask_output, optimize=True)

            anchor_x, anchor_y = anchor
            corrected = {
                "m_Name": name,
                "m_Rect": {"m_X": 0, "m_Y": 0, "m_Width": width, "m_Height": height},
                "m_Pivot": {"m_X": (anchor_x - left) / width, "m_Y": (anchor_y - bottom) / height},
                "m_PixelsToUnits": float(payload["m_PixelsToUnits"]),
                "_SkinTestMesh": {
                    "sourceBounds": {"left": left, "bottom": bottom, "right": right, "top": top},
                    "vertices": [{"x": x, "y": y} for x, y in local_vertices],
                    "triangles": indices,
                    "removedColourAlphaPixels": removed_colour_pixels,
                    "removedMaskAlphaPixels": removed_mask_pixels,
                    "colourSha256": sha256_file(colour_output),
                    "maskSha256": sha256_file(mask_output),
                },
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
