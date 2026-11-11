"""Guard aspect ratio, alpha, frame isolation and static-resource declaration order."""

import importlib.util
from pathlib import Path
import sys
import unittest
import xml.etree.ElementTree as ET

from PIL import Image

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools/hud_assets.py"
sys.path.insert(0, str(MODULE_PATH.parent))
SPEC = importlib.util.spec_from_file_location("hud_assets", MODULE_PATH)
assets = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(assets)


class ImageContractTests(unittest.TestCase):
    def test_matching_canvas_preserves_every_rgba_byte(self):
        source = Image.new("RGBA", (4, 3), (230, 80, 10, 0))
        source.putpixel((2, 1), (30, 50, 70, 128))
        result, rect = assets.fit(source, source.size, "center")
        self.assertEqual(source.tobytes(), result.tobytes())
        self.assertEqual((0, 0, 4, 3), rect)

    def test_proportional_fit_adds_transparent_padding(self):
        source = Image.new("RGBA", (2, 4), (20, 40, 60, 128))
        result, rect = assets.fit(source, (8, 8), "center")
        self.assertEqual((2, 0, 4, 8), rect)
        self.assertEqual(0, result.getpixel((0, 0))[3])
        self.assertEqual(128, result.getpixel((3, 3))[3])

    def test_building_canvas_is_bottom_aligned(self):
        source = Image.new("RGBA", (8, 2), (30, 60, 90, 255))
        result, rect = assets.fit(source, (8, 8), "bottom-center")
        self.assertEqual((0, 6, 8, 2), rect)
        self.assertEqual(0, result.getpixel((4, 0))[3])
        self.assertEqual(255, result.getpixel((4, 7))[3])

    def test_packing_keeps_every_frame_and_gutter_separate(self):
        rows = [{"key": str(i), "width": 12 + i % 7, "height": 8 + i % 5} for i in range(60)]
        sizes, frames = assets.pack(rows, page_size=64)
        self.assertEqual({r["key"] for r in rows}, set(frames))
        occupied = {}
        for frame in frames.values():
            index = frame["page"]
            x, y, w, h = frame["rect"]
            padded = (x - 2, y - 2, w + 4, h + 4)
            self.assertTrue(assets.contains((0, 0, *sizes[index]), padded))
            for previous in occupied.setdefault(index, []):
                self.assertFalse(assets.intersects(padded, previous))
            occupied[index].append(padded)
        self.assertEqual((sizes, frames), assets.pack(list(reversed(rows)), page_size=64))

    def test_oversized_frame_is_rejected(self):
        with self.assertRaises(ValueError):
            assets.pack([{"key": "large", "width": 64, "height": 2}], page_size=64)

    def test_gutter_extrudes_color_and_alpha_without_changing_crop(self):
        source = Image.new("RGBA", (2, 3), (20, 40, 60, 128))
        page = Image.new("RGBA", (6, 7))
        assets.add_gutter(page, source, 2, 2)
        self.assertEqual(source.tobytes(), page.crop((2, 2, 4, 5)).tobytes())
        self.assertEqual((20, 40, 60, 128), page.getpixel((0, 0)))
        self.assertEqual((20, 40, 60, 128), page.getpixel((5, 6)))

    def test_patch_declares_atlases_before_unchanged_type_replacements(self):
        rows = [{"key": "test", "width": 12, "height": 8}]
        sizes, frames = assets.pack(rows, page_size=64)
        patch = ET.fromstring(assets.patch_text(rows, sizes, frames))
        self.assertEqual("InsertAfter", patch[0].get("Type"))
        self.assertEqual("Replace", patch[-1].get("Type"))
        for operation in patch:
            self.assertEqual(1, len(operation.find("Content")))
        self.assertEqual("CroppedBitmap", patch[-1].find("Content")[0].tag)
        self.assertEqual("test", patch[-1].find("Content")[0].get(assets.KEY))
        self.assertTrue(patch[0].find("Content")[0].get("UriSource").endswith(".dds"))

    def test_corrected_gallows_states_use_identical_scale_and_registration(self):
        rows = {r["key"]: r for r in assets.read_json(assets.ROOT / "resources.json")["resources"]}
        rects = []
        for key in ("UI-Buildings G001", "UI-Buildings G002"):
            with Image.open(assets.ROOT / "Sources" / rows[key]["file"]) as source:
                image, rect = assets.normalize(source, rows[key])
                self.assertEqual((86, 160), image.size)
                rects.append(rect)
        self.assertEqual(rects[0], rects[1])

    def test_tower_hover_retains_same_scale_with_extra_transparent_target_row(self):
        rows = {r["key"]: r for r in assets.read_json(assets.ROOT / "resources.json")["resources"]}
        rects = []
        for key in ("UI-Buildings K005", "UI-Buildings K006"):
            with Image.open(assets.ROOT / "Sources" / rows[key]["file"]) as source:
                image, rect = assets.normalize(source, rows[key])
                rects.append(rect)
                if key.endswith("6"):
                    self.assertIsNone(image.getchannel("A").crop((0, 154, 98, 155)).getbbox())
        self.assertEqual(rects[0], rects[1])

    def test_disabled_resources_only_loses_fully_transparent_source_margins(self):
        rows = {r["key"]: r for r in assets.read_json(assets.ROOT / "resources.json")["resources"]}
        row = rows["Resources_Disabled"]
        with Image.open(assets.ROOT / "Sources" / row["file"]) as source:
            image, rect = assets.normalize(source, row)
            self.assertEqual((0, 0, 68, 68), rect)
            self.assertEqual(source.crop((2, 0, 70, 68)).tobytes(), image.tobytes())
            changed = source.copy()
            changed.putpixel((0, 0), (255, 0, 0, 255))
            with self.assertRaisesRegex(ValueError, "visible pixels"):
                assets.normalize(changed, row)

    def test_missing_insertion_anchor_is_rejected_before_packaging(self):
        with self.assertRaisesRegex(ValueError, "insertion anchor"):
            assets.require_anchor({})
        rows = [{"key": "test", "width": 12, "height": 8}]
        sizes, frames = assets.pack(rows, page_size=64)
        patch = ET.fromstring(assets.patch_text(rows, sizes, frames))
        with self.assertRaisesRegex(ValueError, "exactly once"):
            assets.apply_emitted_patch({}, patch)

    def test_application_preserves_other_resources_and_declaration_order(self):
        anchor = ET.Element("{urn:test}BitmapImage", {assets.KEY: "UI-MasterAtlas"})
        original = ET.Element("{urn:test}CroppedBitmap", {assets.KEY: "test", "SourceRect": "2,2,12,8"})
        preserved = ET.Element("{urn:test}CroppedBitmap", {assets.KEY: "Panel_BG", "SourceRect": "4,4,32,32"})
        entries = {"UI-MasterAtlas": anchor, "test": original, "Panel_BG": preserved}
        rows = [{"key": "test", "width": 12, "height": 8}]
        sizes, frames = assets.pack(rows, page_size=64)
        patch = ET.fromstring(assets.patch_text(rows, sizes, frames))
        merged = assets.apply_emitted_patch(entries, patch)
        keys = [element.get(assets.KEY) for element in merged]
        self.assertLess(keys.index(assets.page_key(0)), keys.index("test"))
        self.assertEqual(ET.tostring(preserved), ET.tostring(merged[keys.index("Panel_BG")]))
        self.assertEqual("2,2,12,8", original.get("SourceRect"))


if __name__ == "__main__":
    unittest.main()
