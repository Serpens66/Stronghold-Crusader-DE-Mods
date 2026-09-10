from __future__ import annotations

import json
import math
import os
import re
import shutil
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

import UnityPy
from PIL import Image, ImageChops

from .gm_groups import GROUP_CONTRACTS, SUPPORTED_GROUPS
from .i18n import translate
from .models import GroupConfig, MASK_MODES, MISSING_TARGET_POLICIES, PIVOT_MODES, ProjectConfig


ProgressCallback = Callable[[str], None]
CORNER_PIVOT_EPSILON = 1e-6
ANCHOR_TOLERANCE_PX = 1e-4


class AtlasBuilderError(RuntimeError):
    pass


@dataclass(frozen=True, order=True)
class FrameKey:
    index: int
    alternate: bool = False


@dataclass(frozen=True)
class TargetFrame:
    name: str
    pivot_x: float
    pivot_y: float
    pixels_per_unit: float
    width: float
    height: float


@dataclass(frozen=True)
class SourceSpriteMetadata:
    name: str
    pivot_x: float
    pivot_y: float
    pixels_per_unit: float
    width: float
    height: float
    path: Path


@dataclass(frozen=True)
class SourceFrame:
    key: FrameKey
    colour_path: Path
    mask_path: Path | None
    width: int
    height: int


@dataclass
class PreparedGroup:
    config: GroupConfig
    dash_format: bool
    source_frames: list[SourceFrame]
    target_frames: dict[FrameKey, TargetFrame]
    source_metadata: dict[FrameKey, SourceSpriteMetadata]
    warnings: list[str]


@dataclass
class BuildResult:
    output_mod_directory: Path
    groups: list[str]
    colour_frames: int
    mask_frames: int
    warnings: list[str]


@dataclass(frozen=True)
class FrameCoverage:
    shared: tuple[FrameKey, ...]
    source_only: tuple[FrameKey, ...]
    target_only: tuple[FrameKey, ...]
    source_maximum: str
    target_maximum: str


def _progress(callback: ProgressCallback | None, message: str) -> None:
    if callback:
        callback(message)


def _parse_source_stem(stem: str, configured_prefix: str) -> tuple[str, FrameKey] | None:
    match = re.fullmatch(r"(.*?)(\d+)(x?)", stem, flags=re.IGNORECASE)
    if not match:
        return None
    prefix = match.group(1)
    if configured_prefix != "auto" and prefix.casefold() != configured_prefix.casefold():
        return None
    return prefix, FrameKey(int(match.group(2)), bool(match.group(3)))


def _collect_pngs(
    directory: Path,
    configured_prefix: str,
    kind: str,
) -> tuple[dict[FrameKey, Path], str]:
    if not directory.is_dir():
        raise AtlasBuilderError(f"Source directory does not exist: {directory}")
    found: dict[FrameKey, Path] = {}
    prefixes: dict[str, str] = {}
    for path in sorted(directory.glob("*.png"), key=lambda item: item.name.casefold()):
        stem = path.stem
        is_mask = stem.casefold().endswith("_m")
        if kind == "colour" and is_mask:
            continue
        if kind == "same-directory-mask" and not is_mask:
            continue
        if is_mask:
            stem = stem[:-2]
        parsed = _parse_source_stem(stem, configured_prefix)
        if parsed is None:
            continue
        prefix, key = parsed
        if key in found:
            raise AtlasBuilderError(f"Duplicate frame index {key.index}{'x' if key.alternate else ''}: {path}")
        prefixes.setdefault(prefix.casefold(), prefix)
        found[key] = path
    if not found:
        label = "mask" if "mask" in kind else "colour"
        raise AtlasBuilderError(f"No matching {label} PNG files found in: {directory}")
    if configured_prefix == "auto" and len(prefixes) != 1:
        raise AtlasBuilderError(
            f"Automatic source-prefix detection is ambiguous in {directory}: {sorted(prefixes.values())!r}"
        )
    return found, next(iter(prefixes.values()))


def discover_source_group(project: ProjectConfig, config: GroupConfig) -> tuple[list[SourceFrame], str]:
    if config.mask_mode not in MASK_MODES:
        raise AtlasBuilderError(f"Unsupported maskMode for {config.gm_file_name}: {config.mask_mode}")
    colour_directory = project.resolve_path(config.colour_directory)
    colours, detected_prefix = _collect_pngs(colour_directory, config.source_prefix, "colour")

    masks: dict[FrameKey, Path] = {}
    if config.mask_mode == "same-directory":
        masks, _ = _collect_pngs(colour_directory, detected_prefix, "same-directory-mask")
    elif config.mask_mode == "separate-directory":
        if not config.mask_directory:
            raise AtlasBuilderError(f"{config.gm_file_name}: maskDirectory is required")
        # A dedicated mask directory is unambiguous, so both "12.png" and "12_m.png" are accepted.
        masks, _ = _collect_pngs(project.resolve_path(config.mask_directory), config.source_prefix, "separate-mask")

    if config.mask_mode != "none" and set(masks) != set(colours):
        missing = sorted(set(colours) - set(masks))
        extra = sorted(set(masks) - set(colours))
        raise AtlasBuilderError(
            f"{config.gm_file_name}: colour/mask frame sets differ; missing masks={missing}, extra masks={extra}"
        )

    result: list[SourceFrame] = []
    for key in sorted(colours):
        colour_path = colours[key]
        try:
            with Image.open(colour_path) as image:
                width, height = image.size
                image.verify()
        except Exception as exc:
            raise AtlasBuilderError(f"Invalid colour PNG: {colour_path}: {exc}") from exc
        mask_path = masks.get(key)
        if mask_path:
            try:
                with Image.open(mask_path) as mask:
                    mask_size = mask.size
                    mask.verify()
            except Exception as exc:
                raise AtlasBuilderError(f"Invalid mask PNG: {mask_path}: {exc}") from exc
            if mask_size != (width, height):
                raise AtlasBuilderError(
                    f"Frame {key.index}{'x' if key.alternate else ''}: mask is {mask_size[0]}x{mask_size[1]}, "
                    f"colour is {width}x{height}"
                )
        result.append(SourceFrame(key, colour_path, mask_path, width, height))
    return result, detected_prefix


def read_source_metadata(
    directory: Path,
    configured_prefix: str,
    required_keys: set[FrameKey],
) -> dict[FrameKey, SourceSpriteMetadata]:
    if not directory.is_dir():
        raise AtlasBuilderError(f"Source metadata directory does not exist: {directory}")
    found: dict[FrameKey, SourceSpriteMetadata] = {}
    for path in directory.rglob("*.json"):
        parsed_filename = _parse_source_stem(path.stem, configured_prefix)
        if parsed_filename is None or parsed_filename[1] not in required_keys:
            continue
        try:
            payload = json.loads(path.read_text(encoding="utf-8-sig"))
            name = str(payload["m_Name"])
            parsed_name = _parse_source_stem(name, configured_prefix)
            rect = payload["m_Rect"]
            pivot = payload["m_Pivot"]
            metadata = SourceSpriteMetadata(
                name=name,
                pivot_x=float(pivot["m_X"]),
                pivot_y=float(pivot["m_Y"]),
                pixels_per_unit=float(payload["m_PixelsToUnits"]),
                width=float(rect["m_Width"]),
                height=float(rect["m_Height"]),
                path=path,
            )
        except (KeyError, TypeError, ValueError, json.JSONDecodeError) as exc:
            raise AtlasBuilderError(f"Invalid AssetRipper Sprite metadata: {path}: {exc}") from exc
        if parsed_name is None or parsed_name[1] != parsed_filename[1]:
            raise AtlasBuilderError(f"AssetRipper metadata name does not match its filename: {path}")
        key = parsed_name[1]
        if key in found:
            raise AtlasBuilderError(
                f"Duplicate source metadata for frame {key.index}{'x' if key.alternate else ''}: "
                f"{found[key].path} and {path}"
            )
        if not math.isfinite(metadata.width) or not math.isfinite(metadata.height) or (
            metadata.width <= 0 or metadata.height <= 0
        ):
            raise AtlasBuilderError(f"Source metadata has an invalid Sprite rectangle: {path}")
        if not math.isfinite(metadata.pivot_x) or not math.isfinite(metadata.pivot_y):
            raise AtlasBuilderError(f"Source metadata has a non-finite pivot: {path}")
        if not math.isfinite(metadata.pixels_per_unit) or metadata.pixels_per_unit <= 0:
            raise AtlasBuilderError(f"Source metadata has invalid pixels-per-unit: {path}")
        found[key] = metadata
    missing = sorted(required_keys - set(found))
    if missing:
        raise AtlasBuilderError(f"Missing source metadata for frames: {missing}")
    return found


def _target_key(name: str, gm_file_name: str, dash_format: bool) -> FrameKey | None:
    delimiter = "-" if dash_format else r"\s+"
    match = re.fullmatch(re.escape(gm_file_name) + delimiter + r"(\d+)(x?)", name, flags=re.IGNORECASE)
    if not match:
        return None
    return FrameKey(int(match.group(1)), bool(match.group(2)))


def _split_target_name(name: str) -> tuple[str, bool, FrameKey] | None:
    match = re.search(r"(\d+)(x?)$", name, flags=re.IGNORECASE)
    if match is None or match.start() == 0:
        return None
    delimiter = name[match.start() - 1]
    if delimiter not in "- ":
        return None
    return name[: match.start() - 1], delimiter == "-", FrameKey(
        int(match.group(1)), bool(match.group(2))
    )


def _target_sprite_name(gm_file_name: str, dash_format: bool, key: FrameKey) -> str:
    delimiter = "-" if dash_format else " "
    suffix = "x" if key.alternate else ""
    return f"{gm_file_name}{delimiter}{key.index}{suffix}"


def _format_frame_keys(keys: Iterable[FrameKey]) -> str:
    parts: list[str] = []
    for alternate in (False, True):
        indices = sorted({key.index for key in keys if key.alternate == alternate})
        start = previous = None
        for index in indices + [None]:
            if start is None:
                start = previous = index
                continue
            if index is not None and index == previous + 1:
                previous = index
                continue
            suffix = "x" if alternate else ""
            parts.append(f"{start}{suffix}" if start == previous else f"{start}{suffix}–{previous}{suffix}")
            start = previous = index
    return ", ".join(parts)


def _format_frame_maximum(keys: Iterable[FrameKey]) -> str:
    values = set(keys)
    parts: list[str] = []
    for alternate in (False, True):
        indices = [key.index for key in values if key.alternate == alternate]
        if indices:
            parts.append(f"{max(indices)}{'x' if alternate else ''}")
    return ", ".join(parts) if parts else "-"


def calculate_frame_coverage(
    source_keys: Iterable[FrameKey], target_keys: Iterable[FrameKey]
) -> FrameCoverage:
    source = set(source_keys)
    target = set(target_keys)
    return FrameCoverage(
        shared=tuple(sorted(source & target)),
        source_only=tuple(sorted(source - target)),
        target_only=tuple(sorted(target - source)),
        source_maximum=_format_frame_maximum(source),
        target_maximum=_format_frame_maximum(target),
    )


def read_target_metadata(
    game_data_directory: Path,
    requested_groups: dict[str, bool],
    required_keys: dict[str, set[FrameKey]] | None = None,
    language: str = "en",
    callback: ProgressCallback | None = None,
) -> dict[str, dict[FrameKey, TargetFrame]]:
    if not game_data_directory.is_dir():
        raise AtlasBuilderError(f"SHCDE data directory does not exist: {game_data_directory}")
    primary = game_data_directory / "resources.assets"
    asset_files = ([primary] if primary.is_file() else []) + [
        path for path in sorted(game_data_directory.glob("*.assets")) if path != primary
    ]
    if not asset_files:
        raise AtlasBuilderError(f"No Unity .assets files found in: {game_data_directory}")

    result: dict[str, dict[FrameKey, TargetFrame]] = {name: {} for name in requested_groups}
    requested_lookup = {name.casefold(): name for name in requested_groups}
    required_keys = required_keys or {name: set() for name in requested_groups}
    remaining = set(requested_groups)
    for asset_path in asset_files:
        if asset_path != primary and not remaining:
            break
        _progress(callback, translate(language, "progress_scanning", file=asset_path.name))
        try:
            environment = UnityPy.load(str(asset_path))
        except Exception as exc:
            raise AtlasBuilderError(f"Cannot read Unity asset file {asset_path}: {exc}") from exc
        for obj in environment.objects:
            if obj.type.name != "Sprite":
                continue
            try:
                data = obj.read()
                name = getattr(data, "m_Name", getattr(data, "name", ""))
            except Exception:
                continue
            split = _split_target_name(name)
            if split is None:
                continue
            parsed_group, dash_format, key = split
            group_name = requested_lookup.get(parsed_group.casefold())
            if group_name is None or requested_groups[group_name] != dash_format:
                continue
            target = TargetFrame(
                name=name,
                pivot_x=float(data.m_Pivot.x),
                pivot_y=float(data.m_Pivot.y),
                pixels_per_unit=float(data.m_PixelsToUnits),
                width=float(data.m_Rect.width),
                height=float(data.m_Rect.height),
            )
            existing = result[group_name].get(key)
            if existing and existing != target:
                raise AtlasBuilderError(f"Conflicting target metadata for sprite: {name}")
            result[group_name][key] = target
        remaining = {
            name
            for name, frames in result.items()
            if not frames or not required_keys.get(name, set()).issubset(frames)
        }

    missing_groups = sorted(name for name, frames in result.items() if not frames)
    if missing_groups:
        raise AtlasBuilderError(f"No target Sprite metadata found for: {', '.join(missing_groups)}")
    return result


def prepare_project(project: ProjectConfig, callback: ProgressCallback | None = None) -> list[PreparedGroup]:
    if not project.target_game_data.strip():
        raise AtlasBuilderError("targetGameData is required")
    if not project.output_mod_directory.strip():
        raise AtlasBuilderError("outputModDirectory is required")
    if not project.groups:
        raise AtlasBuilderError("The project does not contain any GM groups")
    if project.packing.gap < 2:
        raise AtlasBuilderError("Packing gap must be at least 2 pixels")
    if project.packing.maximum_texture_size < 64 or project.packing.maximum_texture_size > 8192:
        raise AtlasBuilderError("maximumTextureSize must be between 64 and 8192")

    names = [group.gm_file_name for group in project.groups]
    if len({name.casefold() for name in names}) != len(names):
        raise AtlasBuilderError("Each GM group may only occur once in a project")

    discovered: dict[str, list[SourceFrame]] = {}
    detected_prefixes: dict[str, str] = {}
    requested: dict[str, bool] = {}
    for config in project.groups:
        if not config.colour_directory.strip():
            raise AtlasBuilderError(f"{config.gm_file_name}: colourDirectory is required")
        if not config.source_prefix:
            raise AtlasBuilderError(f"{config.gm_file_name}: sourcePrefix must be 'auto' or an explicit prefix")
        if config.pivot_mode not in PIVOT_MODES:
            raise AtlasBuilderError(f"Unsupported pivotMode for {config.gm_file_name}: {config.pivot_mode}")
        if config.missing_target_policy not in MISSING_TARGET_POLICIES:
            raise AtlasBuilderError(
                f"Unsupported missingTargetPolicy for {config.gm_file_name}: {config.missing_target_policy}"
            )
        if config.missing_target_policy == "source-metadata" and config.pivot_mode != "source-metadata":
            raise AtlasBuilderError(translate(project.language, "missing_target_requires_source_metadata", group=config.gm_file_name))
        if config.pivot_mode == "source-metadata" and not config.source_metadata_directory:
            raise AtlasBuilderError(f"{config.gm_file_name}: sourceMetadataDirectory is required")
        canonical = next((name for name in SUPPORTED_GROUPS if name.casefold() == config.gm_file_name.casefold()), None)
        if canonical is None:
            raise AtlasBuilderError(f"Unknown Script Extender 2.3.0 GM group: {config.gm_file_name}")
        config.gm_file_name = canonical
        contract = GROUP_CONTRACTS[canonical]
        if not contract.overridable_as_atlas:
            raise AtlasBuilderError(translate(project.language, "unsafe_atlas_group", group=canonical))
        has_mask = config.mask_mode != "none"
        if contract.mask_policy == "forbidden" and has_mask:
            raise AtlasBuilderError(translate(project.language, "mask_forbidden", group=canonical))
        if contract.mask_policy == "required" and not has_mask:
            raise AtlasBuilderError(translate(project.language, "mask_required", group=canonical))
        requested[canonical] = SUPPORTED_GROUPS[canonical]
        _progress(callback, translate(project.language, "progress_checking", group=canonical))
        discovered[canonical], detected_prefixes[canonical] = discover_source_group(project, config)

    required_keys = {name: {source.key for source in sources} for name, sources in discovered.items()}
    metadata = read_target_metadata(
        project.resolve_path(project.target_game_data), requested, required_keys, project.language, callback
    )
    prepared: list[PreparedGroup] = []
    for config in project.groups:
        group_name = config.gm_file_name
        sources = discovered[group_name]
        targets = metadata[group_name]
        warnings: list[str] = []
        coverage = calculate_frame_coverage((source.key for source in sources), targets)
        coverage_message = ""
        if coverage.source_only or coverage.target_only:
            coverage_message = translate(
                project.language,
                "coverage_summary",
                group=group_name,
                shared=len(coverage.shared),
                source_only_count=len(coverage.source_only),
                source_only=_format_frame_keys(coverage.source_only) or translate(project.language, "none_indices"),
                target_only_count=len(coverage.target_only),
                target_only=_format_frame_keys(coverage.target_only) or translate(project.language, "none_indices"),
                source_maximum=coverage.source_maximum,
                target_maximum=coverage.target_maximum,
            )
            warnings.append(coverage_message)
        truncation_warnings: list[str] = []
        for alternate in (False, True):
            source_indices = [item.key.index for item in sources if item.key.alternate == alternate]
            target_indices = [key.index for key in targets if key.alternate == alternate]
            if source_indices and target_indices and max(target_indices) > max(source_indices):
                suffix = "x" if alternate else ""
                truncated = [
                    FrameKey(index, alternate)
                    for index in target_indices
                    if index > max(source_indices)
                ]
                truncation_warnings.append(translate(
                    project.language,
                    "partial_warning",
                    group=f"{group_name}{suffix}",
                    target=max(target_indices),
                    source=max(source_indices),
                    indices=_format_frame_keys(truncated),
                ))
        warnings.extend(truncation_warnings)
        if GROUP_CONTRACTS[group_name].material == "foliage":
            warnings.append(translate(project.language, "foliage_warning", group=group_name))
        source_metadata: dict[FrameKey, SourceSpriteMetadata] = {}
        if config.pivot_mode == "source-metadata":
            source_metadata = read_source_metadata(
                project.resolve_path(config.source_metadata_directory or ""),
                detected_prefixes[group_name],
                {source.key for source in sources},
            )
            aspect_mismatches: list[str] = []
            for source in sources:
                item = source_metadata[source.key]
                source_ratio = source.width / source.height
                metadata_ratio = item.width / item.height
                if abs(source_ratio - metadata_ratio) > 0.0001:
                    aspect_mismatches.append(
                        f"{item.name} ({source.width}x{source.height} vs {item.width:g}x{item.height:g})"
                    )
            if aspect_mismatches:
                examples = ", ".join(aspect_mismatches[:5])
                if len(aspect_mismatches) > 5:
                    examples += ", ..."
                warnings.append(translate(
                    project.language,
                    "metadata_aspect_warning",
                    count=len(aspect_mismatches),
                    examples=examples,
                ))
        unknown = sorted(source.key for source in sources if source.key not in targets)
        if unknown:
            if config.missing_target_policy == "reject":
                rejection = translate(
                    project.language,
                    "source_only_rejected",
                    group=group_name,
                    count=len(unknown),
                    indices=_format_frame_keys(unknown),
                )
                diagnostics = [rejection]
                if coverage_message:
                    diagnostics.append(coverage_message)
                diagnostics.extend(truncation_warnings)
                raise AtlasBuilderError("\n".join(diagnostics))
            contract = GROUP_CONTRACTS[group_name]
            outside_loader_range = [key for key in unknown if key.index > contract.maximum_frame_index]
            if outside_loader_range:
                raise AtlasBuilderError(translate(
                    project.language,
                    "missing_target_outside_loader_range",
                    group=group_name,
                    indices=_format_frame_keys(outside_loader_range),
                    maximum=contract.maximum_frame_index,
                ))
            # The game loader can intentionally leave entries null inside its
            # declared array. Source metadata supplies geometry for those
            # slots, but the atlas name must remain in the target namespace.
            for key in unknown:
                item = source_metadata[key]
                targets[key] = TargetFrame(
                    name=_target_sprite_name(group_name, requested[group_name], key),
                    pivot_x=item.pivot_x,
                    pivot_y=item.pivot_y,
                    pixels_per_unit=item.pixels_per_unit,
                    width=item.width,
                    height=item.height,
                )
            warnings.append(translate(
                project.language,
                "missing_target_source_warning",
                group=group_name,
                count=len(unknown),
                indices=_format_frame_keys(unknown),
            ))
        missing_keys = sorted(set(targets) - {source.key for source in sources})
        if missing_keys:
            warnings.append(translate(
                project.language,
                "incomplete_group_warning",
                group=group_name,
                count=len(missing_keys),
                indices=_format_frame_keys(missing_keys),
            ))
        prepared.append(PreparedGroup(config, requested[group_name], sources, targets, source_metadata, warnings))
    return prepared


def output_pivot(prepared: PreparedGroup, source: SourceFrame) -> tuple[float, float]:
    target = prepared.target_frames[source.key]
    mode = prepared.config.pivot_mode
    if mode == "target-normalized":
        return target.pivot_x, target.pivot_y
    if mode == "source-metadata":
        metadata = prepared.source_metadata[source.key]
        scale_x = source.width / metadata.width
        scale_y = source.height / metadata.height
        # Uniformly scaled images keep their normalized source pivot. If only
        # the canvas changed, preserve the source sprite's absolute anchor.
        if abs(scale_x - scale_y) <= 0.0001:
            return metadata.pivot_x, metadata.pivot_y
        return (
            _reanchored_pivot(metadata.pivot_x, metadata.width, source.width),
            _reanchored_pivot(metadata.pivot_y, metadata.height, source.height),
        )
    if target.width <= 0 or target.height <= 0:
        raise AtlasBuilderError(f"{target.name}: target Sprite rectangle must be positive")
    return (
        _reanchored_pivot(target.pivot_x, target.width, source.width),
        _reanchored_pivot(target.pivot_y, target.height, source.height),
    )


def _reanchored_pivot(reference_pivot: float, reference_size: float, own_size: float) -> float:
    if reference_size <= 0 or own_size <= 0:
        raise AtlasBuilderError("Sprite dimensions must be positive for pivot conversion")
    if not math.isfinite(reference_pivot):
        raise AtlasBuilderError("Sprite pivot must be finite")
    # A pivot of exactly one denotes the opposite edge. Its distance from that
    # edge is zero, so it remains one on a differently sized canvas.
    if abs(reference_pivot - 1.0) <= CORNER_PIVOT_EPSILON:
        return 1.0
    return reference_pivot * reference_size / own_size


def _shelf_pack(frames: list[SourceFrame], width: int, gap: int, margin: int, limit: int):
    placements: dict[FrameKey, tuple[int, int, int, int]] = {}
    x = margin
    y = margin
    row_height = 0
    for frame in frames:
        if frame.width + 2 * margin > width:
            return None
        if x != margin and x + frame.width + margin > width:
            x = margin
            y += row_height + gap
            row_height = 0
        placements[frame.key] = (x, y, frame.width, frame.height)
        x += frame.width + gap
        row_height = max(row_height, frame.height)
    height = y + row_height + margin
    return (placements, height) if height <= limit else None


def choose_layout(frames: list[SourceFrame], gap: int, limit: int):
    margin = gap
    widths = []
    width = 64
    while width <= limit:
        widths.append(width)
        width *= 2
    if not widths or widths[-1] != limit:
        widths.append(limit)
    candidates = []
    for candidate_width in widths:
        packed = _shelf_pack(frames, candidate_width, gap, margin, limit)
        if packed:
            placements, height = packed
            candidates.append((candidate_width * height, candidate_width, height, placements))
    if not candidates:
        raise AtlasBuilderError(f"Frames do not fit into a {limit}x{limit} atlas")
    _area, width, height, placements = min(candidates, key=lambda item: (item[0], item[1]))
    return width, height, placements


def _write_crlf_json(path: Path, payload: object) -> None:
    text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    path.write_bytes(text.replace("\n", "\r\n").encode("utf-8"))


def build_group(prepared: PreparedGroup, output_directory: Path, project: ProjectConfig) -> None:
    gap = project.packing.gap
    limit = project.packing.maximum_texture_size
    width, height, placements = choose_layout(prepared.source_frames, gap, limit)
    output_directory.mkdir(parents=True, exist_ok=False)
    colour_atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    has_masks = prepared.config.mask_mode != "none"
    mask_atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0)) if has_masks else None
    frames_json = []

    for source in prepared.source_frames:
        x, top_y, frame_width, frame_height = placements[source.key]
        with Image.open(source.colour_path) as image:
            colour_atlas.paste(image.convert("RGBA"), (x, top_y))
        if mask_atlas is not None and source.mask_path is not None:
            with Image.open(source.mask_path) as image:
                mask_atlas.paste(image.convert("RGBA"), (x, top_y))
        target = prepared.target_frames[source.key]
        pivot_x, pivot_y = output_pivot(prepared, source)
        frame = {
            "name": target.name,
            "rect": {"x": x, "y": height - top_y - frame_height, "w": frame_width, "h": frame_height},
            "pivot": {"x": pivot_x, "y": pivot_y},
        }
        if target.pixels_per_unit <= 0:
            raise AtlasBuilderError(f"{target.name}: target pixels-per-unit must be positive")
        if target.pixels_per_unit != 64.0:
            frame["pixelsPerUnit"] = target.pixels_per_unit
        frames_json.append(frame)

    colour_atlas.save(output_directory / "atlas.png", format="PNG", optimize=True)
    if mask_atlas is not None:
        mask_atlas.save(output_directory / "atlas_m.png", format="PNG", optimize=True)
    _write_crlf_json(output_directory / "atlas.json", {"pixelsPerUnit": 64, "frames": frames_json})


def validate_generated_group(prepared: PreparedGroup, directory: Path, project: ProjectConfig) -> None:
    expected = {"atlas.png", "atlas.json"}
    if prepared.config.mask_mode != "none":
        expected.add("atlas_m.png")
    actual = {path.name for path in directory.iterdir() if path.is_file()}
    if actual != expected:
        raise AtlasBuilderError(f"{prepared.config.gm_file_name}: generated file set is invalid: {actual}")
    raw_json = (directory / "atlas.json").read_bytes()
    if b"\n" in raw_json.replace(b"\r\n", b""):
        raise AtlasBuilderError(f"{prepared.config.gm_file_name}: atlas.json is not CRLF")
    payload = json.loads(raw_json.decode("utf-8"))
    frames = payload.get("frames", [])
    if len(frames) != len(prepared.source_frames):
        raise AtlasBuilderError(f"{prepared.config.gm_file_name}: generated frame count differs")

    with Image.open(directory / "atlas.png") as image:
        atlas = image.convert("RGBA")
    mask_atlas = None
    if prepared.config.mask_mode != "none":
        with Image.open(directory / "atlas_m.png") as image:
            mask_atlas = image.convert("RGBA")
        if mask_atlas.size != atlas.size:
            raise AtlasBuilderError(f"{prepared.config.gm_file_name}: atlas dimensions differ")

    rectangles = []
    for source, frame in zip(prepared.source_frames, frames):
        target = prepared.target_frames[source.key]
        if frame.get("name") != target.name:
            raise AtlasBuilderError(f"{prepared.config.gm_file_name}: generated name mismatch")
        actual_ppu = float(frame.get("pixelsPerUnit", payload.get("pixelsPerUnit", 64)))
        if abs(actual_ppu - target.pixels_per_unit) > 0.0001:
            raise AtlasBuilderError(f"{target.name}: generated pixels-per-unit differs")
        expected_pivot = output_pivot(prepared, source)
        actual_pivot = frame.get("pivot", {})
        pivot_error_x = abs(float(actual_pivot.get("x", math.nan)) - expected_pivot[0]) * source.width
        pivot_error_y = abs(float(actual_pivot.get("y", math.nan)) - expected_pivot[1]) * source.height
        if not math.isfinite(pivot_error_x) or not math.isfinite(pivot_error_y) or (
            pivot_error_x > ANCHOR_TOLERANCE_PX or pivot_error_y > ANCHOR_TOLERANCE_PX
        ):
            raise AtlasBuilderError(f"{target.name}: generated pivot differs")
        rect = frame["rect"]
        x, y, width, height = (rect[key] for key in ("x", "y", "w", "h"))
        if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > atlas.width or y + height > atlas.height:
            raise AtlasBuilderError(f"{target.name}: generated rectangle is invalid")
        if not math.isfinite(float(frame["pivot"]["x"])) or not math.isfinite(float(frame["pivot"]["y"])):
            raise AtlasBuilderError(f"{target.name}: generated pivot is not finite")
        top_y = atlas.height - y - height
        with Image.open(source.colour_path) as image:
            original = image.convert("RGBA")
        if ImageChops.difference(original, atlas.crop((x, top_y, x + width, top_y + height))).getbbox():
            raise AtlasBuilderError(f"{target.name}: generated atlas pixels differ")
        if mask_atlas is not None and source.mask_path is not None:
            with Image.open(source.mask_path) as image:
                original_mask = image.convert("RGBA")
            if ImageChops.difference(original_mask, mask_atlas.crop((x, top_y, x + width, top_y + height))).getbbox():
                raise AtlasBuilderError(f"{target.name}: generated mask pixels differ")
        rectangles.append((x, y, width, height))

    occupied = Image.new("1", atlas.size, 0)
    for x, y, width, height in rectangles:
        top_y = atlas.height - y - height
        occupied.paste(1, (x, top_y, x + width, top_y + height))
    transparent = Image.new("RGBA", atlas.size, (0, 0, 0, 0))
    if ImageChops.difference(
        Image.composite(transparent, atlas, occupied), transparent
    ).getbbox():
        raise AtlasBuilderError(f"{prepared.config.gm_file_name}: pixels outside frame rectangles are not transparent")

    gap = project.packing.gap
    for position, left in enumerate(rectangles):
        lx, ly, lw, lh = left
        for rx, ry, rw, rh in rectangles[position + 1 :]:
            horizontal = max(rx - (lx + lw), lx - (rx + rw))
            vertical = max(ry - (ly + lh), ly - (ry + rh))
            if horizontal < gap and vertical < gap:
                raise AtlasBuilderError(f"{prepared.config.gm_file_name}: frames overlap or violate the gap")


def existing_output_groups(project: ProjectConfig) -> list[str]:
    atlas_root = project.resolve_path(project.output_mod_directory) / "Override" / "Atlas"
    return [group.gm_file_name for group in project.groups if (atlas_root / group.gm_file_name).exists()]


def _remove_path(path: Path) -> None:
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def _create_manifest_if_needed(project: ProjectConfig, output_mod: Path) -> None:
    path = output_mod / "info.json"
    if path.exists() or not project.create_manifest_if_missing:
        return
    required = {
        "GUID": project.mod_info.guid,
        "Name": project.mod_info.name,
        "Version": project.mod_info.version,
    }
    missing = [name for name, value in required.items() if not value.strip()]
    if missing:
        raise AtlasBuilderError(f"Cannot create info.json; empty fields: {', '.join(missing)}")
    _write_crlf_json(path, project.mod_info.to_info_dict())


def build_project(
    project: ProjectConfig,
    overwrite_existing: bool = False,
    callback: ProgressCallback | None = None,
) -> BuildResult:
    prepared_groups = prepare_project(project, callback)
    output_mod = project.resolve_path(project.output_mod_directory)
    existing = existing_output_groups(project)
    if existing and not overwrite_existing:
        raise AtlasBuilderError(f"Output groups already exist: {', '.join(existing)}")
    output_mod.parent.mkdir(parents=True, exist_ok=True)

    warnings = [warning for group in prepared_groups for warning in group.warnings]
    with tempfile.TemporaryDirectory(prefix="AtlasBuilder-", dir=output_mod.parent) as temporary:
        staged_root = Path(temporary)
        staged_atlas = staged_root / "Override" / "Atlas"
        for prepared in prepared_groups:
            _progress(callback, translate(project.language, "progress_building", group=prepared.config.gm_file_name))
            group_directory = staged_atlas / prepared.config.gm_file_name
            build_group(prepared, group_directory, project)
            validate_generated_group(prepared, group_directory, project)

        output_mod.mkdir(parents=True, exist_ok=True)
        atlas_root = output_mod / "Override" / "Atlas"
        atlas_root.mkdir(parents=True, exist_ok=True)
        backups: list[tuple[Path, Path]] = []
        installed: list[Path] = []
        try:
            for prepared in prepared_groups:
                name = prepared.config.gm_file_name
                source = staged_atlas / name
                destination = atlas_root / name
                if destination.exists():
                    backup = atlas_root / f".AtlasBuilder-backup-{name}-{uuid.uuid4().hex}"
                    os.replace(destination, backup)
                    backups.append((destination, backup))
                os.replace(source, destination)
                installed.append(destination)
            _create_manifest_if_needed(project, output_mod)
        except Exception:
            for destination in reversed(installed):
                _remove_path(destination)
            for destination, backup in reversed(backups):
                if backup.exists():
                    os.replace(backup, destination)
            raise
        for _destination, backup in backups:
            _remove_path(backup)

    colour_frames = sum(len(group.source_frames) for group in prepared_groups)
    mask_frames = sum(
        len(group.source_frames) for group in prepared_groups if group.config.mask_mode != "none"
    )
    _progress(callback, translate(project.language, "progress_completed"))
    return BuildResult(output_mod, [group.config.gm_file_name for group in prepared_groups], colour_frames, mask_frames, warnings)
