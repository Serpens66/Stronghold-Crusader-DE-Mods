from __future__ import annotations

import json
import os
import unittest
from pathlib import Path

from atlas_builder.core import build_project
from atlas_builder.models import ProjectConfig


@unittest.skipUnless(os.environ.get("ATLAS_BUILDER_INTEGRATION") == "1", "integration test not requested")
class SH1BaeumeIntegrationTests(unittest.TestCase):
    def test_general_builder_reproduces_all_tree_mappings(self) -> None:
        workspace = Path(__file__).resolve().parents[2]
        project_path = workspace / "Baeume_Test" / "SH1Baeume.atlas-project.json"
        project = ProjectConfig.load(project_path)
        project.output_mod_directory = str(workspace / ".inspect" / "AtlasBuilderIntegration" / "SH1Baeume")
        project.create_manifest_if_missing = True
        result = build_project(project, overwrite_existing=True)

        self.assertEqual(result.colour_frames, 685)
        self.assertEqual(result.mask_frames, 668)
        self.assertEqual(len(result.groups), 8)
        atlas_root = result.output_mod_directory / "Override" / "Atlas"
        reference_root = workspace / "Baeume_Test" / "SH1Baeume" / "Override" / "Atlas"
        self.assertFalse((atlas_root / "tree_cactii" / "atlas_m.png").exists())

        def semantic_frames(path: Path) -> dict[str, tuple[int, int, float, float, float]]:
            payload = json.loads(path.read_text(encoding="utf-8"))
            default_ppu = float(payload.get("pixelsPerUnit", 64))
            return {
                frame["name"]: (
                    int(frame["rect"]["w"]),
                    int(frame["rect"]["h"]),
                    float(frame["pivot"]["x"]),
                    float(frame["pivot"]["y"]),
                    float(frame.get("pixelsPerUnit", default_ppu)),
                )
                for frame in payload["frames"]
            }

        for group in result.groups:
            self.assertEqual(
                semantic_frames(atlas_root / group / "atlas.json"),
                semantic_frames(reference_root / group / "atlas.json"),
            )

        expected_prefixes = {
            "tree_pine": "tree_pine-",
            "Tree_Chestnut": "Tree_Chestnut-",
        }
        for group, prefix in expected_prefixes.items():
            payload = json.loads((atlas_root / group / "atlas.json").read_text(encoding="utf-8"))
            self.assertTrue(payload["frames"])
            self.assertTrue(all(frame["name"].startswith(prefix) for frame in payload["frames"]))


if __name__ == "__main__":
    unittest.main()
