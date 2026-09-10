from __future__ import annotations

import os
import unittest
from pathlib import Path

from atlas_builder.core import FrameKey, read_source_metadata, read_target_metadata


RIP_ROOT = os.environ.get("ATLAS_BUILDER_SH1_RIP")
SHC_DATA = os.environ.get("ATLAS_BUILDER_SHC_DATA")


@unittest.skipUnless(RIP_ROOT, "ATLAS_BUILDER_SH1_RIP is not set")
class AssetRipperIntegrationTests(unittest.TestCase):
    def test_confirmed_sh1de_tile_metadata_uses_fixed_pixel_anchor(self) -> None:
        metadata_root = Path(RIP_ROOT) / "Assets" / "Resources" / "sprites" / "alltiles"
        cases = {
            "tile_land8 ": (FrameKey(1), 32.0, 16.5),
            "tile_buildings1 ": (FrameKey(1), 32.0, 16.5),
            "tile_churches ": (FrameKey(1), 32.0, 16.5),
            "tile_ruins ": (FrameKey(1), 32.0, 16.5),
        }
        for prefix, (key, expected_x, expected_y) in cases.items():
            with self.subTest(prefix=prefix):
                item = read_source_metadata(metadata_root, prefix, {key})[key]
                self.assertAlmostEqual(item.pivot_x * item.width, expected_x, places=3)
                self.assertAlmostEqual(item.pivot_y * item.height, expected_y, places=3)

    @unittest.skipUnless(SHC_DATA, "ATLAS_BUILDER_SHC_DATA is not set")
    def test_swordsman_source_metadata_fills_the_confirmed_shcde_gap(self) -> None:
        expected_gap = {FrameKey(index) for index in range(416, 448)}
        targets = read_target_metadata(
            Path(SHC_DATA),
            {"body_swordsman": True},
            {"body_swordsman": {FrameKey(index) for index in range(1088)}},
        )["body_swordsman"]
        self.assertEqual(
            {FrameKey(index) for index in range(1088)} - set(targets),
            expected_gap,
        )
        metadata_root = Path(RIP_ROOT) / "Assets" / "Resources" / "sprites"
        source = read_source_metadata(metadata_root, "body_swordsman-", expected_gap)
        self.assertEqual(set(source), expected_gap)


if __name__ == "__main__":
    unittest.main()
