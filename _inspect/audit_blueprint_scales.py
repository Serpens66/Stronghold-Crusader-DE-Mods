from pathlib import Path
from collections import Counter
import json
import re


ROOT = Path(__file__).resolve().parents[1]
AIV_CATALOG = ROOT / "AIVParser/AIVParser.Core/AivCatalogs.cs"
ICON_CATALOG = ROOT / "CastlePlanner/src/BlueprintBuildingIconCatalog.cs"
CALIBRATIONS = Path(
    r"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
    r"\BepInEx\config\CastlePlanner_Serp.BlueprintBuildingSizes.tsv"
)
AIV_ROOTS = (
    Path(
        r"C:\Users\Serpens66\AppData\LocalLow\Firefly Studios"
        r"\Stronghold Crusader Definitive Edition\CustomLords"
    ),
    Path(
        r"C:\Users\Serpens66\AppData\LocalLow\Firefly Studios"
        r"\Stronghold Crusader Definitive Edition\ExtendedLords"
    ),
)


def block(text: str, start: str, end: str) -> str:
    return text.split(start, 1)[1].split(end, 1)[0]


def main() -> None:
    aiv_text = AIV_CATALOG.read_text(encoding="utf-8-sig")
    icon_text = ICON_CATALOG.read_text(encoding="utf-8-sig")

    add_pattern = re.compile(
        r'Add\(result,\s*(\d+),\s*"([^"]+)"'
        r'(?:,\s*AivItemCategory\.([A-Za-z]+))?\);'
    )
    mappers = [
        (int(value), name, category or "Building")
        for value, name, category in add_pattern.findall(aiv_text)
    ]
    buildings = [entry for entry in mappers if entry[2] == "Building"]
    keeps = [entry for entry in mappers if entry[2] == "Keep"]

    reserved_text = block(
        icon_text,
        "ReservedAreaVisibleWorldWidths =",
        "private static readonly IReadOnlyDictionary<string, float>\n            NormalScaleOverrides",
    )
    fixed = set(re.findall(r'\["([^"]+)"\]\s*=\s*', reserved_text))

    resources_text = block(
        icon_text,
        "ResourceKeys =",
        "private static readonly IReadOnlyDictionary<string, string>\n            IslamicResourceKeys",
    )
    icons = set(re.findall(r'\["([^"]+)"\]\s*=\s*', resources_text))

    measurements = {}
    for line in CALIBRATIONS.read_text(encoding="utf-8-sig").splitlines():
        if not line or line.startswith("#"):
            continue
        parts = line.split("\t")
        revision = int(parts[6]) if len(parts) >= 7 else 1
        measurements[parts[1]] = revision

    print(f"AIV buildings={len(buildings)}, keeps={len(keeps)}")
    counts = {"fixed": 0, "measured": 0, "missing": 0}
    for value, name, _ in buildings:
        if name in fixed:
            source = "fixed"
        elif measurements.get(name, 0) >= 4:
            source = "measured"
        else:
            source = "missing"
        counts[source] += 1
        print(
            f"{value:3} {name:34} scale={source:8} "
            f"icon={'yes' if name in icons else 'NO'} "
            f"revision={measurements.get(name, '-')}"
        )
    print("counts", counts)
    print("keeps intentionally skipped", ", ".join(name for _, name, _ in keeps))

    outposts = (
        (53, "MAPPER_OUTPOST_BEDOUIN"),
        (178, "MAPPER_OUTPOST"),
        (179, "MAPPER_OUTPOST_ARAB"),
    )
    for value, name in outposts:
        print(
            f"OUTPOST {value:3} {name:34} "
            f"aiv={'yes' if any(entry[1] == name for entry in mappers) else 'NO'} "
            f"fixed={'yes' if name in fixed else 'NO'} "
            f"measured={'yes' if measurements.get(name, 0) >= 4 else 'NO'} "
            f"icon={'yes' if name in icons else 'NO'}"
        )

    mapper_by_value = {value: (name, category) for value, name, category in mappers}
    used_values = Counter()
    parsed_files = 0
    failed_files = []
    for root in AIV_ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*.aivjson"):
            try:
                document = json.loads(path.read_text(encoding="utf-8-sig"))
                for frame in document.get("frames") or []:
                    used_values[int(frame["itemType"])] += len(
                        frame.get("tilePositionOfsets") or []
                    )
                parsed_files += 1
            except Exception as error:
                failed_files.append((path, str(error)))

    print(
        f"installed AIVJSON files={parsed_files}, failed={len(failed_files)}, "
        f"distinct mapper values={len(used_values)}"
    )
    for value in sorted(used_values):
        name, category = mapper_by_value.get(
            value, (f"UNKNOWN_MAPPER_{value}", "Unknown")
        )
        if category not in ("Building", "Keep", "Unknown"):
            continue
        covered = category == "Keep" or name in fixed or measurements.get(name, 0) >= 4
        print(
            f"USED {value:3} {name:34} category={category:8} "
            f"placements={used_values[value]:5} scale={'yes' if covered else 'NO'}"
        )
    for path, error in failed_files:
        print(f"FAILED {path}: {error}")


if __name__ == "__main__":
    main()
