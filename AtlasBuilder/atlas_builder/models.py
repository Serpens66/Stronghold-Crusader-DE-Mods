from __future__ import annotations

import json
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any


SCHEMA_VERSION = 1
MASK_MODES = {"none", "same-directory", "separate-directory"}


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

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "GroupConfig":
        return cls(
            gm_file_name=str(data["gmFileName"]),
            colour_directory=str(data["colourDirectory"]),
            mask_mode=str(data.get("maskMode", "none")),
            mask_directory=data.get("maskDirectory"),
            source_prefix=str(data.get("sourcePrefix", "auto")),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "gmFileName": self.gm_file_name,
            "colourDirectory": self.colour_directory,
            "maskMode": self.mask_mode,
            "maskDirectory": self.mask_directory,
            "sourcePrefix": self.source_prefix,
        }


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

    @classmethod
    def load(cls, path: Path) -> "ProjectConfig":
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        version = data.get("schemaVersion")
        if version != SCHEMA_VERSION:
            raise ValueError(f"Unsupported project schemaVersion: {version!r}")
        project = cls(
            language=str(data.get("language", "de")),
            target_game_data=str(data.get("targetGameData", "")),
            output_mod_directory=str(data.get("outputModDirectory", "")),
            groups=[GroupConfig.from_dict(item) for item in data.get("groups", [])],
            packing=PackingConfig.from_dict(data.get("packing")),
            create_manifest_if_missing=bool(data.get("createManifestIfMissing", True)),
            mod_info=ModInfoConfig.from_dict(data.get("modInfo")),
            project_path=path.resolve(),
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

    def resolve_path(self, raw: str) -> Path:
        path = Path(raw).expanduser()
        if path.is_absolute():
            return path.resolve()
        base = self.project_path.parent if self.project_path else Path.cwd()
        return (base / path).resolve()

