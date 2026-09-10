from __future__ import annotations

import json
from dataclasses import dataclass
from importlib.resources import files


@dataclass(frozen=True)
class GroupContract:
    name: str
    dash_format: bool
    maximum_frame_index: int
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
        maximum_frame_index = int(item["declaredImageCount"])
        if name in contracts:
            raise ValueError(f"Duplicate GM group in bundled contract: {name}")
        if material not in {"plain", "teamcolour", "foliage"}:
            raise ValueError(f"Invalid material in bundled contract for {name}: {material}")
        if mask_policy not in {"forbidden", "required"}:
            raise ValueError(f"Invalid mask policy in bundled contract for {name}: {mask_policy}")
        if maximum_frame_index < 0:
            raise ValueError(f"Invalid maximum frame index in bundled contract for {name}: {maximum_frame_index}")
        contracts[name] = GroupContract(
            name=name,
            dash_format=bool(item["dashFormat"]),
            # spriteLoader treats declaredImageCount as the inclusive maximum
            # index and allocates maximum + 1 entries.
            maximum_frame_index=maximum_frame_index,
            overridable_as_atlas=bool(item["overridableAsAtlas"]),
            material=material,
            mask_policy=mask_policy,
        )
    return contracts


GROUP_CONTRACTS = load_group_contracts()
SUPPORTED_GROUPS = {name: contract.dash_format for name, contract in GROUP_CONTRACTS.items()}


def atlas_material_name(contract: GroupContract) -> str:
    """Return the exact root-level material value accepted by Extender 2.4.0."""
    return {
        "plain": "Plain",
        "teamcolour": "TeamColour",
        "foliage": "Foliage",
    }[contract.material]

