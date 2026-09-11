from __future__ import annotations

import json
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any


SCHEMA_VERSION = 5
SUPPORTED_SCHEMA_VERSIONS = {1, 2, 3, 4, SCHEMA_VERSION}
MASK_MODES = {"none", "same-directory", "separate-directory"}
PIVOT_MODES = {"target-pixel-anchor", "source-metadata", "target-normalized"}
MISSING_TARGET_POLICIES = {"reject", "source-metadata"}
MISSING_SOURCE_METADATA_POLICIES = {"reject", "target-pixel-anchor"}


@dataclass
class PackingConfig:
    gap: int = 2
    maximum_texture_size: int = 8192

    @classmethod
    def from_dict(cls, data: dict[str, Any] | None) -> "PackingConfig":
        data = data or {}
        return cls(
            gap=int(data.get("gap", 2)),
            maximum_texture_size=int(data.get("maximumTextureSize", 8192)),
        )

    def to_dict(self) -> dict[str, Any]:
        return {"gap": self.gap, "maximumTextureSize": self.maximum_texture_size}


@dataclass
class GroupConfig:
    gm_file_name: str
    colour_directory: str
    mask_mode: str = "none"
    mask_directory: str | None = None
    source_prefix: str = "auto"
    pivot_mode: str = "target-pixel-anchor"
    source_metadata_directory: str | None = None
    missing_target_policy: str = "reject"
    missing_source_metadata_policy: str = "reject"
    source_frame_filter: str = ""

    @classmethod
    def from_dict(cls, data: dict[str, Any], schema_version: int = SCHEMA_VERSION) -> "GroupConfig":
        return cls(
            gm_file_name=str(data["gmFileName"]),
            colour_directory=str(data["colourDirectory"]),
            mask_mode=str(data.get("maskMode", "none")),
            mask_directory=data.get("maskDirectory"),
            source_prefix=str(data.get("sourcePrefix", "auto")),
            # Schema 1 implicitly copied the normalized target pivot.
            pivot_mode=str(data.get("pivotMode", "target-normalized" if schema_version == 1 else "target-pixel-anchor")),
            source_metadata_directory=data.get("sourceMetadataDirectory"),
            missing_target_policy=str(data.get("missingTargetPolicy", "reject")),
            missing_source_metadata_policy=str(data.get("missingSourceMetadataPolicy", "reject")),
            source_frame_filter=str(data.get("sourceFrameFilter", "") or ""),
        )

    def to_dict(self) -> dict[str, Any]:
        result = {
            "gmFileName": self.gm_file_name,
            "colourDirectory": self.colour_directory,
            "maskMode": self.mask_mode,
            "maskDirectory": self.mask_directory,
            "sourcePrefix": self.source_prefix,
            "pivotMode": self.pivot_mode,
            "missingTargetPolicy": self.missing_target_policy,
            "missingSourceMetadataPolicy": self.missing_source_metadata_policy,
        }
        if self.pivot_mode == "source-metadata" and self.source_metadata_directory:
            result["sourceMetadataDirectory"] = self.source_metadata_directory
        if self.source_frame_filter.strip():
            result["sourceFrameFilter"] = self.source_frame_filter.strip()
        return result


@dataclass
class ModInfoConfig:
    guid: str = "MyAtlasMod"
    author: str = ""
    name: str = "My Atlas Mod"
    description: str = "Sprite atlas replacements for SHCDE."
    version: str = "1.0.0"
    website: str = ""

    @classmethod
    def from_dict(cls, data: dict[str, Any] | None) -> "ModInfoConfig":
        data = data or {}
        return cls(
            guid=str(data.get("GUID", "MyAtlasMod")),
            author=str(data.get("Author", "")),
            name=str(data.get("Name", "My Atlas Mod")),
            description=str(data.get("Description", "Sprite atlas replacements for SHCDE.")),
            version=str(data.get("Version", "1.0.0")),
            website=str(data.get("Website", "")),
        )

    def to_info_dict(self) -> dict[str, Any]:
        return {
            "GUID": self.guid,
            "Author": self.author,
            "Name": self.name,
            "Description": self.description,
            "Version": self.version,
            "Website": self.website,
            "Manifest": 0,
            "NetworkMode": 0,
        }


@dataclass
class ProjectConfig:
    language: str = "de"
    target_game_data: str = ""
    output_mod_directory: str = ""
    groups: list[GroupConfig] = field(default_factory=list)
    packing: PackingConfig = field(default_factory=PackingConfig)
    create_manifest_if_missing: bool = True
    mod_info: ModInfoConfig = field(default_factory=ModInfoConfig)
    project_path: Path | None = field(default=None, repr=False)
    loaded_schema_version: int = field(default=SCHEMA_VERSION, repr=False)

    @classmethod
    def load(cls, path: Path) -> "ProjectConfig":
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        version = data.get("schemaVersion")
        if version not in SUPPORTED_SCHEMA_VERSIONS:
            raise ValueError(f"Unsupported project schemaVersion: {version!r}")
        project = cls(
            language=str(data.get("language", "de")),
            target_game_data=str(data.get("targetGameData", "")),
            output_mod_directory=str(data.get("outputModDirectory", "")),
            groups=[GroupConfig.from_dict(item, version) for item in data.get("groups", [])],
            packing=PackingConfig.from_dict(data.get("packing")),
            create_manifest_if_missing=bool(data.get("createManifestIfMissing", True)),
            mod_info=ModInfoConfig.from_dict(data.get("modInfo")),
            project_path=path.resolve(),
            loaded_schema_version=version,
        )
        return project

    def to_dict(self, project_path: Path) -> dict[str, Any]:
        base = project_path.resolve().parent

        def portable(raw: str) -> str:
            if not raw:
                return ""
            path = self.resolve_path(raw)
            try:
                return str(path.relative_to(base))
            except ValueError:
                return str(path)

        groups = []
        for group in self.groups:
            item = group.to_dict()
            item["colourDirectory"] = portable(group.colour_directory)
            if group.mask_directory:
                item["maskDirectory"] = portable(group.mask_directory)
            if group.pivot_mode == "source-metadata" and group.source_metadata_directory:
                item["sourceMetadataDirectory"] = portable(group.source_metadata_directory)
            groups.append(item)
        return {
            "schemaVersion": SCHEMA_VERSION,
            "language": self.language,
            "targetGameData": portable(self.target_game_data),
            "outputModDirectory": portable(self.output_mod_directory),
            "groups": groups,
            "packing": self.packing.to_dict(),
            "createManifestIfMissing": self.create_manifest_if_missing,
            "modInfo": self.mod_info.to_info_dict(),
        }

    def save(self, path: Path) -> None:
        text = json.dumps(self.to_dict(path), ensure_ascii=False, indent=2) + "\n"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(text.replace("\n", "\r\n").encode("utf-8"))
        self.project_path = path.resolve()
        self.loaded_schema_version = SCHEMA_VERSION

    def resolve_path(self, raw: str) -> Path:
        path = Path(raw).expanduser()
        if path.is_absolute():
            return path.resolve()
        base = self.project_path.parent if self.project_path else Path.cwd()
        return (base / path).resolve()

