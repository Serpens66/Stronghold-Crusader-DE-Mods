from __future__ import annotations

import json
from dataclasses import dataclass
from importlib.resources import files


@dataclass(frozen=True)
class GroupContract:
    name: str
    dash_format: bool
    overridable_as_atlas: bool
    material: str
    mask_policy: str


def load_group_contracts() -> dict[str, GroupContract]:
    resource = files("atlas_builder.assets").joinpath("supported_gm_groups.json")
    data = json.loads(resource.read_text(encoding="utf-8"))
    contracts: dict[str, GroupContract] = {}
    for item in data["groups"]:
        name = str(item["name"])
        material = str(item["material"])
        mask_policy = str(item["maskPolicy"])
        if name in contracts:
            raise ValueError(f"Duplicate GM group in bundled contract: {name}")
        if material not in {"plain", "teamcolour", "foliage"}:
            raise ValueError(f"Invalid material in bundled contract for {name}: {material}")
        if mask_policy not in {"forbidden", "required"}:
            raise ValueError(f"Invalid mask policy in bundled contract for {name}: {mask_policy}")
        contracts[name] = GroupContract(
            name=name,
            dash_format=bool(item["dashFormat"]),
            overridable_as_atlas=bool(item["overridableAsAtlas"]),
            material=material,
            mask_policy=mask_policy,
        )
    return contracts


GROUP_CONTRACTS = load_group_contracts()
SUPPORTED_GROUPS = {name: contract.dash_format for name, contract in GROUP_CONTRACTS.items()}

