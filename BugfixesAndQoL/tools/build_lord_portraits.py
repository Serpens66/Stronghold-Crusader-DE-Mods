"""Rebuild the HUD portrait PNG from a local SHCDE installation (UnityPy and Pillow)."""

import argparse
from pathlib import Path

import UnityPy
from PIL import Image
from UnityPy.export.SpriteHelper import render_sprite_mesh
from UnityPy.export.Texture2DConverter import get_image_from_texture2d
from UnityPy.helpers.MeshHelper import MeshHandler


# Rows follow APIShared.LordSkin; each source frame faces three-quarters toward the viewer.
FRAMES = (
    "body_lord-2",
    "body_saladin-2",
    "body_bedouin_lord-2",
    "body_scribe_lord-2",
    "body_lord_female-2",
    "body_bessie-2",
    "body_arab_lord_female-2",
    "body_bedouin_lord_female-2",
)
# spriteLoader.lordLadyColours in UI order (see PlayerColours).
COLOURS = (
    (1, 1, 1),
    (0.8, 0.109, 0.219),
    (0.945, 0.478, 0.121),
    (1, 0.823, 0.137),
    (0.058, 0.572, 0.831),
    (0.435, 0.435, 0.435),
    (0.654, 0.156, 0.764),
    (0.274, 0.992, 0.984),
    (0.301, 0.831, 0.129),
)
# Match the proportions of the previous 105 x 179 HUD portrait.
CELL_WIDTH = 72
CELL_HEIGHT = 123
INSET = 1


def render_frame(body, mask, colour):
    # Unlit/TeamColour with zero foot cut and full opacity. Both textures share UVs.
    pixels = []
    for (red, green, blue, alpha), weight in zip(body.getdata(), mask.getdata()):
        if not alpha:
            pixels.append((0, 0, 0, 0))
            continue
        tinted = tuple(
            round(value * (1 + weight / 255 * (tint - 1)))
            for value, tint in zip((red, green, blue), colour)
        )
        pixels.append((*tinted, alpha))
    image = Image.new("RGBA", body.size)
    image.putdata(pixels)
    return image


def build_atlas(game_data):
    assets = UnityPy.load(str(game_data / "resources.assets"))
    sprites = {}
    textures = {}
    for asset in assets.objects:
        if asset.type.name == "Sprite":
            name = asset.peek_name()
            if name in FRAMES:
                sprites[name] = asset.read()
        elif asset.type.name == "Texture2D":
            name = asset.peek_name()
            if name and name.endswith("Sprites_m"):
                textures[name] = asset

    atlas = Image.new("RGBA", (CELL_WIDTH * len(COLOURS), CELL_HEIGHT * len(FRAMES)))
    texture_pairs = {}
    for row, name in enumerate(FRAMES):
        sprite = sprites[name]
        render_data = sprite.m_RD
        if render_data.settingsRaw & 1:
            raise ValueError(f"Unexpected packed sprite {name}")
        texture = render_data.texture.deref()
        texture_name = texture.peek_name()
        if texture_name not in texture_pairs:
            body_atlas = get_image_from_texture2d(texture.read(), False)
            mask_atlas = get_image_from_texture2d(textures[texture_name + "_m"].read(), False).getchannel("R")
            if body_atlas.size != mask_atlas.size:
                raise ValueError(f"Misaligned texture and mask for {texture_name}")
            texture_pairs[texture_name] = body_atlas, mask_atlas
        body_atlas, mask_atlas = texture_pairs[texture_name]
        # Tight meshes can share a bounding rectangle with pixels from a different frame.
        # Render both textures through the same UV triangles to isolate the actual sprite.
        mesh = MeshHandler(render_data, sprite.object_reader.version)
        mesh.process()
        if not mesh.m_UV0:
            raise ValueError(f"Missing texture coordinates for {name}")
        body = render_sprite_mesh(sprite, mesh, body_atlas).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        mask = render_sprite_mesh(sprite, mesh, mask_atlas).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        alpha = body.getchannel("A")
        visible = alpha.getbbox()
        if visible is None:
            raise ValueError(f"Empty sprite {name}")
        # A few almost transparent pixels above the figure can shrink the whole
        # portrait. Keep normal antialiased edges and trim only excess vertical padding.
        solid = alpha.point(lambda value: 255 if value >= 32 else 0).getbbox()
        if solid is not None and solid[3] - solid[1] < 0.95 * (visible[3] - visible[1]):
            left, top, right, bottom = visible
            visible = (left, max(top, solid[1] - 1), right, min(bottom, solid[3] + 1))
        body = body.crop(visible)
        mask = mask.crop(visible)
        scale = min((CELL_WIDTH - 2 * INSET) / body.width, (CELL_HEIGHT - 2 * INSET) / body.height)
        size = (round(body.width * scale), round(body.height * scale))
        for column, colour in enumerate(COLOURS):
            icon = render_frame(body, mask, colour).resize(size, Image.Resampling.LANCZOS)
            x = column * CELL_WIDTH + (CELL_WIDTH - icon.width) // 2
            y = row * CELL_HEIGHT + (CELL_HEIGHT - icon.height) // 2
            atlas.paste(icon, (x, y))
    return atlas


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_data", type=Path, help="Installed Stronghold Crusader Definitive Edition_Data")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] /
                        "Override/Assets/GUI/Sprites/BugfixesAndQoL-Lord.png")
    args = parser.parse_args()
    build_atlas(args.game_data).save(args.output)
