from __future__ import annotations

import json
from importlib.resources import files


def load_supported_groups() -> dict[str, bool]:
    resource = files("atlas_builder.assets").joinpath("supported_gm_groups.json")
    data = json.loads(resource.read_text(encoding="utf-8"))
    return {str(item["name"]): bool(item["dashFormat"]) for item in data["groups"]}


SUPPORTED_GROUPS = load_supported_groups()

