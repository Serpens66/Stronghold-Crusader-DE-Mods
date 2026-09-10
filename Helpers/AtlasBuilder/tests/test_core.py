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
    _format_frame_keys,
    _parse_source_stem,
    _split_target_name,
    build_group,
    build_project,
    calculate_frame_coverage,
    choose_layout,
    discover_source_group,
    output_pivot,
    prepare_project,
    read_source_metadata,
    validate_generated_group,
)
from atlas_builder.models import GroupConfig, PackingConfig, ProjectConfig
from atlas_builder.gm_groups import GROUP_CONTRACTS, atlas_material_name


TEST_TEMP = Path(tempfile.gettempdir()) / "AtlasBuilderTests"
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

    def test_target_names_with_numeric_group_suffixes_are_split_correctly(self) -> None:
        self.assertEqual(_split_target_name("float_pop_circ-1-23"), ("float_pop_circ-1", True, FrameKey(23)))
        self.assertEqual(_split_target_name("smoke-30x30-7x"), ("smoke-30x30", True, FrameKey(7, True)))
        self.assertEqual(_split_target_name("tile_buildings1 007"), ("tile_buildings1", False, FrameKey(7)))

    def test_frame_ranges_are_compact_and_keep_alternate_suffixes(self) -> None:
        keys = {FrameKey(index) for index in range(416, 448)} | {FrameKey(0, True), FrameKey(1, True)}
        self.assertEqual(_format_frame_keys(keys), "416–447, 0x–1x")

    def test_anim_castle_coverage_regression(self) -> None:
        source_missing = {0, 19, 20, 21, 22, 23}
        target_missing = set(range(0, 1)) | set(range(15, 24)) | {25, 26} | set(range(36, 44)) | set(range(84, 90)) | set(range(91, 94)) | set(range(122, 126))
        source = {FrameKey(index) for index in range(128) if index not in source_missing}
        target = {FrameKey(index) for index in range(139) if index not in target_missing}
        coverage = calculate_frame_coverage(source, target)

        self.assertEqual(len(source), 122)
        self.assertEqual(len(target), 106)
        self.assertEqual(len(coverage.shared), 95)
        self.assertEqual(len(coverage.source_only), 27)
        self.assertEqual(_format_frame_keys(coverage.source_only), "15–18, 25–26, 36–43, 84–89, 91–93, 122–125")
        self.assertEqual(len(coverage.target_only), 11)
        self.assertEqual(_format_frame_keys(coverage.target_only), "128–138")
        self.assertEqual(coverage.source_maximum, "127")
        self.assertEqual(coverage.target_maximum, "138")


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
        self.assertEqual(payload["material"], "Plain")
        self.assertEqual([item["name"] for item in payload["frames"]], ["tile_ruins 0", "tile_ruins 1", "tile_ruins 2"])

    def test_mask_atlas_uses_identical_layout(self) -> None:
        prepared, project = self.prepared("separate-directory")
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        self.assertTrue((output / "atlas_m.png").is_file())

    def test_foliage_material_is_written_explicitly(self) -> None:
        prepared, project = self.prepared("separate-directory")
        prepared.config.gm_file_name = "tree_apple"
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        self.assertEqual(payload["material"], "Foliage")

    def test_generated_validator_rejects_wrong_material(self) -> None:
        prepared, project = self.prepared()
        output = self.root / "out"
        build_group(prepared, output, project)
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        payload["material"] = "TeamColour"
        (output / "atlas.json").write_bytes(
            (json.dumps(payload, indent=2) + "\n").replace("\n", "\r\n").encode("utf-8")
        )
        with self.assertRaisesRegex(AtlasBuilderError, "generated material must be Plain"):
            validate_generated_group(prepared, output, project)

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

    def test_generated_json_accepts_negative_and_corner_pivots(self) -> None:
        prepared, project = self.prepared()
        first = prepared.target_frames[FrameKey(0)]
        second = prepared.target_frames[FrameKey(1)]
        prepared.target_frames[FrameKey(0)] = TargetFrame(
            first.name, first.pivot_x, -0.25, first.pixels_per_unit, first.width, first.height
        )
        prepared.target_frames[FrameKey(1)] = TargetFrame(
            second.name, second.pivot_x, 1.0, second.pixels_per_unit, second.width, second.height + 20
        )
        output = self.root / "out"
        build_group(prepared, output, project)
        validate_generated_group(prepared, output, project)
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        self.assertEqual(payload["frames"][0]["pivot"]["y"], -0.25)
        self.assertEqual(payload["frames"][1]["pivot"]["y"], 1.0)

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

    def _write_swordsman_source(self, indices=(0, 1)) -> None:
        for index in indices:
            write_png(self.root / "images" / f"source-{index}.png")
            write_png(self.root / "images" / f"source-{index}_m.png")
            payload = {
                "m_Name": f"source-{index}",
                "m_Rect": {"m_Width": 7, "m_Height": 9},
                "m_Pivot": {"m_X": 0.25 + index / 10, "m_Y": 0.75},
                "m_PixelsToUnits": 64,
            }
            path = self.root / "metadata" / f"source-{index}.json"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(payload), encoding="utf-8")

    def _swordsman_project(self, missing_policy="reject", pivot_mode="source-metadata") -> ProjectConfig:
        return ProjectConfig(
            language="en",
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig(
                "body_swordsman",
                str(self.root / "images"),
                "same-directory",
                pivot_mode=pivot_mode,
                source_metadata_directory=str(self.root / "metadata") if pivot_mode == "source-metadata" else None,
                missing_target_policy=missing_policy,
            )],
        )

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

    def test_partial_group_reports_preserved_target_frames(self) -> None:
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
        self.assertTrue(any("1 SHCDE-only (3)" in warning for warning in prepared[0].warnings))
        self.assertTrue(any("preserves these vanilla frames" in warning for warning in prepared[0].warnings))
        self.assertFalse(any("truncat" in warning for warning in prepared[0].warnings))

    def test_texture_limit_above_8192_is_rejected(self) -> None:
        project = ProjectConfig(
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("tile_ruins", str(self.root / "images"))],
            packing=PackingConfig(2, 8193),
        )
        with self.assertRaisesRegex(AtlasBuilderError, "between 64 and 8192"):
            prepare_project(project)

    def test_incomplete_foliage_group_reports_preserved_vanilla_frames(self) -> None:
        write_png(self.root / "images" / "tree_apple-0.png")
        write_png(self.root / "images" / "tree_apple-0_m.png")
        project = ProjectConfig(
            language="en",
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("tree_apple", str(self.root / "images"), "same-directory")],
        )
        metadata = {"tree_apple": {
            FrameKey(0): TargetFrame("tree_apple-0", 0.5, 0.5, 64, 7, 9),
            FrameKey(1): TargetFrame("tree_apple-1", 0.5, 0.5, 64, 7, 9),
        }}
        with patch("atlas_builder.core.read_target_metadata", return_value=metadata):
            warnings = prepare_project(project)[0].warnings
        self.assertTrue(any("target frames are absent" in warning for warning in warnings))
        self.assertTrue(any("preserves these vanilla frames" in warning for warning in warnings))
        self.assertFalse(any("incorrectly creates" in warning for warning in warnings))

    def test_missing_target_default_policy_still_rejects(self) -> None:
        self._write_swordsman_source()
        metadata = {"body_swordsman": {
            FrameKey(0): TargetFrame("body_swordsman-0", 0.5, 0.5, 64, 7, 9),
        }}
        with patch("atlas_builder.core.read_target_metadata", return_value=metadata):
            with self.assertRaisesRegex(AtlasBuilderError, "source indices not present in SHCDE"):
                prepare_project(self._swordsman_project())

    def test_rejection_keeps_coverage_diagnostics_without_obsolete_truncation_warning(self) -> None:
        write_png(self.root / "images" / "anim_castle 15.png")
        write_png(self.root / "images" / "anim_castle 127.png")
        project = ProjectConfig(
            language="en",
            target_game_data=str(self.root / "Data"),
            output_mod_directory=str(self.root / "Mod"),
            groups=[GroupConfig("anim_castle", str(self.root / "images"))],
        )
        target = {
            FrameKey(index): TargetFrame(f"anim_castle {index}", 0.5, 0.5, 64, 7, 9)
            for index in [127, *range(128, 139)]
        }
        with patch("atlas_builder.core.read_target_metadata", return_value={"anim_castle": target}):
            with self.assertRaises(AtlasBuilderError) as raised:
                prepare_project(project)
        message = str(raised.exception)
        self.assertIn("1 shared; 1 source-only (15); 11 SHCDE-only (128–138)", message)
        self.assertIn("highest source index 127; highest SHCDE index 138", message)
        self.assertNotIn("truncat", message)

    def test_missing_target_fallback_requires_source_metadata_pivot(self) -> None:
        project = self._swordsman_project("source-metadata", "target-pixel-anchor")
        with self.assertRaisesRegex(AtlasBuilderError, "only be filled when the pivot source"):
            prepare_project(project)

    def test_missing_target_fallback_adds_only_absent_slots_and_builds_them(self) -> None:
        self._write_swordsman_source()
        vanilla = TargetFrame("body_swordsman-0", 0.9, 0.8, 32, 11, 13)
        metadata = {"body_swordsman": {FrameKey(0): vanilla}}
        with patch("atlas_builder.core.read_target_metadata", return_value=metadata):
            prepared = prepare_project(self._swordsman_project("source-metadata"))[0]

        self.assertIs(prepared.target_frames[FrameKey(0)], vanilla)
        added = prepared.target_frames[FrameKey(1)]
        self.assertEqual(added.name, "body_swordsman-1")
        self.assertEqual((added.pivot_x, added.pivot_y, added.width, added.height), (0.35, 0.75, 7, 9))
        self.assertTrue(any("validated source metadata is used for 1" in warning for warning in prepared.warnings))

        output = self.root / "atlas"
        build_group(prepared, output, self._swordsman_project("source-metadata"))
        validate_generated_group(prepared, output, self._swordsman_project("source-metadata"))
        payload = json.loads((output / "atlas.json").read_text(encoding="utf-8"))
        self.assertEqual([frame["name"] for frame in payload["frames"]], ["body_swordsman-0", "body_swordsman-1"])

    def test_missing_target_fallback_rejects_mismatched_source_metadata_name(self) -> None:
        self._write_swordsman_source((1,))
        path = self.root / "metadata" / "source-1.json"
        metadata = {"body_swordsman": {FrameKey(0): TargetFrame(
            "body_swordsman-0", 0.5, 0.5, 64, 7, 9
        )}}
        for invalid_name in ("other-1", "source-2", "source-1x"):
            with self.subTest(invalid_name=invalid_name):
                payload = json.loads(path.read_text(encoding="utf-8"))
                payload["m_Name"] = invalid_name
                path.write_text(json.dumps(payload), encoding="utf-8")
                with patch("atlas_builder.core.read_target_metadata", return_value=metadata):
                    with self.assertRaisesRegex(AtlasBuilderError, "name does not match its filename"):
                        prepare_project(self._swordsman_project("source-metadata"))
                payload["m_Name"] = "source-1"
                path.write_text(json.dumps(payload), encoding="utf-8")

    def test_missing_target_fallback_rejects_index_outside_loader_array(self) -> None:
        self._write_swordsman_source((1088,))
        metadata = {"body_swordsman": {FrameKey(0): TargetFrame(
            "body_swordsman-0", 0.5, 0.5, 64, 7, 9
        )}}
        with patch("atlas_builder.core.read_target_metadata", return_value=metadata):
            with self.assertRaisesRegex(AtlasBuilderError, "outside the SHCDE loader's declared range 0–1087"):
                prepare_project(self._swordsman_project("source-metadata"))


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

    def test_schema_one_loads_with_legacy_pivot_and_saves_as_schema_three(self) -> None:
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
            self.assertEqual(json.loads(path.read_text(encoding="utf-8"))["schemaVersion"], 3)
            self.assertEqual(project.loaded_schema_version, 3)

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
            group = ProjectConfig.load(path).groups[0]
            self.assertEqual(group.pivot_mode, "target-pixel-anchor")
            self.assertEqual(group.missing_target_policy, "reject")

    def test_schema_three_roundtrip_preserves_missing_target_policy(self) -> None:
        with tempfile.TemporaryDirectory(dir=TEST_TEMP) as temporary:
            root = Path(temporary)
            path = root / "current.atlas-project.json"
            project = ProjectConfig(groups=[GroupConfig(
                "body_swordsman",
                "images",
                pivot_mode="source-metadata",
                source_metadata_directory="metadata",
                missing_target_policy="source-metadata",
            )])
            project.save(path)
            payload = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(payload["schemaVersion"], 3)
            self.assertEqual(payload["groups"][0]["missingTargetPolicy"], "source-metadata")
            self.assertEqual(ProjectConfig.load(path).groups[0].missing_target_policy, "source-metadata")


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

    def test_pixel_anchor_may_lie_outside_replacement(self) -> None:
        small = SourceFrame(FrameKey(1), self.image, None, 16, 10)
        x, y = output_pivot(self.prepared("target-pixel-anchor"), small)
        self.assertAlmostEqual(x, 2.0)
        self.assertAlmostEqual(y, 1.65, places=4)

    def test_exact_one_pivot_keeps_opposite_edge(self) -> None:
        target = TargetFrame("anim_castle 001", 0.0, 1.0, 64, 183, 183)
        source = SourceFrame(FrameKey(1), self.image, None, 183, 165)
        prepared = PreparedGroup(
            GroupConfig("anim_castle", str(self.root), pivot_mode="target-pixel-anchor"),
            False,
            [source],
            {FrameKey(1): target},
            {},
            [],
        )
        self.assertEqual(output_pivot(prepared, source), (0.0, 1.0))

    def test_negative_target_pivot_remains_valid(self) -> None:
        target = TargetFrame("body_horse_archer_top-1", 0.5, -0.25, 64, 64, 80)
        source = SourceFrame(FrameKey(1), self.image, None, 64, 100)
        prepared = PreparedGroup(
            GroupConfig("body_horse_archer_top", str(self.root), pivot_mode="target-pixel-anchor"),
            True,
            [source],
            {FrameKey(1): target},
            {},
            [],
        )
        self.assertEqual(output_pivot(prepared, source), (0.5, -0.2))

    def test_nonuniform_source_canvas_preserves_source_pixel_anchor(self) -> None:
        metadata = {FrameKey(1): SourceSpriteMetadata(
            "tile_land8 001", 0.5, 0.5, 64, 64, 40, self.root / "frame.json"
        )}
        x, y = output_pivot(self.prepared("source-metadata", metadata), self.source)
        self.assertAlmostEqual(x, 0.5)
        self.assertAlmostEqual(y, 20 / 196)


class SourceMetadataTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(dir=TEST_TEMP)
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_metadata(
        self,
        name: str,
        pivot_y: float = 0.40243897,
        width: float = 64,
        pixels_per_unit: float = 64,
    ) -> Path:
        path = self.root / f"{name}.json"
        path.write_text(json.dumps({
            "m_Name": name,
            "m_Rect": {"m_Width": width, "m_Height": 41},
            "m_Pivot": {"m_X": 0.5, "m_Y": pivot_y},
            "m_PixelsToUnits": pixels_per_unit,
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

    def test_non_finite_source_pivot_is_rejected(self) -> None:
        self.write_metadata("tile_land8 001", float("nan"))
        with self.assertRaisesRegex(AtlasBuilderError, "non-finite pivot"):
            read_source_metadata(self.root, "tile_land8 ", {FrameKey(1)})

    def test_source_pivot_outside_normalized_range_is_allowed(self) -> None:
        self.write_metadata("tile_land8 001", -0.25)
        self.assertEqual(read_source_metadata(self.root, "tile_land8 ", {FrameKey(1)})[FrameKey(1)].pivot_y, -0.25)

    def test_non_finite_source_rect_and_ppu_are_rejected(self) -> None:
        for width, ppu, message in (
            (float("nan"), 64, "invalid Sprite rectangle"),
            (64, float("inf"), "invalid pixels-per-unit"),
        ):
            with self.subTest(message=message):
                self.write_metadata("tile_land8 001", width=width, pixels_per_unit=ppu)
                with self.assertRaisesRegex(AtlasBuilderError, message):
                    read_source_metadata(self.root, "tile_land8 ", {FrameKey(1)})


class GroupContractTests(unittest.TestCase):
    def test_current_contract_counts(self) -> None:
        self.assertEqual(len(GROUP_CONTRACTS), 195)
        self.assertEqual(GROUP_CONTRACTS["body_swordsman"].maximum_frame_index, 1087)
        self.assertEqual(
            {name for name, group in GROUP_CONTRACTS.items() if not group.overridable_as_atlas},
            set(),
        )
        self.assertEqual(sum(group.material == "plain" for group in GROUP_CONTRACTS.values()), 69)
        self.assertEqual(sum(group.material == "foliage" for group in GROUP_CONTRACTS.values()), 7)

    def test_shared_sea_groups_are_supported_by_extender_2_4_contract(self) -> None:
        self.assertTrue(GROUP_CONTRACTS["tile_sea_new_01"].overridable_as_atlas)
        self.assertTrue(GROUP_CONTRACTS["tile_sea_shore"].overridable_as_atlas)

    def test_all_extender_material_names_are_emitted_exactly(self) -> None:
        self.assertEqual(atlas_material_name(GROUP_CONTRACTS["tile_ruins"]), "Plain")
        self.assertEqual(atlas_material_name(GROUP_CONTRACTS["body_archer"]), "TeamColour")
        self.assertEqual(atlas_material_name(GROUP_CONTRACTS["tree_apple"]), "Foliage")

    def test_plain_group_rejects_mask_atlas(self) -> None:
        project = ProjectConfig(
            target_game_data="Data",
            output_mod_directory="Mod",
            groups=[GroupConfig("tile_ruins", "images", "same-directory")],
        )
        with self.assertRaisesRegex(AtlasBuilderError, "maskenlose Plain-Material"):
            prepare_project(project)

    def test_teamcolour_group_requires_mask_atlas(self) -> None:
        project = ProjectConfig(
            target_game_data="Data",
            output_mod_directory="Mod",
            groups=[GroupConfig("body_archer", "images")],
        )
        with self.assertRaisesRegex(AtlasBuilderError, "verwendet im Spiel eine Maske"):
            prepare_project(project)


if __name__ == "__main__":
    unittest.main()
