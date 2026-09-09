from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image

from atlas_builder.core import (
    AtlasBuilderError,
    FrameKey,
    PreparedGroup,
    SourceFrame,
    SourceSpriteMetadata,
    TargetFrame,
    _parse_source_stem,
    build_group,
    build_project,
    choose_layout,
    discover_source_group,
    output_pivot,
    prepare_project,
    read_source_metadata,
    validate_generated_group,
)
from atlas_builder.models import GroupConfig, PackingConfig, ProjectConfig


TEST_TEMP = Path(__file__).resolve().parent / ".tmp"
TEST_TEMP.mkdir(exist_ok=True)


def write_png(path: Path, size: tuple[int, int] = (7, 9), colour=(10, 20, 30, 255)) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.new("RGBA", size, colour).save(path)


class NameParsingTests(unittest.TestCase):
    def test_supported_name_forms(self) -> None:
        cases = {
            "Tree_Oak-12": ("Tree_Oak-", FrameKey(12, False)),
            "tile_ruins 012": ("tile_ruins ", FrameKey(12, False)),
            "012": ("", FrameKey(12, False)),
            "body_name-12x": ("body_name-", FrameKey(12, True)),
        }
        for value, expected in cases.items():
            with self.subTest(value=value):
                self.assertEqual(_parse_source_stem(value, "auto"), expected)

    def test_explicit_prefix_is_exact_but_case_insensitive(self) -> None:
        self.assertEqual(_parse_source_stem("RUIN 001", "ruin "), ("RUIN ", FrameKey(1)))
        self.assertIsNone(_parse_source_stem("other 001", "ruin "))


class DiscoveryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(dir=TEST_TEMP)
        self.root = Path(self.temporary.name)
        self.project_path = self.root / "test.atlas-project.json"

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def project(self, group: GroupConfig) -> ProjectConfig:
        return ProjectConfig(
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[group],
            project_path=self.project_path,
        )

    def test_no_masks(self) -> None:
        write_png(self.root / "colour" / "tile_ruins 000.png")
        group = GroupConfig("tile_ruins", "colour")
        frames, prefix = discover_source_group(self.project(group), group)
        self.assertEqual(prefix, "tile_ruins ")
        self.assertIsNone(frames[0].mask_path)

    def test_same_directory_masks(self) -> None:
        write_png(self.root / "images" / "body-0.png")
        write_png(self.root / "images" / "body-0_m.png")
        group = GroupConfig("tile_ruins", "images", "same-directory")
        frames, _ = discover_source_group(self.project(group), group)
        self.assertIsNotNone(frames[0].mask_path)

    def test_separate_directory_masks_accept_optional_suffix(self) -> None:
        write_png(self.root / "colour" / "body-0.png")
        write_png(self.root / "mask" / "body-0.png")
        group = GroupConfig("tile_ruins", "colour", "separate-directory", "mask")
        frames, _ = discover_source_group(self.project(group), group)
        self.assertEqual(frames[0].mask_path.name, "body-0.png")

    def test_missing_mask_is_rejected(self) -> None:
        write_png(self.root / "images" / "body-0.png")
        group = GroupConfig("tile_ruins", "images", "same-directory")
        with self.assertRaisesRegex(AtlasBuilderError, "No matching mask"):
            discover_source_group(self.project(group), group)

    def test_wrong_mask_dimensions_are_rejected(self) -> None:
        write_png(self.root / "images" / "body-0.png", (7, 9))
        write_png(self.root / "images" / "body-0_m.png", (8, 9))
        group = GroupConfig("tile_ruins", "images", "same-directory")
        with self.assertRaisesRegex(AtlasBuilderError, "mask is 8x9"):
            discover_source_group(self.project(group), group)

    def test_duplicate_numeric_index_is_rejected(self) -> None:
        write_png(self.root / "images" / "body-1.png")
        write_png(self.root / "images" / "body-01.png")
        group = GroupConfig("tile_ruins", "images")
        with self.assertRaisesRegex(AtlasBuilderError, "Duplicate frame index"):
            discover_source_group(self.project(group), group)

    def test_corrupt_png_is_rejected(self) -> None:
        path = self.root / "images" / "body-0.png"
        path.parent.mkdir()
        path.write_bytes(b"not a png")
        group = GroupConfig("tile_ruins", "images")
        with self.assertRaisesRegex(AtlasBuilderError, "Invalid colour PNG"):
            discover_source_group(self.project(group), group)


class BuildTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(dir=TEST_TEMP)
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def prepared(self, mask_mode: str = "none") -> tuple[PreparedGroup, ProjectConfig]:
        sources = []
        targets = {}
        for index, size in enumerate(((7, 9), (11, 5), (4, 13))):
            colour = self.root / "source" / f"ruin {index:03}.png"
            write_png(colour, size, (10 + index, 20, 30, 255))
            mask = None
            if mask_mode != "none":
                mask = self.root / "mask" / f"ruin {index:03}_m.png"
                write_png(mask, size, (40 + index, 50, 60, 255))
            key = FrameKey(index)
            sources.append(SourceFrame(key, colour, mask, *size))
            targets[key] = TargetFrame(f"tile_ruins {index}", 0.25, 0.75, 64.0, *size)
        config = GroupConfig("tile_ruins", str(self.root / "source"), mask_mode)
        project = ProjectConfig(
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            packing=PackingConfig(2, 8192),
        )
        return PreparedGroup(config, False, sources, targets, {}, []), project

    def test_plain_ruins_output_has_no_mask_atlas(self) -> None:
        prepared, project = self.prepared()
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        self.assertEqual({item.name for item in output.iterdir()}, {"atlas.png", "atlas.json"})
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        self.assertEqual([item["name"] for item in payload["frames"]], ["tile_ruins 0", "tile_ruins 1", "tile_ruins 2"])

    def test_mask_atlas_uses_identical_layout(self) -> None:
        prepared, project = self.prepared("separate-directory")
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        self.assertTrue((output / "atlas_m.png").is_file())

    def test_non_default_ppu_is_written_per_frame(self) -> None:
        prepared, project = self.prepared()
        first = prepared.target_frames[FrameKey(0)]
        prepared.target_frames[FrameKey(0)] = TargetFrame(
            first.name, first.pivot_x, first.pivot_y, 32.0, first.width, first.height
        )
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        self.assertEqual(payload["frames"][0]["pixelsPerUnit"], 32.0)

    def test_generated_json_renormalizes_target_pixel_anchor(self) -> None:
        prepared, project = self.prepared()
        first = prepared.target_frames[FrameKey(0)]
        prepared.target_frames[FrameKey(0)] = TargetFrame(
            first.name, 0.5, 0.5, first.pixels_per_unit, 7, 5
        )
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        self.assertAlmostEqual(payload["frames"][0]["pivot"]["x"], 3.5 / 7)
        self.assertAlmostEqual(payload["frames"][0]["pivot"]["y"], 2.5 / 9)

    def test_texture_size_limit(self) -> None:
        prepared, _ = self.prepared()
        with self.assertRaisesRegex(AtlasBuilderError, "do not fit"):
            choose_layout(prepared.source_frames, 2, 8)

    def test_crlf_json(self) -> None:
        prepared, project = self.prepared()
        output = self.root / "out"
        build_group(prepared, output, project)
        raw = (output / "atlas.json").read_bytes()
        self.assertNotIn(b"\n", raw.replace(b"\r\n", b""))

    def test_new_manifest_has_only_local_asset_mod_contract(self) -> None:
        prepared, project = self.prepared()
        project.groups = [prepared.config]
        project.mod_info.guid = "RuinTest"
        project.mod_info.name = "Ruin Test"
        with patch("atlas_builder.core.prepare_project", return_value=[prepared]):
            result = build_project(project)
        payload = json.loads((result.output_mod_directory / "info.json").read_text(encoding="utf-8"))
        self.assertEqual(payload["Manifest"], 0)
        self.assertEqual(payload["NetworkMode"], 0)
        self.assertNotIn("MinimumScriptExtenderVersion", payload)

    def test_existing_manifest_is_not_overwritten(self) -> None:
        prepared, project = self.prepared()
        project.groups = [prepared.config]
        output = Path(project.output_mod_directory)
        output.mkdir(parents=True)
        manifest = output / "info.json"
        manifest.write_bytes(b'{"keep":true}\r\n')
        with patch("atlas_builder.core.prepare_project", return_value=[prepared]):
            build_project(project)
        self.assertEqual(manifest.read_bytes(), b'{"keep":true}\r\n')

    def test_failed_staging_does_not_replace_existing_group(self) -> None:
        prepared, project = self.prepared()
        project.groups = [prepared.config]
        existing = Path(project.output_mod_directory) / "Override" / "Atlas" / "tile_ruins"
        existing.mkdir(parents=True)
        marker = existing / "keep.txt"
        marker.write_text("unchanged", encoding="utf-8")
        broken_targets = dict(prepared.target_frames)
        first = broken_targets[FrameKey(0)]
        broken_targets[FrameKey(0)] = TargetFrame(
            first.name, first.pivot_x, first.pivot_y, 0, first.width, first.height
        )
        broken = PreparedGroup(prepared.config, False, prepared.source_frames, broken_targets, {}, [])
        with patch("atlas_builder.core.prepare_project", return_value=[broken]):
            with self.assertRaisesRegex(AtlasBuilderError, "pixels-per-unit"):
                build_project(project, overwrite_existing=True)
        self.assertEqual(marker.read_text(encoding="utf-8"), "unchanged")


class ProjectValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(dir=TEST_TEMP)
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_unknown_group_is_rejected(self) -> None:
        write_png(self.root / "images" / "unknown-0.png")
        project = ProjectConfig(
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("unknown", str(self.root / "images"))],
        )
        with self.assertRaisesRegex(AtlasBuilderError, "Unknown Script Extender"):
            prepare_project(project)

    def test_missing_target_metadata_is_rejected(self) -> None:
        write_png(self.root / "images" / "tile_ruins 0.png")
        project = ProjectConfig(
            language="en",
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("tile_ruins", str(self.root / "images"))],
        )
        with patch("atlas_builder.core.read_target_metadata", side_effect=AtlasBuilderError("No target Sprite metadata")):
            with self.assertRaisesRegex(AtlasBuilderError, "No target Sprite metadata"):
                prepare_project(project)

    def test_partial_group_warns_about_array_truncation(self) -> None:
        write_png(self.root / "images" / "tile_ruins 0.png")
        project = ProjectConfig(
            language="en",
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("tile_ruins", str(self.root / "images"))],
        )
        metadata = {
            "tile_ruins": {
                FrameKey(0): TargetFrame("tile_ruins 0", 0.5, 0.5, 64, 64, 42),
                FrameKey(3): TargetFrame("tile_ruins 3", 0.5, 0.5, 64, 64, 42),
            }
        }
        with patch("atlas_builder.core.read_target_metadata", return_value=metadata):
            prepared = prepare_project(project)
        self.assertIn("truncates trailing vanilla frames", prepared[0].warnings[0])

    def test_texture_limit_above_8192_is_rejected(self) -> None:
        project = ProjectConfig(
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("tile_ruins", str(self.root / "images"))],
            packing=PackingConfig(2, 8193),
        )
        with self.assertRaisesRegex(AtlasBuilderError, "between 64 and 8192"):
            prepare_project(project)


class ProjectFileTests(unittest.TestCase):
    def test_paths_below_project_are_saved_relative_and_crlf(self) -> None:
        with tempfile.TemporaryDirectory(dir=TEST_TEMP) as temporary:
            root = Path(temporary)
            path = root / "sample.atlas-project.json"
            project = ProjectConfig(
                target_game_data=str(root / "Game_Data"),
                output_mod_directory=str(root / "Mod"),
                groups=[GroupConfig("tile_ruins", str(root / "sources"))],
            )
            project.save(path)
            raw = path.read_bytes()
            self.assertNotIn(b"\n", raw.replace(b"\r\n", b""))
            data = json.loads(raw)
            self.assertEqual(data["outputModDirectory"], "Mod")
            self.assertEqual(ProjectConfig.load(path).resolve_path("Mod"), (root / "Mod").resolve())

    def test_schema_one_loads_with_legacy_pivot_and_saves_as_schema_two(self) -> None:
        with tempfile.TemporaryDirectory(dir=TEST_TEMP) as temporary:
            root = Path(temporary)
            path = root / "legacy.atlas-project.json"
            path.write_text(json.dumps({
                "schemaVersion": 1,
                "groups": [{"gmFileName": "tile_ruins", "colourDirectory": "images"}],
            }), encoding="utf-8")
            project = ProjectConfig.load(path)
            self.assertEqual(project.loaded_schema_version, 1)
            self.assertEqual(project.groups[0].pivot_mode, "target-normalized")
            project.save(path)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8"))["schemaVersion"], 2)
            self.assertEqual(project.loaded_schema_version, 2)

    def test_source_metadata_path_below_project_is_saved_relative(self) -> None:
        with tempfile.TemporaryDirectory(dir=TEST_TEMP) as temporary:
            root = Path(temporary)
            path = root / "sample.atlas-project.json"
            project = ProjectConfig(groups=[GroupConfig(
                "tile_ruins", str(root / "images"), pivot_mode="source-metadata",
                source_metadata_directory=str(root / "ripped"),
            )])
            project.save(path)
            data = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(data["groups"][0]["sourceMetadataDirectory"], "ripped")

    def test_schema_two_group_defaults_to_target_pixel_anchor(self) -> None:
        with tempfile.TemporaryDirectory(dir=TEST_TEMP) as temporary:
            path = Path(temporary) / "current.atlas-project.json"
            path.write_text(json.dumps({
                "schemaVersion": 2,
                "groups": [{"gmFileName": "tile_ruins", "colourDirectory": "images"}],
            }), encoding="utf-8")
            self.assertEqual(ProjectConfig.load(path).groups[0].pivot_mode, "target-pixel-anchor")


class PivotTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(dir=TEST_TEMP)
        self.root = Path(self.temporary.name)
        self.image = self.root / "tile_land8 001.png"
        write_png(self.image, (64, 196))
        self.source = SourceFrame(FrameKey(1), self.image, None, 64, 196)
        self.target = TargetFrame("tile_land8 001", 0.5, 0.40243897, 64, 64, 41)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def prepared(self, mode: str, metadata=None) -> PreparedGroup:
        config = GroupConfig("tile_land8", str(self.root), pivot_mode=mode)
        return PreparedGroup(config, False, [self.source], {FrameKey(1): self.target}, metadata or {}, [])

    def test_target_pixel_anchor_survives_different_image_height(self) -> None:
        x, y = output_pivot(self.prepared("target-pixel-anchor"), self.source)
        self.assertAlmostEqual(x * 64, 32.0, places=5)
        self.assertAlmostEqual(y * 196, 16.5, places=3)

    def test_legacy_mode_copies_normalized_target_pivot(self) -> None:
        self.assertEqual(output_pivot(self.prepared("target-normalized"), self.source), (0.5, 0.40243897))

    def test_source_metadata_mode_uses_individual_source_pivot(self) -> None:
        metadata = {FrameKey(1): SourceSpriteMetadata(
            "tile_land8 001", 0.5, 0.08418399, 64, 64, 196, self.root / "frame.json"
        )}
        x, y = output_pivot(self.prepared("source-metadata", metadata), self.source)
        self.assertAlmostEqual(x * 64, 32.0, places=5)
        self.assertAlmostEqual(y * 196, 16.5, places=3)

    def test_pixel_anchor_outside_replacement_is_rejected(self) -> None:
        small = SourceFrame(FrameKey(1), self.image, None, 16, 10)
        with self.assertRaisesRegex(AtlasBuilderError, "add transparent canvas space"):
            output_pivot(self.prepared("target-pixel-anchor"), small)


class SourceMetadataTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(dir=TEST_TEMP)
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_metadata(self, name: str, pivot_y: float = 0.40243897) -> Path:
        path = self.root / f"{name}.json"
        path.write_text(json.dumps({
            "m_Name": name,
            "m_Rect": {"m_Width": 64, "m_Height": 41},
            "m_Pivot": {"m_X": 0.5, "m_Y": pivot_y},
            "m_PixelsToUnits": 64,
        }), encoding="utf-8")
        return path

    def test_reads_main_and_alternate_metadata_by_source_prefix(self) -> None:
        self.write_metadata("tile_land8 001")
        self.write_metadata("tile_land8 001x")
        result = read_source_metadata(self.root, "tile_land8 ", {FrameKey(1), FrameKey(1, True)})
        self.assertEqual(set(result), {FrameKey(1), FrameKey(1, True)})

    def test_missing_metadata_is_rejected(self) -> None:
        with self.assertRaisesRegex(AtlasBuilderError, "Missing source metadata"):
            read_source_metadata(self.root, "tile_land8 ", {FrameKey(1)})

    def test_duplicate_metadata_is_rejected(self) -> None:
        self.write_metadata("tile_land8 001")
        nested = self.root / "nested"
        nested.mkdir()
        (nested / "tile_land8 001.json").write_text(
            (self.root / "tile_land8 001.json").read_text(encoding="utf-8"), encoding="utf-8"
        )
        with self.assertRaisesRegex(AtlasBuilderError, "Duplicate source metadata"):
            read_source_metadata(self.root, "tile_land8 ", {FrameKey(1)})

    def test_invalid_source_pivot_is_rejected(self) -> None:
        self.write_metadata("tile_land8 001", 1.5)
        with self.assertRaisesRegex(AtlasBuilderError, "pivot outside"):
            read_source_metadata(self.root, "tile_land8 ", {FrameKey(1)})


if __name__ == "__main__":
    unittest.main()
