from __future__ import annotations

import os
import unittest
from pathlib import Path

from atlas_builder.core import FrameKey, read_source_metadata


RIP_ROOT = os.environ.get("ATLAS_BUILDER_SH1_RIP")


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


if __name__ == "__main__":
    unittest.main()
