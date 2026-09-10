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
CASTLE_FRAME_PREFIX = "tile_castle "
CASTLE_FRAME_COUNT = 1467
CASTLE_MAX_INDEX = 1569
CASTLE_ANIM_FRAME_PREFIX = "anim_castle "
CASTLE_ANIM_FRAME_COUNT = 122
CASTLE_ANIM_MAX_INDEX = 138
CASTLE_ANIM_SOURCE_ATLAS_SIZE = (4096, 8192)
CASTLE_ANIM_SOURCE_TEXTURE_PATH_ID = 26
CASTLE_ANIM_TRANSPARENT_INDICES = {55, 56, 57, 61}
PRIVATE_CASTLE_ATLAS_WIDTH = 8192
PACKING_PADDING = 2
UI_SOURCE_RECTS = {
    "UIButtonsK007": (3340, 2050, 100, 190),
    "UIButtonsK008": (3376, 2251, 100, 162),
    "UIBuildingsO011": (5828, 2012, 116, 217),
    "UIBuildingsO012": (5956, 2012, 116, 217),
    "UIBuildingsK009": (3490, 2594, 115, 156),
    "UIBuildingsK010": (3605, 2594, 115, 156),
}
PLAYER_COLOURS = {
    1: (184, 26, 32),
    2: (224, 112, 24),
    3: (226, 190, 20),
    4: (24, 105, 190),
    5: (42, 42, 46),
    6: (116, 55, 157),
    7: (42, 178, 210),
    8: (48, 145, 58),
}


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


def decode_full_rect(payload: dict, atlas_width: int, atlas_height: int) -> tuple[list[tuple[float, float, float]], list[int], tuple[int, int, int, int], tuple[float, float]]:
    name = str(payload["m_Name"])
    render_data = payload["m_RD"]
    vertex_data = render_data["m_VertexData"]
    vertex_count = int(vertex_data["m_VertexCount"])
    if vertex_count != 4:
        raise RuntimeError(f"{name}: FullRect sprite does not have four vertices")
    channels = vertex_data["m_Channels"]
    active_channels = [
        (index, int(channel["m_Stream"]), int(channel["m_Offset"]), int(channel["m_Format"]), int(channel["m_Dimension"]))
        for index, channel in enumerate(channels) if int(channel["m_Dimension"]) > 0
    ]
    if active_channels != [(POSITION_CHANNEL, 0, 0, FLOAT_FORMAT, 3), (UV_CHANNEL, 1, 0, FLOAT_FORMAT, 2)]:
        raise RuntimeError(f"{name}: unsupported FullRect vertex channel layout: {active_channels}")
    raw_vertices = base64.b64decode(vertex_data["m_Data"], validate=True)
    raw_indices = base64.b64decode(render_data["m_IndexBuffer"], validate=True)
    uv_offset = aligned_16(vertex_count * 12)
    if len(raw_vertices) != uv_offset + vertex_count * 8 or len(raw_indices) != 12:
        raise RuntimeError(f"{name}: FullRect vertex/index stream length differs")
    positions = [struct.unpack_from("<3f", raw_vertices, index * 12) for index in range(vertex_count)]
    uvs = [struct.unpack_from("<2f", raw_vertices, uv_offset + index * 8) for index in range(vertex_count)]
    if any(abs(value) > MESH_EPSILON for uv in uvs for value in uv):
        raise RuntimeError(f"{name}: expected AssetRipper's zeroed FullRect UV stream")
    indices = list(struct.unpack("<6H", raw_indices))
    if sorted(set(indices)) != [0, 1, 2, 3]:
        raise RuntimeError(f"{name}: FullRect triangle indices differ")
    submeshes = render_data["m_SubMeshes"]
    if len(submeshes) != 1 or int(submeshes[0]["m_VertexCount"]) != 4 or int(submeshes[0]["m_IndexCount"]) != 6 or int(submeshes[0]["m_Topology"]) != TRIANGLE_TOPOLOGY:
        raise RuntimeError(f"{name}: FullRect submesh contract differs")
    rect = payload["m_Rect"]
    values = [float(rect[key]) for key in ("m_X", "m_Y", "m_Width", "m_Height")]
    if any(abs(value - rounded_pixel(value)) > MESH_EPSILON for value in values):
        raise RuntimeError(f"{name}: FullRect source rectangle is not integral")
    left, bottom, width, height = map(rounded_pixel, values)
    right, top = left + width, bottom + height
    if left < 0 or bottom < 0 or right > atlas_width or top > atlas_height or width <= 0 or height <= 0:
        raise RuntimeError(f"{name}: FullRect source rectangle is outside the atlas")
    ppu = float(payload["m_PixelsToUnits"])
    xs = [position[0] for position in positions]
    ys = [position[1] for position in positions]
    if any(abs(position[2]) > MESH_EPSILON for position in positions) or abs((max(xs) - min(xs)) * ppu - width) > MESH_EPSILON or abs((max(ys) - min(ys)) * ppu - height) > MESH_EPSILON:
        raise RuntimeError(f"{name}: FullRect vertex dimensions differ from m_Rect")
    pivot = payload["m_Pivot"]
    anchor_x = left + float(pivot["m_X"]) * width
    anchor_y = bottom + float(pivot["m_Y"]) * height
    transform = render_data["m_UvTransform"]
    if abs(float(transform["m_X"]) - ppu) > MESH_EPSILON or abs(float(transform["m_Z"]) - ppu) > MESH_EPSILON or abs(float(transform["m_Y"]) - anchor_x) > MESH_EPSILON or abs(float(transform["m_W"]) - anchor_y) > MESH_EPSILON:
        raise RuntimeError(f"{name}: FullRect UV transform differs from rectangle/pivot")
    return positions, indices, (left, bottom, right, top), (anchor_x, anchor_y)


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


def castle_anim_frame_index(path: Path) -> int:
    name = path.stem
    if not name.startswith(CASTLE_ANIM_FRAME_PREFIX):
        raise RuntimeError(f"Unexpected castle animation frame name: {name}")
    return int(name[len(CASTLE_ANIM_FRAME_PREFIX):])


def extract_castle_anim_frames(source_root: Path, skin_test: Path) -> None:
    sprites = source_root / "Assets" / "Resources" / "sprites"
    metadata_root = sprites / "alltiles"
    atlas_path = sprites / "anims1Sprites.png"
    metadata_paths = sorted(metadata_root.glob("anim_castle *.json"), key=castle_anim_frame_index)
    indices = [castle_anim_frame_index(path) for path in metadata_paths]
    expected_indices = list(range(1, 19)) + list(range(24, 128))
    if indices != expected_indices or len(indices) != CASTLE_ANIM_FRAME_COUNT:
        raise RuntimeError(f"SH1DE anim_castle source indices differ: {indices}")

    colour_dir = skin_test / "AtlasSource" / "CastleAnimColour"
    metadata_dir = skin_test / "AtlasSource" / "CastleAnimMetadata"
    reset_directory(colour_dir, skin_test)
    reset_directory(metadata_dir, skin_test)
    with Image.open(atlas_path) as atlas_image:
        atlas = atlas_image.convert("RGBA")
        if atlas.size != CASTLE_ANIM_SOURCE_ATLAS_SIZE:
            raise RuntimeError(f"Unexpected anims1Sprites dimensions: {atlas.size}")
        for metadata_path in metadata_paths:
            payload = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            name = str(payload["m_Name"])
            index = castle_anim_frame_index(metadata_path)
            if name != metadata_path.stem or bool(payload["m_IsPolygon"]):
                raise RuntimeError(f"{name}: castle animation is not the expected FullRect sprite")
            render_data = payload["m_RD"]
            if int(render_data["m_Texture"]["m_PathID"]) != CASTLE_ANIM_SOURCE_TEXTURE_PATH_ID:
                raise RuntimeError(f"{name}: unexpected anims1Sprites texture PathID")
            if int(render_data["m_SettingsRaw"]) != 0:
                raise RuntimeError(f"{name}: unexpected castle animation render settings")
            vertices, triangles, bounds, anchor = decode_full_rect(payload, atlas.width, atlas.height)
            if len(vertices) != 4 or len(triangles) != 6 or sorted(set(triangles)) != [0, 1, 2, 3]:
                raise RuntimeError(f"{name}: castle animation is not a simple indexed quad")
            rect = payload["m_Rect"]
            values = [float(rect[key]) for key in ("m_X", "m_Y", "m_Width", "m_Height")]
            if any(abs(value - rounded_pixel(value)) > MESH_EPSILON for value in values):
                raise RuntimeError(f"{name}: castle animation rectangle is not integral")
            left, bottom, width, height = map(rounded_pixel, values)
            expected_bounds = (left, bottom, left + width, bottom + height)
            if bounds != expected_bounds or float(payload["m_PixelsToUnits"]) != 64.0:
                raise RuntimeError(f"{name}: castle animation mesh bounds or PPU differ")
            crop = atlas.crop((left, atlas.height - bottom - height, left + width, atlas.height - bottom))
            is_transparent = crop.getchannel("A").getbbox() is None
            if is_transparent != (index in CASTLE_ANIM_TRANSPARENT_INDICES):
                raise RuntimeError(f"{name}: unexpected castle animation alpha coverage")
            pivot_x = (anchor[0] - left) / width
            pivot_y = (anchor[1] - bottom) / height
            source_pivot = payload["m_Pivot"]
            if (
                abs(pivot_x - float(source_pivot["m_X"])) > MESH_EPSILON
                or abs(pivot_y - float(source_pivot["m_Y"])) > MESH_EPSILON
                or (pivot_x, pivot_y) not in ((0.0, 0.0), (0.0, 1.0))
            ):
                raise RuntimeError(f"{name}: castle animation pivot contract differs")
            output_path = colour_dir / f"{name}.png"
            crop.save(output_path, optimize=True)
            write_crlf_json(metadata_dir / metadata_path.name, {
                "m_Name": name,
                "m_Rect": {"m_X": 0, "m_Y": 0, "m_Width": width, "m_Height": height},
                "m_Pivot": {"m_X": pivot_x, "m_Y": pivot_y},
                "m_PixelsToUnits": 64,
                "_SkinTestSource": {
                    "index": index,
                    "left": left,
                    "bottom": bottom,
                    "right": left + width,
                    "top": bottom + height,
                    "sourceAtlasSha256": sha256_file(atlas_path),
                    "colourSha256": sha256_file(output_path),
                    "fullyTransparent": is_transparent,
                },
            })


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
    if result.colour_frames != NORMAL_COUNT + ALTERNATE_COUNT + CASTLE_ANIM_FRAME_COUNT or result.mask_frames != NORMAL_COUNT + ALTERNATE_COUNT:
        raise RuntimeError(f"AtlasBuilder returned unexpected counts: {result}")
    gap_warnings = [warning for warning in result.warnings if "32" in warning and "416" in warning and "447" in warning]
    if not gap_warnings:
        raise RuntimeError(f"AtlasBuilder did not report the expected SHCDE target gap: {result.warnings}")
    castle_anim_warnings = [warning for warning in result.warnings if "anim_castle" in warning]
    if not any("27" in warning and "15" in warning and "125" in warning for warning in castle_anim_warnings):
        raise RuntimeError(f"AtlasBuilder did not report the expected SH1DE-only castle animation slots: {result.warnings}")
    if not any("128" in warning and "138" in warning for warning in castle_anim_warnings):
        raise RuntimeError(f"AtlasBuilder did not report the expected SHCDE-only castle animation tail: {result.warnings}")

    generated = staging / "Override" / "Atlas" / "body_swordsman"
    private_assets = skin_test / "Assets" / "CrusaderSwordsman"
    reset_directory(private_assets, skin_test)
    for name in ("atlas.png", "atlas_m.png", "atlas.json"):
        shutil.copy2(generated / name, private_assets / name)
    generated_castle_anim = staging / "Override" / "Atlas" / "anim_castle"
    private_castle_anim = skin_test / "Assets" / "CrusaderRoundTowerAnimations"
    reset_directory(private_castle_anim, skin_test)
    for name in ("atlas.png", "atlas.json"):
        shutil.copy2(generated_castle_anim / name, private_castle_anim / name)
    if (generated_castle_anim / "atlas_m.png").exists() or (private_castle_anim / "atlas_m.png").exists():
        raise RuntimeError("Plain anim_castle output unexpectedly contains a mask")
    shutil.rmtree(staging)


def create_team_mask(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    mask = Image.new("L", rgba.size, 0)
    source = rgba.load()
    target = mask.load()
    selected = 0
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, alpha = source[x, y]
            maximum = max(red, green, blue)
            minimum = min(red, green, blue)
            saturation = 0 if maximum == 0 else (maximum - minimum) / maximum
            if alpha > 0 and blue >= 52 and blue > red * 1.18 and blue > green * 1.04 and saturation >= 0.22:
                target[x, y] = alpha
                selected += 1
    if selected < 50:
        raise RuntimeError("UI team-colour mask contains too few pixels")
    return mask


def recolour_team_pixels(image: Image.Image, mask: Image.Image, target_colour: tuple[int, int, int]) -> Image.Image:
    output = image.convert("RGBA")
    pixels = output.load()
    mask_pixels = mask.load()
    target_luma = max(1.0, 0.2126 * target_colour[0] + 0.7152 * target_colour[1] + 0.0722 * target_colour[2])
    for y in range(output.height):
        for x in range(output.width):
            strength = mask_pixels[x, y] / 255.0
            if strength <= 0.0:
                continue
            red, green, blue, alpha = pixels[x, y]
            luma = 0.2126 * red + 0.7152 * green + 0.0722 * blue
            scale = max(0.2, min(2.2, luma / target_luma))
            tinted = tuple(min(255, rounded_pixel(channel * scale)) for channel in target_colour)
            pixels[x, y] = (
                rounded_pixel(red * (1.0 - strength) + tinted[0] * strength),
                rounded_pixel(green * (1.0 - strength) + tinted[1] * strength),
                rounded_pixel(blue * (1.0 - strength) + tinted[2] * strength),
                alpha,
            )
    return output


def prepare_ui_assets(source_root: Path, skin_test: Path) -> None:
    atlas_path = source_root / "Assets" / "Texture2D" / "UI-MasterAtlas.png"
    source_dir = skin_test / "AtlasSource" / "UI"
    asset_dir = skin_test / "Assets" / "CrusaderUI"
    reset_directory(source_dir, skin_test)
    reset_directory(asset_dir, skin_test)
    provenance = {
        "source": str(atlas_path),
        "sourceSha256": sha256_file(atlas_path),
        "sourceDimensions": [8192, 4096],
        "rectCoordinateOrigin": "top-left",
        "rectangles": {},
    }
    with Image.open(atlas_path) as atlas_image:
        atlas = atlas_image.convert("RGBA")
        if atlas.size != (8192, 4096):
            raise RuntimeError(f"Unexpected SH1DE UI atlas dimensions: {atlas.size}")
        for name, (x, y, width, height) in UI_SOURCE_RECTS.items():
            if x < 0 or y < 0 or x + width > atlas.width or y + height > atlas.height:
                raise RuntimeError(f"UI source rectangle is outside the atlas: {name}")
            crop = atlas.crop((x, y, x + width, y + height))
            if crop.getchannel("A").getbbox() is None:
                raise RuntimeError(f"UI source rectangle is empty: {name}")
            source_path = source_dir / f"{name}.png"
            crop.save(source_path, optimize=True)
            record = {"x": x, "y": y, "width": width, "height": height, "sha256": sha256_file(source_path)}
            if name.startswith("UIButtons") or name in ("UIBuildingsO011", "UIBuildingsO012"):
                mask = create_team_mask(crop)
                mask_path = source_dir / f"{name}_team-mask.png"
                mask.save(mask_path, optimize=True)
                record["teamMaskSha256"] = sha256_file(mask_path)
                for colour, rgb in PLAYER_COLOURS.items():
                    recoloured = recolour_team_pixels(crop, mask, rgb)
                    recoloured.save(asset_dir / f"{name}_colour{colour}.png", optimize=True)
            else:
                crop.save(asset_dir / f"{name}.png", optimize=True)
            provenance["rectangles"][name] = record
    write_crlf_json(source_dir / "provenance.json", provenance)


def castle_frame_index(path: Path) -> int:
    name = path.stem
    if not name.startswith(CASTLE_FRAME_PREFIX):
        raise RuntimeError(f"Unexpected castle frame name: {name}")
    return int(name[len(CASTLE_FRAME_PREFIX):])


def prepare_castle_assets(source_root: Path, skin_test: Path) -> None:
    source_dir = source_root / "Assets" / "Resources" / "sprites" / "alltiles"
    atlas_path = source_dir / "AllTileSprites.png"
    metadata_paths = sorted(source_dir.glob("tile_castle *.json"), key=castle_frame_index)
    indices = [castle_frame_index(path) for path in metadata_paths]
    if len(indices) != CASTLE_FRAME_COUNT or len(set(indices)) != CASTLE_FRAME_COUNT or min(indices) != 1 or max(indices) != CASTLE_MAX_INDEX:
        raise RuntimeError("SH1DE tile_castle source index contract differs from the expected sparse set")
    from atlas_builder.core import FrameKey, read_target_metadata
    from atlas_builder.models import ProjectConfig
    project = ProjectConfig.load(skin_test / "SkinTest.atlas-project.json")
    required_keys = {"tile_castle": {FrameKey(index) for index in indices}}
    target_frames = read_target_metadata(
        Path(project.target_game_data), {"tile_castle": False}, required_keys=required_keys, language=project.language
    )["tile_castle"]
    source_only_indices = sorted(key.index for key in required_keys["tile_castle"].difference(target_frames))
    if source_only_indices != list(range(812, 1072)):
        raise RuntimeError(f"Unexpected SH1DE-only castle index set: {source_only_indices}")
    for index in (index for index in indices if FrameKey(index) in target_frames):
        target = target_frames[FrameKey(index)]
        if target.name != f"{CASTLE_FRAME_PREFIX}{index:03d}" or target.pixels_per_unit != 64.0:
            raise RuntimeError(f"SHCDE target contract differs for castle index {index}: {target}")

    corrected_dir = skin_test / "AtlasSource" / "CastleMetadata"
    asset_dir = skin_test / "Assets" / "CrusaderRoundTower"
    reset_directory(corrected_dir, skin_test)
    reset_directory(asset_dir, skin_test)
    frames: list[tuple[int, Image.Image, float, float, float, str]] = []
    with Image.open(atlas_path) as atlas_image:
        atlas = atlas_image.convert("RGBA")
        if atlas.size != SOURCE_ATLAS_SIZE:
            raise RuntimeError(f"Unexpected AllTileSprites dimensions: {atlas.size}")
        for metadata_path in metadata_paths:
            payload = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            index = castle_frame_index(metadata_path)
            name = str(payload["m_Name"])
            if name != metadata_path.stem or bool(payload["m_IsPolygon"]):
                raise RuntimeError(f"{name}: castle frame is not the expected FullRect sprite")
            vertices, triangles, bounds, anchor = decode_full_rect(payload, atlas.width, atlas.height)
            if len(vertices) != 4 or len(triangles) != 6 or sorted(set(triangles)) != [0, 1, 2, 3]:
                raise RuntimeError(f"{name}: castle frame is not a simple indexed quad")
            left, bottom, right, top = bounds
            rect = payload["m_Rect"]
            expected_bounds = (
                rounded_pixel(float(rect["m_X"])), rounded_pixel(float(rect["m_Y"])),
                rounded_pixel(float(rect["m_X"]) + float(rect["m_Width"])),
                rounded_pixel(float(rect["m_Y"]) + float(rect["m_Height"])),
            )
            if bounds != expected_bounds or float(payload["m_PixelsToUnits"]) != 64.0:
                raise RuntimeError(f"{name}: castle FullRect bounds or PPU differ")
            crop = atlas.crop((left, atlas.height - top, right, atlas.height - bottom))
            if crop.getchannel("A").getbbox() is None:
                raise RuntimeError(f"{name}: castle frame is empty")
            width, height = crop.size
            pivot_x = (anchor[0] - left) / width
            pivot_y = (anchor[1] - bottom) / height
            if not (math.isfinite(pivot_x) and math.isfinite(pivot_y)):
                raise RuntimeError(f"{name}: castle pivot is invalid")
            frames.append((index, crop.copy(), pivot_x, pivot_y, 64.0, name))
            write_crlf_json(corrected_dir / metadata_path.name, {
                "m_Name": name,
                "m_Rect": {"m_X": 0, "m_Y": 0, "m_Width": width, "m_Height": height},
                "m_Pivot": {"m_X": pivot_x, "m_Y": pivot_y},
                "m_PixelsToUnits": 64,
                "_SkinTestSource": {"left": left, "bottom": bottom, "right": right, "top": top},
            })

    placements: list[tuple[int, Image.Image, int, int, float, float, float, str]] = []
    cursor_x = PACKING_PADDING
    cursor_y = PACKING_PADDING
    row_height = 0
    for index, image, pivot_x, pivot_y, ppu, name in frames:
        if cursor_x + image.width + PACKING_PADDING > PRIVATE_CASTLE_ATLAS_WIDTH:
            cursor_x = PACKING_PADDING
            cursor_y += row_height + PACKING_PADDING
            row_height = 0
        placements.append((index, image, cursor_x, cursor_y, pivot_x, pivot_y, ppu, name))
        cursor_x += image.width + PACKING_PADDING
        row_height = max(row_height, image.height)
    atlas_height = cursor_y + row_height + PACKING_PADDING
    if atlas_height > SOURCE_ATLAS_SIZE[1]:
        raise RuntimeError(f"Private castle atlas does not fit 8192x8192: height={atlas_height}")
    output_atlas = Image.new("RGBA", (PRIVATE_CASTLE_ATLAS_WIDTH, atlas_height), (0, 0, 0, 0))
    manifest_frames = []
    for index, image, x, top_y, pivot_x, pivot_y, ppu, name in placements:
        output_atlas.alpha_composite(image, (x, top_y))
        manifest_frames.append({
            "name": name,
            "index": index,
            "rect": {"x": x, "y": atlas_height - top_y - image.height, "w": image.width, "h": image.height},
            "pivot": {"x": pivot_x, "y": pivot_y},
            "pixelsPerUnit": ppu,
        })
    output_atlas.save(asset_dir / "atlas.png", optimize=True)
    write_crlf_json(asset_dir / "atlas.json", {
        "pixelsPerUnit": 64,
        "sourceSha256": sha256_file(atlas_path),
        "sourceIndexCount": CASTLE_FRAME_COUNT,
        "maximumSourceIndex": CASTLE_MAX_INDEX,
        "sourceOnlyIndices": source_only_indices,
        "targetFrameCount": len(target_frames),
        "targetMaximumIndex": max(key.index for key in target_frames),
        "frames": manifest_frames,
    })


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
    extract_castle_anim_frames(args.source_root.resolve(), skin_test)
    build_private_atlas(workspace, skin_test)
    prepare_ui_assets(args.source_root.resolve(), skin_test)
    prepare_castle_assets(args.source_root.resolve(), skin_test)
    print("Prepared swordsman world/HUD graphics, 1467 sparse SH1DE castle tiles and 122 castle animation frames.")


if __name__ == "__main__":
    main()
